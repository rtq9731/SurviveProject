using UnityEngine;
using UnityEngine.AI;
using Survive.Combat;
using Survive.Player;

namespace Survive.Creatures
{
    /// <summary>
    /// 생물의 상태머신. 지상은 NavMeshAgent, 비행은 FlyerMotor가 실제 이동을 맡는다.
    /// 챕터 1의 4종(눈·공·날개·열매게)은 전부 Skittish 또는 Defensive다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CreatureHealth))]
    public class CreatureBrain : MonoBehaviour
    {
        enum State { Idle, Wander, Flee, Chase, Attack, Feed, Scavenge, Dead }

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
        State _state = State.Idle;
        float _stateTimer;
        float _aggroLeft;
        float _nextAttackTime;
        Vector3 _homePosition;
        Vector3 _destination;

        void Awake()
        {
            _health = GetComponent<CreatureHealth>();
            if (definition == null) definition = _health.Definition;
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (flyer == null) flyer = GetComponent<FlyerMotor>();
            _homePosition = transform.position;

            _feeding = GetComponent<CreatureFeeding>();
            _scavenger = GetComponent<ScavengerBehavior>();

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
            _state = State.Dead;
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        }

        void OnDamaged(CreatureHealth _, DamageInfo info)
        {
            if (definition == null) return;

            switch (definition.behavior)
            {
                case BehaviorProfile.Skittish:
                    TransitionTo(State.Flee);
                    break;
                case BehaviorProfile.Defensive:
                case BehaviorProfile.Aggressive:
                    _aggroLeft = definition.aggroSeconds;
                    TransitionTo(State.Chase);
                    break;
            }
        }

        void Update()
        {
            if (_state == State.Dead || definition == null) return;

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
            RunState(distance);
        }

        void UpdateState(float distance)
        {
            bool detected = distance <= definition.detectRadius;

            switch (definition.behavior)
            {
                case BehaviorProfile.Passive:
                    if (_stateTimer <= 0f) TransitionTo(PickEcologyState());
                    break;

                case BehaviorProfile.Skittish:
                    if (detected) TransitionTo(State.Flee);
                    else if (_stateTimer <= 0f) TransitionTo(PickEcologyState());
                    break;

                case BehaviorProfile.Defensive:
                    if (_aggroLeft > 0f)
                        TransitionTo(distance <= definition.attackRange ? State.Attack : State.Chase);
                    else if (_stateTimer <= 0f) TransitionTo(PickEcologyState());
                    break;

                case BehaviorProfile.Aggressive:
                    if (detected || _aggroLeft > 0f)
                        TransitionTo(distance <= definition.attackRange ? State.Attack : State.Chase);
                    else if (_stateTimer <= 0f) TransitionTo(State.Wander);
                    break;
            }
        }

        /// <summary>
        /// 위협이 없을 때 무엇을 할지. 생산자는 먹으러, 분해자는 주우러 간다.
        /// 할 일이 없으면 배회한다.
        /// </summary>
        State PickEcologyState()
        {
            if (_feeding != null && _feeding.TryGetFeedTarget(out var food))
            {
                _destination = food;
                return State.Feed;
            }
            if (_scavenger != null && _scavenger.TryGetScavengeTarget(out var junk))
            {
                _destination = junk;
                return State.Scavenge;
            }
            return State.Wander;
        }

        void TransitionTo(State next)
        {
            if (_state == next && _stateTimer > 0f) return;
            _state = next;

            switch (next)
            {
                case State.Wander:
                    _destination = _homePosition + Random.insideUnitSphere * wanderRadius;
                    _destination.y = _homePosition.y;
                    _stateTimer = Random.Range(2f, 4f);
                    break;

                case State.Feed:
                case State.Scavenge:
                    // 목표는 생태행동()에서 이미 잡았다
                    _stateTimer = 1.5f;
                    break;

                case State.Flee:
                    if (_player != null)
                    {
                        Vector3 away = (transform.position - _player.position).normalized;
                        _destination = transform.position + away * fleeDistance;
                        _destination.y = _homePosition.y;
                    }
                    _stateTimer = 1.5f;
                    break;

                default:
                    _stateTimer = 0.5f;
                    break;
            }
        }

        void RunState(float distance)
        {
            switch (_state)
            {
                case State.Wander:
                case State.Flee:
                case State.Feed:
                case State.Scavenge:
                    MoveTo(_destination);
                    break;

                case State.Chase:
                    if (_player != null) MoveTo(_player.position);
                    break;

                case State.Attack:
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
            if (_player == null || Time.time < _nextAttackTime) return;
            _nextAttackTime = Time.time + definition.attackCooldown;

            var target = _player.GetComponentInChildren<IDamageable>();
            if (target == null || target.IsDead) return;

            Vector3 dir = (_player.position - transform.position).normalized;
            target.TakeDamage(new DamageInfo(definition.attackDamage, gameObject,
                                          transform.position + dir, -dir));
        }
    }
}
