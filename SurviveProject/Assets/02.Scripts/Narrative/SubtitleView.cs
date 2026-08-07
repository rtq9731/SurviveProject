using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;
using Survive.Localization;

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
        [SerializeField] TMP_Text label;
        [SerializeField] float fadeSeconds = 0.25f;

        /// <summary>
        /// 캔버스에 자막 한 줄을 세운다. 이미 있으면 그것을 쓴다.
        ///
        /// 프롤로그 씬에는 자막판이 놓여 있지만 본 게임 씬에는 없다. 씬 파일을
        /// 고치는 대신 런타임에 붙인다 — CraftQueueView가 대기열 띠를 붙이는
        /// 것과 같은 방식이고, 여러 갈래로 나뉜 작업이 같은 씬 파일에서
        /// 부딪히는 일도 없앤다.
        /// </summary>
        public static SubtitleView Ensure(Transform canvasParent, TMP_FontAsset font)
        {
            if (canvasParent == null) return null;

            var existing = canvasParent.GetComponentInChildren<SubtitleView>(true);
            if (existing != null) return existing;

            var go = new GameObject("SubtitleLine", typeof(RectTransform));
            go.transform.SetParent(canvasParent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            // 대기열 띠(y 100)와 퀵슬롯 위. 그보다 위에 앉힌다.
            rt.anchoredPosition = new Vector2(0f, 180f);
            rt.sizeDelta = new Vector2(920f, 84f);

            var plate = go.AddComponent<Image>();
            plate.color = new Color(0f, 0f, 0f, 0.55f);
            plate.raycastTarget = false;

            var cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(18f, 8f);
            trt.offsetMax = new Vector2(-18f, -8f);

            var txt = textGo.AddComponent<TextMeshProUGUI>();
            if (font != null) txt.font = font;
            txt.fontSize = 22f;
            txt.enableAutoSizing = true;
            txt.fontSizeMin = 15f;
            txt.fontSizeMax = 22f;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = new Color(0.92f, 0.95f, 1f, 1f);
            txt.raycastTarget = false;

            var view = go.AddComponent<SubtitleView>();
            view.group = cg;
            view.label = txt;
            return view;
        }

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

        void OnDestroy() => group?.DOKill();

        /// <summary>
        /// 자막 한 줄을 띄운다. 넘어오는 <paramref name="speaker"/>·<paramref name="text"/>는
        /// 이미 표를 거친 값이다.
        ///
        /// <b>화자와 자막을 잇는 꼴도 표에서 나온다.</b> <c>" : "</c>를 코드에 박으면
        /// 그 기호가 언어마다 다르다는 사실을 표현할 자리가 없어진다 — 중국어·일본어는
        /// 전각 기호를 쓰고, 언어에 따라 화자를 뒤에 붙이는 편이 자연스러울 수도 있다
        /// (docs/번역-체계.md §3). 자리표 순서를 바꿀 수 있게 통짜 문장으로 둔다.
        /// </summary>
        public void Show(string speaker, string text)
        {
            gameObject.SetActive(true);   // Awake가 여기서 처음 돌 수 있다

            if (label != null)
                label.text = string.IsNullOrEmpty(speaker)
                    ? text
                    : Loc.F("UI", "subtitle_line", speaker, text);

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
