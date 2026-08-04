using NUnit.Framework;
using UnityEngine;
using Survive.Items;

/// <summary>
/// 사망 드롭 규칙 (P2 스펙 §6-3). "무엇을 잃고 무엇을 남기는가"가 여기서 정해지므로,
/// 경계는 전부 여기서 잡는다 — 씬을 띄워 죽어 보는 것으로는 늦다.
/// </summary>
public class DeathDropTests
{
    static ItemDataSO 아이템(string id, ItemCategory category, int maxStack = 99)
    {
        var it = ScriptableObject.CreateInstance<ItemDataSO>();
        it.id = id;
        it.displayName = id;
        it.maxStack = maxStack;
        it.category = category;
        return it;
    }

    static ItemDataSO 자원(string id = "scrap", int maxStack = 99)
        => 아이템(id, ItemCategory.Resource, maxStack);

    static ItemDataSO 도구(string id = "lantern") => 아이템(id, ItemCategory.Tool, 1);

    // ── 분류 규칙 ────────────────────────────────────────────

    [Test]
    public void 자원은_떨군다()
        => Assert.IsTrue(DeathDrop.DropsOnDeath(ItemCategory.Resource));

    [Test]
    public void 소비품은_떨군다()
        => Assert.IsTrue(DeathDrop.DropsOnDeath(ItemCategory.Consumable));

    [Test]
    public void 도구는_남는다()
        => Assert.IsFalse(DeathDrop.DropsOnDeath(ItemCategory.Tool),
                          "랜턴을 잃으면 회수하러 갈 수단까지 잃는다");

    [Test]
    public void 진행_아이템은_남는다()
        => Assert.IsFalse(DeathDrop.DropsOnDeath(ItemCategory.Quest));

    [Test]
    public void 정의가_없으면_떨구지_않는다()
    {
        ItemDataSO 없음 = null;
        Assert.IsFalse(DeathDrop.DropsOnDeath(없음));
    }

    // ── 떼어내기 ─────────────────────────────────────────────

    [Test]
    public void 떨굴_것만_떼어내고_나머지는_남긴다()
    {
        var inv = new Inventory(6);
        inv.TryAdd(자원("scrap"), 30);
        inv.TryAdd(아이템("repair_kit", ItemCategory.Consumable, 5), 2);
        inv.TryAdd(도구("lantern"), 1);
        inv.TryAdd(아이템("portal_key", ItemCategory.Quest, 1), 1);

        var 떨군것 = DeathDrop.Extract(inv);

        Assert.AreEqual(0, inv.CountOf("scrap"));
        Assert.AreEqual(0, inv.CountOf("repair_kit"));
        Assert.AreEqual(1, inv.CountOf("lantern"), "도구는 그대로 있어야 한다");
        Assert.AreEqual(1, inv.CountOf("portal_key"), "진행 아이템은 그대로 있어야 한다");
        Assert.AreEqual(2, 떨군것.Count);
    }

    [Test]
    public void 떼어낸_것은_사본이라_인벤토리와_얽히지_않는다()
    {
        var scrap = 자원("scrap");
        var inv = new Inventory(3);
        inv.TryAdd(scrap, 10);

        var 떨군것 = DeathDrop.Extract(inv);
        inv.TryAdd(scrap, 4);       // 죽은 뒤 새로 주운 것

        Assert.AreEqual(10, 떨군것[0].count, "가방 몫이 인벤토리 변화에 따라가면 안 된다");
        Assert.AreEqual(4, inv.CountOf("scrap"));
    }

    [Test]
    public void 여러_슬롯에_걸친_것은_슬롯_단위로_떼어낸다()
    {
        var scrap = 자원("scrap", 10);
        var inv = new Inventory(4);
        inv.TryAdd(scrap, 25);      // 10 / 10 / 5

        var 떨군것 = DeathDrop.Extract(inv);

        Assert.AreEqual(3, 떨군것.Count);
        Assert.AreEqual(25, 떨군것[0].count + 떨군것[1].count + 떨군것[2].count);
        Assert.AreEqual(0, inv.CountOf("scrap"));
    }

    [Test]
    public void 떨굴_것이_없으면_빈_목록이다()
    {
        var inv = new Inventory(3);
        inv.TryAdd(도구("pickaxe"), 1);

        Assert.AreEqual(0, DeathDrop.Extract(inv).Count);
        Assert.AreEqual(1, inv.CountOf("pickaxe"));
    }

    [Test]
    public void 빈_인벤토리를_떼어내도_터지지_않는다()
    {
        Assert.AreEqual(0, DeathDrop.Extract(new Inventory(3)).Count);
        Assert.AreEqual(0, DeathDrop.Extract(null).Count);
    }

    [Test]
    public void 떼어내면_Changed가_울린다()
    {
        var inv = new Inventory(3);
        inv.TryAdd(자원("scrap"), 5);

        int 횟수 = 0;
        inv.Changed += () => 횟수++;
        DeathDrop.Extract(inv);

        Assert.AreEqual(1, 횟수, "소지품 화면이 죽기 전 상태로 남으면 안 된다");
    }

    // ── 가방에 담기 ──────────────────────────────────────────

    [Test]
    public void 가방이_넉넉하면_전부_담긴다()
    {
        var inv = new Inventory(4);
        inv.TryAdd(자원("scrap"), 30);
        inv.TryAdd(자원("alien_alloy", 20), 10);

        var 가방 = new Inventory(4);
        Assert.AreEqual(0, DeathDrop.Fill(가방, DeathDrop.Extract(inv)));
        Assert.AreEqual(30, 가방.CountOf("scrap"));
        Assert.AreEqual(10, 가방.CountOf("alien_alloy"));
    }

    [Test]
    public void 가방이_좁으면_못_담은_개수를_돌려준다()
    {
        var inv = new Inventory(4);
        inv.TryAdd(자원("scrap", 10), 25);      // 10 / 10 / 5

        var 가방 = new Inventory(1);            // 10개까지만
        int 남은수 = DeathDrop.Fill(가방, DeathDrop.Extract(inv));

        Assert.AreEqual(15, 남은수);
        Assert.AreEqual(10, 가방.CountOf("scrap"));
    }

    // ── 회수 ─────────────────────────────────────────────────

    [Test]
    public void 회수하면_전부_돌아온다()
    {
        var inv = new Inventory(6);
        inv.TryAdd(자원("scrap"), 30);
        inv.TryAdd(아이템("oxygen_filter", ItemCategory.Consumable, 5), 3);

        var 가방 = new Inventory(6);
        DeathDrop.Fill(가방, DeathDrop.Extract(inv));

        int 옮긴수 = DeathDrop.Retrieve(가방, inv);

        Assert.AreEqual(33, 옮긴수);
        Assert.AreEqual(30, inv.CountOf("scrap"));
        Assert.AreEqual(3, inv.CountOf("oxygen_filter"));
        Assert.IsFalse(DeathDrop.HasAnything(가방), "다 돌려줬으면 가방은 비어야 한다");
    }

    [Test]
    public void 자리가_모자라면_남은_것은_가방에_남는다()
    {
        var scrap = 자원("scrap", 10);
        var 가방 = new Inventory(4);
        가방.TryAdd(scrap, 25);

        var inv = new Inventory(1);             // 10개만 들어간다
        int 옮긴수 = DeathDrop.Retrieve(가방, inv);

        Assert.AreEqual(10, 옮긴수);
        Assert.AreEqual(10, inv.CountOf("scrap"));
        Assert.AreEqual(15, 가방.CountOf("scrap"), "못 받은 것이 증발하면 헛걸음이 된다");
    }

    [Test]
    public void 같은_아이템이_여러_슬롯에_있어도_수량이_맞는다()
    {
        var scrap = 자원("scrap", 10);
        var 가방 = new Inventory(4);
        가방.TryAdd(scrap, 25);                 // 10 / 10 / 5

        var inv = new Inventory(6);
        Assert.AreEqual(25, DeathDrop.Retrieve(가방, inv));
        Assert.AreEqual(25, inv.CountOf("scrap"));
        Assert.IsFalse(DeathDrop.HasAnything(가방));
    }

    [Test]
    public void 빈_가방을_회수하면_아무_일도_없다()
    {
        var inv = new Inventory(3);
        Assert.AreEqual(0, DeathDrop.Retrieve(new Inventory(3), inv));
        Assert.AreEqual(0, DeathDrop.Retrieve(null, inv));
        Assert.AreEqual(0, DeathDrop.Retrieve(new Inventory(3), null));
    }

    [Test]
    public void 남은_것이_있는지_판정한다()
    {
        var 가방 = new Inventory(2);
        Assert.IsFalse(DeathDrop.HasAnything(가방));
        Assert.IsFalse(DeathDrop.HasAnything(null));

        가방.TryAdd(자원("scrap"), 1);
        Assert.IsTrue(DeathDrop.HasAnything(가방));
    }

    // ── 왕복 ─────────────────────────────────────────────────

    [Test]
    public void 죽고_회수하면_원래_수량으로_돌아온다()
    {
        var inv = new Inventory(8);
        inv.TryAdd(자원("scrap"), 47);
        inv.TryAdd(자원("alien_alloy", 20), 13);
        inv.TryAdd(도구("pickaxe"), 1);
        inv.TryAdd(도구("lantern"), 1);

        var 가방 = new Inventory(8);
        DeathDrop.Fill(가방, DeathDrop.Extract(inv));

        // 죽은 직후: 도구만 남아 있다
        Assert.AreEqual(0, inv.CountOf("scrap"));
        Assert.AreEqual(1, inv.CountOf("pickaxe"));
        Assert.AreEqual(1, inv.CountOf("lantern"));

        DeathDrop.Retrieve(가방, inv);

        Assert.AreEqual(47, inv.CountOf("scrap"));
        Assert.AreEqual(13, inv.CountOf("alien_alloy"));
        Assert.AreEqual(1, inv.CountOf("pickaxe"));
        Assert.AreEqual(1, inv.CountOf("lantern"));
    }
}
