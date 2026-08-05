using NUnit.Framework;
using Survive.Crafting;

/// <summary>
/// 대기열 칸에 적히는 남은 시간 표기.
///
/// 이 함수는 매 프레임 불리고 화면에서 사람이 계속 쳐다보는 유일한 숫자다.
/// Domain으로 옮기기 전에는 Assembly-CSharp에 있어 테스트가 닿지 못했다.
/// </summary>
public class CraftTimeTextTests
{
    [Test]
    public void 일_분_미만은_초만_적는다()
    {
        Assert.AreEqual("45초", CraftTimeText.Short(45f));
        Assert.AreEqual("1초", CraftTimeText.Short(1f));
    }

    [Test]
    public void 남은_시간은_올림한다()
    {
        // 0.4초 남았는데 "0초"라고 적으면 이미 끝난 것처럼 보인다.
        Assert.AreEqual("1초", CraftTimeText.Short(0.4f));
        Assert.AreEqual("46초", CraftTimeText.Short(45.1f));
    }

    [Test]
    public void 다_끝났으면_영_초다()
    {
        Assert.AreEqual("0초", CraftTimeText.Short(0f));
    }

    [Test]
    public void 음수는_영_초로_접는다()
    {
        // 프레임이 밀려 만료 시각을 지나쳐도 "-3초"가 뜨면 안 된다.
        Assert.AreEqual("0초", CraftTimeText.Short(-3f));
    }

    [Test]
    public void 일_분_경계에서_분으로_넘어간다()
    {
        Assert.AreEqual("59초", CraftTimeText.Short(59f));
        Assert.AreEqual("1분", CraftTimeText.Short(60f));
    }

    [Test]
    public void 초가_남으면_분과_초를_붙여_적는다()
    {
        Assert.AreEqual("2분30초", CraftTimeText.Short(150f));
        Assert.AreEqual("1분1초", CraftTimeText.Short(61f));
    }

    [Test]
    public void 정각이면_초를_적지_않는다()
    {
        Assert.AreEqual("5분", CraftTimeText.Short(300f));
    }

    [Test]
    public void 한_시간_경계에서_시간으로_넘어간다()
    {
        // 예전에는 여기서 "60m"이 나왔다.
        Assert.AreEqual("59분59초", CraftTimeText.Short(3599f));
        Assert.AreEqual("1시간", CraftTimeText.Short(3600f));
    }

    [Test]
    public void 시간_단위에서는_초를_버린다()
    {
        // 한 시간을 기다리는 사람에게 초 자리는 정보가 아니라 소음이다.
        Assert.AreEqual("1시간5분", CraftTimeText.Short(3600f + 5 * 60f + 30f));
        Assert.AreEqual("2시간", CraftTimeText.Short(7200f));
    }

    [Test]
    public void 어떤_값이든_영문_단위가_섞이지_않는다()
    {
        // 한국어 UI에 s/m이 섞여 있던 것이 이 작업의 출발점이다.
        for (float t = 0f; t < 8000f; t += 7.3f)
        {
            var s = CraftTimeText.Short(t);
            Assert.IsFalse(s.Contains("s") || s.Contains("m") || s.Contains("h"),
                           $"{t}초에서 영문 단위가 나왔다: {s}");
        }
    }

    [Test]
    public void 칸에_들어갈_만큼_짧다()
    {
        // 대기열 슬롯은 좁다. 가장 긴 경우가 몇 글자인지 못 박아 둔다.
        int longest = 0;
        for (float t = 0f; t < 8000f; t += 1f)
            longest = System.Math.Max(longest, CraftTimeText.Short(t).Length);

        Assert.LessOrEqual(longest, 6, "표기가 여섯 글자를 넘으면 칸을 밀어낸다");
    }
}
