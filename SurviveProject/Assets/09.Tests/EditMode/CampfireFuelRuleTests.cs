using NUnit.Framework;
using Survive.Building;
using Survive.Harvesting;

/// <summary>
/// 화톳불 연료의 순수 규칙.
///
/// 여기서 지키려는 계약은 셋이다.
/// <b>"타는 것은 목재뿐"</b>, <b>"가진 만큼만 넣는다"</b>, <b>"넘치지 않는다"</b>.
///
/// 첫째가 세계관이다 — 스크랩은 태우는 물건이 아니라 에너지를 담고 있는 매체이고,
/// 같은 불 앞에서 스크랩이 하는 일은 타는 것이 아니라 배터리로 짜여 나오는 것이다.
/// 이 구분이 무너지면 플레이어는 불에 무엇을 넣었을 때 무엇이 일어날지 알 수 없다.
/// </summary>
public class CampfireFuelRuleTests
{
    [Test]
    public void 연료는_버섯_목재다()
    {
        Assert.AreEqual(MushroomLumberRule.WoodItemId, CampfireFuelRule.FuelItemId);
        Assert.AreNotEqual("scrap", CampfireFuelRule.FuelItemId,
                           "스크랩은 연소재가 아니라 에너지 저장 매체다");
    }

    [Test]
    public void 한_번_넣으면_90초다()
    {
        // 스크랩 시절의 리듬을 그대로 옮겼다. 물질만 바뀌었을 뿐
        // 불을 지키러 돌아오는 주기가 달라질 이유는 없다.
        Assert.AreEqual(90f,
            CampfireFuelRule.SecondsPerLog * CampfireFuelRule.LogsPerRefuel, 1e-4f);
    }

    // ── 가진 만큼만 넣는다 ───────────────────────────────────

    [Test]
    public void 넉넉하면_정량을_넣는다()
    {
        Assert.AreEqual(2, CampfireFuelRule.LogsToTake(10, 2));
    }

    [Test]
    public void 모자라면_가진_만큼만_넣는다()
    {
        // 하나뿐이라고 거절하면 어두운 데서 손에 든 것을 못 쓴다.
        Assert.AreEqual(1, CampfireFuelRule.LogsToTake(1, 2));
    }

    [Test]
    public void 하나도_없으면_넣지_않는다()
    {
        Assert.AreEqual(0, CampfireFuelRule.LogsToTake(0, 2));
        Assert.AreEqual(0, CampfireFuelRule.LogsToTake(-3, 2));
    }

    // ── 넘치지 않는다 ────────────────────────────────────────

    [Test]
    public void 넣은_만큼_연료가_는다()
    {
        Assert.AreEqual(90f, CampfireFuelRule.AfterRefuel(0f, 2, 45f, 180f), 1e-4f);
        Assert.AreEqual(135f, CampfireFuelRule.AfterRefuel(90f, 1, 45f, 180f), 1e-4f);
    }

    [Test]
    public void 최대치를_넘지_않는다()
    {
        Assert.AreEqual(180f, CampfireFuelRule.AfterRefuel(170f, 2, 45f, 180f), 1e-4f);
    }

    [Test]
    public void 넣을_것이_없으면_연료는_그대로다()
    {
        Assert.AreEqual(30f, CampfireFuelRule.AfterRefuel(30f, 0, 45f, 180f), 1e-4f);
    }

    // ── 탄다 ─────────────────────────────────────────────────

    [Test]
    public void 시간이_지난_만큼_탄다()
    {
        Assert.AreEqual(85f, CampfireFuelRule.AfterBurn(90f, 5f), 1e-4f);
    }

    [Test]
    public void 연료는_0_아래로_내려가지_않는다()
    {
        Assert.AreEqual(0f, CampfireFuelRule.AfterBurn(2f, 10f), 1e-4f);
    }

    [Test]
    public void 연료가_남아_있으면_타고_있다()
    {
        Assert.IsTrue(CampfireFuelRule.IsBurning(0.01f));
        Assert.IsFalse(CampfireFuelRule.IsBurning(0f));
    }

    [Test]
    public void 한_그루로_불을_얼마나_지키는지()
    {
        // 밸런스 축의 반대편. 거대 버섯 한 그루(평균 5개)면
        // 두 번 반 넣을 수 있고 그동안 불은 3분 넘게 산다.
        float 한그루평균 = (MushroomLumberRule.MinYield + MushroomLumberRule.MaxYield) / 2f;
        float 초 = 한그루평균 * CampfireFuelRule.SecondsPerLog;
        Assert.Greater(초, 180f, "한 그루가 불 한 통(180초)도 못 채우면 벌목이 노동이 된다");
    }
}
