using NUnit.Framework;
using UnityEngine;
using Survive.Crafting;
using Survive.Items;
using Survive.Progression;

/// <summary>
/// 채널 1 — 재료를 처음 손에 넣으면 AI가 반응하고 청사진이 열린다.
///
/// 여기서 지키는 것: 첫 습득만 울리는 것, 매핑 없는 아이템은 조용한 것,
/// 그리고 <b>재료가 있어도 모르면 못 만든다</b>는 이중 잠금 — 이것이
/// 재료 게이팅 위에 한 겹 얹는 이 기능의 전부다.
/// </summary>
public class FieldDiscoveryTests
{
    UnlockLedger _ledger;
    ItemDataSO _scrap;
    ItemDataSO _fiber;
    BlueprintSO _scrapworks;
    DiscoveryBookSO _book;

    [SetUp]
    public void SetUp()
    {
        _ledger = new UnlockLedger();
        _scrap = 아이템("scrap", "스크랩");
        _fiber = 아이템("fern_fiber", "양치 섬유");
        _scrapworks = 청사진("bp_scrapworks");
        _book = 매핑표(발견("disc_scrap", _scrap, "쓸 만한 껍질이다", _scrapworks));
    }

    // ── 첫 습득 ──────────────────────────────────────────────

    [Test]
    public void 처음_주우면_청사진이_열린다()
    {
        Assert.IsTrue(FieldDiscovery.TryDiscover(_book, _ledger, "scrap", out var d));

        Assert.AreEqual("disc_scrap", d.id);
        Assert.IsTrue(_ledger.IsUnlocked("bp_scrapworks"));
    }

    [Test]
    public void 두_번째부터는_조용하다()
    {
        Assert.IsTrue(FieldDiscovery.TryDiscover(_book, _ledger, "scrap", out _));
        Assert.IsFalse(FieldDiscovery.TryDiscover(_book, _ledger, "scrap", out var 두번째),
            "같은 재료를 또 주웠다고 AI가 또 말하면 안 된다");
        Assert.IsNull(두번째);
    }

    [Test]
    public void 발견_자체도_원장에_남아_저장을_왕복한다()
    {
        FieldDiscovery.TryDiscover(_book, _ledger, "scrap", out _);

        var 이어받은판 = new UnlockLedger();
        이어받은판.Restore(_ledger.Capture());

        Assert.IsFalse(FieldDiscovery.TryDiscover(_book, 이어받은판, "scrap", out _),
            "불러온 뒤에 다시 주워도 첫 습득이 되풀이되면 안 된다");
    }

    [Test]
    public void 매핑에_없는_재료는_아무_일도_없다()
    {
        Assert.IsFalse(FieldDiscovery.TryDiscover(_book, _ledger, "fern_fiber", out var d));
        Assert.IsNull(d);
        Assert.AreEqual(0, _ledger.Count);
    }

    [Test]
    public void 매핑표나_원장이_없으면_조용히_넘어간다()
    {
        Assert.IsFalse(FieldDiscovery.TryDiscover(null, _ledger, "scrap", out _));
        Assert.IsFalse(FieldDiscovery.TryDiscover(_book, null, "scrap", out _));
        Assert.IsFalse(FieldDiscovery.TryDiscover(_book, _ledger, null, out _));
        Assert.IsFalse(FieldDiscovery.TryDiscover(_book, _ledger, "", out _));
    }

    [Test]
    public void 발견_id가_비어_있으면_아이템_id로_열쇠를_만든다()
    {
        var book = 매핑표(발견(null, _fiber, "질기다", 청사진("bp_fibercraft")));

        Assert.IsTrue(FieldDiscovery.TryDiscover(book, _ledger, "fern_fiber", out _));
        Assert.IsTrue(_ledger.IsUnlocked("discovery:fern_fiber"));
        Assert.IsFalse(FieldDiscovery.TryDiscover(book, _ledger, "fern_fiber", out _));
    }

    [Test]
    public void 여는_청사진이_여럿이면_전부_열린다()
    {
        var a = 청사진("bp_a");
        var b = 청사진("bp_b");
        var book = 매핑표(발견("disc_scrap", _scrap, "쓸 만하다", a, b));

        FieldDiscovery.TryDiscover(book, _ledger, "scrap", out _);

        Assert.IsTrue(_ledger.IsUnlocked("bp_a"));
        Assert.IsTrue(_ledger.IsUnlocked("bp_b"));
    }

    [Test]
    public void 빈_칸이_섞인_매핑표도_버티고_찾는다()
    {
        var book = ScriptableObject.CreateInstance<DiscoveryBookSO>();
        var 아이템없는발견 = ScriptableObject.CreateInstance<DiscoverySO>();
        book.discoveries = new[]
        {
            null, 아이템없는발견, 발견("disc_scrap", _scrap, "쓸 만하다", _scrapworks)
        };

        Assert.IsTrue(FieldDiscovery.TryDiscover(book, _ledger, "scrap", out _));
        Assert.IsNull(book.Find("없는아이템"));
        Assert.IsNull(book.Find(null));
    }

    // ── 이중 잠금: 재료가 있어도 모르면 못 만든다 ─────────────

    [Test]
    public void 재료가_있어도_청사진이_없으면_못_만든다()
    {
        var (inv, recipe) = 곡괭이판();
        recipe.requiredBlueprint = _scrapworks;

        Assert.IsFalse(CraftingService.CanCraft(recipe, inv, StationType.None, _ledger));
        Assert.AreEqual(0, CraftQueueService.MaxCraftable(recipe, inv, StationType.None, _ledger));

        var queue = new CraftQueue();
        Assert.IsFalse(CraftQueueService.TryEnqueue(queue, recipe, 1, inv, StationType.None, _ledger));
        Assert.AreEqual(0, queue.Count);
        Assert.AreEqual(5, inv.CountOf("scrap"), "실패했으면 재료도 그대로여야 한다");
    }

    [Test]
    public void 첫_습득으로_열리면_바로_만들_수_있다()
    {
        var (inv, recipe) = 곡괭이판();
        recipe.requiredBlueprint = _scrapworks;

        FieldDiscovery.TryDiscover(_book, _ledger, "scrap", out _);

        Assert.IsTrue(CraftingService.CanCraft(recipe, inv, StationType.None, _ledger));

        var queue = new CraftQueue();
        Assert.IsTrue(CraftQueueService.TryEnqueue(queue, recipe, 1, inv, StationType.None, _ledger));
        Assert.AreEqual(1, queue.Count);
    }

    [Test]
    public void 청사진이_열려도_재료가_없으면_못_만든다()
    {
        var (inv, recipe) = 곡괭이판();
        recipe.requiredBlueprint = _scrapworks;
        inv.TryRemove("scrap", 5);

        _ledger.Unlock("bp_scrapworks");

        Assert.IsFalse(CraftingService.CanCraft(recipe, inv, StationType.None, _ledger),
            "두 잠금은 독립이다 — 하나가 열렸다고 다른 하나가 풀리지 않는다");
    }

    [Test]
    public void 요구_청사진이_없는_레시피는_원장과_무관하게_만들_수_있다()
    {
        var (inv, recipe) = 곡괭이판();

        Assert.IsTrue(CraftingService.CanCraft(recipe, inv, StationType.None, _ledger));
        Assert.IsTrue(CraftingService.CanCraft(recipe, inv, StationType.None));
    }

    // ── 조립 도우미 ──────────────────────────────────────────

    static ItemDataSO 아이템(string id, string name, int maxStack = 99)
    {
        var item = ScriptableObject.CreateInstance<ItemDataSO>();
        item.id = id;
        item.displayName = name;
        item.maxStack = maxStack;
        return item;
    }

    static BlueprintSO 청사진(string id)
    {
        var bp = ScriptableObject.CreateInstance<BlueprintSO>();
        bp.id = id;
        bp.displayName = id;
        return bp;
    }

    static DiscoverySO 발견(string id, ItemDataSO item, string text, params BlueprintSO[] unlocks)
    {
        var d = ScriptableObject.CreateInstance<DiscoverySO>();
        d.id = id;
        d.item = item;
        d.unlocks = unlocks;
        d.line = new Survive.Narrative.SequenceSO.Line
        {
            speaker = "우주복 AI", text = text, holdSeconds = 4f
        };
        return d;
    }

    static DiscoveryBookSO 매핑표(params DiscoverySO[] discoveries)
    {
        var book = ScriptableObject.CreateInstance<DiscoveryBookSO>();
        book.discoveries = discoveries;
        return book;
    }

    /// <summary>스크랩 5개와 그걸 쓰는 레시피 하나.</summary>
    (Inventory, RecipeSO) 곡괭이판()
    {
        var inv = new Inventory(8);
        inv.TryAdd(_scrap, 5);

        var recipe = ScriptableObject.CreateInstance<RecipeSO>();
        recipe.id = "pickaxe";
        recipe.displayName = "곡괭이";
        recipe.craftSeconds = 1f;
        recipe.ingredients = new[] { new ItemStack { item = _scrap, count = 5 } };
        recipe.result = new ItemStack { item = 아이템("pickaxe", "곡괭이", 1), count = 1 };
        return (inv, recipe);
    }
}
