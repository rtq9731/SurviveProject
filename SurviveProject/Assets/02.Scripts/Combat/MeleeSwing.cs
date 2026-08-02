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

        float _nextSwingTime;
        readonly List<IDamageable> _hitThisSwing = new List<IDamageable>();

        void Awake()
        {
            if (toolHolder == null) toolHolder = GetComponentInParent<PlayerToolHolder>();
            if (swingOrigin == null && Camera.main != null) swingOrigin = Camera.main.transform;
        }

        void OnEnable()
        {
            if (input != null) input.AttackEvent += TrySwing;
        }

        void OnDisable()
        {
            if (input != null) input.AttackEvent -= TrySwing;
        }

        public void TrySwing()
        {
            var tool = toolHolder != null ? toolHolder.EquippedTool : null;
            if (tool == null) return;                       // 맨손으로는 때리지 않는다
            if (Time.time < _nextSwingTime) return;
            if (swingOrigin == null) return;

            _nextSwingTime = Time.time + tool.attackCooldown;
            _hitThisSwing.Clear();
            swingFeedback?.PlayFeedbacks();

            var candidates = Physics.OverlapSphere(swingOrigin.position, tool.attackRange,
                                             targetMask, QueryTriggerInteraction.Collide);

            float cosLimit = Mathf.Cos(coneAngle * 0.5f * Mathf.Deg2Rad);

            foreach (var col in candidates)
            {
                var target = col.GetComponentInParent<IDamageable>();
                if (target == null || target.IsDead) continue;
                if (_hitThisSwing.Contains(target)) continue;        // 콜라이더 여러 개인 대상 중복 방지

                Vector3 dir = (col.bounds.center - swingOrigin.position).normalized;
                if (Vector3.Dot(swingOrigin.forward, dir) < cosLimit) continue;

                _hitThisSwing.Add(target);
                target.TakeDamage(new DamageInfo(tool.damage, gameObject,
                                              col.ClosestPoint(swingOrigin.position), -dir));
            }

            if (_hitThisSwing.Count > 0) hitFeedback?.PlayFeedbacks();
        }
    }
}
