using System.Collections.Generic;
using UnityEngine;
using Survive.Core;
using Survive.Items;
using Survive.World;

namespace Survive.Player
{
    /// <summary>
    /// 플레이어가 지금 갖춘 이동 장비를 <see cref="GearCapability"/> 목록으로 내놓는다.
    /// P2 스펙 §8-2의 "판정 연결" — 인벤토리와 <see cref="HazardZoneRegistry"/> 사이에서
    /// 이 컴포넌트 하나만 서 있으면 된다.
    ///
    /// 판정 규칙도 목록 만드는 규칙도 여기 없다. 전자는 <see cref="EnvironmentThreat"/>,
    /// 후자는 <see cref="TraversalLoadout"/>에 있고 둘 다 Unity 없이 테스트된다.
    /// 여기 남는 것은 "누구의 인벤토리인가"뿐이다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerTraversalGear : MonoBehaviour
    {
        [SerializeField] PlayerInventory inventory;

        // 매 프레임 물어도 쓰레기가 생기지 않게 한 벌만 두고 다시 채운다.
        readonly List<GearCapability> _loadout = new List<GearCapability>();

        void Awake()
        {
            if (inventory == null) inventory = GetComponentInParent<PlayerInventory>();
            if (inventory == null) inventory = GetComponentInChildren<PlayerInventory>(true);
        }

        void OnEnable() => GameServices.Register(this);
        void OnDisable() => GameServices.Unregister<PlayerTraversalGear>();

        /// <summary>
        /// 지금 갖춘 장비. 부를 때마다 인벤토리에서 다시 만든다 —
        /// <see cref="PlayerInventory.RestoreState"/>가 <see cref="Inventory"/> 객체 자체를
        /// 갈아 끼우므로 변경 이벤트를 붙들고 있으면 세이브 복원 뒤에 옛 것을 보게 된다.
        /// 슬롯 열몇 개를 훑는 값이 그 위험보다 싸다.
        /// </summary>
        public IReadOnlyList<GearCapability> Loadout
        {
            get
            {
                _loadout.Clear();
                TraversalLoadout.Collect(inventory?.Inventory, _loadout);
                // 수영과 랜턴은 출처가 인벤토리가 아니다(각각 산소·배터리).
                // 그 둘을 관문으로 배치하는 것은 씬 배치의 일이라 여기서는 얹지 않는다.
                return _loadout;
            }
        }

        /// <summary>이 자리를 지금 갖춘 것으로 지날 수 있는가, 아니면 무엇이 모자란가.</summary>
        public PassageCheck Evaluate(Vector3 position) =>
            HazardZoneRegistry.Evaluate(position, Loadout);

        /// <summary>가부만 필요할 때.</summary>
        public bool CanEnter(Vector3 position) => Evaluate(position).CanPass;

        /// <summary>지금 서 있는 자리 기준.</summary>
        public bool CanStandHere() => CanEnter(transform.position);
    }
}
