using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Survive.Items;

namespace Survive.UI
{
    /// <summary>
    /// 커서가 물건 위에 머무는 동안 뜨는 쪽지 하나.
    ///
    /// <b>왜 하나뿐인가.</b> 소지품·제작·보관함·도감이 각자 쪽지를 만들면
    /// 다음에 글꼴이나 여백을 고칠 때 네 군데를 고쳐야 하고, 한 군데를 빠뜨리면
    /// 같은 게임 안에서 쪽지가 두 가지 모양으로 뜬다. 어느 화면에서든
    /// <see cref="Show(ItemDataSO, Vector2, Component, string)"/> 하나로 부른다.
    ///
    /// <b>패널 스택에 들어가지 않는다.</b> 이것은 ESC로 닫는 물건이 아니라
    /// 커서를 치우면 사라지는 것이다. <see cref="UIStateService"/>에 등록하면
    /// ESC 한 번을 삼켜 정작 닫으려던 소지품이 안 닫힌다.
    ///
    /// <b>씬을 고치지 않는다.</b> 스스로 선다 — <see cref="CodexUI"/>·
    /// <see cref="CraftQueueView"/>와 같은 관례다.
    ///
    /// <b>포인터를 먹지 않는다.</b> 쪽지에 GraphicRaycaster를 달지 않고 글자마다
    /// raycastTarget을 꺼 둔다. 커서 아래에 쪽지가 끼어들면 곧바로 PointerExit가
    /// 날아와 떴다 사라졌다를 반복한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ItemTooltipView : MonoBehaviour
    {
        /// <summary>도감(3000) 위, 밝기 조절 화면(5000) 아래.</summary>
        public const int SortingOrder = 4000;

        static ItemTooltipView _instance;
        public static ItemTooltipView Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_instance != null) return;

            var go = new GameObject("ItemTooltipView");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ItemTooltipView>();
        }

        // ── 상태 ─────────────────────────────────────────────────

        GameObject _root;
        RectTransform _canvasRect;
        RectTransform _panel;
        Canvas _canvas;
        TMP_Text _title;
        TMP_Text _body;
        TMP_Text _meta;
        TMP_Text _detail;

        /// <summary>지금 쪽지를 띄운 쪽. 그 화면이 닫히면 쪽지도 같이 걷힌다.</summary>
        Component _owner;

        /// <summary>지금 화면에 떠 있는가. 검증 하네스가 본다.</summary>
        public bool IsVisible => _root != null && _root.activeSelf;

        /// <summary>지금 설명하고 있는 물건. 검증 하네스가 본다.</summary>
        public ItemDataSO Item { get; private set; }

        /// <summary>쪽지가 놓인 자리(화면 픽셀). 화면 밖으로 나갔는지 여기서 잰다.</summary>
        public Rect ScreenRect { get; private set; }

        void OnDisable()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// 쪽지를 띄운 화면이 닫히거나 사라지면 쪽지도 걷는다.
        /// PointerExit는 오브젝트가 꺼지는 순간에는 오지 않는다 — 소지품을 닫으면
        /// 쪽지만 허공에 남는다.
        /// </summary>
        void LateUpdate()
        {
            if (!IsVisible) return;

            // 주인 없이 띄운 쪽지는 부른 쪽이 알아서 걷는다.
            if (ReferenceEquals(_owner, null)) return;

            // 파괴됐거나(Unity의 == 비교) 꺼졌으면 쪽지도 따라 사라진다.
            if (_owner == null || !_owner.gameObject.activeInHierarchy) HideNow();
        }

        // ── 창구 ─────────────────────────────────────────────────

        /// <summary>
        /// 물건 하나를 커서 옆에 설명한다.
        /// </summary>
        /// <param name="item">설명할 물건. null이면 쪽지를 걷는다.</param>
        /// <param name="screenPosition">커서 자리(화면 픽셀).</param>
        /// <param name="owner">띄운 쪽. 그 화면이 닫히면 쪽지도 걷힌다.</param>
        /// <param name="detail">물건 설명 아래에 덧붙일 한 덩어리(예: 제작 재료). 없으면 null.</param>
        public static void Show(ItemDataSO item, Vector2 screenPosition,
                                Component owner = null, string detail = null)
        {
            if (_instance == null) Install();
            _instance?.ShowInternal(item, screenPosition, owner, detail);
        }

        /// <summary>
        /// 쪽지를 걷는다. <paramref name="owner"/>를 주면 그 쪽이 띄운 것일 때만 걷는다 —
        /// 칸에서 칸으로 커서를 옮기면 나중 칸의 Enter가 먼저 도착할 수 있는데,
        /// 그때 앞 칸의 Exit가 새로 뜬 쪽지를 지워 버리면 안 된다.
        /// </summary>
        public static void Hide(Component owner = null)
        {
            if (_instance == null) return;
            if (owner != null && !ReferenceEquals(_instance._owner, owner)) return;
            _instance.HideNow();
        }

        // ── 알맹이 ───────────────────────────────────────────────

        void ShowInternal(ItemDataSO item, Vector2 screenPosition, Component owner, string detail)
        {
            if (!ItemTooltipContent.CanShow(item)) { HideNow(); return; }

            if (_root == null) Build();

            Item = item;
            _owner = owner;

            _title.text = ItemTooltipContent.Title(item);

            string body = ItemTooltipContent.Body(item);
            _body.text = body;
            _body.gameObject.SetActive(!string.IsNullOrEmpty(body));

            _detail.text = detail ?? "";
            _detail.gameObject.SetActive(!string.IsNullOrWhiteSpace(detail));

            _meta.text = ItemTooltipContent.Meta(item);

            _root.SetActive(true);
            Reposition(screenPosition);
        }

        void HideNow()
        {
            Item = null;
            _owner = null;
            if (_root != null) _root.SetActive(false);
        }

        /// <summary>
        /// 잰 크기로 자리를 정한다.
        ///
        /// 픽셀이 아니라 <b>캔버스 단위</b>로 계산한다. CanvasScaler가 1920 기준으로
        /// 화면을 늘였다 줄였다 하므로, 픽셀로 재고 캔버스 좌표에 넣으면 해상도가
        /// 바뀔 때마다 여백이 어긋난다.
        /// </summary>
        void Reposition(Vector2 screenPosition)
        {
            float scale = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;

            Vector2 screen = _canvasRect.rect.size;
            float width = TooltipPlacement.ClampWidth(DesiredWidth(), screen.x);
            _panel.sizeDelta = new Vector2(width, _panel.sizeDelta.y);

            // 글자가 몇 줄로 접히는지는 폭이 정해진 뒤에야 안다. 지금 재지 않으면
            // 한 프레임 늦은 높이로 자리를 잡아 쪽지가 아래로 삐져나간다.
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);

            var size = new Vector2(_panel.rect.width, _panel.rect.height);
            var cursor = screenPosition / scale;
            var placed = TooltipPlacement.Place(cursor, size, screen);

            _panel.anchoredPosition = new Vector2(placed.x, placed.y);
            ScreenRect = new Rect(placed.x * scale, placed.y * scale,
                                  placed.width * scale, placed.height * scale);
        }

        /// <summary>
        /// 접지 않았을 때 필요한 너비. 짧은 설명에까지 최대 너비를 주면 오른쪽이
        /// 텅 빈 판때기가 뜬다 — 실제로 그렇게 찍혔다. 긴 설명은 어차피
        /// <see cref="TooltipPlacement.ClampWidth"/>가 잘라 접는다.
        /// </summary>
        float DesiredWidth()
        {
            float need = 0f;
            foreach (var label in new[] { _title, _body, _detail, _meta })
            {
                if (label == null || !label.gameObject.activeSelf) continue;
                need = Mathf.Max(need, label.GetPreferredValues(label.text, 1e5f, 0f).x);
            }

            // 재는 값이 소수점 아래에서 반올림되면 마지막 글자가 다음 줄로 넘어간다.
            return need + Padding * 2f + 2f;
        }

        // ── 색과 치수 ────────────────────────────────────────────

        // 도감·제작 목록과 같은 계열이다. 쪽지만 혼자 다른 색이면 같은 게임으로 보이지 않는다.
        static readonly Color PanelFrame = new Color(0.10f, 0.12f, 0.15f, 0.97f);
        static readonly Color Backing = new Color(0.07f, 0.08f, 0.11f, 0.98f);
        static readonly Color TitleLabel = new Color(0.92f, 0.94f, 0.96f, 1f);
        static readonly Color BodyLabel = new Color(0.80f, 0.86f, 0.90f, 1f);
        static readonly Color DetailLabel = new Color(0.72f, 0.86f, 0.92f, 1f);
        static readonly Color MetaLabel = new Color(0.60f, 0.64f, 0.70f, 1f);

        const int Padding = 14;

        // ── 화면 조립 ────────────────────────────────────────────

        void Build()
        {
            _root = new GameObject("TooltipCanvas");
            _root.transform.SetParent(transform, false);

            _canvas = _root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // GraphicRaycaster를 달지 않는다. 쪽지는 눌리는 물건이 아니다.
            _canvasRect = (RectTransform)_root.transform;

            var panelGo = new GameObject("Tooltip", typeof(RectTransform));
            panelGo.transform.SetParent(_root.transform, false);
            _panel = (RectTransform)panelGo.transform;
            _panel.anchorMin = Vector2.zero;
            _panel.anchorMax = Vector2.zero;
            _panel.pivot = Vector2.zero;

            var frame = panelGo.AddComponent<Image>();
            UISkin.ApplyPanel(frame, PanelFrame);
            frame.raycastTarget = false;

            var layout = panelGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(Padding, Padding, Padding, Padding);
            layout.spacing = 6f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            // 세로만 내용에 맞춘다. 가로까지 맡기면 긴 설명문이 한 줄로 뻗어
            // 화면을 가로지른다 — 접히는 것이 읽힌다.
            var fitter = panelGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 판때기 그림(Semi_Round)은 속이 살짝 비친다. 화면 한가운데 판 위에
            // 겹쳐 뜨는 쪽지에서는 뒤의 글자가 배어 나와 둘 다 못 읽게 되므로,
            // 테두리만 남기고 안쪽을 불투명하게 메운다. 배치에서는 빼 둔다 —
            // 세로 배치에 끼면 글자 위가 아니라 글자 아래 칸 하나를 차지한다.
            var backing = new GameObject("Backing", typeof(RectTransform));
            backing.transform.SetParent(_panel, false);
            var brt = (RectTransform)backing.transform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(4f, 4f);
            brt.offsetMax = new Vector2(-4f, -4f);

            var fill = backing.AddComponent<Image>();
            fill.sprite = null;
            fill.color = Backing;
            fill.raycastTarget = false;
            backing.AddComponent<LayoutElement>().ignoreLayout = true;

            _title = NewText("Title", _panel, 23f, TitleLabel);
            _body = NewText("Body", _panel, 19f, BodyLabel);
            _detail = NewText("Detail", _panel, 18f, DetailLabel);
            _meta = NewText("Meta", _panel, 17f, MetaLabel);

            _root.SetActive(false);
        }

        static TMP_Text NewText(string name, Transform parent, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<TextMeshProUGUI>();
            var font = ResolveFont();
            if (font != null) text.font = font;

            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;

            // 자식에 ContentSizeFitter를 또 달지 않는다. 높이는 위의 세로 배치가
            // preferredHeight로 이미 잡아 준다 — 둘이 겹치면 레이아웃이 서로를 다시 부른다.
            return text;
        }

        static TMP_FontAsset _font;

        /// <summary>
        /// 이미 화면에 쓰이고 있는 글꼴을 빌린다. 본문이 전부 한글이라 TMP 기본
        /// 글꼴로 떨어지면 쪽지가 통째로 두부(네모)가 된다 — 도감과 같은 처방이다.
        /// </summary>
        static TMP_FontAsset ResolveFont()
        {
            if (_font != null) return _font;

            var any = FindAnyObjectByType<TextMeshProUGUI>(FindObjectsInactive.Include);
            _font = any != null && any.font != null ? any.font : TMP_Settings.defaultFontAsset;
            return _font;
        }
    }
}
