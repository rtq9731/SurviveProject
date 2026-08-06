using System.Collections.Generic;
using NUnit.Framework;
using Survive.World;

/// <summary>
/// 백로그 36 — "챕터 1은 남의 장치를 켜는 것이 아니라 짙은 매크로늄 층을 뚫고 내려가며 끝난다".
///
/// 두 가지를 본다.
/// <list type="number">
/// <item>액면에 닿았을 때 돌파정이 결과를 어떻게 바꾸는가 — 죽던 자리에서 가라앉는다</item>
/// <item>층을 다 지났는가 — 종막이 열리는 조건</item>
/// </list>
///
/// 규칙은 Unity 없이 도는 순수 정적 클래스이므로 씬도 MonoBehaviour도 쓰지 않는다.
/// 그 규칙이 실제로 사람을 내려보내고 챕터를 끝내는지는 <c>E2EDescent</c>가 본다.
/// </summary>
public class MacroniumDescentTests
{
    static List<GearCapability> 장비(params GearCapability[] 목록) => new List<GearCapability>(목록);

    static HazardZone 액면(float 폭 = 30f) => new HazardZone(EnvironmentHazard.MacroniumSurface, 폭);
    static HazardZone 층(float 두께 = 12f) => new HazardZone(EnvironmentHazard.MacroniumLayer, 두께);

    static GearCapability 보행기(float 용량 = 36f) => new GearCapability(TraversalGear.SurfaceWalker, 용량);
    static GearCapability 돌파정(float 용량 = 20f) => new GearCapability(TraversalGear.BreachPod, 용량);

    // ── 장비표가 닫혀 있는가 ───────────────────────────────────────────────

    [Test]
    public void 층을_뚫는_것은_돌파정이다()
    {
        Assert.AreEqual(TraversalGear.BreachPod,
                        EnvironmentThreat.RequiredGear(EnvironmentHazard.MacroniumLayer));
    }

    [Test]
    public void 액면_위를_걷는_것은_여전히_액면_보행_장비다()
    {
        // 같은 물질이라고 해서 한 장비가 둘을 다 하지는 않는다.
        // 위를 걷는 것과 뚫고 내려가는 것은 다른 물음이다.
        Assert.AreEqual(TraversalGear.SurfaceWalker,
                        EnvironmentThreat.RequiredGear(EnvironmentHazard.MacroniumSurface));
    }

    // ── 액면 접촉 — 돌파정이 결과를 바꾼다 ─────────────────────────────────

    [Test]
    public void 맨몸으로_닿으면_여전히_죽는다()
    {
        // 종막이 생겼다고 해서 액면이 순해지지 않는다. 기획서 §6.2 —
        // "액면은 여전히 맨몸에게 즉사이고, 바뀌는 것은 무엇을 걸쳤는가뿐이다".
        Assert.AreEqual(MacroniumContactOutcome.Lethal,
                        MacroniumContact.Resolve(true, 액면(), 장비()));
    }

    [Test]
    public void 돌파정만_지니면_닿는_즉시_가라앉는다()
    {
        // 걸을 수단이 없으니 남는 것은 내려가는 것뿐이다. 그리고 그것은 사고가 아니다.
        Assert.AreEqual(MacroniumContactOutcome.Descending,
                        MacroniumContact.Resolve(true, 액면(), 장비(돌파정())));
    }

    [Test]
    public void 돌파정을_지니면_하강을_누르지_않아도_죽지는_않는다()
    {
        var 결과 = MacroniumContact.Resolve(true, 액면(), 장비(돌파정()), descentRequested: false);
        Assert.AreNotEqual(MacroniumContactOutcome.Lethal, 결과);
    }

    [Test]
    public void 둘_다_지니면_기본은_액면_위를_걷는_것이다()
    {
        // 종막에 이른 사람은 반드시 둘 다 지니고 있다(§5.4의 사슬).
        // 여기서 가라앉기가 기본이면 4번 섬으로 돌아가려다 챕터가 끝난다.
        Assert.AreEqual(MacroniumContactOutcome.Supported,
                        MacroniumContact.Resolve(true, 액면(), 장비(보행기(), 돌파정()),
                                                 descentRequested: false));
    }

    [Test]
    public void 둘_다_지닌_채_하강을_누르면_가라앉는다()
    {
        Assert.AreEqual(MacroniumContactOutcome.Descending,
                        MacroniumContact.Resolve(true, 액면(), 장비(보행기(), 돌파정()),
                                                 descentRequested: true));
    }

    [Test]
    public void 돌파정이_없으면_하강을_눌러도_아무것도_달라지지_않는다()
    {
        // 키를 누르는 것만으로 액면이 열리면 장비가 관문이 아니게 된다.
        Assert.AreEqual(MacroniumContactOutcome.Supported,
                        MacroniumContact.Resolve(true, 액면(), 장비(보행기()), descentRequested: true));
        Assert.AreEqual(MacroniumContactOutcome.Lethal,
                        MacroniumContact.Resolve(true, 액면(), 장비(), descentRequested: true));
    }

    [Test]
    public void 액면_보행_장비의_용량이_모자라도_돌파정이_있으면_죽지_않는다()
    {
        // 걷기로는 못 지나는 폭이다. 하지만 껍데기를 걸친 사람은 빠져 죽는 것이 아니라
        // 내려간다 — "못 걷는다"의 결말이 죽음에서 하강으로 바뀐다.
        Assert.AreEqual(MacroniumContactOutcome.Descending,
                        MacroniumContact.Resolve(true, 액면(폭: 40f),
                                                 장비(보행기(용량: 10f), 돌파정())));
    }

    [Test]
    public void 닿지_않았으면_돌파정이_있어도_아무_일도_없다()
    {
        Assert.AreEqual(MacroniumContactOutcome.None,
                        MacroniumContact.Resolve(false, 액면(), 장비(돌파정()), descentRequested: true));
    }

    [Test]
    public void 액면이_아닌_구역에서는_하강을_눌러도_아무_일도_없다()
    {
        var 어둠 = new HazardZone(EnvironmentHazard.Darkness, 20f);
        Assert.AreEqual(MacroniumContactOutcome.None,
                        MacroniumContact.Resolve(true, 어둠, 장비(돌파정()), descentRequested: true));
    }

    [Test]
    public void 껍데기를_걸쳤는가는_용량을_보지_않는다()
    {
        // 얼마나 깊은 층을 뚫는지는 층이 묻는다. 접촉은 걸쳤는가만 본다.
        Assert.IsTrue(MacroniumContact.HasHull(장비(new GearCapability(TraversalGear.BreachPod, 0f))));
        Assert.IsFalse(MacroniumContact.HasHull(장비(보행기())));
        Assert.IsFalse(MacroniumContact.HasHull(null));
    }

    // ── 층을 다 내려갔는가 ─────────────────────────────────────────────────

    [Test]
    public void 층의_아랫면은_윗면에서_두께만큼_아래다()
    {
        Assert.AreEqual(38f, MacroniumDescent.BottomY(layerTopY: 50f, thickness: 12f), 0.0001f);
    }

    [Test]
    public void 아랫면을_넘어서면_뚫은_것이다()
    {
        Assert.IsTrue(MacroniumDescent.Breached(feetY: 37.9f, layerTopY: 50f, 층(), 장비(돌파정())));
    }

    [Test]
    public void 아랫면에_정확히_닿은_것도_뚫은_것으로_친다()
    {
        // 경계값은 통과로 친다 — EnvironmentThreat이 용량에서 잡은 규칙과 같다.
        Assert.IsTrue(MacroniumDescent.Breached(feetY: 38f, layerTopY: 50f, 층(), 장비(돌파정())));
    }

    [Test]
    public void 층_속에_있는_동안은_아직_뚫은_것이_아니다()
    {
        Assert.IsFalse(MacroniumDescent.Breached(feetY: 44f, layerTopY: 50f, 층(), 장비(돌파정())));
    }

    [Test]
    public void 액면_위에_서_있으면_뚫은_것이_아니다()
    {
        Assert.IsFalse(MacroniumDescent.Breached(feetY: 50f, layerTopY: 50f, 층(), 장비(돌파정())));
    }

    [Test]
    public void 돌파정_없이_그_깊이에_있어도_뚫은_것이_아니다()
    {
        // 지형의 틈 하나가 챕터를 끝내면 안 된다.
        Assert.IsFalse(MacroniumDescent.Breached(feetY: 10f, layerTopY: 50f, 층(), 장비()));
        Assert.IsFalse(MacroniumDescent.Breached(feetY: 10f, layerTopY: 50f, 층(), 장비(보행기())));
    }

    [Test]
    public void 용량이_모자란_껍데기로는_층을_뚫지_못한다()
    {
        Assert.IsFalse(MacroniumDescent.Breached(feetY: 10f, layerTopY: 50f,
                                                 층(두께: 30f), 장비(돌파정(용량: 12f))));
    }

    [Test]
    public void 용량이_두께와_같으면_뚫는다()
    {
        Assert.IsTrue(MacroniumDescent.Breached(feetY: 10f, layerTopY: 50f,
                                                층(두께: 20f), 장비(돌파정(용량: 20f))));
    }

    [Test]
    public void 층이_아닌_구간은_아무리_내려가도_뚫리지_않는다()
    {
        Assert.IsFalse(MacroniumDescent.Breached(feetY: -100f, layerTopY: 50f,
                                                 액면(), 장비(돌파정())));
    }

    [Test]
    public void 모자란_것을_판정이_말해_준다()
    {
        var 결과 = MacroniumDescent.Evaluate(층(두께: 30f), 장비());
        Assert.AreEqual(PassageResult.MissingGear, 결과.Result);
        Assert.AreEqual(TraversalGear.BreachPod, 결과.RequiredGear);
        Assert.AreEqual(EnvironmentHazard.MacroniumLayer, 결과.Hazard);

        var 모자람 = MacroniumDescent.Evaluate(층(두께: 30f), 장비(돌파정(용량: 12f)));
        Assert.AreEqual(PassageResult.NotEnough, 모자람.Result);
        Assert.AreEqual(18f, 모자람.Shortfall, 0.0001f);
    }

    // ── 얼마나 내려왔는가 ──────────────────────────────────────────────────

    [Test]
    public void 깊이는_윗면에서_발까지의_거리다()
    {
        Assert.AreEqual(4f, MacroniumDescent.DepthBelow(feetY: 46f, layerTopY: 50f), 0.0001f);
    }

    [Test]
    public void 아직_액면_위면_깊이가_음수다()
    {
        Assert.Less(MacroniumDescent.DepthBelow(feetY: 52f, layerTopY: 50f), 0f);
    }

    [Test]
    public void 남은_깊이는_액면_위에서_두께_전부이고_다_내려가면_0이다()
    {
        Assert.AreEqual(12f, MacroniumDescent.RemainingDepth(52f, 50f, 12f), 0.0001f);
        Assert.AreEqual(12f, MacroniumDescent.RemainingDepth(50f, 50f, 12f), 0.0001f);
        Assert.AreEqual(7f, MacroniumDescent.RemainingDepth(45f, 50f, 12f), 0.0001f);
        Assert.AreEqual(0f, MacroniumDescent.RemainingDepth(38f, 50f, 12f), 0.0001f);
        Assert.AreEqual(0f, MacroniumDescent.RemainingDepth(10f, 50f, 12f), 0.0001f);
    }
}
