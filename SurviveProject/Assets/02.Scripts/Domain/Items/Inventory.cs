using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survive.Items
{
    /// <summary>
    /// 고정 슬롯 인벤토리. MonoBehaviour가 아니므로 Unity 실행 없이 테스트할 수 있다.
    /// </summary>
    public class Inventory
    {
        readonly ItemStack[] _slots;

        public Inventory(int slotCount)
        {
            if (slotCount <= 0) throw new ArgumentOutOfRangeException(nameof(slotCount));
            _slots = new ItemStack[slotCount];
            for (int i = 0; i < slotCount; i++) _slots[i] = new ItemStack();
        }

        public int SlotCount => _slots.Length;
        public IReadOnlyList<ItemStack> Slots => _slots;

        public event Action Changed;

        /// <summary>
        /// 방금 <b>실제로 들어간</b> 아이템과 개수. 자리가 없어 하나도 못 들어가면
        /// 울리지 않는다.
        ///
        /// <see cref="Changed"/>가 있는데 하나 더 두는 이유: 저쪽은 "무언가 바뀌었다"만
        /// 말한다. 현장 발견(첫 습득)은 <b>무엇이</b> 들어왔는지를 알아야 한다.
        /// 여기가 게임의 모든 획득 경로가 지나는 유일한 길목이라, 줍기·채집·제작
        /// 완성·보관함 인출을 한 자리에서 듣는다.
        /// </summary>
        public event Action<ItemDataSO, int> ItemAdded;

        /// <summary>
        /// 기존 스택을 먼저 채우고, 남으면 빈 슬롯을 쓴다.
        /// </summary>
        /// <returns>넣지 못하고 남은 개수. 0이면 전부 들어갔다.</returns>
        public int TryAdd(ItemDataSO item, int count)
        {
            if (item == null || count <= 0) return count > 0 ? count : 0;

            int remaining = count;

            // 1단계 — 기존 스택 채우기
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                var slot = _slots[i];
                if (slot.IsEmpty || slot.item != item) continue;

                int toAdd = Mathf.Min(slot.RemainingSpace, remaining);
                slot.count += toAdd;
                remaining -= toAdd;
            }

            // 2단계 — 빈 슬롯 쓰기
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                var slot = _slots[i];
                if (!slot.IsEmpty) continue;

                int toAdd = Mathf.Min(item.maxStack, remaining);
                slot.item = item;
                slot.count = toAdd;
                remaining -= toAdd;
            }

            if (remaining != count)
            {
                // ItemAdded가 먼저다. 첫 습득으로 청사진이 열리는 일이 여기서
                // 일어나므로, 그다음에 Changed를 듣는 화면들은 이미 열린 원장을 본다.
                ItemAdded?.Invoke(item, count - remaining);
                Changed?.Invoke();
            }
            return remaining;
        }

        /// <summary>수량이 모자라면 아무것도 건드리지 않고 false를 돌려준다.</summary>
        public bool TryRemove(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return false;
            if (!Has(itemId, count)) return false;

            int remaining = count;
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                var slot = _slots[i];
                if (slot.IsEmpty || slot.item.id != itemId) continue;

                int toRemove = Mathf.Min(slot.count, remaining);
                slot.count -= toRemove;
                remaining -= toRemove;
                if (slot.count <= 0) slot.Clear();
            }

            Changed?.Invoke();
            return true;
        }

        public int CountOf(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            int sum = 0;
            foreach (var slot in _slots)
                if (!slot.IsEmpty && slot.item.id == itemId) sum += slot.count;
            return sum;
        }

        public bool Has(string itemId, int count) => CountOf(itemId) >= count;

        /// <summary>
        /// 같은 아이템이면 병합(초과분은 출발 슬롯에 남음), 다르면 교환, 빈 곳이면 이동.
        /// </summary>
        public void MoveOrSwap(int fromSlot, int toSlot)
        {
            if (!IsValidSlot(fromSlot) || !IsValidSlot(toSlot) || fromSlot == toSlot) return;

            var from = _slots[fromSlot];
            var to = _slots[toSlot];
            if (from.IsEmpty) return;

            if (!to.IsEmpty && to.item == from.item)
            {
                int moved = Mathf.Min(to.RemainingSpace, from.count);
                if (moved <= 0) return;

                to.count += moved;
                from.count -= moved;
                if (from.count <= 0) from.Clear();
            }
            else
            {
                var tmpItem = to.item;
                var tmpCount = to.count;
                to.item = from.item;
                to.count = from.count;
                from.item = tmpItem;
                from.count = tmpCount;
            }

            Changed?.Invoke();
        }

        bool IsValidSlot(int index) => index >= 0 && index < _slots.Length;
    }
}
