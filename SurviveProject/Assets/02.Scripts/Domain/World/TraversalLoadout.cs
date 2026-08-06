using System.Collections.Generic;
using Survive.Items;

namespace Survive.World
{
    /// <summary>
    /// 인벤토리에 있는 것을 <see cref="GearCapability"/> 목록으로 옮긴다.
    /// <see cref="EnvironmentThreat.Evaluate"/>가 받는 것이 그 목록이므로,
    /// 이 클래스가 "플레이어의 장비 상태 → 판정" 사이의 유일한 연결 고리다.
    ///
    /// P2 스펙 §4 "판정은 Domain에"를 공급 쪽까지 끌고 온다 — MonoBehaviour가 아니라
    /// <see cref="Inventory"/>만 받으므로 Unity 실행 없이 테스트한다.
    ///
    /// <b>보유가 곧 장착이다.</b> 별도의 착용 슬롯을 두지 않는 이유는
    /// <see cref="TraversalGearItemSO"/>의 주석에 적어 두었다.
    ///
    /// 여기서 나오는 것은 <b>아이템에서 오는 장비뿐</b>이다. 수영은 몸에 딸린 능력이고,
    /// 랜턴의 용량은 남은 배터리에서 나온다 —
    /// 출처가 인벤토리가 아니므로 각 시스템이 자기 몫을 따로 얹는다
    /// (<c>Survive.Player.PlayerTraversalGear</c> 참고).
    /// </summary>
    public static class TraversalLoadout
    {
        /// <summary>
        /// 인벤토리를 훑어 장비를 <paramref name="into"/>에 담는다. 목록을 비우지 않으므로
        /// 다른 출처에서 온 장비와 섞어 쌓을 수 있다.
        ///
        /// 같은 장비가 여러 슬롯에 있으면 그 수만큼 담긴다 — 걸러내지 않는 이유는
        /// <see cref="EnvironmentThreat.Evaluate"/>가 이미 중복을 최댓값 하나로 접기 때문이다.
        /// 여기서 또 접으면 같은 규칙이 두 군데 있게 된다.
        /// </summary>
        public static void Collect(Inventory inventory, List<GearCapability> into)
        {
            if (inventory == null || into == null) return;

            var slots = inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || slot.IsEmpty) continue;

                // 용량이 0인 장비도 담는다. 그래야 판정이 "장비가 아예 없다"가 아니라
                // "감당하는 크기가 모자라다"로 나온다 — 플레이어에게 다른 말이다.
                if (!(slot.item is TraversalGearItemSO gearItem)) continue;
                if (gearItem.gear == TraversalGear.None) continue;

                into.Add(new GearCapability(gearItem.gear, gearItem.capacity));
            }
        }

        /// <summary>인벤토리에서 나오는 장비만으로 목록을 새로 만든다.</summary>
        public static List<GearCapability> From(Inventory inventory)
        {
            var list = new List<GearCapability>();
            Collect(inventory, list);
            return list;
        }

        /// <summary>이 인벤토리만으로 그 구간을 지날 수 있는가. 편의용.</summary>
        public static PassageCheck Evaluate(HazardZone zone, Inventory inventory) =>
            EnvironmentThreat.Evaluate(zone, From(inventory));
    }
}
