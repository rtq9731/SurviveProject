using System.Collections.Generic;
using NUnit.Framework;
using Survive.World;

/// <summary>
/// P2 스펙 §9 [자동] — "환경 위협 판정 EditMode 테스트: 장비별 통과 가부, 경계값".
/// 판정은 Unity 없이 도는 순수 정적 클래스이므로 씬도 MonoBehaviour도 쓰지 않는다.
/// </summary>
public class EnvironmentThreatTests
{
    static List<GearCapability> 장비(params GearCapability[] 목록) => new List<GearCapability>(목록);

    // ── 어떤 장비가 필요한가 (스펙 §3·§4 "뚫는 것" 열) ───────────────────────

    [TestCase(EnvironmentHazard.None, TraversalGear.None)]
    [TestCase(EnvironmentHazard.Darkness, TraversalGear.Lantern)]
    [TestCase(EnvironmentHazard.Depth, TraversalGear.Swimming)]
    [TestCase(EnvironmentHazard.Gap, TraversalGear.Bridge)]
    [TestCase(EnvironmentHazard.MacroniumSurface, TraversalGear.SurfaceWalker)]
    public void 위협마다_뚫는_장비가_하나씩_대응한다(EnvironmentHazard 위협, TraversalGear 장비종류)
    {
        Assert.AreEqual(장비종류, EnvironmentThreat.RequiredGear(위협));
    }

    // ── 장비별 통과 가부 ────────────────────────────────────────────────────

    [Test]
    public void 막는_것이_없으면_맨몸으로_지난다()
    {
        var 구간 = new HazardZone(EnvironmentHazard.None, 0f);
        Assert.IsTrue(EnvironmentThreat.CanPass(구간, null));
    }

    [Test]
    public void 막는_것이_없으면_크기가_적혀_있어도_지난다()
    {
        // None 구간의 Magnitude는 쓰이지 않는 값이다 — 남아 있어도 판정을 흔들면 안 된다.
        var 구간 = new HazardZone(EnvironmentHazard.None, 999f);
        Assert.IsTrue(EnvironmentThreat.CanPass(구간, null));
    }

    [TestCase(EnvironmentHazard.Darkness, TraversalGear.Lantern)]
    [TestCase(EnvironmentHazard.Depth, TraversalGear.Swimming)]
    [TestCase(EnvironmentHazard.Gap, TraversalGear.Bridge)]
    [TestCase(EnvironmentHazard.MacroniumSurface, TraversalGear.SurfaceWalker)]
    public void 맞는_장비를_충분히_갖추면_지난다(EnvironmentHazard 위협, TraversalGear 장비종류)
    {
        var 구간 = new HazardZone(위협, 10f);
        Assert.IsTrue(EnvironmentThreat.CanPass(구간, 장비(new GearCapability(장비종류, 20f))));
    }

    [TestCase(EnvironmentHazard.Darkness)]
    [TestCase(EnvironmentHazard.Depth)]
    [TestCase(EnvironmentHazard.Gap)]
    [TestCase(EnvironmentHazard.MacroniumSurface)]
    public void 맨몸으로는_어떤_위협도_뚫지_못한다(EnvironmentHazard 위협)
    {
        var 구간 = new HazardZone(위협, 10f);
        var 판정 = EnvironmentThreat.Evaluate(구간, 장비());

        Assert.AreEqual(PassageResult.MissingGear, 판정.Result);
        Assert.AreEqual(위협, 판정.Hazard);
        Assert.AreEqual(EnvironmentThreat.RequiredGear(위협), 판정.RequiredGear);
    }

    [Test]
    public void 다른_위협용_장비는_이_구간을_뚫지_못한다()
    {
        // 랜턴을 아무리 좋은 것으로 들어도 액면은 걸어 건널 수 없다.
        var 액면 = new HazardZone(EnvironmentHazard.MacroniumSurface, 30f);
        var 판정 = EnvironmentThreat.Evaluate(액면, 장비(
            new GearCapability(TraversalGear.Lantern, 999f),
            new GearCapability(TraversalGear.Swimming, 999f),
            new GearCapability(TraversalGear.Bridge, 999f)));

        Assert.AreEqual(PassageResult.MissingGear, 판정.Result);
        Assert.AreEqual(TraversalGear.SurfaceWalker, 판정.RequiredGear);
    }

    [Test]
    public void 장비가_아예_없으면_위협이_작아도_막힌다()
    {
        // 관문은 "수단을 갖췄는가"이지 "크기가 작은가"가 아니다.
        var 구간 = new HazardZone(EnvironmentHazard.Depth, 0f);
        var 판정 = EnvironmentThreat.Evaluate(구간, 장비());

        Assert.AreEqual(PassageResult.MissingGear, 판정.Result);
        Assert.IsFalse(판정.CanPass);
    }

    [Test]
    public void 장비는_있는데_모자라면_없는_것과_다르게_보고한다()
    {
        var 구간 = new HazardZone(EnvironmentHazard.Gap, 18f);
        var 판정 = EnvironmentThreat.Evaluate(구간, 장비(new GearCapability(TraversalGear.Bridge, 12f)));

        Assert.AreEqual(PassageResult.NotEnough, 판정.Result);
        Assert.AreEqual(EnvironmentHazard.Gap, 판정.Hazard);
        Assert.AreEqual(TraversalGear.Bridge, 판정.RequiredGear);
        Assert.AreEqual(6f, 판정.Shortfall, 0.0001f);
    }

    [Test]
    public void 장비가_없으면_모자란_양은_위협의_크기_전부다()
    {
        var 구간 = new HazardZone(EnvironmentHazard.Depth, 25f);
        var 판정 = EnvironmentThreat.Evaluate(구간, 장비());

        Assert.AreEqual(25f, 판정.Shortfall, 0.0001f);
    }

    [Test]
    public void 통과한_판정은_아무것도_지목하지_않는다()
    {
        var 구간 = new HazardZone(EnvironmentHazard.Darkness, 8f);
        var 판정 = EnvironmentThreat.Evaluate(구간, 장비(new GearCapability(TraversalGear.Lantern, 14f)));

        Assert.IsTrue(판정.CanPass);
        Assert.AreEqual(EnvironmentHazard.None, 판정.Hazard);
        Assert.AreEqual(TraversalGear.None, 판정.RequiredGear);
        Assert.AreEqual(0f, 판정.Shortfall, 0.0001f);
    }

    // ── 경계값 ──────────────────────────────────────────────────────────────

    [Test]
    public void 용량이_위협의_크기와_같으면_지난다()
    {
        var 구간 = new HazardZone(EnvironmentHazard.Depth, 20f);
        Assert.IsTrue(EnvironmentThreat.CanPass(구간, 장비(new GearCapability(TraversalGear.Swimming, 20f))));
    }

    [Test]
    public void 용량이_조금이라도_모자라면_막힌다()
    {
        var 구간 = new HazardZone(EnvironmentHazard.Depth, 20f);
        Assert.IsFalse(EnvironmentThreat.CanPass(구간, 장비(new GearCapability(TraversalGear.Swimming, 19.999f))));
    }

    [Test]
    public void 용량이_조금이라도_넘으면_지난다()
    {
        var 구간 = new HazardZone(EnvironmentHazard.Depth, 20f);
        Assert.IsTrue(EnvironmentThreat.CanPass(구간, 장비(new GearCapability(TraversalGear.Swimming, 20.001f))));
    }

    [Test]
    public void 크기가_0인_구간은_용량_0짜리_장비로도_지난다()
    {
        var 구간 = new HazardZone(EnvironmentHazard.Gap, 0f);
        Assert.IsTrue(EnvironmentThreat.CanPass(구간, 장비(new GearCapability(TraversalGear.Bridge, 0f))));
    }

    // ── 목록 처리 ───────────────────────────────────────────────────────────

    [Test]
    public void 같은_장비가_여럿이면_가장_큰_용량을_쓴다()
    {
        // 합산(10+14=24)이 아니라 최댓값(14)이다 — 랜턴을 두 개 든다고 두 배 밝지 않다.
        var 구간 = new HazardZone(EnvironmentHazard.Darkness, 14f);
        var 목록 = 장비(
            new GearCapability(TraversalGear.Lantern, 10f),
            new GearCapability(TraversalGear.Lantern, 14f));

        Assert.IsTrue(EnvironmentThreat.CanPass(구간, 목록));
        Assert.IsFalse(EnvironmentThreat.CanPass(new HazardZone(EnvironmentHazard.Darkness, 24f), 목록));
    }

    [Test]
    public void 좋은_것을_먼저_들고_있어도_최댓값_규칙은_같다()
    {
        var 구간 = new HazardZone(EnvironmentHazard.Darkness, 14f);
        var 목록 = 장비(
            new GearCapability(TraversalGear.Lantern, 14f),
            new GearCapability(TraversalGear.Lantern, 10f));

        Assert.IsTrue(EnvironmentThreat.CanPass(구간, 목록));
    }

    [Test]
    public void 음수_용량도_가진_것으로_친다()
    {
        // 망가진 장비를 "없는 것"으로 바꿔 치지 않는다 — 없는 것과 모자란 것은 다른 사정이다.
        var 구간 = new HazardZone(EnvironmentHazard.Gap, 5f);
        var 판정 = EnvironmentThreat.Evaluate(구간, 장비(new GearCapability(TraversalGear.Bridge, -3f)));

        Assert.AreEqual(PassageResult.NotEnough, 판정.Result);
        Assert.AreEqual(8f, 판정.Shortfall, 0.0001f);
    }

    [Test]
    public void null_목록은_맨몸과_같다()
    {
        var 구간 = new HazardZone(EnvironmentHazard.Gap, 5f);
        Assert.AreEqual(PassageResult.MissingGear, EnvironmentThreat.Evaluate(구간, null).Result);
    }

    [Test]
    public void 상관없는_장비가_섞여_있어도_판정은_흔들리지_않는다()
    {
        var 구간 = new HazardZone(EnvironmentHazard.Gap, 18f);
        var 목록 = 장비(
            new GearCapability(TraversalGear.None, 999f),
            new GearCapability(TraversalGear.Lantern, 999f),
            new GearCapability(TraversalGear.Bridge, 18f));

        Assert.IsTrue(EnvironmentThreat.CanPass(구간, 목록));
    }

    // ── 티어 (스펙 §3: 섬 번호 = 티어 번호) ─────────────────────────────────

    [Test]
    public void 하위_티어_장비로는_상위_관문을_지나지_못한다()
    {
        // 스펙 §3의 관문 셋. 수심은 초, 폭과 액면은 미터다(HANDOFF §2.2의 실측 간격).
        var 여울 = new HazardZone(EnvironmentHazard.Depth, 20f);
        var 먼섬 = new HazardZone(EnvironmentHazard.Gap, 18f);
        var 액면 = new HazardZone(EnvironmentHazard.MacroniumSurface, 30f);

        var 수영까지 = 장비(new GearCapability(TraversalGear.Swimming, 20f));
        Assert.IsTrue(EnvironmentThreat.CanPass(여울, 수영까지));
        Assert.IsFalse(EnvironmentThreat.CanPass(먼섬, 수영까지));
        Assert.IsFalse(EnvironmentThreat.CanPass(액면, 수영까지));

        var 다리까지 = 장비(
            new GearCapability(TraversalGear.Swimming, 20f),
            new GearCapability(TraversalGear.Bridge, 18f));
        Assert.IsTrue(EnvironmentThreat.CanPass(먼섬, 다리까지));
        Assert.IsFalse(EnvironmentThreat.CanPass(액면, 다리까지));

        var 액면보행까지 = 장비(
            new GearCapability(TraversalGear.Swimming, 20f),
            new GearCapability(TraversalGear.Bridge, 18f),
            new GearCapability(TraversalGear.SurfaceWalker, 30f));
        Assert.IsTrue(EnvironmentThreat.CanPass(액면, 액면보행까지));
    }
}
