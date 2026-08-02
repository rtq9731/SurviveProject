using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace Survive.Narrative
{
    /// <summary>
    /// 전체 화면 암전. CanvasGroup + DOTween.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public class ScreenFader : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;

        void Awake()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
        }

        /// <summary>화면이 밝아진다 (alpha 1 → 0).</summary>
        public IEnumerator FadeIn(float seconds)
        {
            group.blocksRaycasts = true;
            yield return group.DOFade(0f, seconds).SetUpdate(true).WaitForCompletion();
            group.blocksRaycasts = false;
        }

        /// <summary>화면이 어두워진다 (alpha 0 → 1).</summary>
        public IEnumerator FadeOut(float seconds)
        {
            group.blocksRaycasts = true;
            yield return group.DOFade(1f, seconds).SetUpdate(true).WaitForCompletion();
        }

        public void SetBlack(bool black)
        {
            group.alpha = black ? 1f : 0f;
            group.blocksRaycasts = black;
        }
    }
}
