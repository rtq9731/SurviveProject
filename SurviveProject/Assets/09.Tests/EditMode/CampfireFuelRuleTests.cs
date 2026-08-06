using NUnit.Framework;
using Survive.Building;
using Survive.Harvesting;

/// <summary>
/// 화톳불 연료의 순수 규칙.
///
/// 여기서 지키려는 계약은 넷이다.
/// <b>"타는 것은 목재뿐"</b>, <b>"가진 만큼만 넣는다"</b>,
/// <b>"들어갈 만큼만 넣는다"</b>, <b>"목재 N개는 정확히 T초다"</b>.
///
/// 첫째가 세계관이다 — 스크랩은 태우는 물건이 아니라 에너지를 담고 있는 매체이고,
/// 같은 불 앞에서 스크랩이 하는 일은 타는 것이 아니라 배터리로 짜여 나오는 것이다.
/// 이 구분이 무너지면 플레이어는 불에 무엇을 넣었을 때 무엇이 일어날지 알 수 없다.
///
/// 셋째가 이번 라운드에서 새로 세운 것이다. 예전에는 남은 자리를 보지 않고
/// 정량을 받아 갔고, 상한에 잘린 몫만큼 배낭의 목재가 그대로 사라졌다.
/// <b>목재 재고가 안전 재고</b>라는 설계(기획서 §5.3)에서 재고가 조용히 증발하면
/// 축 자체가 성립하지 않는다.
/// </summary>
public class CampfireFuelRuleTests
{
    // 규칙 상수를 그대로 쓴다. 수치는 아직 확정이 아니라(상세기획서 §13)
    // 값을 테스트에 베껴 두면 손잡이를 돌릴 때마다 테스트가 거짓으로 깨진다.
    static float PerLog => CampfireFuelRule.SecondsPerLog;
    static float Max => CampfireFuelRule.MaxFuelSeconds;

    [Test]
    public void 연료는_버섯_목재다()
    {
        Assert.AreEqual(MushroomLumberRule.WoodItemId, CampfireFuelRule.FuelItemId);
        Assert.AreNotEqual("scrap", CampfireFuelRule.FuelItemId,
                           "스크랩은 연소재가 아니라 에너지 저장 매체다");
    }

    // ── 목재 N개 → 유지 시간 T ───────────────────────────────

    [Test]
    public void 목재_한_개는_한_개분만큼_탄다()
    {
        Assert.AreEqual(PerLog, CampfireFuelRule.SecondsFor(1, PerLog, Max), 1e-4f);
    }

    [Test]
    public void 목재_영_개는_한_순간도_타지_않는다()
    {
        // 경계값. 0을 45초로 세면 빈손으로도 불이 붙는다.
        Assert.AreEqual(0f, CampfireFuelRule.SecondsFor(0, PerLog, Max), 1e-4f);
        Assert.AreEqual(0f, CampfireFuelRule.SecondsFor(-3, PerLog, Max), 1e-4f);
    }

    [Test]
    public void 목재_N개는_N배_탄다()
    {
        for (int n = 1; n <= CampfireFuelRule.CapacityLogs; n++)
            Assert.AreEqual(n * PerLog, CampfireFuelRule.SecondsFor(n, PerLog, Max), 1e-4f,
                            $"목재 {n}개");
    }

    [Test]
    public void 가득_채우면_용량만큼_탄다()
    {
        Assert.AreEqual(Max,
            CampfireFuelRule.SecondsFor(CampfireFuelRule.CapacityLogs, PerLog, Max), 1e-4f);
    }

    [Test]
    public void 용량보다_많이_세어도_용량까지다()
    {
        // 경계값 바깥. 배낭에 백 개가 있어도 불 하나가 품는 것은 정해져 있다.
        Assert.AreEqual(Max,
            CampfireFuelRule.SecondsFor(CampfireFuelRule.CapacityLogs + 1, PerLog, Max), 1e-4f);
        Assert.AreEqual(Max, CampfireFuelRule.SecondsFor(100, PerLog, Max), 1e-4f);
    }

    [Test]
    public void 세우자마자_주는_불씨는_비율이_아니라_고정이다()
    {
        // 용량의 절반 같은 비율로 두면 용량을 올릴 때마다 공짜 목재가 함께 는다.
        Assert.AreEqual(CampfireFuelRule.StarterLogs * PerLog,
                        CampfireFuelRule.StarterFuelSeconds, 1e-4f);
        Assert.Less(CampfireFuelRule.StarterLogs, CampfireFuelRule.CapacityLogs,
                    "세우자마자 가득 차 있으면 목재를 넣을 이유가 없다");
        Assert.Greater(CampfireFuelRule.StarterFuelSeconds, 0f,
                       "세우자마자 꺼져 있으면 무엇을 지은 건지 알 수 없다");
    }

    // ── 가진 만큼만 넣는다 ───────────────────────────────────

    [Test]
    public void 넉넉하면_정량을_넣는다()
    {
        Assert.AreEqual(CampfireFuelRule.LogsPerRefuel,
            CampfireFuelRule.LogsToTake(99, CampfireFuelRule.LogsPerRefuel, 0f, PerLog, Max));
    }

    [Test]
    public void 모자라면_가진_만큼만_넣는다()
    {
        // 하나뿐이라고 거절하면 어두운 데서 손에 든 것을 못 쓴다.
        Assert.AreEqual(1, CampfireFuelRule.LogsToTake(1, 5, 0f, PerLog, Max));
    }

    [Test]
    public void 하나도_없으면_넣지_않는다()
    {
        Assert.AreEqual(0, CampfireFuelRule.LogsToTake(0, 5, 0f, PerLog, Max));
        Assert.AreEqual(0, CampfireFuelRule.LogsToTake(-3, 5, 0f, PerLog, Max));
    }

    // ── 들어갈 만큼만 넣는다 ─────────────────────────────────

    [Test]
    public void 빈_불에는_용량만큼_자리가_있다()
    {
        Assert.AreEqual(CampfireFuelRule.CapacityLogs,
                        CampfireFuelRule.RoomInLogs(0f, PerLog, Max));
    }

    [Test]
    public void 가득_찬_불에는_자리가_없다()
    {
        Assert.AreEqual(0, CampfireFuelRule.RoomInLogs(Max, PerLog, Max));
        Assert.AreEqual(0, CampfireFuelRule.LogsToTake(99, 5, Max, PerLog, Max),
                        "가득 찬 불에 넣으면 그 목재는 그냥 사라진다");
    }

    [Test]
    public void 한_개도_안_들어가는_자리는_없는_자리다()
    {
        // 경계값. 한 개분에서 1초 모자라면 그 한 개는 넣어 봐야 잘린다.
        Assert.AreEqual(0, CampfireFuelRule.RoomInLogs(Max - PerLog + 1f, PerLog, Max));
        Assert.AreEqual(1, CampfireFuelRule.RoomInLogs(Max - PerLog, PerLog, Max));
    }

    [Test]
    public void 넣은_목재는_한_개도_증발하지_않는다()
    {
        // 이 규칙의 존재 이유. 어떤 연료 상태에서도
        // "넣기로 한 수 × 한 개분"이 통째로 연료에 얹혀야 한다.
        for (int fuelLogs = 0; fuelLogs <= CampfireFuelRule.CapacityLogs; fuelLogs++)
        {
            float fuel = fuelLogs * PerLog;
            int take = CampfireFuelRule.LogsToTake(99, CampfireFuelRule.LogsPerRefuel,
                                                   fuel, PerLog, Max);
            float after = CampfireFuelRule.AfterRefuel(fuel, take, PerLog, Max);
            Assert.AreEqual(fuel + take * PerLog, after, 1e-3f,
                            $"연료 {fuel}초에 {take}개를 넣었는데 그만큼 오르지 않았다");
        }
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
    public void 연료가_바닥나는_순간_불이_꺼진다()
    {
        // 경계값. 마지막 한 방울이 남아 있는 동안은 아직 타는 중이고,
        // 정확히 0이 되는 프레임에 꺼진다 — 그 프레임에 밝은 구역도 사라진다.
        float 한방울 = 0.01f;
        Assert.IsTrue(CampfireFuelRule.IsBurning(한방울));
        Assert.IsFalse(CampfireFuelRule.IsBurning(CampfireFuelRule.AfterBurn(한방울, 한방울)));
    }

    [Test]
    public void 연료가_남아_있으면_타고_있다()
    {
        Assert.IsTrue(CampfireFuelRule.IsBurning(0.01f));
        Assert.IsFalse(CampfireFuelRule.IsBurning(0f));
    }

    // ── 밸런스 축 ────────────────────────────────────────────

    [Test]
    public void 한_그루로_불을_얼마나_지키는지()
    {
        // 밸런스 축의 반대편. 거대 버섯 한 그루(평균 5개)면 3분 넘게 산다.
        float 한그루평균 = (MushroomLumberRule.MinYield + MushroomLumberRule.MaxYield) / 2f;
        float 초 = 한그루평균 * CampfireFuelRule.SecondsPerLog;
        Assert.Greater(초, 180f, "한 그루가 3분도 못 버티면 벌목이 노동이 된다");
    }

    [Test]
    public void 불_하나를_가득_채우는_데_두세_그루가_든다()
    {
        // 다리 게이트가 빠지며 "다리 하나 = 아홉 그루"가 없어졌다. 그 축을
        // "전진 기지 하나 = 몇 그루"가 대신한다(기획서 §5.3). 전진 기지 하나는
        // 가득 채운 불 하나이므로, 그 값이 곧 이 게임의 벌목 압력이다.
        float 한그루평균 = (MushroomLumberRule.MinYield + MushroomLumberRule.MaxYield) / 2f;
        float 그루수 = CampfireFuelRule.CapacityLogs / 한그루평균;

        Assert.GreaterOrEqual(그루수, 1.5f,
            $"기지 하나가 {그루수:F1}그루면 벌목이 값어치를 잃는다");
        Assert.LessOrEqual(그루수, 4f,
            $"기지 하나가 {그루수:F1}그루면 전진 기지를 여러 개 깔 수 없다");
    }

    [Test]
    public void 재고를_불에_맡기고_떠날_수_있다()
    {
        // "목재 재고 = 안전 재고"의 최소 조건. 한 번 넣고 돌아서야 하는 크기면
        // 재고는 안전이 아니라 불 앞에 묶이는 시간이 된다.
        Assert.GreaterOrEqual(CampfireFuelRule.CapacityLogs,
                              CampfireFuelRule.LogsPerRefuel * 2,
                              "한 번에 가득 차면 재고를 쌓을 이유가 없다");
        Assert.Greater(CampfireFuelRule.MaxFuelSeconds, 300f,
                       "가득 채운 불이 5분도 못 가면 원정을 나갈 수 없다");
    }
}
