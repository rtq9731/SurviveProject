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

        Vector3 _속도;
        float _bobPhase;
        bool _정지;

        void Start() => _bobPhase = Random.value * Mathf.PI * 2f;

        public void MoveTowards(Vector3 target)
        {
            _정지 = false;
            _목표 = target;
        }

        public void Stop() => _정지 = true;

        Vector3 _목표;

        void Update()
        {
            float dt = Time.deltaTime;
            _bobPhase += dt * bobFrequency;

            Vector3 원하는위치 = _정지 ? transform.position : _목표;
            원하는위치.y = 지면높이(transform.position) + hoverHeight
                        + Mathf.Sin(_bobPhase) * bobAmplitude;

            Vector3 방향 = 원하는위치 - transform.position;
            if (!_정지 && 방향.sqrMagnitude > 0.01f)
            {
                _속도 = Vector3.Lerp(_속도, 방향.normalized * Speed, dt * 3f);

                Vector3 수평 = new Vector3(방향.x, 0f, 방향.z);
                if (수평.sqrMagnitude > 0.01f)
                {
                    var 목표회전 = Quaternion.LookRotation(수평);
                    transform.rotation = Quaternion.Slerp(transform.rotation, 목표회전, dt * turnSpeed);
                }
            }
            else _속도 = Vector3.Lerp(_속도, Vector3.zero, dt * 4f);

            // 고도는 항상 보정한다 (정지 중에도 떠 있어야 한다)
            Vector3 이동 = _속도 * dt;
            이동.y = (원하는위치.y - transform.position.y) * Mathf.Clamp01(dt * 3f);
            transform.position += 이동;
        }

        float 지면높이(Vector3 pos)
        {
            if (Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out var hit, 200f,
                                groundMask, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return 0f;
        }
    }
}
