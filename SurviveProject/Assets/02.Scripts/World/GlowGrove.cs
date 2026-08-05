using System.Collections.Generic;
using UnityEngine;
using Survive.Harvesting;

namespace Survive.World
{
    /// <summary>
    /// 발광 버섯 군락 하나. 갓 무더기 몇 개와 그것들이 내는 빛을 묶는다.
    ///
    /// <b>무엇이 달라지는가.</b> 지금까지 군락은 배터리를 채워 주기만 하는 배경이었다
    /// (<see cref="LightZone"/>). 이제 군락은 <see cref="ILitZoneSource"/>이기도 하다 —
    /// 화톳불·랜턴과 같은 창구로 "여기가 밝은가"에 답한다. 그래서 빛을 꺼리는
    /// 소비자(낫)가 켜진 군락 안으로 들어오지 못하고, <b>갓을 전부 따서 꺼뜨리면
    /// 들어온다</b>. 무료 충전소를 자원으로 바꿔 먹는 대가가 그것이다.
    ///
    /// 밝기 규칙은 여기 없다. <see cref="GlowGroveRule"/>이 Unity 없이 답한다.
    /// 이 컴포넌트는 남은 갓을 세어 규칙에 묻고, 나온 배율을 라이트에 옮기는 일만 한다.
    ///
    /// <b>씬에 놓지 않는다.</b> MainScene은 병합할 수 없는 단일 파일이라
    /// <see cref="GlowGroveService"/>가 실행 시점에 스스로 붙인다 —
    /// <see cref="MacroniumContactService"/>와 같은 이유, 같은 방식이다.
    /// </summary>
    [DisallowMultipleComponent]
    public class GlowGrove : MonoBehaviour, ILitZoneSource
    {
        /// <summary>지금 세계에 서 있는 군락들. 검증과 조회에서 쓴다.</summary>
        public static IReadOnlyList<GlowGrove> Active => _active;
        static readonly List<GlowGrove> _active = new List<GlowGrove>();

        readonly List<GlowCapCluster> _caps = new List<GlowCapCluster>();

        Light _light;
        float _baseIntensity;
        float _baseRange;

        /// <summary>이 군락에 달린 갓 무더기 전부.</summary>
        public IReadOnlyList<GlowCapCluster> Caps => _caps;

        /// <summary>전체 갓 무더기 수. 밝기 규칙의 분모다.</summary>
        public int Total => _caps.Count;

        /// <summary>아직 따지 않은 갓 무더기 수. 밝기 규칙의 분자다.</summary>
        public int Remaining { get; private set; }

        /// <summary>지금 밝기 배율(0~1).</summary>
        public float Brightness => GlowGroveRule.Brightness(Remaining, Total);

        /// <summary>군락이 온전할 때의 빛 세기. 되돌릴 기준값이다.</summary>
        public float BaseIntensity => _baseIntensity;

        /// <summary>군락이 온전할 때의 빛 반경. 안전 반경의 기준값이다.</summary>
        public float BaseRange => _baseRange;

        public Light Light => _light;

        void Awake()
        {
            _light = GetComponent<Light>();
            if (_light != null)
            {
                _baseIntensity = _light.intensity;
                _baseRange = _light.range;
            }
        }

        void OnEnable()
        {
            if (!_active.Contains(this)) _active.Add(this);
            LitZoneRegistry.Register(this);
        }

        void OnDisable()
        {
            _active.Remove(this);
            LitZoneRegistry.Unregister(this);
        }

        /// <summary>갓 무더기 하나를 이 군락에 등록한다. 설치 서비스가 부른다.</summary>
        public void Attach(GlowCapCluster cap)
        {
            if (cap == null || _caps.Contains(cap)) return;
            _caps.Add(cap);
            Refresh();
        }

        /// <summary>
        /// 남은 갓을 다시 세고 빛을 맞춘다. 갓 무더기가 따이거나 다시 자랄 때 부른다.
        ///
        /// 세기와 반경에 같은 배율을 건다. 눈에 보이는 빛과 포식자를 막는 반경이
        /// 어긋나면 플레이어는 "얼마나 남았는지"를 화면에서 읽을 수 없다.
        /// </summary>
        public void Refresh()
        {
            int remaining = 0;
            for (int i = 0; i < _caps.Count; i++)
                if (_caps[i] != null && !_caps[i].IsHarvested) remaining++;
            Remaining = remaining;

            if (_light == null) return;

            float b = Brightness;
            _light.intensity = _baseIntensity * b;
            _light.range = _baseRange * b;

            // 반경 0짜리 라이트를 켜 두면 렌더러가 매 프레임 헛일을 한다.
            // 꺼진 군락은 실제로 꺼 둔다 — 화면에서도 "이 자리는 죽었다"가 읽힌다.
            _light.enabled = GlowGroveRule.IsLit(remaining);
        }

        /// <summary>
        /// 검증용 — 이 군락의 갓들이 다시 자라는 데 걸리는 시간을 바꾼다.
        ///
        /// 규칙(<see cref="GlowGroveRule.HasRegrown"/>)은 그대로 두고 상수만 줄인다.
        /// 실제 값 180초를 E2E에서 그대로 기다리면 시나리오 하나가 3분을 잡아먹는다.
        /// </summary>
        public void OverrideRegrowSeconds(float seconds)
        {
            for (int i = 0; i < _caps.Count; i++)
                if (_caps[i] != null) _caps[i].RegrowSeconds = seconds;
        }

        // ── ILitZoneSource ───────────────────────────────────────
        // 화톳불(Building.Campfire)·랜턴(LanternController)이 이미 같은 방식으로
        // 자신을 내놓는다. 반경은 실제 빛이 닿는 거리를 그대로 쓴다 —
        // 랜턴이 fullRange를 쓰는 것과 같은 관례다.

        public Vector3 LitZoneCenter => transform.position;

        public float LitZoneRadius => _baseRange * Brightness;

        bool ILitZoneSource.IsLit => GlowGroveRule.IsLit(Remaining);
    }
}
