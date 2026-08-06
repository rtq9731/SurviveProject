using System.Collections.Generic;
using UnityEngine;
using Survive.Items;
using Survive.Player;
using Survive.Vitals;

namespace Survive.World
{
    /// <summary>
    /// 물에 머리까지 잠기면 산소를 소모한다.
    ///
    /// 지하는 테라포밍이 성공해 대기를 호흡할 수 있으므로, 산소는 상시 자원이 아니라
    /// 수중·특수 필드에서만 쓰는 상황 자원이다.
    ///
    /// 판정은 PlayerSwimming에 맡긴다. 씬의 물 오브젝트에는 콜라이더가 없어
    /// 레이캐스트로는 수면을 찾을 수 없다.
    ///
    /// <b>수치는 여기 없다.</b> 전부 <see cref="DiveRule"/>에서 읽는다 —
    /// 직렬화 필드로 두면 프리팹에 사본이 남아 상수를 돌려도 게임이 안 바뀐다
    /// (<see cref="LanternRule"/>·<c>CampfireFuelRule</c>에서 실제로 겪은 일이다).
    ///
    /// <b>방호복을 걸치면 숨이 길어진다</b>(실행 스펙 §8-1). 잠수 통로 안에서만
    /// 그런 것이 아니라 <b>잠긴 동안 언제나</b> 그렇다 — 장비가 어디서는 듣고
    /// 어디서는 안 듣는 것은 플레이어가 배울 수 없는 규칙이고, 그렇게 나누면
    /// "통로 밖에서는 왜 숨이 짧은가"를 픽션으로 설명할 길이 없다.
    /// </summary>
    [DisallowMultipleComponent]
    public class SubmergedOxygenDrain : MonoBehaviour, IOxygenModifier
    {
        [SerializeField] PlayerSwimming swimming;

        PlayerVitals _vitals;
        PlayerTraversalGear _gear;
        PlayerInventory _inventory;

        // 매 프레임 물어도 쓰레기가 생기지 않게 한 벌만 두고 다시 채운다.
        readonly List<GearCapability> _loadout = new List<GearCapability>();

        public bool IsSubmerged => swimming != null && swimming.IsHeadSubmerged;

        /// <summary>방호복을 걸치고 있는가. 숨이 얼마나 가는지가 여기서 갈린다.</summary>
        public bool IsSuited => DiveRule.HasSuit(CurrentLoadout());

        // 잠기면 소모, 아니면 회복. 다른 보정과 겹치면 가장 유리한 값이 채택된다.
        public float OxygenDeltaPerSecond => DiveRule.OxygenDeltaPerSecond(IsSubmerged, IsSuited);

        void Awake()
        {
            _vitals = GetComponentInParent<PlayerVitals>();
            if (swimming == null) swimming = GetComponentInParent<PlayerSwimming>();

            var root = _vitals != null ? _vitals.transform : transform;
            _gear = root.GetComponentInParent<PlayerTraversalGear>();
            if (_gear == null) _gear = root.GetComponentInChildren<PlayerTraversalGear>(true);
            _inventory = root.GetComponentInParent<PlayerInventory>();
            if (_inventory == null) _inventory = root.GetComponentInChildren<PlayerInventory>(true);
        }

        void OnEnable() => _vitals?.RegisterOxygenModifier(this);
        void OnDisable() => _vitals?.UnregisterOxygenModifier(this);

        /// <summary>
        /// 지금 갖춘 이동 장비. <see cref="PlayerTraversalGear"/>가 있으면 그쪽에 묻는다 —
        /// <see cref="MacroniumContactService"/>가 잡은 방식 그대로다.
        /// </summary>
        IReadOnlyList<GearCapability> CurrentLoadout()
        {
            if (_gear != null) return _gear.Loadout;

            _loadout.Clear();
            TraversalLoadout.Collect(_inventory?.Inventory, _loadout);
            return _loadout;
        }
    }
}
