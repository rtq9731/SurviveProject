using System.Collections.Generic;
using UnityEngine;
using Survive.World;

namespace Survive.Creatures
{
    /// <summary>
    /// 낫 하나가 지금 어떤 상태인가를 <b>들고 있는</b> 자리 (스펙 §20).
    ///
    /// <b>왜 <see cref="CreatureBrain"/>이 아닌가.</b> 4상태는 낫만 갖는다. 범용 두뇌에
    /// 낫 전용 필드를 심으면 다음 생물이 그 필드를 물려받고, 물려받은 필드는 언젠가
    /// 쓰인다. 이 저장소는 이미 그 답을 갖고 있다 — <see cref="HoverDrifter"/>도
    /// <see cref="ScytheTail"/>도 <b>낫에만 붙는 부품</b>이고, 프리팹을 고치지 않고
    /// <c>CreatureBrain.Awake</c>가 정의(SO)를 보고 붙인다. 이것도 같은 자리에 선다.
    ///
    /// <b>규칙을 갈아치우지 않고 그 위에 얹었다.</b> 어디로 갈지는 여전히
    /// <see cref="CreatureDecision"/>이 정한다 — 갈아치우면 낫만 다른 코드로 돌기
    /// 시작하고, 그때부터 나머지 넷과 낫의 감촉이 따로 움직인다. 여기서 하는 일은
    /// <b>범용 판단이 할 수 없는 것 둘</b>뿐이다.
    /// <list type="number">
    /// <item><b>따라붙기를 사건으로 푼다.</b> 고정 조명에 사람이 들어서면 그 프레임에
    ///       어그로를 끊는다. 범용 판단은 빛에 막히면 어그로를 <i>채우지 않을</i> 뿐이라
    ///       실제로 끊기까지 최대 6초가 걸리고, 그러면 "시간으로 풀지 않는다"가 거짓이 된다</item>
    /// <item><b>재등장.</b> 같은 방향에 머무르지 않게 자리를 옮긴다
    ///       (<see cref="ScytheReappearance"/>)</item>
    /// </list>
    ///
    /// <b>판정이 갈라지지 않는다는 것</b>은 두 가지로 지킨다. 첫째, 상황을 만드는 값이
    /// 전부 두뇌가 이미 잰 것이다 — 거리도 빛도 여기서 다시 재지 않는다. 둘째,
    /// 꼬리가 보는 상태도 <b>여기 저장된 것 하나</b>다(<see cref="ScytheTail"/>).
    /// 통역 함수(<c>ScytheFsm.FromCreatureState</c>)는 이 부품이 없는 경우의 대비책으로만 남는다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CreatureBrain))]
    public class ScytheMind : MonoBehaviour
    {
        /// <summary>
        /// 재등장 후보를 사람 둘레 몇 방향에서 찾는가.
        ///
        /// <b>성기게 깔면 쓸 수 있는 자리를 통째로 놓친다.</b> 앞쪽은 눈에 보여서,
        /// 뒤쪽은 사각이라서 빠지므로 실제로 남는 것은 <b>양옆의 좁은 띠</b>다.
        /// 30도 간격(12곳)으로는 그 띠에 후보가 한 곳도 안 떨어지는 판이 나왔고
        /// (실측: 통과한 자리 0개로 재등장이 멎었다), 15도로 좁히면 띠마다 두세 곳이
        /// 들어온다. 6초에 한 번 도는 계산이라 스물넷을 재도 값이 싸다.
        /// </summary>
        const int CandidateCount = 24;

        /// <summary>
        /// 가려는 자리가 눈에 들어오는지 볼 때, 시야각의 어느 선까지를 "보인다"로 치는가.
        ///
        /// 카메라의 실제 화각을 쓰되 <b>가로로 넓다</b>는 것을 감안해 절반보다 조금
        /// 넉넉히 잡는다. 좁게 잡으면 화면 가장자리에 툭 나타나는 것이 통과한다.
        /// </summary>
        const float ViewMargin = 1.4f;

        CreatureBrain _brain;
        HoverDrifter _drifter;
        CreatureDefinitionSO _definition;

        ScytheState _state = ScytheState.Patrol;
        ScytheDirective _directive = ScytheDirective.None;

        float _lastThreatDistance = float.MaxValue;
        float _sinceReappear;

        readonly List<ReappearSpot> _spots = new List<ReappearSpot>(CandidateCount);
        readonly List<Vector3> _spotPositions = new List<Vector3>(CandidateCount);

        /// <summary>
        /// 지금 상태. <b>쓰기는 없다</b> — 바꾸는 것은 규칙(<see cref="ScytheFsm"/>)의
        /// 답과 월드의 지시(<see cref="Order"/>)뿐이다.
        /// </summary>
        public ScytheState State => _state;

        /// <summary>마지막으로 자리를 옮긴 뒤 흐른 시간. 검증이 리듬을 값으로 확인한다.</summary>
        public float SinceReappear => _sinceReappear;

        /// <summary>이번 판에 자리를 몇 번 옮겼는가. §20 게이트 8이 세는 값이다.</summary>
        public int ReappearCount { get; private set; }

        /// <summary>마지막으로 고른 자리. 옮기지 못했으면 제자리다.</summary>
        public Vector3 LastReappearTarget { get; private set; }

        /// <summary>
        /// 마지막 재등장 판단에서 <b>조건 셋을 통과한 자리가 몇 개였는가</b>.
        /// 고정 조명 앞에서 자리가 줄어드는 것을 실측이 값으로 확인한다.
        /// </summary>
        public int LastAcceptableCount { get; private set; }

        /// <summary>
        /// 월드가 상태를 지정하는 통로 (스펙 §20). <b>개체 쪽에서는 아무도 부르지 않는다</b> —
        /// 코어가 떨어지는 것은 월드에서 벌어지는 사건이고, §9가 서면 그쪽이 부른다.
        /// </summary>
        public void Order(ScytheDirective directive) => _directive = directive;

        void Awake()
        {
            _brain = GetComponent<CreatureBrain>();
            _drifter = GetComponent<HoverDrifter>();
            _definition = GetComponent<Survive.Combat.CreatureHealth>()?.Definition;
            LastReappearTarget = transform.position;
        }

        void Update()
        {
            if (_definition == null || _brain == null) return;
            if (_brain.State == CreatureState.Dead) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _sinceReappear += dt;

            var situation = Observe();

            // <b>한 프레임에 정확히 한 번, 최대 한 단계.</b> Next가 이력을 보므로 두 번
            // 부르면 Patrol에서 Attack까지 한 프레임에 건너뛴다 — 그러면 "꼬리를 드는
            // 경고가 공격보다 먼저 온다"가 어떤 프레임에서는 지켜지지 않는다.
            // 이 규칙은 ScytheFsmTests가 두 번 부른 결과를 직접 적어 못 박고 있다.
            var before = _state;
            _state = ScytheFsm.Apply(_state, _directive, situation);

            // 지시는 한 프레임만 산다. 남겨 두면 월드가 취소를 잊었을 때 개체가
            // 영원히 그 지시에 묶인다.
            if (_directive != ScytheDirective.None) _directive = ScytheDirective.None;

            if (before != _state) OnEntered(before, _state);
            if (_state == ScytheState.Beware) Follow(situation);

            _lastThreatDistance = _brain.ThreatDistance;
        }

        /// <summary>
        /// 이번 프레임의 상황. <b>여기서 새로 재는 것은 둘뿐이다</b> — 간격이 줄고
        /// 있는가와 사람이 고정 조명 안인가. 나머지는 두뇌가 이미 잰 것을 그대로 쓴다.
        /// </summary>
        ScytheSituation Observe()
        {
            var traits = CreatureTraits.From(_definition);
            var roster = _brain.Threats;
            bool selfInLight = _definition.avoidsLight && LitZoneRegistry.IsLit(transform.position);
            var senses = roster.Senses(traits, _brain.AggroLeft, 0f, selfInLight);

            bool detected = CreatureDecision.IsDetected(senses.DistanceToThreat, traits.DetectRadius);
            var light = CreatureDecision.JudgeLight(traits, senses);

            // "이동속도 조건"(§20). 숫자를 들지 않고 <b>간격이 줄고 있는가</b>로 읽는다 —
            // 달아나는 사람과 멈춰 선 사람이 같은 대접을 받으면 안 되고, 속도 값은
            // 튜닝으로 바뀌므로 규칙이 그것을 알면 안 된다.
            float now = _brain.ThreatDistance;
            bool closing = now <= _lastThreatDistance;

            var target = _brain.Target;
            bool nearFixedLight = target != null && LitZoneRegistry.IsLitByFixed(target.position);

            return new ScytheSituation(detected, light, closing, nearFixedLight,
                                       pushedByFlare: false, alert: ScytheWatch.Alert);
        }

        /// <summary>상태에 <b>처음 들어선</b> 프레임에만 하는 일.</summary>
        void OnEntered(ScytheState from, ScytheState to)
        {
            if (to == ScytheState.Patrol)
            {
                // <b>따라붙기가 풀리는 순간이 여기다.</b> 어그로를 그 자리에서 끊지 않으면
                // 규칙은 Patrol이라고 말하는데 몸은 최대 6초를 더 쫓는다 — 두 판정이
                // 갈라지고, 꼬리는 작업 자세인데 몸은 달려오는 프레임이 생긴다.
                _brain.ReleaseAggro();
                _sinceReappear = 0f;
            }
            else if (to == ScytheState.Beware && from == ScytheState.Patrol)
            {
                // <b>새로 붙기 시작한 때만 시계를 되돌린다.</b> 막 따라붙기 시작한 개체가
                // 그 프레임에 순간이동하면 사람이 무엇을 본 것인지 알 수 없다.
                //
                // <b>교전에서 밀려 내려온 경우는 되돌리지 않는다.</b> 되돌리면 붙었다
                // 밀렸다를 반복하는 동안 시계가 영영 차지 않아, 교전이 한창일 때 —
                // 곧 재등장이 가장 값을 하는 때 — 자리를 한 번도 바꾸지 않는다.
                // 실측: 되돌리던 판에서는 48초에 두 번뿐이었다. 밀려난 직후야말로
                // "멀어졌다가 다른 데서 나타난다"가 성립하는 자리다.
                _sinceReappear = 0f;
            }
        }

        /// <summary>
        /// 따라붙는 중에 하는 일 — <b>같은 방향에 머무르지 않는다</b>.
        /// 쫓아가는 것 자체는 여전히 <see cref="CreatureDecision"/>이 시킨다.
        /// </summary>
        void Follow(in ScytheSituation situation)
        {
            if (!ScytheReappearance.DueToMove(_sinceReappear, _definition.aggroSeconds)) return;

            var target = _brain.Target;
            if (target == null) return;

            _sinceReappear = 0f;

            Vector3 anchor = target.position;
            Vector3 here = transform.position;

            // <b>거리는 그대로 두고 방위만 바꾼다.</b> 새 거리 값을 정하면 그것이 또 하나의
            // 튜닝 값이 되고, 감지 반경 밖으로 나가 버리면 따라붙기 자체가 풀린다.
            // "멀어졌다가 다른 방향에서 나타난다"에서 바뀌는 것은 방향이다.
            float radius = Vector3.Distance(new Vector3(here.x, 0f, here.z),
                                            new Vector3(anchor.x, 0f, anchor.z));
            if (radius < 0.5f) return;

            BuildCandidates(anchor, here, radius);
            LastAcceptableCount = ScytheReappearance.CountAcceptable(anchor, here, _spots);

            int pick = ScytheReappearance.Pick(anchor, here, _spots);
            if (pick == ScytheReappearance.Stay)
            {
                // <b>머무는 것이 답인 자리가 있다.</b> 고정 조명 앞에서는 통과하는 자리가
                // 없어지고, 그때 억지로 옮기면 규칙이 스스로 어긴 자리(빛 안이거나
                // 빤히 보이는 자리)로 간다.
                LastReappearTarget = here;
                return;
            }

            LastReappearTarget = _spotPositions[pick];
            transform.position = _spotPositions[pick];

            // 이 프로젝트는 Physics.autoSyncTransforms가 꺼져 있다. 옮긴 직후의
            // Collider.bounds와 광선 질의는 옛 자리를 답한다 — 부유체가 발밑 지형을
            // 재는 것이 곧 광선이므로, 맞춰 두지 않으면 한 프레임 동안 엉뚱한
            // 구역으로 읽힌다.
            Physics.SyncTransforms();

            ReappearCount++;
        }

        /// <summary>
        /// 사람 둘레에 후보를 깐다. <b>재는 일만 한다</b> — 고르는 것은
        /// <see cref="ScytheReappearance"/>다.
        /// </summary>
        void BuildCandidates(Vector3 anchor, Vector3 here, float radius)
        {
            _spots.Clear();
            _spotPositions.Clear();

            var eye = Camera.main;

            for (int i = 0; i < CandidateCount; i++)
            {
                float rad = i * (360f / CandidateCount) * Mathf.Deg2Rad;
                var p = new Vector3(anchor.x + Mathf.Sin(rad) * radius,
                                    here.y,
                                    anchor.z + Mathf.Cos(rad) * radius);

                // 서식지 밖은 애초에 후보가 아니다. 규칙이 고르고 나서 몸이 도로
                // 돌아오면 옮긴 적이 없는 것과 같다.
                if (_drifter != null && !_drifter.CanOccupy(p)) continue;

                // <b>등 뒤 사각으로는 나타나지 않는다.</b> 사각은 교전이 열리는 자리인데
                // (§19), 거기로 순간이동하면 <b>사람이 마주 보고 있어도 그 프레임에
                // 등 뒤를 잡힌다</b> — "플레이어 조작 자체가 방어가 된다"는 §19의 축이
                // 통째로 무너진다. 실측으로 「랜턴 오프셋」 시나리오가 정확히 그것을
                // 잡아냈다("마주보는 동안 낫이 사각에 든 적이 없다"가 깨졌다).
                //
                // 사각을 노리는 것 자체는 맞다. 다만 그것은 <b>움직여서</b> 도달해야
                // 하는 자리이지 나타나는 자리가 아니다. 옆으로 나타나 거기서부터
                // 파고드는 것이 규칙 둘을 함께 지킨다.
                if (LitZoneRegistry.IsBlindSide(p)) continue;

                _spotPositions.Add(p);
                _spots.Add(new ReappearSpot(p, LitZoneRegistry.IsLit(p), Seen(eye, p)));
            }
        }

        /// <summary>
        /// 그 자리가 지금 사람 눈에 들어와 있는가.
        ///
        /// <b>「이동 구간」이 아니라 도착점을 잰다.</b> 옮기는 방식이 순간이동에 페이드라
        /// 지나가는 구간이 아예 없기 때문이다(기획서 §4.5 "어둠이라 페이드만으로 된다").
        /// 규칙이 막으려던 것 — <b>옆으로 도는 것이 보이는 일</b> — 은 구간이 없으므로
        /// 성립할 수 없고, 남는 위험은 빤히 보이는 자리에 툭 나타나는 것 하나다.
        /// 걸어서 옮기게 바뀌면 이 함수가 구간을 재도록 바뀌어야 한다.
        /// </summary>
        bool Seen(Camera eye, Vector3 p)
        {
            if (eye == null) return false;

            Vector3 toSpot = p - eye.transform.position;
            float dist = toSpot.magnitude;
            if (dist < 0.01f) return true;

            float half = eye.fieldOfView * 0.5f * ViewMargin;
            if (Vector3.Angle(eye.transform.forward, toSpot) > half) return false;

            // 화각 안이어도 사이에 지형이 있으면 보이지 않는다.
            return !Physics.Raycast(eye.transform.position, toSpot.normalized, dist * 0.95f,
                                    ~0, QueryTriggerInteraction.Ignore);
        }
    }
}
