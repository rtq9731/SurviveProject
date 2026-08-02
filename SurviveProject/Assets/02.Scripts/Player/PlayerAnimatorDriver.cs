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
        static readonly int 수평 = Animator.StringToHash("HorizontalMove");
        static readonly int 수직 = Animator.StringToHash("VerticalMove");
        static readonly int 속도 = Animator.StringToHash("Speed");
        static readonly int 이동중 = Animator.StringToHash("isMove");

        void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (locomotion == null) locomotion = GetComponent<PlayerLocomotion>();
        }

        void Update()
        {
            if (animator == null || locomotion == null) return;

            Vector3 지역속도 = transform.InverseTransformDirection(locomotion.PlanarVelocity);
            float 크기 = locomotion.CurrentSpeed;

            animator.SetFloat(수평, 크기 > 0.01f ? 지역속도.x / 크기 : 0f);
            animator.SetFloat(수직, 크기 > 0.01f ? 지역속도.z / 크기 : 0f);
            animator.SetFloat(속도, walkSpeedReference <= 0f ? 1f : 크기 / walkSpeedReference);
            animator.SetBool(이동중, 크기 > 0.1f);
        }
    }
}
