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
    /// </summary>
    [DisallowMultipleComponent]
    public class HoldProgressView : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] Image fill;

        PlayerInteractor _interactor;
        Tween _fade;

        IEnumerator Start()
        {
            SetVisible(false, immediate: true);

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
        }

        void Refresh(float progress)
        {
            if (fill != null) fill.fillAmount = progress;
            SetVisible(progress > 0.001f);
        }

        void SetVisible(bool visible, bool immediate = false)
        {
            if (group == null) return;

            _fade?.Kill();
            if (immediate)
            {
                group.alpha = visible ? 1f : 0f;
                return;
            }
            _fade = group.DOFade(visible ? 1f : 0f, 0.12f);
        }
    }
}
