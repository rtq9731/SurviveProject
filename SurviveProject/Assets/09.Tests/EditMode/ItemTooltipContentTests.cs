using NUnit.Framework;
using UnityEngine;
using Survive.Items;
using Survive.UI;

/// <summary>
/// 쪽지에 무엇이 적히는가.
///
/// 아이템 에셋 23종에 진작부터 있던 <c>description</c>이 화면에 닿는 길이 없었다.
/// 여기서 못박는 것은 그 문장이 <b>그대로</b> 실린다는 것과, 곁에 붙는 한 줄이
/// 설명문을 밀어내지 않는다는 것이다.
/// </summary>
public class ItemTooltipContentTests
{
    static ItemDataSO 아이템(string 이름, string 설명 = "",
                             ItemCategory 분류 = ItemCategory.Resource, int 묶음 = 1)
    {
        var it = ScriptableObject.CreateInstance<ItemDataSO>();
        it.id = "test_item";
        it.displayName = 이름;
        it.description = 설명;
        it.category = 분류;
        it.maxStack = 묶음;
        return it;
    }

    [Test]
    public void 설명문은_손대지_않고_그대로_싣는다()
    {
        const string 문장 = "거대 버섯의 단단한 대. 이 세계에서 유일하게 타는 것이다.";
        Assert.AreEqual(문장, ItemTooltipContent.Body(아이템("버섯 목재", 문장)));
    }

    [Test]
    public void 앞뒤_빈칸은_털어낸다()
    {
        Assert.AreEqual("설명", ItemTooltipContent.Body(아이템("무엇", "  설명\n")));
    }

    [Test]
    public void 설명이_없으면_본문은_빈다()
    {
        // 빈 줄을 남겨 두면 쪽지 아래에 이유 없는 여백이 생긴다.
        Assert.AreEqual("", ItemTooltipContent.Body(아이템("무엇", "   ")));
    }

    [Test]
    public void 이름이_비면_id라도_보여_준다()
    {
        Assert.AreEqual("test_item", ItemTooltipContent.Title(아이템("")));
    }

    [Test]
    public void 이름이_없는_물건에는_쪽지를_띄우지_않는다()
    {
        var 무명 = ScriptableObject.CreateInstance<ItemDataSO>();
        Assert.IsFalse(ItemTooltipContent.CanShow(무명));
        Assert.IsFalse(ItemTooltipContent.CanShow(null));
        Assert.IsTrue(ItemTooltipContent.CanShow(아이템("스크랩")));
    }

    [Test]
    public void 겹쳐지는_물건만_묶음_수를_적는다()
    {
        Assert.AreEqual("재료  ·  한 칸에 200개",
                        ItemTooltipContent.Meta(아이템("버섯 목재", "", ItemCategory.Resource, 200)));
    }

    [Test]
    public void 한_칸에_하나뿐인_물건에는_묶음_수를_적지_않는다()
    {
        // "최대 1개"는 아무것도 알려 주지 않는다.
        Assert.AreEqual("도구", ItemTooltipContent.Meta(아이템("곡괭이", "", ItemCategory.Tool, 1)));
    }

    [Test]
    public void 분류는_한국어로_적는다()
    {
        Assert.AreEqual("재료", ItemTooltipContent.CategoryName(ItemCategory.Resource));
        Assert.AreEqual("도구", ItemTooltipContent.CategoryName(ItemCategory.Tool));
        Assert.AreEqual("소모품", ItemTooltipContent.CategoryName(ItemCategory.Consumable));
        Assert.AreEqual("진행 물품", ItemTooltipContent.CategoryName(ItemCategory.Quest));
    }

    [Test]
    public void 화면에_나가는_문장에_줄표를_쓰지_않는다()
    {
        // 본문 글꼴(ChosunGu)에 U+2014가 없어 네모로 찍힌다.
        var it = 아이템("버섯 목재", "거대 버섯의 단단한 대.", ItemCategory.Resource, 200);

        foreach (var 문장 in new[] { ItemTooltipContent.Title(it),
                                     ItemTooltipContent.Body(it),
                                     ItemTooltipContent.Meta(it) })
        {
            Assert.IsFalse(문장.Contains("—"), $"줄표가 섞였다: {문장}");
            Assert.IsFalse(문장.Contains("−"), $"빼기 기호가 섞였다: {문장}");
        }
    }

    [Test]
    public void 없는_물건에는_아무것도_적지_않는다()
    {
        Assert.AreEqual("", ItemTooltipContent.Title(null));
        Assert.AreEqual("", ItemTooltipContent.Body(null));
        Assert.AreEqual("", ItemTooltipContent.Meta(null));
    }
}
