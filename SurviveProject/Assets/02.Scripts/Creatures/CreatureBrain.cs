using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Survive.Combat;
using Survive.Player;
using Survive.World;

namespace Survive.Creatures
{
    /// <summary>
    /// 생물의 상태머신. 실제 이동은 <see cref="CreatureDefinitionSO.locomotion"/>에 따라
    /// 셋 중 하나가 맡는다 — 지상은 NavMeshAgent, 비행은 <see cref="FlyerMotor"/>,
    /// 부유는 <see cref="HoverDrifter"/>.
    /// 챕터 1의 4종(눈·공·날개·열매게)은 전부 Skittish 또는 Defensive다.
    ///
    /// <b>판단은 여기에 없다.</b> 무엇으로 전이할지, 그 상태에서 몸이 무엇을 할지는
    /// <see cref="CreatureDecision"/>이 정한다. 이 컴포넌트가 하는 일은 셋뿐이다 —
    /// 값을 모으고(거리·시간), 답을 묻고, 답대로 움직인다.
    /// 그래야 감지 반경 경계 같은 것을 씬 없이 확인할 수 있다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CreatureHealth))]
    public class CreatureBrain : MonoBehaviour
    {
        [SerializeField] CreatureDefinitionSO definition;
        [SerializeField] NavMeshAgent agent;
        [SerializeField] FlyerMotor flyer;

        [Tooltip("배회 반경")]
        [SerializeField] float wanderRadius = 6f;

        [Tooltip("도주 목표 거리")]
        [SerializeField] float fleeDistance = 12f;

        CreatureHealth _health;
        CreatureFeeding _feeding;
        ScavengerBehavior _scavenger;

        /// <summary>
        /// NavMeshAgent가 아닌 몸. 비행(<see cref="FlyerMotor"/>)과
        /// 부유(<see cref="HoverDrifter"/>)가 같은 창구로 들어온다.
        /// </summary>
        ICreatureMotor _motor;

        Transform _player;
        CreatureState _state = CreatureState.Idle;
        float _stateTimer;
        float _aggroLeft;
        float _nextAttackTime;
        Vector3 _homePosition;
        Vector3 _destination;

        // 매 프레임 델리게이트를 새로 만들지 않도록 Awake에서 한 번만 묶는다.
        TryGetDestination _feedProbe;
        TryGetDestination _scavengeProbe;

        /// <summary>
        /// 이번 프레임에 잰 위협들과 그 몸. <b>두 목록의 같은 자리가 같은 위협이다.</b>
        ///
        /// <b>지금 담기는 것은 언제나 하나 아니면 없음이다</b> — 두 번째 플레이어를
        /// 만드는 것은 스펙 §22가 명시적으로 범위 밖에 둔 일이다. 여기서 여는 것은
        /// <b>담는 그릇</b>뿐이고, 그래서 길이 1일 때 판단의 입력이 예전과 필드
        /// 하나까지 같다. 목록을 들고 있는 것은 매 프레임 할당을 만들지 않기 위해서다.
        /// </summary>
        readonly List<ThreatSighting> _threats = new List<ThreatSighting>(1);
        readonly List<Transform> _threatBodies = new List<Transform>(1);

        ThreatRoster _roster = ThreatRoster.Empty;

        /// <summary>
        /// 지금 무엇을 하고 있는가. 읽기 전용이다 — 상태를 바꾸는 것은 여전히
        /// <see cref="CreatureDecision"/>의 답뿐이다.
        ///
        /// E2E가 "쫓고 있는가 / 물러났는가"를 눈이 아니라 값으로 확인해야 해서 연다.
        /// 화면으로 보면 "가까워 보인다" 정도밖에 말할 수 없고, 빛에 막혀 멈춘 것과
        /// 길이 막혀 멈춘 것을 구별하지 못한다.
        /// </summary>
        public CreatureState State => _state;

        /// <summary>남은 어그로 시간. 추격을 언제 포기하는지 확인할 때 쓴다.</summary>
        public float AggroLeft => _aggroLeft;

        /// <summary>
        /// 이번 프레임에 잰 위협까지의 거리. 위협이 없으면 <see cref="float.MaxValue"/>다.
        ///
        /// <b>왜 여는가.</b> 표현하는 쪽(<see cref="ScytheTail"/>)이 같은 판단을 다시
        /// 하려면 같은 입력이 필요한데, 거리를 저쪽에서 따로 재면 두뇌가 보는 것과
        /// 꼬리가 보는 것이 갈린다 — 두뇌는 쫓기 시작했는데 꼬리는 아직 늘어져 있는
        /// 프레임이 그렇게 생긴다. 재는 곳은 한 군데여야 한다.
        /// </summary>
        public float ThreatDistance { get; private set; } = float.MaxValue;

        /// <summary>
        /// 이번 프레임에 이 개체가 아는 위협 목록. <b>이 프레임 안에서만 유효하다</b> —
        /// 안쪽 목록을 재사용하기 때문이다.
        ///
        /// 표현하는 쪽(<see cref="ScytheTail"/>)이 같은 판단을 다시 하려면 두뇌가 본
        /// 것과 <b>같은 목록</b>을 봐야 한다. 거리 하나만 넘기면 "빛 안인가"를 위협마다
        /// 물을 수 없어, 등 뒤 사각(스펙 §19)이 붙는 순간 둘의 답이 갈린다.
        /// </summary>
        public ThreatRoster Threats => _roster;

        /// <summary>
        /// NavMeshAgent가 아닌 몸. 검증이 "무엇으로 움직이는 개체인가"를 값으로
        /// 확인하고, 부유체의 서식 범위를 집어 보라고 연다.
        /// </summary>
        public ICreatureMotor Motor => _motor;

        void Awake()
        {
            _health = GetComponent<CreatureHealth>();
            if (definition == null) definition = _health.Definition;
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (flyer == null) flyer = GetComponent<FlyerMotor>();
            _homePosition = transform.position;

            _feeding = GetComponent<CreatureFeeding>();
            _scavenger = GetComponent<ScavengerBehavior>();

            _feedProbe = TryGetFeedTarget;
            _scavengeProbe = TryGetScavengeTarget;

            _motor = flyer;

            // 부유체는 NavMesh 위를 걷지 않는다. 프리팹을 고치지 않고 여기서 갈아 끼우는
            // 것은 CreatureCollisionGuard·RelicShedder와 같은 이유다 — 씬과 프리팹은
            // 병합할 수 없는 단일 파일이고, 이동 방식은 이미 정의(SO)에 적혀 있다.
            //
            // 에이전트를 <b>끄기만 하고 지우지 않는다.</b> 프리팹에 물려 있는 참조를
            // 끊으면 다음에 정의를 되돌려도 걷는 몸이 살아나지 않는다.
            if (definition != null && definition.locomotion == LocomotionType.Hovering)
            {
                if (agent != null)
                {
                    agent.enabled = false;
                    agent = null;
                }

                var drifter = GetComponent<HoverDrifter>();
                if (drifter == null) drifter = gameObject.AddComponent<HoverDrifter>();
                _motor = drifter;

                // 꼬리가 상태를 말한다(스펙 §4). 부유하는 몸 다음에 붙이는 것은
                // ScytheTail이 그 몸에서 구역과 태세를 읽기 때문이다.
                // 꼬리 부품이 없는 프리팹에 붙어도 스스로 아무것도 하지 않는다.
                if (GetComponent<ScytheTail>() == null) gameObject.AddComponent<ScytheTail>();
            }

            if (agent != null && definition != null) agent.speed = definition.moveSpeed;
            if (_motor != null && definition != null) _motor.Speed = definition.moveSpeed;

            // NavMeshAgent도 FlyerMotor도 물리를 보지 않는다. 벽 앞에서 멈추는 일은
            // 이동이 끝난 뒤에 따로 처리한다. 씬·프리팹을 고치지 않으려고 여기서 붙인다.
            if (GetComponent<CreatureCollisionGuard>() == null)
                gameObject.AddComponent<CreatureCollisionGuard>();

            // 유물을 흘리는 종만 부품을 하나 더 단다. 같은 이유로 여기서 붙인다 —
            // 낫 프리팹을 고치지 않고도 순찰 드롭이 성립해야 한다.
            if (definition != null && definition.relicShed != null &&
                GetComponent<RelicShedder>() == null)
                gameObject.AddComponent<RelicShedder>();

            // 기척을 내는 종만 재는 컴포넌트를 단다. 소리가 없는 종에는 아예 붙지
            // 않으므로 Update 하나가 늘지 않는다 — 지금은 모든 종이 그 상태다.
            if (definition != null && definition.approachCue != null &&
                GetComponent<CreatureApproachAudio>() == null)
                gameObject.AddComponent<CreatureApproachAudio>().Bind(definition);
        }

        void OnEnable()
        {
            _health.Died += OnDied;
            _health.Damaged += OnDamaged;
        }

        void OnDisable()
        {
            _health.Died -= OnDied;
            _health.Damaged -= OnDamaged;
        }

        void OnDied(CreatureHealth _)
        {
            _state = CreatureState.Dead;
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        }

        void OnDamaged(CreatureHealth _, DamageInfo info)
        {
            if (definition == null) return;

            switch (CreatureDecision.ReactToDamage(definition.behavior))
            {
                case DamageReaction.Flee:
                    TransitionTo(CreatureState.Flee);
                    break;

                case DamageReaction.Retaliate:
                    _aggroLeft = definition.aggroSeconds;
                    TransitionTo(CreatureState.Chase);
                    break;
            }
        }

        void Update()
        {
            if (_state == CreatureState.Dead || definition == null) return;

            if (_player == null)
            {
                var ctx = UnityEngine.Object.FindAnyObjectByType<PlayerContext>(FindObjectsInactive.Exclude);
                if (ctx != null) _player = ctx.transform;
            }

            _stateTimer -= Time.deltaTime;
            if (_aggroLeft > 0f) _aggroLeft -= Time.deltaTime;

            SenseThreats();

            UpdateState();
            RunState();
        }

        /// <summary>
        /// 이번 프레임의 위협 목록을 채운다.
        ///
        /// <b>찾는 방식은 예전 그대로다</b> — 플레이어 하나를 찾아 캐시한다. 바뀐 것은
        /// 그 하나를 <b>길이 1짜리 목록</b>으로 넘긴다는 것뿐이고, 그래서 만들어지는
        /// 감각(<see cref="CreatureSenses"/>)이 예전에 손으로 만들던 것과 같다.
        /// 여럿을 실제로 모으는 일은 코옵 구현의 몫이고, 그때 고칠 곳은 이 함수 하나다.
        /// </summary>
        void SenseThreats()
        {
            _threats.Clear();
            _threatBodies.Clear();

            if (_player != null)
            {
                _threats.Add(new ThreatSighting(
                    Vector3.Distance(transform.position, _player.position),
                    Lit(_player.position),
                    BlindSide()));
                _threatBodies.Add(_player);
            }

            _roster = ThreatRoster.From(_threats);
            ThreatDistance = _roster.NearestDistance;
        }

        /// <summary>
        /// 규칙이 고른 위협의 몸. <b>고르는 일은 Domain이 하고 여기서는 자리만 되돌려
        /// 받는다</b> — 지금까지 이 함수가 아예 없던 것은 고를 것이 하나뿐이었기
        /// 때문이다(스펙 §0-2 ⑧ "타겟 선정 로직이 아예 없다").
        ///
        /// 정의가 없으면 <c>default</c> 성질을 넘긴다. 그것은 빛을 꺼리지 않는 성질이라
        /// 규칙이 "가장 가까운 것"으로 접히고, 곧 이 함수가 예전에 하던 일과 같다.
        /// </summary>
        Transform Target
        {
            get
            {
                var traits = definition != null ? CreatureTraits.From(definition) : default;
                int i = CreatureDecision.SelectThreat(traits, _roster);
                return i >= 0 && i < _threatBodies.Count ? _threatBodies[i] : null;
            }
        }

        void UpdateState()
        {
            var traits = CreatureTraits.From(definition);
            bool selfInLight = Lit(transform.position);

            // 어그로를 갱신하기 <b>전</b>의 값. 두 물음이 같은 프레임의 같은 입력을
            // 봐야 한다 — 갱신한 값으로 전이를 물으면 그 프레임부터 어그로가 살아
            // 있는 것으로 보여, 예전 코드가 감각 하나를 두 번 쓰던 것과 답이 갈린다.
            float aggro = _aggroLeft;

            // 선공 성향은 위협을 보고 있는 동안 어그로를 계속 채운다. 그래야 놓친 뒤에
            // 곧바로 흥미를 잃지 않고, 그 시간이 다하면 거처로 돌아간다.
            if (CreatureDecision.ShouldRenewAggro(traits, _roster, aggro, _stateTimer, selfInLight))
                _aggroLeft = definition.aggroSeconds;

            switch (CreatureDecision.NextIntent(traits, _roster, aggro, _stateTimer, selfInLight))
            {
                case CreatureIntent.Ecology:
                    TransitionTo(PickEcologyState());
                    break;

                case CreatureIntent.Wander:
                    TransitionTo(CreatureState.Wander);
                    break;

                case CreatureIntent.Flee:
                    TransitionTo(CreatureState.Flee);
                    break;

                case CreatureIntent.Chase:
                    TransitionTo(CreatureState.Chase);
                    break;

                case CreatureIntent.Attack:
                    TransitionTo(CreatureState.Attack);
                    break;

                // Hold — 하던 것을 계속한다.
            }
        }

        /// <summary>
        /// 이 자리가 밝은가. 빛을 꺼리지 않는 생물에게는 묻지도 않는다 —
        /// 기존 4종까지 매 프레임 레지스트리를 두 번씩 훑을 이유가 없고,
        /// 묻지 않으면 그들의 판단 입력이 예전과 글자 그대로 같다.
        ///
        /// 무엇이 광원인지는 여기서 정하지 않는다. <see cref="LitZoneRegistry"/>에
        /// 등록된 것이 광원이다 — 화톳불(<see cref="Survive.Building.Campfire"/>)과
        /// 켜진 랜턴(<see cref="LanternController"/>)이 스스로 등록한다.
        /// </summary>
        bool Lit(Vector3 position) =>
            definition != null && definition.avoidsLight && LitZoneRegistry.IsLit(position);

        /// <summary>
        /// 내가 지금 사람이 <b>내준 쪽</b>(등 뒤 사각)에 서 있는가.
        ///
        /// <b>재는 것만 여기서 한다.</b> 판단은 여전히
        /// <see cref="CreatureDecision.JudgeLight"/>에 있고, 기하는
        /// <see cref="LitZoneRegistry.IsBlindSide"/>에 있다.
        ///
        /// 빛을 꺼리지 않는 기존 4종에게는 묻지도 않는다 — 그들에게 빛은 애초에
        /// 아무것도 막지 않으므로, 내준 쪽이라는 개념도 값을 하지 않는다.
        /// 그래서 여기를 지나고 나면 그들의 판단 입력은 글자 그대로 예전과 같다.
        /// </summary>
        bool BlindSide() =>
            definition != null && definition.avoidsLight &&
            LitZoneRegistry.IsBlindSide(transform.position);

        /// <summary>
        /// 위협이 없을 때 무엇을 할지. 우선순위 판단은 Domain에 있고,
        /// 여기서는 고른 목적지를 받아 적기만 한다.
        /// </summary>
        CreatureState PickEcologyState()
        {
            var choice = CreatureDecision.PickEcology(_feedProbe, _scavengeProbe);
            if (choice.HasDestination) _destination = choice.Destination;
            return choice.State;
        }

        bool TryGetFeedTarget(out Vector3 destination)
        {
            if (_feeding != null) return _feeding.TryGetFeedTarget(out destination);
            destination = Vector3.zero;
            return false;
        }

        bool TryGetScavengeTarget(out Vector3 destination)
        {
            if (_scavenger != null) return _scavenger.TryGetScavengeTarget(out destination);
            destination = Vector3.zero;
            return false;
        }

        void TransitionTo(CreatureState next)
        {
            if (!CreatureDecision.ShouldTransition(_state, next, _stateTimer)) return;
            _state = next;

            // 난수는 Domain 밖에 남는다. 뽑는 순서를 바꾸면 무리 전체의 배회 모양이 달라진다.
            float wanderDuration = 0f;

            switch (next)
            {
                case CreatureState.Wander:
                    _destination = CreatureNavigation.WanderDestination(
                        _homePosition, Random.insideUnitSphere, wanderRadius);
                    wanderDuration = Random.Range(CreatureStateDurations.WanderMinSeconds,
                                                  CreatureStateDurations.WanderMaxSeconds);
                    break;

                case CreatureState.Flee:
                    // 빛에 밀려 물러나는 것이면 빛에서 멀어져야 한다. 플레이어에게서
                    // 멀어지는 것으로 대신하면 화톳불 건너편에 사람이 서 있을 때
                    // 불 속으로 들어가는 방향이 답으로 나온다.
                    if (definition != null && definition.avoidsLight &&
                        LitZoneRegistry.TryGetLitCenter(transform.position, out var litCenter))
                        _destination = CreatureNavigation.FleeDestination(
                            transform.position, litCenter, fleeDistance, _homePosition.y);
                    else if (Target != null)
                        _destination = CreatureNavigation.FleeDestination(
                            transform.position, Target.position, fleeDistance, _homePosition.y);
                    break;

                    // Feed·Scavenge의 목표는 PickEcologyState()에서 이미 잡았다.
            }

            _stateTimer = CreatureStateDurations.For(next, wanderDuration);
        }

        void RunState()
        {
            switch (CreatureDecision.ActionFor(_state))
            {
                case CreatureAction.MoveToDestination:
                    MoveTo(_destination);
                    break;

                case CreatureAction.PursueThreat:
                    var pursued = Target;
                    if (pursued != null) MoveTo(pursued.position);
                    break;

                case CreatureAction.AttackThreat:
                    StopMoving();
                    Attack();
                    break;

                default:
                    StopMoving();
                    break;
            }
        }

        /// <summary>
        /// 다시 걷기 시작하는 자리. <b>멈춰 세웠던 것을 여기서 되돌린다.</b>
        ///
        /// <see cref="StopMoving"/>가 켠 <see cref="NavMeshAgent.isStopped"/>는
        /// <see cref="NavMeshAgent.SetDestination"/>으로는 풀리지 않는다. 그래서 한 번
        /// 공격 자세를 잡은 생물은 어그로가 식어 배회로 돌아간 뒤에도 영영 굳어 있었다 —
        /// 목적지는 매번 새로 잡히는데(hasPath=True) 속도는 0이었다.
        /// 실측: 공이 사거리 안에서 한 번 멈춘 뒤 20초 동안 0.000m 움직였다.
        ///
        /// 비행 쪽은 <see cref="FlyerMotor.MoveTowards"/>가 이미 제 멈춤 표시를 푼다.
        /// 지상도 같은 자리에서 같은 일을 하게 맞춘 것뿐이다.
        /// </summary>
        void MoveTo(Vector3 destination)
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(destination);
            }
            else _motor?.MoveTowards(destination);
        }

        void StopMoving()
        {
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            else _motor?.Stop();
        }

        void Attack()
        {
            // 때리는 상대도 규칙이 고른다. 지금은 목록이 하나뿐이라 예전과 같은 몸이다.
            var victim = Target;
            if (victim == null || !CreatureDecision.IsReady(Time.time, _nextAttackTime)) return;
            _nextAttackTime = Time.time + definition.attackCooldown;

            var target = victim.GetComponentInChildren<IDamageable>();
            if (target == null || target.IsDead) return;

            Vector3 dir = (victim.position - transform.position).normalized;
            target.TakeDamage(new DamageInfo(definition.attackDamage, gameObject,
                                          transform.position + dir, -dir));
        }
    }
}
