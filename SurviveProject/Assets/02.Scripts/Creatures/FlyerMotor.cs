using UnityEngine;
using Survive.Combat;

namespace Survive.Creatures
{
    /// <summary>
    /// 비행 생물의 단순 이동. NavMesh를 쓰지 않고 고도를 유지하며 목표로 향한다.
    /// 눈·날개·하늘 가오리처럼 떠다니는 기계가 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    public class FlyerMotor : MonoBehaviour, ICreatureMotor
    {
        [Tooltip("지면으로부터 유지할 높이")]
        [SerializeField] float hoverHeight = 2.5f;

        [Tooltip("좌우 흔들림 폭. 기계적인 부유감을 준다")]
        [SerializeField] float bobAmplitude = 0.25f;
        [SerializeField] float bobFrequency = 1.4f;

        [SerializeField] float turnSpeed = 6f;
        [SerializeField] LayerMask groundMask = ~0;

        /// <summary>지면 탐색 광선을 쏘기 시작하는 높이. 몸보다 위에서 내려다본다.</summary>
        const float ProbeRise = 50f;

        /// <summary>광선의 총 길이.</summary>
        const float ProbeLength = 200f;

        /// <summary>몸을 맞았을 때 다시 쏘는 지점을 그 표면보다 조금 아래로 내리는 값.</summary>
        const float ProbeSkipEpsilon = 0.01f;

        /// <summary>한 프레임에 걸러 낼 몸의 최대 개수. 무리가 겹쳐 있어도 넉넉하다.</summary>
        const int MaxProbeSkips = 8;

        /// <summary>천장 아래로 남겨 둘 여유. 몸이 바위에 박혀 보이지 않을 만큼만.</summary>
        const float CeilingClearance = 0.6f;

        public float Speed { get; set; } = 3f;

        /// <summary>지면 위로 유지하는 높이가 곧 순항 고도다. 흔들림은 이 값 둘레의 진폭일 뿐이다.</summary>
        public float CruiseHeight => hoverHeight;

        Vector3 _velocity;
        float _bobPhase;
        bool _halted;

        /// <summary>
        /// 까딱임의 시작 위상. <b>이쪽은 눈뿐이다</b> — 나는 몸은 서식 구역을 묻지
        /// 않으므로(<c>HoverDrifter</c>와 다른 자리다) 이 값이 판정에 닿지 않는다.
        /// 그래도 주인 있는 난수로 뽑는 것은 <b>「난수는 전부 주인이 있다」</b>가
        /// 예외를 두는 순간 다시 새기 때문이다 — 예외를 기억하는 것보다 규칙 하나가 싸다.
        /// </summary>
        void Start()
        {
            var rng = Survive.World.WorldSeed.Rng(
                Survive.World.WorldSeedBranch.Wander, transform.position, occasion: 2);
            _bobPhase = (float)rng.NextDouble() * Mathf.PI * 2f;
        }

        public void MoveTowards(Vector3 target)
        {
            _halted = false;
            _target = target;
        }

        public void Stop() => _halted = true;

        Vector3 _target;

        void Update()
        {
            float dt = Time.deltaTime;
            _bobPhase += dt * bobFrequency;

            Vector3 desired = _halted ? transform.position : _target;
            desired.y = TargetHeight(transform.position);

            Vector3 dir = desired - transform.position;
            if (!_halted && dir.sqrMagnitude > 0.01f)
            {
                _velocity = Vector3.Lerp(_velocity, dir.normalized * Speed, dt * 3f);

                Vector3 planar = new Vector3(dir.x, 0f, dir.z);
                if (planar.sqrMagnitude > 0.01f)
                {
                    var targetRot = Quaternion.LookRotation(planar);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, dt * turnSpeed);
                }
            }
            else _velocity = Vector3.Lerp(_velocity, Vector3.zero, dt * 4f);

            // 고도는 항상 보정한다 (정지 중에도 떠 있어야 한다)
            Vector3 MoveTo = _velocity * dt;
            MoveTo.y = (desired.y - transform.position.y) * Mathf.Clamp01(dt * 3f);
            transform.position += MoveTo;
        }

        /// <summary>
        /// 이 자리에서 떠 있어야 할 높이. 지면에서 <see cref="hoverHeight"/>만큼 띄우되,
        /// <b>머리 위에 바위가 있으면 그 아래에 머문다.</b>
        ///
        /// 천장을 따로 봐야 하는 이유는 <see cref="GroundHeight"/>가 보는 방향에 있다.
        /// 그쪽은 몸보다 <see cref="ProbeRise"/>만큼 <i>위에서</i> 내려다보는데, 동굴 안에
        /// 있는 생물의 머리 위 50m는 이미 바위 천장 바깥이다. 그러면 광선이 천장의
        /// <b>윗면</b>부터 맞고, 그 높이가 "지면"이 되어 목표 고도가 천장 위로 잡힌다.
        /// 고도 보정은 물리를 보지 않으므로 생물은 그대로 바위를 뚫고 올라갔다
        /// (실측: 천장 아랫면 62.42 아래 59.42에 있던 눈이 8초 만에 66.86까지 올라감).
        ///
        /// 벽은 <see cref="CreatureCollisionGuard"/>가 막지만 그쪽은 수평 전용이라
        /// 세로 방향은 여기서 막는다.
        /// </summary>
        float TargetHeight(Vector3 pos)
        {
            float wanted = GroundHeight(pos) + hoverHeight + Mathf.Sin(_bobPhase) * bobAmplitude;

            float ceiling = CeilingHeight(pos);
            if (ceiling < float.MaxValue)
                wanted = Mathf.Min(wanted, ceiling - CeilingClearance);

            return wanted;
        }

        /// <summary>
        /// 머리 위를 덮은 것의 아랫면 높이. 하늘만 있으면 <see cref="float.MaxValue"/>다 —
        /// 그래야 트인 곳에서는 아무것도 달라지지 않는다.
        ///
        /// 아래를 볼 때와 같은 이유로 몸은 천장이 아니다. 무리가 위아래로 겹치면
        /// 아래쪽이 위쪽을 천장으로 삼아 눌러앉고, 그러면 이번엔 반대가 되어 서로를
        /// 밀어 내린다. <see cref="IsBody"/>로 아래쪽과 똑같이 걸러 낸다.
        /// </summary>
        float CeilingHeight(Vector3 pos)
        {
            Vector3 origin = pos;
            float remaining = ProbeLength;

            for (int i = 0; i < MaxProbeSkips; i++)
            {
                if (!Physics.Raycast(origin, Vector3.up, out var hit, remaining,
                                     groundMask, QueryTriggerInteraction.Ignore))
                    break;

                if (!IsBody(hit.collider)) return hit.point.y;

                float step = hit.distance + ProbeSkipEpsilon;
                origin += Vector3.up * step;
                remaining -= step;
                if (remaining <= 0f) break;
            }

            return float.MaxValue;
        }

        /// <summary>
        /// 발밑 지면의 높이. 몸은 지면이 아니다 — 자기 것이든 남의 것이든.
        ///
        /// 예전에는 여기서 광선을 한 번만 쏘았다. 그런데 생물은 Default 레이어에
        /// 있고 <see cref="groundMask"/>는 Everything이라, 위에서 내려온 광선이
        /// <b>자기 캡슐부터</b> 맞았다. 그러면 지면 높이가 자기 머리 높이로 나오고
        /// 목표 고도는 매 프레임 "지금 내 위치 + hoverHeight"가 되어, 비행 생물이
        /// 초당 8.8m로 끝없이 올라갔다(실측: 20초 만에 y 52 → 350).
        ///
        /// 남의 몸도 같은 이유로 걸러야 한다. 두 마리가 겹치면 아래쪽이 위쪽을
        /// 지면으로 삼아 올라서고, 그러면 이번엔 반대가 되어 둘이 서로를 밟고
        /// 하늘까지 올라간다.
        ///
        /// 레이어를 나누는 편이 깔끔하겠지만 그건 프리팹·씬을 고쳐야 하는 일이라
        /// 여기서는 코드만으로 걸러 낸다.
        /// </summary>
        float GroundHeight(Vector3 pos)
        {
            Vector3 origin = pos + Vector3.up * ProbeRise;
            float remaining = ProbeLength;

            for (int i = 0; i < MaxProbeSkips; i++)
            {
                if (!Physics.Raycast(origin, Vector3.down, out var hit, remaining,
                                     groundMask, QueryTriggerInteraction.Ignore))
                    break;

                if (!IsBody(hit.collider)) return hit.point.y;

                // 몸을 맞았으면 그 표면 바로 아래에서 다시 쏜다. RaycastAll과 달리
                // 버퍼 크기에 따라 지면을 놓칠 일이 없다.
                float step = hit.distance + ProbeSkipEpsilon;
                origin += Vector3.down * step;
                remaining -= step;
                if (remaining <= 0f) break;
            }

            // 지면을 못 찾으면 지금 고도를 유지한다. 예전처럼 0을 돌려주면
            // 아무것도 없는 허공 위에서 세계 바닥 높이로 곤두박질친다.
            return pos.y - hoverHeight;
        }

        /// <summary>자기 몸이거나 다른 생물의 몸인가.</summary>
        bool IsBody(Collider col)
        {
            var t = col.transform;
            if (t.IsChildOf(transform) || transform.IsChildOf(t)) return true;
            return col.GetComponentInParent<CreatureHealth>() != null;
        }
    }
}
