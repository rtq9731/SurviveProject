using NUnit.Framework;
using Survive.Creatures;

/// <summary>
/// 백로그 15 — CreatureFeeding에서 뽑아낸 포만도 계산.
///
/// 이 값 하나가 두 곳을 동시에 정한다. 더 먹으러 갈지 말지(판단)와,
/// 몸이 얼마나 부풀고 접합부가 얼마나 밝은지(표현)다.
/// 플레이어는 표현을 보고 판단을 읽는다 — 그래서 둘이 어긋나면 안 된다.
/// </summary>
public class FeedingStoreTests
{
    const float 허용오차 = 1e-4f;
    const float 정원 = 12f;

    [Test]
    public void 텅_비면_0이다()
    {
        Assert.AreEqual(0f, FeedingStore.Fullness(0f, 정원), 허용오차);
    }

    [Test]
    public void 절반_먹으면_절반이다()
    {
        Assert.AreEqual(0.5f, FeedingStore.Fullness(6f, 정원), 허용오차);
    }

    [Test]
    public void 정원을_넘겨도_1을_넘지_않는다()
    {
        // 한 입에 정원을 훌쩍 넘는 영양가를 먹을 수 있다. 몸이 무한히 부풀면 안 된다.
        Assert.AreEqual(1f, FeedingStore.Fullness(정원 * 3f, 정원), 허용오차);
    }

    [Test]
    public void 음수는_0으로_눌린다()
    {
        Assert.AreEqual(0f, FeedingStore.Fullness(-5f, 정원), 허용오차);
    }

    [Test]
    public void 정원이_0이면_0으로_본다()
    {
        // 나눌 수 없다. 텅 빈 것으로 그린다.
        Assert.AreEqual(0f, FeedingStore.Fullness(5f, 0f), 허용오차);
        Assert.AreEqual(0f, FeedingStore.Fullness(5f, -1f), 허용오차);
    }

    [Test]
    public void 정확히_가득_찬_것도_배부른_것이다()
    {
        Assert.IsTrue(FeedingStore.IsFull(정원, 정원));
    }

    [Test]
    public void 한_눈금_모자라면_아직_먹으러_간다()
    {
        Assert.IsFalse(FeedingStore.IsFull(정원 - 0.001f, 정원));
    }

    [Test]
    public void 넘치게_먹었으면_당연히_배부르다()
    {
        Assert.IsTrue(FeedingStore.IsFull(정원 + 1f, 정원));
    }

    [Test]
    public void 배부른_경계와_포만도_1이_같은_지점이다()
    {
        // 표현이 가득 찼는데 아직 먹으러 가거나, 다 먹었는데 덜 부푼 일이 없어야 한다.
        Assert.IsTrue(FeedingStore.IsFull(정원, 정원));
        Assert.AreEqual(1f, FeedingStore.Fullness(정원, 정원), 허용오차);
    }
}
