using NUnit.Framework;
using UnityEngine;
using Survive.World;

/// <summary>
/// ⑦ 빛기둥이 밝은 구역에 미등록 — 화면과 규칙이 같은 답을 하게 만드는 규칙.
///
/// <b>여기서 못 박는 것의 절반은 「집지 않는 것」이다.</b> 자동 등록이 낙하물 표식이나
/// 매크로늄 석영까지 밝은 구역으로 만들면, 플레이어는 아무것도 하지 않고 표식 옆에
/// 서서 밤을 넘긴다. 실제 값으로 적어 두어야 나중에 상수를 만지는 사람이 무엇을
/// 깨뜨리는지 안다.
/// </summary>
public class FixedLightRuleTests
{
    // 씬·프리팹에서 실측한 값들. 상수를 흔들면 여기가 먼저 무너진다.
    const float 빛기둥세기 = 1200f, 빛기둥사거리 = 60f, 빛기둥각도 = 25.4f;
    const float 낙하물표식세기 = 1.1f, 낙하물표식사거리 = 3.5f;
    const float 석영세기 = 0.6f, 석영사거리 = 2.2f;
    const float 장식버섯세기 = 3.2f, 장식버섯사거리 = 5.5f;
    const float 군락세기 = 5.5f, 군락사거리 = 11f;
    const float 랜턴세기 = 2.2f, 랜턴사거리 = 14f;
    const float 화톳불세기 = 1.9f, 화톳불사거리 = 10f;

    [Test]
    public void 시작_지점_빛기둥은_밝은_구역이다()
    {
        Assert.IsTrue(FixedLightRule.IsZoneWorthy(빛기둥세기, 빛기둥사거리));
    }

    [Test]
    public void 낙하물_표식은_밝은_구역이_아니다()
    {
        Assert.IsFalse(FixedLightRule.IsZoneWorthy(낙하물표식세기, 낙하물표식사거리));
    }

    [Test]
    public void 매크로늄_석영은_밝은_구역이_아니다()
    {
        Assert.IsFalse(FixedLightRule.IsZoneWorthy(석영세기, 석영사거리));
    }

    [Test]
    public void 장식용_발광버섯은_밝은_구역이_아니다()
    {
        Assert.IsFalse(FixedLightRule.IsZoneWorthy(장식버섯세기, 장식버섯사거리));
    }

    [Test]
    public void 이미_주인이_있는_빛들은_세기만으로도_걸러진다()
    {
        // 군락·랜턴·화톳불은 자기를 등록하는 컴포넌트가 따로 있다. 그래도 이 규칙
        // 하나만으로 걸러지는지 확인해 둔다 — 거르는 층이 둘이면 한쪽이 새도 안전하다.
        Assert.IsFalse(FixedLightRule.IsZoneWorthy(군락세기, 군락사거리), "발광 군락");
        Assert.IsFalse(FixedLightRule.IsZoneWorthy(랜턴세기, 랜턴사거리), "랜턴");
        Assert.IsFalse(FixedLightRule.IsZoneWorthy(화톳불세기, 화톳불사거리), "화톳불");
    }

    [Test]
    public void 걸러진_것_중_가장_센_것과도_두_배_넘게_벌어진다()
    {
        // 가장 아슬아슬한 후보는 발광 군락(5.5/11)이다. 여유가 없으면 누군가
        // 장식 광원을 조금 밝히는 순간 조용히 안전 지대가 생긴다.
        float 가장센_비대상 = FixedLightRule.Reach(군락세기);
        Assert.Less(가장센_비대상 * 2f, FixedLightRule.MinLitRadius,
                    $"군락의 도달 거리 {가장센_비대상:F2}m가 기준 {FixedLightRule.MinLitRadius}m에 너무 가깝다");
    }

    [Test]
    public void 사거리를_줄여_둔_연출용_광원은_세기가_아무리_커도_빠진다()
    {
        Assert.IsFalse(FixedLightRule.IsZoneWorthy(9000f, 2f));
    }

    [Test]
    public void 거의_꺼진_광원은_사거리가_아무리_길어도_빠진다()
    {
        Assert.IsFalse(FixedLightRule.IsZoneWorthy(0.01f, 500f));
    }

    [Test]
    public void 도달_거리는_역제곱의_역함수다()
    {
        // 세기를 네 배로 올려야 반경이 두 배가 된다.
        float 하나 = FixedLightRule.Reach(100f);
        float 넷 = FixedLightRule.Reach(400f);
        Assert.AreEqual(하나 * 2f, 넷, 1e-3f);
    }

    [Test]
    public void 세기가_0이면_뻗지_않는다()
    {
        Assert.AreEqual(0f, FixedLightRule.Reach(0f), 1e-6f);
        Assert.AreEqual(0f, FixedLightRule.Reach(-5f), 1e-6f);
    }

    [Test]
    public void 스폿의_원뿔은_거리에_비례해_벌어진다()
    {
        float 가까이 = FixedLightRule.ConeRadius(10f, 60f);
        float 멀리 = FixedLightRule.ConeRadius(20f, 60f);
        Assert.AreEqual(가까이 * 2f, 멀리, 1e-3f);
        // 60도 원뿔의 반각은 30도, tan 30도는 1/√3이다.
        Assert.AreEqual(10f / Mathf.Sqrt(3f), 가까이, 1e-3f);
    }

    [Test]
    public void 빛기둥은_바닥에서_원뿔만큼만_밝다()
    {
        // 실측: 광원 (0,92,0)에서 바닥 (0,52,0)까지 40m.
        // 세기가 버티는 폭은 28m지만 원뿔이 9m라 원뿔이 정한다.
        float r = FixedLightRule.SpotZoneRadius(빛기둥세기, 40f, 빛기둥각도);
        Assert.AreEqual(9.0f, r, 0.3f, $"실제 {r:F2}m");
    }

    [Test]
    public void 광원을_구로_보면_세_배_넓어진다()
    {
        // 이 대비가 SpotZoneRadius가 존재하는 이유다. 광원 자리를 중심으로 도달
        // 거리짜리 구를 두면 바닥에서 28m가 밝아지는데 화면의 원은 9m다.
        float 구로보면 = Mathf.Sqrt(FixedLightRule.Reach(빛기둥세기) * FixedLightRule.Reach(빛기둥세기)
                                    - 40f * 40f);
        float 원뿔로보면 = FixedLightRule.SpotZoneRadius(빛기둥세기, 40f, 빛기둥각도);
        Assert.Greater(구로보면, 원뿔로보면 * 2.5f);
    }

    [Test]
    public void 도달_거리보다_먼_바닥은_밝지_않다()
    {
        Assert.AreEqual(0f, FixedLightRule.SpotZoneRadius(4f, 50f, 60f), 1e-6f);
    }

    [Test]
    public void 점광원은_사거리와_도달_거리_중_작은_쪽이다()
    {
        Assert.AreEqual(10f, FixedLightRule.PointZoneRadius(100000f, 10f), 1e-3f);
        Assert.AreEqual(FixedLightRule.Reach(50f), FixedLightRule.PointZoneRadius(50f, 1000f), 1e-3f);
    }

    // ── 거르는 층 전부 ──────────────────────────────────────────

    static bool 세운다(LightType type = LightType.Spot, bool on = true, bool hasOwner = false,
                      float intensity = 빛기둥세기, float range = 빛기둥사거리) =>
        FixedLightRule.ShouldRegister(type, on, hasOwner, intensity, range);

    [Test]
    public void 빛기둥은_세운다()
    {
        Assert.IsTrue(세운다());
    }

    [Test]
    public void 주인이_있으면_아무리_세도_빠진다()
    {
        // 화톳불·랜턴·발광 군락은 자기 연료와 전원을 안다. 덮으면 꺼진 불이
        // 계속 밝은 구역으로 남는다.
        Assert.IsFalse(세운다(hasOwner: true));
    }

    [Test]
    public void 꺼진_광원은_빠진다()
    {
        Assert.IsFalse(세운다(on: false));
    }

    [Test]
    public void 태양은_구역이_될_수_없다()
    {
        // 자리가 없는 전역광이다. 세기가 아무리 커도 「어디가 밝은가」에 답할 수 없다.
        Assert.IsFalse(세운다(type: LightType.Directional));
    }

    [Test]
    public void 장식_광원은_켜져_있고_주인이_없어도_빠진다()
    {
        Assert.IsFalse(세운다(type: LightType.Point,
                              intensity: 장식버섯세기, range: 장식버섯사거리));
    }
}
