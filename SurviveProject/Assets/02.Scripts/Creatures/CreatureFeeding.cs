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

        float _stored;
        float _nextEatTime;
        ICreatureMotor _motor;
        Vector3 _baseScale;
        MaterialPropertyBlock _mpb;
        Transform _targetPlant;

        /// <summary>축적한 스크랩 가치. 사망 시 드롭량에 더해진다.</summary>
        public float Stored => _stored;
        public float Fullness => FeedingStore.Fullness(_stored, fullAt);
        public bool IsFull => FeedingStore.IsFull(_stored, fullAt);

        /// <summary>배가 부르는 지점. 실측이 「가득」을 이 값으로 세운다.</summary>
        public float Capacity => fullAt;

        /// <summary>한 입과 한 입 사이(초). 실측이 배부르기까지의 바닥 시간을 셈한다.</summary>
        public float BiteCooldown => eatCooldown;

        /// <summary>
        /// <b>실측이 배를 채워 놓고 잰다.</b>
        ///
        /// 실제로 먹여서 채우려면 식물이 다시 자라기를 기다려야 해서 한 판이 몇 분이
        /// 되는데, 그 기다림은 <b>드롭 배율</b>과 무관하다. 재려는 것이 둘이므로
        /// 무대도 둘로 가른다 — 「몇 초에 배부른가」는 실제로 먹여서 재고,
        /// 「배부르면 몇 개가 나오는가」는 채워 놓고 잰다.
        ///
        /// 표현도 함께 갱신한다. 그래야 이 문으로 세운 개체가 화면에서도
        /// 배부른 개체로 보이고, 스크린샷이 거짓말을 하지 않는다.
        /// </summary>
        public void Prefill(float stored)
        {
            _stored = Mathf.Max(0f, stored);
            RefreshAppearance();
        }

        /// <summary>
        /// 위아래로 얼마까지 손이 닿는가. <b>사거리에 순항 고도를 얹는다</b> —
        /// 나는 몸은 늘 그만큼 위에 있으므로 얹지 않으면 어느 자리에서도 못 먹고,
        /// 무한정 얹으면 거대 버섯 갓 위를 지나며 9m 아래의 풀을 뜯는다(둘 다 실측).
        /// </summary>
        float VerticalReach => eatRange + (Motor != null ? Motor.CruiseHeight : 0f);

        /// <summary>
        /// 몸을 옮기는 것. <b>Awake에서 한 번만 찾으면 안 된다</b> —
        /// <see cref="CreatureBrain"/>이 제 Awake에서 <see cref="HoverDrifter"/>를
        /// 붙이는 길이 있어서, 깨어나는 차례에 따라 null로 굳는다.
        /// </summary>
        ICreatureMotor Motor => _motor ??= GetComponent<ICreatureMotor>();

        void Awake()
        {
            _baseScale = transform.localScale;
            if (glowRenderer == null) glowRenderer = GetComponentInChildren<Renderer>();
            _mpb = new MaterialPropertyBlock();
            RefreshAppearance();
        }

        void Update()
        {
            if (IsFull) return;
            if (!CreatureDecision.IsReady(Time.time, _nextEatTime)) return;

            var plant = NearestPlant();
            if (plant == null) return;

            _targetPlant = plant.transform;

            // <b>원기둥으로 잰다</b> (CreatureDecision.IsWithinReach). 3차원 거리로
            // 재면 몸이 식물 위쪽에 있는 만큼이 그대로 사거리를 깎아, 바로 앞에 선
            // 개체도 영영 못 먹는다 — 그 실측이 규칙 쪽 주석에 있다.
            if (!CreatureDecision.IsWithinReach(transform.position, plant.transform.position,
                                                eatRange, VerticalReach))
                return;

            float nutrition = plant.Eat();
            if (nutrition <= 0f) return;

            _stored += nutrition;
            _nextEatTime = Time.time + eatCooldown;
            eatFeedback?.PlayFeedbacks();
            RefreshAppearance();
        }

        /// <summary>CreatureBrain이 이동 목표로 쓴다.</summary>
        public bool TryGetFeedTarget(out Vector3 pos)
        {
            if (!IsFull && _targetPlant == null) NearestPlant();
            if (IsFull || _targetPlant == null)
            {
                pos = Vector3.zero;
                return false;
            }
            pos = _targetPlant.position;
            return true;
        }

        static readonly Collider[] _buf = new Collider[24];

        PlantNode NearestPlant()
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

            _targetPlant = best != null ? best.transform : null;
            return best;
        }

        void RefreshAppearance()
        {
            float f = Fullness;
            transform.localScale = _baseScale * Mathf.Lerp(1f, swellScale, f);

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
