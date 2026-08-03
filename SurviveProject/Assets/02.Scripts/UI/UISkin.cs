using UnityEngine;
using UnityEngine.UI;

namespace Survive.UI
{
    /// <summary>
    /// 코드로 만드는 UI가 쓰는 기본 판때기 그림.
    ///
    /// 유니티 내장 UISprite는 Filled로 쓰면 모양이 깨진다. 프로젝트에는
    /// 9-슬라이스 테두리(32px)가 잡힌 Semi_Round가 있으니 그것을 쓴다.
    /// 세 군데에서 각자 내장 리소스를 부르고 있었는데, 그러면 다음에
    /// 그림을 바꿀 때 또 세 군데를 고쳐야 한다.
    ///
    /// 런타임 코드는 AssetDatabase를 못 쓰므로 Resources 아래에 둔 사본을 읽는다.
    /// 못 찾으면 내장으로 돌아간다 — 그림 하나 없다고 UI가 사라지면 안 된다.
    /// </summary>
    public static class UISkin
    {
        const string PanelPath = "UI/Semi_Round";

        static Sprite _panel;

        /// <summary>패널·슬롯·막대에 두루 쓰는 둥근 판때기. 9-슬라이스다.</summary>
        public static Sprite Panel
        {
            get
            {
                if (_panel != null) return _panel;

                _panel = Resources.Load<Sprite>(PanelPath);
                if (_panel == null)
                {
                    Debug.LogWarning($"[UISkin] {PanelPath}를 찾지 못해 내장 스프라이트를 쓴다");
                    _panel = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
                }
                return _panel;
            }
        }

        /// <summary>
        /// 목록 길이에 맞춰 패널 높이를 늘린다.
        ///
        /// 패널 높이를 고정해 두면 항목이 늘어난 날 아래가 잘려 나간다.
        /// 건설 목록에 모듈 조각 다섯을 더하자마자 네 줄이 판때기 밖으로 흘렀다.
        ///
        /// 위아래 여백(제목 자리 포함)은 Rows의 인셋에서 그대로 읽는다 —
        /// 숫자를 코드에 또 적으면 디자인을 고칠 때 두 군데가 어긋난다.
        /// </summary>
        public static void FitPanelHeight(RectTransform panel, RectTransform rows,
                                          int visibleRows, float rowHeight, float spacing)
        {
            if (panel == null || rows == null) return;

            float chrome = rows.offsetMin.y - rows.offsetMax.y;   // 아래 여백 + 위 여백
            float content = visibleRows <= 0
                ? 0f
                : visibleRows * rowHeight + (visibleRows - 1) * spacing;

            panel.sizeDelta = new Vector2(panel.sizeDelta.x, content + chrome);
        }

        /// <summary>
        /// 판때기용 Image를 한 번에 맞춘다.
        /// Sliced로 두어야 32px 테두리가 살고, 크기를 키워도 모서리가 늘어나지 않는다.
        /// </summary>
        public static void ApplyPanel(Image image, Color color)
        {
            if (image == null) return;

            image.sprite = Panel;
            image.type = Image.Type.Sliced;
            image.color = color;
        }
    }
}
