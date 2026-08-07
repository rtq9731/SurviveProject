using UnityEngine;
using UnityEngine.SceneManagement;
using Survive.Core;

namespace Survive.World
{
    /// <summary>
    /// <b>하루를 굴리고 그것을 화면에 옮기는 몸.</b> 판단은 여기 없다 —
    /// 시각이 어떤 밝기이고 어떤 색인지는 <see cref="DayNightCycle"/>이 Unity 없이 답하고,
    /// 이 컴포넌트는 <b>그 값을 태양과 환경광에 넣는 일</b>만 한다.
    ///
    /// <b>씬을 고치지 않는다.</b> MainScene은 병합할 수 없는 단일 파일이라 여러 갈래가
    /// 동시에 손대면 한쪽을 버려야 한다. <see cref="GlowGroveService"/>·
    /// <see cref="LiquidContactService"/>와 같은 방식으로 실행 시점에 스스로 붙는다.
    ///
    /// <b>태양은 새로 만들지 않고 씬에 있던 것을 쓴다.</b> MainScene에는 꺼진 채로
    /// Directional Light가 하나 놓여 있다 — 그림자 설정과 방위가 이미 손으로 잡혀
    /// 있으므로, 옆에 하나 더 만들면 그 손질이 버려지고 방향광이 둘이 된다.
    /// 없는 씬에서는 그때 만든다.
    ///
    /// <b>포그는 건드리지 않는다.</b> 지금 포그의 주인은 둘이다 —
    /// <see cref="Survive.Art.DepthFogService"/>(지상은 거리, 지하는 깊이)와
    /// <c>UnderwaterView</c>(물속). 여기서 매 프레임 색을 덮으면 물에 들어간 순간
    /// 두 주인이 같은 값을 놓고 다투고, 물속이 물속으로 안 보이게 된다.
    ///
    /// <b>밤이 보라색으로 짙어지는 것은 둘이 함께 말한다.</b> 환경광은 여기서
    /// (<see cref="DayNightCycle.AmbientAt"/>), 지평선의 안개색은 저쪽에서
    /// (<see cref="Survive.Domain.Art.DepthFog.HorizonColor"/>) — 저쪽이 <b>이 시계를
    /// 읽어 가는</b> 방향이므로 주인은 여전히 하나다.
    ///
    /// <b>밝은 구역은 여기서 아무 영향도 받지 않는다.</b> 밤이 온다는 것은 세계 전체가
    /// 어두워진다는 뜻이지 화톳불이 꺼진다는 뜻이 아니다. 그래서 이 파일에는
    /// <see cref="LitZoneRegistry"/>를 부르는 줄이 하나도 없다.
    /// </summary>
    [DisallowMultipleComponent]
    public class DayNightService : MonoBehaviour, ISaveable
    {
        /// <summary>
        /// 게임을 시작하는 시각. <b>아침이다</b> — 착륙한 사람이 첫 화면에서
        /// 주변을 볼 수 있어야 하고, 그러고도 밤까지 준비할 시간이 남아야 한다.
        /// <b>임시값</b>이고 하루 길이와 함께 사람이 정한다.
        /// </summary>
        public const float StartTimeOfDay = 0.35f;

        /// <summary>저장본에서 이 항목을 찾는 열쇠.</summary>
        public const string Key = "dayNight";

        public static DayNightService Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Instance != null) return;

            var go = new GameObject("DayNightService");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<DayNightService>();
        }

        Light _sun;
        bool _sunWasActive;
        bool _sunWasEnabled;
        float _sunIntensityBefore;
        Color _sunColorBefore;
        Quaternion _sunRotationBefore;
        Color _ambientBefore;
        bool _captured;

        /// <summary>
        /// 시계를 세운다. 검증이 한 시각을 붙들고 픽셀을 재는 동안
        /// 시간이 흐르면 두 장이 서로 다른 시각의 값이 된다.
        /// </summary>
        public bool Frozen { get; set; }

        /// <summary>
        /// 흐른 초. 하루를 넘어도 계속 자란다.
        ///
        /// <b>값은 여기 없다.</b> 세계 시각의 주인은 <see cref="WorldClock"/>이고
        /// 이 컴포넌트는 그것을 <b>모는 몸</b>이다 — 재생 시각과 지핀 시각도 같은
        /// 시계를 보므로, 시계가 둘로 갈리면 「저장을 건너서도 도는 재생」이 성립하지 않는다.
        /// </summary>
        public double Seconds => WorldClock.Now;

        /// <summary>지금 시각(0~1). 0이 자정이다.</summary>
        public float TimeOfDay => DayNightCycle.Wrap(WorldClock.Now);

        /// <summary>지금 국면.</summary>
        public DayPhase Phase => DayNightCycle.PhaseAt(TimeOfDay);

        /// <summary>지금이 밤인가. 「낫은 밤에 다닌다」가 물어볼 창구다.</summary>
        public bool IsNight => DayNightCycle.IsNight(TimeOfDay);

        /// <summary>지금 태양. 씬에 방향광이 하나도 없었으면 이쪽이 만든 것이다.</summary>
        public Light Sun => _sun;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // 아침에서 시작하는 것은 <b>처음 한 번</b>뿐이다. 컴포넌트가 다시
            // 깨어날 때마다 시각을 앉히면 이미 흐르던 세계가 아침으로 되감기고,
            // 그러면 다 캔 시각과 불 지핀 시각이 전부 미래가 되어 영영 안 돌아온다.
            WorldClock.StartAt(DayNightCycle.SecondsAt(StartTimeOfDay));

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            AcquireSun();
            Apply();
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Restore();
            if (Instance == this) Instance = null;
        }

        // 씬을 갈아 끼우면 앞 씬의 태양은 사라진다. 새 씬에서 다시 찾는다.
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _captured = false;
            _sun = null;
            AcquireSun();
            Apply();
        }

        /// <summary>
        /// 세계를 굴린다.
        ///
        /// <b>배속을 태우지 않는다 — <c>deltaTime</c>이 아니라 <c>unscaledDeltaTime</c>이다.</b>
        /// 배속은 검증이 기다리기 싫을 때 돌리는 <b>화면 쪽 손잡이</b>이지 세계가
        /// 빨라진 것이 아니다. 그것이 세계 시각에 곱해지면 재생 주기와 하루 길이가
        /// 검증 설정에 따라 달라지고, 재는 것이 게임의 값이 아니게 된다.
        ///
        /// <b>다만 완전히 멈춘 것은 존중한다.</b> 일시정지(<c>timeScale = 0</c>)는
        /// 「빠르게/느리게」가 아니라 <b>세계가 섰다</b>는 뜻이다. 그때까지 재생이
        /// 돌면 메뉴를 열어 둔 채 자리를 뜨는 것이 자원 전략이 된다.
        ///
        /// <b>화면 연출은 반대쪽에 남는다.</b> 픽업 피드·레이더 숨김·오디오
        /// 스로틀이 일시정지 중에도 흐르는 것은 <b>의도다</b> — 그것들은 세계의
        /// 상태가 아니라 화면이 사람에게 말을 거는 중인 것이고, 정지 화면 위에서
        /// 반쯤 사라지다 만 줄이 얼어붙어 있으면 고장으로 읽힌다.
        /// </summary>
        void Update()
        {
            WorldClock.Paused = Frozen || Time.timeScale <= 0f;
            WorldClock.Advance(Time.unscaledDeltaTime);
            Apply();
        }

        // ── 시간을 옮기는 창구 ───────────────────────────────────
        //
        // 셋 다 <b>같은 한 값</b>(WorldClock)만 움직인다. 시각을 따로 들고 있는
        // 자리를 하나라도 두면 되감기와 건너뛰기가 서로 다른 답을 내게 된다.

        /// <summary>
        /// 시각을 이 값으로 옮긴다. <b>되감지 않는다</b> — 지나간 시각을 달라고 하면
        /// <b>다음 날</b>의 같은 자리로 간다.
        ///
        /// 되감기를 막는 이유: 다 캔 시각과 불 지핀 시각이 같은 시계로 적혀 있어,
        /// 시계를 뒤로 돌리면 그 자리들이 <b>미래에 일어난 일</b>이 되어 영영
        /// 돌아오지 않는다. 하루를 앞뒤로 훑는 검증은 「다음 날 같은 시각」으로도
        /// 똑같이 성립한다 — 재는 것은 날짜가 아니라 시각이기 때문이다.
        /// </summary>
        public void SetTimeOfDay(float timeOfDay)
        {
            WorldClock.ForwardTo(DayNightCycle.SecondsAt(timeOfDay), DayNightCycle.DayLengthSeconds);
            Apply();
        }

        /// <summary>이만큼 흘려보낸다. 음수는 무시한다(되감지 않는다).</summary>
        public void Skip(double seconds)
        {
            WorldClock.Skip(seconds);
            Apply();
        }

        // ── 화면에 넣기 ──────────────────────────────────────────

        /// <summary>지금 시각의 값을 태양과 환경광에 넣는다.</summary>
        public void Apply()
        {
            float t = TimeOfDay;

            RenderSettings.ambientLight = DayNightCycle.AmbientAt(t);

            if (_sun == null) return;

            float intensity = DayNightCycle.SunIntensity(t);

            // 세기 0인 방향광을 켜 둘 이유가 없다. 밤에는 통째로 끈다 —
            // 0으로만 두면 URP가 여전히 그림자 패스를 돌린다.
            bool lit = intensity > 0.0001f;
            if (_sun.gameObject.activeSelf != lit) _sun.gameObject.SetActive(lit);
            _sun.enabled = lit;
            if (!lit) return;

            _sun.intensity = intensity;
            _sun.color = DayNightCycle.SunColor;

            // 방위는 씬이 잡아 둔 것을 그대로 쓰고 고도만 시각이 정한다.
            // 방위까지 돌리면 손으로 맞춰 둔 그림자 방향이 버려진다.
            var e = _sunRotationBefore.eulerAngles;
            _sun.transform.rotation = Quaternion.Euler(DayNightCycle.SunPitchDegrees(t), e.y, e.z);
        }

        /// <summary>
        /// 씬의 방향광을 찾아 태양으로 삼는다. 꺼져 있어도 찾는다 —
        /// MainScene의 것이 실제로 꺼진 채 놓여 있다.
        /// </summary>
        void AcquireSun()
        {
            var lights = FindObjectsByType<Light>(FindObjectsInactive.Include);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null || lights[i].type != LightType.Directional) continue;
                _sun = lights[i];
                break;
            }

            if (_sun == null)
            {
                var go = new GameObject("Sun");
                go.transform.SetParent(transform, false);
                _sun = go.AddComponent<Light>();
                _sun.type = LightType.Directional;
                _sun.shadows = LightShadows.Soft;
                _sun.transform.rotation = Quaternion.Euler(50f, 35f, 0f);
            }

            Capture();
        }

        void Capture()
        {
            if (_captured) return;
            _captured = true;

            _ambientBefore = RenderSettings.ambientLight;
            _sunWasActive = _sun.gameObject.activeSelf;
            _sunWasEnabled = _sun.enabled;
            _sunIntensityBefore = _sun.intensity;
            _sunColorBefore = _sun.color;
            _sunRotationBefore = _sun.transform.rotation;
        }

        /// <summary>
        /// 손댄 것을 되돌린다. 재생을 끄면 씬이 다시 읽히므로 대개 필요 없지만,
        /// 에디터에서 이 컴포넌트만 지웠을 때 세계가 낮에 얼어붙지 않게 한다.
        /// </summary>
        void Restore()
        {
            if (!_captured) return;

            RenderSettings.ambientLight = _ambientBefore;
            if (_sun == null) return;

            _sun.gameObject.SetActive(_sunWasActive);
            _sun.enabled = _sunWasEnabled;
            _sun.intensity = _sunIntensityBefore;
            _sun.color = _sunColorBefore;
            _sun.transform.rotation = _sunRotationBefore;
        }

        // ── 저장 ────────────────────────────────────────────────
        //
        // <b>들어간다.</b> 「낫은 밤에 다닌다」가 서고 나면 시각은 세계의 상태이지
        // 화면 설정이 아니다 — 밤에 저장하고 불러왔더니 아침이면 그때까지 감수한
        // 위험이 통째로 사라지고, 반대로 낮에 저장하고 불러왔더니 밤이면 준비 없이
        // 밤에 던져진다. 수분·식량의 감소 속도를 「원정 한 번의 길이」로 재는 것도
        // 시각이 이어질 때만 성립한다.
        //
        // <b>세이브 포맷은 움직이지 않는다.</b> 저장본은 열쇠-값 목록이고
        // (<c>SaveSnapshot</c>) 불러올 때 모르는 열쇠는 건너뛴다. 그래서 항목을
        // 하나 더하는 것은 <b>덧붙임</b>이지 형식 변경이 아니다 — 옛 저장본은
        // 이 열쇠가 없는 채로 그냥 열리고, 그때는 시작 시각으로 남는다.

        [System.Serializable]
        public class State
        {
            public float timeOfDay;
            public int daysElapsed;
        }

        public string SaveKey => Key;

        public object CaptureState() => new State
        {
            timeOfDay = TimeOfDay,
            daysElapsed = Mathf.FloorToInt((float)(WorldClock.Now / DayNightCycle.DayLengthSeconds)),
        };

        /// <summary>
        /// <b>세계 시각을 저장본에서 되돌리는 자리는 여기 하나다.</b> 세계 원장은
        /// 이 시계의 초로 시각을 적을 뿐 시계를 소유하지 않는다 — 시계를 저장하는
        /// 곳이 둘이면 두 값이 어긋날 수 있고, 어긋나는 순간 재생이 미래에서 멈춘다.
        /// </summary>
        public void RestoreState(object state)
        {
            if (state is not State s) return;
            WorldClock.Restore(s.daysElapsed * (double)DayNightCycle.DayLengthSeconds +
                               DayNightCycle.SecondsAt(s.timeOfDay));
            Apply();
        }
    }
}
