using UnityEngine;
using UnityEngine.AI;
using Survive.Combat;
using Survive.Player;

namespace Survive.Creatures
{
    /// <summary>
    /// 생물의 상태머신. 지상은 NavMeshAgent, 비행은 FlyerMotor가 실제 이동을 맡는다.
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

            if (agent != null && definition != null) agent.speed = definition.moveSpeed;
            if (flyer != null && definition != null) flyer.Speed = definition.moveSpeed;
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
                var ctx = UnityEngine.Object.FindFirstObjectByType<PlayerContext>(FindObjectsInactive.Exclude);
                if (ctx != null) _player = ctx.transform;
            }

            _stateTimer -= Time.deltaTime;
            if (_aggroLeft > 0f) _aggroLeft -= Time.deltaTime;

            float distance = _player != null
                ? Vector3.Distance(transform.position, _player.position)
                : float.MaxValue;

            UpdateState(distance);
            RunState();
        }

        void UpdateState(float distance)
        {
            var traits = CreatureTraits.From(definition);
            var senses = new CreatureSenses(distance, _aggroLeft, _stateTimer);

            switch (CreatureDecision.NextIntent(traits, senses))
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
                    if (_player != null)
                        _destination = CreatureNavigation.FleeDestination(
                            transform.position, _player.position, fleeDistance, _homePosition.y);
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
                    if (_player != null) MoveTo(_player.position);
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

        void MoveTo(Vector3 destination)
        {
            if (agent != null && agent.isOnNavMesh) agent.SetDestination(destination);
            else if (flyer != null) flyer.MoveTowards(destination);
        }

        void StopMoving()
        {
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            else flyer?.Stop();
        }

        void Attack()
        {
            if (_player == null || !CreatureDecision.IsReady(Time.time, _nextAttackTime)) return;
            _nextAttackTime = Time.time + definition.attackCooldown;

            var target = _player.GetComponentInChildren<IDamageable>();
            if (target == null || target.IsDead) return;

            Vector3 dir = (_player.position - transform.position).normalized;
            target.TakeDamage(new DamageInfo(definition.attackDamage, gameObject,
                                          transform.position + dir, -dir));
        }
    }
}
