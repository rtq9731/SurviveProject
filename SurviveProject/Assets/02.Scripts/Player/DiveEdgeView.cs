using UnityEngine;
using UnityEngine.UI;
using Survive.Core;
using Survive.Vitals;
using Survive.World;

namespace Survive.Player
{
    /// <summary>
    /// 잠수 중 화면 가장자리가 조여든다 (실행 스펙 §8-1).
    ///
    /// <b>왜 이것이 필요한가.</b> 잠수 구간에서 <b>산소는 가짜 압박</b>이다 —
    /// 통로 안 사망은 사실상 일어나지 않게 잡아 두었으므로, 숫자만 두면
    /// 게이지가 줄어드는 것을 보고도 아무 일이 없다는 것을 두 번째 잠수에
    /// 배워 버린다. 그러면 남은 것은 지루한 이동뿐이다. <b>압박은 수치가 아니라
    /// 연출이 진다</b>(기획서 갱신점 _3 §2) — 게이지 속도, 화면 가장자리,
    /// 심박음, 숨소리. 이 컴포넌트가 그중 화면 몫을 맡는다.
    ///
    /// <b>어둠을 깨지 않는다.</b> 내는 것은 검은 테두리뿐이고 세계에 빛을 더하지
    /// 않는다. 오히려 시야를 좁히므로 어둠이라는 축과 같은 방향이다.
    /// <c>artViolations</c>가 세는 광원·발광은 하나도 늘지 않는다.
    ///
    /// <b>소리는 아직 없다.</b> 이 저장소에 클립이 하나도 없어
    /// (<c>AudioCueSO</c>) 심박은 <b>맥동의 리듬</b>으로만 보인다.
    /// 귀 쪽 자리는 <c>AudioCueBookSO.diveHeartbeat</c>·<c>diveBreath</c>에
    /// 비워 두었고, 간격은 이미 <see cref="DiveRule.BeatSeconds"/>가 답한다 —
    /// 클립이 꽂히면 눈과 귀가 같은 박자로 뛴다.
    ///
    /// <b>규칙은 여기 없다.</b> 얼마나 조이는가도 얼마나 빨리 뛰는가도
    /// <see cref="DiveRule"/>이 답한다. 여기 남는 것은 그 값을 화면에 바르는 일뿐이다.
    ///
    /// <b>씬을 고치지 않는다.</b> 스스로 선다 — <c>RadarScanView</c>와 같은 이유다.
    /// </summary>
    [DisallowMultipleComponent]
    public class DiveEdgeView : MonoBehaviour
    {
        /// <summary>획득 알림(2500) 아래. 소지품·도감을 열면 그 위로 덮이지 않는다.</summary>
        public const int SortingOrder = 2400;

        /// <summary>맥동이 어둠에 더하는 폭. 조여든 정도에 비례하므로 얕을 때는 티가 안 난다.</summary>
        const float PulseAmount = 0.18f;

        /// <summary>가장자리 그림의 한 변(px). 늘려 쓰므로 클 이유가 없다.</summary>
        const int MaskSize = 64;

        static DiveEdgeView _instance;
        public static DiveEdgeView Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_instance != null) return;

            var go = new GameObject("DiveEdgeView");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<DiveEdgeView>();
        }

        PlayerVitals _vitals;
        SubmergedOxygenDrain _drain;

        GameObject _root;
        Image _edge;
        float _beatPhase;

        /// <summary>지금 화면에 서 있는가. 검증 하네스가 본다.</summary>
        public bool IsVisible => _root != null && _root.activeSelf;

        /// <summary>지금 가장자리가 얼마나 조여 있는가(0~1). 검증 하네스가 읽는다.</summary>
        public float Darkness => _edge != null ? _edge.color.a : 0f;

        void OnEnable() => _instance = this;

        void OnDisable()
        {
            if (_instance == this) _instance = null;
        }

        void Update()
        {
            if (!Acquire() || !_drain.IsSubmerged)
            {
                Hide();
                return;
            }

            float o = Mathf.Clamp01(_vitals.Oxygen.Normalized);
            float baseDarkness = DiveRule.EdgeDarkness(o);
            if (baseDarkness <= 0f)
            {
                Hide();
                return;
            }

            // 심박. 간격이 짧아지는 것 자체가 정보라, 세기가 아니라 <b>빈도</b>를 움직인다.
            float beat = Mathf.Max(0.05f, DiveRule.BeatSeconds(o));
            _beatPhase += Time.deltaTime / beat;
            if (_beatPhase > 1f) _beatPhase -= Mathf.Floor(_beatPhase);

            // 0에서 부풀었다 가라앉는다. 사인의 양수 반쪽만 써서 "쿵" 하고 한 번씩 온다.
            float pulse = Mathf.Max(0f, Mathf.Sin(_beatPhase * Mathf.PI * 2f));
            float darkness = Mathf.Clamp01(baseDarkness * (1f + PulseAmount * pulse));

            Show(darkness);
        }

        bool Acquire()
        {
            if (_vitals != null && _drain != null) return true;

            if (!GameServices.TryGet<PlayerVitals>(out var found) || found == null)
            {
                _vitals = null;
                _drain = null;
                return false;
            }

            _vitals = found;
            _drain = found.GetComponentInChildren<SubmergedOxygenDrain>(true);
            if (_drain == null)
            {
                var body = found.GetComponentInParent<PlayerContext>();
                if (body != null) _drain = body.GetComponentInChildren<SubmergedOxygenDrain>(true);
            }
            return _drain != null;
        }

        void Show(float darkness)
        {
            EnsureBuilt();
            if (!_root.activeSelf) _root.SetActive(true);

            var c = _edge.color;
            c.a = darkness;
            _edge.color = c;
        }

        void Hide()
        {
            _beatPhase = 0f;
            if (_root != null && _root.activeSelf) _root.SetActive(false);
        }

        void EnsureBuilt()
        {
            if (_root != null) return;

            _root = new GameObject("DiveEdge");
            _root.transform.SetParent(transform, false);

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var imageGo = new GameObject("Edge");
            imageGo.transform.SetParent(_root.transform, false);

            var rect = imageGo.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _edge = imageGo.AddComponent<Image>();
            _edge.sprite = BuildEdgeSprite();
            _edge.type = Image.Type.Simple;
            _edge.color = new Color(0f, 0f, 0f, 0f);

            // 화면 위의 판이지 조작 대상이 아니다. 레이를 먹으면 그 아래 버튼이 안 눌린다.
            _edge.raycastTarget = false;
        }

        /// <summary>
        /// 가운데가 비고 가장자리로 갈수록 짙어지는 판. 텍스처 에셋을 새로 두지 않고
        /// 코드로 굽는다 — 64px짜리 흐린 그림 하나 때문에 아트 파이프라인을 늘릴 이유가 없다.
        /// </summary>
        static Sprite BuildEdgeSprite()
        {
            var tex = new Texture2D(MaskSize, MaskSize, TextureFormat.Alpha8, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[MaskSize * MaskSize];
            float half = (MaskSize - 1) * 0.5f;

            for (int y = 0; y < MaskSize; y++)
            {
                for (int x = 0; x < MaskSize; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;

                    // 가운데에서 모서리까지를 0~1로. 세제곱을 씌워 가운데를 넓게 비운다 —
                    // 가운데가 흐리면 조여드는 것이 아니라 화면이 그냥 어두워진 것으로 보인다.
                    float r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 1.41421356f);
                    float a = Mathf.Clamp01(r * r * r);
                    pixels[y * MaskSize + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            var sprite = Sprite.Create(tex, new Rect(0f, 0f, MaskSize, MaskSize), new Vector2(0.5f, 0.5f));
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
