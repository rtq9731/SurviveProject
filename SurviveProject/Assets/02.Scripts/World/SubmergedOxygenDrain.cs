using UnityEngine;
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
    /// </summary>
    [DisallowMultipleComponent]
    public class SubmergedOxygenDrain : MonoBehaviour, IOxygenModifier
    {
        [SerializeField] PlayerSwimming swimming;

        [Tooltip("머리까지 잠겼을 때 초당 산소 소모량")]
        [SerializeField] float drainPerSecond = 6f;

        [Tooltip("물 밖에서 초당 회복량")]
        [SerializeField] float refillPerSecond = 20f;

        PlayerVitals _vitals;

        public bool IsSubmerged => swimming != null && swimming.IsHeadSubmerged;

        // 잠기면 소모, 아니면 회복. 다른 보정과 겹치면 가장 유리한 값이 채택된다.
        public float OxygenDeltaPerSecond => IsSubmerged ? -drainPerSecond : refillPerSecond;

        void Awake()
        {
            _vitals = GetComponentInParent<PlayerVitals>();
            if (swimming == null) swimming = GetComponentInParent<PlayerSwimming>();
        }

        void OnEnable() => _vitals?.RegisterOxygenModifier(this);
        void OnDisable() => _vitals?.UnregisterOxygenModifier(this);
    }
}
