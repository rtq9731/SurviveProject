using UnityEngine;

namespace Survive.World
{
    /// <summary>
    /// 박힌 조명탄 하나. <b>얇은 껍데기다</b> — 규칙과 시계는 전부
    /// <see cref="FlareZone"/>이 들고, 여기서 하는 일은 셋뿐이다.
    /// 등록하고, 빛을 켜고, 다 타면 스스로 사라진다.
    ///
    /// <b>프리팹이 없다.</b> 코드가 세운다 — 조명탄은 씬에 미리 놓아 두는 물건이
    /// 아니라 쏘는 순간 생기는 것이고, 프리팹을 만들면 반경·색·세기의 사본이
    /// 거기 생겨 <see cref="FlareRule"/>을 돌려도 게임이 안 바뀐다
    /// (화톳불의 <c>maxFuel</c>에서 실제로 겪은 일이다).
    ///
    /// <b><see cref="ILitZoneSource"/>를 이 컴포넌트가 구현하는 이유</b>는 격리다.
    /// 안쪽 <see cref="FlareZone"/>을 그대로 등록하면 검증 무대가 광원을 훑어
    /// 끄는 길(<c>E2EHarness.MuteAmbientLitZones</c>)이 이것을 못 본다 —
    /// 그쪽은 <c>MonoBehaviour</c> 중에서 고른다. 화톳불·발광 군락과 같은 자격으로
    /// 서 있어야 다음 시나리오가 남은 불빛에 오염되지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class FlareBurn : MonoBehaviour, ILitZoneSource
    {
        FlareZone _zone;
        Light _lamp;

        /// <summary>이 조명탄의 밝은 구역. 규칙 판정과 실측이 읽는 창구다.</summary>
        public FlareZone Zone => _zone;

        /// <summary>남은 시간(초).</summary>
        public float SecondsLeft => _zone != null ? _zone.SecondsLeft : 0f;

        /// <summary>
        /// 이 자리에 조명탄을 지핀다.
        /// </summary>
        /// <param name="at">박힌 자리.</param>
        /// <param name="radius">
        /// 반경(m). 비워 두면 규칙값이다. 값을 받는 것은 실측이 후보를 재기 위해서다
        /// (<see cref="FlareZone"/> 참조) — 게임이 쏘는 조명탄은 언제나 규칙값이다.
        /// </param>
        public static FlareBurn Ignite(Vector3 at, float radius = -1f)
        {
            var go = new GameObject("Flare");
            go.transform.position = at;

            var burn = go.AddComponent<FlareBurn>();
            burn.Setup(at, radius);
            return burn;
        }

        void Setup(Vector3 at, float radius)
        {
            _zone = new FlareZone(at, radius);
            BuildLamp();

            // Awake·OnEnable은 AddComponent 시점에 이미 지났다. 그때는 구역이
            // 없어 등록을 미뤄 두었으므로 여기서 한 번 부른다.
            LitZoneRegistry.Register(this);
        }

        /// <summary>
        /// 실제로 보이는 빛. <b>판정 반경과 같은 값을 쓴다</b> — 화면과 규칙이
        /// 다른 말을 하면 플레이어가 "여기까지가 안전하다"를 눈으로 배울 수 없다.
        /// 화톳불이 <c>fullRange</c>를 그대로 판정에 쓰는 것과 같은 규율이다.
        /// </summary>
        void BuildLamp()
        {
            var lampGo = new GameObject("FlareLight");
            lampGo.transform.SetParent(transform, false);

            _lamp = lampGo.AddComponent<Light>();
            _lamp.type = LightType.Point;

            // 자홍이다. 매크로늄 석영으로 만들었으므로 재료의 색이 그대로 간다 —
            // 다섯 번째 광원 색을 만들지 않는다(광원 4색 규칙).
            _lamp.color = FlareRule.Color;
            _lamp.range = _zone.Radius;
            _lamp.intensity = FlareRule.Intensity;
            _lamp.shadows = LightShadows.None;
        }

        void OnEnable()
        {
            // 구역이 아직 없으면 Setup이 부른다. 여기서 앞질러 등록하면
            // 반경 0짜리 광원이 한 프레임 등록돼 있게 된다.
            if (_zone != null) LitZoneRegistry.Register(this);
        }

        // 비활성화·철거·씬 언로드 전부 여기를 지난다.
        void OnDisable() => LitZoneRegistry.Unregister(this);

        void Update()
        {
            if (_zone == null) return;

            _zone.Tick(Time.deltaTime);
            if (_zone.IsLit) return;

            // 다 탔다. 꺼진 광원을 남겨 두면 등록부에는 안 잡히면서 화면에는
            // 자홍 웅덩이가 그대로 남아, 규칙과 화면이 갈린다.
            Destroy(gameObject);
        }

        // ── ILitZoneSource ───────────────────────────────────────
        // 전부 구역에 넘긴다. 사본을 두면 규칙을 돌려도 이쪽이 옛 값을 답한다.

        public Vector3 LitZoneCenter => _zone != null ? _zone.LitZoneCenter : transform.position;
        public float LitZoneRadius => _zone != null ? _zone.LitZoneRadius : 0f;
        public bool IsLit => _zone != null && _zone.IsLit;
    }
}
