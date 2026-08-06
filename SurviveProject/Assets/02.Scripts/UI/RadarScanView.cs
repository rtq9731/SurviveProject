using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Survive.Instruments;
using Survive.Localization;

namespace Survive.UI
{
    /// <summary>
    /// 관측 중인 화면과 결과 화면 (챕터 1 스펙 §8-2).
    ///
    /// <b>왜 화면이 필요한가.</b> 이 장비가 파는 것은 정보뿐이다. 화면이 없으면
    /// 배터리와 시간만 사라지고 산 것이 어디에도 안 남는다.
    ///
    /// <b>패널 스택에 들어가지 않는다.</b> ESC로 닫는 물건이 아니라 관측이 끝나면
    /// 스스로 사라진다 — <see cref="PickupFeedView"/>와 같은 판단이다.
    ///
    /// <b>어둠을 밝히지 않는다.</b> 반투명 판 하나와 글자뿐이고 세계에 빛을 내지 않는다.
    /// 관측 연출이 주변을 밝히기 시작하면 그것이 설계 위반이다.
    ///
    /// <b>씬을 고치지 않는다.</b> 스스로 선다.
    /// </summary>
    [DisallowMultipleComponent]
    public class RadarScanView : MonoBehaviour
    {
        /// <summary>획득 알림(2500) 위, 도감(3000) 아래.</summary>
        public const int SortingOrder = 2700;

        /// <summary>결과나 끊김을 알린 뒤 스스로 물러나기까지의 시간(초).</summary>
        public const float HoldSeconds = 6f;

        static RadarScanView _instance;
        public static RadarScanView Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_instance != null) return;

            var go = new GameObject("RadarScanView");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<RadarScanView>();
        }

        // ── 자리와 치수 ──────────────────────────────────────────
        //
        // 1920x1080 기준. 화면 가운데 조금 위 — 서서 기다리는 동안 눈이 가는 자리다.
        // 아래 가운데는 조작 안내와 빠른 슬롯이 쓰고 있어 비켜 간다.

        const float PanelWidth = 520f;
        const float PanelBottom = 620f;
        const float BarHeight = 8f;

        static readonly Color PanelColor = new Color(0.10f, 0.12f, 0.15f, 0.86f);
        static readonly Color BarBack = new Color(0.07f, 0.08f, 0.11f, 0.98f);

        /// <summary>
        /// 게이지 색. 광원 4색 규칙은 <b>세계의 발광체</b>에 걸리는 것이고 화면 위 글자는
        /// 그 밖이지만, 그래도 흰끼 도는 청록으로 둔다 — 자홍은 낫과 코어의 색이라
        /// 화면에서 그 색을 쓰면 신호가 흐려진다.
        /// </summary>
        static readonly Color BarFill = new Color(0.60f, 0.86f, 0.88f, 1f);

        static readonly Color TitleColor = new Color(0.92f, 0.94f, 0.96f, 1f);
        static readonly Color BodyColor = new Color(0.78f, 0.82f, 0.86f, 1f);

        // ── 상태 ─────────────────────────────────────────────────

        GameObject _root;
        RectTransform _panel;
        TMP_Text _title;
        TMP_Text _body;
        RectTransform _barFill;
        GameObject _barRoot;

        float _hideAt;
        RadarScanState _shownState = RadarScanState.Idle;

        /// <summary>지금 화면에 서 있는가. 검증 하네스가 본다.</summary>
        public bool IsVisible => _root != null && _root.activeSelf;

        /// <summary>지금 큰 글자에 적힌 것. 검증 하네스가 읽는다.</summary>
        public string TitleText => _title != null ? _title.text : "";

        /// <summary>지금 본문에 적힌 것. 검증 하네스가 읽는다.</summary>
        public string BodyText => _body != null ? _body.text : "";

        void OnEnable()
        {
            _instance = this;
            Loc.LocaleChanged += Redraw;
        }

        void OnDisable()
        {
            Loc.LocaleChanged -= Redraw;
            if (_instance == this) _instance = null;
        }

        void Update() => Redraw();

        // ── 그리기 ───────────────────────────────────────────────

        void Redraw()
        {
            var radar = RadarController.Instance;
            var scan = radar != null ? radar.Scan : null;

            if (scan == null)
            {
                Hide();
                return;
            }

            switch (scan.State)
            {
                case RadarScanState.Scanning:
                    DrawScanning(scan);
                    break;

                case RadarScanState.Complete:
                    if (_shownState != RadarScanState.Complete)
                        _hideAt = Time.unscaledTime + HoldSeconds;
                    DrawResult(radar);
                    Expire(radar);
                    break;

                case RadarScanState.Cancelled:
                    if (_shownState != RadarScanState.Cancelled)
                        _hideAt = Time.unscaledTime + HoldSeconds;
                    DrawCancelled(scan);
                    Expire(radar);
                    break;

                default:
                    Hide();
                    break;
            }

            _shownState = scan.State;
        }

        void Expire(RadarController radar)
        {
            // 다음 관측을 걸 수 있게 스스로 치운다. 사람이 닫는 창이 아니다.
            if (Time.unscaledTime >= _hideAt) radar.Dismiss();
        }

        void DrawScanning(RadarScan scan)
        {
            Show();

            _title.text = Loc.T(RadarText.Category, "scanning");
            _body.text = Loc.F(RadarText.Category, "progress", Mathf.RoundToInt(scan.Progress * 100f));

            _barRoot.SetActive(true);
            _barFill.anchorMax = new Vector2(Mathf.Clamp01(scan.Progress), 1f);
        }

        void DrawResult(RadarController radar)
        {
            Show();

            _title.text = Loc.T(RadarText.Category, "result_title");
            _barRoot.SetActive(false);

            var readings = radar.Readings;
            if (readings == null || readings.Count == 0)
            {
                _body.text = Loc.T(RadarText.Category, "result_empty");
                return;
            }

            _lines.Clear();
            foreach (var c in readings) _lines.Add(RadarText.Reading(c));

            // 줄을 잇는 것은 문장을 조립하는 것이 아니다 — 각 줄이 이미 완성된 문장이고
            // 사이에 들어가는 것은 낱말이 아니라 줄바꿈이다.
            _body.text = string.Join("\n", _lines);
        }

        readonly List<string> _lines = new List<string>();

        void DrawCancelled(RadarScan scan)
        {
            Show();

            _title.text = RadarText.CancelLine(scan.CancelReason);
            _body.text = Loc.F(RadarText.Category, "spent", Mathf.RoundToInt(scan.Drawn));
            _barRoot.SetActive(false);
        }

        void Show()
        {
            if (_root == null) Build();
            if (!_root.activeSelf) _root.SetActive(true);
        }

        void Hide()
        {
            _shownState = RadarScanState.Idle;
            if (_root != null && _root.activeSelf) _root.SetActive(false);
        }

        // ── 화면 조립 ────────────────────────────────────────────

        void Build()
        {
            _root = new GameObject("RadarScanCanvas");
            _root.transform.SetParent(transform, false);

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // GraphicRaycaster를 달지 않는다. 눌리는 물건이 아니고, 달면 화면 가운데에서
            // 오는 클릭을 이 판이 삼킨다.

            var panelGo = new GameObject("Panel", typeof(RectTransform));
            panelGo.transform.SetParent(_root.transform, false);
            _panel = (RectTransform)panelGo.transform;
            _panel.anchorMin = new Vector2(0.5f, 0f);
            _panel.anchorMax = new Vector2(0.5f, 0f);
            _panel.pivot = new Vector2(0.5f, 0f);
            _panel.anchoredPosition = new Vector2(0f, PanelBottom);
            _panel.sizeDelta = new Vector2(PanelWidth, 0f);

            var frame = panelGo.AddComponent<Image>();
            UISkin.ApplyPanel(frame, PanelColor);
            frame.raycastTarget = false;

            var layout = panelGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 16, 16);
            layout.spacing = 10f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childAlignment = TextAnchor.UpperCenter;

            var fitter = panelGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _title = NewText("Title", panelGo.transform, 22f, TitleColor);
            _title.alignment = TextAlignmentOptions.Center;

            _barRoot = new GameObject("Bar", typeof(RectTransform));
            _barRoot.transform.SetParent(panelGo.transform, false);

            var back = _barRoot.AddComponent<Image>();
            back.color = BarBack;
            back.raycastTarget = false;

            var barElement = _barRoot.AddComponent<LayoutElement>();
            barElement.preferredHeight = BarHeight;
            barElement.minHeight = BarHeight;

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(_barRoot.transform, false);
            _barFill = (RectTransform)fillGo.transform;
            _barFill.anchorMin = Vector2.zero;
            _barFill.anchorMax = new Vector2(0f, 1f);
            _barFill.pivot = new Vector2(0f, 0.5f);
            _barFill.offsetMin = Vector2.zero;
            _barFill.offsetMax = Vector2.zero;

            var fill = fillGo.AddComponent<Image>();
            fill.color = BarFill;
            fill.raycastTarget = false;

            _body = NewText("Body", panelGo.transform, 19f, BodyColor);
            _body.alignment = TextAlignmentOptions.Center;

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
            text.raycastTarget = false;
            return text;
        }

        static TMP_FontAsset _font;

        /// <summary>
        /// 이미 화면에 쓰이고 있는 글꼴을 빌린다. TMP 기본 글꼴로 떨어지면 한글이
        /// 통째로 두부(네모)가 된다 — 획득 알림·도감과 같은 처방이다.
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
