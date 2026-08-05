using UnityEngine;
using Survive.Core;
using Survive.Domain.Art;
using Survive.Player;
using Survive.Vitals;

namespace Survive.Art
{
    /// <summary>
    /// 깊이에 따라 안개를 갈아 끼운다 (백로그 40, 1순위).
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

        void OnDisable()
        {
            if (_eye != null && _sceneFar > 0f) _eye.farClipPlane = _sceneFar;
            _body = null;
            _swim = null;
        }

        // 안개를 LateUpdate에서 쓴다. UnderwaterView가 Update에서 물 밖으로
        // 나올 때 씬 기본값을 되돌리는데, 그것을 같은 프레임에 덮어야
        // "수면 위로 나온 한 프레임만 색이 튀는" 일이 없다.
        void LateUpdate()
        {
            if (!Acquire()) return;

            // 잠긴 동안은 UnderwaterView의 것이다.
            if (_swim != null && _swim.IsHeadSubmerged) return;

            DepthFog.Sample(_body.position.y, out var color, out float density);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = color;
            RenderSettings.fogDensity = density;

            LastColor = color;
            LastDensity = density;

            if (_eye == null) return;

            // 배경색을 안개색과 맞춘다. 원거리 평면 밖은 이 색으로 지워지므로
            // 잘라 낸 자리가 "안개 저편"으로 자연스럽게 읽힌다.
            if (_eye.clearFlags == CameraClearFlags.SolidColor) _eye.backgroundColor = color;

            float far = TrimFarClip ? DepthFog.FarClipFor(density, _sceneFar) : _sceneFar;
            _eye.farClipPlane = far;
            LastFarClip = far;
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
