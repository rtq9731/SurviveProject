using UnityEngine;
using Survive.InputSystem;

namespace Survive.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerLocomotion : MonoBehaviour
    {
        [SerializeField] InputReaderSO input;

        [Header("이동")]
        [SerializeField] float walkSpeed = 5f;
        [SerializeField] float runSpeed = 7f;
        [SerializeField] float gravityScale = 1f;
        [SerializeField] float jumpPower = 2f;
        [SerializeField] float jumpCooldown = 1f;

        CharacterController _cc;
        Vector2 _입력 = Vector2.zero;
        float _수직속도;
        float _다음점프시각;
        bool _잠김;

        public bool IsGrounded => _cc != null && _cc.isGrounded;
        public float CurrentSpeed { get; private set; }
        public Vector3 PlanarVelocity { get; private set; }

        void Awake() => _cc = GetComponent<CharacterController>();

        void OnEnable()
        {
            if (input == null) return;
            input.MoveEvent += 이동입력;
            input.JumpEvent += 점프;
        }

        void OnDisable()
        {
            if (input == null) return;
            input.MoveEvent -= 이동입력;
            input.JumpEvent -= 점프;
        }

        void 이동입력(Vector2 v) => _입력 = v;

        void 점프()
        {
            if (_잠김) return;
            if (Time.time < _다음점프시각) return;
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

            Vector3 방향 = _잠김 ? Vector3.zero : new Vector3(_입력.x, 0f, _입력.y);
            if (방향.sqrMagnitude > 1f) 방향.Normalize();

            bool 달림 = input != null && input.IsSprinting;
            float 속도 = 달림 ? runSpeed : walkSpeed;

            Vector3 수평 = transform.TransformDirection(방향) * 속도;

            if (_cc.isGrounded && _수직속도 < 0f) _수직속도 = -1f;   // 지면에 붙여둔다
            _수직속도 += -9.81f * gravityScale * dt;

            Vector3 전체 = 수평 + Vector3.up * _수직속도;
            _cc.Move(전체 * dt);

            PlanarVelocity = 수평;
            CurrentSpeed = 수평.magnitude;
        }
    }
}
