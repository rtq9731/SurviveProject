using UnityEngine;
using DG.Tweening;

namespace Survive.Combat
{
    /// <summary>
    /// 손에 든 도구를 실제로 휘두른다.
    ///
    /// 화면을 번쩍이는 것으로 타격감을 내려 했더니 눈이 아프다는 말을 들었다.
    /// 당연한 일이다 — 때린 것은 도구인데 반응하는 것은 화면 전체였다.
    /// 반응은 때린 물건에서 나와야 한다.
    ///
    /// 애니메이터 클립을 만들지 않고 트윈으로 도는 이유: 도구 모델이 손 소켓
    /// 아래 자식으로 붙어 있을 뿐 각자 리그가 없다. 로컬 회전을 돌리는 것이
    /// 지금 구조에서 가장 적은 것을 건드린다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ToolSwingAnimator : MonoBehaviour
    {
        [Tooltip("손 소켓. 비우면 PlayerToolHolder의 것을 찾는다")]
        [SerializeField] Transform handSocket;

        [Header("휘두르기")]
        [Tooltip("치켜드는 시간")]
        [SerializeField] float windUpSeconds = 0.09f;

        [Tooltip("내리치는 시간")]
        [SerializeField] float strikeSeconds = 0.07f;

        [Tooltip("제자리로 돌아오는 시간")]
        [SerializeField] float recoverSeconds = 0.16f;

        [Tooltip("치켜들 때의 각도")]
        [SerializeField] Vector3 windUpAngles = new Vector3(-55f, 10f, 0f);

        [Tooltip("내리쳤을 때의 각도")]
        [SerializeField] Vector3 strikeAngles = new Vector3(48f, -14f, 0f);

        [Tooltip("내리칠 때 앞으로 나가는 거리")]
        [SerializeField] float lungeDistance = 0.12f;

        Quaternion _restRotation;
        Vector3 _restPosition;
        Sequence _swing;
        Tween _impact;
        bool _captured;

        void Awake()
        {
            // 인스펙터 배선이 없으면 이름으로 찾는다
            if (handSocket == null)
            {
                var holder = GetComponentInParent<Survive.Player.PlayerToolHolder>();
                if (holder != null) handSocket = FindSocket(holder.transform);
            }
            Capture();
        }

        static Transform FindSocket(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "jointItemR") return t;
            return null;
        }

        void Capture()
        {
            if (handSocket == null || _captured) return;
            _restRotation = handSocket.localRotation;
            _restPosition = handSocket.localPosition;
            _captured = true;
        }

        /// <summary>한 번 휘두른다. MeleeSwing이 부른다.</summary>
        public void Play()
        {
            if (handSocket == null) return;
            Capture();

            // 연타로 들어와도 자세가 어긋나지 않게 항상 기준으로 되돌리고 시작한다.
            _swing?.Kill();
            handSocket.localRotation = _restRotation;
            handSocket.localPosition = _restPosition;

            var windUp = _restRotation * Quaternion.Euler(windUpAngles);
            var strike = _restRotation * Quaternion.Euler(strikeAngles);
            var lunge = _restPosition + Vector3.forward * lungeDistance;

            _swing = DOTween.Sequence()
                .Append(handSocket.DOLocalRotateQuaternion(windUp, windUpSeconds).SetEase(Ease.OutQuad))
                .Append(handSocket.DOLocalRotateQuaternion(strike, strikeSeconds).SetEase(Ease.InQuad))
                .Join(handSocket.DOLocalMove(lunge, strikeSeconds).SetEase(Ease.InQuad))
                .Append(handSocket.DOLocalRotateQuaternion(_restRotation, recoverSeconds).SetEase(Ease.OutBack))
                .Join(handSocket.DOLocalMove(_restPosition, recoverSeconds).SetEase(Ease.OutQuad));
        }

        /// <summary>무언가에 맞았을 때. 멈칫하는 반동을 준다.</summary>
        public void PlayImpact()
        {
            if (handSocket == null) return;
            // 휘두르던 트윈은 그대로 두고 흔들림만 얹는다.
            _impact?.Kill();
            _impact = handSocket.DOShakePosition(0.12f, 0.035f, 14, 60f, false, false);
        }

        void OnDestroy()
        {
            _swing?.Kill();
            _impact?.Kill();
        }
    }
}
