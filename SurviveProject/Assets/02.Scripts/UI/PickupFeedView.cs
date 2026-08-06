using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Survive.Core;
using Survive.Items;
using Survive.Localization;

namespace Survive.UI
{
    /// <summary>
    /// 방금 얻은 것을 화면 <b>왼쪽 아래</b>에 잠깐 띄우는 목록.
    ///
    /// 지금까지는 무언가를 주워도 화면에 아무 표시가 없어 소지품을 열어야 알았다.
    /// 러스트·발하임 계열이 모두 갖고 있는 그 목록이다.
    ///
    /// <b>어디서 듣는가.</b> <see cref="Inventory.ItemAdded"/> 하나다. 줍기·제작 완성·
    /// 해체 환급·보관함 인출·사망 가방 회수가 전부 <c>Inventory.TryAdd</c>를 지난다.
    /// 예외는 <see cref="PlayerInventory.RestoreState"/> 하나인데, 그쪽은 슬롯에 값을
    /// 직접 앉히므로 <b>불러오기 때 알림이 쏟아지지 않는다</b> — 다행한 예외다.
    /// 다만 그때 소지품 객체가 통째로 갈리므로 참조를 갈아 끼워야 한다
    /// (<see cref="RebindInventory"/>). 한 번 걸고 잊으면 불러오기 이후로 조용해진다.
    ///
    /// <b>패널 스택에 들어가지 않는다.</b> ESC로 닫는 물건이 아니라 스스로 사라지는
    /// 것이다. <see cref="UIStateService"/>에 등록하면 ESC 한 번을 삼켜 정작 닫으려던
    /// 소지품이 안 닫힌다 — <see cref="ItemTooltipView"/>와 같은 판단이다.
    ///
    /// <b>다른 창이 열려도 그대로 둔다.</b> 자리(왼쪽 아래)가 소지품·제작·보관함
    /// 판때기와 겹치지 않고, 무엇보다 보관함에서 물건을 꺼내는 그 순간이 이 목록이
    /// 가장 필요한 자리다. 창이 열렸다고 감추면 획득이 가장 많이 일어나는 화면에서만
    /// 목록이 사라진다.
    ///
    /// <b>씬을 고치지 않는다.</b> 스스로 선다 — <see cref="ItemTooltipView"/>·
    /// <see cref="CodexUI"/>와 같은 관례다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PickupFeedView : MonoBehaviour
    {
        /// <summary>씬 HUD(0) 위, 도감(3000) 아래. 쪽지와 밝기 조절 화면은 이것을 덮는다.</summary>
        public const int SortingOrder = 2500;

        static PickupFeedView _instance;
        public static PickupFeedView Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_instance != null) return;

            var go = new GameObject("PickupFeedView");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PickupFeedView>();
        }

        // ── 자리와 치수 ──────────────────────────────────────────
        //
        // 1920x1080 기준. 실측한 다른 요소와 겹치지 않는 자리를 골랐다.
        //   목표 목록   왼쪽 <b>위</b>  (24, -24)  460x40
        //   조작 안내   아래 가운데     x∈[580,1340]
        //   빠른 슬롯   아래 가운데     x∈[760,1160]
        //   배터리·스크랩  오른쪽
        //   소지품/제작/보관함 판때기   가운데, 왼쪽 끝이 x=384
        // 왼쪽 아래 x∈[24,364]·y∈[150,364]는 아무것도 없다.

        const float LeftMargin = 24f;
        const float BottomMargin = 150f;
        const float Width = 340f;
        const float RowHeight = 38f;
        const float RowSpacing = 6f;
        const float IconSize = 30f;
        const float FadeSeconds = 0.3f;

        // 색은 쪽지·도감과 같은 계열이다. 목록만 혼자 다른 색이면 같은 게임으로 보이지 않는다.
        static readonly Color RowFrame = new Color(0.10f, 0.12f, 0.15f, 0.86f);
        static readonly Color IconBox = new Color(0.07f, 0.08f, 0.11f, 0.98f);
        static readonly Color LabelColor = new Color(0.92f, 0.94f, 0.96f, 1f);
        static readonly Color LetterColor = new Color(0.60f, 0.64f, 0.70f, 1f);

        // ── 상태 ─────────────────────────────────────────────────

        readonly PickupFeed _feed = new PickupFeed();

        /// <summary>규칙 쪽. 검증 하네스가 본다.</summary>
        public PickupFeed Feed => _feed;

        GameObject _root;
        RectTransform _list;

        PlayerInventory _player;
        Inventory _bound;

        int _shownVersion = -1;

        sealed class RowView
        {
            public PickupFeedRow Model;
            public GameObject Go;
            public CanvasGroup Group;
            public Image Icon;
            public TMP_Text Letter;
            public TMP_Text Label;

            /// <summary>사라지는 중. 목록에는 없지만 화면에서는 아직 옅어지고 있다.</summary>
            public bool Leaving;
        }

        readonly List<RowView> _views = new List<RowView>();

        /// <summary>지금 화면에 서 있는 줄들의 글자. 검증 하네스가 읽는다.</summary>
        public IReadOnlyList<string> VisibleLines
        {
            get
            {
                var lines = new List<string>();
                foreach (var v in _views)
                    if (!v.Leaving && v.Label != null) lines.Add(v.Label.text);
                return lines;
            }
        }

        /// <summary>지금 화면에 서 있는 줄 오브젝트. 검증 하네스가 실제 글자를 읽는다.</summary>
        public IReadOnlyList<TMP_Text> VisibleLabels
        {
            get
            {
                var labels = new List<TMP_Text>();
                foreach (var v in _views)
                    if (!v.Leaving && v.Label != null) labels.Add(v.Label);
                return labels;
            }
        }

        // ── 삶 ───────────────────────────────────────────────────

        void OnEnable()
        {
            _instance = this;

            // 한 번 쓰고 마는 글자다. 로케일이 바뀌어도 아무도 다시 쓰지 않으므로
            // 직접 듣는다 — 창을 닫았다 여는 물건이 아니라 다시 그릴 계기가 없다.
            Loc.LocaleChanged += Redraw;
        }

        void OnDisable()
        {
            Loc.LocaleChanged -= Redraw;
            Unbind();
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// 옅어지는 중인 줄은 트윈이 끝나며 자기를 지운다. 그 전에 화면이 통째로
        /// 사라지면 이미 없는 오브젝트를 향해 콜백이 날아간다.
        /// </summary>
        void OnDestroy()
        {
            foreach (var view in _views)
                view.Group?.DOKill();
            _views.Clear();
        }

        void Update()
        {
            RebindInventory();

            // 정지 화면에서도 시간은 흘러야 한다. 소지품을 열어 둔 채로 목록이
            // 영원히 남아 있으면 그것은 알림이 아니라 낙서다.
            _feed.Tick(Time.unscaledTime);

            if (_feed.Version != _shownVersion) Redraw();
        }

        /// <summary>
        /// 소지품 객체는 저장 복원 때 통째로 갈린다. 참조가 바뀌었는지만 보고
        /// 갈아 끼운다 — <c>UnlockService</c>가 같은 처방을 쓴다.
        /// </summary>
        void RebindInventory()
        {
            if (_player == null) GameServices.TryGet<PlayerInventory>(out _player);

            var inv = _player != null ? _player.Inventory : null;
            if (ReferenceEquals(inv, _bound)) return;

            Unbind();
            _bound = inv;
            if (_bound == null) return;

            _bound.ItemAdded += OnItemAdded;

            // 소지품이 갈렸다는 것은 방금 저장본을 불러왔다는 뜻이다. 그 전에 쌓인
            // 줄은 지난 판의 것이므로 데리고 가지 않는다.
            _feed.Clear();
        }

        void Unbind()
        {
            if (_bound == null) return;
            _bound.ItemAdded -= OnItemAdded;
            _bound = null;
        }

        void OnItemAdded(ItemDataSO item, int count) =>
            _feed.Add(item, count, Time.unscaledTime);

        // ── 그리기 ───────────────────────────────────────────────

        void Redraw()
        {
            if (_root == null) Build();

            _shownVersion = _feed.Version;
            var rows = _feed.Rows;

            // ① 목록에서 빠진 줄은 옅어지며 물러난다.
            for (int i = _views.Count - 1; i >= 0; i--)
            {
                var view = _views[i];
                if (view.Leaving || Contains(rows, view.Model)) continue;

                view.Leaving = true;
                view.Model = null;

                var group = view.Group;
                var go = view.Go;
                var captured = view;
                group.DOKill();
                group.DOFade(0f, FadeSeconds).SetUpdate(true).OnComplete(() =>
                {
                    _views.Remove(captured);
                    if (go != null) Destroy(go);
                });
            }

            // ② 목록에 있는 줄을 맞춘다. 없으면 새로 짓고 옅은 데서 떠오른다.
            foreach (var row in rows)
            {
                var view = Find(row);
                if (view == null)
                {
                    view = NewRow();
                    view.Model = row;
                    _views.Add(view);

                    view.Group.alpha = 0f;
                    view.Group.DOKill();
                    view.Group.DOFade(1f, FadeSeconds).SetUpdate(true);
                }

                Apply(view, row);
            }

            // ③ 물러나는 줄을 맨 위에 두고, 그 아래로 목록 순서를 세운다.
            //    만료는 언제나 가장 오래된 줄부터라 물러나는 줄은 늘 맨 위다.
            int sibling = 0;
            foreach (var view in _views)
                if (view.Leaving) view.Go.transform.SetSiblingIndex(sibling++);

            foreach (var row in rows)
            {
                var view = Find(row);
                if (view != null) view.Go.transform.SetSiblingIndex(sibling++);
            }
        }

        static bool Contains(IReadOnlyList<PickupFeedRow> rows, PickupFeedRow row)
        {
            for (int i = 0; i < rows.Count; i++)
                if (ReferenceEquals(rows[i], row)) return true;
            return false;
        }

        RowView Find(PickupFeedRow row)
        {
            foreach (var view in _views)
                if (!view.Leaving && ReferenceEquals(view.Model, row)) return view;
            return null;
        }

        void Apply(RowView view, PickupFeedRow row)
        {
            view.Label.text = PickupFeedText.Line(row);

            var icon = row.Item != null ? row.Item.icon : null;
            view.Icon.sprite = icon;
            view.Icon.enabled = icon != null;

            // 아이콘이 없는 아이템이 있다. 칸을 비우면 그 줄만 글자가 왼쪽으로
            // 밀리므로 이름 첫 글자로 자리를 지킨다.
            string letter = icon != null ? "" : PickupFeedText.IconLetter(row.Item);
            view.Letter.text = letter;
            view.Letter.gameObject.SetActive(letter.Length > 0);
        }

        // ── 화면 조립 ────────────────────────────────────────────

        void Build()
        {
            _root = new GameObject("PickupFeedCanvas");
            _root.transform.SetParent(transform, false);

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // GraphicRaycaster를 달지 않는다. 눌리는 물건이 아니고, 달면 왼쪽 아래
            // 자리에서 오는 클릭을 이 목록이 삼킨다.

            var listGo = new GameObject("Rows", typeof(RectTransform));
            listGo.transform.SetParent(_root.transform, false);
            _list = (RectTransform)listGo.transform;

            // 왼쪽 <b>아래</b>에 붙이고 위로 자란다. 그래서 가장 새 줄이 언제나
            // 같은 자리(맨 아래)에 선다 — 눈이 그 자리를 외운다. 반대로 위에 붙여
            // 아래로 늘리면 새 줄이 뜰 때마다 그 자리가 옮겨 다닌다.
            _list.anchorMin = Vector2.zero;
            _list.anchorMax = Vector2.zero;
            _list.pivot = Vector2.zero;
            _list.anchoredPosition = new Vector2(LeftMargin, BottomMargin);
            _list.sizeDelta = new Vector2(Width, 0f);

            var layout = listGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = RowSpacing;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childAlignment = TextAnchor.LowerLeft;

            // 세로만 내용에 맞춘다. 줄이 하나도 없으면 높이가 0이 되어 아무것도 안 보인다.
            var fitter = listGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        RowView NewRow()
        {
            var go = new GameObject("Row", typeof(RectTransform));
            go.transform.SetParent(_list, false);

            var group = go.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var frame = go.AddComponent<Image>();
            UISkin.ApplyPanel(frame, RowFrame);
            frame.raycastTarget = false;

            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = RowHeight;
            element.minHeight = RowHeight;

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(4, 10, 4, 4);
            layout.spacing = 8f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;

            // 아이콘 칸. 그림이 있든 없든 자리는 늘 같은 너비다.
            var boxGo = new GameObject("IconBox", typeof(RectTransform));
            boxGo.transform.SetParent(go.transform, false);

            var box = boxGo.AddComponent<Image>();
            box.sprite = null;
            box.color = IconBox;
            box.raycastTarget = false;

            var boxElement = boxGo.AddComponent<LayoutElement>();
            boxElement.preferredWidth = IconSize;
            boxElement.minWidth = IconSize;
            boxElement.preferredHeight = IconSize;

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(boxGo.transform, false);
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(2f, 2f);
            iconRect.offsetMax = new Vector2(-2f, -2f);

            var icon = iconGo.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var letter = NewText("Letter", boxGo.transform, 17f, LetterColor);
            var letterRect = (RectTransform)letter.transform;
            letterRect.anchorMin = Vector2.zero;
            letterRect.anchorMax = Vector2.one;
            letterRect.offsetMin = Vector2.zero;
            letterRect.offsetMax = Vector2.zero;
            letter.alignment = TextAlignmentOptions.Center;

            var label = NewText("Label", go.transform, 19f, LabelColor);
            label.alignment = TextAlignmentOptions.Left;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;

            return new RowView
            {
                Go = go,
                Group = group,
                Icon = icon,
                Letter = letter,
                Label = label,
            };
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
            text.raycastTarget = false;
            return text;
        }

        static TMP_FontAsset _font;

        /// <summary>
        /// 이미 화면에 쓰이고 있는 글꼴을 빌린다. TMP 기본 글꼴로 떨어지면 한글이
        /// 통째로 두부(네모)가 된다 — 쪽지·도감과 같은 처방이다.
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
