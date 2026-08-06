using System.Collections.Generic;
using NUnit.Framework;
using Survive.World;

/// <summary>
/// 백로그 29 — "장비 없이 매크로늄 액면에 닿으면 죽고, 액면 보행 장비를 지니면 그 위를 걷는다".
///
/// 규칙은 Unity 없이 도는 순수 정적 클래스이므로 씬도 MonoBehaviour도 쓰지 않는다.
/// 여기서 보는 것은 <b>대가를 정하는 규칙</b>이고, 그 규칙이 실제로 사람을 죽이는지는
/// <c>E2EMacroniumContact</c>가 실제 사망 경로로 본다.
/// </summary>
public class MacroniumContactTests
{
    static List<GearCapability> 장비(params GearCapability[] 목록) => new List<GearCapability>(목록);

    static HazardZone 액면(float 폭 = 30f) => new HazardZone(EnvironmentHazard.MacroniumSurface, 폭);

    static GearCapability 보행기(float 용량 = 36f) => new GearCapability(TraversalGear.SurfaceWalker, 용량);

    // ── 닿았는가 ────────────────────────────────────────────────────────────

    [Test]
    public void 발이_액면_아래로_내려가면_닿은_것이다()
    {
        Assert.IsTrue(MacroniumContact.Touches(feetY: 9.0f, surfaceY: 10f));
    }

    [Test]
    public void 발이_액면_위로_충분히_떠_있으면_닿지_않은_것이다()
    {
        Assert.IsFalse(MacroniumContact.Touches(feetY: 11f, surfaceY: 10f));
    }

    [Test]
    public void 액면에_정확히_선_상태도_닿은_것으로_친다()
    {
        // 액면 보행 장비로 걷는 동안이 바로 이 높이다. 여기가 "닿지 않음"으로 떨어지면
        // 걷고 있는데 아무것도 닿지 않은 것으로 보고된다.
        Assert.IsTrue(MacroniumContact.Touches(feetY: 10f, surfaceY: 10f));
    }

    [Test]
    public void 발바닥_두께만큼의_여유_안쪽은_닿은_것이다()
    {
        Assert.IsTrue(MacroniumContact.Touches(10f + MacroniumContact.ContactSkin, 10f));
        Assert.IsFalse(MacroniumContact.Touches(10f + MacroniumContact.ContactSkin + 0.01f, 10f));
    }

    // ── 대가 ────────────────────────────────────────────────────────────────

    [Test]
    public void 맨몸으로_닿으면_죽는다()
    {
        Assert.AreEqual(MacroniumContactOutcome.Lethal,
                        MacroniumContact.Resolve(true, 액면(), 장비()));
    }

    [Test]
    public void 장비_목록이_null이어도_맨몸과_같다()
    {
        Assert.AreEqual(MacroniumContactOutcome.Lethal,
                        MacroniumContact.Resolve(true, 액면(), null));
    }

    [Test]
    public void 액면_보행_장비를_지니면_받쳐진다()
    {
        Assert.AreEqual(MacroniumContactOutcome.Supported,
                        MacroniumContact.Resolve(true, 액면(30f), 장비(보행기(36f))));
    }

    [Test]
    public void 닿지_않으면_장비가_있든_없든_아무_일도_없다()
    {
        Assert.AreEqual(MacroniumContactOutcome.None,
                        MacroniumContact.Resolve(false, 액면(), 장비()));
        Assert.AreEqual(MacroniumContactOutcome.None,
                        MacroniumContact.Resolve(false, 액면(), 장비(보행기())));
    }

    [Test]
    public void 다른_장비를_아무리_갖춰도_액면에서는_죽는다()
    {
        var 다른것들 = 장비(
            new GearCapability(TraversalGear.Lantern, 999f),
            new GearCapability(TraversalGear.Swimming, 999f),
            // 돌파정은 여기 넣지 않는다 — 그것을 들고 액면에 서면 죽는 대신 내려간다
            // (MacroniumContact.Resolve). "받쳐 주지 못한다"를 보는 자리라 섞으면 물음이 바뀐다.
            new GearCapability(TraversalGear.None, 999f));

        Assert.AreEqual(MacroniumContactOutcome.Lethal,
                        MacroniumContact.Resolve(true, 액면(), 다른것들));
    }

    [Test]
    public void 용량이_모자란_장비는_받쳐_주지_못한다()
    {
        // 반쯤 받쳐 주는 상태는 없다. 관문 판정이 "못 지난다"고 답한 이상 빠진다.
        Assert.AreEqual(MacroniumContactOutcome.Lethal,
                        MacroniumContact.Resolve(true, 액면(30f), 장비(보행기(29.999f))));
    }

    [Test]
    public void 용량이_폭과_같으면_받쳐진다()
    {
        // 경계값은 통과로 친다 — EnvironmentThreat와 같은 규칙이어야 한다.
        Assert.AreEqual(MacroniumContactOutcome.Supported,
                        MacroniumContact.Resolve(true, 액면(30f), 장비(보행기(30f))));
    }

    // ── 액면이 아닌 것 ──────────────────────────────────────────────────────

    [TestCase(EnvironmentHazard.None)]
    [TestCase(EnvironmentHazard.Darkness)]
    [TestCase(EnvironmentHazard.Depth)]
    [TestCase(EnvironmentHazard.MacroniumLayer)]
    public void 액면이_아닌_위협은_밟아도_대가가_없다(EnvironmentHazard 위협)
    {
        // 어둠도 수심도 진한 층도 "밟으면 죽는" 종류가 아니다 — 위협은 막는 것이다.
        // 진한 층은 액면 아래에 있으므로 "밟는" 판정의 대상이 아예 아니다.
        var 구간 = new HazardZone(위협, 30f);

        Assert.AreEqual(MacroniumContactOutcome.None, MacroniumContact.Resolve(true, 구간, 장비()));
        Assert.AreEqual(MacroniumContactOutcome.None, MacroniumContact.Resolve(true, 구간, 장비(보행기())));
    }

    // ── 관문 판정과 어긋나지 않는다 ─────────────────────────────────────────

    [Test]
    public void 지날_수_있다고_답한_것은_받쳐지고_아니면_죽는다()
    {
        // 두 물음이 갈라지면 "관문은 열리는데 밟으면 죽는" 상태가 생긴다.
        var 구간 = 액면(30f);

        foreach (float 용량 in new[] { 0f, 15f, 29.999f, 30f, 30.001f, 36f, 100f })
        {
            var 목록 = 장비(보행기(용량));
            bool 지난다 = EnvironmentThreat.CanPass(구간, 목록);
            var 결과 = MacroniumContact.Resolve(true, 구간, 목록);

            Assert.AreEqual(지난다 ? MacroniumContactOutcome.Supported : MacroniumContactOutcome.Lethal,
                            결과, $"용량 {용량}");
        }
    }

    // ── 높이까지 한 번에 보는 쪽 ────────────────────────────────────────────

    [Test]
    public void 높이를_함께_넘기면_닿았는지까지_한_번에_본다()
    {
        Assert.AreEqual(MacroniumContactOutcome.Lethal,
                        MacroniumContact.Resolve(feetY: 9.5f, surfaceY: 10f, 액면(), 장비()));

        Assert.AreEqual(MacroniumContactOutcome.None,
                        MacroniumContact.Resolve(feetY: 12f, surfaceY: 10f, 액면(), 장비()));

        Assert.AreEqual(MacroniumContactOutcome.Supported,
                        MacroniumContact.Resolve(feetY: 9.5f, surfaceY: 10f, 액면(), 장비(보행기())));
    }
}
