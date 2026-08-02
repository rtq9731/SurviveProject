using NUnit.Framework;
using UnityEngine;
using Survive.Crafting;
using Survive.Items;

public class CraftingTests
{
    static ItemDataSO 아이템(string id, int maxStack = 99)
    {
        var it = ScriptableObject.CreateInstance<ItemDataSO>();
        it.id = id;
        it.displayName = id;
        it.maxStack = maxStack;
        return it;
    }

    static RecipeSO 레시피(ItemStack result, StationType station, params ItemStack[] ing)
    {
        var r = ScriptableObject.CreateInstance<RecipeSO>();
        r.id = "test";
        r.result = result;
        r.ingredients = ing;
        r.requiredStation = station;
        return r;
    }

    [Test]
    public void 재료가_충족되면_제작할_수_있다()
    {
        var scrap = 아이템("scrap");
        var 필터 = 아이템("filter", 5);
        var inv = new Inventory(6);
        inv.TryAdd(scrap, 10);

        var r = 레시피(new ItemStack(필터, 1), StationType.None, new ItemStack(scrap, 8));
        Assert.IsTrue(CraftingService.CanCraft(r, inv, StationType.None));
    }

    [Test]
    public void 재료가_모자라면_제작할_수_없다()
    {
        var scrap = 아이템("scrap");
        var 필터 = 아이템("filter", 5);
        var inv = new Inventory(6);
        inv.TryAdd(scrap, 3);

        var r = 레시피(new ItemStack(필터, 1), StationType.None, new ItemStack(scrap, 8));
        Assert.IsFalse(CraftingService.CanCraft(r, inv, StationType.None));
    }

    [Test]
    public void 제작하면_재료가_차감되고_결과가_들어온다()
    {
        var scrap = 아이템("scrap");
        var 버섯 = 아이템("mushroom");
        var 필터 = 아이템("filter", 5);
        var inv = new Inventory(6);
        inv.TryAdd(scrap, 10);
        inv.TryAdd(버섯, 5);

        var r = 레시피(new ItemStack(필터, 1), StationType.None,
                     new ItemStack(scrap, 8), new ItemStack(버섯, 3));

        Assert.IsTrue(CraftingService.Craft(r, inv, StationType.None));
        Assert.AreEqual(2, inv.CountOf("scrap"));
        Assert.AreEqual(2, inv.CountOf("mushroom"));
        Assert.AreEqual(1, inv.CountOf("filter"));
    }

    [Test]
    public void 스테이션이_필요한데_없으면_제작할_수_없다()
    {
        var scrap = 아이템("scrap");
        var 키 = 아이템("key", 1);
        var inv = new Inventory(6);
        inv.TryAdd(scrap, 30);

        var r = 레시피(new ItemStack(키, 1), StationType.Bench, new ItemStack(scrap, 20));
        Assert.IsFalse(CraftingService.CanCraft(r, inv, StationType.None));
        Assert.IsTrue(CraftingService.CanCraft(r, inv, StationType.Bench));
    }

    [Test]
    public void 휴대_레시피는_스테이션이_있어도_제작된다()
    {
        var scrap = 아이템("scrap");
        var 필터 = 아이템("filter", 5);
        var inv = new Inventory(6);
        inv.TryAdd(scrap, 10);

        var r = 레시피(new ItemStack(필터, 1), StationType.None, new ItemStack(scrap, 8));
        Assert.IsTrue(CraftingService.CanCraft(r, inv, StationType.Bench));
    }

    [Test]
    public void 제작에_실패하면_인벤토리가_변하지_않는다()
    {
        var scrap = 아이템("scrap");
        var 필터 = 아이템("filter", 5);
        var inv = new Inventory(6);
        inv.TryAdd(scrap, 3);

        var r = 레시피(new ItemStack(필터, 1), StationType.None, new ItemStack(scrap, 8));
        Assert.IsFalse(CraftingService.Craft(r, inv, StationType.None));
        Assert.AreEqual(3, inv.CountOf("scrap"));
        Assert.AreEqual(0, inv.CountOf("filter"));
    }

    [Test]
    public void 결과를_넣을_자리가_없으면_재료를_되돌린다()
    {
        // 슬롯 1칸짜리 인벤토리에 스크랩만 가득. 결과가 들어갈 자리가 없다.
        var scrap = 아이템("scrap", 99);
        var 필터 = 아이템("filter", 1);
        var inv = new Inventory(1);
        inv.TryAdd(scrap, 20);

        var r = 레시피(new ItemStack(필터, 1), StationType.None, new ItemStack(scrap, 8));

        Assert.IsFalse(CraftingService.Craft(r, inv, StationType.None),
            "결과를 넣을 수 없으면 실패해야 한다");
        Assert.AreEqual(20, inv.CountOf("scrap"), "재료가 되돌아와야 한다");
        Assert.AreEqual(0, inv.CountOf("filter"));
    }

    [Test]
    public void null_레시피나_인벤토리는_false다()
    {
        var inv = new Inventory(4);
        Assert.IsFalse(CraftingService.CanCraft(null, inv, StationType.None));
        Assert.IsFalse(CraftingService.CanCraft(레시피(null, StationType.None), inv, StationType.None));
        Assert.IsFalse(CraftingService.CanCraft(레시피(new ItemStack(아이템("x"), 1), StationType.None), null, StationType.None));
    }

    [Test]
    public void 재료가_없는_레시피도_제작된다()
    {
        var 돌 = 아이템("stone");
        var inv = new Inventory(4);
        var r = 레시피(new ItemStack(돌, 1), StationType.None);

        Assert.IsTrue(CraftingService.Craft(r, inv, StationType.None));
        Assert.AreEqual(1, inv.CountOf("stone"));
    }
}
