using UnityEngine;
using Survive.Input;

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

        [Tooltip("물속에서 위아래로 움직이는 속도. Space=상승, Ctrl=하강")]
        [SerializeField] float swimVerticalSpeed = 2.6f;

        [Tooltip("물속에서 Shift로 빨라지는 배율")]
        [SerializeField] float swimSprintFactor = 1.45f;

        [Tooltip("가만히 있을 때 수면으로 떠오르는 속도")]
        [SerializeField] float buoyancy = 0.9f;

        [Tooltip("허리까지 잠겼을 때 걷기 속도 배율")]
        [Range(0.2f, 1f)] [SerializeField] float wadeSpeedFactor = 0.55f;

        [Tooltip("물가에서 뭍으로 기어오를 때의 상승 속도")]
        [SerializeField] float climbOutBoost = 3.2f;

        // 물의 저항. 수직 속도가 부력 속도로 수렴하는 초당 비율이다.
        // 크면 손을 뗀 순간 뚝 멈춰 물이 아니라 젤리 같고, 작으면 상승·하강이 한참 남는다.
        const float BuoyancyConvergeRate = 3.5f;

        // 물가 기어오르기 판정의 앞쪽 탐지 거리. 가슴 높이와 머리 높이를 같은 거리로 본다 —
        // 두 값이 어긋나면 "벽은 있는데 그 위는 비었다"가 서로 다른 지점을 가리키게 되어
        // 오를 수 없는 턱을 오를 수 있다고 판정한다.
        const float ClimbOutProbeDistance = 0.7f;

        CharacterController _cc;
        Vector2 _moveInput = Vector2.zero;
        float _verticalSpeed;
        float _nextJumpTime;
        bool _locked;

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
            input.MoveEvent += OnMoveInput;
            input.JumpEvent += OnJump;
        }

        void OnDisable()
        {
            if (input == null) return;
            input.MoveEvent -= OnMoveInput;
            input.JumpEvent -= OnJump;
        }

        void OnMoveInput(Vector2 v) => _moveInput = v;

        void OnJump()
        {
            if (_locked) return;

            // 물속에서는 점프 키가 상승이다
            if (swimming != null && swimming.IsSwimming)
            {
                _verticalSpeed = swimVerticalSpeed;
                return;
            }

            if (Time.time < _nextJumpTime) return;
            if (!_cc.isGrounded) return;
            _verticalSpeed = jumpPower;
            _nextJumpTime = Time.time + jumpCooldown;
        }

        public void SetMovementLocked(bool locked)
        {
            _locked = locked;
            if (locked) _moveInput = Vector2.zero;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool isSwimming = swimming != null && swimming.IsSwimming;

            Vector3 dir = _locked ? Vector3.zero : new Vector3(_moveInput.x, 0f, _moveInput.y);
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            if (isSwimming) SwimMove(dir, dt);
            else GroundMove(dir, dt);
        }

        void GroundMove(Vector3 dir, float dt)
        {
            bool sprinting = input != null && input.IsSprinting;
            float speed = sprinting ? runSpeed : walkSpeed;

            // 허리까지 잠기면 느려진다
            if (swimming != null && swimming.IsWading) speed *= wadeSpeedFactor;

            Vector3 planar = transform.TransformDirection(dir) * speed;

            if (_cc.isGrounded && _verticalSpeed < 0f) _verticalSpeed = -1f;
            _verticalSpeed += -9.81f * gravityScale * dt;

            _cc.Move((planar + Vector3.up * _verticalSpeed) * dt);

            PlanarVelocity = planar;
            CurrentSpeed = planar.magnitude;
        }

        void SwimMove(Vector3 dir, float dt)
        {
            // 물속에서는 보는 방향으로 나아간다. 위아래를 보면 그쪽으로 간다.
            Vector3 forward = transform.forward;
            var camT = cameraRig != null ? cameraRig.CameraTransform : null;
            if (camT != null) forward = camT.forward;

            Vector3 right = transform.right;

            // Shift는 어디서나 빨라지는 것이다. 물에 들어갔다고 뜻이 바뀌면
            // 같은 손가락이 두 가지를 하게 되고, 그건 익힐 것이 하나 더 느는 것이다.
            float speed = swimSpeed * (input != null && input.IsSprinting ? swimSprintFactor : 1f);
            Vector3 MoveTo = (forward * dir.z + right * dir.x) * speed;

            bool ascendInput = false;

            // 상하 조작: Ctrl은 하강, Space는 상승. 둘 다 누르고 있는 동안 계속이다.
            //
            // 전에는 Space가 점프 이벤트 한 번뿐이라, 누르고 있어도 곧 부력 속도로
            // 수렴했다. 툴팁은 "Space=상승"이라고 적혀 있는데 실제로는 한 번 튀고 마니
            // 깊은 곳에서 손을 놓지 않고도 익사했다.
            if (!_locked && input != null && input.IsDescending) { _verticalSpeed = -swimVerticalSpeed; }
            else if (!_locked && input != null && input.IsJumpHeld)
            {
                _verticalSpeed = swimVerticalSpeed;
                ascendInput = true;
            }
            else if (_verticalSpeed > buoyancy + 0.05f) ascendInput = true;

            // 물의 저항. 수직 속도가 부력 쪽으로 서서히 수렴한다
            _verticalSpeed = Mathf.MoveTowards(_verticalSpeed, buoyancy, dt * BuoyancyConvergeRate);
            _verticalSpeed = swimming.DampBuoyancyNearSurface(_verticalSpeed, _cc.isGrounded, ascendInput);

            // 물가로 나가려 할 때: 앞이 막혀 있고 발밑에 지면이 있으면 밀어 올린다.
            // 이게 없으면 경사를 못 타고 물에 갇힌다.
            if (dir.sqrMagnitude > 0.01f && CanClimbOut(MoveTo))
                _verticalSpeed = Mathf.Max(_verticalSpeed, climbOutBoost);

            _cc.Move((MoveTo + Vector3.up * _verticalSpeed) * dt);

            PlanarVelocity = new Vector3(MoveTo.x, 0f, MoveTo.z);
            CurrentSpeed = PlanarVelocity.magnitude;
        }

        /// <summary>
        /// 진행 방향에 오를 만한 턱이 있는지. 물가에서 뭍으로 나가는 것을 돕는다.
        /// </summary>
        bool CanClimbOut(Vector3 MoveTo)
        {
            Vector3 planar = new Vector3(MoveTo.x, 0f, MoveTo.z);
            if (planar.sqrMagnitude < 0.01f) return false;

            Vector3 ahead = planar.normalized;
            Vector3 feet = transform.position - Vector3.up * (_cc.height * 0.5f - _cc.radius);

            // 가슴 높이에 벽이 있는데 그 위는 비어 있으면 오를 수 있는 턱이다
            bool wall = Physics.SphereCast(feet, _cc.radius * 0.8f, ahead, out _, ClimbOutProbeDistance,
                                        ~0, QueryTriggerInteraction.Ignore);
            if (!wall) return false;

            Vector3 head = feet + Vector3.up * (_cc.height * 0.9f);
            bool headBlocked = Physics.SphereCast(head, _cc.radius * 0.8f, ahead, out _, ClimbOutProbeDistance,
                                             ~0, QueryTriggerInteraction.Ignore);
            return !headBlocked;
        }
    }
}
