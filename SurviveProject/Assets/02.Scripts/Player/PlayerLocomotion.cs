using UnityEngine;
using Survive.Combat;
using Survive.Input;
using Survive.World;

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

        // 몸이 옮겨졌는지 가리는 여유. CharacterController는 부탁한 것보다 멀리 가지 않는다 —
        // 계단을 밟고 오르거나 경사에 붙는 만큼만 더 움직인다. 그 여유를 넘어서는 이동은
        // 우리가 시킨 것이 아니라 누군가 좌표를 바꾼 것이다(부활·검증 하네스).
        const float TeleportSlack = 1f;

        CharacterController _cc;
        PlayerDamageReceiver _damage;
        PlayerFootsteps _footsteps;
        Vector2 _moveInput = Vector2.zero;
        float _verticalSpeed;
        float _nextJumpTime;
        bool _locked;

        // ── 낙하 추적 ──
        Vector3 _lastPosition;
        Vector3 _lastRequestedMove;
        bool _hasLastPosition;
        bool _wasGrounded = true;

        // 이번 체공이 낙하로 셈해지지 않는 상태. 몸이 옮겨졌거나 물에 닿으면 선다.
        // 발이 다시 땅에 닿는 순간 풀린다 — 다음 낙하는 정상으로 센다.
        bool _fallDisarmed;

        public bool IsGrounded => _cc != null && _cc.isGrounded;
        public float CurrentSpeed { get; private set; }
        public Vector3 PlanarVelocity { get; private set; }

        /// <summary>
        /// 바깥에서 거는 걸음 배율. 1이면 아무 일도 없다.
        ///
        /// <b>왜 여기에 창구를 하나 두는가.</b> 몸이 느려질 이유가 늘어날 때마다
        /// <see cref="GroundMove"/> 안에 <c>if</c>를 하나씩 더하면, 이유들이 서로
        /// 곱해지는지 이기는지가 이 파일 안에 흩어져 버린다. 창구를 하나로 두면
        /// <b>누가 얼마를 걸지는 거는 쪽이 정하고</b> 여기서는 곱하기 한 번만 한다.
        /// 지금 이것을 거는 것은 수분·식량이 바닥일 때의 <see cref="Sustenance"/>이고,
        /// 그쪽이 이미 「나쁜 쪽 하나만 쓴다」로 여럿을 하나로 접어 온다.
        ///
        /// 허리까지 잠겨 느려지는 것(<c>wadeSpeedFactor</c>)과는 곱해진다 — 그쪽은
        /// 물에 든 몸의 물리이고 이쪽은 몸의 상태라, 서로 대신할 수 있는 것이 아니다.
        /// </summary>
        public float ExternalSpeedFactor { get; set; } = 1f;

        /// <summary>마지막으로 땅에 닿을 때의 하강 속도(m/s). 무해했어도 적는다 — 실측용이다.</summary>
        public float LastLandingSpeed { get; private set; }

        /// <summary>그 착지로 실제로 입은 피해. 무해했으면 0.</summary>
        public float LastFallDamage { get; private set; }

        /// <summary>지금까지 낙하로 입은 피해의 합. 구간별로 빼서 보라고 둔 것이다.</summary>
        public float TotalFallDamage { get; private set; }

        /// <summary>땅에 닿은 횟수. "아프지 않았다"와 "아예 착지가 없었다"를 가른다.</summary>
        public int LandingCount { get; private set; }

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (swimming == null) swimming = GetComponent<PlayerSwimming>();
            if (cameraRig == null) cameraRig = GetComponent<PlayerCameraRig>();
            _damage = GetComponentInChildren<PlayerDamageReceiver>(true);

            // 발소리는 이동 상태에서만 잴 수 있다. 플레이어 프리팹을 고치지 않으려고
            // 여기서 붙인다 — CreatureBrain이 CreatureCollisionGuard를 붙이는 것과 같다.
            // 소리가 하나도 없는 지금은 붙어 있어도 아무것도 재생하지 않는다.
            _footsteps = GetComponent<PlayerFootsteps>();
            if (_footsteps == null) _footsteps = gameObject.AddComponent<PlayerFootsteps>();
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

            // 성사된 도약만 소리를 낸다. 쿨타임에 막힌 시도까지 소리가 나면
            // 안 뛰었는데 뛴 것으로 들린다.
            _footsteps?.Jump();
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

            TrackFall();

            Vector3 dir = _locked ? Vector3.zero : new Vector3(_moveInput.x, 0f, _moveInput.y);
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            if (isSwimming) SwimMove(dir, dt);
            else GroundMove(dir, dt);

            _lastPosition = transform.position;
            _hasLastPosition = true;
        }

        // ── 낙하 대가 ────────────────────────────────────────────

        /// <summary>
        /// 떨어졌다가 굳은 땅에 부딪히면 대가를 치른다.
        ///
        /// 여기서 하는 일은 셋뿐이다 — 낙하가 아닌 것을 걸러 내고(순간이동·입수),
        /// 착지 순간을 잡고, 그때의 하강 속도를 <see cref="FallImpact"/>에 묻는다.
        /// 얼마나 아픈가는 Unity 없이 시험되는 그쪽에 있다.
        ///
        /// 이동 자체는 건드리지 않는다. 낙하 판정을 위해 <c>_verticalSpeed</c>를
        /// 손보기 시작하면 점프·부력·물가 기어오르기가 전부 같이 흔들린다.
        /// </summary>
        void TrackFall()
        {
            // 몸이 옮겨졌는가. CharacterController는 부탁한 것보다 멀리 가지 않으므로,
            // 그보다 멀리 갔다면 부활이나 검증 하네스가 좌표를 바꾼 것이다.
            // 옮겨진 뒤의 첫 착지는 낙하가 아니다.
            bool teleported = false;
            if (_hasLastPosition)
            {
                float moved = (transform.position - _lastPosition).magnitude;
                teleported = moved > _lastRequestedMove.magnitude + TeleportSlack;
                if (teleported) _fallDisarmed = true;
            }

            // 물에 닿았으면 아무 높이에서 떨어졌어도 없던 일이 된다.
            if (InWater()) _fallDisarmed = true;

            bool grounded = _cc.isGrounded;

            // 발이 닿은 첫 프레임에는 하강 속도가 아직 그대로 남아 있다 —
            // GroundMove가 -1로 눌러 버리기 전인 지금이 부딪힌 속도를 읽을 유일한 때다.
            if (grounded && !_wasGrounded) Land(Mathf.Max(0f, -_verticalSpeed));

            // 옮겨진 프레임에는 발판을 풀지 않는다.
            //
            // CharacterController의 isGrounded는 마지막 Move가 남긴 값이라, 땅에 서 있던
            // 몸을 하늘로 옮긴 그 프레임에도 여전히 "닿아 있음"이다. 그 값을 믿고 걸쇠를
            // 풀면 방금 세운 것이 같은 줄에서 바로 지워지고, 부활이 사람을 높은 데
            // 세울 때마다 되살아나자마자 떨어져 죽는다. 실제로 그렇게 죽었다.
            if (grounded && !teleported) _fallDisarmed = false;
            _wasGrounded = grounded;
        }

        void Land(float impactSpeed)
        {
            LastLandingSpeed = impactSpeed;
            LandingCount++;

            float damage = _fallDisarmed ? 0f : FallImpact.DamageFor(impactSpeed);
            LastFallDamage = damage;
            if (damage <= 0f) return;

            TotalFallDamage += damage;

            if (_damage == null) _damage = GetComponentInChildren<PlayerDamageReceiver>(true);
            if (_damage == null) return;

            // 가해자가 없는 피해다. 때린 것은 땅이므로 자기 타격 걸름막에 걸리지 않는다.
            _damage.TakeDamage(new DamageInfo(damage, null, transform.position, Vector3.up));
        }

        /// <summary>
        /// 발이 물에 잠겼는가.
        ///
        /// <see cref="PlayerSwimming"/>의 상태를 빌리지 않고 직접 보는 이유는 같은 프레임
        /// 안의 실행 순서에 기대지 않기 위해서다 — 빠르게 떨어질 때는 한 프레임이 몇 미터라,
        /// 상태가 한 박자만 늦어도 물에 빠진 사람이 바닥에 부딪힌 것으로 셈해진다.
        /// </summary>
        bool InWater()
        {
            if (!WaterBody.TryGetSurfaceAt(transform.position, out float surfaceY)) return false;

            float feetY = transform.position.y - _cc.height * 0.5f + _cc.center.y;
            return feetY < surfaceY;
        }

        void GroundMove(Vector3 dir, float dt)
        {
            bool sprinting = input != null && input.IsSprinting;
            float speed = sprinting ? runSpeed : walkSpeed;

            // 허리까지 잠기면 느려진다
            if (swimming != null && swimming.IsWading) speed *= wadeSpeedFactor;

            speed *= Mathf.Max(0.01f, ExternalSpeedFactor);

            Vector3 planar = transform.TransformDirection(dir) * speed;

            if (_cc.isGrounded && _verticalSpeed < 0f) _verticalSpeed = -1f;
            _verticalSpeed += -9.81f * gravityScale * dt;

            _lastRequestedMove = (planar + Vector3.up * _verticalSpeed) * dt;
            _cc.Move(_lastRequestedMove);

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

            // 헤엄에도 같은 배율이 걸린다. 걷기만 느려지면 목마른 사람이 헤엄쳐
            // 도망치는 것이 최적해가 되어, 벌이 오히려 이동 방식을 고르게 만든다.
            speed *= Mathf.Max(0.01f, ExternalSpeedFactor);

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

            _lastRequestedMove = (MoveTo + Vector3.up * _verticalSpeed) * dt;
            _cc.Move(_lastRequestedMove);

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
