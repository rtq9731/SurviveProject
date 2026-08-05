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
            public Image frame;        // 잠긴 줄은 판때기까지 가라앉힌다
            public TMP_Text label;
            public Button minus;
            public Button plus;
            public Button max;
            public TMP_Text amount;
        }

        readonly List<Row> _rows = new List<Row>();

        /// <summary>레시피마다 마지막으로 고른 수량. 창을 닫아도 기억한다.</summary>
        readonly Dictionary<string, int> _wanted = new Dictionary<string, int>();

        PlayerInventory _inventory;
        Survive.Player.PlayerContext _player;
        StationType _station = StationType.None;
        ICraftStation _stationHost;
        CraftQueueView _queueView;

        /// <summary>연료 넣기처럼 스테이션마다 붙는 한 줄. 없는 스테이션에서는 꺼진다.</summary>
        Button _sideButton;
        TMP_Text _sideLabel;

        /// <summary>지금 열려 있는 목록이 어느 작업대 기준인지. 소지품 UI가 구분에 쓴다.</summary>
        public StationType CurrentStation => _station;

        /// <summary>제작대·화톳불에서 열었으면 그 물건. 손 제작이면 null.</summary>
        public ICraftStation CurrentStationHost => _stationHost;

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
            StartCoroutine(BindWhenReady());
        }

        void OnDisable()
        {
            if (GameServices.TryGet<UIStateService>(out var ui)) ui.UnregisterPanel(this);
        }

        IEnumerator BindWhenReady()
        {
            yield return null;
            GameServices.TryGet<PlayerInventory>(out _inventory);
            _player = UnityEngine.Object.FindFirstObjectByType<Survive.Player.PlayerContext>(FindObjectsInactive.Exclude);
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
            RefreshList();
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
            var max    = MakeMiniButton(go.transform, "Max",   "최대",  60f, 52f);

            var row = new Row
            {
                recipe = r, button = btn, frame = img, label = txt,
                minus = minus, plus = plus, max = max, amount = amount
            };

            var captured = r;
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

        /// <summary>잠긴 줄의 판때기·글자색. 실루엣만 남기고 가라앉힌다.</summary>
        static readonly Color LockedFrame = new Color(0.09f, 0.10f, 0.13f, 0.75f);
        static readonly Color LockedLabel = new Color(0.46f, 0.48f, 0.56f, 1f);
        static readonly Color NormalFrame = new Color(0.12f, 0.14f, 0.18f, 0.9f);

        void RefreshList()
        {
            var inv = _inventory?.Inventory;
            var ledger = BlueprintGate.Active;
            int shown = 0;

            foreach (var row in _rows)
            {
                // 손 제작 목록에서는 스테이션 전용을 아예 숨긴다.
                // 회색으로 남겨두면 "왜 안 되지"를 매번 확인하게 된다.
                // 스테이션에서는 손으로 되는 것까지 보인다 — 제작대 앞에서
                // 곡괭이를 만들려고 창을 두 번 여는 일은 없어야 한다.
                // 다만 <b>다른</b> 스테이션 전용은 여기서도 숨긴다.
                bool visible = row.recipe.requiredStation == StationType.None ||
                               row.recipe.requiredStation == _station;
                if (row.button != null && row.button.gameObject.activeSelf != visible)
                    row.button.gameObject.SetActive(visible);
                if (!visible) continue;
                shown++;

                // 모르는 것은 목록에서 지우지 않는다. 무엇이 있는지는 보여야
                // 찾아 나설 수 있다 — 대신 실루엣으로 가라앉히고, 왜 잠겼는지와
                // 무엇을 하면 열리는지를 그 자리에 적는다.
                bool known = BlueprintGate.IsUnlocked(row.recipe.requiredBlueprint, ledger);

                int max = known ? MaxFor(row.recipe) : 0;
                int want = Mathf.Clamp(Wanted(row.recipe), 1, Mathf.Max(1, max));
                _wanted[row.recipe.id] = want;

                var queue = ActiveQueue;
                bool queueRoom = queue != null && !queue.IsFull;
                bool canMake = known && max >= want && queueRoom;

                if (row.button != null) row.button.interactable = canMake;
                if (row.frame != null) row.frame.color = known ? NormalFrame : LockedFrame;
                if (row.minus != null) row.minus.interactable = known && want > 1;
                if (row.plus != null) row.plus.interactable = known && want < max;
                if (row.max != null) row.max.interactable = known && max >= 1 && want != max;
                if (row.amount != null)
                {
                    row.amount.text = known ? "x" + want : "—";
                    row.amount.color = canMake ? Color.white
                                     : known ? new Color(0.6f, 0.6f, 0.66f)
                                             : LockedLabel;
                }

                if (row.label != null)
                {
                    row.label.text = known
                        ? Describe(row.recipe, inv, want, queueRoom)
                        : DescribeLocked(row.recipe);
                    row.label.color = canMake ? Color.white
                                    : known ? new Color(0.65f, 0.65f, 0.7f, 1f)
                                            : LockedLabel;
                }
            }

            shown += RefreshSideRow() ? 1 : 0;
            FitPanel(shown);
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
            if (panel == null || rowsRect == null || _rows.Count == 0) return;

            var first = _rows[0].button != null ? (RectTransform)_rows[0].button.transform : null;
            float rowHeight = first != null ? first.sizeDelta.y : RowHeight;

            var layout = rowsRect.GetComponent<VerticalLayoutGroup>();
            float spacing = layout != null ? layout.spacing : 8f;

            UISkin.FitPanelHeight(panel, rowsRect, visibleRows, rowHeight, spacing);
        }

        string Describe(RecipeSO r, Inventory inv, int want, bool queueRoom)
        {
            var sb = new StringBuilder();
            sb.Append(string.IsNullOrEmpty(r.displayName) ? r.result?.item?.displayName ?? r.id : r.displayName);
            sb.Append("  ·  ");

            if (r.ingredients == null || r.ingredients.Length == 0) sb.Append("재료 없음");
            else
            {
                bool first = true;
                foreach (var need in r.ingredients)
                {
                    if (need?.item == null) continue;
                    if (!first) sb.Append(", ");
                    int held = inv != null ? inv.CountOf(need.item.id) : 0;
                    sb.Append($"{need.item.displayName} {held}/{need.count * want}");
                    first = false;
                }
            }

            sb.Append($"  ·  {CraftTimeText.Short(r.craftSeconds * want)}");
            if (!queueRoom) sb.Append("  (대기열이 가득 찼다)");
            return sb.ToString();
        }

        /// <summary>
        /// 잠긴 줄. 재료 목록은 적지 않는다 — 무엇이 드는지도 아직 모르는 것이
        /// 잠겼다는 뜻이다. 이름과 여는 방법만 남긴다.
        /// </summary>
        static string DescribeLocked(RecipeSO r)
        {
            string name = string.IsNullOrEmpty(r.displayName)
                ? r.result?.item?.displayName ?? r.id
                : r.displayName;

            return $"{name}  ·  {BlueprintGate.LockText(r.requiredBlueprint)}";
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
        public void Open(StationType station) => OpenInternal(station, null);

        /// <summary>제작대·화톳불에서 연다. 걸리는 작업은 그 물건에 귀속된다.</summary>
        public void Open(ICraftStation station)
        {
            if (station == null) { OpenInternal(StationType.None, null); return; }
            OpenInternal(station.StationType, station);
        }

        void OpenInternal(StationType station, ICraftStation host)
        {
            _station = station;
            _stationHost = host;

            EnsureQueueView();
            BindQueueView();

            if (_isOpen) { RefreshList(); return; }

            _isOpen = true;
            if (_queueView != null) _queueView.SetExpanded(true);

            // 행이 아직 없으면 지금 만든다. 오브젝트가 꺼져 있어
            // 연결대기 코루틴이 돌지 못했을 수 있다.
            if (_rows.Count == 0) BuildRows();
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
