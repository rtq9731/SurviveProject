using UnityEngine;
using Survive.Items;

namespace Survive.Crafting
{
    /// <summary>
    /// 스테이션에 귀속된 대기열 — 걸어 두고 떠날 수 있는 제작.
    ///
    /// 손 제작은 완성품이 곧바로 소지품으로 들어가면 되지만, 제작대와 화톳불은
    /// 사람이 자리에 없는 동안에도 돈다. 그래서 완성품이 갈 자리가 따로 있어야 한다 —
    /// 그것이 <see cref="Output"/>이고, 돌아와서 회수하는 것이 <see cref="CollectInto"/>다.
    ///
    /// MonoBehaviour가 아니다. 제작대와 화톳불이 각자 이것을 하나씩 들고 있으면
    /// 스테이션마다 같은 코드를 다시 쓰지 않아도 되고, 미래의 화로도 같은 모양이 된다.
    /// </summary>
    public class StationCraftQueue
    {
        /// <summary>회수함 칸 수. 며칠 걸어 둘 것이 아니므로 넉넉할 필요가 없다.</summary>
        public const int DefaultOutputSlots = 4;

        public StationCraftQueue(int capacity = CraftQueue.DefaultCapacity,
                                 int outputSlots = DefaultOutputSlots)
        {
            Queue = new CraftQueue(capacity);
            Output = new Inventory(Mathf.Max(1, outputSlots));
        }

        public CraftQueue Queue { get; }

        /// <summary>완성됐지만 아직 가져가지 않은 것.</summary>
        public Inventory Output { get; }

        public bool HasOutput
        {
            get
            {
                foreach (var slot in Output.Slots)
                    if (slot != null && !slot.IsEmpty) return true;
                return false;
            }
        }

        /// <summary>회수함에 쌓인 총 개수. 안내 문구가 읽는다.</summary>
        public int OutputCount
        {
            get
            {
                int sum = 0;
                foreach (var slot in Output.Slots)
                    if (slot != null && !slot.IsEmpty) sum += slot.count;
                return sum;
            }
        }

        public int Tick(float deltaSeconds, bool powered) =>
            CraftQueueService.Tick(Queue, deltaSeconds, Output, powered);

        /// <summary>
        /// 회수함을 비워 대상 소지품으로 옮긴다.
        /// 자리가 모자라면 들어간 만큼만 옮기고 나머지는 남겨 둔다.
        /// </summary>
        /// <returns>실제로 옮긴 개수.</returns>
        public int CollectInto(Inventory target)
        {
            if (target == null) return 0;

            int moved = 0;
            foreach (var slot in Output.Slots)
            {
                if (slot == null || slot.IsEmpty) continue;

                int leftover = target.TryAdd(slot.item, slot.count);
                int taken = slot.count - leftover;
                if (taken <= 0) continue;

                moved += taken;
                slot.count = leftover;
                if (slot.count <= 0) slot.Clear();
            }
            return moved;
        }
    }
}
