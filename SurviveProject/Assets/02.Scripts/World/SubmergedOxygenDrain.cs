using UnityEngine;
using Survive.Vitals;

namespace Survive.World
{
    /// <summary>
    /// 물에 잠기면 산소를 소모한다.
    /// 지하는 테라포밍이 성공해 대기를 호흡할 수 있으므로, 산소는 상시 자원이 아니라
    /// 수중·특수 필드에서만 쓰이는 상황 자원이다.
    /// 플레이어에 붙이고 수면 높이를 알려주면 스스로 판단한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class SubmergedOxygenDrain : MonoBehaviour, IOxygenModifier
    {
        [Tooltip("이 높이 아래로 머리가 잠기면 산소를 쓴다")]
        [SerializeField] Transform headPoint;

        [Tooltip("수면 판정에 쓸 레이어. 물 오브젝트를 지정한다")]
        [SerializeField] LayerMask waterMask;

        [Tooltip("잠겼을 때 초당 산소 소모량")]
        [SerializeField] float drainPerSecond = 6f;

        [Tooltip("물 밖에서 초당 회복량")]
        [SerializeField] float refillPerSecond = 20f;

        PlayerVitals _vitals;
        bool _잠김;

        public bool IsSubmerged => _잠김;

        // 잠기면 소모, 아니면 회복. 다른 보정과 겹치면 가장 유리한 값이 채택된다.
        public float OxygenDeltaPerSecond => _잠김 ? -drainPerSecond : refillPerSecond;

        void Awake()
        {
            _vitals = GetComponentInParent<PlayerVitals>();
            if (headPoint == null) headPoint = transform;
        }

        void OnEnable() => _vitals?.RegisterOxygenModifier(this);
        void OnDisable() => _vitals?.UnregisterOxygenModifier(this);

        void Update()
        {
            // 머리 위치에서 위로 쏴서 물 표면에 맞으면 잠긴 것이다.
            bool 이번에잠김 = Physics.Raycast(headPoint.position, Vector3.up, 50f,
                                             waterMask, QueryTriggerInteraction.Collide);

            if (이번에잠김 != _잠김) _잠김 = 이번에잠김;
        }
    }
}
