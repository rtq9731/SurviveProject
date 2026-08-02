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
            보임(false, 즉시: true);

            // 플레이어가 준비될 때까지 기다린다
            for (int i = 0; i < 120 && _interactor == null; i++)
            {
                _interactor = Object.FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Exclude);
                if (_interactor != null) break;
                yield return null;
            }

            if (_interactor == null) yield break;
            _interactor.HoldProgressChanged += 갱신;
        }

        void OnDestroy()
        {
            if (_interactor != null) _interactor.HoldProgressChanged -= 갱신;
        }

        void 갱신(float 진행도)
        {
            if (fill != null) fill.fillAmount = 진행도;
            보임(진행도 > 0.001f);
        }

        void 보임(bool 보일까, bool 즉시 = false)
        {
            if (group == null) return;

            _fade?.Kill();
            if (즉시)
            {
                group.alpha = 보일까 ? 1f : 0f;
                return;
            }
            _fade = group.DOFade(보일까 ? 1f : 0f, 0.12f);
        }
    }
}
