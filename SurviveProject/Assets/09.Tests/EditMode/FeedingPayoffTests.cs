using NUnit.Framework;
using Survive.Creatures;

/// <summary>
/// 기획서 §3.4 — 관찰의 보상이 <b>얼마나 큰가</b>.
///
/// 이 축은 크기가 곧 성립 여부다. 배부른 개체를 골라 두세 개 더 나오는 정도라면
/// 아무도 안 고르고 가까운 것을 잡는 것이 최적해가 된다. 그래서 배율이
/// <b>규칙으로</b> 서 있어야 하고, 경계에서 흔들리면 안 된다 — 그것이 여기서
/// 확인하는 전부다.
///
/// <b>값을 정하는 시험이 아니다.</b> "몇 배여야 하는가"는 사람의 몫(§16)이고,
/// 여기서는 "지금 셈이 무엇을 내놓는가"만 못 박는다.
/// </summary>
public class FeedingPayoffTests
{
    const float 허용오차 = 1e-4f;

    // ── 축적 → 스크랩 ───────────────────────────────────────────

    [Test]
    public void 굶은_개체는_한_개도_더_주지_않는다()
    {
        Assert.AreEqual(0, FeedingPayoff.Bonus(0f));
    }

    [Test]
    public void 음수는_0으로_눌린다()
    {
        // 축적이 음수가 될 길은 없지만, 있다면 그것은 스크랩을 빼앗아 가는 것이 된다.
        Assert.AreEqual(0, FeedingPayoff.Bonus(-7f));
    }

    [Test]
    public void 축적한_만큼_그대로_붙는다()
    {
        Assert.AreEqual(12, FeedingPayoff.Bonus(12f));
        Assert.AreEqual(3, FeedingPayoff.Bonus(3f));
    }

    [Test]
    public void 반올림은_짝수로_간다()
    {
        // Mathf.RoundToInt의 결이다. 지금 게임이 실제로 내놓는 개수가 이것이므로
        // 여기서 바꾸면 실측이 실측이 아니게 된다.
        Assert.AreEqual(0, FeedingPayoff.Bonus(0.5f));
        Assert.AreEqual(2, FeedingPayoff.Bonus(1.5f));
        Assert.AreEqual(2, FeedingPayoff.Bonus(2.5f));
    }

    [Test]
    public void 한_입도_안_되는_양은_0개다()
    {
        // 0.4는 한 개도 안 된다. "먹긴 먹었는데 아무것도 안 나온다"가 성립한다.
        Assert.AreEqual(0, FeedingPayoff.Bonus(0.4f));
    }

    // ── 배율 ────────────────────────────────────────────────────

    [Test]
    public void 굶은_개체의_배율은_1배다()
    {
        Assert.AreEqual(1f, FeedingPayoff.Multiplier(3f, 0f), 허용오차);
    }

    [Test]
    public void 기본_드롭에_축적이_얹혀_배율이_난다()
    {
        // 기본 3개짜리가 12를 쌓으면 15개 = 5배.
        Assert.AreEqual(5f, FeedingPayoff.Multiplier(3f, 12f), 허용오차);
    }

    [Test]
    public void 기본_드롭이_작을수록_같은_축적이_더_큰_배율이_된다()
    {
        // 같은 12를 쌓아도 기본이 1.5인 종은 9배, 3인 종은 5배다.
        // <b>배율만 보면 안 되는 이유</b>가 여기 있다 — 실제로 더 얻는 개수는 같다.
        Assert.AreEqual(9f, FeedingPayoff.Multiplier(1.5f, 12f), 허용오차);
        Assert.AreEqual(5f, FeedingPayoff.Multiplier(3f, 12f), 허용오차);
        Assert.AreEqual(FeedingPayoff.Bonus(12f), 12);
    }

    [Test]
    public void 기본_드롭이_없으면_잴_수_없다()
    {
        // 「무한 배」는 답이 아니다. 표에서 그 칸만 유독 좋아 보이면 안 된다.
        Assert.AreEqual(0f, FeedingPayoff.Multiplier(0f, 12f), 허용오차);
        Assert.AreEqual(0f, FeedingPayoff.Multiplier(-2f, 12f), 허용오차);
    }

    // ── 몇 입인가 ───────────────────────────────────────────────

    [Test]
    public void 정원을_한_입에_못_채우면_올림한다()
    {
        // 정원 12, 한 입 5 → 세 입(마지막 입은 넘친다)
        Assert.AreEqual(3, FeedingPayoff.BitesToFull(12f, 5f));
    }

    [Test]
    public void 딱_나누어떨어지면_그_수만큼이다()
    {
        Assert.AreEqual(6, FeedingPayoff.BitesToFull(12f, 2f));
        Assert.AreEqual(4, FeedingPayoff.BitesToFull(12f, 3f));
    }

    [Test]
    public void 정원이_0이면_처음부터_배부르다()
    {
        Assert.AreEqual(0, FeedingPayoff.BitesToFull(0f, 2f));
    }

    [Test]
    public void 영양가가_없는_먹이로는_영영_안_찬다()
    {
        Assert.AreEqual(FeedingPayoff.NeverFull, FeedingPayoff.BitesToFull(12f, 0f));
        Assert.AreEqual(FeedingPayoff.NeverFull, FeedingPayoff.BitesToFull(12f, -1f));
    }

    // ── 몇 초인가 ───────────────────────────────────────────────

    [Test]
    public void 첫_입은_기다리지_않는다()
    {
        // 여섯 입이면 간격은 다섯 번. 6×6=36이 아니라 5×6=30이다.
        Assert.AreEqual(30f, FeedingPayoff.SecondsToFull(12f, 2f, 6f), 허용오차);
    }

    [Test]
    public void 한_입이면_기다림이_없다()
    {
        Assert.AreEqual(0f, FeedingPayoff.SecondsToFull(12f, 12f, 6f), 허용오차);
        Assert.AreEqual(0f, FeedingPayoff.SecondsToFull(12f, 99f, 6f), 허용오차);
    }

    [Test]
    public void 영영_안_차면_시간도_무한이다()
    {
        Assert.IsTrue(float.IsPositiveInfinity(FeedingPayoff.SecondsToFull(12f, 0f, 6f)));
    }

    [Test]
    public void 이_시간은_바닥이지_실제가_아니다()
    {
        // 찾아가는 시간도 식물이 다시 자라는 시간도 들어 있지 않다.
        // 규칙이 내놓는 값은 언제나 세계에서 잰 값보다 짧아야 한다.
        Assert.Less(FeedingPayoff.SecondsToFull(12f, 2f, 6f), 40f);
    }

    // ── 기다린 시간 대 더 얻은 것 ───────────────────────────────

    [Test]
    public void 기다린_1분이_만드는_스크랩이_셈으로_나온다()
    {
        // 12개를 30초에 → 분당 24개
        Assert.AreEqual(24f, FeedingPayoff.BonusPerMinute(12f, 2f, 6f), 허용오차);
    }

    [Test]
    public void 더_영양가_있는_먹이는_같은_이득을_더_빨리_만든다()
    {
        float 양치 = FeedingPayoff.BonusPerMinute(12f, 2f, 6f);   // 6입 · 30초
        float 버섯 = FeedingPayoff.BonusPerMinute(12f, 3f, 6f);   // 4입 · 18초
        Assert.Greater(버섯, 양치);
    }

    [Test]
    public void 기다림이_0인데_이득이_있으면_공짜다()
    {
        // 한 입에 배부르면 기다린 시간이 없다. 그것은 표에 그대로 적혀야 하는 사실이다.
        Assert.IsTrue(float.IsPositiveInfinity(FeedingPayoff.BonusPerMinute(12f, 12f, 6f)));
    }

    [Test]
    public void 영영_안_차면_분당_이득은_0이다()
    {
        Assert.AreEqual(0f, FeedingPayoff.BonusPerMinute(12f, 0f, 6f), 허용오차);
    }

    [Test]
    public void 정원이_0이면_이득도_0이다()
    {
        Assert.AreEqual(0f, FeedingPayoff.BonusPerMinute(0f, 2f, 6f), 허용오차);
    }

    // ── 표현과 이득이 같은 눈금을 쓴다 ──────────────────────────

    [Test]
    public void 포만도가_1인_지점이_최대_이득_지점이다()
    {
        // 플레이어는 표현(부풂·색)을 보고 이득을 고른다. 표현이 가득 찼는데
        // 이득이 더 늘어난다면, 눈으로 고르는 일이 최적해가 아니게 된다.
        const float 정원 = 12f;
        Assert.IsTrue(FeedingStore.IsFull(정원, 정원));
        Assert.AreEqual(1f, FeedingStore.Fullness(정원, 정원), 허용오차);
        Assert.AreEqual(FeedingPayoff.Bonus(정원), 12);
    }
}
