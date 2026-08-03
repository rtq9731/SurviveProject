using UnityEngine;
using DG.Tweening;

namespace Survive.Player
{
    /// <summary>
    /// 헤엄칠 때 몸을 눕힌다.
    ///
    /// 이 프로젝트의 애니메이터에는 수영 클립이 없다(Idle/Sprint/Run 계열뿐).
    /// 클립을 새로 만들지 않고도 "수영 중"이 읽히게 하는 가장 싼 방법은
    /// 몸을 앞으로 눕히고 기존 이동 블렌드를 그대로 돌리는 것이다 —
    /// 팔다리는 계속 움직이고 자세는 엎드린 자세가 된다.
    ///
    /// 클립이 생기면 이 컴포넌트를 끄고 애니메이터 상태로 옮기면 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class SwimPoseDriver : MonoBehaviour
    {
        [Tooltip("눕힐 대상. 보통 캐릭터 메시 루트(Armature의 부모)")]
        [SerializeField] Transform body;

        [SerializeField] PlayerSwimming swimming;

        [Header("자세")]
        [Tooltip("헤엄칠 때 앞으로 숙이는 각도")]
        [SerializeField] float pitchDegrees = 72f;

        [Tooltip("허리까지 잠겼을 때의 각도")]
        [SerializeField] float wadePitchDegrees = 12f;

        [Tooltip("자세가 바뀌는 데 걸리는 시간")]
        [SerializeField] float blendSeconds = 0.3f;

        [Tooltip("헤엄칠 때 몸이 위아래로 일렁이는 폭")]
        [SerializeField] float bobAmplitude = 0.08f;

        [SerializeField] float bobSpeed = 1.6f;

        Vector3 _restEuler;
        Vector3 _restLocalPos;
        float _current;
        float _target;
        float _bobPhase;

        void Awake()
        {
            if (swimming == null) swimming = GetComponentInParent<PlayerSwimming>();
            if (body == null)
            {
                // 메시 루트를 찾는다. 애니메이터가 붙은 것이 보통 그것이다.
                var anim = GetComponentInChildren<Animator>(true);
                body = anim != null ? anim.transform : transform;
            }
            _restEuler = body.localEulerAngles;
            _restLocalPos = body.localPosition;
        }

        void OnDisable()
        {
            if (body == null) return;
            body.localEulerAngles = _restEuler;
            body.localPosition = _restLocalPos;
        }

        void Update()
        {
            if (swimming == null || body == null) return;

            _target = swimming.IsSwimming ? pitchDegrees
                    : swimming.IsWading ? wadePitchDegrees
                    : 0f;

            // 자세는 부드럽게 따라간다. 물에 들어가자마자 툭 눕으면 어색하다.
            float k = 1f - Mathf.Exp(-Time.deltaTime * (5f / Mathf.Max(0.05f, blendSeconds)));
            _current = Mathf.Lerp(_current, _target, k);

            body.localEulerAngles = _restEuler + new Vector3(_current, 0f, 0f);

            if (swimming.IsSwimming)
            {
                _bobPhase += Time.deltaTime * bobSpeed;
                body.localPosition = _restLocalPos + Vector3.up * (Mathf.Sin(_bobPhase) * bobAmplitude);
            }
            else
            {
                _bobPhase = 0f;
                body.localPosition = Vector3.Lerp(body.localPosition, _restLocalPos, k);
            }
        }
    }
}
