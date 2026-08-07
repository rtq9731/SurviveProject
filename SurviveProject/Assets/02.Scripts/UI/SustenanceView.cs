using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Survive.Domain.Art;
using Survive.Localization;
using Survive.Vitals;

namespace Survive.UI
{
    /// <summary>
    /// 수분과 식량. <b>평소에는 화면에 없다.</b>
    ///
    /// <b>이것이 이 항목 전체의 설계다.</b> 수분·식량은 한 번 기각됐던 것이고
    /// 그 근거는 *"시계가 둘이면 하나는 반드시 잡무가 된다"*였다(기획서 §0.2).
    /// 게이지를 늘 띄워 두면 그 근거가 그 자리에서 되살아난다 — 눈에 들어오는
    /// 게이지는 쳐다보게 되고, 쳐다보는 게이지는 관리 대상이 되며, 관리 대상이
    /// 된 순간 그것은 <b>배터리가 이미 하는 일의 반복</b>이다.
    ///
    /// 그래서 <see cref="Sustenance.WarningThreshold"/> 위에서는 아무것도 그리지
    /// 않는다. 화면에 나타나는 것 자체가 신호다 — <b>보이면 낮은 것</b>이라
    /// 눈금을 읽을 필요조차 없다. 원정 한 번을 돌아도 나타나지 않는다는 것이
    /// 이 라운드의 게이트이고, <c>E2ESustenance</c>가 그것을 잰다.
    ///
    /// <b>둘 다 청록인 이유.</b> 광원 4색의 버섯 청록(<see cref="ArtPalette.Glowshroom"/>)은
    /// 생명과 안전의 색이고 지상에서는 그것이 곧 호수다 — 멀리서 청록이 보이면
    /// 물과 식량이 있다는 뜻이다(기획서 §2.1). 화면 아래의 두 줄이 같은 색인 것은
    /// <b>둘이 같은 곳에서 온다</b>는 말이고, 그 한 문장이 이 항목의 전부다.
    ///
    /// <b>씬을 고치지 않는다.</b> 스스로 선다 — <see cref="PickupFeedView"/>·
    /// <see cref="ItemTooltipView"/>와 같은 관례다.
    /// </summary>
    [DisallowMultipleComponent]
    public class SustenanceView : MonoBehaviour
    {
        /// <summary>씬 HUD(0) 위, 획득 목록(2500) 아래.</summary>
        public const int SortingOrder = 2400;

        static SustenanceView _instance;
        public static SustenanceView Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_instance != null) return;

            var go = new GameObject("SustenanceView");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SustenanceView>();
        }

        // ── 자리와 치수 ──────────────────────────────────────────
        //
        // 1920x1080 기준. 오른쪽 아래의 체력·산소 막대(InfoBar: (-32, 32), 높이 128)
        // 바로 위다. 같은 종류의 것을 같은 자리에 모아 두어야, 나타났을 때
        // 「어디에 뜬 무엇인가」를 찾지 않는다.

        const float RightMargin = 32f;
        const float BottomMargin = 176f;
        const float Width = 220f;
        const float RowHeight = 14f;
        const float RowSpacing = 8f;
        const float LabelWidth = 52f;
        const float FadeSeconds = 0.35f;

        // 틀은 검은 화면 위에서도 보여야 한다. 이 게임의 배경은 대부분 검정이라
        // 다른 판때기의 색(0.07)을 그대로 쓰면 틀이 사라지고, 그러면 채움만 남아
        // 「얼마나 남았는가」가 아니라 「무언가 조금 있다」로만 읽힌다.
        static readonly Color Frame = new Color(0.17f, 0.19f, 0.23f, 0.80f);
        static readonly Color LabelColor = new Color(0.82f, 0.86f, 0.90f, 1f);

        GameObject _root;
        Row _hydration;
        Row _food;

        sealed class Row
        {
            public CanvasGroup Group;
            public Image Fill;
            public TMP_Text Label;
            public RectTransform FillRect;
        }

        /// <summary>지금 화면에 실제로 보이는가. 검증 하네스가 읽는다.</summary>
        public bool HydrationVisible => _hydration != null && _hydration.Group.alpha > 0.01f;

        /// <inheritdoc cref="HydrationVisible"/>
        public bool FoodVisible => _food != null && _food.Group.alpha > 0.01f;

        /// <summary>어느 쪽이든 보이는가.</summary>
        public bool AnyVisible => HydrationVisible || FoodVisible;

        void Awake() => Build();

        void Update()
        {
            if (_root == null) return;

            Refresh(_hydration, SustenanceService.HydrationNormalized);
            Refresh(_food, SustenanceService.FoodNormalized);
        }

        void Refresh(Row row, float normalized)
        {
            if (row == null) return;

            float target = Sustenance.IsWarning(normalized) ? 1f : 0f;
            row.Group.alpha = Mathf.MoveTowards(row.Group.alpha, target,
                                                Time.unscaledDeltaTime / FadeSeconds);

            // 옅어지는 동안에도 눈금은 따라간다 - 사라지는 중에 값이 얼어 있으면
            // 다시 나타날 때 옛 눈금이 한 프레임 스친다.
            float w = (Width - LabelWidth) * Mathf.Clamp01(normalized);
            row.FillRect.sizeDelta = new Vector2(w, RowHeight);
        }

        // ── 짓기 ─────────────────────────────────────────────────

        void Build()
        {
            _root = new GameObject("SustenanceCanvas",
                                   typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _root.transform.SetParent(transform, false);

            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 두 줄을 아래에서 위로 쌓는다. 수분이 아래, 식량이 위다 -
            // 빨리 주는 쪽이 자주 나타나므로 눈이 가장 먼저 닿는 자리에 둔다.
            _hydration = BuildRow(0, Sustenance.HydrationId, 1f);
            _food = BuildRow(1, Sustenance.FoodId, 0.72f);
        }

        Row BuildRow(int index, string vitalId, float tint)
        {
            float y = BottomMargin + index * (RowHeight + RowSpacing);

            var go = new GameObject("Row_" + vitalId, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(_root.transform, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-RightMargin, y);
            rect.sizeDelta = new Vector2(Width, RowHeight);

            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var label = NewText(rect, LabelWidth, NameOf(vitalId));
            label.alignment = TextAlignmentOptions.MidlineRight;

            // 막대. 틀 위에 채움을 얹는다 - 틀만 있어도 「무엇이 얼마나 남았는가」가
            // 읽히므로 눈금 글자를 따로 두지 않는다. 숫자를 적으면 그것을 읽게 되고,
            // 읽게 되는 순간 이것은 다시 시계가 된다.
            var frame = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            frame.transform.SetParent(rect, false);
            var frameRect = (RectTransform)frame.transform;
            frameRect.anchorMin = frameRect.anchorMax = frameRect.pivot = new Vector2(0f, 0.5f);
            frameRect.anchoredPosition = new Vector2(LabelWidth, 0f);
            frameRect.sizeDelta = new Vector2(Width - LabelWidth, RowHeight);
            UISkin.ApplyPanel(frame.GetComponent<Image>(), Frame);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(frameRect, false);
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = fillRect.anchorMax = fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(Width - LabelWidth, RowHeight);

            var image = fill.GetComponent<Image>();
            var c = ArtPalette.Glowshroom * tint;
            c.a = 0.92f;
            image.color = c;
            image.raycastTarget = false;

            return new Row { Group = group, Fill = image, Label = label, FillRect = fillRect };
        }

        TMP_Text NewText(RectTransform parent, float width, string text)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(width - 8f, RowHeight + 6f);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 15f;
            tmp.color = LabelColor;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>
        /// 게이지의 이름. <b>표를 지난다</b> — 에셋에 적힌 원문은 표가 없을 때의
        /// 폴백이다(<see cref="DataText"/>).
        /// </summary>
        static string NameOf(string vitalId)
        {
            string file = char.ToUpperInvariant(vitalId[0]) + vitalId.Substring(1);
            var def = Resources.Load<VitalDefinitionSO>(file);
            return DataText.Name(def);
        }
    }
}
