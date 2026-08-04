using System;
using NUnit.Framework;
using Survive.UI;

/// <summary>
/// 백로그 17 — UIStateService에서 뽑아낸 열림 스택 규칙.
/// 연 순서, 최후 열림 우선 닫기, 전체 닫기. Unity를 쓰지 않는 순수 클래스라
/// 씬도 MonoBehaviour도 없이 확인한다.
/// </summary>
public class PanelStackTests
{
    /// <summary>패널 대역. 스택은 이것이 무엇인지 알 필요가 없다.</summary>
    class 패널
    {
        public bool 열림 = true;
        readonly string _이름;
        public 패널(string 이름) { _이름 = 이름; }
        public override string ToString() => _이름;
    }

    static PanelStack<패널> 스택() => new PanelStack<패널>();
    static bool 열려있나(패널 p) => p != null && p.열림;

    // ── 쌓기 ────────────────────────────────────────────────────────────

    [Test]
    public void 빈_스택에는_닫을_것이_없다()
    {
        Assert.IsNull(스택().PeekOpen(열려있나));
    }

    [Test]
    public void 연_순서대로_쌓인다()
    {
        var s = 스택();
        var a = new 패널("a"); var b = new 패널("b"); var c = new 패널("c");
        s.Push(a); s.Push(b); s.Push(c);

        CollectionAssert.AreEqual(new[] { a, b, c }, s.OpenOrder);
        Assert.AreEqual(3, s.Count);
    }

    [Test]
    public void 가장_나중에_연_것이_먼저_닫힌다()
    {
        var s = 스택();
        var a = new 패널("a"); var b = new 패널("b");
        s.Push(a); s.Push(b);

        Assert.AreSame(b, s.PeekOpen(열려있나));
    }

    [Test]
    public void 이미_쌓인_것을_다시_열면_맨_위로_올라온다()
    {
        // 소지품 위에 보관함이 떠 있는데 소지품을 다시 열면, ESC는 소지품부터 닫아야 한다.
        var s = 스택();
        var a = new 패널("a"); var b = new 패널("b");
        s.Push(a); s.Push(b); s.Push(a);

        CollectionAssert.AreEqual(new[] { b, a }, s.OpenOrder);
        Assert.AreEqual(2, s.Count, "다시 열었다고 두 번 쌓이면 안 된다");
        Assert.AreSame(a, s.PeekOpen(열려있나));
    }

    [Test]
    public void null은_쌓이지_않는다()
    {
        var s = 스택();
        s.Push(null);
        Assert.AreEqual(0, s.Count);
    }

    // ── 빼기 ────────────────────────────────────────────────────────────

    [Test]
    public void 닫은_것을_빼면_그_아래가_다음_차례가_된다()
    {
        var s = 스택();
        var a = new 패널("a"); var b = new 패널("b");
        s.Push(a); s.Push(b);

        Assert.IsTrue(s.Remove(b));
        Assert.AreSame(a, s.PeekOpen(열려있나));
    }

    [Test]
    public void 가운데_것을_빼도_나머지_순서는_그대로다()
    {
        var s = 스택();
        var a = new 패널("a"); var b = new 패널("b"); var c = new 패널("c");
        s.Push(a); s.Push(b); s.Push(c);

        s.Remove(b);
        CollectionAssert.AreEqual(new[] { a, c }, s.OpenOrder);
    }

    [Test]
    public void 쌓인_적_없는_것을_빼면_아무_일도_없다()
    {
        var s = 스택();
        var a = new 패널("a");
        s.Push(a);

        Assert.IsFalse(s.Remove(new 패널("남")));
        Assert.IsFalse(s.Remove(null));
        Assert.AreEqual(1, s.Count);
    }

    [Test]
    public void 담긴_것을_확인할_수_있다()
    {
        var s = 스택();
        var a = new 패널("a");
        s.Push(a);

        Assert.IsTrue(s.Contains(a));
        Assert.IsFalse(s.Contains(new 패널("남")));
        Assert.IsFalse(s.Contains(null));
    }

    // ── 죽은 것 걸러내기 ────────────────────────────────────────────────

    [Test]
    public void 다른_경로로_닫힌_것은_건너뛰고_그_아래를_고른다()
    {
        // 버튼으로 닫은 패널이 꼭대기에 남아 있으면 ESC 한 번을 삼킨다.
        var s = 스택();
        var a = new 패널("a"); var b = new 패널("b");
        s.Push(a); s.Push(b);
        b.열림 = false;

        Assert.AreSame(a, s.PeekOpen(열려있나));
    }

    [Test]
    public void 건너뛴_것은_스택에서_지워진다()
    {
        var s = 스택();
        var a = new 패널("a"); var b = new 패널("b");
        s.Push(a); s.Push(b);
        b.열림 = false;

        s.PeekOpen(열려있나);
        Assert.AreEqual(1, s.Count);
        Assert.IsFalse(s.Contains(b));
    }

    [Test]
    public void 고른_것은_스택에_남는다()
    {
        // 실제로 닫히면 그때 Remove가 온다. 여기서 미리 빼면
        // 닫히지 않았는데 기록만 사라져 다음 ESC가 엉뚱한 곳으로 간다.
        var s = 스택();
        var a = new 패널("a");
        s.Push(a);

        Assert.AreSame(a, s.PeekOpen(열려있나));
        Assert.AreEqual(1, s.Count);
        Assert.AreSame(a, s.PeekOpen(열려있나));
    }

    [Test]
    public void 전부_닫혀_있으면_고를_것이_없고_스택도_빈다()
    {
        var s = 스택();
        var a = new 패널("a") { 열림 = false };
        var b = new 패널("b") { 열림 = false };
        s.Push(a); s.Push(b);

        Assert.IsNull(s.PeekOpen(열려있나));
        Assert.AreEqual(0, s.Count);
    }

    [Test]
    public void 판정_기준이_없으면_거부한다()
    {
        Assert.Throws<ArgumentNullException>(() => 스택().PeekOpen(null));
    }

    // ── 전체 닫기 ───────────────────────────────────────────────────────

    [Test]
    public void 전체_닫기는_기록을_비운다()
    {
        var s = 스택();
        s.Push(new 패널("a")); s.Push(new 패널("b"));

        s.Clear();
        Assert.AreEqual(0, s.Count);
        Assert.IsNull(s.PeekOpen(열려있나));
    }
}
