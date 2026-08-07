using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Survive.Core;
using Survive.Creatures;
using Survive.Domain.Art;
using Survive.Player;
using Survive.Vitals;
using Survive.World;

namespace Survive.Art
{
    /// <summary>
    /// 상황에 따라 움직이는 후처리 (백로그 40).
    ///
    /// <b>고정 등급은 여기에 없다.</b> 톤매핑·Bloom·색보정·Shadows/Midtones/Highlights
    /// 처럼 항상 같은 값인 것은 씬의 전역 볼륨(<c>Volume_Islands</c> 프로파일)에 있다.
    /// 이 서비스는 그 위에 얹히는 <b>상태 의존분</b>만 소유한다 — 비네트·필름 그레인·
    /// 색수차·감마 보정. 판단은 <see cref="PostFxGrade"/>가 하고 여기는 재서 넣는다.
    ///
    /// <b>씬을 고치지 않는다.</b> MainScene의 카메라는 후처리가 꺼져 있고
    /// (<c>m_RenderPostProcessing: 0</c>) 씬 파일은 병합할 수 없어 손대지 않기로 되어 있다.
    /// 그래서 카메라의 후처리·디더링을 런타임에 켜고, 볼륨도 런타임에 세운다.
    /// <see cref="LiquidContactService"/>가 스스로 붙는 것과 같은 방식이다.
    ///
    /// <b>디더링을 켜는 이유.</b> 거의 검정인 화면에서 8비트 출력은 계단(밴딩)을
    /// 만든다. 디더링과 아주 약한 필름 그레인이 그 계단을 부순다 — 이것이
    /// 이 게임에서 그레인을 쓰는 유일한 이유다(연출이 아니다).
    ///
    /// <b>넣지 않는 것.</b> DoF·모션블러(상세기획서 §7.4), 그리고 상시 색수차.
    /// </summary>
    [DisallowMultipleComponent]
    public class PostFxService : MonoBehaviour
    {
        /// <summary>낫이 이 거리 안에 들어오면 그레인이 오르기 시작한다.</summary>
        public const float ReaperRange = 22f;

        /// <summary>액면 위 이 높이부터 붉은 기가 돌기 시작한다.</summary>
        public const float MacroniumRange = 7f;

        /// <summary>피격 잔향이 사라지는 데 걸리는 시간(초). 짧고 약하게.</summary>
        public const float HurtDecaySeconds = 0.35f;

        /// <summary>상태 변화가 화면에 도달하는 속도. 경계에서 툭 튀지 않게 한다.</summary>
        public const float BlendPerSecond = 3.5f;

        static PostFxService _instance;

        public static PostFxService Instance => _instance;

        /// <summary>가장 최근에 볼륨에 넣은 값. 검증이 눈이 아니라 값으로 보기 위한 것이다.</summary>
        public static PostFxLook LastLook { get; private set; }

        /// <summary>가장 최근에 읽은 상태.</summary>
        public static PostFxState LastState { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_instance != null) return;

            LastLook = PostFxGrade.Evaluate(PostFxState.Default);
            LastState = PostFxState.Default;

            var go = new GameObject("PostFxService");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PostFxService>();
        }

        Volume _volume;
        VolumeProfile _profile;
        Vignette _vignette;
        FilmGrain _grain;
        ChromaticAberration _chromatic;
        ColorAdjustments _grade;

        Camera _eye;
        Transform _body;
        PlayerVitals _vitals;
        LanternController _lantern;

        float _hurt;
        float _hurtBearing;
        float _macronium;
        float _reaper;
        float _nextCreatureScan;
        CreatureBrain[] _creatures = new CreatureBrain[0];

        void Awake()
        {
            BuildVolume();
            GammaSettings.Changed += OnGammaChanged;
        }

        void OnDestroy()
        {
            GammaSettings.Changed -= OnGammaChanged;
            if (_profile != null) Destroy(_profile);
        }

        void OnGammaChanged(float _) { /* 다음 프레임 Apply가 읽어 간다 */ }

        /// <summary>
        /// 상태 의존분만 담은 런타임 볼륨. 우선순위를 씬 볼륨보다 높게 두어
        /// 여기서 켠 것만 덮어쓰고, 손대지 않은 항목은 씬 볼륨의 값이 그대로 산다.
        ///
        /// <b><c>Add&lt;T&gt;(false)</c>여야 한다.</b> 인자를 true로 주면 그 컴포넌트의
        /// <i>모든</i> 파라미터에 override가 켜진다. 우선순위가 높은 이 볼륨이
        /// 손댈 생각도 없던 값까지 기본값으로 덮어써서, 씬 볼륨의 고정 그레이드가
        /// 런타임에 지워진다. 실제로 그랬다 — 노출만 얹으려던 ColorAdjustments가
        /// 대비 14와 채도 -8을 0으로 되돌려, 화면 평균 밝기가 0.079에서 0.097로
        /// 들렸다(암부 검증에서 발각). 필요한 항목만 아래에서 하나씩 켠다.
        /// </summary>
        void BuildVolume()
        {
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "Volume_PostFxRuntime";

            _vignette = _profile.Add<Vignette>(false);
            _vignette.intensity.overrideState = true;
            _vignette.smoothness.overrideState = true;
            _vignette.smoothness.value = 0.4f;
            // 중심을 옮겨 피격 방향을 말한다. 밝히는 것이 아니라 가리는 것이라
            // 어둠을 깨지 않는다 (PostFxGrade의 "피격 방향 신호").
            _vignette.center.overrideState = true;

            _grain = _profile.Add<FilmGrain>(false);
            _grain.type.overrideState = true;
            _grain.type.value = FilmGrainLookup.Thin1;
            _grain.intensity.overrideState = true;
            _grain.response.overrideState = true;
            // 응답을 높게 둔다 — 밝은 곳일수록 그레인을 덜 넣는다는 뜻이다.
            // 어두운 픽셀을 들어올리지 않으려면 반대로 가야 할 것 같지만,
            // 실제로 밴딩이 생기는 곳이 어두운 계조 쪽이라 여기에 남겨야 한다.
            _grain.response.value = 0.8f;

            _chromatic = _profile.Add<ChromaticAberration>(false);
            _chromatic.intensity.overrideState = true;
            _chromatic.intensity.value = 0f;

            _grade = _profile.Add<ColorAdjustments>(false);
            _grade.postExposure.overrideState = true;
            _grade.colorFilter.overrideState = true;

            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 100f;
            _volume.weight = 1f;
            _volume.sharedProfile = _profile;
        }

        void LateUpdate()
        {
            EnsureCamera();
            Acquire();

            float dt = Time.unscaledDeltaTime;
            _hurt = HurtDecaySeconds <= 0f ? 0f : Mathf.Max(0f, _hurt - dt / HurtDecaySeconds);

            float blend = Mathf.Clamp01(dt * BlendPerSecond);
            _macronium = Mathf.Lerp(_macronium, ReadMacronium(), blend);
            _reaper = Mathf.Lerp(_reaper, ReadReaper(), blend);

            var state = new PostFxState(
                _lantern != null && _lantern.IsOn,
                _body != null && LitZoneRegistry.IsLit(_body.position),
                _macronium, _reaper, _hurt, GammaSettings.Value, _hurtBearing);

            LastState = state;
            Apply(PostFxGrade.Evaluate(state));
        }

        void Apply(in PostFxLook look)
        {
            LastLook = look;
            if (_vignette == null) return;

            _vignette.intensity.value = look.Vignette;
            _vignette.center.value = new Vector2(0.5f, 0.5f) + look.VignetteCenterOffset;
            _grain.intensity.value = look.FilmGrain;
            _chromatic.intensity.value = look.ChromaticAberration;
            _grade.postExposure.value = look.PostExposure;
            _grade.colorFilter.value = look.ColorFilter;
        }

        /// <summary>
        /// 카메라에 후처리와 디더링을 켠다. 씬 파일을 고치지 않기 때문에
        /// 매번 확인한다 — 씬을 다시 올리면 카메라가 새로 생긴다.
        /// </summary>
        void EnsureCamera()
        {
            if (_eye != null && _eye.isActiveAndEnabled) return;

            _eye = Camera.main;
            if (_eye == null) return;

            var data = _eye.GetUniversalAdditionalCameraData();
            if (data == null) return;

            data.renderPostProcessing = true;
            data.dithering = true;
        }

        void Acquire()
        {
            if (_lantern == null) GameServices.TryGet(out _lantern);

            if (_vitals != null && _body != null) return;

            if (!GameServices.TryGet<PlayerVitals>(out var found) || found == null) return;

            if (_vitals != null) _vitals.Health.Changed -= OnHealthChanged;
            _vitals = found;
            _vitals.Health.Changed += OnHealthChanged;
            _lastHealth = _vitals.Health.Current;
            _body = found.GetComponentInParent<PlayerContext>()?.transform ?? found.transform;
        }

        float _lastHealth;

        void OnHealthChanged(float current, float max)
        {
            if (current < _lastHealth - 0.001f)
            {
                _hurt = 1f;
                // 방향을 말해 주는 쪽이 없으면 정면으로 둔다. 추락·질식처럼
                // 돌아설 방향이 없는 피해가 여기 걸린다.
                _hurtBearing = Survive.Combat.HurtBearing.Unknown;
            }
            _lastHealth = current;
        }

        /// <summary>
        /// 때린 것이 <b>어디 있었는지</b>를 화면에 알린다.
        ///
        /// <see cref="OnHealthChanged"/>가 이미 잔향을 세우지만 그쪽은 방향을 모른다 —
        /// 체력이 줄었다는 사실만 온다. 가해자를 아는 쪽(<c>PlayerDamageReceiver</c>)이
        /// 곧이어 이것을 불러 방향을 얹는다. <b>순서가 중요하다</b>: 체력을 깎은
        /// <i>다음에</i> 불러야 하고, 그래야 잔향이 방금 세워진 그 프레임의 방향이 된다.
        ///
        /// 등 뒤 사각이 생긴 뒤로 이 신호가 없으면 플레이어는 무엇에 맞았는지도
        /// 어느 쪽으로 돌아야 하는지도 모른다 (기획서 §9).
        /// </summary>
        public static void ReportHurtFrom(Vector3 sourcePosition)
        {
            var self = _instance;
            if (self == null || self._body == null) return;

            self._hurt = 1f;
            self._hurtBearing = Survive.Combat.HurtBearing.Degrees(
                self._body.forward, self._body.position, sourcePosition);
        }

        /// <summary>가장 최근 피격의 방향(도). 검증이 눈이 아니라 값으로 보기 위한 것이다.</summary>
        public static float LastHurtBearing => _instance != null ? _instance._hurtBearing : 0f;

        /// <summary>
        /// 액면에 얼마나 붙었는가. 수평으로 구역 안이고 액면 높이에 가까울수록 1.
        /// 구역 밖은 0이지만 값은 프레임마다 lerp되므로 경계에서 툭 튀지 않는다.
        /// </summary>
        float ReadMacronium()
        {
            if (_body == null) return 0f;
            if (!MacroniumSurfaceZone.TryGetSurfaceAt(_body.position, out float surfaceY, out _)) return 0f;

            float above = Mathf.Abs(_body.position.y - surfaceY);
            return 1f - Mathf.Clamp01(above / MacroniumRange);
        }

        /// <summary>
        /// 낫이 얼마나 붙었는가.
        ///
        /// 정의 에셋(성향)을 보지 않고 <see cref="CreatureBrain.State"/>만 본다.
        /// 챕터 1의 다른 생물은 겁이 많거나 방어적이라 먼저 쫓아오지 않는다 —
        /// "지금 나를 쫓고 있는 것"이면 그레인을 올릴 이유로 충분하고,
        /// 이렇게 하면 생물 쪽 코드를 한 줄도 고치지 않는다.
        ///
        /// 매 프레임 씬을 훑지 않는다. 생물은 자주 생기고 사라지지 않으므로
        /// 1초에 한 번만 목록을 새로 뜬다.
        /// </summary>
        float ReadReaper()
        {
            if (_body == null) return 0f;

            if (Time.unscaledTime >= _nextCreatureScan)
            {
                _nextCreatureScan = Time.unscaledTime + 1f;
                _creatures = FindObjectsByType<CreatureBrain>();
            }

            float nearest = float.MaxValue;
            for (int i = 0; i < _creatures.Length; i++)
            {
                var c = _creatures[i];
                if (c == null) continue;
                if (c.State != CreatureState.Chase && c.State != CreatureState.Attack) continue;

                float d = Vector3.Distance(c.transform.position, _body.position);
                if (d < nearest) nearest = d;
            }

            if (nearest >= ReaperRange) return 0f;
            return 1f - Mathf.Clamp01(nearest / ReaperRange);
        }
    }
}
