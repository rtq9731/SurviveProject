using System.Collections.Generic;

namespace Survive.Items
{
    /// <summary>
    /// 죽었을 때 무엇을 떨구고 무엇을 남기는가, 그리고 회수할 때 무엇이 돌아오는가.
    ///
    /// P2 스펙 §6의 마지막 조각이다 — "못 돌아가면 잃는 것이 있다".
    /// 잃을 것이 없으면 "더 갈까 돌아갈까"가 성립하지 않는다.
    ///
    /// <b>판정은 여기 있고 MonoBehaviour에는 없다.</b> 같은 스펙 §4 "판정은 Domain에"를
    /// 따른다 — 무엇을 잃는가는 게임 규칙이지 씬의 사정이 아니다.
    ///
    /// <b>떨구는 것은 이번 왕복에서 벌어온 것뿐이다.</b> 자원과 소비품은 떨구고,
    /// 도구(<see cref="ItemCategory.Tool"/>)와 진행 아이템(<see cref="ItemCategory.Quest"/>)은 남긴다.
    /// 이유는 스펙이 도구에 준 위치 때문이다 — P2 §3에서 <b>장비가 곧 진행</b>이고
    /// "진행은 한 방향"이다. 죽을 때마다 랜턴과 액면 보행 장비를 잃으면
    /// 사망이 티어를 되돌리게 되고, 그것은 스펙이 명시적으로 배제한 구조다.
    ///
    /// 실전에서도 같은 결론이 나온다. 어둠은 이 게임의 첫 관문이고 랜턴이 그것을 뚫는다
    /// (스펙 §4). 죽으면서 랜턴을 같이 떨구면 회수하러 갈 수단 자체를 잃는다 —
    /// 그건 압박이 아니라 막다른 길이다. 회수는 갈 수 있어야 압박이 된다.
    /// </summary>
    public static class DeathDrop
    {
        /// <summary>이 분류는 죽을 때 떨구는가.</summary>
        public static bool DropsOnDeath(ItemCategory category) =>
            category == ItemCategory.Resource || category == ItemCategory.Consumable;

        /// <summary>이 아이템은 죽을 때 떨구는가. 정의가 없으면 떨구지 않는다.</summary>
        public static bool DropsOnDeath(ItemDataSO item) =>
            item != null && DropsOnDeath(item.category);

        /// <summary>
        /// 사망 시 떨굴 것을 인벤토리에서 떼어낸다. 남기는 것은 건드리지 않는다.
        ///
        /// 슬롯 단위로 떼어내는 이유: 같은 아이템이 여러 슬롯에 나뉘어 있으면
        /// 가방에서도 같은 모양으로 놓여야 회수 후 원래 배치에 가깝게 돌아온다.
        /// </summary>
        /// <returns>가방에 담을 목록. 인벤토리의 슬롯과는 다른 사본이다.</returns>
        public static List<ItemStack> Extract(Inventory inventory)
        {
            var taken = new List<ItemStack>();
            if (inventory == null) return taken;

            foreach (var slot in inventory.Slots)
            {
                if (slot == null || slot.IsEmpty) continue;
                if (!DropsOnDeath(slot.item)) continue;
                taken.Add(new ItemStack(slot.item, slot.count));
            }

            // 슬롯을 직접 비우지 않고 TryRemove로 뺀다. 슬롯을 손으로 Clear하면
            // Inventory.Changed가 울리지 않아 소지품 화면이 죽기 전 상태로 남는다.
            foreach (var stack in taken)
                inventory.TryRemove(stack.item.id, stack.count);

            return taken;
        }

        /// <summary>떼어낸 것을 가방에 담는다.</summary>
        /// <returns>가방이 좁아 담지 못한 개수의 합. 0이면 전부 들어갔다.</returns>
        public static int Fill(Inventory bag, IReadOnlyList<ItemStack> stacks)
        {
            if (bag == null || stacks == null) return 0;

            int leftover = 0;
            for (int i = 0; i < stacks.Count; i++)
            {
                var stack = stacks[i];
                if (stack == null || stack.IsEmpty) continue;
                leftover += bag.TryAdd(stack.item, stack.count);
            }
            return leftover;
        }

        /// <summary>
        /// 가방을 인벤토리로 옮긴다. 들어가는 만큼만 옮기고 나머지는 가방에 남는다 —
        /// 죽은 뒤에 다른 것을 주워 자리가 없을 수 있고, 그때 남은 것이 증발하면
        /// 회수하러 온 걸음이 헛수고가 된다.
        /// </summary>
        /// <returns>실제로 옮긴 개수의 합.</returns>
        public static int Retrieve(Inventory bag, Inventory into)
        {
            if (bag == null || into == null) return 0;

            // 옮기면서 가방을 고치므로, 무엇을 옮길지는 먼저 확정해 둔다.
            var pending = new List<ItemStack>();
            foreach (var slot in bag.Slots)
            {
                if (slot == null || slot.IsEmpty) continue;
                pending.Add(new ItemStack(slot.item, slot.count));
            }

            int moved = 0;
            for (int i = 0; i < pending.Count; i++)
            {
                var stack = pending[i];
                int left = into.TryAdd(stack.item, stack.count);
                int taken = stack.count - left;
                if (taken <= 0) continue;

                bag.TryRemove(stack.item.id, taken);
                moved += taken;
            }
            return moved;
        }

        /// <summary>가방에 남은 것이 있는가.</summary>
        public static bool HasAnything(Inventory bag)
        {
            if (bag == null) return false;
            foreach (var slot in bag.Slots)
                if (slot != null && !slot.IsEmpty) return true;
            return false;
        }
    }
}
