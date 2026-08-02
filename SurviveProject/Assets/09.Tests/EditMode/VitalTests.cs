using NUnit.Framework;
using Survive.Vitals;

public class VitalTests
{
    [Test]
    public void 생성시_시작값을_가진다()
    {
        var v = new Vital(100f, 60f);
        Assert.AreEqual(60f, v.Current);
        Assert.AreEqual(100f, v.Max);
    }

    [Test]
    public void 최대치를_넘지_못한다()
    {
        var v = new Vital(100f, 90f);
        v.Modify(50f);
        Assert.AreEqual(100f, v.Current);
    }

    [Test]
    public void 영_아래로_내려가지_않는다()
    {
        var v = new Vital(100f, 10f);
        v.Modify(-50f);
        Assert.AreEqual(0f, v.Current);
    }

    [Test]
    public void 비었을때_IsEmpty가_참이다()
    {
        var v = new Vital(100f, 1f);
        Assert.IsFalse(v.IsEmpty);
        v.Modify(-1f);
        Assert.IsTrue(v.IsEmpty);
    }

    [Test]
    public void Normalized는_영에서_일_사이다()
    {
        var v = new Vital(200f, 50f);
        Assert.AreEqual(0.25f, v.Normalized, 0.0001f);
    }

    [Test]
    public void 값이_바뀌면_Changed가_발생한다()
    {
        var v = new Vital(100f, 50f);
        float 받은현재 = -1f, 받은최대 = -1f;
        int 횟수 = 0;
        v.Changed += (cur, max) => { 받은현재 = cur; 받은최대 = max; 횟수++; };

        v.Modify(-10f);

        Assert.AreEqual(1, 횟수);
        Assert.AreEqual(40f, 받은현재);
        Assert.AreEqual(100f, 받은최대);
    }

    [Test]
    public void 값이_그대로면_Changed가_발생하지_않는다()
    {
        var v = new Vital(100f, 100f);
        int 횟수 = 0;
        v.Changed += (_, __) => 횟수++;

        v.Modify(10f);   // 이미 최대치라 변화 없음

        Assert.AreEqual(0, 횟수);
    }

    [Test]
    public void 최대치를_줄이면_현재값도_함께_잘린다()
    {
        var v = new Vital(100f, 100f);
        v.SetMax(40f);
        Assert.AreEqual(40f, v.Max);
        Assert.AreEqual(40f, v.Current);
    }

    [Test]
    public void 최대치는_영_아래로_설정되지_않는다()
    {
        var v = new Vital(100f, 100f);
        v.SetMax(-5f);
        Assert.AreEqual(0f, v.Max);
        Assert.AreEqual(0f, v.Current);
    }

    [Test]
    public void 최대치가_영이면_Normalized는_영이다()
    {
        var v = new Vital(0f, 0f);
        Assert.AreEqual(0f, v.Normalized);
    }
}
