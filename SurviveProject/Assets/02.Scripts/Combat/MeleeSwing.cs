using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Audio;
using Survive.Domain.Audio;
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

        [Header("소리")]
        // 아래 셋은 전부 비워 두는 것이 기본이다. 비어 있으면 소리 표(AudioCueBook)의
        // 값을 쓰고, 표도 없으면 아무 소리도 나지 않는다 — 지금이 그 상태다.
        [Tooltip("휘두를 때마다. 비우면 소리 표의 swing")]
        [SerializeField] AudioCueSO swingCue;

        [Tooltip("무언가에 맞았을 때. 비우면 소리 표의 swingHit")]
        [SerializeField] AudioCueSO swingHitCue;

        [Tooltip("아무것도 맞지 않았을 때. 비우면 소리 표의 swingMiss")]
        [SerializeField] AudioCueSO swingMissCue;

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

            var book = AudioService.Book;
            AudioService.Play(AudioCueBookSO.Or(swingCue, book != null ? book.swing : null),
                              swingOrigin.position);

            var candidates = Physics.OverlapSphere(swingOrigin.position, tool.attackRange,
                                             targetMask, QueryTriggerInteraction.Collide);

            float cosLimit = MeleeTargeting.ConeCosLimit(coneAngle);
            Transform self = transform;
            Vector3 origin = swingOrigin.position;

            foreach (var col in candidates)
            {
                var target = col.GetComponentInParent<IDamageable>();
                if (target == null || target.IsDead) continue;

                // 판정 구의 중심이 카메라라 자기 몸은 언제나 후보로 잡힌다.
                // 고개를 숙였다고 자기 곡괭이에 맞을 이유는 없다.
                if (MeleeTargeting.IsSelfTarget(self, target)) continue;

                if (_hitThisSwing.Contains(target)) continue;        // 콜라이더 여러 개인 대상 중복 방지

                // 겨냥은 한가운데가 아니라 가장 가까운 자리로 한다 — 이유는 TrySelectAim에.
                if (!MeleeTargeting.TrySelectAim(swingOrigin.forward, cosLimit, origin,
                                                 NearestPoint(col, origin), col.bounds.center,
                                                 out Vector3 aim)) continue;

                if (IsBlocked(col)) continue;       // 벽 너머는 때리지 않는다

                Vector3 dir = (aim - origin).normalized;
                _hitThisSwing.Add(target);
                target.TakeDamage(new DamageInfo(tool.damage, gameObject, aim, -dir));
            }

            if (_hitThisSwing.Count > 0)
            {
                swingAnimator?.PlayImpact();
                hitFeedback?.PlayFeedbacks();

                // 맞은 자리에서 난다. 여러 대상을 한 번에 맞혀도 소리는 하나다 —
                // 대상마다 내면 같은 파형이 겹쳐 진폭만 치솟는다. 맞은 쪽의
                // 비명은 CreatureHealth가 따로 낸다.
                AudioService.Play(AudioCueBookSO.Or(swingHitCue, book != null ? book.swingHit : null),
                                  swingOrigin.position + swingOrigin.forward * (tool.attackRange * 0.5f));
            }
            else
            {
                AudioService.Play(AudioCueBookSO.Or(swingMissCue, book != null ? book.swingMiss : null),
                                  swingOrigin.position);
            }
        }

        /// <summary>
        /// 시전 지점에서 이 콜라이더로 가장 가까운 점.
        ///
        /// <see cref="Collider.ClosestPoint"/>는 상자·구·캡슐과 볼록한 메시에서만 표면을
        /// 돌려준다. 오목한 메시(광맥 대부분, 거대 버섯 통짜 메시)에 물으면 넣은 점을
        /// 그대로 돌려주면서 콘솔에 경고까지 남긴다. 그래서 그런 콜라이더에는 아예 묻지 않고
        /// 경계 상자의 가장 가까운 점으로 대신한다 — 실제 표면보다 넉넉하지만 판정이
        /// 후해지는 쪽이라, 눈앞의 것을 놓치는 것보다는 낫다.
        ///
        /// 시전 지점이 대상 안에 들어가 있으면 두 방법 모두 넣은 점을 그대로 돌려준다.
        /// 그때는 방향이 서지 않으므로 부르는 쪽에서 다른 겨냥점으로 넘어간다.
        /// </summary>
        static Vector3 NearestPoint(Collider col, Vector3 origin) =>
            col is MeshCollider mesh && !mesh.convex
                ? col.bounds.ClosestPoint(origin)
                : col.ClosestPoint(origin);

        /// <summary>
        /// 시전 지점과 이 콜라이더 사이를 세워 올린 벽이 가로막고 있는가.
        ///
        /// 판정이 구(球)라 얇은 벽 하나쯤은 그냥 지나쳐, 방 밖에 서서 안에 있는 것을
        /// 때리는 일이 벌어졌다. 벽 너머는 도구도 닿지 않아야 한다.
        ///
        /// 막혔는가는 <see cref="MeleeTargeting.IsOccluder"/>가 정하고 —
        /// 지형·소품을 왜 빼는지도 거기 적어 두었다 —
        /// 여기서는 그 앞에 무엇이 있었는지만 물리에 물어본다.
        /// </summary>
        bool IsBlocked(Collider target)
        {
            Vector3 origin = swingOrigin.position;

            // 원점이 대상 안에 들어가 있으면 사이에 낄 자리가 없다.
            if (target.bounds.Contains(origin)) return false;

            // 겨냥할 점은 둘이다. 하나만 뚫려 있어도 닿은 것으로 본다 —
            // 광맥 한쪽이 흙에 묻혀 있다고 드러난 쪽까지 못 캘 이유는 없다.
            Vector3 surface = NearestPoint(target, origin);
            if ((surface - origin).sqrMagnitude > AimEpsilon * AimEpsilon &&
                HasClearLine(origin, target, surface))
                return false;

            return !HasClearLine(origin, target, target.bounds.center);
        }

        /// <summary>
        /// 시전 지점에서 이 점까지 가로막은 것 없이 닿는가.
        ///
        /// 겨냥점은 대상 <b>속</b>일 수도 있다 — <see cref="NearestPoint"/>가 오목한 메시에
        /// 대해 돌려주는 경계 상자 위의 점은 실제 표면보다 안쪽일 수 있고, 한가운데는 말할
        /// 것도 없다. 그래서 대상 표면에 먼저 닿으면 거기서 선을 끊는다 —
        /// 그 너머는 이미 대상 안이라 무엇이 있든 가로막은 것이 아니다.
        /// </summary>
        bool HasClearLine(Vector3 origin, Collider target, Vector3 aim)
        {
            Vector3 to = aim - origin;
            float distance = to.magnitude;
            if (distance <= AimEpsilon) return true;

            Vector3 dir = to / distance;
            if (target.Raycast(new Ray(origin, dir), out RaycastHit onTarget, distance))
                distance = onTarget.distance;

            var hits = Physics.RaycastAll(origin, dir, distance,
                                          Physics.DefaultRaycastLayers,
                                          QueryTriggerInteraction.Ignore);

            foreach (var hit in hits)
            {
                bool isWall = hit.collider.GetComponentInParent<Survive.Building.BuiltStructure>() != null;
                if (MeleeTargeting.IsOccluder(transform, target.transform,
                                              hit.collider.transform, hit.collider.isTrigger, isWall))
                    return false;
            }

            return true;
        }

        /// <summary>이 거리 안쪽이면 겨냥점이 시전 지점과 겹친 것으로 본다(1cm).</summary>
        const float AimEpsilon = 0.01f;
    }
}
