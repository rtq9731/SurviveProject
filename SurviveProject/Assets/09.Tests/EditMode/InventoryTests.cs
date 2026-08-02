using NUnit.Framework;
using UnityEngine;
using Survive.Items;

public class InventoryTests
{
    static ItemDataSO 아이템(string id, int maxStack)
    {
        var it = ScriptableObject.CreateInstance<ItemDataSO>();
        it.id = id;
        it.displayName = id;
        it.maxStack = maxStack;
        return it;
    }

    [Test]
    public void 빈_인벤토리에_넣으면_전부_들어간다()
    {
        var inv = new Inventory(4);
        int 남은수 = inv.TryAdd(아이템("scrap", 99), 30);
        Assert.AreEqual(0, 남은수);
        Assert.AreEqual(30, inv.CountOf("scrap"));
    }

    [Test]
    public void 기존_스택을_먼저_채운다()
    {
        var scrap = 아이템("scrap", 99);
        var inv = new Inventory(4);
        inv.TryAdd(scrap, 90);
        inv.TryAdd(scrap, 5);

        Assert.AreEqual(95, inv.CountOf("scrap"));
        Assert.AreEqual(95, inv.Slots[0].count);
        Assert.IsTrue(inv.Slots[1].IsEmpty, "두 번째 슬롯을 쓰면 안 된다");
    }

    [Test]
    public void 스택이_넘치면_다음_슬롯으로_넘어간다()
    {
        var scrap = 아이템("scrap", 99);
        var inv = new Inventory(4);
        inv.TryAdd(scrap, 150);

        Assert.AreEqual(99, inv.Slots[0].count);
        Assert.AreEqual(51, inv.Slots[1].count);
        Assert.AreEqual(150, inv.CountOf("scrap"));
    }

    [Test]
    public void 자리가_모자라면_남은_수량을_돌려준다()
    {
        var scrap = 아이템("scrap", 10);
        var inv = new Inventory(2);
        int 남은수 = inv.TryAdd(scrap, 25);   // 최대 20개만 들어감

        Assert.AreEqual(5, 남은수);
        Assert.AreEqual(20, inv.CountOf("scrap"));
    }

    [Test]
    public void 스택_불가_아이템은_슬롯을_하나씩_쓴다()
    {
        var 곡괭이 = 아이템("pickaxe", 1);
        var inv = new Inventory(3);
        int 남은수 = inv.TryAdd(곡괭이, 3);

        Assert.AreEqual(0, 남은수);
        Assert.AreEqual(1, inv.Slots[0].count);
        Assert.AreEqual(1, inv.Slots[1].count);
        Assert.AreEqual(1, inv.Slots[2].count);
    }

    [Test]
    public void 제거하면_수량이_준다()
    {
        var scrap = 아이템("scrap", 99);
        var inv = new Inventory(4);
        inv.TryAdd(scrap, 50);

        Assert.IsTrue(inv.TryRemove("scrap", 20));
        Assert.AreEqual(30, inv.CountOf("scrap"));
    }

    [Test]
    public void 수량이_모자라면_제거하지_않는다()
    {
        var scrap = 아이템("scrap", 99);
        var inv = new Inventory(4);
        inv.TryAdd(scrap, 10);

        Assert.IsFalse(inv.TryRemove("scrap", 20));
        Assert.AreEqual(10, inv.CountOf("scrap"), "실패했으면 원상태여야 한다");
    }

    [Test]
    public void 여러_슬롯에_걸쳐_제거한다()
    {
        var scrap = 아이템("scrap", 10);
        var inv = new Inventory(4);
        inv.TryAdd(scrap, 25);          // 10 / 10 / 5

        Assert.IsTrue(inv.TryRemove("scrap", 22));
        Assert.AreEqual(3, inv.CountOf("scrap"));
    }

    [Test]
    public void 다_비운_슬롯은_비워진다()
    {
        var scrap = 아이템("scrap", 99);
        var inv = new Inventory(2);
        inv.TryAdd(scrap, 5);
        inv.TryRemove("scrap", 5);

        Assert.IsTrue(inv.Slots[0].IsEmpty);
        Assert.IsNull(inv.Slots[0].item);
    }

    [Test]
    public void 없는_아이템_제거는_실패한다()
    {
        var inv = new Inventory(2);
        Assert.IsFalse(inv.TryRemove("없는아이템", 1));
    }

    [Test]
    public void Has는_보유량을_판정한다()
    {
        var scrap = 아이템("scrap", 99);
        var inv = new Inventory(2);
        inv.TryAdd(scrap, 5);

        Assert.IsTrue(inv.Has("scrap", 5));
        Assert.IsFalse(inv.Has("scrap", 6));
    }

    [Test]
    public void 빈_슬롯으로_옮기면_이동한다()
    {
        var scrap = 아이템("scrap", 99);
        var inv = new Inventory(3);
        inv.TryAdd(scrap, 5);

        inv.MoveOrSwap(0, 2);

        Assert.IsTrue(inv.Slots[0].IsEmpty);
        Assert.AreEqual(5, inv.Slots[2].count);
    }

    [Test]
    public void 다른_아이템끼리는_교환된다()
    {
        var scrap = 아이템("scrap", 99);
        var 곡괭이 = 아이템("pickaxe", 1);
        var inv = new Inventory(2);
        inv.TryAdd(scrap, 5);
        inv.TryAdd(곡괭이, 1);

        inv.MoveOrSwap(0, 1);

        Assert.AreEqual("pickaxe", inv.Slots[0].item.id);
        Assert.AreEqual("scrap", inv.Slots[1].item.id);
    }

    [Test]
    public void 같은_아이템끼리는_병합된다()
    {
        var scrap = 아이템("scrap", 99);
        var inv = new Inventory(2);
        inv.TryAdd(scrap, 5);
        inv.Slots[1].item = scrap;      // 두 번째 슬롯에 수동 배치
        inv.Slots[1].count = 3;

        inv.MoveOrSwap(1, 0);

        Assert.AreEqual(8, inv.Slots[0].count);
        Assert.IsTrue(inv.Slots[1].IsEmpty);
    }

    [Test]
    public void 병합시_최대치를_넘는_분량은_남는다()
    {
        var scrap = 아이템("scrap", 10);
        var inv = new Inventory(2);
        inv.TryAdd(scrap, 8);
        inv.Slots[1].item = scrap;
        inv.Slots[1].count = 7;

        inv.MoveOrSwap(1, 0);

        Assert.AreEqual(10, inv.Slots[0].count);
        Assert.AreEqual(5, inv.Slots[1].count);
    }

    [Test]
    public void 범위를_벗어난_슬롯_이동은_무시된다()
    {
        var inv = new Inventory(2);
        Assert.DoesNotThrow(() => inv.MoveOrSwap(0, 5));
        Assert.DoesNotThrow(() => inv.MoveOrSwap(-1, 0));
    }

    [Test]
    public void 변경되면_Changed가_발생한다()
    {
        var inv = new Inventory(2);
        int 횟수 = 0;
        inv.Changed += () => 횟수++;

        inv.TryAdd(아이템("scrap", 99), 5);

        Assert.AreEqual(1, 횟수);
    }

    [Test]
    public void 아무것도_넣지_못하면_Changed가_발생하지_않는다()
    {
        var scrap = 아이템("scrap", 1);
        var inv = new Inventory(1);
        inv.TryAdd(scrap, 1);

        int 횟수 = 0;
        inv.Changed += () => 횟수++;
        int 남은수 = inv.TryAdd(scrap, 1);

        Assert.AreEqual(1, 남은수);
        Assert.AreEqual(0, 횟수);
    }

    [Test]
    public void null_아이템_추가는_전량_반환한다()
    {
        var inv = new Inventory(2);
        Assert.AreEqual(5, inv.TryAdd(null, 5));
    }

    [Test]
    public void 영개_이하_추가는_아무것도_하지_않는다()
    {
        var inv = new Inventory(2);
        Assert.AreEqual(0, inv.TryAdd(아이템("scrap", 99), 0));
        Assert.AreEqual(0, inv.CountOf("scrap"));
    }
}
