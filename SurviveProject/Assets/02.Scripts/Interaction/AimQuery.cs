using System.Collections.Generic;
using UnityEngine;

namespace Survive.Interaction
{
    /// <summary>
    /// "지금 어디를 겨누고 있는가"를 물리에 물어 <see cref="AimSelection"/>에 넘기는 자리.
    ///
    /// <see cref="PlayerInteractor"/>에서 떼어 냈다. 이유가 둘이다.
    /// ① 컴포넌트는 얇은 껍데기로 두는 것이 이 저장소의 결이다.
    /// ② <b>실측을 할 수 있어야 한다</b> — 조준이 정말 나아졌는지는 씬의 여러 지점에서
    /// 여러 방향으로 겨눠 보고 세는 수밖에 없는데, 그러려면 카메라 없이 아무 좌표에서나
    /// 같은 판정을 돌릴 수 있어야 한다(<c>Testing/AimAccuracyProbe</c>).
    ///
    /// 매 프레임 도는 자리라 버퍼를 전부 재사용한다. 새로 만드는 것은 하나도 없다.
    /// </summary>
    public sealed class AimQuery : IAimObstruction
    {
        /// <summary>손이 닿는 거리(m).</summary>
        public float MaxDistance = 3f;

        /// <summary>시선에서 이만큼 벗어난 것까지는 겨눈 것으로 봐준다(m).</summary>
        public float Radius = 0.3f;

        /// <summary>후보를 모을 레이어.</summary>
        public LayerMask Mask = ~0;

        /// <summary>이 계층에 속한 것은 후보로 세지 않는다. 보통 플레이어 자신의 몸.</summary>
        public Transform Self;

        /// <summary>가림 검사를 할 것인가. 끄면 옛 판정처럼 벽 너머도 잡힌다(실측용).</summary>
        public bool CheckOcclusion = true;

        // ── 버퍼 ─────────────────────────────────────────────────

        static readonly RaycastHit[] SphereHits = new RaycastHit[64];
        static readonly RaycastHit[] RayHits = new RaycastHit[32];
        static readonly RaycastHit[] OcclusionHits = new RaycastHit[32];
        static readonly List<Renderer> RendererBuffer = new List<Renderer>();
        static readonly List<Collider> ColliderBuffer = new List<Collider>();

        readonly List<IInteractable> _targets = new List<IInteractable>();
        readonly List<Transform> _roots = new List<Transform>();
        readonly List<Vector3> _exactHits = new List<Vector3>();
        readonly List<float> _exactDistances = new List<float>();
        readonly List<bool> _hasExactHit = new List<bool>();
        readonly List<AimCandidate> _candidates = new List<AimCandidate>();
        readonly List<AimScore> _ranked = new List<AimScore>();

        Vector3 _origin;

        /// <summary>마지막 판정의 순위. 앞에서부터 이길 순서다. 진단·실측용.</summary>
        public IReadOnlyList<AimScore> Ranked => _ranked;

        /// <summary>순위표의 <c>Id</c>가 가리키는 대상.</summary>
        public IInteractable TargetAt(int id) => _targets[id];

        /// <summary>
        /// <paramref name="origin"/>에서 <paramref name="forward"/>를 볼 때 무엇이 잡히는가.
        /// </summary>
        public bool TryPick(Vector3 origin, Vector3 forward,
                            out IInteractable target, out Transform root, out AimScore score)
        {
            _origin = origin;
            _targets.Clear();
            _roots.Clear();
            _exactHits.Clear();
            _exactDistances.Clear();
            _hasExactHit.Clear();
            _candidates.Clear();

            // ① 넓게 훑어 후보를 모은다. 넓은 트리거(InteractBounds)가 여기서 일한다 —
            //    작은 물건을 픽셀 단위로 겨누게 하지 않으려는 너그러움은 여기까지다.
            //    "그래서 그것을 겨눴는가"는 아래에서 대상의 몸으로 다시 판정한다.
            int sphereCount = Physics.SphereCastNonAlloc(origin, Radius, forward, SphereHits,
                                                         MaxDistance, Mask,
                                                         QueryTriggerInteraction.Collide);
            for (int i = 0; i < sphereCount; i++)
                Register(SphereHits[i].collider);

            // ② 시선이 단단한 표면을 실제로 뚫고 지나갔으면 그 지점을 기억해 둔다.
            //    상자 근사보다 언제나 정확하다. 트리거는 일부러 뺀다 —
            //    통과해 지나가는 부피를 "정확히 겨눴다"고 볼 수는 없다.
            int rayCount = Physics.RaycastNonAlloc(origin, forward, RayHits, MaxDistance,
                                                   Mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < rayCount; i++)
            {
                int index = Register(RayHits[i].collider);
                if (index < 0) continue;
                SetExactHit(index, RayHits[i].point, RayHits[i].distance);
            }

            // ③ 고르는 것은 순수부의 몫이다.
            for (int i = 0; i < _targets.Count; i++)
            {
                Bounds body = BodyBounds(_roots[i]);
                _candidates.Add(_hasExactHit[i]
                    ? new AimCandidate(i, body, _exactHits[i])
                    : new AimCandidate(i, body));
            }

            var view = new AimView(origin, forward, MaxDistance, Radius);
            if (AimSelection.TrySelect(_candidates, view, CheckOcclusion ? this : null,
                                       _ranked, out score))
            {
                target = _targets[score.Id];
                root = _roots[score.Id];
                return true;
            }

            target = null;
            root = null;
            return false;
        }

        /// <summary>이 콜라이더가 물고 있는 상호작용 대상을 후보 목록에 넣고 자리를 준다. 없으면 -1.</summary>
        int Register(Collider col)
        {
            if (col == null) return -1;
            if (BelongsTo(col, Self)) return -1;

            var target = col.GetComponentInParent<IInteractable>();
            if (target == null) return -1;

            for (int i = 0; i < _targets.Count; i++)
                if (ReferenceEquals(_targets[i], target)) return i;

            _targets.Add(target);
            _roots.Add(target is Component c ? c.transform : col.transform);
            _exactHits.Add(default);
            _exactDistances.Add(float.MaxValue);
            _hasExactHit.Add(false);
            return _targets.Count - 1;
        }

        /// <summary>
        /// 같은 대상을 여러 번 뚫었으면 가까운 쪽을 남긴다.
        /// RaycastNonAlloc은 순서를 보장하지 않으므로 직접 견준다.
        /// </summary>
        void SetExactHit(int index, Vector3 point, float distance)
        {
            if (_hasExactHit[index] && distance >= _exactDistances[index]) return;
            _exactHits[index] = point;
            _exactDistances[index] = distance;
            _hasExactHit[index] = true;
        }

        /// <summary>
        /// 대상의 <b>눈에 보이는 몸</b>. 조준 판정은 이것으로 한다.
        ///
        /// 채집물에는 편의를 위해 몸보다 서너 배 큰 트리거가 붙어 있다
        /// (실측: 0.3m짜리 잔해에 1.1~1.6m 상자, 3.4m짜리 재 고사리에 4.7m 상자).
        /// 그 부피로 조준을 판정하면 옆을 스치기만 해도 "그것을 보고 있다"가 되어,
        /// 정작 겨눈 것이 진다. 그래서 렌더러가 그리는 범위를 먼저 쓰고,
        /// 없으면 단단한 콜라이더, 그것도 없으면 마지막에야 트리거를 쓴다.
        /// </summary>
        public static Bounds BodyBounds(Transform root)
        {
            var bounds = new Bounds();
            bool has = false;

            root.GetComponentsInChildren(true, RendererBuffer);
            for (int i = 0; i < RendererBuffer.Count; i++)
            {
                var r = RendererBuffer[i];
                if (r == null || !r.enabled) continue;
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                Encapsulate(ref bounds, ref has, r.bounds);
            }
            if (has) return bounds;

            root.GetComponentsInChildren(true, ColliderBuffer);
            for (int i = 0; i < ColliderBuffer.Count; i++)
            {
                var c = ColliderBuffer[i];
                if (c == null || c.isTrigger) continue;
                Encapsulate(ref bounds, ref has, c.bounds);
            }
            if (has) return bounds;

            for (int i = 0; i < ColliderBuffer.Count; i++)
            {
                var c = ColliderBuffer[i];
                if (c == null) continue;
                Encapsulate(ref bounds, ref has, c.bounds);
            }
            if (has) return bounds;

            return new Bounds(root.position, Vector3.zero);
        }

        static void Encapsulate(ref Bounds bounds, ref bool has, Bounds add)
        {
            if (!has) { bounds = add; has = true; }
            else bounds.Encapsulate(add);
        }

        // ── 가림 검사 ────────────────────────────────────────────

        /// <summary>
        /// 겨냥점에 닿기 전에 <b>다른 단단한 것</b>이 가로막고 있는가.
        ///
        /// 근접 타격이 "벽 너머는 때리지 못한다"를 지키는 것과 같은 결이다
        /// (<see cref="Survive.Combat.MeleeTargeting.IsOccluder"/>). 다만 거기서
        /// 지형과 소품을 일부러 뺀 것과 달리 여기서는 빼지 않는다. 겨냥점이 다르기
        /// 때문이다 — 근접은 경계 상자의 한가운데를 겨눠 흙 속을 향할 수 있지만,
        /// 여기서는 <b>시선이 스치는 지점</b>, 곧 화면에서 실제로 보이는 자리를 겨눈다.
        /// 흙에 반쯤 묻힌 광맥이라도 드러난 쪽을 향하므로 지형이 사이에 끼지 않는다.
        ///
        /// 세 가지는 막은 것으로 세지 않는다.
        /// <list type="bullet">
        /// <item>대상 자신의 표면 — 거기 닿는 것이 곧 명중이다.</item>
        /// <item>플레이어 자신의 몸 — 판정 원점이 머릿속이라 늘 먼저 걸린다.</item>
        /// <item>트리거 — 풀숲·채집 범위는 통과해 지나가는 것이지 벽이 아니다.</item>
        /// </list>
        /// </summary>
        public bool IsBlocked(in AimScore score)
        {
            Transform target = _roots[score.Id];
            Vector3 to = score.Point - _origin;

            float distance = to.magnitude;
            if (distance <= SurfaceSlack) return false;

            Vector3 dir = to / distance;
            int count = Physics.RaycastNonAlloc(_origin, dir, OcclusionHits, distance,
                                                Physics.DefaultRaycastLayers,
                                                QueryTriggerInteraction.Ignore);

            // 대상 표면에 먼저 닿으면 거기서 선을 끊는다. 그 너머는 이미 대상 안이다.
            float barrier = distance;
            for (int i = 0; i < count; i++)
                if (BelongsTo(OcclusionHits[i].collider, target) &&
                    OcclusionHits[i].distance < barrier)
                    barrier = OcclusionHits[i].distance;

            for (int i = 0; i < count; i++)
            {
                var hit = OcclusionHits[i];
                if (hit.distance >= barrier - SurfaceSlack) continue;
                if (BelongsTo(hit.collider, target)) continue;
                if (BelongsTo(hit.collider, Self)) continue;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 표면을 스치는 것으로 자기가 자기를 막지 않게 두는 여유(10cm).
        /// 지면에 반쯤 파묻힌 광맥·잔해가 자기 발치의 흙에 걸리는 것을 막는다.
        /// </summary>
        public const float SurfaceSlack = 0.1f;

        static bool BelongsTo(Collider col, Transform root) =>
            col != null && root != null &&
            (col.transform == root || col.transform.IsChildOf(root));
    }
}
