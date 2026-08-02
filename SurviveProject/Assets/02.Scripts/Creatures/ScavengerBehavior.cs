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

        float _다음회수시각;
        Transform _목표;

        public int Collected { get; private set; }

        /// <summary>CreatureBrain이 이동 목표로 쓴다.</summary>
        public bool TryGetScavengeTarget(out Vector3 pos)
        {
            if (_목표 == null) 가장가까운잔해();
            if (_목표 == null)
            {
                pos = Vector3.zero;
                return false;
            }
            pos = _목표.position;
            return true;
        }

        void Update()
        {
            if (Time.time < _다음회수시각) return;

            var pickup = 가장가까운잔해();
            if (pickup == null) return;

            if (Vector3.Distance(transform.position, pickup.transform.position) > collectRange) return;

            // 회수 = 세계에서 사라진다. 분해자는 코어로 가져간다는 설정이다.
            Destroy(pickup.gameObject);
            Collected++;
            _목표 = null;
            _다음회수시각 = Time.time + collectCooldown;
            collectFeedback?.PlayFeedbacks();
        }

        static readonly Collider[] _buf = new Collider[24];

        ItemPickup 가장가까운잔해()
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

            _목표 = best != null ? best.transform : null;
            return best;
        }
    }
}
