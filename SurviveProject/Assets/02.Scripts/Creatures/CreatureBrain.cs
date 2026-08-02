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
        State _상태 = State.Idle;
        float _상태타이머;
        float _어그로남은시간;
        float _다음공격시각;
        Vector3 _시작위치;
        Vector3 _목표지점;

        void Awake()
        {
            _health = GetComponent<CreatureHealth>();
            if (definition == null) definition = _health.Definition;
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (flyer == null) flyer = GetComponent<FlyerMotor>();
            _시작위치 = transform.position;

            _feeding = GetComponent<CreatureFeeding>();
            _scavenger = GetComponent<ScavengerBehavior>();

            if (agent != null && definition != null) agent.speed = definition.moveSpeed;
            if (flyer != null && definition != null) flyer.Speed = definition.moveSpeed;
        }

        void OnEnable()
        {
            _health.Died += 사망처리;
            _health.Damaged += 피격처리;
        }

        void OnDisable()
        {
            _health.Died -= 사망처리;
            _health.Damaged -= 피격처리;
        }

        void 사망처리(CreatureHealth _)
        {
            _상태 = State.Dead;
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        }

        void 피격처리(CreatureHealth _, DamageInfo info)
        {
            if (definition == null) return;

            switch (definition.behavior)
            {
                case BehaviorProfile.Skittish:
                    전환(State.Flee);
                    break;
                case BehaviorProfile.Defensive:
                case BehaviorProfile.Aggressive:
                    _어그로남은시간 = definition.aggroSeconds;
                    전환(State.Chase);
                    break;
            }
        }

        void Update()
        {
            if (_상태 == State.Dead || definition == null) return;

            if (_player == null)
            {
                var ctx = UnityEngine.Object.FindFirstObjectByType<PlayerContext>(FindObjectsInactive.Exclude);
                if (ctx != null) _player = ctx.transform;
            }

            _상태타이머 -= Time.deltaTime;
            if (_어그로남은시간 > 0f) _어그로남은시간 -= Time.deltaTime;

            float 거리 = _player != null
                ? Vector3.Distance(transform.position, _player.position)
                : float.MaxValue;

            상태갱신(거리);
            행동실행(거리);
        }

        void 상태갱신(float 거리)
        {
            bool 감지됨 = 거리 <= definition.detectRadius;

            switch (definition.behavior)
            {
                case BehaviorProfile.Passive:
                    if (_상태타이머 <= 0f) 전환(생태행동());
                    break;

                case BehaviorProfile.Skittish:
                    if (감지됨) 전환(State.Flee);
                    else if (_상태타이머 <= 0f) 전환(생태행동());
                    break;

                case BehaviorProfile.Defensive:
                    if (_어그로남은시간 > 0f)
                        전환(거리 <= definition.attackRange ? State.Attack : State.Chase);
                    else if (_상태타이머 <= 0f) 전환(생태행동());
                    break;

                case BehaviorProfile.Aggressive:
                    if (감지됨 || _어그로남은시간 > 0f)
                        전환(거리 <= definition.attackRange ? State.Attack : State.Chase);
                    else if (_상태타이머 <= 0f) 전환(State.Wander);
                    break;
            }
        }

        /// <summary>
        /// 위협이 없을 때 무엇을 할지. 생산자는 먹으러, 분해자는 주우러 간다.
        /// 할 일이 없으면 배회한다.
        /// </summary>
        State 생태행동()
        {
            if (_feeding != null && _feeding.TryGetFeedTarget(out var food))
            {
                _목표지점 = food;
                return State.Feed;
            }
            if (_scavenger != null && _scavenger.TryGetScavengeTarget(out var junk))
            {
                _목표지점 = junk;
                return State.Scavenge;
            }
            return State.Wander;
        }

        void 전환(State 새상태)
        {
            if (_상태 == 새상태 && _상태타이머 > 0f) return;
            _상태 = 새상태;

            switch (새상태)
            {
                case State.Wander:
                    _목표지점 = _시작위치 + Random.insideUnitSphere * wanderRadius;
                    _목표지점.y = _시작위치.y;
                    _상태타이머 = Random.Range(2f, 4f);
                    break;

                case State.Feed:
                case State.Scavenge:
                    // 목표는 생태행동()에서 이미 잡았다
                    _상태타이머 = 1.5f;
                    break;

                case State.Flee:
                    if (_player != null)
                    {
                        Vector3 반대 = (transform.position - _player.position).normalized;
                        _목표지점 = transform.position + 반대 * fleeDistance;
                        _목표지점.y = _시작위치.y;
                    }
                    _상태타이머 = 1.5f;
                    break;

                default:
                    _상태타이머 = 0.5f;
                    break;
            }
        }

        void 행동실행(float 거리)
        {
            switch (_상태)
            {
                case State.Wander:
                case State.Flee:
                case State.Feed:
                case State.Scavenge:
                    이동(_목표지점);
                    break;

                case State.Chase:
                    if (_player != null) 이동(_player.position);
                    break;

                case State.Attack:
                    정지();
                    공격();
                    break;

                default:
                    정지();
                    break;
            }
        }

        void 이동(Vector3 목적지)
        {
            if (agent != null && agent.isOnNavMesh) agent.SetDestination(목적지);
            else if (flyer != null) flyer.MoveTowards(목적지);
        }

        void 정지()
        {
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            else flyer?.Stop();
        }

        void 공격()
        {
            if (_player == null || Time.time < _다음공격시각) return;
            _다음공격시각 = Time.time + definition.attackCooldown;

            var 대상 = _player.GetComponentInChildren<IDamageable>();
            if (대상 == null || 대상.IsDead) return;

            Vector3 방향 = (_player.position - transform.position).normalized;
            대상.TakeDamage(new DamageInfo(definition.attackDamage, gameObject,
                                          transform.position + 방향, -방향));
        }
    }
}
