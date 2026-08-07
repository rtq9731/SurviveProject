using UnityEngine;
using Survive.Combat;
using Survive.Player;
using Survive.World;

namespace Survive.Creatures
{
    /// <summary>
    /// 다리 없이 <b>액면 위를 미끄러지는</b> 몸 (기획서 §4.5, 스펙 §3).
    ///
    /// <b>왜 <see cref="FlyerMotor"/>를 쓰지 않는가.</b> 셋이 다르다.
    /// <list type="number">
    /// <item><b>기준면.</b> 비행체는 <i>지면</i> 위 고도를 지킨다. 그 규칙을 바다에
    ///       적용하면 낫이 해저에서 2.5m 뜬 자리, 곧 <b>수면 아래</b>에 잠긴다
    ///       (실측 지형: 액면 50.1인데 바다 한가운데 해저는 47.0). 낫은 수면에
    ///       붙어야 한다 — 꼬리가 액체를 훑는 것이 이 개체가 하는 일이다</item>
    /// <item><b>갈 수 있는 곳.</b> 비행체는 어디든 간다. 낫은 <see cref="ScytheHabitat"/>가
    ///       허락하는 곳까지만 간다. 평시에 육지로 올라오지 않는 것이 안쪽 뭍을
    ///       안전하게 만드는 규칙 자체이므로, 몸이 그것을 어기면 설계가 무너진다</item>
    /// <item><b>움직이는 결.</b> 비행체는 초당 1.4번 위아래로 까딱이며 방향을 빠르게
    ///       튼다. 낫은 <b>관성으로 미끄러진다</b> — 멈추라고 해도 곧바로 서지 않고,
    ///       몸을 천천히 돌리고, 위아래로는 액면의 느린 너울만 탄다.
    ///       "걷지 않고 미끄러진다"가 멀리서도 식별 정보이려면(§4.5) 실루엣이
    ///       안 보이는 거리에서 <b>움직임만으로</b> 달라야 한다</item>
    /// </list>
    ///
    /// 판단은 여기에 없다. 어디로 갈지는 <see cref="CreatureDecision"/>이 정하고,
    /// 여기서는 <b>갈 수 있는 만큼만</b> 간다 — 벽 앞에서 미끄러지는
    /// <see cref="CreatureCollisionGuard"/>와 같은 문법이다.
    /// </summary>
    [DisallowMultipleComponent]
    public class HoverDrifter : MonoBehaviour, ICreatureMotor
    {
        [Tooltip("액면(또는 해안선 지형) 위로 띄울 높이. 꼬리가 액체에 닿는 높이다")]
        [SerializeField] float clearance = 0.6f;

        [Tooltip("붙는 가속(m/s^2). 낮을수록 미끄러지는 느낌이 강해진다")]
        [SerializeField] float acceleration = 9f;

        [Tooltip("멈추라는 지시를 받은 뒤 속도가 죽는 비율(1/s). 곧바로 서지 않는다")]
        [SerializeField] float coastDamping = 1.4f;

        [Tooltip("몸을 돌리는 속도. 걷는 것보다 느긋해야 미끄러지는 것으로 읽힌다")]
        [SerializeField] float turnSpeed = 2.4f;

        [Tooltip("액면의 너울. 보행 사이클이 아니라 느린 물결이다")]
        [SerializeField] float swellAmplitude = 0.1f;
        [SerializeField] float swellFrequency = 0.32f;

        [Tooltip("높이를 따라잡는 속도(1/s)")]
        [SerializeField] float riseLerp = 2.5f;

        [SerializeField] LayerMask groundMask = ~0;

        /// <summary>
        /// 지형을 찾으려고 훑기 시작하는, 액면(또는 몸) 위 여유(m).
        ///
        /// <b>작아야 한다.</b> 이 값만큼 위의 것까지 "발밑 지형"으로 읽히는데, 이 세계의
        /// 물가에는 거대 버섯의 갓이 y 56~70에 걸려 있다(실측). 넉넉히 잡으면 낫이
        /// 그 갓을 지면으로 삼아 기어 올라간다. 0.75면 한 프레임에 오를 수 있는
        /// 비탈(6.2 m/s로 0.1m 전진에 0.75m 상승 = 82도)을 다 덮고도 남는다.
        /// </summary>
        const float ProbeRise = 0.75f;

        /// <summary>액면 아래로 이만큼까지 지형을 찾는다(m).</summary>
        const float ProbeDepth = 30f;

        /// <summary>몸을 맞았을 때 다시 쏘는 지점을 그 표면보다 조금 아래로 내리는 값.</summary>
        const float ProbeSkipEpsilon = 0.01f;

        /// <summary>한 번의 훑기에서 걸러 낼 몸의 최대 개수.</summary>
        const int MaxProbeSkips = 8;

        /// <summary>막혔을 때 앞을 내다보는 거리(m). 몸통 반지름보다 조금 크게.</summary>
        const float LookAhead = 0.9f;

        /// <summary>해안선 띠의 윗선에서 남겨 두는 여유(m). <see cref="CanStand"/> 참고.</summary>
        const float EdgeMargin = 0.3f;

        /// <summary>서식지 밖에 놓였을 때 돌아갈 자리를 찾는 반경(m).</summary>
        static readonly float[] ReturnRadii = { 4f, 9f, 16f, 26f };

        /// <summary>
        /// 지금 경계 태세. 이 값이 <see cref="ScytheAlert.Alarmed"/>일 때만 육지로 올라온다.
        ///
        /// <b>읽기만 한다 — 이 몸은 등급을 갖고 있지 않다</b>(스펙 §0-2 ⑨ · §20).
        /// 값의 주인은 월드 하나(<see cref="ScytheWatch"/>)다. 개체마다 하나씩 들고
        /// 있으면 같은 프레임에 한 마리는 평시이고 다른 마리는 발령인 상태가 만들어질
        /// 수 있고, 그때 "다섯이 전부 꼬리를 들고 온다"는 종막의 그림이 개체별로 흩어진다.
        ///
        /// <b>setter를 두지 않는 것이 규칙의 내용이다.</b> 있으면 언젠가 개체 쪽 코드가
        /// 그것을 쓰고, 그 순간 소유권이 다시 갈라진다.
        /// </summary>
        public ScytheAlert Alert => ScytheWatch.Alert;

        /// <summary>
        /// 지금 나와 있는가 — 밤이면 참. <b>읽기만 한다</b>(<see cref="ScytheWatch"/>와 같은 결).
        /// 시계가 아직 없으면 밤으로 친다: 시간 축이 붙기 전과 같은 답이라,
        /// 시계 없는 무대에서 동작이 달라지지 않는다.
        /// </summary>
        public bool Abroad
        {
            get
            {
                var 시계 = DayNightService.Instance;
                return 시계 == null || ScytheHabitat.IsAbroad(시계.TimeOfDay, Alert);
            }
        }

        /// <summary>지금 서 있는 자리가 무엇인가. 검증이 눈이 아니라 값으로 확인하라고 연다.</summary>
        public HabitatZone Zone { get; private set; } = HabitatZone.Liquid;

        /// <summary>서식지 밖으로 밀려나 돌아가는 중인가.</summary>
        public bool Returning { get; private set; }

        /// <summary>
        /// 그 자리에 있어도 되는가 — <b>묻기만 하는 창구</b>.
        ///
        /// 재등장 자리를 고르는 쪽(<see cref="ScytheMind"/>)이 서식지 밖을 후보에서
        /// 미리 빼려고 쓴다. 규칙이 고른 뒤에 몸이 도로 돌아오면 옮긴 적이 없는 것과
        /// 같고, 그 판정을 저쪽에서 다시 짜면 <b>같은 물음에 답하는 곳이 둘</b>이 된다.
        /// </summary>
        public bool CanOccupy(Vector3 position) => CanStand(position);

        public float Speed { get; set; } = 3f;

        /// <summary>액면 위로 띄운 높이가 곧 순항 고도다. 너울은 이 값 둘레로 흔들릴 뿐이다.</summary>
        public float CruiseHeight => clearance;

        /// <summary>
        /// <b>너울을 뺀, 딛고 선 높이.</b> 화면에 보이는 몸은 여기에 너울을 더한 자리에
        /// 있지만, <b>무엇을 딛고 있는지를 물을 때는 이 값을 쓴다.</b>
        ///
        /// <b>왜 갈랐는가 (2026-08-07).</b> 예전에는 구역 판정이 <c>transform.position.y</c>를
        /// 그대로 썼고, 거기에는 ±0.1m 너울이 섞여 있었다. 그 y는 <see cref="Sample"/>의
        /// 지형 광선 시작 높이가 되므로, 물가에서 위로 까딱인 프레임에 광선이 한 뼘
        /// 높은 데서 출발해 <b>발밑에 없는 것을 지형으로 읽는</b> 일이 생겼다 —
        /// 「낫 꼬리」가 세던 「육지로 읽힌 프레임」이 그것이다.
        ///
        /// <b>너울은 보여 주기 위한 것이지 딛고 선 높이가 아니다.</b> 그것이 판정에
        /// 들어가 있던 것 자체가 결함이었고, 위상을 결정적으로 만든 것은 그 결함을
        /// <b>드러냈을 뿐</b>이다(늘 같은 위상이면 늘 같은 프레임에 흔들린다).
        /// 그래서 시드를 되돌리지 않고 <b>커플링을 끊었다</b> — 무작위로 돌려놓으면
        /// 흔들림이 판마다 다른 자리에서 다시 나타난다.
        /// </summary>
        float _baseY;
        bool _baseSet;

        Vector3 _velocity;
        Vector3 _target;
        float _swellPhase;
        bool _halted = true;

        /// <summary>
        /// 너울의 시작 위상. <b>눈만의 값이 아니라 상태다 — 그래서 주인 있는 난수로 뽑는다.</b>
        ///
        /// 이 위상이 <see cref="swellAmplitude"/>(0.1m)만큼 몸의 Y를 흔들고, 그 Y가
        /// <see cref="Sample"/>의 지형 광선 시작 높이로 들어가 <see cref="Zone"/>을
        /// 정한다. 해안선 띠의 폭이 0.75m이고 가장자리 여유가 0.3m이니, 0.1m 흔들림은
        /// 경계에 선 개체의 구역 판정을 뒤집기에 충분하다 — 「낫 꼬리」가 세던
        /// <b>「육지로 읽힌 프레임」</b>이 그 자리다.
        ///
        /// 자리를 시드에 넣으므로 <b>같은 세계의 같은 자리에 선 개체는 늘 같은 위상</b>으로
        /// 시작한다. 여럿이 한꺼번에 같은 박자로 까딱이지 않는 것은 자리가 서로 다르기
        /// 때문이지 난수가 매번 달라서가 아니다.
        /// </summary>
        void Start()
        {
            var rng = Survive.World.WorldSeed.Rng(
                Survive.World.WorldSeedBranch.Wander, transform.position, occasion: 1);
            _swellPhase = (float)rng.NextDouble() * Mathf.PI * 2f;
        }

        /// <summary>
        /// 이 자리를 <b>딛고 선 높이로</b> 다시 적은 것. 구역·지형을 묻는 모든 자리가
        /// 이것을 거친다 — 묻는 것이 "무엇을 딛고 있는가"이지 "지금 몇 미터에
        /// 떠 있는가"가 아니기 때문이다.
        /// </summary>
        Vector3 딛고선자리(Vector3 p) => _baseSet ? new Vector3(p.x, _baseY, p.z) : p;

        public void MoveTowards(Vector3 target)
        {
            _halted = false;
            _target = target;
        }

        public void Stop() => _halted = true;

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _swellPhase += dt * swellFrequency * Mathf.PI * 2f;

            Vector3 here = transform.position;

            // <b>누가 몸을 옮겼으면 딛고 선 높이도 따라간다.</b> 이 값은 제 걸음으로만
            // 조금씩 움직이는데, 검증이나 다른 부품이 순간이동시키면 옛 높이가 남아
            // 광선이 엉뚱한 데서 출발한다 — 육지에 올려 놓았는데 발밑이 비어
            // 「액면」으로 읽히는 일이 그것이다(실측: 다섯 판 연속).
            //
            // 가르는 자는 <b>너울 폭</b>이다. 그보다 크게 벌어졌으면 제가 흔들린 것이
            // 아니라 남이 옮긴 것이므로, 새 자리에 맞춰 다시 앉는다.
            if (!_baseSet || Mathf.Abs(here.y - _baseY) > swellAmplitude * 2f)
            {
                _baseY = here.y;
                _baseSet = true;
            }

            var sample = Sample(딛고선자리(here));
            Zone = sample.Zone;

            // 서식지 밖에 놓였으면 무엇을 쫓든 먼저 돌아간다. 은폐 프로토콜은
            // "가지 않는다"가 아니라 "있으면 안 된다"이므로, 밀려 들어간 개체도
            // 제자리로 돌아가야 규칙이 상태와 무관하게 성립한다.
            Vector3 goal = _target;
            Returning = !ScytheHabitat.CanEnter(sample.Zone, Alert, Abroad);
            if (Returning && TryFindWayHome(here, out var home)) goal = home;

            Steer(dt, here, goal);
            Glide(dt, here, sample);
        }

        /// <summary>수평 속도를 목표 쪽으로 기울인다. 관성이 남는 것이 이 몸의 결이다.</summary>
        void Steer(float dt, Vector3 here, Vector3 goal)
        {
            bool driving = Returning || !_halted;

            Vector3 toGoal = goal - here;
            toGoal.y = 0f;

            if (driving && toGoal.sqrMagnitude > 0.04f)
            {
                Vector3 wanted = toGoal.normalized * Speed;
                _velocity = Vector3.MoveTowards(_velocity, wanted, acceleration * dt);

                var planar = new Vector3(_velocity.x, 0f, _velocity.z);
                if (planar.sqrMagnitude > 0.04f)
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                                                          Quaternion.LookRotation(planar),
                                                          dt * turnSpeed);
            }
            else
            {
                // 멈추라고 해도 곧바로 서지 않는다. 걷는 것과 가장 크게 갈리는 자리다.
                _velocity = Vector3.Lerp(_velocity, Vector3.zero, Mathf.Clamp01(dt * coastDamping));
                if (_velocity.sqrMagnitude < 0.0004f) _velocity = Vector3.zero;
            }
        }

        /// <summary>
        /// 한 걸음 미끄러진다. <b>서식지 밖으로는 발을 내밀지 않는다</b> —
        /// 막히면 그 방향의 속도를 죽이고 남은 것으로 경계를 따라 흐른다.
        /// </summary>
        void Glide(float dt, Vector3 here, Sampled sample)
        {
            var step = new Vector3(_velocity.x * dt, 0f, _velocity.z * dt);
            if (step.sqrMagnitude > 1e-8f && !Returning)
            {
                step = Allowed(here, step);
                if (step.sqrMagnitude < 1e-8f) _velocity = Vector3.zero;
            }

            Vector3 next = here + step;

            // 높이는 옮겨 간 자리에서 다시 잰다. 해안선으로 올라서는 순간 기준면이
            // 액면에서 지형으로 바뀌므로, 옮기기 전 값을 쓰면 한 프레임씩 파묻힌다.
            var at = step.sqrMagnitude > 1e-8f ? Sample(딛고선자리(next)) : sample;

            // <b>딛고 선 높이는 너울을 빼고 따라간다.</b> 너울을 여기 섞으면
            // 그것이 다음 프레임의 지형 광선 시작 높이가 되어 판정으로 새어 든다.
            if (!_baseSet) { _baseY = at.FloatY; _baseSet = true; }
            _baseY = Mathf.Lerp(_baseY, at.FloatY, Mathf.Clamp01(dt * riseLerp));

            // 보이는 몸에만 너울을 얹는다.
            next.y = _baseY + Mathf.Sin(_swellPhase) * swellAmplitude;
            transform.position = next;
        }

        /// <summary>
        /// 이번 걸음에서 <b>실제로 갈 수 있는</b> 부분만 남긴다.
        /// 통째로 막히면 축 하나씩 떼어 본다 — 경계를 비스듬히 만났을 때
        /// 벽에 붙어 서는 대신 해안선을 따라 흐르게 하는 것이 목적이다.
        /// </summary>
        Vector3 Allowed(Vector3 here, Vector3 step)
        {
            // 한 프레임 걸음은 짧다. 그대로 재면 경계를 스치듯 넘은 다음에야 걸리므로
            // 몸통만큼 앞을 내다본다.
            Vector3 probe = step.normalized * Mathf.Max(step.magnitude, LookAhead);

            if (CanStand(here + probe)) return step;

            var alongX = new Vector3(step.x, 0f, 0f);
            if (alongX.sqrMagnitude > 1e-8f &&
                CanStand(here + alongX.normalized * Mathf.Max(alongX.magnitude, LookAhead)))
            {
                _velocity.z = 0f;
                return alongX;
            }

            var alongZ = new Vector3(0f, 0f, step.z);
            if (alongZ.sqrMagnitude > 1e-8f &&
                CanStand(here + alongZ.normalized * Mathf.Max(alongZ.magnitude, LookAhead)))
            {
                _velocity.x = 0f;
                return alongZ;
            }

            return Vector3.zero;
        }

        /// <summary>
        /// 그 자리에 발을 얹어도 되는가.
        ///
        /// <b>띠의 맨 끝을 조금 남겨 둔다</b>(<see cref="EdgeMargin"/>). 경계에 코를
        /// 박고 서면 지형 굴곡 몇 센티에 판정이 오락가락한다 — 실측으로 해안선 윗선
        /// 51.60에 대해 발밑이 51.61로 읽히는 프레임이 섞여, 규칙이 지켜지고 있는데도
        /// "육지에 들어갔다"가 한 프레임 찍혔다. 여유를 두면 자리 자체가 흔들리지 않는다.
        /// 남기는 폭은 몸통 반지름보다 작아 서식 범위가 실질적으로 좁아지지는 않는다.
        /// </summary>
        bool CanStand(Vector3 p)
        {
            var s = Sample(딛고선자리(p));
            if (!ScytheHabitat.CanEnter(s.Zone, Alert, Abroad)) return false;
            if (Alert == ScytheAlert.Alarmed) return true;
            return s.EdgeRoom > EdgeMargin;
        }

        /// <summary>
        /// 밀려 들어간 자리에서 가장 가까운 <b>돌아갈 수 있는</b> 자리.
        /// 반경을 넓혀 가며 열두 방향을 훑는다.
        /// </summary>
        bool TryFindWayHome(Vector3 here, out Vector3 home)
        {
            for (int r = 0; r < ReturnRadii.Length; r++)
            {
                for (int a = 0; a < 12; a++)
                {
                    Vector3 dir = Quaternion.Euler(0f, a * 30f, 0f) * Vector3.forward;
                    Vector3 candidate = here + dir * ReturnRadii[r];
                    if (!CanStand(candidate)) continue;

                    home = candidate;
                    return true;
                }
            }

            home = here;
            return false;
        }

        readonly struct Sampled
        {
            public readonly HabitatZone Zone;
            public readonly float FloatY;

            /// <summary>띠의 윗선까지 얼마나 남았는가(m). 음수면 이미 넘었다.</summary>
            public readonly float EdgeRoom;

            public Sampled(HabitatZone zone, float floatY, float edgeRoom)
            {
                Zone = zone;
                FloatY = floatY;
                EdgeRoom = edgeRoom;
            }
        }

        /// <summary>
        /// 이 수평 자리를 잰다 — 액체가 있는가, 받칠 지형이 어디까지 올라와 있는가.
        ///
        /// <b>액체를 묻는 높이를 몸의 높이로 두지 않는다.</b> "이 기둥에 액체가 있는가"는
        /// 수평만의 물음인데, <see cref="WaterBody.TryGetSurfaceAt"/>은 수면보다 한참
        /// 위에 있는 점을 그 물과 무관한 것으로 친다. 몸이 잠깐 튀어 오른 것만으로
        /// 바다가 사라져서는 안 된다.
        ///
        /// <b>지형은 위에서 얕게 내려다본다.</b> <see cref="FlyerMotor.GroundHeight"/>처럼
        /// 50m 위에서 쏘면 거대 버섯의 갓(실측 y 56~70)부터 맞고, 물가에 선 낫이
        /// "지형 높이 58"을 읽어 육지로 판정된다. 액면(또는 몸)에서 <see cref="ProbeRise"/>만
        /// 위에서 시작하면 머리 위의 것은 애초에 광선에 들어오지 않는다.
        /// </summary>
        Sampled Sample(Vector3 p)
        {
            bool hasLiquid = WaterBody.TryGetSurfaceAt(new Vector3(p.x, 0f, p.z), out float surfaceY);
            float reference = hasLiquid ? Mathf.Max(surfaceY, p.y) : p.y;

            bool hasGround = TryGroundHeight(p, reference, out float groundY);

            var zone = ScytheHabitat.Classify(hasLiquid, surfaceY, hasGround, groundY);
            float floatY = ScytheHabitat.FloatHeight(hasLiquid, surfaceY, hasGround, groundY, clearance);

            float edgeRoom = hasLiquid && hasGround
                           ? surfaceY + ScytheHabitat.ShoreRise - groundY
                           : float.MaxValue;

            return new Sampled(zone, floatY, edgeRoom);
        }

        bool TryGroundHeight(Vector3 p, float reference, out float groundY)
        {
            groundY = 0f;

            var origin = new Vector3(p.x, reference + ProbeRise, p.z);
            float remaining = ProbeRise + ProbeDepth + Mathf.Max(0f, p.y - reference);

            for (int i = 0; i < MaxProbeSkips; i++)
            {
                if (!Physics.Raycast(origin, Vector3.down, out var hit, remaining,
                                     groundMask, QueryTriggerInteraction.Ignore))
                    return false;

                if (!IsSoft(hit.collider))
                {
                    groundY = hit.point.y;
                    return true;
                }

                float step = hit.distance + ProbeSkipEpsilon;
                origin += Vector3.down * step;
                remaining -= step;
                if (remaining <= 0f) return false;
            }

            return false;
        }

        /// <summary>지형이 아닌 것 — 자기 몸, 다른 생물, 사람.</summary>
        bool IsSoft(Collider col)
        {
            var t = col.transform;
            if (t.IsChildOf(transform) || transform.IsChildOf(t)) return true;
            if (col.GetComponentInParent<CreatureHealth>() != null) return true;
            if (col.GetComponentInParent<PlayerContext>() != null) return true;
            if (col.GetComponentInParent<CharacterController>() != null) return true;
            return false;
        }
    }
}
