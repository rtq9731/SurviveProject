using NUnit.Framework;
using Survive.Core;

public class GameServicesTests
{
    class 더미서비스 { public int 값; }

    [SetUp]
    public void 초기화() => GameServices.Clear();

    [TearDown]
    public void 정리() => GameServices.Clear();

    [Test]
    public void 등록한_서비스를_되찾는다()
    {
        var s = new 더미서비스 { 값 = 7 };
        GameServices.Register(s);
        Assert.AreSame(s, GameServices.Get<더미서비스>());
    }

    [Test]
    public void 미등록_서비스_조회는_예외를_던진다()
    {
        Assert.Throws<System.InvalidOperationException>(() => GameServices.Get<더미서비스>());
    }

    [Test]
    public void TryGet은_미등록시_false를_돌려준다()
    {
        Assert.IsFalse(GameServices.TryGet<더미서비스>(out var found));
        Assert.IsNull(found);
    }

    [Test]
    public void 해제한_서비스는_조회되지_않는다()
    {
        GameServices.Register(new 더미서비스());
        GameServices.Unregister<더미서비스>();
        Assert.IsFalse(GameServices.TryGet<더미서비스>(out _));
    }

    [Test]
    public void 같은_타입_재등록은_덮어쓴다()
    {
        GameServices.Register(new 더미서비스 { 값 = 1 });
        GameServices.Register(new 더미서비스 { 값 = 2 });
        Assert.AreEqual(2, GameServices.Get<더미서비스>().값);
    }
}
