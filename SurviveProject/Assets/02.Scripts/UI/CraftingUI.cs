using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;
using MoreMountains.Feedbacks;
using Survive.Core;
using Survive.Crafting;
using Survive.Input;
using Survive.Items;
using Survive.Localization;
using Survive.Progression;

namespace Survive.UI
{
    /// <summary>
    /// 레시피 목록과 제작 버튼. 행은 런타임에 만든다 —
    /// 기존 씬에 제작 UI 레이아웃이 없어서 재활용할 것이 없다.
    ///
    /// 제작에 시간이 걸리게 되면서 이 화면은 "만드는 곳"이 아니라
    /// <b>거는 곳</b>이 되었다. 누르면 물건이 나오는 것이 아니라 줄에 선다.
    /// 그래서 수량을 고르는 자리(− x N + 최대)와 걸린 줄을 보는 자리가 생겼다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CraftingUI : MonoBehaviour, IClosablePanel
    {
        [SerializeField] InputReaderSO input;
        [SerializeField] RecipeBookSO book;
        [SerializeField] RectTransform panel;
        [SerializeField] CanvasGroup group;
        [SerializeField] Transform rowParent;
        [Tooltip("비우면 TMP 기본 폰트를 쓴다")]
        [SerializeField] TMP_FontAsset font;
        [SerializeField] MMF_Player craftFeedback;
        [SerializeField] float tweenSeconds = 0.18f;

        /// <summary>
        /// 수량 고르는 칸이 차지하는 오른쪽 폭. 오른쪽 끝에서부터 안쪽으로
        /// [− ][x N][ +][최대]를 8px 여백 안에 세운다.
        /// </summary>
        const float QuantityBlockWidth = 188f;

        const float RowWidth = 520f;
        const float RowHeight = 52f;

        class Row
        {
            public RecipeSO recipe;
            public Button button;      // 행 전체 = 걸기
            public Image frame;
            public TMP_Text label;
            public Button minus;
            public Button plus;
            public Button max;
            public TMP_Text amount;

            /// <summary>"최대" 버튼의 글자. 한 번 쓰고 마는 자리라 로케일이 바뀌면 여기만 다시 쓴다.</summary>
            public TMP_Text maxCaption;
        }

        readonly List<Row> _rows = new List<Row>();

        /// <summary>
        /// 연구대의 줄. 제작 행과 나란히 만들어 두고 서 있는 자리에 따라 한쪽만 켠다 —
        /// 화면을 하나 더 만들면 같은 목록 코드가 두 벌이 되고, 앞으로 잠금 표시를
        /// 고칠 때마다 두 군데를 고쳐야 한다.
        /// </summary>
        class ResearchRow
        {
            public ResearchEntrySO entry;
            public Button button;
            public Image frame;
            public TMP_Text label;
        }

        readonly List<ResearchRow> _researchRows = new List<ResearchRow>();

        /// <summary>연구대에 걸린 것이 몇이고 얼마나 남았는지 한 줄로 적는 자리.</summary>
        Button _researchHeader;
        TMP_Text _researchHeaderLabel;

        /// <summary>레시피마다 마지막으로 고른 수량. 창을 닫아도 기억한다.</summary>
        readonly Dictionary<string, int> _wanted = new Dictionary<string, int>();

        PlayerInventory _inventory;
        Survive.Player.PlayerContext _player;
        StationType _station = StationType.None;
        ICraftStation _stationHost;
        IResearchStation _researchHost;
        CraftQueueView _queueView;

        /// <summary>연료 넣기처럼 스테이션마다 붙는 한 줄. 없는 스테이션에서는 꺼진다.</summary>
        Button _sideButton;
        TMP_Text _sideLabel;

        /// <summary>
        /// 실린 줄이 하나도 없을 때 대신 서는 한 줄. 판때기가 여백만 남은 띠로
        /// 찌그러지지 않게 하는 것이 첫째 이유이고, 창이 고장 난 것이 아니라는
        /// 것을 말해 주는 것이 둘째다.
        /// </summary>
        GameObject _emptyRow;
        TMP_Text _emptyLabel;

        /// <summary>지금 열려 있는 목록이 어느 작업대 기준인지. 소지품 UI가 구분에 쓴다.</summary>
        public StationType CurrentStation => _station;

        /// <summary>제작대·화톳불에서 열었으면 그 물건. 손 제작이면 null.</summary>
        public ICraftStation CurrentStationHost => _stationHost;

        /// <summary>연구대에서 열었으면 그 물건. 아니면 null.</summary>
        public IResearchStation CurrentResearchHost => _researchHost;

        bool _isOpen;

        public bool IsOpen => _isOpen;

        /// <summary>
        /// 같은 컴포넌트지만 규칙에서는 둘로 나뉜다. 손 제작 목록은 소지품에
        /// 딸려 다니고 보관함에 밀려 닫히지만, 제작대에서 연 목록은 그렇지 않다.
        /// </summary>
        public UIPanelKind PanelKind => _station == StationType.None
            ? UIPanelKind.HandCrafting
            : UIPanelKind.StationCrafting;

        /// <summary>지금 이 화면이 다루는 줄. 손 제작이면 몸에 달린 줄이다.</summary>
        public CraftQueue ActiveQueue => _stationHost != null
            ? _stationHost.Work.Queue
            : HandCraftingService.Instance?.Queue;

        void Awake()
        {
            // 여기서 gameObject.SetActive(false)를 하면 안 된다.
            // 이 스크립트가 붙은 오브젝트를 끄면, Open()의 SetActive(true)가
            // 이 Awake를 처음 깨우고 곧바로 다시 닫아버린다.
            // 보이고 안 보이고는 CanvasGroup으로만 다룬다.
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
            _isOpen = false;
        }

        void OnEnable()
        {
            // ESC 처리는 UIStateService가 전담한다. 여기서 따로 듣지 않는다 —
            // 패널마다 각자 들으면 닫히는 것과 안 닫히는 것이 생긴다.
            if (GameServices.TryGet<UIStateService>(out var ui)) ui.RegisterPanel(this);

            // 목록 줄은 매 프레임 다시 그려지므로 로케일을 따라온다. 그러나 "최대"처럼
            // 만들 때 한 번 쓰고 마는 글자는 아무도 다시 쓰지 않는다 — 그 자리만 듣는다.
            Loc.LocaleChanged += ApplyStaticText;
            StartCoroutine(BindWhenReady());
        }

        void OnDisable()
        {
            Loc.LocaleChanged -= ApplyStaticText;
            if (GameServices.TryGet<UIStateService>(out var ui)) ui.UnregisterPanel(this);
        }

        /// <summary>매 프레임 다시 쓰지 않는 글자들. 만들 때와 로케일이 바뀔 때만 부른다.</summary>
        void ApplyStaticText()
        {
            foreach (var row in _rows)
                if (row.maxCaption != null) row.maxCaption.text = Loc.T("UI", "craft_max_button");
        }

        IEnumerator BindWhenReady()
        {
            yield return null;
            GameServices.TryGet<PlayerInventory>(out _inventory);
            _player = UnityEngine.Object.FindAnyObjectByType<Survive.Player.PlayerContext>(FindObjectsInactive.Exclude);
            EnsureQueueView();
            BuildRows();
        }

        void EnsureQueueView()
        {
            if (_queueView != null) return;
            _queueView = CraftQueueView.Ensure(transform.parent, font);
            BindQueueView();
        }

        void BindQueueView()
        {
            if (_queueView == null) return;
            _queueView.Bind(ActiveQueue, CancelQueued);
            _queueView.SetExpanded(_isOpen);
        }

        void Update()
        {
            if (!_isOpen) return;

            // 줄이 흐르는 동안 만들 수 있는 개수와 남은 시간이 계속 바뀐다.
            // 목록은 가볍고(행 수가 한 자리), 매 프레임 다시 그려도 값이 없다.
            RefreshList();
        }

        // ── 목록 ────────────────────────────────────────────────

        void BuildRows()
        {
            if (rowParent == null || book == null) return;
            foreach (var row in _rows) if (row.button != null) Destroy(row.button.gameObject);
            _rows.Clear();

            foreach (var r in book.recipes)
            {
                if (r == null) continue;
                _rows.Add(BuildRow(r));
            }

            BuildSideRow();
            BuildResearchHeader();
            BuildEmptyRow();
            RefreshList();
        }

        /// <summary>
        /// 연구 항목 줄을 만든다. 연구대를 처음 열 때 한 번, 그 자리의 목록으로.
        /// 연구대마다 다른 목록을 물릴 이유는 없지만 그렇게 될 수도 있으므로
        /// 들고 있는 책이 바뀌면 다시 만든다.
        /// </summary>
        void BuildResearchRows(ResearchBookSO book)
        {
            foreach (var row in _researchRows)
                if (row.button != null) Destroy(row.button.gameObject);
            _researchRows.Clear();
            _researchBook = book;

            if (rowParent == null || book?.entries == null) return;

            foreach (var e in book.entries)
            {
                if (e == null) continue;
                _researchRows.Add(BuildResearchRow(e));
            }
        }

        ResearchBookSO _researchBook;

        ResearchRow BuildResearchRow(ResearchEntrySO e)
        {
            var go = new GameObject("Row_" + e.id, typeof(RectTransform));
            go.transform.SetParent(rowParent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(RowWidth, RowHeight);

            var img = go.AddComponent<Image>();
            UISkin.ApplyPanel(img, NormalFrame);

            var btn = go.AddComponent<Button>();

            var txt = MakeLabel(go.transform, "Label", 12f, -12f);
            txt.alignment = TextAlignmentOptions.Left;
            txt.enableAutoSizing = true;
            txt.fontSizeMin = 12f;
            txt.fontSizeMax = 19f;

            var captured = e;
            btn.onClick.AddListener(() => ToggleResearch(captured));

            go.SetActive(false);
            return new ResearchRow { entry = e, button = btn, frame = img, label = txt };
        }

        /// <summary>연구대 목록 맨 위의 대기열 한 줄. 누르는 자리가 아니다.</summary>
        void BuildResearchHeader()
        {
            if (_researchHeader != null) return;

            var go = new GameObject("Row_ResearchQueue", typeof(RectTransform));
            go.transform.SetParent(rowParent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(RowWidth, RowHeight);

            var img = go.AddComponent<Image>();
            UISkin.ApplyPanel(img, new Color(0.10f, 0.16f, 0.20f, 0.92f));

            _researchHeader = go.AddComponent<Button>();
            _researchHeader.interactable = false;

            _researchHeaderLabel = MakeLabel(go.transform, "Label", 12f, -12f);
            _researchHeaderLabel.alignment = TextAlignmentOptions.Center;

            go.SetActive(false);
        }

        Row BuildRow(RecipeSO r)
        {
            var go = new GameObject("Row_" + r.id, typeof(RectTransform));
            go.transform.SetParent(rowParent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(RowWidth, RowHeight);

            var img = go.AddComponent<Image>();
            UISkin.ApplyPanel(img, new Color(0.12f, 0.14f, 0.18f, 0.9f));

            var btn = go.AddComponent<Button>();

            var txt = MakeLabel(go.transform, "Label", 12f, -(QuantityBlockWidth + 10f));
            txt.alignment = TextAlignmentOptions.Left;
            // 재료가 많은 레시피는 한 줄을 넘긴다. 줄이 늘면 행이 밀리므로 줄인다.
            txt.enableAutoSizing = true;
            txt.fontSizeMin = 12f;
            txt.fontSizeMax = 19f;

            // ── 수량 칸: [−][x N][+][최대] ──
            // 자리는 오른쪽 끝에서부터 잰다. 왼쪽에서 재면 행 폭을 바꿀 때마다
            // 네 군데를 같이 고쳐야 하고, 한 군데를 빠뜨리면 판때기 밖으로 흘러나간다.
            // 빼기 기호는 ASCII 하이픈을 쓴다 — U+2212는 이 프로젝트 폰트 아틀라스에
            // 없어서 네모(□)로 찍힌다.
            var minus  = MakeMiniButton(go.transform, "Minus", "-",   188f, 34f);
            var amount = MakeMiniLabel(go.transform, "Amount",        150f, 44f);
            var plus   = MakeMiniButton(go.transform, "Plus",  "+",   102f, 34f);
            var max    = MakeMiniButton(go.transform, "Max",
                                        Loc.T("UI", "craft_max_button"), 60f, 52f);

            var row = new Row
            {
                recipe = r, button = btn, frame = img, label = txt,
                minus = minus, plus = plus, max = max, amount = amount,
                maxCaption = max.GetComponentInChildren<TMP_Text>(true)
            };

            var captured = r;

            // 줄에는 이름과 재료 수치만 적힌다. 만들어 봐야 무엇에 쓰는 물건인지
            // 모르는 채로 목록을 훑게 되므로, 커서를 올리면 결과물의 설명문을 읽어 준다.
            // 모르는 레시피를 걱정할 필요는 없다 — 그런 줄은 켜지지도 않는다.
            ItemTooltipTrigger.Attach(go).Bind(
                () => captured.result?.item,
                () => MenuListing.IngredientLine(captured));

            btn.onClick.AddListener(() => Enqueue(captured));
            minus.onClick.AddListener(() => Nudge(captured, -1));
            plus.onClick.AddListener(() => Nudge(captured, +1));
            max.onClick.AddListener(() => SetWanted(captured, MaxFor(captured)));

            return row;
        }

        /// <summary>화톳불의 "연료 넣기"처럼 스테이션이 들고 오는 한 줄.</summary>
        void BuildSideRow()
        {
            var go = new GameObject("Row_StationAction", typeof(RectTransform));
            go.transform.SetParent(rowParent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(RowWidth, RowHeight);

            var img = go.AddComponent<Image>();
            UISkin.ApplyPanel(img, new Color(0.20f, 0.14f, 0.10f, 0.92f));

            _sideButton = go.AddComponent<Button>();
            _sideLabel = MakeLabel(go.transform, "Label", 12f, -12f);
            _sideLabel.alignment = TextAlignmentOptions.Center;

            _sideButton.onClick.AddListener(RunSideAction);
            go.SetActive(false);
        }

        /// <summary>
        /// "아직 아는 제작법이 없다" 한 줄. 누르는 자리가 아니라 Button을 달지 않는다 —
        /// 이름이 Row_로 시작하지 않는 것도 일부러다. 검증 하네스가 목록에 실린 줄을
        /// 셀 때 이 자리가 한 개로 세어지면 "비어 있다"를 증명할 수 없다.
        /// </summary>
        void BuildEmptyRow()
        {
            if (_emptyRow != null) return;

            var go = new GameObject("EmptyNotice", typeof(RectTransform));
            go.transform.SetParent(rowParent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(RowWidth, RowHeight);

            var img = go.AddComponent<Image>();
            UISkin.ApplyPanel(img, new Color(0.10f, 0.11f, 0.14f, 0.85f));
            img.raycastTarget = false;

            _emptyLabel = MakeLabel(go.transform, "Label", 12f, -12f);
            _emptyLabel.alignment = TextAlignmentOptions.Center;
            _emptyLabel.color = new Color(0.62f, 0.64f, 0.70f, 1f);

            _emptyRow = go;
            go.SetActive(false);
        }

        TMP_Text MakeLabel(Transform parent, string name, float left, float right)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, 0f);
            rt.offsetMax = new Vector2(right, 0f);

            var txt = go.AddComponent<TextMeshProUGUI>();
            if (font != null) txt.font = font;
            txt.fontSize = 20f;
            txt.color = Color.white;
            txt.raycastTarget = false;
            return txt;
        }

        /// <summary>
        /// 행 오른쪽에 붙는 칸 하나를 만든다.
        /// <paramref name="fromRight"/>는 행 오른쪽 끝에서 이 칸의 <b>왼쪽 변</b>까지의 거리다.
        /// </summary>
        RectTransform MakeMiniSlot(Transform parent, string name, float fromRight, float width)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(width, 30f);
            rt.anchoredPosition = new Vector2(-fromRight, 0f);
            return rt;
        }

        Button MakeMiniButton(Transform parent, string name, string caption,
                              float fromRight, float width)
        {
            var rt = MakeMiniSlot(parent, name, fromRight, width);
            var go = rt.gameObject;

            var img = go.AddComponent<Image>();
            UISkin.ApplyPanel(img, new Color(0.20f, 0.23f, 0.30f, 0.95f));

            var btn = go.AddComponent<Button>();
            MakeFilledText(go.transform, "Label", 17f).text = caption;
            return btn;
        }

        TMP_Text MakeMiniLabel(Transform parent, string name, float fromRight, float width)
        {
            var rt = MakeMiniSlot(parent, name, fromRight, width);
            return MakeFilledText(rt, name + "Text", 17f);
        }

        TMP_Text MakeFilledText(Transform parent, string name, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var lrt = (RectTransform)go.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            var txt = go.AddComponent<TextMeshProUGUI>();
            if (font != null) txt.font = font;
            txt.fontSize = size;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = Color.white;
            txt.raycastTarget = false;
            return txt;
        }

        /// <summary>
        /// 이미 아는 연구 항목의 판때기·글자색. 실루엣만 남기고 가라앉힌다.
        /// 제작 줄에는 더 이상 쓰이지 않는다 — 모르는 제작법은 가라앉히는 것이
        /// 아니라 아예 실리지 않는다.
        /// </summary>
        static readonly Color LockedFrame = new Color(0.09f, 0.10f, 0.13f, 0.75f);
        static readonly Color LockedLabel = new Color(0.46f, 0.48f, 0.56f, 1f);
        static readonly Color NormalFrame = new Color(0.12f, 0.14f, 0.18f, 0.9f);

        void RefreshList()
        {
            var inv = _inventory?.Inventory;
            var ledger = BlueprintGate.Active;
            int shown = 0;

            // 연구대 앞에서는 제작 줄을 전부 접는다. 같은 판때기를 쓰되 목록은
            // 하나만 뜬다 — 만드는 자리와 알아내는 자리는 다른 자리다.
            bool researching = _researchHost != null;

            foreach (var row in _rows)
            {
                // 실리는 조건은 두 가지다. 이 자리에서 만드는 것이어야 하고,
                // <b>만들 줄 알아야</b> 한다. 모르는 것은 회색으로 남기지 않고
                // 줄 자체를 끈다 — 남겨 두면 이름도, 여는 방법도, 그 뒤에 무엇이
                // 기다리는지도 통째로 새어 나간다(MenuListing 참조).
                bool visible = !researching &&
                               MenuListing.ShouldList(row.recipe, _station, ledger);

                if (row.button != null && row.button.gameObject.activeSelf != visible)
                    row.button.gameObject.SetActive(visible);
                if (!visible) continue;
                shown++;

                int max = MaxFor(row.recipe);
                int want = Mathf.Clamp(Wanted(row.recipe), 1, Mathf.Max(1, max));
                _wanted[row.recipe.id] = want;

                var queue = ActiveQueue;
                bool queueRoom = queue != null && !queue.IsFull;
                bool canMake = max >= want && queueRoom;

                if (row.button != null) row.button.interactable = canMake;
                if (row.frame != null) row.frame.color = NormalFrame;
                if (row.minus != null) row.minus.interactable = want > 1;
                if (row.plus != null) row.plus.interactable = want < max;
                if (row.max != null) row.max.interactable = max >= 1 && want != max;
                if (row.amount != null)
                {
                    row.amount.text = "x" + want;
                    row.amount.color = canMake ? Color.white : new Color(0.6f, 0.6f, 0.66f);
                }

                if (row.label != null)
                {
                    row.label.text = MenuListing.RecipeLine(row.recipe, inv, want, queueRoom);
                    row.label.color = canMake ? Color.white : new Color(0.65f, 0.65f, 0.7f, 1f);
                }
            }

            shown += RefreshSideRow() ? 1 : 0;
            shown += RefreshResearchRows(inv, ledger);
            RefreshEmptyRow(shown);
            FitPanel(MenuListing.PanelRows(shown));
        }

        /// <summary>
        /// 목록이 통째로 비었을 때만 안내 한 줄을 세운다.
        /// 몇 개가 잠겼는지는 적지 않는다 — 그 숫자도 앞으로 무엇이 있는지를 말한다.
        /// </summary>
        void RefreshEmptyRow(int shown)
        {
            if (_emptyRow == null) return;

            bool empty = shown <= 0;
            if (_emptyRow.activeSelf != empty) _emptyRow.SetActive(empty);
            if (empty && _emptyLabel != null) _emptyLabel.text = MenuListing.NothingKnownToCraft;
        }

        // ── 연구 목록 ────────────────────────────────────────────

        /// <summary>연구대 줄들을 그린다. 연구대가 아니면 전부 접고 0을 돌려준다.</summary>
        int RefreshResearchRows(Inventory inv, UnlockLedger ledger)
        {
            bool researching = _researchHost != null;

            if (_researchHeader != null && _researchHeader.gameObject.activeSelf != researching)
                _researchHeader.gameObject.SetActive(researching);

            if (!researching)
            {
                foreach (var row in _researchRows)
                    if (row.button != null && row.button.gameObject.activeSelf)
                        row.button.gameObject.SetActive(false);
                return 0;
            }

            var queue = _researchHost.Work;
            var energy = _researchHost.EnergyItem;

            if (_researchHeaderLabel != null)
            {
                float left = ResearchService.TotalSecondsLeft(queue);
                _researchHeaderLabel.text = queue == null || queue.IsEmpty
                    ? $"{_researchHost.StationName}  ·  분석 대기열 비어 있음"
                    : $"{_researchHost.StationName}  ·  분석 대기열 {queue.Count}/{queue.Capacity}" +
                      $"  ·  남은 {CraftTimeText.Short(left)}";
                _researchHeaderLabel.color = new Color(0.72f, 0.86f, 0.92f, 1f);
            }

            int shown = 1;
            foreach (var row in _researchRows)
            {
                if (row.button != null && !row.button.gameObject.activeSelf)
                    row.button.gameObject.SetActive(true);
                shown++;

                var state = ResearchService.Evaluate(row.entry, inv, ledger, queue, energy);
                int at = queue != null ? queue.IndexOf(row.entry) : -1;

                bool clickable = state == ResearchReadiness.Ready || at >= 0;

                if (row.button != null) row.button.interactable = clickable;
                if (row.frame != null)
                    row.frame.color = state == ResearchReadiness.AlreadyKnown ? LockedFrame : NormalFrame;

                if (row.label == null) continue;

                row.label.text = at >= 0
                    ? DescribeQueued(row.entry, queue, at)
                    : DescribeResearch(row.entry, inv, energy, state);
                row.label.color = at >= 0 ? new Color(0.72f, 0.86f, 0.92f, 1f)
                                : state == ResearchReadiness.Ready ? Color.white
                                : state == ResearchReadiness.AlreadyKnown ? LockedLabel
                                                                          : new Color(0.65f, 0.65f, 0.7f, 1f);
            }
            return shown;
        }

        /// <summary>걸리지 않은 항목 — 무엇이 얼마나 드는가.</summary>
        string DescribeResearch(ResearchEntrySO e, Inventory inv, ItemDataSO energy,
                                ResearchReadiness state)
        {
            var sb = new StringBuilder();
            sb.Append(string.IsNullOrEmpty(e.displayName) ? e.id : e.displayName);

            if (state == ResearchReadiness.AlreadyKnown)
            {
                sb.Append("  ·  ").Append(ResearchService.Describe(state));
                return sb.ToString();
            }

            sb.Append("  ·  ");
            if (e.materials != null)
            {
                foreach (var need in e.materials)
                {
                    if (need?.item == null || need.count <= 0) continue;
                    int held = inv != null ? inv.CountOf(need.item.id) : 0;
                    sb.Append($"{need.item.displayName} {held}/{need.count}, ");
                }
            }

            string energyName = energy != null ? energy.displayName : "스크랩";
            int heldEnergy = inv != null ? inv.CountOf(ResearchService.EnergyIdOf(energy)) : 0;
            sb.Append($"{energyName} {heldEnergy}/{e.energyCost}");

            sb.Append($"  ·  {CraftTimeText.Short(e.researchSeconds)}");
            if (state != ResearchReadiness.Ready)
                sb.Append($"  ({ResearchService.Describe(state)})");
            return sb.ToString();
        }

        /// <summary>줄에 서 있는 항목 — 몇 번째인지와 얼마나 남았는지.</summary>
        static string DescribeQueued(ResearchEntrySO e, ResearchQueue queue, int index)
        {
            var job = queue.At(index);
            string name = string.IsNullOrEmpty(e.displayName) ? e.id : e.displayName;

            if (index == 0)
                return $"{name}  ·  분석 중 {job.Progress:P0}  ·  남은 " +
                       $"{CraftTimeText.Short(job.SecondsLeft)}  (누르면 물린다)";

            return $"{name}  ·  대기 {index + 1}번째  ·  " +
                   $"{CraftTimeText.Short(job.SecondsLeft)}  (누르면 물린다)";
        }

        /// <summary>
        /// 연구 줄을 누른다. 안 걸린 것은 걸고, 걸린 것은 물린다 —
        /// 대기열 띠를 눌러 물리는 제작 쪽과 같은 몸짓이다.
        /// </summary>
        void ToggleResearch(ResearchEntrySO e)
        {
            var inv = _inventory?.Inventory;
            if (inv == null || e == null || _researchHost == null) return;

            var queue = _researchHost.Work;
            var energy = _researchHost.EnergyItem;

            int at = queue != null ? queue.IndexOf(e) : -1;
            bool ok = at >= 0
                ? ResearchService.TryCancel(queue, at, inv, energy)
                : ResearchService.TryEnqueue(queue, e, inv, BlueprintGate.Active, energy);

            if (ok) craftFeedback?.PlayFeedbacks();
            RefreshList();
        }

        /// <summary>
        /// 연구를 거는 창구. 검증 하네스가 UI 클릭 없이 부를 수 있게 열어 둔다.
        /// </summary>
        public bool RequestResearch(ResearchEntrySO entry)
        {
            if (_researchHost == null) return false;

            int before = _researchHost.Work?.Count ?? 0;
            ToggleResearch(entry);
            return (_researchHost.Work?.Count ?? 0) > before;
        }

        bool RefreshSideRow()
        {
            var action = _stationHost?.SideAction;
            bool visible = action != null;

            if (_sideButton != null && _sideButton.gameObject.activeSelf != visible)
                _sideButton.gameObject.SetActive(visible);
            if (!visible) return false;

            bool can = action.CanRun == null || action.CanRun();
            if (_sideButton != null) _sideButton.interactable = can;
            if (_sideLabel != null)
            {
                _sideLabel.text = action.Label != null ? action.Label() : "";
                _sideLabel.color = can ? Color.white : new Color(0.65f, 0.65f, 0.7f);
            }
            return true;
        }

        void RunSideAction()
        {
            var action = _stationHost?.SideAction;
            if (action == null) return;
            if (action.CanRun != null && !action.CanRun()) return;

            action.Run?.Invoke();
            craftFeedback?.PlayFeedbacks();
            RefreshList();
        }

        /// <summary>
        /// 보이는 줄 수에 맞춰 판때기를 늘린다.
        ///
        /// 손 제작과 제작대는 보이는 줄 수가 다르다. 높이를 하나로 고정해 두면
        /// 한쪽은 아래가 비고 다른 쪽은 흘러넘친다.
        /// </summary>
        void FitPanel(int visibleRows)
        {
            var rowsRect = rowParent as RectTransform;
            if (panel == null || rowsRect == null) return;

            // 줄이 하나도 만들어지지 않았어도 높이는 재야 한다 — 안내 한 줄이 서 있다.
            var first = _rows.Count > 0 && _rows[0].button != null
                ? (RectTransform)_rows[0].button.transform
                : null;
            float rowHeight = first != null ? first.sizeDelta.y : RowHeight;

            var layout = rowsRect.GetComponent<VerticalLayoutGroup>();
            float spacing = layout != null ? layout.spacing : 8f;

            UISkin.FitPanelHeight(panel, rowsRect, visibleRows, rowHeight, spacing);
        }

        // ── 수량과 걸기 ──────────────────────────────────────────

        int Wanted(RecipeSO r) =>
            r != null && _wanted.TryGetValue(r.id, out var n) ? n : 1;

        void SetWanted(RecipeSO r, int n)
        {
            if (r == null) return;
            _wanted[r.id] = Mathf.Clamp(n, 1, Mathf.Max(1, MaxFor(r)));
            RefreshList();
        }

        void Nudge(RecipeSO r, int delta) => SetWanted(r, Wanted(r) + delta);

        int MaxFor(RecipeSO r) =>
            CraftQueueService.MaxCraftable(r, _inventory?.Inventory, _station, BlueprintGate.Active);

        /// <summary>
        /// 누르면 만들어지는 것이 아니라 줄에 선다. 재료는 이때 빠진다 —
        /// 걸어 놓은 뒤에 재료를 다른 데 써서 대기열이 멈추는 일은 없다.
        /// </summary>
        void Enqueue(RecipeSO r)
        {
            var inv = _inventory?.Inventory;
            if (inv == null || r == null) return;

            int want = Mathf.Clamp(Wanted(r), 1, Mathf.Max(1, MaxFor(r)));

            bool ok;
            if (_stationHost != null)
                ok = CraftQueueService.TryEnqueue(_stationHost.Work.Queue, r, want, inv, _station,
                                                  BlueprintGate.Active);
            else
                ok = HandCraftingService.Instance != null &&
                     HandCraftingService.Instance.TryEnqueue(r, want, _station);

            if (ok) craftFeedback?.PlayFeedbacks();
            RefreshList();
        }

        /// <summary>대기열 칸을 눌렀을 때. 완성되지 않은 것은 전부 돌아온다.</summary>
        void CancelQueued(int index)
        {
            var inv = _inventory?.Inventory;

            if (_stationHost != null)
            {
                if (inv != null) CraftQueueService.TryCancel(_stationHost.Work.Queue, index, inv);
            }
            else HandCraftingService.Instance?.Cancel(index);

            RefreshList();
        }

        /// <summary>
        /// 대기열에 거는 창구. 검증 하네스가 UI 클릭 없이 부를 수 있게 열어 둔다.
        /// </summary>
        public bool RequestCraft(RecipeSO recipe, int count)
        {
            SetWanted(recipe, count);
            int before = ActiveQueue?.Count ?? 0;
            Enqueue(recipe);
            return (ActiveQueue?.Count ?? 0) > before;
        }

        // ── 열고 닫기 ────────────────────────────────────────────

        /// <summary>손 제작 또는 스테이션 없는 열기.</summary>
        public void Open(StationType station) => OpenInternal(station, null, null);

        /// <summary>제작대·화톳불에서 연다. 걸리는 작업은 그 물건에 귀속된다.</summary>
        public void Open(ICraftStation station)
        {
            if (station == null) { OpenInternal(StationType.None, null, null); return; }
            OpenInternal(station.StationType, station, null);
        }

        /// <summary>
        /// 연구대에서 연다. 같은 판때기에 제작 줄 대신 연구 줄이 뜬다 —
        /// 새 패널을 만들지 않는 것이 이 화면의 규율이다(대기열 띠와 같은 이유).
        /// </summary>
        public void Open(IResearchStation station)
        {
            if (station == null) { OpenInternal(StationType.None, null, null); return; }
            OpenInternal(StationType.Research, null, station);
        }

        void OpenInternal(StationType station, ICraftStation host, IResearchStation research)
        {
            _station = station;
            _stationHost = host;
            _researchHost = research;

            EnsureQueueView();
            BindQueueView();

            // 행이 아직 없으면 지금 만든다. 오브젝트가 꺼져 있어
            // 연결대기 코루틴이 돌지 못했을 수 있다.
            if (_rows.Count == 0) BuildRows();

            // 연구 목록은 그 자리가 들고 온 책으로 만든다. 같은 책이면 다시 만들지 않는다.
            if (research != null && (research.Book != _researchBook || _researchRows.Count == 0))
                BuildResearchRows(research.Book);

            if (_isOpen) { RefreshList(); return; }

            _isOpen = true;
            if (_queueView != null) _queueView.SetExpanded(true);

            RefreshList();

            if (group != null)
            {
                group.blocksRaycasts = true;
                group.interactable = true;
                group.DOKill();
                group.DOFade(1f, tweenSeconds);
            }
            if (panel != null)
            {
                panel.DOKill();
                panel.localScale = Vector3.one * 0.92f;
                panel.DOScale(1f, tweenSeconds).SetEase(Ease.OutBack);
            }

            LockControls(true);
            if (GameServices.TryGet<UIStateService>(out var ui)) ui.NotifyOpened(this);
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;

            // 화면은 닫히지만 줄은 계속 흐른다. 스테이션 줄은 그 자리에 두고
            // 화면에는 손 제작 줄만 남긴다 — 들고 다니는 것은 그쪽뿐이다.
            _stationHost = null;
            _researchHost = null;
            _station = StationType.None;
            BindQueueView();
            if (_queueView != null) _queueView.SetExpanded(false);

            if (group != null)
            {
                group.blocksRaycasts = false;
                group.interactable = false;
                group.DOKill();
                group.DOFade(0f, tweenSeconds);
            }

            LockControls(false);
            if (GameServices.TryGet<UIStateService>(out var ui)) ui.NotifyClosed(this);
        }

        void LockControls(bool locked)
        {
            _player?.Locomotion?.SetMovementLocked(locked);
            _player?.CameraRig?.SetLookLocked(locked);
        }

        void OnDestroy()
        {
            group?.DOKill();
            panel?.DOKill();
        }
    }
}
