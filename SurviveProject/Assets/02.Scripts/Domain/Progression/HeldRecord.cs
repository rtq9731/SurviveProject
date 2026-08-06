using Survive.Items;

namespace Survive.Progression
{
    /// <summary>
    /// <b>이 물건을 한 번이라도 손에 쥐어 봤는가.</b>
    ///
    /// 있는지도 모르는 물체를 가져오라고 할 수는 없다. 연구대 목록이 처음부터
    /// 일곱 줄을 펼쳐 놓고 "낫의 핵 0/1"이라고 적어 두면, 낫을 본 적도 없는 사람에게
    /// 생물 다섯 종의 존재와 부위 일곱 종의 이름이 통째로 새어 나간다. 제작 목록에서
    /// 지운 정보가 옆문으로 돌아오는 셈이다(<see cref="Survive.UI.MenuListing"/>).
    ///
    /// <b>왜 원장에 얹는가.</b> "겪은 것"을 적어 두는 자리는 이미 하나 있다 —
    /// <see cref="UnlockLedger"/>는 문자열 열쇠 집합이고 저장 왕복까지 이미 돈다.
    /// 새 저장 항목을 만들면 저장할 것이 둘, 복원 순서 문제도 둘이 되는데
    /// 실제로 필요한 질문은 여전히 "이 열쇠가 있는가" 하나뿐이다.
    /// 도감이 같은 판단을 이미 했다(<see cref="CodexCatalog"/>).
    ///
    /// <b>소비해도 남는다.</b> 열쇠는 한 번 들어가면 빠지지 않으므로, 다 써 버렸다고
    /// 목록에서 줄이 사라지는 일은 없다. 이것이 "지금 가진 것"(인벤토리)과
    /// "가져 본 것"(이 기록)을 나누는 이유다.
    ///
    /// 순수 정적이라 Unity 실행 없이 테스트한다.
    /// </summary>
    public static class HeldRecord
    {
        /// <summary>
        /// 습득 기록 열쇠의 접두. 슬래시를 쓰는 이유는 청사진 id(<c>bp_*</c>)·
        /// 도감 열쇠(<c>codex_*</c>)·발견 id(<c>disc_*</c>)와 <b>절대</b> 겹치지 않게
        /// 하기 위해서다. 원장은 하나뿐이라 이름이 겹치면 조용히 서로를 연다.
        /// </summary>
        public const string Prefix = "held/";

        /// <summary>이 아이템의 습득 기록 열쇠. id가 비면 null.</summary>
        public static string KeyFor(string itemId) =>
            string.IsNullOrWhiteSpace(itemId) ? null : Prefix + itemId;

        public static string KeyFor(ItemDataSO item) => item == null ? null : KeyFor(item.id);

        /// <summary>
        /// 가져 본 적이 있는가.
        ///
        /// 원장이 없으면 <b>false</b>다. 여기서는 <see cref="BlueprintGate"/>와 반대로
        /// 판단하지 않는다 — 이 함수는 "무엇을 감출까"를 묻는 자리가 아니라
        /// "무엇을 겪었나"를 묻는 자리이고, 겪은 기록이 없으면 겪지 않은 것이 맞다.
        /// 개방 쪽으로 실패해야 하는 판단은 부르는 쪽(<c>MenuListing.ShouldList</c>)이
        /// 원장 자체가 null인지를 따로 보고 한다.
        /// </summary>
        public static bool Has(UnlockLedger ledger, string itemId)
        {
            var key = KeyFor(itemId);
            if (key == null || ledger == null) return false;
            return ledger.IsUnlocked(key);
        }

        public static bool Has(UnlockLedger ledger, ItemDataSO item) =>
            item != null && Has(ledger, item.id);

        /// <returns>이번이 첫 습득이면 true. 이미 적혀 있었거나 적을 수 없으면 false.</returns>
        public static bool Record(UnlockLedger ledger, string itemId)
        {
            var key = KeyFor(itemId);
            if (key == null || ledger == null) return false;
            return ledger.Unlock(key);
        }

        public static bool Record(UnlockLedger ledger, ItemDataSO item) =>
            item != null && Record(ledger, item.id);

        /// <summary>
        /// 그릇 안에 <b>지금 들어 있는 것</b>을 전부 찍는다.
        ///
        /// 두 자리에서 필요하다.
        /// <list type="number">
        /// <item><b>소급</b> — 이 기록이 생기기 전에 만든 저장본에는 열쇠가 하나도 없다.
        ///       불러온 직후 소지품·보관함을 훑어 찍지 않으면, 이미 낫의 핵을 들고
        ///       있는 사람의 연구 항목이 사라진다. 지금 문제보다 나쁘다</item>
        /// <item><b>슬롯 직접 쓰기</b> — <c>PlayerInventory.RestoreState</c>는 슬롯에
        ///       값을 직접 앉힌다. <c>TryAdd</c>를 지나지 않으므로 습득 신호가 울리지
        ///       않는다. 손에 있는 것이 기록에 없는 상태는 여기서만 생긴다</item>
        /// </list>
        ///
        /// 이미 써 버린 것까지는 되살릴 수 없다. 남아 있는 것으로만 찍는다.
        /// </summary>
        /// <returns>이번에 새로 적힌 열쇠 수.</returns>
        public static int RecordAll(UnlockLedger ledger, Inventory inventory)
        {
            if (ledger == null || inventory == null) return 0;

            int n = 0;
            foreach (var slot in inventory.Slots)
            {
                if (slot == null || slot.IsEmpty) continue;
                if (Record(ledger, slot.item)) n++;
            }

            // 걸어 둔 장비도 손에 있는 것이다. 칸을 훑는 것만으로는 안 보인다.
            var equipment = inventory.Equipment;
            if (equipment != null)
            {
                for (int i = 0; i < equipment.SlotCount; i++)
                    if (Record(ledger, equipment.GetAt(i))) n++;
            }
            return n;
        }
    }
}
