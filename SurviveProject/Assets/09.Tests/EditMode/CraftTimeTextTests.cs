using NUnit.Framework;
using Survive.Crafting;

/// <summary>
/// 대기열 칸에 적히는 남은 시간 표기(HH:MM:SS).
///
/// 이 함수는 매 프레임 불리고 화면에서 사람이 계속 쳐다보는 유일한 숫자다.
/// Domain으로 옮기기 전에는 Assembly-CSharp에 있어 테스트가 닿지 못했다.
/// </summary>
public class CraftTimeTextTests
{
    [Test]
    public void 초만_남아도_시_분_자리를_채운다()
    {
        Assert.AreEqual("00:00:45", CraftTimeText.Short(45f));
        Assert.AreEqual("00:00:01", CraftTimeText.Short(1f));
    }

    [Test]
    public void 남은_시간은_올림한다()
    {
        // 0.4초 남았는데 00:00:00이라고 적으면 이미 끝난 것처럼 보인다.
        Assert.AreEqual("00:00:01", CraftTimeText.Short(0.4f));
        Assert.AreEqual("00:00:46", CraftTimeText.Short(45.1f));
    }

    [Test]
    public void 다_끝났으면_전부_영이다()
    {
        Assert.AreEqual("00:00:00", CraftTimeText.Short(0f));
    }

    [Test]
    public void 음수는_영으로_접는다()
    {
        // 프레임이 밀려 만료 시각을 지나쳐도 음수 시계가 뜨면 안 된다.
        Assert.AreEqual("00:00:00", CraftTimeText.Short(-3f));
    }

    [Test]
    public void 일_분_경계에서_분_자리가_오른다()
    {
        Assert.AreEqual("00:00:59", CraftTimeText.Short(59f));
        Assert.AreEqual("00:01:00", CraftTimeText.Short(60f));
    }

    [Test]
    public void 분과_초를_함께_센다()
    {
        Assert.AreEqual("00:02:30", CraftTimeText.Short(150f));
        Assert.AreEqual("00:01:01", CraftTimeText.Short(61f));
    }

    [Test]
    public void 한_시간_경계에서_시_자리가_오른다()
    {
        Assert.AreEqual("00:59:59", CraftTimeText.Short(3599f));
        Assert.AreEqual("01:00:00", CraftTimeText.Short(3600f));
    }

    [Test]
    public void 시_단위에서도_초를_버리지_않는다()
    {
        // 시계 꼴에서는 초 자리가 늘 있다. 비워 두면 멈춘 것처럼 읽힌다.
        Assert.AreEqual("01:05:30", CraftTimeText.Short(3600f + 5 * 60f + 30f));
        Assert.AreEqual("02:00:00", CraftTimeText.Short(7200f));
    }

    [Test]
    public void 폭이_변하지_않는다()
    {
        // 이 표기를 고른 이유 자체다. 길이가 흔들리면 칸 안에서 글자가 들썩인다.
        for (float t = 0f; t < 8000f; t += 1f)
            Assert.AreEqual(8, CraftTimeText.Short(t).Length, $"{t}초에서 폭이 달라졌다");
    }

    [Test]
    public void 자리는_항상_콜론_둘로_나뉜다()
    {
        for (float t = 0f; t < 8000f; t += 7.3f)
        {
            var s = CraftTimeText.Short(t);
            var parts = s.Split(':');
            Assert.AreEqual(3, parts.Length, $"{t}초에서 형식이 깨졌다: {s}");
            foreach (var p in parts)
                Assert.AreEqual(2, p.Length, $"{t}초에서 자릿수가 모자라거나 넘쳤다: {s}");
        }
    }

    [Test]
    public void 백_시간을_넘겨도_잘라_내지_않는다()
    {
        // 그런 제작은 없지만, 칸을 넘기는 것이 거짓말하는 것보다 낫다.
        Assert.AreEqual("100:00:00", CraftTimeText.Short(100f * 3600f));
    }

    [Test]
    public void 글자는_숫자와_콜론뿐이다()
    {
        for (float t = 0f; t < 8000f; t += 13f)
            foreach (var c in CraftTimeText.Short(t))
                Assert.IsTrue(char.IsDigit(c) || c == ':', $"{t}초에서 뜻밖의 글자: {c}");
    }
}
