using NUnit.Framework;
using Survive.Harvesting;

/// <summary>
/// 거대 버섯 벌목의 순수 규칙.
///
/// 여기서 지키려는 계약은 둘이다.
/// <b>"무엇을 베는가"</b>와 <b>"언제 다시 서는가"</b>.
///
/// 첫째가 특히 중요하다 — 벌목 노드는 씬에 놓여 있지 않고 이름으로 골라
/// 실행 시점에 붙는다. 고르는 기준이 조용히 넓어지면 발밑의 장식 버섯까지
/// 곡괭이 대상이 되고, 그러면 무엇이 자원인지 화면에서 읽히지 않는다.
/// </summary>
public class MushroomLumberRuleTests
{
    // ── 무엇을 베는가 ────────────────────────────────────────

    [Test]
    public void 거대_버섯은_벌목_대상이다()
    {
        Assert.IsTrue(MushroomLumberRule.IsGiant("Mushroom_Glow_Giant_A_Fantasy"));
        Assert.IsTrue(MushroomLumberRule.IsGiant("Mushroom_Drip_Giant_Cluster_A_Fantasy"));
        Assert.IsTrue(MushroomLumberRule.IsGiant("Mushroom_Fluffy_Giant_C_Fantasy"));
    }

    [Test]
    public void 큰_버섯과_중간_버섯은_벌목_대상이_아니다()
    {
        // 씬에는 Big·Medium이 백 개 넘게 깔려 있다. 이것까지 베게 하면
        // 거대 버섯을 찾아 나설 이유가 사라진다.
        Assert.IsFalse(MushroomLumberRule.IsGiant("Mushroom_Jellyfish_Big_A_Fantasy"));
        Assert.IsFalse(MushroomLumberRule.IsGiant("Mushroom_Glow_Medium_Cluster_A_Fantasy"));
        Assert.IsFalse(MushroomLumberRule.IsGiant("Mushroom_Slim_Big_D_Fantasy"));
    }

    [Test]
    public void 버섯이_아닌_거대한_것은_벌목_대상이_아니다()
    {
        // 이름에 Giant가 들어간 바위나 생물이 나중에 놓일 수 있다.
        Assert.IsFalse(MushroomLumberRule.IsGiant("Rock_Giant_A"));
        Assert.IsFalse(MushroomLumberRule.IsGiant("Giant_Crab"));
    }

    [Test]
    public void 이름이_없으면_벌목_대상이_아니다()
    {
        Assert.IsFalse(MushroomLumberRule.IsGiant(null));
        Assert.IsFalse(MushroomLumberRule.IsGiant(""));
    }

    // ── 언제 다시 서는가 ─────────────────────────────────────

    [Test]
    public void 재생_시간이_지나기_전에는_돌아오지_않는다()
    {
        Assert.IsFalse(MushroomLumberRule.HasRegrown(100f, 100f, 300f));
        Assert.IsFalse(MushroomLumberRule.HasRegrown(100f, 399.9f, 300f));
    }

    [Test]
    public void 경계는_돌아온_것으로_본다()
    {
        // GlowGroveRule.HasRegrown과 같은 관례다. 한쪽만 >를 쓰면
        // 같은 초에 하나는 돌아오고 하나는 안 돌아온다.
        Assert.IsTrue(MushroomLumberRule.HasRegrown(100f, 400f, 300f));
    }

    [Test]
    public void 재생_시간이_0이면_즉시_돌아온다()
    {
        Assert.IsTrue(MushroomLumberRule.HasRegrown(100f, 100f, 0f));
    }

    // ── 밸런스 축 ────────────────────────────────────────────

    [Test]
    public void 그루터기_재생은_기존_재생보다_느리다()
    {
        // 발광 버섯 노드 90초, 군락 갓 180초. 나무 한 그루가 다시 서는 데는
        // 그보다 오래 걸려야 "다리 하나 = 거대 버섯 몇 그루"가 성립한다.
        Assert.Greater(MushroomLumberRule.RegrowSeconds, Survive.World.GlowGroveRule.RegrowSeconds);
    }

    [Test]
    public void 한_그루의_수확량은_범위로_정해져_있다()
    {
        Assert.Greater(MushroomLumberRule.MinYield, 0);
        Assert.GreaterOrEqual(MushroomLumberRule.MaxYield, MushroomLumberRule.MinYield);
    }

    [Test]
    public void 도끼_두_번이면_넘어간다()
    {
        // 도끼(Axe.asset)의 damage는 12다. 광맥 34는 곡괭이 세 번,
        // 거대 버섯 20은 도끼 두 번 — 살아 있는 것이 돌보다 무르다.
        const float 도끼damage = 12f;
        Assert.Greater(MushroomLumberRule.Durability, 도끼damage);
        Assert.LessOrEqual(MushroomLumberRule.Durability, 도끼damage * 2f);
    }
}
