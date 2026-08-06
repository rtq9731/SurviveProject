using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Survive.Domain.Art;
using Survive.Localization;
using Survive.Testing;
using Survive.UI;

namespace Survive.Art
{
    /// <summary>
    /// 첫 실행에 화면 밝기를 맞추게 하는 화면 (백로그 42).
    ///
    /// <b>왜 이 게임에 필수인가.</b> 화면의 대부분이 검정이면 모니터 편차가 그대로
    /// 난이도가 된다. 밝은 화면에서는 랜턴 없이도 지형이 읽히고, 어두운 화면에서는
    /// 랜턴을 켜도 벽이 안 보인다. "어둠이 위협"이라는 설계가 사람마다 다른
    /// 게임이 되지 않게 출발선에서 한 번 맞춘다.
    ///
    /// <b>표준 문법.</b> 아주 어두운 무늬 셋을 띄우고 가운데 것이 "간신히 보일 때까지"
    /// 슬라이더를 옮기게 한다. 왼쪽은 안 보여야 하고 오른쪽은 확실히 보여야 한다 —
    /// 셋을 나란히 두면 기준이 생겨서 혼자 판단하기가 쉬워진다.
    ///
    /// <b>씬을 고치지 않는다.</b> 캔버스를 런타임에 세운다. 그림은
    /// <see cref="UISkin"/>의 판때기를 그대로 쓴다 — 코드로 만드는 다른 UI와 같은 관례다.
    ///
    /// <b>시간을 멈추지 않는다.</b> 멈추면 코루틴으로 도는 검증(E2E)이 같이 멈춘다.
    /// 첫 실행에 잠깐 겹쳐 보이는 것이 검증 전체를 세우는 것보다 싸다.
    /// </summary>
    [DisallowMultipleComponent]
    public class GammaCalibrationScreen : MonoBehaviour
    {
        /// <summary>다시 부르는 키. 설정 화면이 생기면 그쪽으로 옮긴다.</summary>
        public const Key ReopenKey = Key.F9;

        /// <summary>첫 실행에 스스로 뜰 것인가. 검증 하네스가 끄고 쓴다.</summary>
        public static bool AutoShow { get; set; } = true;

        static GammaCalibrationScreen _instance;

        public static bool IsOpen => _instance != null && _instance._root != null && _instance._root.activeSelf;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_instance != null) return;

            var go = new GameObject("GammaCalibrationScreen");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GammaCalibrationScreen>();
        }

        /// <summary>바깥에서 다시 부른다(일시정지 메뉴 등).</summary>
        public static void Open()
        {
            Install();
            _instance.Show();
        }

        /// <summary>
        /// 치운다. 사람이 끝낸 것이 아니므로 "맞췄다"로 기록하지 않는다 —
        /// 검증이 잠깐 치운 것 때문에 다음 실행에 조절 화면이 안 뜨면 안 된다.
        /// </summary>
        public static void Close()
        {
            if (_instance != null) _instance.Hide(false);
        }

        GameObject _root;
        Slider _slider;

        // 한 번 쓰고 마는 글자들. 이 화면에는 매 프레임 다시 그리는 고리가 없어서
        // 로케일이 바뀌어도 아무도 이 칸들을 건드리지 않는다 — 붙들고 있다가
        // 직접 갈아 끼운다 (docs/번역-체계.md 2절).
        TextMeshProUGUI _title;
        TextMeshProUGUI _guide;
        TextMeshProUGUI _footer;
        TextMeshProUGUI _confirmLabel;
        TextMeshProUGUI _patchHidden;
        TextMeshProUGUI _patchBarely;
        TextMeshProUGUI _patchClear;

        void OnEnable() => Loc.LocaleChanged += ApplyStaticText;

        void OnDisable() => Loc.LocaleChanged -= ApplyStaticText;

        void Start()
        {
            if (AutoShow && !GammaSettings.Calibrated && !GammaSettings.ForceNeutral) Show();
        }

        void Update()
        {
            // 검증이 돌기 시작하면 비켜난다. 화면을 가린 채로 스크린샷을 찍으면
            // 무엇을 본 것인지 알 수 없다.
            if (IsOpen && E2ERunner.Status == E2ERunner.RunStatus.Running) { Hide(false); return; }

            // 커서를 매 프레임 다시 붙든다. 이 화면은 스스로 서기 때문에
            // PlayerCameraRig의 Start와 어느 쪽이 먼저 도는지 정해져 있지 않고,
            // 그쪽이 나중이면 커서가 도로 잠긴다. 열려 있는 동안만이라 값싸다.
            if (IsOpen && Cursor.lockState != CursorLockMode.None) GrabCursor();

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard[ReopenKey].wasPressedThisFrame)
            {
                if (IsOpen) Hide();
                else Show();
            }
            else if (IsOpen)
            {
                if (keyboard[Key.Escape].wasPressedThisFrame) { Hide(); return; }
                HandleKeyboard(keyboard);
            }
        }

        /// <summary>
        /// 키보드로도 맞추고 넘어갈 수 있게 한다.
        ///
        /// 마우스 하나에만 기대면 커서가 잡히는 순간 이 화면에 갇힌다 —
        /// 실제로 그렇게 갇혔다. 게임의 첫 화면에 빠져나갈 길이 하나뿐이면 안 된다.
        /// </summary>
        void HandleKeyboard(Keyboard keyboard)
        {
            if (_slider == null) return;

            if (keyboard[Key.Enter].wasPressedThisFrame ||
                keyboard[Key.NumpadEnter].wasPressedThisFrame ||
                keyboard[Key.Space].wasPressedThisFrame)
            {
                Hide();
                return;
            }

            float step = 0f;
            if (keyboard[Key.LeftArrow].wasPressedThisFrame || keyboard[Key.A].wasPressedThisFrame) step = -KeyStep;
            if (keyboard[Key.RightArrow].wasPressedThisFrame || keyboard[Key.D].wasPressedThisFrame) step = KeyStep;
            if (step == 0f) return;

            _slider.value = Mathf.Clamp(_slider.value + step, _slider.minValue, _slider.maxValue);
        }

        /// <summary>
        /// 화살표 한 번에 움직이는 폭. 슬라이더 전체를 스무 칸으로 나눈 값이다 —
        /// 한 번 눌러 변화가 안 보이면 사람은 키가 안 먹는다고 생각한다.
        /// </summary>
        const float KeyStep = 0.05f;

        void Show()
        {
            if (_root == null) Build();
            _slider.SetValueWithoutNotify(GammaSettings.Value);
            _root.SetActive(true);
            GrabCursor();
        }

        void Hide(bool remember = true)
        {
            if (_root == null) return;
            _root.SetActive(false);
            ReleaseCursor();
            if (remember) GammaSettings.Calibrated = true;
        }

        // ── 커서 ─────────────────────────────────────────────────

        /// <summary>
        /// 커서를 놓아 준다. <b>이것이 없으면 이 화면의 어떤 것도 눌리지 않는다.</b>
        ///
        /// <see cref="Survive.Player.PlayerCameraRig"/>가 <c>Start</c>에서 커서를 잠그고
        /// 숨기는데, 잠긴 커서는 화면 한가운데에 고정된다. 그래서 마우스를 어디로
        /// 옮겨 클릭하든 판정은 언제나 화면 중앙으로 가고, 슬라이더도 확인 단추도
        /// 거기 없으니 아무 일도 일어나지 않았다.
        ///
        /// <see cref="Survive.Player.PlayerCameraRig.SetLookLocked"/>를 쓰는 이유는
        /// 커서만 풀면 시점이 마우스를 따라 계속 돌기 때문이다 — 밝기를 맞추는 동안
        /// 배경이 휙휙 돌아가면 무늬를 견줄 수가 없다. (그쪽 이름의 <c>locked</c>는
        /// "시점을 잠근다"는 뜻이라 커서는 반대로 풀린다.)
        /// </summary>
        void GrabCursor()
        {
            _rig = FindRig();
            if (_rig != null) _rig.SetLookLocked(true);

            // 리그를 못 찾아도 커서는 풀어야 한다. 이 화면이 못 눌리는 것보다
            // 시점이 도는 편이 낫다.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void ReleaseCursor()
        {
            if (_rig != null) { _rig.SetLookLocked(false); _rig = null; return; }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>
        /// 시점 리그를 찾는다. 이 화면은 씬을 모르는 채로 스스로 서기 때문에
        /// 인스펙터로 물릴 자리가 없다. 화면이 열려 있는 동안만, 그것도
        /// 놓쳤을 때만 다시 찾는다.
        /// </summary>
        static Survive.Player.PlayerCameraRig FindRig() =>
            Object.FindAnyObjectByType<Survive.Player.PlayerCameraRig>(FindObjectsInactive.Exclude);

        Survive.Player.PlayerCameraRig _rig;

        // ── 화면 조립 ────────────────────────────────────────────
        void Build()
        {
            _root = new GameObject("GammaCalibrationCanvas");
            _root.transform.SetParent(transform, false);

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;   // 무엇보다 위. 밝기를 맞추는 동안은 이것만 보여야 한다

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            _root.AddComponent<GraphicRaycaster>();

            // 배경은 완전한 검정이어야 한다. 무늬가 2%인데 배경이 3%면
            // 무늬가 배경보다 어두워서 조절 자체가 성립하지 않는다.
            var backdrop = NewPlain("Backdrop", _root.transform, Color.black);
            Stretch(backdrop.rectTransform);

            _title = NewText("Title", _root.transform, "", 46, TextAlignmentOptions.Center);
            Anchor(_title.rectTransform, new Vector2(0.5f, 0.86f), new Vector2(1200f, 70f));

            _guide = NewText("Guide", _root.transform, "", 28, TextAlignmentOptions.Top);
            Anchor(_guide.rectTransform, new Vector2(0.5f, 0.76f), new Vector2(1200f, 110f));

            BuildPatches(_root.transform);

            _slider = BuildSlider(_root.transform);
            _slider.onValueChanged.AddListener(v => GammaSettings.Value = v);

            var done = BuildButton(_root.transform, new Vector2(0.5f, 0.13f), out _confirmLabel);
            done.onClick.AddListener(() => Hide());

            _footer = NewText("Footer", _root.transform, "", 22, TextAlignmentOptions.Center);
            Anchor(_footer.rectTransform, new Vector2(0.5f, 0.06f), new Vector2(1200f, 40f));

            ApplyStaticText();
        }

        /// <summary>
        /// 세울 때 한 번 쓰이고 마는 글자를 표에서 다시 꺼낸다.
        /// 이 화면은 다시 그리는 고리가 없어서, 로케일이 바뀌면 여기를 부르지 않는 한
        /// 묵은 글자가 그대로 남는다 (docs/번역-체계.md 2절).
        /// </summary>
        void ApplyStaticText()
        {
            if (_title != null) _title.text = Loc.T("UI", "gamma_title");
            if (_guide != null) _guide.text = Loc.T("UI", "gamma_guide");
            if (_confirmLabel != null) _confirmLabel.text = Loc.T("UI", "gamma_confirm");
            if (_footer != null) _footer.text = Loc.F("UI", "gamma_footer", ReopenKey);

            // 이름표를 변수로 만들지 않는다 — 변수 키는 누락 키 게이트가
            // 검사할 수 없어 사각지대가 하나 늘어난다.
            if (_patchHidden != null) _patchHidden.text = Loc.T("UI", "gamma_patch_hidden");
            if (_patchBarely != null) _patchBarely.text = Loc.T("UI", "gamma_patch_barely");
            if (_patchClear != null) _patchClear.text = Loc.T("UI", "gamma_patch_clear");
        }

        /// <summary>
        /// 세 무늬. 가운데가 기준점(<see cref="GammaGrade.PatchLuminance"/>)이고
        /// 양옆은 그 절반과 세 배다. 무늬 안에 한 단계 밝은 사각을 넣어 두어
        /// "판이 보이는가"가 아니라 "판 안의 무늬가 보이는가"를 묻는다 —
        /// 균일한 회색판은 주변 대비 때문에 실제보다 잘 보인다.
        /// </summary>
        void BuildPatches(Transform parent)
        {
            var row = new GameObject("Patches", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            Anchor((RectTransform)row.transform, new Vector2(0.5f, 0.48f), new Vector2(1080f, 300f));

            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 40f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            _patchHidden = AddPatch(row.transform, GammaGrade.PatchLuminance * 0.5f);
            _patchBarely = AddPatch(row.transform, GammaGrade.PatchLuminance);
            _patchClear = AddPatch(row.transform, GammaGrade.PatchLuminance * 3f);
        }

        /// <summary>무늬 하나. 아래 글자는 <see cref="ApplyStaticText"/>가 표에서 채운다.</summary>
        TextMeshProUGUI AddPatch(Transform parent, float luminance)
        {
            var cell = new GameObject("Patch", typeof(RectTransform));
            cell.transform.SetParent(parent, false);

            var plate = NewPlain("Plate", cell.transform, Color.black);
            Stretch(plate.rectTransform);

            // 판 안의 무늬. 판보다 한 단계 밝다.
            var mark = NewPlain("Mark", plate.transform, new Color(luminance, luminance, luminance, 1f));
            var r = mark.rectTransform;
            r.anchorMin = new Vector2(0.25f, 0.3f);
            r.anchorMax = new Vector2(0.75f, 0.85f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;

            var label = NewText("Caption", plate.transform, "", 22, TextAlignmentOptions.Bottom);
            var lr = label.rectTransform;
            lr.anchorMin = new Vector2(0f, 0f);
            lr.anchorMax = new Vector2(1f, 0.25f);
            lr.offsetMin = Vector2.zero;
            lr.offsetMax = Vector2.zero;
            label.color = new Color(0.45f, 0.45f, 0.45f, 1f);
            return label;
        }

        Slider BuildSlider(Transform parent)
        {
            var go = new GameObject("GammaSlider", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Anchor((RectTransform)go.transform, new Vector2(0.5f, 0.26f), new Vector2(760f, 44f));

            var background = NewImage("Background", go.transform, new Color(0.16f, 0.16f, 0.18f, 1f));
            Stretch(background.rectTransform);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            Stretch((RectTransform)fillArea.transform);

            var fill = NewImage("Fill", fillArea.transform, new Color(0.55f, 0.55f, 0.6f, 1f));
            Stretch(fill.rectTransform);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            Stretch((RectTransform)handleArea.transform);

            var handle = NewImage("Handle", handleArea.transform, Color.white);
            handle.rectTransform.sizeDelta = new Vector2(28f, 56f);

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(GammaSettings.Value);
            return slider;
        }

        Button BuildButton(Transform parent, Vector2 anchor, out TextMeshProUGUI label)
        {
            var image = NewImage("Confirm", parent, new Color(0.2f, 0.2f, 0.24f, 1f));
            Anchor(image.rectTransform, anchor, new Vector2(280f, 62f));

            var text = NewText("Label", image.transform, "", 28, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            label = text;

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        // ── 잡동사니 ─────────────────────────────────────────────

        /// <summary>판때기(9-슬라이스). 버튼·막대 같은 살림살이에 쓴다.</summary>
        static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            UISkin.ApplyPanel(image, color);
            return image;
        }

        /// <summary>
        /// 무늬 없는 순수한 사각. 조절 무늬와 배경은 반드시 이것이어야 한다 —
        /// 9-슬라이스 판때기는 테두리가 둥글고 가장자리 알파가 섞여서
        /// "2% 밝기"라는 기준이 무너진다.
        /// </summary>
        static Image NewPlain(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = null;
            image.color = color;
            return image;
        }

        static TextMeshProUGUI NewText(string name, Transform parent, string body, int size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();

            var font = ResolveFont();
            if (font != null) text.font = font;

            text.text = body;
            text.fontSize = size;
            text.alignment = align;
            text.color = new Color(0.8f, 0.8f, 0.82f, 1f);
            text.raycastTarget = false;
            return text;
        }

        static TMP_FontAsset _font;

        /// <summary>
        /// 이미 화면에 쓰이고 있는 글꼴을 그대로 빌린다. 한글이 들어가므로
        /// TMP 기본 글꼴로 떨어지면 네모가 뜬다 — 씬의 UI가 쓰는 것을 먼저 찾는다.
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
    }
}
