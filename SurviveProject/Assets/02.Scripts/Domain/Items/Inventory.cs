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
        /// 기존 스택을 먼저 채우고, 남으면 빈 슬롯을 쓴다.
        /// </summary>
        /// <returns>넣지 못하고 남은 개수. 0이면 전부 들어갔다.</returns>
        public int TryAdd(ItemDataSO item, int count)
        {
            if (item == null || count <= 0) return count > 0 ? count : 0;

            int 남은수 = count;

            // 1단계 — 기존 스택 채우기
            for (int i = 0; i < _slots.Length && 남은수 > 0; i++)
            {
                var slot = _slots[i];
                if (slot.IsEmpty || slot.item != item) continue;

                int 넣을수 = Mathf.Min(slot.RemainingSpace, 남은수);
                slot.count += 넣을수;
                남은수 -= 넣을수;
            }

            // 2단계 — 빈 슬롯 쓰기
            for (int i = 0; i < _slots.Length && 남은수 > 0; i++)
            {
                var slot = _slots[i];
                if (!slot.IsEmpty) continue;

                int 넣을수 = Mathf.Min(item.maxStack, 남은수);
                slot.item = item;
                slot.count = 넣을수;
                남은수 -= 넣을수;
            }

            if (남은수 != count) Changed?.Invoke();
            return 남은수;
        }

        /// <summary>수량이 모자라면 아무것도 건드리지 않고 false를 돌려준다.</summary>
        public bool TryRemove(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return false;
            if (!Has(itemId, count)) return false;

            int 남은수 = count;
            for (int i = 0; i < _slots.Length && 남은수 > 0; i++)
            {
                var slot = _slots[i];
                if (slot.IsEmpty || slot.item.id != itemId) continue;

                int 뺄수 = Mathf.Min(slot.count, 남은수);
                slot.count -= 뺄수;
                남은수 -= 뺄수;
                if (slot.count <= 0) slot.Clear();
            }

            Changed?.Invoke();
            return true;
        }

        public int CountOf(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            int 합 = 0;
            foreach (var slot in _slots)
                if (!slot.IsEmpty && slot.item.id == itemId) 합 += slot.count;
            return 합;
        }

        public bool Has(string itemId, int count) => CountOf(itemId) >= count;

        /// <summary>
        /// 같은 아이템이면 병합(초과분은 출발 슬롯에 남음), 다르면 교환, 빈 곳이면 이동.
        /// </summary>
        public void MoveOrSwap(int fromSlot, int toSlot)
        {
            if (!유효한슬롯(fromSlot) || !유효한슬롯(toSlot) || fromSlot == toSlot) return;

            var from = _slots[fromSlot];
            var to = _slots[toSlot];
            if (from.IsEmpty) return;

            if (!to.IsEmpty && to.item == from.item)
            {
                int 옮길수 = Mathf.Min(to.RemainingSpace, from.count);
                if (옮길수 <= 0) return;

                to.count += 옮길수;
                from.count -= 옮길수;
                if (from.count <= 0) from.Clear();
            }
            else
            {
                var 임시아이템 = to.item;
                var 임시개수 = to.count;
                to.item = from.item;
                to.count = from.count;
                from.item = 임시아이템;
                from.count = 임시개수;
            }

            Changed?.Invoke();
        }

        bool 유효한슬롯(int index) => index >= 0 && index < _slots.Length;
    }
}
