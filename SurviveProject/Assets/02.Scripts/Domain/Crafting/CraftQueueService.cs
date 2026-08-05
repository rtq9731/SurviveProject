using UnityEngine;
using Survive.Items;

namespace Survive.Crafting
{
    /// <summary>
    /// 대기열의 생애 규칙 — 걸고, 진행하고, 취소한다.
    ///
    /// 손 제작·제작대·화톳불 가공이 전부 이 하나를 쓴다. 스테이션마다
    /// 제작 규칙을 따로 쓰면 나중에 화로를 붙일 때 세 번째 사본이 생기고,
    /// 환급 규칙이 스테이션마다 다른 게임이 된다.
    ///
    /// 순수 정적이라 Unity 실행 없이 테스트한다.
    /// </summary>
    public static class CraftQueueService
    {
        /// <summary>한 항목에 걸 수 있는 최대 수량. "최대" 버튼의 상한이기도 하다.</summary>
        public const int MaxBatch = 100;

        /// <summary>
        /// 한 번의 Tick에서 완성시킬 수 있는 최대 개수.
        /// 소요 시간이 0인 레시피가 섞여도 프레임이 멈추지 않게 막는다.
        /// </summary>
        public const int MaxUnitsPerTick = 64;

        /// <summary>
        /// 지금 가진 재료로 이 레시피를 몇 개까지 걸 수 있는가.
        /// 재료가 없는 레시피는 <see cref="MaxBatch"/>까지.
        /// </summary>
        public static int MaxCraftable(RecipeSO recipe, Inventory inventory, StationType available)
        {
            if (recipe == null || inventory == null) return 0;
            if (recipe.result == null || recipe.result.item == null) return 0;
            if (!CraftingService.CanCraft(recipe, inventory, available)) return 0;

            int best = MaxBatch;
            if (recipe.ingredients != null)
            {
                foreach (var need in recipe.ingredients)
                {
                    if (need?.item == null || need.count <= 0) continue;
                    int possible = inventory.CountOf(need.item.id) / need.count;
                    if (possible < best) best = possible;
                }
            }
            return Mathf.Clamp(best, 0, MaxBatch);
        }

        /// <summary>
        /// 재료를 지금 빼고 줄 끝에 건다.
        ///
        /// 실패하면 아무것도 건드리지 않는다 — 반만 빠진 재료가 남으면
        /// 사용자는 자기가 무엇을 잃었는지 알 수 없다.
        /// </summary>
        public static bool TryEnqueue(CraftQueue queue, RecipeSO recipe, int count,
                                      Inventory source, StationType available)
        {
            if (queue == null || source == null || count <= 0) return false;
            if (queue.IsFull) return false;
            if (count > MaxCraftable(recipe, source, available)) return false;

            if (recipe.ingredients != null)
            {
                foreach (var need in recipe.ingredients)
                {
                    if (need?.item == null || need.count <= 0) continue;
                    source.TryRemove(need.item.id, need.count * count);
                }
            }

            queue.Add(new CraftJob(recipe, count, recipe.craftSeconds));
            queue.RaiseChanged();
            return true;
        }

        /// <summary>
        /// 시간을 흘린다. 맨 앞 항목만 진행한다.
        /// </summary>
        /// <param name="output">완성품을 받을 곳. 손 제작은 플레이어 소지품, 스테이션은 회수함.</param>
        /// <param name="powered">진행 조건. 화톳불은 불이 타고 있어야 true다.</param>
        /// <returns>이번에 완성된 개수.</returns>
        public static int Tick(CraftQueue queue, float deltaSeconds, Inventory output, bool powered)
        {
            if (queue == null || output == null) return 0;
            if (!powered || deltaSeconds <= 0f) return 0;

            var job = queue.Active;
            if (job == null || job.IsDone) return 0;

            job.Elapsed += deltaSeconds;

            int produced = 0;
            while (produced < MaxUnitsPerTick && job.Remaining > 0 &&
                   job.Elapsed >= job.UnitSeconds)
            {
                if (!TryDeposit(output, job.Recipe))
                {
                    // 넣을 자리가 없다. 완성된 채로 기다린다 — 버리지 않는다.
                    job.Stalled = true;
                    job.Elapsed = job.UnitSeconds;
                    break;
                }

                job.Stalled = false;
                job.Remaining--;
                produced++;

                if (job.UnitSeconds <= 0f) job.Elapsed = 0f;
                else job.Elapsed -= job.UnitSeconds;

                if (job.Remaining <= 0)
                {
                    queue.RemoveAt(0);
                    break;
                }
            }

            if (produced > 0) queue.RaiseChanged();
            return produced;
        }

        /// <summary>
        /// 한 항목을 통째로 물린다. <b>남은 개수 전부</b>를 환급한다 —
        /// 진행 중이던 한 개도 포함해서.
        ///
        /// 반쯤 만든 것의 재료만 태우는 규칙도 생각할 수 있지만, 그러려면
        /// "어디까지가 착수인가"를 어딘가에 또 적어 둬야 하고 UI도 그것을
        /// 설명해야 한다. 완성되지 않은 것은 전부 돌려준다 — 규칙이 하나다.
        /// 취소로 이득을 볼 수는 없으므로 악용 여지도 없다.
        /// </summary>
        public static bool TryCancel(CraftQueue queue, int index, Inventory refundTo)
        {
            if (queue == null) return false;
            var job = queue.At(index);
            if (job == null) return false;

            Refund(job, refundTo);
            queue.RemoveAt(index);
            queue.RaiseChanged();
            return true;
        }

        /// <summary>줄 전체를 물린다. 돌려준 항목 수.</summary>
        public static int CancelAll(CraftQueue queue, Inventory refundTo)
        {
            if (queue == null || queue.IsEmpty) return 0;

            int n = queue.Count;
            for (int i = 0; i < n; i++) Refund(queue.At(i), refundTo);
            queue.Clear();
            queue.RaiseChanged();
            return n;
        }

        /// <summary>줄 전체가 끝날 때까지 남은 시간(초).</summary>
        public static float TotalSecondsLeft(CraftQueue queue)
        {
            if (queue == null) return 0f;
            float sum = 0f;
            foreach (var job in queue.Jobs) sum += job.SecondsLeft;
            return sum;
        }

        static void Refund(CraftJob job, Inventory refundTo)
        {
            if (job == null || refundTo == null) return;
            if (job.Recipe?.ingredients == null || job.Remaining <= 0) return;

            foreach (var need in job.Recipe.ingredients)
            {
                if (need?.item == null || need.count <= 0) continue;
                refundTo.TryAdd(need.item, need.count * job.Remaining);
            }
        }

        /// <summary>
        /// 결과 하나를 넣는다. 전부 들어가지 못하면 넣은 만큼 되돌리고 실패로 친다 —
        /// <see cref="CraftingService.Craft"/>가 쓰는 것과 같은 규칙이다.
        /// </summary>
        static bool TryDeposit(Inventory output, RecipeSO recipe)
        {
            if (recipe?.result?.item == null) return false;

            int count = Mathf.Max(1, recipe.result.count);
            int leftover = output.TryAdd(recipe.result.item, count);
            if (leftover <= 0) return true;

            if (leftover < count)
                output.TryRemove(recipe.result.item.id, count - leftover);
            return false;
        }
    }
}
