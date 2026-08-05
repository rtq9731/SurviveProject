using NUnit.Framework;
using Survive.Harvesting;
using Survive.Items;

/// <summary>
/// 도구는 전용이다.
///
/// 지키려는 계약은 하나다 — <b>곡괭이로는 나무를 베지 못하고 도끼로는 돌을 깨지 못한다.</b>
/// 이 한 줄이 무너지면 도구를 여러 개 만들 이유가 사라지고, 도구 이름이
/// 그 도구가 하는 일을 말해 주지 않게 된다.
/// </summary>
public class ToolMatchTests
{
    [Test]
    public void 맨손으로_되는_것은_아무_도구로도_된다()
    {
        Assert.IsTrue(ToolMatch.Satisfies(ToolType.None, 0, ToolType.None, 0));
        Assert.IsTrue(ToolMatch.Satisfies(ToolType.None, 0, ToolType.Pickaxe, 1));
        Assert.IsTrue(ToolMatch.Satisfies(ToolType.None, 0, ToolType.Axe, 1));
    }

    [Test]
    public void 맞는_도구는_통과한다()
    {
        Assert.IsTrue(ToolMatch.Satisfies(ToolType.Pickaxe, 1, ToolType.Pickaxe, 1));
        Assert.IsTrue(ToolMatch.Satisfies(ToolType.Axe, 1, ToolType.Axe, 1));
    }

    [Test]
    public void 곡괭이로는_나무를_베지_못한다()
    {
        Assert.IsFalse(ToolMatch.Satisfies(ToolType.Axe, 1, ToolType.Pickaxe, 1));
    }

    [Test]
    public void 도끼로는_광맥을_깨지_못한다()
    {
        Assert.IsFalse(ToolMatch.Satisfies(ToolType.Pickaxe, 1, ToolType.Axe, 1));
    }

    [Test]
    public void 아무리_좋은_곡괭이라도_나무는_못_벤다()
    {
        // 등급은 같은 종류 안에서만 견준다. 종류가 다르면 등급을 볼 것도 없다.
        Assert.IsFalse(ToolMatch.Satisfies(ToolType.Axe, 1, ToolType.Pickaxe, 99));
    }

    [Test]
    public void 맨손으로는_도구가_필요한_것을_캘_수_없다()
    {
        Assert.IsFalse(ToolMatch.Satisfies(ToolType.Pickaxe, 1, ToolType.None, 0));
        Assert.IsFalse(ToolMatch.Satisfies(ToolType.Axe, 1, ToolType.None, 0));
    }

    [Test]
    public void 등급이_모자라면_같은_종류라도_안_된다()
    {
        Assert.IsFalse(ToolMatch.Satisfies(ToolType.Pickaxe, 2, ToolType.Pickaxe, 1));
        Assert.IsTrue(ToolMatch.Satisfies(ToolType.Pickaxe, 2, ToolType.Pickaxe, 3));
    }

    [Test]
    public void 도구마다_부를_이름이_있다()
    {
        Assert.AreEqual("곡괭이", ToolMatch.Name(ToolType.Pickaxe));
        Assert.AreEqual("도끼", ToolMatch.Name(ToolType.Axe));
        Assert.AreEqual("망치", ToolMatch.Name(ToolType.Hammer));
        Assert.IsNotEmpty(ToolMatch.Name(ToolType.None), "이름이 비면 프롬프트가 '  필요'가 된다");
    }

    [Test]
    public void 거대_버섯은_도끼를_요구한다()
    {
        Assert.AreEqual(ToolType.Axe, MushroomLumberRule.RequiredTool);
        Assert.IsFalse(ToolMatch.Satisfies(MushroomLumberRule.RequiredTool,
                                           MushroomLumberRule.RequiredTier,
                                           ToolType.Pickaxe, 99));
    }
}
