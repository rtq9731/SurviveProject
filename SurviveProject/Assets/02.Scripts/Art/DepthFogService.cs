using UnityEngine;
using UnityEngine.Rendering;
using Survive.Core;
using Survive.Domain.Art;
using Survive.Player;
using Survive.Vitals;
using Survive.World;

namespace Survive.Art
{
    /// <summary>
    /// 안개를 매 프레임 갈아 끼운다 (백로그 40, 1순위).
    ///
    /// <b>재는 축이 둘이다</b> (상세기획서 §7.4, 2026-08-07 개정).
    /// <list type="bullet">
    /// <item><b>지상</b>(<see cref="DepthFog.AirLineY"/> 위) — <b>거리</b>다.
    ///   두꺼운 대기가 사이에 있으므로 멀리가 안 보이고, <b>시선을 올릴수록
    ///   대기를 짧게 지나 맑아진다.</b> 그래서 여기서 재어 넣는 것은 높이가
    ///   아니라 <b>카메라가 향한 고도</b>다</item>
    /// <item><b>지하</b>(그 아래) — <b>깊이</b>다. 예전 밴드 표 그대로다</item>
    /// </list>
    ///
    /// <b>왜 카메라 고도로 밀도를 바꾸는가.</b> Unity의 안개는 방향을 모른다 —
    /// 밀도 하나만 받는다. 높이에 따라 옅어지는 안개를 픽셀 단위로 그리려면
    /// 셰이더를 새로 써야 하는데, 그것은 마지막 수단이다. 대신 <b>지금 어디를
    /// 보고 있는가</b>로 밀도를 정하면 "위를 보면 맑고 수평을 보면 뿌옇다"가
    /// 공짜로 나온다. <b>대가는 한 화면 안에서 두 밀도가 동시에 보이지 않는
    /// 것</b>이고, 그것은 스카이박스가 서면 배경 쪽에서 해결된다
    /// (<see cref="DepthFog.SkyColor"/>).
    ///
    /// <b>안개가 승부처다.</b> 이 게임의 색은 후처리가 아니라 안개가 만든다 —
    /// 환경광이 0이라 화면 대부분은 광원이 닿지 않는 검정이고, 그 검정의
    /// 색조를 정하는 것이 <see cref="RenderSettings.fogColor"/> 하나다.
    /// "깊이가 곧 자홍의 농도"(상세기획서 §7.3)는 별도 시스템이 아니라
    /// 여기서 밴드를 보간하는 것으로 끝난다. 규칙은 <see cref="DepthFog"/>에 있고
    /// 이 컴포넌트는 높이를 재서 넣기만 한다.
    ///
    /// <b>왜 씬에 놓지 않고 스스로 붙는가.</b> <see cref="MacroniumSeaService"/>·
    /// <see cref="DeathDropService"/>와 같은 이유다 — MainScene은 병합할 수 없는
    /// 단일 파일이라 여러 갈래로 나뉘어 일하는 동안 손대지 않는다.
    ///
    /// <b>물속은 건드리지 않는다.</b> 머리가 잠긴 동안의 안개는
    /// <see cref="UnderwaterView"/>가 이미 소유하고 있다. 둘이 같은 프레임에
    /// 서로 다른 값을 쓰면 화면이 떨린다. 그래서 잠긴 동안은 여기서 손을 떼고,
    /// 나오면 다시 이어받는다(<see cref="UnderwaterView"/>가 되돌려 놓은
    /// 씬 기본값 위로 같은 프레임 LateUpdate에서 덮으므로 깜빡임이 없다).
    /// </summary>
    [DisallowMultipleComponent]
    public class DepthFogService : MonoBehaviour
    {
        static DepthFogService _instance;

        /// <summary>가장 최근에 깔린 안개. 검증이 값으로 집기 위한 것이다.</summary>
        public static Color LastColor { get; private set; }
        public static float LastDensity { get; private set; }
        public static float LastFarClip { get; private set; }

        /// <summary>가장 최근에 깔린 배경(하늘) 색.</summary>
        public static Color LastSkyColor { get; private set; }

        /// <summary>가장 최근에 읽은 시선 고도(도). 지상 규칙이 돌 때만 뜻이 있다.</summary>
        public static float LastElevation { get; private set; }

        /// <summary>가장 최근에 읽은 햇빛의 양(0~1).</summary>
        public static float LastDaylight { get; private set; }

        /// <summary>지금 지상 규칙(거리 축)으로 돌고 있는가. 거짓이면 깊이 축이다.</summary>
        public static bool LastOnSurface { get; private set; }

        /// <summary>
        /// 안개 밀도에 맞춰 카메라 원거리 평면을 줄일 것인가.
        /// 안개가 다 덮은 뒤를 그리는 것은 낭비지만, 잘라 낸 자리에 유도등이
        /// 있으면 게임이 망가진다 — <see cref="DepthFog.FarClipFor"/>의 바닥값이
        /// 그것을 막는다. 검증에서 끄고 비교할 수 있게 열어 둔다.
        /// </summary>
        public static bool TrimFarClip { get; set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_instance != null) return;

            var go = new GameObject("DepthFogService");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<DepthFogService>();
        }

        Transform _body;
        PlayerSwimming _swim;
        Camera _eye;
        float _sceneFar;

        // 원거리 평면은 LateUpdate에서 정할 수 없다 — CinemachineBrain이 매 프레임
        // 가상 카메라의 렌즈로 카메라를 통째로 덮어쓰기 때문에 여기서 넣은 값이
        // 그리기 전에 사라진다(실측: 250을 넣어도 3000으로 되돌아왔다).
        // 그리기 직전, 컬링 파라미터를 뽑기 전에 넣는다.
        void OnEnable() => RenderPipelineManager.beginCameraRendering += OnBeginCamera;

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            if (_eye != null && _sceneFar > 0f) _eye.farClipPlane = _sceneFar;
            _body = null;
            _swim = null;
        }

        void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != _eye || _eye == null || _sceneFar <= 0f) return;
            _eye.farClipPlane = LastFarClip > 0f ? LastFarClip : _sceneFar;
        }

        // 안개를 LateUpdate에서 쓴다. UnderwaterView가 Update에서 물 밖으로
        // 나올 때 씬 기본값을 되돌리는데, 그것을 같은 프레임에 덮어야
        // "수면 위로 나온 한 프레임만 색이 튀는" 일이 없다.
        void LateUpdate()
        {
            if (!Acquire()) return;

            // 잠긴 동안은 UnderwaterView의 것이다.
            if (_swim != null && _swim.IsHeadSubmerged) return;

            Color color, sky;
            float density;

            // <b>축을 가르는 것은 눈의 높이다.</b> 발이 호수에 잠겼다고 시야가
            // 물속이 되지는 않는다 — 무릎까지 담그고 서 있는 사람이 보는 것은
            // 여전히 공기 너머다. 카메라가 액면 아래로 내려가는 순간이 곧
            // 머리가 잠기는 순간이고, 그때는 UnderwaterView가 이미 이어받는다.
            //
            // 깊이 밴드가 읽는 값은 그대로 <b>몸의 높이</b>다. 밴드 표가 몸
            // 높이로 짜여 있고, 그 표는 이 라운드에서 한 값도 움직이지 않는다.
            float eyeY = _eye != null ? _eye.transform.position.y : _body.position.y;
            bool onSurface = eyeY >= DepthFog.AirLineY;
            LastOnSurface = onSurface;

            if (onSurface)
            {
                float elevation = ViewElevationDegrees();
                float daylight = DaylightNow();

                DepthFog.SampleSurface(elevation, daylight, out color, out density);
                sky = DepthFog.SkyColor(elevation, daylight);

                LastElevation = elevation;
                LastDaylight = daylight;
            }
            else
            {
                DepthFog.Sample(_body.position.y, out color, out density);
                // 깊이 축에서는 배경도 안개색 그대로다 — 지하에 하늘은 없다.
                sky = color;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = color;
            RenderSettings.fogDensity = density;

            LastColor = color;
            LastDensity = density;
            LastSkyColor = sky;

            if (_eye == null) return;

            // 배경색이 곧 하늘이다. 원거리 평면 밖은 이 색으로 지워지므로
            // 잘라 낸 자리가 "대기 저편"으로 자연스럽게 읽힌다.
            if (_eye.clearFlags == CameraClearFlags.SolidColor) _eye.backgroundColor = sky;

            LastFarClip = TrimFarClip ? DepthFog.FarClipFor(density, _sceneFar) : _sceneFar;
        }

        /// <summary>
        /// 카메라가 향한 고도(도). 수평이 0, 천정이 90이다.
        ///
        /// <b>플레이어의 몸이 아니라 카메라를 읽는다.</b> 재려는 것이 "어느 방향으로
        /// 대기를 지나는가"이므로 기준은 실제로 그려지는 시선이다 — 몸의 방향으로
        /// 재면 위를 올려다봐도 안개가 그대로다.
        /// </summary>
        float ViewElevationDegrees()
        {
            if (_eye == null) return 0f;
            return Mathf.Asin(Mathf.Clamp(_eye.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// 지금 햇빛의 양(0~1). 시계가 아직 안 섰으면 낮으로 본다 —
        /// 안개가 시계보다 먼저 도는 프레임에서 화면이 한 번 캄캄해지지 않게.
        /// </summary>
        static float DaylightNow()
        {
            var clock = DayNightService.Instance;
            return clock == null ? 1f : DayNightCycle.Daylight(clock.TimeOfDay);
        }

        bool Acquire()
        {
            if (_eye == null || !_eye.isActiveAndEnabled)
            {
                _eye = Camera.main;
                if (_eye != null) _sceneFar = Mathf.Max(_sceneFar, _eye.farClipPlane);
            }

            if (_body != null) return true;

            if (!GameServices.TryGet<PlayerVitals>(out var vitals) || vitals == null) return false;

            _body = vitals.GetComponentInParent<PlayerContext>()?.transform ?? vitals.transform;
            _swim = _body.GetComponentInChildren<PlayerSwimming>(true);
            return true;
        }
    }
}
