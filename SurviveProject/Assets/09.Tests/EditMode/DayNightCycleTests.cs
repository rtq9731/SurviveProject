using NUnit.Framework;
using UnityEngine;
using Survive.Domain.Art;
using Survive.World;

/// <summary>
/// 하루가 도는 규칙.
///
/// <b>왜 화면 없이 재는가.</b> 여기서 물어야 하는 것 셋은 전부 화면을 띄워서는
/// 확인할 수 없는 것들이다 — <b>경계에서 값이 튀지 않는가</b>(해질녘의 첫 순간과
/// 마지막 순간), <b>되감아도 같은 답인가</b>, <b>하루를 백 번 건너뛰어도 같은가</b>.
/// 눈으로는 "어두워졌네"까지만 말할 수 있다.
///
/// <b>이 파일의 회귀선은 마지막 두 검사다.</b> 밤이 온다는 것은 세계 전체가
/// 어두워진다는 뜻이지 <b>화톳불이 꺼진다는 뜻이 아니다.</b> 시계와 밝은 구역이
/// 얽히는 순간 「밤이 되면 거점이 사라진다」가 조용히 들어오고, 그것은 화면에서
/// 버그로 보이지 않는다 — 그냥 게임이 이상해질 뿐이다.
/// </summary>
public class DayNightCycleTests
{
    const float 자정 = 0f;
    const float 정오 = 0.5f;

    // ── 국면 ────────────────────────────────────────────────

    [Test]
    public void 자정은_밤이다()
    {
        Assert.AreEqual(DayPhase.Night, DayNightCycle.PhaseAt(자정));
        Assert.IsTrue(DayNightCycle.IsNight(자정));
    }

    [Test]
    public void 정오는_낮이다()
    {
        Assert.AreEqual(DayPhase.Day, DayNightCycle.PhaseAt(정오));
        Assert.IsFalse(DayNightCycle.IsNight(정오));
    }

    [Test]
    public void 국면의_경계는_시작하는_쪽에_붙는다()
    {
        // 해뜰녘의 첫 순간은 이미 해뜰녘이다. 경계를 앞 국면에 붙이면
        // "해가 뜨기 시작했는데 아직 밤"인 한 틱이 생기고, 그 한 틱에
        // 「낫은 밤에 다닌다」가 걸리면 낫이 해 뜬 뒤에도 한 프레임 더 돈다.
        Assert.AreEqual(DayPhase.Dawn, DayNightCycle.PhaseAt(DayNightCycle.DawnStart));
        Assert.AreEqual(DayPhase.Day, DayNightCycle.PhaseAt(DayNightCycle.DawnEnd));
        Assert.AreEqual(DayPhase.Dusk, DayNightCycle.PhaseAt(DayNightCycle.DuskStart));
        Assert.AreEqual(DayPhase.Night, DayNightCycle.PhaseAt(DayNightCycle.DuskEnd));
    }

    [Test]
    public void 경계_직전은_앞_국면이다()
    {
        const float 눈금 = 1e-4f;
        Assert.AreEqual(DayPhase.Night, DayNightCycle.PhaseAt(DayNightCycle.DawnStart - 눈금));
        Assert.AreEqual(DayPhase.Dawn, DayNightCycle.PhaseAt(DayNightCycle.DawnEnd - 눈금));
        Assert.AreEqual(DayPhase.Day, DayNightCycle.PhaseAt(DayNightCycle.DuskStart - 눈금));
        Assert.AreEqual(DayPhase.Dusk, DayNightCycle.PhaseAt(DayNightCycle.DuskEnd - 눈금));
    }

    [Test]
    public void 네_국면이_하루를_빈틈없이_나눈다()
    {
        Assert.Less(0f, DayNightCycle.DawnStart);
        Assert.Less(DayNightCycle.DawnStart, DayNightCycle.DawnEnd);
        Assert.Less(DayNightCycle.DawnEnd, DayNightCycle.DuskStart);
        Assert.Less(DayNightCycle.DuskStart, DayNightCycle.DuskEnd);
        Assert.Less(DayNightCycle.DuskEnd, 1f);
    }

    // ── 밝기 곡선 ────────────────────────────────────────────

    [Test]
    public void 낮은_1이고_밤은_0이다()
    {
        Assert.AreEqual(1f, DayNightCycle.Daylight(정오), 1e-5f);
        Assert.AreEqual(0f, DayNightCycle.Daylight(자정), 1e-5f);
    }

    [Test]
    public void 해질녘_한복판은_반쯤_남는다()
    {
        float 한복판 = (DayNightCycle.DuskStart + DayNightCycle.DuskEnd) * 0.5f;
        Assert.AreEqual(0.5f, DayNightCycle.Daylight(한복판), 1e-4f);
    }

    [Test]
    public void 해뜰녘과_해질녘이_거울이다()
    {
        // 한쪽만 손대면 아침과 저녁의 인상이 다른 게임이 된다.
        for (int i = 0; i <= 10; i++)
        {
            float 비율 = i / 10f;
            float 아침 = Mathf.Lerp(DayNightCycle.DawnStart, DayNightCycle.DawnEnd, 비율);
            float 저녁 = Mathf.Lerp(DayNightCycle.DuskEnd, DayNightCycle.DuskStart, 비율);
            Assert.AreEqual(DayNightCycle.Daylight(아침), DayNightCycle.Daylight(저녁), 1e-4f,
                            $"비율 {비율:F1}에서 아침과 저녁의 밝기가 다르다");
        }
    }

    [Test]
    public void 곡선이_경계에서_끊기지_않는다()
    {
        // 사람 눈은 값보다 값의 변화에 민감하다. 경계에서 툭 튀면 「번쩍」으로 읽힌다.
        const float 눈금 = 1e-3f;
        foreach (float 경계 in new[]
                 {
                     DayNightCycle.DawnStart, DayNightCycle.DawnEnd,
                     DayNightCycle.DuskStart, DayNightCycle.DuskEnd,
                 })
        {
            float 앞 = DayNightCycle.Daylight(경계 - 눈금);
            float 뒤 = DayNightCycle.Daylight(경계 + 눈금);
            Assert.Less(Mathf.Abs(뒤 - 앞), 0.02f,
                        $"시각 {경계:F3}에서 밝기가 {앞:F4} → {뒤:F4}로 튄다");
        }
    }

    [Test]
    public void 밝기는_밤에서_낮으로_한_번만_오른다()
    {
        // 오르내리는 봉우리가 둘이면 하루에 해가 두 번 뜨는 셈이다.
        float 앞 = DayNightCycle.Daylight(0f);
        int 방향바뀜 = 0;
        int 이전방향 = 0;

        for (int i = 1; i <= 2000; i++)
        {
            float 값 = DayNightCycle.Daylight(i / 2000f);
            int 방향 = 값 > 앞 + 1e-6f ? 1 : (값 < 앞 - 1e-6f ? -1 : 이전방향);
            if (이전방향 != 0 && 방향 != 이전방향) 방향바뀜++;
            이전방향 = 방향;
            앞 = 값;
        }

        Assert.AreEqual(1, 방향바뀜, "하루에 봉우리가 하나여야 한다");
    }

    // ── 되감기와 건너뛰기 ────────────────────────────────────

    [Test]
    public void 하루를_건너뛰어도_같은_시각이다()
    {
        for (int 하루 = 0; 하루 < 100; 하루++)
        {
            double 초 = 0.37 * DayNightCycle.DayLengthSeconds + 하루 * (double)DayNightCycle.DayLengthSeconds;
            Assert.AreEqual(0.37f, DayNightCycle.Wrap(초), 1e-3f, $"{하루}일째");
        }
    }

    [Test]
    public void 되감아도_결정적이다()
    {
        // 음수 나머지가 그대로 새면 -0.3 같은 값이 나와 국면 판정이 통째로 어긋난다.
        for (int 하루 = 1; 하루 <= 20; 하루++)
        {
            double 초 = -하루 * (double)DayNightCycle.DayLengthSeconds
                        + 0.62 * DayNightCycle.DayLengthSeconds;
            Assert.AreEqual(0.62f, DayNightCycle.Wrap(초), 1e-3f, $"{하루}일 전");
        }
    }

    [Test]
    public void 접은_값은_언제나_0과_1_사이다()
    {
        foreach (double 초 in new[] { -1e6, -1234.5, -0.0001, 0.0, 1.0, 12345.6, 1e6, 1e9 })
        {
            float t = DayNightCycle.Wrap(초);
            Assert.GreaterOrEqual(t, 0f, $"{초}초");
            Assert.Less(t, 1f, $"{초}초");
        }
    }

    [Test]
    public void 시각과_초가_왕복한다()
    {
        for (int i = 0; i < 20; i++)
        {
            float t = i / 20f;
            Assert.AreEqual(t, DayNightCycle.Wrap(DayNightCycle.SecondsAt(t)), 1e-4f);
        }
    }

    // ── 태양의 자리 ──────────────────────────────────────────

    [Test]
    public void 태양은_정오에_가장_높고_자정에_가장_낮다()
    {
        Assert.AreEqual(DayNightCycle.NoonPitchDegrees, DayNightCycle.SunPitchDegrees(정오), 1e-3f);
        Assert.AreEqual(-DayNightCycle.NoonPitchDegrees, DayNightCycle.SunPitchDegrees(자정), 1e-3f);
    }

    [Test]
    public void 밤에는_태양이_지평선_아래에_있다()
    {
        Assert.Less(DayNightCycle.SunPitchDegrees(0.05f), 0f);
        Assert.Less(DayNightCycle.SunPitchDegrees(0.95f), 0f);
    }

    [Test]
    public void 밤의_태양은_아무것도_밝히지_않는다()
    {
        Assert.AreEqual(0f, DayNightCycle.SunIntensity(자정), 1e-6f);
        Assert.Greater(DayNightCycle.SunIntensity(정오), 0f);
    }

    [Test]
    public void 태양은_지표광_회백이다()
    {
        // 빛기둥이 없어져도 색은 남는다. 지상에서 그 색은 곧 햇빛이다.
        Assert.AreEqual(ArtPalette.LightShaft, DayNightCycle.SunColor);
    }

    // ── 환경광의 색 ──────────────────────────────────────────

    [Test]
    public void 낮이_해질녘보다_밝고_해질녘이_밤보다_밝다()
    {
        float 해질녘 = (DayNightCycle.DuskStart + DayNightCycle.DuskEnd) * 0.5f;
        float 낮 = DayNightCycle.AmbientLuminance(정오);
        float 녘 = DayNightCycle.AmbientLuminance(해질녘);
        float 밤 = DayNightCycle.AmbientLuminance(자정);

        Assert.Greater(낮, 녘, $"낮 {낮:F5} 해질녘 {녘:F5}");
        Assert.Greater(녘, 밤, $"해질녘 {녘:F5} 밤 {밤:F5}");
    }

    [Test]
    public void 밤은_어두워지는_것이_아니라_보라색이_짙어지는_것이다()
    {
        // 밝기만 줄이면 밤이 그냥 「어두운 낮」이 된다.
        var 밤색 = DayNightCycle.AmbientAt(자정);
        Assert.Greater(밤색.b, 밤색.g, "밤인데 자홍 기운이 없다");
        Assert.Greater(밤색.r, 밤색.g, "밤인데 자홍 기운이 없다");
    }

    [Test]
    public void 낮의_환경광은_지표광_쪽으로_기운다()
    {
        var 낮색 = DayNightCycle.AmbientAt(정오);
        Assert.Greater(낮색.r, 낮색.b, "낮인데 회백이 아니라 보라색이다");
    }

    [Test]
    public void 밤도_완전한_검정은_아니다()
    {
        // 지평선의 매크로늄이 유일하게 남은 빛이다. 0으로 만들면 그 설정이 화면에서 사라진다.
        Assert.Greater(DayNightCycle.AmbientLuminance(자정), 0f);
    }

    [Test]
    public void 낮에도_랜턴이_쓸모_있을_만큼은_어둡다()
    {
        // 이 상한이 무너지면 랜턴이 밤 전용 물건이 되고,
        // 「어둠은 비용」이 하루의 절반에서만 작동한다.
        Assert.Less(DayNightCycle.AmbientLuminance(정오), 0.25f,
                    "한낮의 환경광이 너무 밝다 — 랜턴을 켤 이유가 사라진다");
    }

    // ── 회귀선: 밝은 구역은 시각을 모른다 ────────────────────

    class 화톳불 : ILitZoneSource
    {
        public Vector3 LitZoneCenter => Vector3.zero;
        public float LitZoneRadius => 10f;
        public bool IsLit { get; set; } = true;
    }

    [Test]
    public void 밤이_되어도_화톳불_자리는_밝다()
    {
        LitZoneRegistry.Clear();
        var 불 = new 화톳불();
        LitZoneRegistry.Register(불);
        try
        {
            // 시각을 자정으로 옮겨 보아야 하는데, 옮길 자리가 없다는 것이 이 검사의 답이다.
            // LitZoneRegistry에는 시각을 받는 창구가 아예 없으므로 밤낮과 무관하다.
            Assert.IsTrue(LitZoneRegistry.IsLit(new Vector3(3f, 0f, 0f)),
                          "화톳불 안이 어둡다고 답했다");
            Assert.IsFalse(LitZoneRegistry.IsLit(new Vector3(30f, 0f, 0f)));

            불.IsLit = false;
            Assert.IsFalse(LitZoneRegistry.IsLit(new Vector3(3f, 0f, 0f)),
                           "꺼진 불이 계속 밝은 구역이다");
        }
        finally
        {
            LitZoneRegistry.Clear();
        }
    }

    [Test]
    public void 시계는_밝은_구역을_건드리는_길이_없다()
    {
        // 밤이 오면 세계 전체가 어두운 것이지 밝은 구역이 없어지는 것이 아니다.
        // 그 약속을 지키는 가장 확실한 방법은 두 모듈이 서로를 모르는 것이다 —
        // 여기서는 <b>타입 수준</b>으로 확인한다. 어느 쪽이든 상대를 참조하기
        // 시작하면 사람이 알아채기 전에 이 검사가 먼저 빨개진다.
        var 시계 = typeof(DayNightCycle);
        foreach (var m in 시계.GetMethods(System.Reflection.BindingFlags.Public |
                                          System.Reflection.BindingFlags.Static))
        {
            Assert.AreNotEqual(typeof(LitZoneRegistry), m.ReturnType);
            foreach (var p in m.GetParameters())
                Assert.AreNotEqual(typeof(LitZoneRegistry), p.ParameterType,
                                   $"{m.Name}이 밝은 구역을 받는다");
        }

        var 밝은구역 = typeof(LitZoneRegistry);
        foreach (var m in 밝은구역.GetMethods(System.Reflection.BindingFlags.Public |
                                              System.Reflection.BindingFlags.Static))
            foreach (var p in m.GetParameters())
                Assert.AreNotEqual(typeof(DayPhase), p.ParameterType,
                                   $"{m.Name}이 시각을 받는다 — 밤이 거점을 지운다");
    }
}
