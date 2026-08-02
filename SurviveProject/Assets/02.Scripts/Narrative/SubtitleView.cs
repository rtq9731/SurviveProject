using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Survive.Narrative
{
    /// <summary>
    /// 자막 한 줄을 표시한다.
    /// 기존 PanelDialog는 네 줄을 한꺼번에 띄우는 정적 패널이었다.
    /// 여기서는 한 줄짜리 패널을 순차로 갈아끼운다.
    /// </summary>
    [DisallowMultipleComponent]
    public class SubtitleView : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] Text label;
        [SerializeField] float fadeSeconds = 0.25f;

        void Awake()
        {
            if (group == null) group = GetComponent<CanvasGroup>();

            // 여기서 SetActive(false)를 하면 안 된다. 프리팹이 비활성으로 저장돼 있어
            // Show()의 SetActive(true)가 이 Awake를 처음 깨우는데, 그때 다시 숨겨버려
            // 자막이 영영 보이지 않는다. 알파만 0으로 둔다.
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
            }
        }

        public void Show(string speaker, string text)
        {
            gameObject.SetActive(true);   // Awake가 여기서 처음 돌 수 있다

            if (label != null)
                label.text = string.IsNullOrEmpty(speaker) ? text : $"{speaker} : {text}";

            if (group != null)
            {
                group.DOKill();
                group.DOFade(1f, fadeSeconds).SetUpdate(true);
            }
        }

        public void HideView(bool immediate = false)
        {
            if (group == null)
            {
                gameObject.SetActive(false);
                return;
            }

            group.DOKill();
            if (immediate)
            {
                group.alpha = 0f;
                gameObject.SetActive(false);
                return;
            }

            group.DOFade(0f, fadeSeconds).SetUpdate(true)
                 .OnComplete(() => gameObject.SetActive(false));
        }
    }
}
