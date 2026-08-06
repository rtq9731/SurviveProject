using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Survive.Core;
using Survive.Creatures;
using Survive.Localization;
using Survive.Progression;

namespace Survive.UI
{
    /// <summary>
    /// 분석 기록 — 우주복 AI가 지금까지 알아낸 것을 열람하는 화면 (백로그 43).
    ///
    /// <b>새로 기억하는 것이 하나도 없다.</b> 발견도 청사진도 연구도 생물도 전부
    /// <see cref="UnlockLedger"/> 한 곳에 이미 적혀 있고, 이 화면은 그 원장을
    /// 갈래별로 다시 읽을 뿐이다. 목록을 짓는 규칙은 Unity를 모르는
    /// <see cref="CodexCatalog"/>에 있고, 여기 남은 일은 그것을 그리는 것뿐이다.
    ///
    /// <b>씬을 고치지 않는다.</b> 캔버스를 런타임에 세운다 —
    /// <see cref="Survive.Art.GammaCalibrationScreen"/>·<see cref="CraftQueueView"/>와
    /// 같은 관례다. 프리팹을 늘리면 UI 작업이 또 한 파일에서 부딪친다.
    ///
    /// <b>ESC는 듣지 않는다.</b> 닫는 순서는 <see cref="UIStateService"/> 하나가 정한다.
    /// 열릴 때 스스로 등록하고 알리기만 하면, 소지품 위에 겹쳐 뜬 경우에도
    /// ESC 한 번이 이것만 걷어낸다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CodexUI : MonoBehaviour, IClosablePanel
    {
        /// <summary>여닫는 키. 기록(Journal)의 J다. 다른 조작과 겹치지 않는다.</summary>
        public const Key ToggleKey = Key.J;

        /// <summary>Resources 아래에서 생물 목록을 찾는 이름.</summary>
        public const string CreatureBookResourceName = "CreatureBook";

        static CodexUI _instance;
        public static CodexUI Instance => _instance;

        /// <summary>
        /// 스스로 선다. 무엇을 아는가는 특정 씬의 것이 아니라 판 전체의 상태라,
        /// 그것을 읽는 화면도 씬 배선에 매이지 않는 편이 맞다
        /// (<see cref="UnlockService"/>와 같은 이유).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_instance != null) return;

            var go = new GameObject("CodexUI");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<CodexUI>();
        }

        // ── 상태 ─────────────────────────────────────────────────

        static readonly CodexSection[] Sections =
        {
            CodexSection.Discovery, CodexSection.Blueprint,
            CodexSection.Research, CodexSection.Creature,
        };

        readonly List<CodexEntry> _entries = new List<CodexEntry>();

        /// <summary>탭의 "3/5"를 세느라 잠깐 쓰는 자리. 매번 새로 만들면 여는 동안 쓰레기가 쌓인다.</summary>
        readonly List<CodexEntry> _scratch = new List<CodexEntry>();

        readonly List<GameObject> _rows = new List<GameObject>();
        readonly Dictionary<CodexSection, TMP_Text> _tabLabels = new Dictionary<CodexSection, TMP_Text>();
        readonly Dictionary<CodexSection, Image> _tabFrames = new Dictionary<CodexSection, Image>();

        CodexSection _section = CodexSection.Discovery;
        int _selected;
        bool _isOpen;
        bool _dirty;

        DiscoveryBookSO _discoveries;
        ResearchBookSO _research;
        CreatureBookSO _creatures;
        UnlockLedger _bound;

        Survive.Player.PlayerContext _player;

        GameObject _root;
        RectTransform _list;
        TMP_Text _detailTitle;
        TMP_Text _detailBody;
        TMP_Text _summary;

        // 한 번 쓰고 마는 글자들. 로케일이 바뀌면 아무도 다시 쓰지 않으므로
        // 붙들고 있다가 직접 갈아 끼운다 (docs/번역-체계.md 2절).
        TMP_Text _title;
        TMP_Text _caption;
        TMP_Text _footer;

        /// <summary>지금 화면에 뜬 갈래. 검증 하네스가 본다.</summary>
        public CodexSection Section => _section;

        /// <summary>지금 화면에 뜬 줄들. 검증 하네스가 본다.</summary>
        public IReadOnlyList<CodexEntry> Entries => _entries;

        public bool IsOpen => _isOpen;
        public UIPanelKind PanelKind => UIPanelKind.Codex;

        // ── 생애 ─────────────────────────────────────────────────

        void OnEnable() => Loc.LocaleChanged += OnLocaleChanged;

        void OnDisable()
        {
            Loc.LocaleChanged -= OnLocaleChanged;
            Unbind();
            if (GameServices.TryGet<UIStateService>(out var ui)) ui.UnregisterPanel(this);
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// 로케일이 바뀌면 <b>창을 연 채로</b> 글자가 바뀌어야 한다. 목록·탭·요약은
        /// 다시 그리면 따라오지만 제목·부제·바닥글은 세울 때 한 번 쓰이고 아무도
        /// 다시 쓰지 않는다 — 창을 닫았다 열면 바뀌어 있어 사람 눈으로는
        /// 통과한 것처럼 보인다.
        /// </summary>
        void OnLocaleChanged()
        {
            ApplyStaticText();
            _dirty = true;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[ToggleKey].wasPressedThisFrame) Toggle();

            if (!_isOpen) return;

            // 화면이 떠 있는 동안 원장이 바뀔 수 있다 — 대기열이 끝나거나
            // 상자를 열어 새 재료가 들어오거나. 바뀐 프레임에만 다시 그린다.
            RebindLedger();
            if (_dirty) { _dirty = false; Refresh(); }
        }

        void RebindLedger()
        {
            var ledger = BlueprintGate.Active;
            if (ReferenceEquals(ledger, _bound)) return;

            Unbind();
            _bound = ledger;
            if (_bound != null) _bound.KeyUnlocked += OnKeyUnlocked;
            _dirty = true;
        }

        void Unbind()
        {
            if (_bound == null) return;
            _bound.KeyUnlocked -= OnKeyUnlocked;
            _bound = null;
        }

        void OnKeyUnlocked(string _) => _dirty = true;

        // ── 열고 닫기 ────────────────────────────────────────────

        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        public void Open()
        {
            // 캔버스는 씬과 함께 사라질 수 있다(DontDestroyOnLoad 아래에 두었지만
            // 파괴 경로가 하나뿐이라고 믿지 않는다). 없으면 다시 세운다.
            if (_root == null) Build();

            RebindLedger();
            Refresh();

            _root.SetActive(true);
            if (_isOpen) return;
            _isOpen = true;

            LockControls(true);
            if (GameServices.TryGet<UIStateService>(out var ui))
            {
                ui.RegisterPanel(this);
                ui.NotifyOpened(this);
            }
        }

        public void Close()
        {
            if (_root != null) _root.SetActive(false);
            if (!_isOpen) return;
            _isOpen = false;

            if (GameServices.TryGet<UIStateService>(out var ui))
            {
                ui.NotifyClosed(this);

                // 소지품 위에 겹쳐 떠 있었다면 아래 화면이 아직 남아 있다.
                // 무조건 풀어 주면 목록을 보는 중에 몸이 움직이기 시작한다.
                LockControls(ui.AnyPanelOpen);
            }
            else LockControls(false);
        }

        /// <summary>
        /// 읽는 동안 몸은 멈춘다. 시간까지 멈추지는 않는다 — 멈추면 코루틴으로 도는
        /// 검증(E2E)이 같이 서고, 걸어 둔 제작·연구도 함께 얼어붙는다.
        /// </summary>
        void LockControls(bool locked)
        {
            if (_player == null)
                _player = FindAnyObjectByType<Survive.Player.PlayerContext>(FindObjectsInactive.Exclude);

            _player?.Locomotion?.SetMovementLocked(locked);
            _player?.CameraRig?.SetLookLocked(locked);
        }

        // ── 목록 ────────────────────────────────────────────────

        /// <summary>갈래를 바꾼다. 검증 하네스도 이 창구로 탭을 누른다.</summary>
        public void ShowSection(CodexSection section)
        {
            _section = section;
            _selected = 0;
            if (_root != null) Refresh();
        }

        void EnsureBooks()
        {
            if (_discoveries == null)
                _discoveries = UnlockService.Instance != null && UnlockService.Instance.Book != null
                    ? UnlockService.Instance.Book
                    : Resources.Load<DiscoveryBookSO>(UnlockService.BookResourceName);

            if (_research == null)
                _research = Resources.Load<ResearchBookSO>(ResearchStation.BookResourceName);

            if (_creatures == null)
                _creatures = Resources.Load<CreatureBookSO>(CreatureBookResourceName);
        }

        void Refresh()
        {
            EnsureBooks();

            var ledger = BlueprintGate.Active;
            switch (_section)
            {
                case CodexSection.Blueprint:
                    CodexCatalog.BuildBlueprints(_discoveries, _research, ledger, _entries); break;
                case CodexSection.Research:
                    CodexCatalog.BuildResearch(_research, ledger, _entries); break;
                case CodexSection.Creature:
                    CodexCatalog.BuildCreatures(_creatures, ledger, _entries); break;
                default:
                    CodexCatalog.BuildDiscoveries(_discoveries, ledger, _entries); break;
            }

            RefreshTabs(ledger);
            RebuildRows();
            RefreshDetail();
        }

        /// <summary>탭에 "3/5"를 적는다. 얼마나 남았는지가 곧 다음에 할 일이다.</summary>
        void RefreshTabs(UnlockLedger ledger)
        {
            foreach (var section in Sections)
            {
                if (!_tabLabels.TryGetValue(section, out var label)) continue;

                List<CodexEntry> source;
                if (section == _section) source = _entries;
                else
                {
                    switch (section)
                    {
                        case CodexSection.Blueprint:
                            CodexCatalog.BuildBlueprints(_discoveries, _research, ledger, _scratch); break;
                        case CodexSection.Research:
                            CodexCatalog.BuildResearch(_research, ledger, _scratch); break;
                        case CodexSection.Creature:
                            CodexCatalog.BuildCreatures(_creatures, ledger, _scratch); break;
                        default:
                            CodexCatalog.BuildDiscoveries(_discoveries, ledger, _scratch); break;
                    }
                    source = _scratch;
                }

                label.text = Loc.F("UI", "codex_tab", CodexCatalog.SectionTitle(section),
                                   CodexCatalog.CountUnlocked(source), source.Count);
                label.color = section == _section ? ActiveTabLabel : IdleTabLabel;

                if (_tabFrames.TryGetValue(section, out var frame))
                    frame.color = section == _section ? ActiveTabFrame : IdleTabFrame;
            }
        }

        void RebuildRows()
        {
            // 부모에서 먼저 떼고 없앤다. Destroy는 프레임 끝에야 실제로 지우는데,
            // 그때까지 옛 줄이 세로 배치에 계속 끼어 있으면 탭을 바꾼 순간
            // 목록이 두 벌로 겹쳐 보인다.
            foreach (var row in _rows)
            {
                if (row == null) continue;
                row.transform.SetParent(null, false);
                Destroy(row);
            }
            _rows.Clear();

            if (_list == null) return;

            if (_selected >= _entries.Count) _selected = Mathf.Max(0, _entries.Count - 1);

            for (int i = 0; i < _entries.Count; i++)
                _rows.Add(BuildRow(_entries[i], i));
        }

        GameObject BuildRow(CodexEntry entry, int index)
        {
            var go = new GameObject("Codex_" + entry.Key, typeof(RectTransform));
            go.transform.SetParent(_list, false);

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(0f, RowHeight);

            var layout = go.AddComponent<LayoutElement>();
            layout.minHeight = RowHeight;
            layout.preferredHeight = RowHeight;

            var frame = go.AddComponent<Image>();
            UISkin.ApplyPanel(frame, index == _selected ? SelectedRowFrame
                                   : entry.Unlocked ? RowFrame : LockedRowFrame);

            var label = NewText("Label", go.transform, entry.Title, 21, TextAlignmentOptions.Left);
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(16f, 0f);
            lrt.offsetMax = new Vector2(-12f, 0f);
            label.color = entry.Unlocked ? RowLabel : LockedRowLabel;

            var button = go.AddComponent<Button>();
            button.targetGraphic = frame;
            int captured = index;
            button.onClick.AddListener(() => Select(captured));

            // 물질 분석 줄에는 그 물질의 아이템 정의가 붙어 있다. 오른쪽 설명 칸은
            // <b>그때 AI가 한 말</b>을 그대로 싣는 자리라 손대지 않고(E2ECodex의 계약이다),
            // 아이템 자신의 설명문은 커서를 올렸을 때 쪽지로 읽힌다.
            // 잠긴 줄에는 붙이지 않는다 — 이름조차 주지 않는 화면이 쪽지로 흘리면 안 된다.
            if (entry.Section == CodexSection.Discovery && entry.Unlocked)
            {
                var item = DiscoveredItem(entry.Key);
                if (item != null) ItemTooltipTrigger.Attach(go).Bind(item);
            }
            return go;
        }

        /// <summary>
        /// 물질 분석 열쇠가 가리키는 아이템. 발견 목록을 되짚어 찾는다 —
        /// 열쇠와 아이템을 잇는 표를 여기서 또 만들면 <see cref="FieldDiscovery.KeyOf"/>와
        /// 어긋날 자리가 하나 늘어난다.
        /// </summary>
        Survive.Items.ItemDataSO DiscoveredItem(string key)
        {
            if (string.IsNullOrEmpty(key) || _discoveries?.discoveries == null) return null;

            foreach (var d in _discoveries.discoveries)
                if (d != null && FieldDiscovery.KeyOf(d) == key) return d.item;

            return null;
        }

        /// <summary>줄 하나를 고른다. 잠긴 줄도 고를 수 있다 — 왜 잠겼는지가 오른쪽에 뜬다.</summary>
        public void Select(int index)
        {
            if (index < 0 || index >= _entries.Count) return;

            _selected = index;
            for (int i = 0; i < _rows.Count; i++)
            {
                var image = _rows[i] != null ? _rows[i].GetComponent<Image>() : null;
                if (image == null) continue;

                image.color = i == _selected ? SelectedRowFrame
                            : _entries[i].Unlocked ? RowFrame : LockedRowFrame;
            }
            RefreshDetail();
        }

        void RefreshDetail()
        {
            if (_detailTitle == null || _detailBody == null) return;

            if (_entries.Count == 0)
            {
                _detailTitle.text = Loc.T("UI", "codex_empty_title");
                _detailBody.text = Loc.T("UI", "codex_empty_body");
                if (_summary != null) _summary.text = "";
                return;
            }

            var entry = _entries[Mathf.Clamp(_selected, 0, _entries.Count - 1)];
            _detailTitle.text = entry.Title;
            _detailTitle.color = entry.Unlocked ? RowLabel : LockedRowLabel;
            _detailBody.text = entry.Body;
            _detailBody.color = entry.Unlocked ? BodyLabel : LockedRowLabel;

            if (_summary != null)
                _summary.text = Loc.F("UI", "codex_summary",
                                      CodexCatalog.CountUnlocked(_entries), _entries.Count);
        }

        // ── 색과 치수 ────────────────────────────────────────────

        const float RowHeight = 44f;

        static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.78f);
        static readonly Color PanelFrame = new Color(0.10f, 0.12f, 0.15f, 0.97f);

        // 목록·설명 칸은 판때기보다 <b>어둡다</b>. 밝게 얹으면 판이 둘로 보이는데,
        // 파 놓은 자리처럼 가라앉혀야 그 안의 줄이 도드라진다.
        static readonly Color ColumnFrame = new Color(0.05f, 0.06f, 0.08f, 0.95f);
        static readonly Color RowFrame = new Color(0.15f, 0.18f, 0.23f, 0.95f);
        static readonly Color LockedRowFrame = new Color(0.09f, 0.10f, 0.13f, 0.85f);
        static readonly Color SelectedRowFrame = new Color(0.20f, 0.30f, 0.38f, 0.98f);
        static readonly Color RowLabel = new Color(0.92f, 0.94f, 0.96f, 1f);
        static readonly Color LockedRowLabel = new Color(0.46f, 0.48f, 0.56f, 1f);
        static readonly Color BodyLabel = new Color(0.80f, 0.86f, 0.90f, 1f);
        static readonly Color ActiveTabFrame = new Color(0.20f, 0.30f, 0.38f, 0.98f);
        static readonly Color IdleTabFrame = new Color(0.12f, 0.14f, 0.18f, 0.92f);
        static readonly Color ActiveTabLabel = new Color(0.95f, 0.97f, 1f, 1f);
        static readonly Color IdleTabLabel = new Color(0.60f, 0.64f, 0.70f, 1f);
        static readonly Color Accent = new Color(0.72f, 0.86f, 0.92f, 1f);

        // ── 화면 조립 ────────────────────────────────────────────

        void Build()
        {
            _root = new GameObject("CodexCanvas");
            _root.transform.SetParent(transform, false);

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 소지품·제작 위에 뜨되 밝기 조절 화면(5000) 아래다.
            canvas.sortingOrder = 3000;

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            _root.AddComponent<GraphicRaycaster>();

            var backdrop = NewPlain("Backdrop", _root.transform, Backdrop);
            Stretch(backdrop.rectTransform);

            var panel = NewImage("Panel", _root.transform, PanelFrame);
            Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1360f, 820f));

            _title = NewText("Title", panel.transform, "", 40, TextAlignmentOptions.Left);
            Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -30f), new Vector2(700f, 56f));
            _title.color = Accent;

            _caption = NewText("Caption", panel.transform, "", 20, TextAlignmentOptions.Left);
            Place(_caption.rectTransform, new Vector2(0f, 1f), new Vector2(42f, -84f), new Vector2(700f, 30f));

            _summary = NewText("Summary", panel.transform, "", 20, TextAlignmentOptions.Right);
            Place(_summary.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(420f, 32f));

            BuildTabs(panel.transform);
            BuildColumns(panel.transform);

            _footer = NewText("Footer", panel.transform, "", 19, TextAlignmentOptions.Center);
            Place(_footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(700f, 30f));

            ApplyStaticText();
            _root.SetActive(false);
        }

        /// <summary>
        /// 세울 때 한 번 쓰이고 마는 글자를 표에서 다시 꺼낸다.
        /// <see cref="OnLocaleChanged"/>가 로케일이 바뀔 때마다 다시 부른다.
        /// </summary>
        void ApplyStaticText()
        {
            if (_title != null) _title.text = Loc.T("UI", "codex_title");
            if (_caption != null) _caption.text = Loc.T("UI", "codex_caption");
            if (_footer != null) _footer.text = Loc.F("UI", "codex_footer", ToggleKey);
        }

        void BuildTabs(Transform parent)
        {
            var row = new GameObject("Tabs", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            Place((RectTransform)row.transform, new Vector2(0f, 1f),
                  new Vector2(40f, -124f), new Vector2(1280f, 52f));

            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            foreach (var section in Sections)
            {
                var image = NewImage("Tab_" + section, row.transform, IdleTabFrame);

                var label = NewText("Label", image.transform,
                                    CodexCatalog.SectionTitle(section), 21, TextAlignmentOptions.Center);
                Stretch(label.rectTransform);

                var button = image.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                var captured = section;
                button.onClick.AddListener(() => ShowSection(captured));

                _tabLabels[section] = label;
                _tabFrames[section] = image;
            }
        }

        void BuildColumns(Transform parent)
        {
            // 왼쪽: 무엇이 있는가. 오른쪽: 그것이 무엇인가.
            var listFrame = NewImage("ListColumn", parent, ColumnFrame);
            Place(listFrame.rectTransform, new Vector2(0f, 1f),
                  new Vector2(40f, -190f), new Vector2(470f, 560f));

            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(listFrame.transform, false);
            Stretch((RectTransform)viewport.transform);
            var vrt = (RectTransform)viewport.transform;
            vrt.offsetMin = new Vector2(10f, 10f);
            vrt.offsetMax = new Vector2(-10f, -10f);
            viewport.AddComponent<RectMask2D>();

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            _list = (RectTransform)content.transform;
            _list.anchorMin = new Vector2(0f, 1f);
            _list.anchorMax = new Vector2(1f, 1f);
            _list.pivot = new Vector2(0.5f, 1f);
            _list.anchoredPosition = Vector2.zero;

            // 새 RectTransform의 sizeDelta 기본값(100)을 그대로 두면 좌우로 늘어난
            // 앵커에 100이 더해져 목록이 창보다 넓어진다. 그러면 줄이 왼쪽으로
            // 밀려 나가 이름 앞머리가 잘린다 — 실제로 그렇게 찍혔다.
            _list.sizeDelta = Vector2.zero;

            var vertical = content.AddComponent<VerticalLayoutGroup>();
            vertical.spacing = 6f;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;
            vertical.childControlHeight = true;
            vertical.childControlWidth = true;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 항목이 늘어나면 목록이 판때기를 넘긴다. 넘긴 만큼은 굴려서 본다.
            var scroll = listFrame.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = vrt;
            scroll.content = _list;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            var detailFrame = NewImage("DetailColumn", parent, ColumnFrame);
            Place(detailFrame.rectTransform, new Vector2(0f, 1f),
                  new Vector2(530f, -190f), new Vector2(790f, 560f));

            _detailTitle = NewText("DetailTitle", detailFrame.transform, "", 28, TextAlignmentOptions.TopLeft);
            Place(_detailTitle.rectTransform, new Vector2(0f, 1f),
                  new Vector2(26f, -24f), new Vector2(738f, 46f));

            _detailBody = NewText("DetailBody", detailFrame.transform, "", 21, TextAlignmentOptions.TopLeft);
            Place(_detailBody.rectTransform, new Vector2(0f, 1f),
                  new Vector2(26f, -84f), new Vector2(738f, 450f));
            _detailBody.color = BodyLabel;
        }

        // ── 잡동사니 ─────────────────────────────────────────────

        static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            UISkin.ApplyPanel(image, color);
            return image;
        }

        /// <summary>무늬 없는 순수한 사각. 화면 전체를 덮는 어둠에 쓴다.</summary>
        static Image NewPlain(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = null;
            image.color = color;
            return image;
        }

        static TextMeshProUGUI NewText(string name, Transform parent, string body,
                                       int size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<TextMeshProUGUI>();
            var font = ResolveFont();
            if (font != null) text.font = font;

            text.text = body;
            text.fontSize = size;
            text.alignment = align;
            text.color = RowLabel;
            text.raycastTarget = false;
            return text;
        }

        static TMP_FontAsset _font;

        /// <summary>
        /// 이미 화면에 쓰이고 있는 글꼴을 빌린다. 본문이 전부 한글이라
        /// TMP 기본 글꼴로 떨어지면 화면이 두부(□)로 찬다.
        /// </summary>
        static TMP_FontAsset ResolveFont()
        {
            if (_font != null) return _font;

            var any = FindAnyObjectByType<TextMeshProUGUI>(FindObjectsInactive.Include);
            _font = any != null && any.font != null ? any.font : TMP_Settings.defaultFontAsset;
            return _font;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void Anchor(RectTransform rect, Vector2 anchor, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        /// <summary>모서리에서 잰 자리. 판때기 크기를 바꿔도 여백이 따라온다.</summary>
        static void Place(RectTransform rect, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(anchor.x, anchor.y);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }
    }
}
