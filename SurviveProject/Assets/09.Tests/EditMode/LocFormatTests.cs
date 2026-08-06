using System.Collections.Generic;
using NUnit.Framework;
using Survive.Localization;

/// <summary>
/// 자리표 <c>{0} {1} {2}</c>의 규약.
///
/// <b>여기서 지키는 것은 하나다 — 번역가가 자리 순서를 마음대로 바꿀 수 있다.</b>
/// 한국어 "가진 수/드는 수"가 영어에서 같은 순서일 이유가 없고, 세는 단위는
/// 언어마다 아예 없거나(영어) 명사마다 다르다(개/마리/자루). 그래서 문장을
/// 통째로 표에 넣고 자리만 표시하는 것이고, 그 자리가 뒤집혀도 돌아야 한다.
///
/// 두 번째로 지키는 것은 <b>절대 예외를 던지지 않는다</b>이다. 번역가의 오타로
/// 게임이 죽으면 안 된다 — 잘못은 화면에 드러나고 게이트가 잡는다.
/// </summary>
public class LocFormatTests
{
    // ── 값을 끼운다 ──────────────────────────────────────────

    [Test]
    public void 자리표에_값이_들어간다()
    {
        Assert.AreEqual("버섯 목재 2/5",
            LocFormat.Apply("{0} {1}/{2}", LocArgs.Of("버섯 목재", 2, 5)));
    }

    [Test]
    public void 자리_순서를_뒤집어도_돈다()
    {
        // 이것이 이 층의 존재 이유다. 같은 인자로 en은 수를 앞에 둔다.
        Assert.AreEqual("2/5 버섯 목재",
            LocFormat.Apply("{1}/{2} {0}", LocArgs.Of("버섯 목재", 2, 5)));
    }

    [Test]
    public void 같은_자리를_두_번_써도_돈다()
    {
        // 언어에 따라 같은 값을 문장 안에서 두 번 불러야 할 때가 있다.
        Assert.AreEqual("도끼와 도끼",
            LocFormat.Apply("{0}와 {0}", LocArgs.Of("도끼")));
    }

    [Test]
    public void 인자가_모자라면_자리표를_그대로_남긴다()
    {
        // 빈칸으로 지우면 무엇이 빠졌는지 아무도 모른다. 눈에 띄어야 고쳐진다.
        Assert.AreEqual("도끼 {1}", LocFormat.Apply("{0} {1}", LocArgs.Of("도끼")));
    }

    [Test]
    public void 인자가_남아도_조용히_넘어간다()
    {
        Assert.AreEqual("도끼", LocFormat.Apply("{0}", LocArgs.Of("도끼", 2, 5)));
    }

    [Test]
    public void 어떤_입력에도_예외를_던지지_않는다()
    {
        Assert.DoesNotThrow(() => LocFormat.Apply("{", LocArgs.Of(1)));
        Assert.DoesNotThrow(() => LocFormat.Apply("}", LocArgs.Of(1)));
        Assert.DoesNotThrow(() => LocFormat.Apply("{0", LocArgs.Of(1)));
        Assert.DoesNotThrow(() => LocFormat.Apply("{999999999999}", LocArgs.Of(1)));
        Assert.DoesNotThrow(() => LocFormat.Apply(null, LocArgs.Of(1)));
        Assert.DoesNotThrow(() => LocFormat.Apply("{0}", LocArgs.Of((object[])null)));
    }

    [Test]
    public void 중괄호는_겹쳐_쓰면_글자가_된다()
    {
        Assert.AreEqual("{도끼}", LocFormat.Apply("{{{0}}}", LocArgs.Of("도끼")));
    }

    [Test]
    public void 자리표가_아닌_중괄호는_글자로_둔다()
    {
        Assert.AreEqual("{이름} 도끼", LocFormat.Apply("{이름} {0}", LocArgs.Of("도끼")));
    }

    [Test]
    public void null_인자는_빈_글자다()
    {
        // "null"이라고 화면에 적히는 것이 더 나쁘다.
        Assert.AreEqual("[]", LocFormat.Apply("[{0}]", LocArgs.Of((object)null)));
    }

    [Test]
    public void 자리표가_없는_틀은_그대로_나온다()
    {
        Assert.AreEqual("재료 없음", LocFormat.Apply("재료 없음", LocArgs.Of(1)));
    }

    // ── 게이트가 보는 것 ─────────────────────────────────────

    [Test]
    public void 자리표_번호를_나온_순서대로_센다()
    {
        CollectionAssert.AreEqual(new[] { 1, 0, 1 }, LocFormat.Indices("{1} {0} {1}"));
        CollectionAssert.IsEmpty(LocFormat.Indices("재료 없음"));
        CollectionAssert.IsEmpty(LocFormat.Indices("{{0}}"), "겹친 중괄호는 자리표가 아니다");
    }

    [Test]
    public void 필요한_인자_개수는_가장_큰_번호_더하기_하나다()
    {
        Assert.AreEqual(0, LocFormat.RequiredArgCount("재료 없음"));
        Assert.AreEqual(1, LocFormat.RequiredArgCount("{0}개"));
        Assert.AreEqual(3, LocFormat.RequiredArgCount("{2} {0}"), "빠진 번호도 자리는 세어야 한다");
    }

    [Test]
    public void 번호가_0부터_이어지지_않으면_잡는다()
    {
        Assert.IsTrue(LocFormat.IsContiguousFromZero("{0} {1} {2}", out _));
        Assert.IsTrue(LocFormat.IsContiguousFromZero("{2} {1} {0}", out _), "순서는 상관없다");
        Assert.IsTrue(LocFormat.IsContiguousFromZero("자리표 없음", out _));

        Assert.IsFalse(LocFormat.IsContiguousFromZero("{0} {2}", out var missing));
        CollectionAssert.AreEqual(new List<int> { 1 }, missing);

        Assert.IsFalse(LocFormat.IsContiguousFromZero("{1}", out missing),
            "0부터 시작하지 않으면 첫 인자가 화면에 닿지 못한다");
        CollectionAssert.AreEqual(new List<int> { 0 }, missing);
    }
}
