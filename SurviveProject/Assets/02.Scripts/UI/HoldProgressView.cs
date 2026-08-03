using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Survive.Interaction;

namespace Survive.UI
{
    /// <summary>
    /// 채집처럼 눌러서 채우는 상호작용의 진행도.
    ///
    /// 문구만으로는 "길게 눌러야 한다"가 잘 전달되지 않는다.
    /// 실제로 차오르는 게이지가 보여야 사용자가 손을 떼지 않는다.
    ///
    /// 한 번 "안 보인다"는 말을 두 번 들었다. 원인은 로직이 아니라 크기였다 —
    /// 1440p에서 높이 19px짜리 선은 어두운 화면에서 눈에 들어오지 않는다.
    /// 그래서 여기서는 <b>눈에 띄는 것</b>을 우선한다: 나타날 때 튀어오르고,
    /// 차오르는 동안 테두리가 빛나고, 완성되면 번쩍인다.
    /// </summary>
    [DisallowMultipleComponent]
    public class HoldProgressView : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] Image fill;

        [Tooltip("차오르는 동안 빛나는 테두리. 없어도 된다")]
        [SerializeField] Image frame;


        [Header("연출")]
        [SerializeField] float fadeSeconds = 0.1f;

        [Tooltip("나타날 때 튀어오르는 크기")]
        [SerializeField] float popScale = 1.18f;

        [SerializeField] Color fillLow = new Color(0.95f, 0.85f, 0.45f);
        [SerializeField] Color fillHigh = new Color(0.55f, 1f, 0.60f);

        PlayerInteractor _interactor;
        RectTransform _rect;
        Tween _fade;
        Tween _pop;
        bool _shown;
        Vector3 _baseScale;

        void Awake()
        {
            _rect = transform as RectTransform;
            _baseScale = transform.localScale;
        }

        IEnumerator Start()
        {
            ApplyVisible(false, immediate: true);

            // 플레이어가 준비될 때까지 기다린다
            for (int i = 0; i < 120 && _interactor == null; i++)
            {
                _interactor = Object.FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Exclude);
                if (_interactor != null) break;
                yield return null;
            }

            if (_interactor == null) yield break;
            _interactor.HoldProgressChanged += Refresh;
        }

        void OnDestroy()
        {
            if (_interactor != null) _interactor.HoldProgressChanged -= Refresh;
            _fade?.Kill();
            _pop?.Kill();
        }

        void OnEnable() => Survive.Core.GameServices.Register(this);
        void OnDisable() => Survive.Core.GameServices.Unregister<HoldProgressView>();

        /// <summary>
        /// 상호작용 말고 다른 경로(철거 등)에서 진행도를 밀어 넣는다.
        /// 게이지 하나를 여럿이 쓰는 편이 화면에 막대가 늘어나는 것보다 낫다.
        /// </summary>
        public void SetExternalProgress(float p) => Refresh(p);

        void Refresh(float progress)
        {
            bool wantVisible = progress > 0.001f;

            // 완성 직전까지 갔다가 0으로 떨어지면 완료다. 번쩍여서 알린다.
            if (_shown && !wantVisible && fill != null && fill.fillAmount > 0.95f)
                FlashComplete();

            if (fill != null)
            {
                fill.fillAmount = progress;
                fill.color = Color.Lerp(fillLow, fillHigh, progress);
            }
            if (frame != null)
                frame.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.25f, 0.9f, progress));

            // 매 프레임 트윈을 죽이고 다시 걸면 알파가 제자리걸음을 한다.
            // 상태가 바뀔 때만 건드린다.
            if (wantVisible != _shown) ApplyVisible(wantVisible);
        }

        void ApplyVisible(bool visible, bool immediate = false)
        {
            _shown = visible;
            if (group == null) return;

            _fade?.Kill();

            if (immediate)
            {
                group.alpha = visible ? 1f : 0f;
                return;
            }

            _fade = group.DOFade(visible ? 1f : 0f, fadeSeconds).SetUpdate(true);

            if (!visible) return;

            // 나타나는 순간 튀어오른다. 화면 어디를 봐야 하는지 알려주는 신호다.
            _pop?.Kill();
            transform.localScale = _baseScale * popScale;
            _pop = transform.DOScale(_baseScale, 0.18f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        void FlashComplete()
        {
            _pop?.Kill();
            transform.localScale = _baseScale;
            _pop = transform.DOPunchScale(_baseScale * 0.22f, 0.25f, 8, 0.6f).SetUpdate(true);
        }
    }
}
