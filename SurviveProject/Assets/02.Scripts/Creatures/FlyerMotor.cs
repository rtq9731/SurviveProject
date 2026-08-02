using UnityEngine;

namespace Survive.Creatures
{
    /// <summary>
    /// 비행 생물의 단순 이동. NavMesh를 쓰지 않고 고도를 유지하며 목표로 향한다.
    /// 눈·날개·하늘 가오리처럼 떠다니는 기계가 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    public class FlyerMotor : MonoBehaviour
    {
        [Tooltip("지면으로부터 유지할 높이")]
        [SerializeField] float hoverHeight = 2.5f;

        [Tooltip("좌우 흔들림 폭. 기계적인 부유감을 준다")]
        [SerializeField] float bobAmplitude = 0.25f;
        [SerializeField] float bobFrequency = 1.4f;

        [SerializeField] float turnSpeed = 6f;
        [SerializeField] LayerMask groundMask = ~0;

        public float Speed { get; set; } = 3f;

        Vector3 _velocity;
        float _bobPhase;
        bool _halted;

        void Start() => _bobPhase = Random.value * Mathf.PI * 2f;

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
            desired.y = GroundHeight(transform.position) + hoverHeight
                        + Mathf.Sin(_bobPhase) * bobAmplitude;

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

        float GroundHeight(Vector3 pos)
        {
            if (Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out var hit, 200f,
                                groundMask, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return 0f;
        }
    }
}
