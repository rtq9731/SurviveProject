using System;
using System.Collections.Generic;

namespace Survive.Items
{
    /// <summary>
    /// 필수 장비가 걸리는 자리들. 종류마다 한 자리씩이고, 소지품 칸을 먹지 않는다.
    ///
    /// <b>왜 있는가.</b> 스펙 §11: 랜턴이 채집물과 칸을 두고 경쟁하면 그건 선택이
    /// 아니라 상수 감소다. 15칸 중 한 칸이 늘 랜턴이면 실질 소지품은 14칸이고,
    /// 플레이어는 그 한 칸을 두고 아무 판단도 하지 않는다. 판단하지 않는 자리는
    /// 자리로 둘 값어치가 없으므로 아예 밖으로 뺀다.
    ///
    /// <b>무엇이 "필수 장비"인가 (판단과 근거).</b> 지금은 <b>랜턴뿐이다.</b>
    /// 기준은 "플레이어가 챙길지 말지를 고르지 않는 것"이다.
    /// <list type="bullet">
    /// <item>랜턴은 상시 점등이 전제고 끄는 선택지조차 두지 않는다(스펙 §12).
    ///       두고 나갈 수 있는 물건이 아니므로 칸을 차지할 이유가 없다.</item>
    /// <item>액면 보행 장비·돌파정(<c>TraversalGearItemSO</c>)은 <b>넣지 않았다.</b>
    ///       스펙 §11이 지키려는 것이 바로 "출발 전에 무엇을 챙길지 정하는" 판단이고
    ///       (원정 2단 구조), 통행 장비는 그 판단의 알맹이다. 정찰 원정은 최소한만
    ///       챙긴다. 통행 장비를 공짜 자리로 옮기면 그 판단이 사라진다.
    ///       코드 쪽 근거도 같은 방향이다. <c>TraversalGearItemSO</c>와
    ///       <c>TraversalLoadout</c>은 "인벤토리에 있으면 몸에 걸친 것으로 본다"를
    ///       명시적으로 택했고, 통행 판정이 전부 그 전제 위에 서 있다.</item>
    /// </list>
    /// 좁게 시작하고 <see cref="EquipmentSlotKind"/>에 값을 더할 자리를 남긴다.
    ///
    /// <b>사망 드롭.</b> 여기 걸린 것은 죽어도 떨구지 않는다. 별도 규칙을 두지
    /// 않았고 둘 필요도 없다 - <see cref="DeathDrop.Extract"/>는 인벤토리의
    /// <see cref="Inventory.Slots"/>만 훑으므로 장비 슬롯은 구조적으로 닿지 않는다.
    /// 기존 설계와도 어긋나지 않는다. <see cref="DeathDrop"/>는 이미 도구를 남기고,
    /// 그 이유로 "랜턴을 같이 떨구면 회수하러 갈 수단 자체를 잃는다"를 든다.
    /// 장비 슬롯은 그 판단을 데이터로 못 박은 것이지 뒤집은 것이 아니다.
    ///
    /// MonoBehaviour가 아니므로 씬 없이 테스트한다.
    /// </summary>
    public sealed class EquipmentSlots
    {
        /// <summary>
        /// 실제로 존재하는 자리. <see cref="EquipmentSlotKind.None"/>은 자리가 아니다.
        /// 열거형에서 뽑으므로 종류를 더하면 자리도 같이 는다.
        /// </summary>
        public static readonly EquipmentSlotKind[] AllKinds = BuildKinds();

        readonly ItemDataSO[] _items = new ItemDataSO[AllKinds.Length];

        static EquipmentSlotKind[] BuildKinds()
        {
            var values = (EquipmentSlotKind[])Enum.GetValues(typeof(EquipmentSlotKind));
            var list = new List<EquipmentSlotKind>(values.Length);
            foreach (var v in values)
                if (v != EquipmentSlotKind.None) list.Add(v);
            return list.ToArray();
        }

        /// <summary>자리가 하나라도 바뀌면 울린다.</summary>
        public event Action Changed;

        public int SlotCount => _items.Length;
        public IReadOnlyList<EquipmentSlotKind> Kinds => AllKinds;

        /// <summary>이 아이템이 장비인가. 정의가 없으면 장비가 아니다.</summary>
        public static bool IsEquipment(ItemDataSO item) =>
            item != null && item.equipSlot != EquipmentSlotKind.None && IndexOf(item.equipSlot) >= 0;

        static int IndexOf(EquipmentSlotKind kind)
        {
            for (int i = 0; i < AllKinds.Length; i++)
                if (AllKinds[i] == kind) return i;
            return -1;
        }

        /// <summary>지금 이 아이템을 걸 수 있는가. 장비가 아니면 거부한다.</summary>
        public bool CanEquip(ItemDataSO item) => IsEquipment(item);

        public ItemDataSO Get(EquipmentSlotKind kind)
        {
            int i = IndexOf(kind);
            return i < 0 ? null : _items[i];
        }

        /// <summary>순번으로 읽는다. 화면이 자리를 차례로 그릴 때 쓴다.</summary>
        public ItemDataSO GetAt(int index) =>
            index < 0 || index >= _items.Length ? null : _items[index];

        public bool IsEmpty(EquipmentSlotKind kind) => Get(kind) == null;

        /// <summary>비어 있는 자리에만 건다. 이미 차 있으면 아무것도 하지 않는다.</summary>
        public bool TryEquipIntoEmpty(ItemDataSO item)
        {
            if (!CanEquip(item)) return false;
            int i = IndexOf(item.equipSlot);
            if (_items[i] != null) return false;

            _items[i] = item;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 건다. 이미 다른 것이 걸려 있으면 그것을 <paramref name="displaced"/>로 돌려준다.
        /// 부르는 쪽이 그것을 어디로 보낼지 정한다 - 여기서 버리지 않는다.
        /// </summary>
        public bool TryEquip(ItemDataSO item, out ItemDataSO displaced)
        {
            displaced = null;
            if (!CanEquip(item)) return false;

            int i = IndexOf(item.equipSlot);
            if (_items[i] == item) return false;

            displaced = _items[i];
            _items[i] = item;
            Changed?.Invoke();
            return true;
        }

        public bool TryEquip(ItemDataSO item) => TryEquip(item, out _);

        /// <summary>자리를 비운다. 비어 있었으면 null.</summary>
        public ItemDataSO Unequip(EquipmentSlotKind kind)
        {
            int i = IndexOf(kind);
            if (i < 0 || _items[i] == null) return null;

            var was = _items[i];
            _items[i] = null;
            Changed?.Invoke();
            return was;
        }

        /// <summary>id로 벗긴다. 실제로 벗긴 개수(0 또는 1)를 돌려준다.</summary>
        public int RemoveById(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return 0;

            int removed = 0;
            for (int i = 0; i < _items.Length && removed < count; i++)
            {
                if (_items[i] == null || _items[i].id != itemId) continue;
                _items[i] = null;
                removed++;
            }
            if (removed > 0) Changed?.Invoke();
            return removed;
        }

        public int CountOf(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            int sum = 0;
            foreach (var it in _items)
                if (it != null && it.id == itemId) sum++;
            return sum;
        }

        public bool Has(string itemId) => CountOf(itemId) > 0;

        public void Clear()
        {
            bool any = false;
            for (int i = 0; i < _items.Length; i++)
            {
                if (_items[i] == null) continue;
                _items[i] = null;
                any = true;
            }
            if (any) Changed?.Invoke();
        }
    }
}
