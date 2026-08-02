using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Combat;
using Survive.Harvesting;

namespace Survive.Creatures
{
    /// <summary>
    /// 생산자 기계의 섭취. 세계관의 순환에서 두 번째 단계다.
    ///
    /// 근처 식물을 찾아가 먹고 스크랩을 축적한다.
    /// 많이 먹은 개체일수록 몸이 부풀고 접합부가 밝아져 <b>겉으로 보인다</b> —
    /// 플레이어가 관찰해서 배부른 개체를 고를 수 있어야 생태계가 게임이 된다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CreatureHealth))]
    public class CreatureFeeding : MonoBehaviour
    {
        [SerializeField] float searchRadius = 14f;
        [SerializeField] float eatRange = 1.6f;
        [SerializeField] float eatCooldown = 6f;

        [Tooltip("이만큼 쌓이면 배가 불러 더 먹지 않는다")]
        [SerializeField] float fullAt = 12f;

        [Header("배부른 표현")]
        [Tooltip("가득 찼을 때 몸이 커지는 배율")]
        [SerializeField] float swellScale = 1.22f;

        [Tooltip("배부를수록 밝아지는 부위. 비우면 자기 렌더러를 쓴다")]
        [SerializeField] Renderer glowRenderer;

        [SerializeField] Color emptyColor = new Color(0.35f, 0.36f, 0.40f);
        [SerializeField] Color fullColor = new Color(0.55f, 0.95f, 0.70f);

        [Header("피드백")]
        [SerializeField] MMF_Player eatFeedback;

        float _축적;
        float _다음섭취시각;
        Vector3 _원래크기;
        MaterialPropertyBlock _mpb;
        Transform _목표식물;

        /// <summary>축적한 스크랩 가치. 사망 시 드롭량에 더해진다.</summary>
        public float Stored => _축적;
        public float Fullness => fullAt <= 0f ? 0f : Mathf.Clamp01(_축적 / fullAt);
        public bool IsFull => _축적 >= fullAt;

        void Awake()
        {
            _원래크기 = transform.localScale;
            if (glowRenderer == null) glowRenderer = GetComponentInChildren<Renderer>();
            _mpb = new MaterialPropertyBlock();
            표현갱신();
        }

        void Update()
        {
            if (IsFull) return;
            if (Time.time < _다음섭취시각) return;

            var plant = 가장가까운식물();
            if (plant == null) return;

            _목표식물 = plant.transform;
            if (Vector3.Distance(transform.position, plant.transform.position) > eatRange) return;

            float 영양 = plant.Eat();
            if (영양 <= 0f) return;

            _축적 += 영양;
            _다음섭취시각 = Time.time + eatCooldown;
            eatFeedback?.PlayFeedbacks();
            표현갱신();
        }

        /// <summary>CreatureBrain이 이동 목표로 쓴다.</summary>
        public bool TryGetFeedTarget(out Vector3 pos)
        {
            if (!IsFull && _목표식물 == null) 가장가까운식물();
            if (IsFull || _목표식물 == null)
            {
                pos = Vector3.zero;
                return false;
            }
            pos = _목표식물.position;
            return true;
        }

        static readonly Collider[] _buf = new Collider[24];

        PlantNode 가장가까운식물()
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position, searchRadius, _buf,
                                                  ~0, QueryTriggerInteraction.Collide);
            PlantNode best = null;
            float bestD = float.MaxValue;

            for (int i = 0; i < n; i++)
            {
                var p = _buf[i].GetComponentInParent<PlantNode>();
                if (p == null || !p.IsEdible) continue;

                float d = (p.transform.position - transform.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = p; }
            }

            _목표식물 = best != null ? best.transform : null;
            return best;
        }

        void 표현갱신()
        {
            float f = Fullness;
            transform.localScale = _원래크기 * Mathf.Lerp(1f, swellScale, f);

            if (glowRenderer == null) return;
            glowRenderer.GetPropertyBlock(_mpb);
            var c = Color.Lerp(emptyColor, fullColor, f);
            _mpb.SetColor("_BaseColor", c);
            // 배부를수록 접합부가 빛난다
            _mpb.SetColor("_EmissionColor", c * (f * 1.6f));
            glowRenderer.SetPropertyBlock(_mpb);
        }
    }
}
