using System;
using System.Collections.Generic;
using UnityEngine;
using Survive.Items;
using Survive.World;

namespace Survive.Crafting
{
    /// <summary>
    /// 저장본에 실리는 <b>대기열 한 벌</b>.
    ///
    /// 몸이 <b>생성 목록에 없는</b> 대기열이 쓴다 — 씬이 놓은 제작대와 손 제작이다.
    /// 세운 스테이션은 이 절을 쓰지 않는다. 그쪽은 몸을 싣는 줄이 함께 싣는다
    /// (<see cref="Survive.World.SpawnRecord"/>).
    ///
    /// <b>손 제작은 회수함이 비어 있다.</b> 자기 손에서 만든 것은 곧바로 소지품으로
    /// 들어가서 회수할 자리가 없다. 그렇다고 갈래를 하나 더 두면 「걸어 둔 제작」을
    /// 옮기는 규칙이 두 벌이 된다 — 빈 목록 하나가 그보다 싸다.
    /// </summary>
    [Serializable]
    public class StationSaveState
    {
        public List<SpawnStack> output = new List<SpawnStack>();
        public List<SpawnJobRow> queued = new List<SpawnJobRow>();
    }

    /// <summary>
    /// <b>대기열과 그릇을 저장본의 모양으로 옮기고 되돌린다.</b>
    ///
    /// 이 일이 일어나는 자리가 셋이다 — 세운 스테이션(생성 목록의 줄이 싣는다),
    /// 씬에 놓인 스테이션(제 몸이 싣는다), 손 제작(사람이 싣는다). 셋의
    /// <b>주인은 다르지만 옮기는 규칙은 같아야</b> 한다. 규칙을 세 벌 쓰면
    /// 「손으로 걸면 이어지는데 제작대에 걸면 사라지는」 게임이 된다.
    ///
    /// Unity 없이 검증된다. 표를 찾는 일은 부르는 쪽이 손으로 건넨다 —
    /// 그래야 이 규칙이 <c>Resources</c>도 씬도 모르는 채로 선다.
    /// </summary>
    public static class StationSaveRule
    {
        // ── 적는다 ───────────────────────────────────────────────

        /// <summary>
        /// 그릇에 든 것을 무더기 목록으로. <b>빈 칸은 안 적는다</b> —
        /// 18칸짜리 보관함에 하나만 들어 있으면 저장본에도 한 줄이다.
        /// 칸 번호를 적지 않는 이유도 같다. 되돌릴 때 앞에서부터 채우면
        /// 사람 눈에는 같은 것이고, 칸 번호를 적으면 칸 수를 바꾸는 날
        /// 저장본이 범위 밖을 가리킨다.
        /// </summary>
        public static List<SpawnStack> Capture(Inventory inventory)
        {
            var rows = new List<SpawnStack>();
            if (inventory == null) return rows;

            foreach (var slot in inventory.Slots)
            {
                if (slot == null || slot.IsEmpty || slot.item == null) continue;
                rows.Add(new SpawnStack { id = slot.item.id, count = slot.count });
            }
            return rows;
        }

        /// <summary>
        /// 걸려 있는 제작을 줄 목록으로. <b>차례가 곧 순서</b>다 —
        /// 맨 앞이 지금 진행 중인 것이고, 그 순서가 사람이 정한 것이다.
        ///
        /// <c>Stalled</c>는 안 적는다. 그것은 「지금 넣을 자리가 없다」는 <b>판정</b>이지
        /// 상태가 아니고, 되살아난 스테이션의 첫 <c>Tick</c>이 그 자리에서 다시 판정한다.
        /// 적어 두면 오히려 자리가 생긴 뒤에도 멈춘 것으로 보일 수 있다.
        /// </summary>
        public static List<SpawnJobRow> Capture(CraftQueue queue)
        {
            var rows = new List<SpawnJobRow>();
            if (queue == null) return rows;

            foreach (var job in queue.Jobs)
            {
                if (job == null || job.Recipe == null) continue;
                if (string.IsNullOrEmpty(job.Recipe.id) || job.Remaining <= 0) continue;

                rows.Add(new SpawnJobRow
                {
                    recipe = job.Recipe.id,
                    remaining = job.Remaining,
                    elapsed = job.Elapsed,
                    unitSeconds = job.UnitSeconds,
                });
            }
            return rows;
        }

        // ── 되돌린다 ─────────────────────────────────────────────

        /// <summary>
        /// 무더기 목록을 그릇에 붓는다. <b>붓기 전에 비운다</b> —
        /// 안 비우면 같은 몸에 두 번 되돌릴 때 내용물이 불어난다.
        /// </summary>
        /// <param name="items">id에서 아이템 정의를 찾는 손.</param>
        /// <returns>실제로 들어간 개수.</returns>
        public static int AdoptInto(Inventory target, IList<SpawnStack> rows,
                                    Func<string, ItemDataSO> items)
        {
            if (target == null) return 0;

            비운다(target);
            if (rows == null || items == null) return 0;

            int 담긴것 = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || string.IsNullOrEmpty(row.id) || row.count <= 0) continue;

                var item = items(row.id);
                if (item == null)
                {
                    // 표에서 사라진 아이템이다. 조용히 넘기면 사람은 보관함이
                    // 물건을 먹었다고 본다.
                    Debug.LogWarning($"[StationSaveRule] 저장본이 가리키는 아이템을 " +
                                     $"표에서 찾지 못했다: {row.id}");
                    continue;
                }

                담긴것 += row.count - target.TryAdd(item, row.count);
            }
            return 담긴것;
        }

        /// <summary>
        /// 줄 목록을 대기열에 되돌린다. <b>재료를 다시 받지 않는다</b> —
        /// 걸 때 이미 냈다. <c>CraftQueueService.TryEnqueue</c>를 지나면 그것을
        /// 한 번 더 내게 되고, 저장할 때마다 재료가 사라진다.
        ///
        /// <b>이어 가지 돌려주지 않는다.</b> 취소는 재료를 되돌리지만 불러오기는
        /// 다르다. 취소는 사람이 「그만두겠다」고 말한 것이고 불러오기는
        /// 「이어서 하겠다」다. 되돌려 주면 스무 개를 걸어 놓고 나간 사람이
        /// 돌아왔을 때 스무 개가 아니라 재료 더미를 보게 되고, 그것은 규칙 §5.4가
        /// 약속한 「걸어두고 자리를 뜰 수 있다」가 아니다.
        /// </summary>
        /// <param name="recipes">id에서 레시피를 찾는 손.</param>
        /// <returns>실제로 되돌린 항목 수.</returns>
        public static int AdoptInto(CraftQueue queue, IList<SpawnJobRow> rows,
                                    Func<string, RecipeSO> recipes)
        {
            if (queue == null) return 0;

            queue.Clear();
            if (rows == null || recipes == null) { queue.RaiseChanged(); return 0; }

            int 되돌린것 = 0;
            for (int i = 0; i < rows.Count && !queue.IsFull; i++)
            {
                var row = rows[i];
                if (row == null || string.IsNullOrEmpty(row.recipe) || row.remaining <= 0) continue;

                var recipe = recipes(row.recipe);
                if (recipe == null)
                {
                    Debug.LogWarning($"[StationSaveRule] 저장본이 가리키는 레시피를 " +
                                     $"찾지 못했다: {row.recipe} — 걸어 둔 제작 하나가 " +
                                     $"사라진다(재료는 이미 빠진 뒤다)");
                    continue;
                }

                var job = new CraftJob(recipe, row.remaining, Mathf.Max(0f, row.unitSeconds));

                // 진행도는 한 개분을 넘을 수 없다. 넘은 값을 그대로 앉히면
                // 다음 Tick 한 번에 여러 개가 쏟아진다.
                job.Elapsed = Mathf.Clamp(row.elapsed, 0f, job.UnitSeconds);

                queue.Add(job);
                되돌린것++;
            }

            queue.RaiseChanged();
            return 되돌린것;
        }

        static void 비운다(Inventory inventory)
        {
            foreach (var slot in inventory.Slots)
                if (slot != null && !slot.IsEmpty) slot.Clear();
        }
    }
}
