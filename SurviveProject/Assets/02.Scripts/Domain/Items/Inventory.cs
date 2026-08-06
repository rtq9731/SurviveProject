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
        readonly EquipmentSlots _equipment;

        /// <summary>
        /// 장비 슬롯을 이쪽에서 고치는 동안에는 저쪽의 Changed를 되받지 않는다.
        /// 안 그러면 한 번의 습득에 화면이 두 번 다시 그려지고, 무엇보다
        /// <see cref="ItemAdded"/>보다 <see cref="Changed"/>가 먼저 울려 버린다.
        /// </summary>
        bool _relayMuted;

        /// <param name="equipment">
        /// 필수 장비가 걸리는 자리. 넘기지 않으면 장비 슬롯이 없는 순수 격자다
        /// (보관함·사망 가방이 그렇다).
        /// </param>
        public Inventory(int slotCount, EquipmentSlots equipment = null)
        {
            if (slotCount <= 0) throw new ArgumentOutOfRangeException(nameof(slotCount));
            _slots = new ItemStack[slotCount];
            for (int i = 0; i < slotCount; i++) _slots[i] = new ItemStack();

            _equipment = equipment;
            if (_equipment != null) _equipment.Changed += OnEquipmentChanged;
        }

        void OnEquipmentChanged()
        {
            if (_relayMuted) return;
            Changed?.Invoke();
        }

        /// <summary>
        /// 소지품 칸의 개수. <b>장비 슬롯은 여기 포함되지 않는다.</b>
        /// 15칸은 랜턴을 걸어도 15칸이다.
        /// </summary>
        public int SlotCount => _slots.Length;
        public IReadOnlyList<ItemStack> Slots => _slots;

        /// <summary>
        /// 필수 장비 자리. 없으면 null.
        ///
        /// 인벤토리가 이것을 들고 있는 이유는 <b>습득 길목이 하나여야</b> 하기 때문이다.
        /// 랜턴은 제작(<c>CraftingService</c>)·줍기·상자 인출 어디로도 들어오는데,
        /// 그 전부가 <see cref="TryAdd"/>를 지난다. 여기서 갈라 보내지 않으면
        /// 경로마다 같은 규칙을 다시 적어야 한다.
        /// </summary>
        public EquipmentSlots Equipment => _equipment;

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

            // 0단계. 필수 장비는 빈 장비 자리로 먼저 간다. 소지품 칸을 먹지 않는다.
            // 이미 걸려 있으면 밀어내지 않는다 - 두 번째 랜턴은 그냥 짐이다.
            if (_equipment != null && remaining > 0 && _equipment.CanEquip(item))
            {
                _relayMuted = true;
                try { if (_equipment.TryEquipIntoEmpty(item)) remaining--; }
                finally { _relayMuted = false; }
            }

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

        /// <summary>
        /// 수량이 모자라면 아무것도 건드리지 않고 false를 돌려준다.
        ///
        /// 소지품 칸을 먼저 비우고, 그래도 모자랄 때만 장비 자리를 벗긴다.
        /// 걸어 둔 것이 먼저 사라지면 "장비는 밀려나지 않는다"가 깨진다.
        /// (장비를 재료로 쓰는 상위 티어 제작은 스펙 §12가 예고한 길이다.)
        /// </summary>
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

            if (remaining > 0 && _equipment != null)
            {
                _relayMuted = true;
                try { remaining -= _equipment.RemoveById(itemId, remaining); }
                finally { _relayMuted = false; }
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 소지품 칸과 장비 자리를 합쳐 센다.
        ///
        /// 합치는 이유: 장비 슬롯은 소지품에서 <b>자리</b>를 뺀 것이지 <b>소지</b>를
        /// 뺀 것이 아니다. "랜턴을 가졌는가"를 묻는 곳(목표 판정·제작 재료·안내 문구)이
        /// 걸어 둔 랜턴을 못 보면, 슬롯을 나눈 대가로 없던 버그가 생긴다.
        /// </summary>
        public int CountOf(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            int sum = 0;
            foreach (var slot in _slots)
                if (!slot.IsEmpty && slot.item.id == itemId) sum += slot.count;
            if (_equipment != null) sum += _equipment.CountOf(itemId);
            return sum;
        }

        /// <summary>소지품 칸에만 있는 개수. 자리를 세는 쪽(사망 드롭)이 쓴다.</summary>
        public int CountInSlots(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            int sum = 0;
            foreach (var slot in _slots)
                if (!slot.IsEmpty && slot.item.id == itemId) sum += slot.count;
            return sum;
        }

        /// <summary>
        /// 소지품 칸에 놓인 필수 장비를 장비 자리로 옮긴다.
        ///
        /// 슬롯에 직접 앉히는 길이 하나 있어서 필요하다 - 저장 복원이다. 이 길로
        /// 들어온 랜턴은 <see cref="TryAdd"/>를 지나지 않으므로 칸에 남는다.
        /// </summary>
        /// <returns>옮긴 개수.</returns>
        public int RehomeEquipment()
        {
            if (_equipment == null) return 0;

            int moved = 0;
            _relayMuted = true;
            try
            {
                foreach (var slot in _slots)
                {
                    if (slot.IsEmpty || !_equipment.CanEquip(slot.item)) continue;
                    if (!_equipment.TryEquipIntoEmpty(slot.item)) continue;

                    slot.count--;
                    if (slot.count <= 0) slot.Clear();
                    moved++;
                }
            }
            finally { _relayMuted = false; }

            if (moved > 0) Changed?.Invoke();
            return moved;
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
