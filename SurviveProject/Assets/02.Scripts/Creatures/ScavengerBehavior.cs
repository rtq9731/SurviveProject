using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Interaction;

namespace Survive.Creatures
{
    /// <summary>
    /// 분해자 기계의 회수. 세계관의 순환에서 마지막 단계다.
    ///
    /// 바닥에 떨어진 전리품을 찾아가 가져간다.
    /// 플레이어에게는 <b>시간 압박</b>이 된다 — 전투 후 꾸물대면 분해자가 먼저 챙긴다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ScavengerBehavior : MonoBehaviour
    {
        [SerializeField] float searchRadius = 12f;
        [SerializeField] float collectRange = 1.2f;

        [Tooltip("회수 사이의 간격. 너무 빠르면 플레이어가 손쓸 틈이 없다")]
        [SerializeField] float collectCooldown = 4f;

        [Header("피드백")]
        [SerializeField] MMF_Player collectFeedback;

        float _nextCollectTime;
        Transform _target;
        ICreatureMotor _motor;

        public int Collected { get; private set; }

        /// <summary>
        /// 위아래로 얼마까지 손이 닿는가. 먹이 쪽과 같은 셈이다 —
        /// 사거리에 순항 고도를 얹는다(<see cref="CreatureFeeding"/>).
        /// </summary>
        float VerticalReach => collectRange + (Motor != null ? Motor.CruiseHeight : 0f);

        /// <summary>몸을 옮기는 것. 두뇌가 나중에 붙일 수 있어 처음 쓸 때 찾는다.</summary>
        ICreatureMotor Motor => _motor ??= GetComponent<ICreatureMotor>();

        /// <summary>CreatureBrain이 이동 목표로 쓴다.</summary>
        public bool TryGetScavengeTarget(out Vector3 pos)
        {
            if (_target == null) NearestPickup();
            if (_target == null)
            {
                pos = Vector3.zero;
                return false;
            }
            pos = _target.position;
            return true;
        }

        void Update()
        {
            if (!CreatureDecision.IsReady(Time.time, _nextCollectTime)) return;

            var pickup = NearestPickup();
            if (pickup == null) return;

            // 회수도 <b>원기둥으로</b> 판정한다. 잔해는 바닥에 놓이고 몸은 그 위에
            // 있으므로, 3차원 거리로 재면 높이차가 그대로 사거리를 깎는다 —
            // 먹이 쪽과 같은 함정이다(CreatureDecision.IsWithinReach).
            if (!CreatureDecision.IsWithinReach(transform.position, pickup.transform.position,
                                                collectRange, VerticalReach))
                return;

            // 회수 = 세계에서 사라진다. 분해자는 코어로 가져간다는 설정이다.
            Destroy(pickup.gameObject);
            Collected++;
            _target = null;
            _nextCollectTime = Time.time + collectCooldown;
            collectFeedback?.PlayFeedbacks();
        }

        static readonly Collider[] _buf = new Collider[24];

        ItemPickup NearestPickup()
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position, searchRadius, _buf,
                                                  ~0, QueryTriggerInteraction.Collide);
            ItemPickup best = null;
            float bestD = float.MaxValue;

            for (int i = 0; i < n; i++)
            {
                var p = _buf[i].GetComponentInParent<ItemPickup>();
                if (p == null) continue;

                float d = (p.transform.position - transform.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = p; }
            }

            _target = best != null ? best.transform : null;
            return best;
        }
    }
}
