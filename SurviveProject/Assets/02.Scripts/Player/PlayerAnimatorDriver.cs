using UnityEngine;

namespace Survive.Player
{
    [DisallowMultipleComponent]
    public class PlayerAnimatorDriver : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] PlayerLocomotion locomotion;
        [SerializeField] float walkSpeedReference = 5f;

        // 파라미터 이름은 기존 애니메이터 컨트롤러와 맞춰야 하므로 바꾸지 않는다.
        static readonly int planar = Animator.StringToHash("HorizontalMove");
        static readonly int vertical = Animator.StringToHash("VerticalMove");
        static readonly int speed = Animator.StringToHash("Speed");
        static readonly int moving = Animator.StringToHash("isMove");

        void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (locomotion == null) locomotion = GetComponent<PlayerLocomotion>();
        }

        void Update()
        {
            if (animator == null || locomotion == null) return;

            Vector3 localVel = transform.InverseTransformDirection(locomotion.PlanarVelocity);
            float magnitude = locomotion.CurrentSpeed;

            animator.SetFloat(planar, magnitude > 0.01f ? localVel.x / magnitude : 0f);
            animator.SetFloat(vertical, magnitude > 0.01f ? localVel.z / magnitude : 0f);
            animator.SetFloat(speed, walkSpeedReference <= 0f ? 1f : magnitude / walkSpeedReference);
            animator.SetBool(moving, magnitude > 0.1f);
        }
    }
}
