using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Input;
using Survive.Player;

namespace Survive.Combat
{
    /// <summary>
    /// 장착한 도구로 전방 원뿔 안의 대상을 때린다.
    /// 도구가 없으면 발동하지 않는다.
    /// 타격감은 Feel에 위임한다 — 코드는 재생만 요청하고, 내용은 에디터에서 조립한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class MeleeSwing : MonoBehaviour
    {
        [SerializeField] InputReaderSO input;
        [SerializeField] PlayerToolHolder toolHolder;
        [SerializeField] Transform swingOrigin;         // 보통 카메라
        [SerializeField] LayerMask targetMask = ~0;

        [Tooltip("전방 판정 각도(도). 90이면 좌우 45도씩")]
        [SerializeField] float coneAngle = 90f;

        [Header("피드백")]
        [Tooltip("휘두를 때마다 재생. 도구 스윙음·모션")]
        [SerializeField] MMF_Player swingFeedback;

        [Tooltip("무언가에 맞았을 때만 재생. 화면 흔들림·히트스톱·타격음")]
        [SerializeField] MMF_Player hitFeedback;

        [Tooltip("손에 든 도구를 휘두르는 연출. 비우면 자동으로 찾는다")]
        [SerializeField] ToolSwingAnimator swingAnimator;

        float _nextSwingTime;
        readonly List<IDamageable> _hitThisSwing = new List<IDamageable>();

        /// <summary>지금까지 휘두른 횟수. 쿨타임에 막힌 시도는 세지 않는다.</summary>
        public int SwingCount { get; private set; }

        void Awake()
        {
            if (toolHolder == null) toolHolder = GetComponentInParent<PlayerToolHolder>();
            if (swingOrigin == null && Camera.main != null) swingOrigin = Camera.main.transform;
            if (swingAnimator == null) swingAnimator = GetComponentInParent<ToolSwingAnimator>();
        }

        void OnEnable()
        {
            if (input != null) input.AttackEvent += TrySwing;
        }

        void OnDisable()
        {
            if (input != null) input.AttackEvent -= TrySwing;
        }

        /// <summary>
        /// 꾹 누르고 있으면 쿨타임마다 다시 휘두른다.
        ///
        /// 광맥 하나를 부수는 데 여러 번 때려야 하는데, 그때마다 클릭을 요구하면
        /// 손가락만 바쁘고 얻는 것은 같다. 홀드 채집(E)과 같은 몸짓이 되기도 한다.
        /// 쿨타임은 그대로라 연타로 더 빨라지지는 않는다.
        /// </summary>
        void Update()
        {
            if (input == null || !input.IsAttackHeld) return;
            TrySwing();
        }

        public void TrySwing()
        {
            var tool = toolHolder != null ? toolHolder.EquippedTool : null;
            if (tool == null) return;                       // 맨손으로는 때리지 않는다
            if (Time.time < _nextSwingTime) return;
            if (swingOrigin == null) return;

            _nextSwingTime = Time.time + tool.attackCooldown;
            _hitThisSwing.Clear();
            SwingCount++;

            // 반응은 화면이 아니라 때린 물건에서 나와야 한다.
            swingAnimator?.Play();
            swingFeedback?.PlayFeedbacks();

            var candidates = Physics.OverlapSphere(swingOrigin.position, tool.attackRange,
                                             targetMask, QueryTriggerInteraction.Collide);

            float cosLimit = MeleeTargeting.ConeCosLimit(coneAngle);
            Transform self = transform;

            foreach (var col in candidates)
            {
                var target = col.GetComponentInParent<IDamageable>();
                if (target == null || target.IsDead) continue;

                // 판정 구의 중심이 카메라라 자기 몸은 언제나 후보로 잡힌다.
                // 고개를 숙였다고 자기 곡괭이에 맞을 이유는 없다.
                if (MeleeTargeting.IsSelfTarget(self, target)) continue;

                if (_hitThisSwing.Contains(target)) continue;        // 콜라이더 여러 개인 대상 중복 방지

                Vector3 toTarget = col.bounds.center - swingOrigin.position;
                if (!MeleeTargeting.IsWithinCone(swingOrigin.forward, toTarget, cosLimit)) continue;

                Vector3 dir = toTarget.normalized;
                _hitThisSwing.Add(target);
                target.TakeDamage(new DamageInfo(tool.damage, gameObject,
                                              col.ClosestPoint(swingOrigin.position), -dir));
            }

            if (_hitThisSwing.Count > 0)
            {
                swingAnimator?.PlayImpact();
                hitFeedback?.PlayFeedbacks();
            }
        }
    }
}
