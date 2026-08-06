using UnityEngine;
using Survive.World;

namespace Survive.Creatures
{
    /// <summary>
    /// 낫의 꼬리를 상태에 맞춰 세우고, 그 꼬리를 따라 빛을 옮긴다
    /// (기획서 §4.5 "상태 표현 — 꼬리"·"아트", 스펙 §4).
    ///
    /// <b>이것은 UI다.</b> 게이지도 알림도 없이 상태를 읽게 하는 장치이고,
    /// <b>꼬리를 거두는 동작 자체가 경고</b>다 — AI 동료의 대사보다 먼저 온다.
    /// 그래서 판단은 여기에 없다. 무엇이 경계이고 무엇이 공격 태세인가는
    /// <see cref="ScytheStance"/>가 정하고, 여기서는 <b>그 답을 몸으로 옮기기만</b> 한다.
    /// <see cref="CreatureBrain"/>이 <see cref="CreatureDecision"/>에 대해 하는 일과 같다.
    ///
    /// <b>왜 프리팹을 고치지 않고 스스로 붙는가.</b> 씬·프리팹은 병합할 수 없는 단일
    /// 파일이라 여러 갈래가 동시에 손대면 한쪽을 버려야 한다.
    /// <see cref="HoverDrifter"/>·<see cref="CreatureCollisionGuard"/>·
    /// <see cref="RelicShedder"/>가 전부 같은 이유로 <see cref="CreatureBrain.Awake"/>에서 붙는다.
    ///
    /// <b>지금은 있는 본으로 할 수 있는 만큼만 한다.</b> 이 프리팹은 다리 달린 옛
    /// 포식자 모델이고(Thigh·Shin·Foot·Skull·Jaw가 그대로 있다), 꼬리 셋은 서로
    /// 부모-자식이 아니라 <b>루트의 형제</b>다. 그래서 본 체인을 굽히는 대신
    /// 셋을 <b>한 덩어리로</b> 꼬리 밑동에서 돌린다. 둥근 몸통과 진짜 꼬리 체인은
    /// 사람이 만든다(스펙 §17) — 그때 이 클래스는 각도를 애니메이터에 넘기고
    /// 빛만 맡으면 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ScytheTail : MonoBehaviour
    {
        /// <summary>꼬리를 이루는 부품들. 이 프리팹에 이미 있는 이름 그대로다.</summary>
        static readonly string[] SegmentNames = { "Tail", "TailBlade", "TailSpike" };

        /// <summary>날 — 응축된 빛을 맡는 부품. 나머지는 호를 맡는다.</summary>
        const string BladeName = "TailSpike";

        [Header("자세 (꼬리 밑동 기준 피치, 도)")]
        [Tooltip("작업 중 — 늘어뜨려 액면을 훑는다. 실루엣이 넓고 낮아야 한다")]
        [SerializeField] float trailingPitch = -62f;

        [Tooltip("경계 — 든다. 작업을 멈췄다는 뜻이다")]
        [SerializeField] float raisedPitch = 34f;

        [Tooltip("공격 태세 — 몸 위로 말아 올린다. 뭉치고 뾰족해야 한다")]
        [SerializeField] float furledPitch = 104f;

        [Tooltip("공격 태세에서 꼬리를 밑동 쪽으로 당기는 비율. 1이면 그대로, 작을수록 뭉친다")]
        [Range(0.2f, 1f)]
        [SerializeField] float furledTuck = 0.52f;

        [Tooltip("자세가 바뀌는 속도(도/초). 느리면 경고가 늦고, 빠르면 동작으로 안 읽힌다")]
        [SerializeField] float swingSpeed = 165f;

        [Header("빛 (에미션만 — 광원은 두지 않는다)")]
        [Tooltip("펼친 호가 낼 수 있는 최대 세기. 날에 구워 둔 2.2보다 세다 — 작업 중일 때는 꼬리가 이 개체에서 가장 밝은 것이어야 상태가 읽힌다")]
        [SerializeField] float arcIntensity = 3.2f;

        [Tooltip("날이 낼 수 있는 최대 세기. 응축되므로 호보다 세다")]
        [SerializeField] float bladeIntensity = 3.6f;

        [Tooltip("액면을 긋는 빛줄기의 세기. 멀리서 보이는 것은 이것이다")]
        [SerializeField] float contactIntensity = 2.1f;

        [Tooltip("빛이 따라오는 속도(1/s). 자세보다 조금 늦게 와야 꼬리가 먼저 읽힌다")]
        [SerializeField] float glowLerp = 4.5f;

        [Header("액면 빛줄기")]
        [Tooltip("수면 위에 그리는 줄기의 길이(m)")]
        [SerializeField] float streakLength = 3.2f;

        [Tooltip("줄기의 폭(m)")]
        [SerializeField] float streakWidth = 0.22f;

        [Tooltip("수면에서 띄우는 높이(m). 붙이면 z-파이팅이 난다")]
        [SerializeField] float streakLift = 0.03f;

        /// <summary>
        /// 발광색. <b>자홍 계열 안이다</b> — 매크로늄은 MARSO의 인공물이고 낫은 그것을
        /// 관리하는 유닛이므로 색이 곧 소속이다(<c>Survive.Domain.Art.ArtPalette</c>).
        /// Domain.Art를 참조하지 않고 값을 적은 것은 이 어셈블리가 그쪽을 물지 않기
        /// 때문이다. 값이 갈리지 않도록 <c>ArtPalette.Macronium</c>(#A12EE0)과 같은 수를 쓴다.
        /// </summary>
        static readonly Color Macronium = new Color(0xA1 / 255f, 0x2E / 255f, 0xE0 / 255f, 1f);

        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        CreatureBrain _brain;
        CreatureDefinitionSO _definition;
        HoverDrifter _drifter;

        Segment[] _segments;
        Renderer[] _arcRenderers;
        Renderer _bladeRenderer;
        Material[] _owned;

        Transform _streak;
        Material _streakMaterial;

        /// <summary>꼬리 밑동. 루트 로컬 좌표다 — 셋을 여기서 함께 돌린다.</summary>
        Vector3 _pivot;

        float _pitch;
        float _tuck = 1f;
        float _arc;
        float _contact;
        float _blade;

        /// <summary>
        /// 지금 꼬리가 말하고 있는 것. <b>검증이 눈이 아니라 값으로 확인하라고 연다</b> —
        /// 화면만 보면 "뾰족해 보인다"까지밖에 말할 수 없다.
        /// </summary>
        public ScythePosture Posture { get; private set; } = ScythePosture.Trailing;

        /// <summary>지금 어디가 얼마나 빛나는가. 목표값이지 화면에 도달한 값이 아니다.</summary>
        public ScytheGlow Glow { get; private set; }

        /// <summary>꼬리 부품을 실제로 찾았는가. 못 찾았으면 이 컴포넌트는 아무것도 하지 않는다.</summary>
        public bool HasTail => _segments != null && _segments.Length > 0;

        void Awake()
        {
            _brain = GetComponent<CreatureBrain>();
            _definition = GetComponent<Survive.Combat.CreatureHealth>()?.Definition;
            _drifter = GetComponent<HoverDrifter>();

            CollectSegments();
            if (!HasTail) return;

            PrepareGlowMaterials();
            BuildStreak();

            _pitch = trailingPitch;
            ApplyPose();
        }

        void OnDestroy()
        {
            // 인스턴스 머티리얼은 자동으로 정리되지 않는다. 개체수가 다섯으로 느는
            // 것이 이미 예정되어 있으므로(스펙 §5) 여기서 지운다.
            if (_owned != null)
                foreach (var m in _owned)
                    if (m != null) Destroy(m);

            if (_streakMaterial != null) Destroy(_streakMaterial);
        }

        void LateUpdate()
        {
            if (!HasTail) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Posture = Decide();
            Glow = ScytheStance.GlowFor(Posture, Zone);

            _pitch = Mathf.MoveTowards(_pitch, TargetPitch(Posture), swingSpeed * dt);
            _tuck = Mathf.MoveTowards(_tuck, Posture == ScythePosture.Furled ? furledTuck : 1f,
                                      dt * 1.6f);
            ApplyPose();

            float k = Mathf.Clamp01(dt * glowLerp);
            _arc = Mathf.Lerp(_arc, Glow.Arc, k);
            _contact = Mathf.Lerp(_contact, Glow.Contact, k);
            _blade = Mathf.Lerp(_blade, Glow.Blade, k);
            ApplyGlow();
        }

        /// <summary>지금 서 있는 자리. 부유하는 몸이 없으면 액면 위로 친다.</summary>
        HabitatZone Zone => _drifter != null ? _drifter.Zone : HabitatZone.Liquid;

        ScytheAlert Alert => _drifter != null ? _drifter.Alert : ScytheAlert.Calm;

        /// <summary>
        /// 판단은 <see cref="ScytheStance"/>에 묻는다. 여기서 하는 일은 값을 모으는 것뿐이고,
        /// 그 값들은 전부 <see cref="CreatureBrain"/>이 이미 재어 둔 것이다 —
        /// 거리를 여기서 다시 재면 두뇌가 보는 것과 꼬리가 보는 것이 갈린다.
        ///
        /// <b>위협 목록을 통째로 넘긴다</b>(스펙 §22). 거리 하나만 받으면 위협이 둘일 때
        /// 꼬리가 누구를 보고 있는지 스스로 다시 골라야 하고, 그러면 두뇌와 다른 규칙을
        /// 쓸 자리가 생긴다. 목록을 넘기면 고르는 일이 한 군데
        /// (<see cref="ThreatSelection"/>)에만 있다.
        /// </summary>
        ScythePosture Decide()
        {
            if (_brain == null || _definition == null) return ScythePosture.Trailing;

            var traits = CreatureTraits.From(_definition);
            return ScytheStance.PostureFor(traits, _brain.Threats, _brain.AggroLeft,
                                           _brain.State, Zone, Alert);
        }

        float TargetPitch(ScythePosture posture)
        {
            switch (posture)
            {
                case ScythePosture.Raised: return raisedPitch;
                case ScythePosture.Furled: return furledPitch;
                default: return trailingPitch;
            }
        }

        // ── 자세 ────────────────────────────────────────────────

        readonly struct Segment
        {
            public readonly Transform T;
            public readonly Vector3 BasePosition;
            public readonly Quaternion BaseRotation;

            public Segment(Transform t)
            {
                T = t;
                BasePosition = t.localPosition;
                BaseRotation = t.localRotation;
            }
        }

        void CollectSegments()
        {
            var found = new System.Collections.Generic.List<Segment>();
            var arcs = new System.Collections.Generic.List<Renderer>();

            foreach (var name in SegmentNames)
            {
                var t = transform.Find(name);
                if (t == null) continue;

                found.Add(new Segment(t));

                var r = t.GetComponent<Renderer>();
                if (r == null) continue;
                if (name == BladeName) _bladeRenderer = r;
                else arcs.Add(r);
            }

            _segments = found.ToArray();
            _arcRenderers = arcs.ToArray();
            if (_segments.Length > 0) _pivot = RootOf(_segments[0]);
        }

        /// <summary>
        /// 꼬리 밑동을 부품의 생김새에서 뽑아낸다.
        ///
        /// 이 부품들은 <b>길쭉한 상자</b>이고 긴 축이 로컬 +Z다(스케일의 z가 길이). 두 끝
        /// 중 <b>몸통 축에 가까운 쪽</b>이 밑동이다. 좌표를 상수로 박지 않는 이유는
        /// 모델이 교체될 예정이기 때문이다 — 새 꼬리가 와도 규칙이 따라 움직인다.
        /// </summary>
        static Vector3 RootOf(Segment tail)
        {
            Vector3 axis = tail.BaseRotation * Vector3.forward;
            float half = 0.5f * tail.T.localScale.z;

            Vector3 a = tail.BasePosition + axis * half;
            Vector3 b = tail.BasePosition - axis * half;

            float da = a.x * a.x + a.z * a.z;
            float db = b.x * b.x + b.z * b.z;
            return da <= db ? a : b;
        }

        /// <summary>
        /// 셋을 밑동에서 한 덩어리로 돌린다. <paramref name="_tuck"/>이 1보다 작으면
        /// 밑동 쪽으로 당겨져 뭉친다 — "뭉치고 뾰족하다"가 각도만으로는 안 나온다.
        /// </summary>
        void ApplyPose()
        {
            var swing = Quaternion.AngleAxis(_pitch, Vector3.right);

            for (int i = 0; i < _segments.Length; i++)
            {
                var s = _segments[i];
                if (s.T == null) continue;

                Vector3 offset = (s.BasePosition - _pivot) * _tuck;
                s.T.localPosition = _pivot + swing * offset;
                s.T.localRotation = swing * s.BaseRotation;
            }
        }

        // ── 빛 ──────────────────────────────────────────────────

        /// <summary>
        /// 발광을 켤 수 있는 머티리얼 인스턴스를 만든다.
        ///
        /// 꼬리의 몸통 부분은 Chassis 머티리얼을 쓰는데 그쪽은 <c>_EMISSION</c> 키워드가
        /// 꺼져 있다. 에셋을 고치면 <b>그 머티리얼을 쓰는 모든 부품</b>(허벅지·두개골까지)이
        /// 같이 빛나므로, 인스턴스를 떠서 이 개체의 꼬리에만 켠다.
        /// </summary>
        void PrepareGlowMaterials()
        {
            var owned = new System.Collections.Generic.List<Material>();

            foreach (var r in _arcRenderers) Adopt(r, owned);
            Adopt(_bladeRenderer, owned);

            _owned = owned.ToArray();
        }

        static void Adopt(Renderer r, System.Collections.Generic.List<Material> owned)
        {
            if (r == null) return;

            var m = new Material(r.sharedMaterial);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor(EmissionColorId, Color.black);

            r.sharedMaterial = m;
            owned.Add(m);
        }

        /// <summary>
        /// 액면을 긋는 빛줄기.
        ///
        /// <b>광원이 아니라 발광하는 면이다.</b> 환경광 0인 세계에서 광원을 하나 더 두면
        /// 그 둘레가 밝아져 "어둠을 지킨다"가 깨지고 아트 게이트에 걸린다. 얇은 판 하나를
        /// 수면에 띄우고 자홍으로 발광시키면, 멀리서 보이는 것이 몸통이 아니라
        /// <b>수면을 긋는 선</b>이 된다.
        /// </summary>
        void BuildStreak()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "TailStreak";

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                var source = _bladeRenderer != null ? _bladeRenderer.sharedMaterial
                           : (_arcRenderers.Length > 0 ? _arcRenderers[0].sharedMaterial : null);
                _streakMaterial = source != null ? new Material(source)
                                                 : new Material(r.sharedMaterial);
                _streakMaterial.EnableKeyword("_EMISSION");
                _streakMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                _streakMaterial.SetColor(EmissionColorId, Color.black);

                r.sharedMaterial = _streakMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            _streak = go.transform;
            _streak.SetParent(transform, worldPositionStays: false);
            _streak.localScale = new Vector3(streakWidth, streakLength, 1f);
            go.SetActive(false);
        }

        void ApplyGlow()
        {
            foreach (var r in _arcRenderers)
                if (r != null) r.sharedMaterial.SetColor(EmissionColorId, Macronium * (arcIntensity * _arc));

            if (_bladeRenderer != null)
                _bladeRenderer.sharedMaterial.SetColor(EmissionColorId,
                                                       Macronium * (bladeIntensity * _blade));

            PlaceStreak();
        }

        /// <summary>
        /// 줄기를 수면에 눕힌다. <b>꼬리 끝이 아니라 그 아래 수면</b>이 자리다 —
        /// 빛이 번지는 곳은 접점이기 때문이다. 액면이 없으면 그릴 곳도 없다.
        /// </summary>
        void PlaceStreak()
        {
            if (_streak == null) return;

            bool visible = _contact > 0.02f;
            if (_streak.gameObject.activeSelf != visible) _streak.gameObject.SetActive(visible);
            if (!visible) return;

            Vector3 here = transform.position;
            if (!WaterBody.TryGetSurfaceAt(new Vector3(here.x, 0f, here.z), out float surfaceY))
            {
                _streak.gameObject.SetActive(false);
                return;
            }

            Vector3 back = -transform.forward;
            back.y = 0f;
            if (back.sqrMagnitude < 1e-4f) back = Vector3.forward;
            back.Normalize();

            // 줄기는 몸 뒤로 끌린다. 꼬리가 뒤에 달려 있고, 지나간 자리에 남는 것이므로.
            _streak.position = new Vector3(here.x, surfaceY + streakLift, here.z)
                             + back * (streakLength * 0.45f);
            // 판의 앞면(로컬 +Z)이 위를 보고, 긴 축(로컬 +Y)이 뒤로 눕는다.
            _streak.rotation = Quaternion.LookRotation(Vector3.up, back);

            _streakMaterial.SetColor(EmissionColorId, Macronium * (contactIntensity * _contact));
        }
    }
}
