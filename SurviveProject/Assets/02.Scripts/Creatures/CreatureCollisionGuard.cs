using UnityEngine;
using UnityEngine.AI;
using Survive.Combat;
using Survive.Interaction;
using Survive.Player;

namespace Survive.Creatures
{
    /// <summary>
    /// 생물이 벽을 통과하지 못하게 하는 문지기.
    ///
    /// 생물의 이동은 둘 중 하나로 일어난다 — 지상은 <see cref="NavMeshAgent"/>,
    /// 비행은 <see cref="FlyerMotor"/>. 그런데 <b>둘 다 물리를 보지 않는다.</b>
    /// NavMeshAgent는 <i>구워 둔</i> NavMesh만 알아서 플레이어가 나중에 세운 벽을
    /// 통째로 무시하고, FlyerMotor는 <c>transform.position</c>을 직접 더한다.
    /// 콜라이더가 붙어 있어도 Rigidbody가 없으니 아무것도 밀어내지 않는다.
    ///
    /// 그래서 이 컴포넌트는 <b>이동이 끝난 뒤에</b>(LateUpdate) 이번 프레임에
    /// 지나온 길을 캡슐로 훑어, 벽을 관통했다면 벽 앞으로 되돌리고 남은 이동량을
    /// 벽면을 따라 미끄러뜨린다. 판단(<see cref="CreatureDecision"/>)에는 손대지 않는다 —
    /// 어디로 가려 했는지는 그대로고, 갈 수 있는 만큼만 간다.
    ///
    /// 높이는 건드리지 않는다. 접지는 NavMesh가, 고도는 FlyerMotor가 맡는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CreatureCollisionGuard : MonoBehaviour
    {
        /// <summary>부딪힐 것을 한 번에 몇 개까지 볼지. 가장 가까운 것 하나만 쓴다.</summary>
        static readonly RaycastHit[] _hits = new RaycastHit[16];

        NavMeshAgent _agent;
        Collider _body;
        Vector3 _previous;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _body = GetComponent<Collider>();
            if (_body == null) _body = GetComponentInChildren<Collider>();
        }

        void OnEnable() => _previous = transform.position;

        void LateUpdate()
        {
            Vector3 now = transform.position;
            Vector3 delta = now - _previous;
            var planar = new Vector3(delta.x, 0f, delta.z);

            if (planar.sqrMagnitude < 1e-8f ||
                CreatureCollision.IsTeleport(delta.magnitude, CreatureCollision.TeleportDistance))
            {
                _previous = now;
                return;
            }

            // 훑기는 지난 프레임의 수평 자리에서 시작하되 높이는 지금 것을 쓴다.
            // 오르막에서 지난 프레임의 낮은 높이로 훑으면 앞의 비탈이 벽으로 잡힌다.
            var origin = new Vector3(_previous.x, now.y, _previous.z);
            Vector3 resolved = Resolve(origin, planar);
            resolved.y = now.y;

            if ((resolved - now).sqrMagnitude > 1e-8f) MoveTo(resolved);
            _previous = transform.position;
        }

        /// <summary>벽에 부딪히면 앞에서 멈추고, 남은 만큼은 벽을 따라 미끄러진다(최대 두 번).</summary>
        Vector3 Resolve(Vector3 origin, Vector3 motion)
        {
            Vector3 position = origin;
            Vector3 remaining = motion;

            for (int pass = 0; pass < 2; pass++)
            {
                float distance = remaining.magnitude;
                if (distance < 1e-4f) break;

                Vector3 direction = remaining / distance;
                if (!TrySweep(position, direction, distance + CreatureCollision.SkinWidth, out var hit))
                {
                    position += remaining;
                    break;
                }

                position = CreatureCollision.StopShort(position, direction, hit.distance,
                                                       CreatureCollision.SkinWidth);
                float travelled = Mathf.Max(0f, hit.distance - CreatureCollision.SkinWidth);
                remaining = CreatureCollision.SlideAlong(direction * (distance - travelled), hit.normal);
            }

            return position;
        }

        /// <summary>
        /// <paramref name="position"/>에 선 몸통을 <paramref name="direction"/>으로 훑어
        /// 가장 가까운 <b>벽</b>을 찾는다. 바닥·비탈, 자기 몸, 다른 생물, 플레이어,
        /// 바닥에 떨어진 전리품은 벽이 아니다.
        /// </summary>
        bool TrySweep(Vector3 position, Vector3 direction, float distance, out RaycastHit nearest)
        {
            nearest = default;
            if (_body == null) return false;

            Bounds bounds = _body.bounds;
            float radius = Mathf.Max(0.05f, Mathf.Min(bounds.extents.x, bounds.extents.z));
            float bottomY = bounds.min.y + CreatureCollision.StepOffset + radius;
            float topY = Mathf.Max(bottomY, bounds.max.y - radius);

            // bounds는 지금 자리 기준이라, 훑기 시작점까지의 수평 차이만큼 옮겨 준다.
            var shift = new Vector3(position.x - transform.position.x, 0f,
                                    position.z - transform.position.z);
            Vector3 p1 = new Vector3(bounds.center.x, bottomY, bounds.center.z) + shift;
            Vector3 p2 = new Vector3(bounds.center.x, topY, bounds.center.z) + shift;

            int count = Physics.CapsuleCastNonAlloc(p1, p2, radius * 0.95f, direction, _hits, distance,
                                                    ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];

                // 거리 0은 시작부터 겹쳐 있다는 뜻이라 법선을 믿을 수 없다. 되돌리면 오히려 튄다.
                if (hit.distance <= 0f) continue;
                if (CreatureCollision.IsWalkable(hit.normal.y, CreatureCollision.WalkableNormalY)) continue;
                if (!IsWall(hit.collider)) continue;
                if (hit.distance >= best) continue;

                best = hit.distance;
                nearest = hit;
                found = true;
            }

            return found;
        }

        /// <summary>세계의 구조물인가. 살아 움직이는 것과 주울 것은 막지 않는다.</summary>
        bool IsWall(Collider other)
        {
            Transform t = other.transform;
            if (t == transform || t.IsChildOf(transform)) return false;

            // 생물끼리 막으면 무리가 서로를 벽으로 삼아 굳는다. 플레이어를 막으면 공격이 닿지 않는다.
            if (other.GetComponentInParent<CreatureHealth>() != null) return false;
            if (other.GetComponentInParent<PlayerContext>() != null) return false;
            if (other.GetComponentInParent<CharacterController>() != null) return false;

            // 분해자는 전리품을 밟고 가야 주울 수 있다.
            if (other.GetComponentInParent<ItemPickup>() != null) return false;

            return true;
        }

        /// <summary>
        /// 되돌린 자리를 실제로 적용한다. NavMeshAgent에게는
        /// <see cref="NavMeshAgent.nextPosition"/>으로 "몸은 여기까지밖에 못 왔다"고 알려 준다.
        /// 이걸 빼면 에이전트가 제 시뮬레이션 좌표를 그대로 밀고 나가
        /// 다음 프레임에 또 벽 안으로 들어간다.
        /// </summary>
        void MoveTo(Vector3 position)
        {
            transform.position = position;
            if (_agent != null && _agent.isOnNavMesh) _agent.nextPosition = position;
        }
    }
}
