using UnityEngine;
using Survive.InputSystem;

namespace Survive.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerLocomotion : MonoBehaviour
    {
        [SerializeField] InputReaderSO input;
        [SerializeField] PlayerSwimming swimming;
        [SerializeField] PlayerCameraRig cameraRig;

        [Header("이동")]
        [SerializeField] float walkSpeed = 5f;
        [SerializeField] float runSpeed = 7f;
        [SerializeField] float gravityScale = 1f;
        [SerializeField] float jumpPower = 2f;
        [SerializeField] float jumpCooldown = 1f;

        [Header("수영")]
        [Tooltip("헤엄칠 때 이동 속도")]
        [SerializeField] float swimSpeed = 3.2f;

        [Tooltip("물속에서 위아래로 움직이는 속도. Space=상승, LeftShift=하강")]
        [SerializeField] float swimVerticalSpeed = 2.6f;

        [Tooltip("가만히 있을 때 수면으로 떠오르는 속도")]
        [SerializeField] float buoyancy = 0.9f;

        [Tooltip("허리까지 잠겼을 때 걷기 속도 배율")]
        [Range(0.2f, 1f)] [SerializeField] float wadeSpeedFactor = 0.55f;

        [Tooltip("물가에서 뭍으로 기어오를 때의 상승 속도")]
        [SerializeField] float climbOutBoost = 3.2f;

        CharacterController _cc;
        Vector2 _입력 = Vector2.zero;
        float _수직속도;
        float _다음점프시각;
        bool _잠김;
        bool _상승중;

        public bool IsGrounded => _cc != null && _cc.isGrounded;
        public float CurrentSpeed { get; private set; }
        public Vector3 PlanarVelocity { get; private set; }

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (swimming == null) swimming = GetComponent<PlayerSwimming>();
            if (cameraRig == null) cameraRig = GetComponent<PlayerCameraRig>();
        }

        void OnEnable()
        {
            if (input == null) return;
            input.MoveEvent += 이동입력;
            input.JumpEvent += 점프;
            input.SprintEvent += 상승입력;
        }

        void OnDisable()
        {
            if (input == null) return;
            input.MoveEvent -= 이동입력;
            input.JumpEvent -= 점프;
            input.SprintEvent -= 상승입력;
        }

        void 이동입력(Vector2 v) => _입력 = v;
        void 상승입력(bool 눌림) => _상승중 = 눌림;

        void 점프()
        {
            if (_잠김) return;

            // 물속에서는 점프 키가 상승이다
            if (swimming != null && swimming.IsSwimming)
            {
                _수직속도 = swimVerticalSpeed;
                return;
            }

            if (Time.time < _다음점프시각) return;
            if (!_cc.isGrounded) return;
            _수직속도 = jumpPower;
            _다음점프시각 = Time.time + jumpCooldown;
        }

        public void SetMovementLocked(bool locked)
        {
            _잠김 = locked;
            if (locked) _입력 = Vector2.zero;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool 헤엄 = swimming != null && swimming.IsSwimming;

            Vector3 방향 = _잠김 ? Vector3.zero : new Vector3(_입력.x, 0f, _입력.y);
            if (방향.sqrMagnitude > 1f) 방향.Normalize();

            if (헤엄) 수영이동(방향, dt);
            else 지상이동(방향, dt);
        }

        void 지상이동(Vector3 방향, float dt)
        {
            bool 달림 = input != null && input.IsSprinting;
            float 속도 = 달림 ? runSpeed : walkSpeed;

            // 허리까지 잠기면 느려진다
            if (swimming != null && swimming.IsWading) 속도 *= wadeSpeedFactor;

            Vector3 수평 = transform.TransformDirection(방향) * 속도;

            if (_cc.isGrounded && _수직속도 < 0f) _수직속도 = -1f;
            _수직속도 += -9.81f * gravityScale * dt;

            _cc.Move((수평 + Vector3.up * _수직속도) * dt);

            PlanarVelocity = 수평;
            CurrentSpeed = 수평.magnitude;
        }

        void 수영이동(Vector3 방향, float dt)
        {
            // 물속에서는 보는 방향으로 나아간다. 위아래를 보면 그쪽으로 간다.
            Vector3 전방 = transform.forward;
            var camT = cameraRig != null ? cameraRig.CameraTransform : null;
            if (camT != null) 전방 = camT.forward;

            Vector3 오른쪽 = transform.right;
            Vector3 이동 = (전방 * 방향.z + 오른쪽 * 방향.x) * swimSpeed;

            bool 상승조작 = false;

            // 상하 조작: Shift는 하강 (Space 상승은 점프 이벤트에서 처리)
            if (!_잠김 && _상승중) { _수직속도 = -swimVerticalSpeed; }
            else if (_수직속도 > buoyancy + 0.05f) 상승조작 = true;   // Space로 밀어 올린 상태

            // 물의 저항. 수직 속도가 부력 쪽으로 서서히 수렴한다
            _수직속도 = Mathf.MoveTowards(_수직속도, buoyancy, dt * 3.5f);
            _수직속도 = swimming.DampBuoyancyNearSurface(_수직속도, _cc.isGrounded, 상승조작);

            // 물가로 나가려 할 때: 앞이 막혀 있고 발밑에 지면이 있으면 밀어 올린다.
            // 이게 없으면 경사를 못 타고 물에 갇힌다.
            if (방향.sqrMagnitude > 0.01f && 물가오르기(이동))
                _수직속도 = Mathf.Max(_수직속도, climbOutBoost);

            _cc.Move((이동 + Vector3.up * _수직속도) * dt);

            PlanarVelocity = new Vector3(이동.x, 0f, 이동.z);
            CurrentSpeed = PlanarVelocity.magnitude;
        }

        /// <summary>
        /// 진행 방향에 오를 만한 턱이 있는지. 물가에서 뭍으로 나가는 것을 돕는다.
        /// </summary>
        bool 물가오르기(Vector3 이동)
        {
            Vector3 수평 = new Vector3(이동.x, 0f, 이동.z);
            if (수평.sqrMagnitude < 0.01f) return false;

            Vector3 앞 = 수평.normalized;
            Vector3 발밑 = transform.position - Vector3.up * (_cc.height * 0.5f - _cc.radius);

            // 가슴 높이에 벽이 있는데 그 위는 비어 있으면 오를 수 있는 턱이다
            bool 벽 = Physics.SphereCast(발밑, _cc.radius * 0.8f, 앞, out _, 0.7f,
                                        ~0, QueryTriggerInteraction.Ignore);
            if (!벽) return false;

            Vector3 위 = 발밑 + Vector3.up * (_cc.height * 0.9f);
            bool 머리막힘 = Physics.SphereCast(위, _cc.radius * 0.8f, 앞, out _, 0.7f,
                                             ~0, QueryTriggerInteraction.Ignore);
            return !머리막힘;
        }
    }
}
