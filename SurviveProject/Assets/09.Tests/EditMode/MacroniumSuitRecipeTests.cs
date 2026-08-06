using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Survive.Crafting;
using Survive.Items;
using Survive.Localization;
using Survive.Progression;
using Survive.World;

/// <summary>
/// 실행 스펙 §8-1의 실제 데이터. 코드가 맞아도 에셋이 틀리면 게임 안에서는
/// 아무 일도 일어나지 않으므로, 제작 UI가 실제로 읽는 <c>RecipeBook</c>과
/// <c>ItemDatabase</c>를 그대로 열어 본다. <c>SurfaceWalkerRecipeTests</c>와 같은 자세다.
///
/// <b>여기서 지키는 것은 채널과 순환이다.</b>
/// <list type="number">
/// <item><b>연구 채널이 아니라 재료 기반</b>이다(기획서 갱신점 _3 §2). 방호복 설계는
///       연구대에서 나오지 않고 무광버섯을 처음 쥐는 순간 열린다</item>
/// <item><b>이 장비가 여는 곳의 자원을 재료로 쓰지 않는다.</b> 방호복은 B섬 지하로
///       가는 문이므로 지하의 것(매크로늄·석영)을 요구하면 영영 못 만든다 —
///       액면 보행 장비가 매크로늄을 안 쓰는 것과 같은 이유다</item>
/// </list>
/// </summary>
public class MacroniumSuitRecipeTests
{
    const string BookPath = "Assets/08.Data/Recipes/RecipeBook.asset";
    const string DbPath = "Assets/08.Data/Items/ItemDatabase.asset";
    const string DiscoveryBookPath = "Assets/08.Data/Progression/Resources/DiscoveryBook.asset";

    const string 방호복 = "macronium_suit";
    const string 무광버섯 = "matte_mushroom";
    const string 청사진 = "bp_macronium_suit";

    static RecipeSO 레시피()
    {
        var book = AssetDatabase.LoadAssetAtPath<RecipeBookSO>(BookPath);
        Assert.IsNotNull(book, $"{BookPath}를 못 읽었다");

        var r = book.recipes.FirstOrDefault(x => x != null && x.id == 방호복);
        Assert.IsNotNull(r, $"레시피북에 {방호복}이 없다 — 제작 UI에 안 뜬다");
        return r;
    }

    static ItemDatabaseSO DB()
    {
        var db = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(DbPath);
        Assert.IsNotNull(db, $"{DbPath}를 못 읽었다");
        return db;
    }

    // ── 아이템 ───────────────────────────────────────────────

    [Test]
    public void 아이템_DB에_등록돼_있다()
    {
        // 등록이 빠지면 세이브를 다시 불러왔을 때 조용히 사라진다.
        Assert.IsTrue(DB().TryGetById(방호복, out var item), $"ItemDatabase에 {방호복}이 없다");
        Assert.AreSame(레시피().result.item, item, "레시피 결과와 DB의 것이 다른 에셋이다");
    }

    [Test]
    public void 결과물이_잠수를_뚫는_장비로_선언돼_있다()
    {
        var gear = 레시피().result.item as TraversalGearItemSO;
        Assert.IsNotNull(gear, "TraversalGearItemSO가 아니면 인벤토리에 넣어도 판정에 잡히지 않는다");
        Assert.AreEqual(TraversalGear.MacroniumSuit, gear.gear);
        Assert.AreEqual(1, 레시피().result.count);
    }

    [Test]
    public void 이름과_설명이_번역_표에_있다()
    {
        var item = 레시피().result.item;
        Assert.IsTrue(Loc.TryT(DataText.NameKey(item), out _), $"Item/{방호복}.name이 표에 없다");
        Assert.IsTrue(Loc.TryT(DataText.DescKey(item), out _), $"Item/{방호복}.desc가 표에 없다");
    }

    [Test]
    public void 한_벌만_들고_다닌다()
    {
        Assert.AreEqual(1, 레시피().result.item.maxStack, "몸에 걸치는 장비를 쌓을 이유가 없다");
    }

    // ── 재료 ─────────────────────────────────────────────────

    [Test]
    public void 무광버섯으로_만든다()
    {
        // 재료가 곧 픽션이다 — 매크로늄을 빨아들이는 갓이라야 차폐가 설명된다.
        Assert.IsTrue(레시피().ingredients.Any(i => i?.item != null && i.item.id == 무광버섯),
                      "무광버섯이 안 들어간다 — 차폐의 근거가 사라진다");
    }

    [TestCase("macronium")]
    [TestCase("macronium_quartz")]
    public void 지하에서만_나는_것을_요구하지_않는다(string 지하자원)
    {
        // 방호복이 여는 곳이 B섬 지하다. 그 안의 것을 재료로 쓰면 순환이라 영영 못 만든다.
        Assert.IsFalse(레시피().ingredients.Any(i => i?.item != null && i.item.id == 지하자원),
                       $"{지하자원}은 이 장비로 들어가는 곳에서 나온다 — 만들 방법이 없어진다");
    }

    [Test]
    public void 재료가_전부_실재한다()
    {
        foreach (var i in 레시피().ingredients)
        {
            Assert.IsNotNull(i?.item, "빈 재료 칸이 있다");
            Assert.Greater(i.count, 0, $"{i.item.id}의 수량이 0이다");
            Assert.IsTrue(DB().TryGetById(i.item.id, out _), $"ItemDatabase에 {i.item.id}가 없다");
        }
    }

    [Test]
    public void 제작대에서만_만든다()
    {
        Assert.AreEqual(StationType.Bench, 레시피().requiredStation,
                        "티어 장비를 맨손으로 만들면 거점을 세울 이유가 준다");
        Assert.Greater(레시피().craftSeconds, 0f, "만드는 데 시간이 든다");
    }

    // ── 채널 — 연구가 아니라 재료다 ──────────────────────────

    [Test]
    public void 청사진을_알아야_만든다()
    {
        var bp = 레시피().requiredBlueprint;
        Assert.IsNotNull(bp, "청사진이 없으면 처음부터 열려 있다 — 관문이 아니게 된다");
        Assert.AreEqual(청사진, bp.id);
    }

    [Test]
    public void 설계는_연구대가_아니라_무광버섯이_연다()
    {
        // 기획서 갱신점 _3 §2 — 티어 2·4는 연구 채널이고 티어 3은 재료 기반이다.
        var book = AssetDatabase.LoadAssetAtPath<DiscoveryBookSO>(DiscoveryBookPath);
        Assert.IsNotNull(book, $"{DiscoveryBookPath}를 못 읽었다");

        var d = book.discoveries.FirstOrDefault(
            x => x != null && x.unlocks != null && x.unlocks.Any(b => b != null && b.id == 청사진));

        Assert.IsNotNull(d, $"{청사진}을 여는 현장 발견이 없다 — 설계가 어디서도 열리지 않는다");
        Assert.IsNotNull(d.item, "재료 계기가 아니라 장소 계기다 — 재료 기반이 아니게 된다");
        Assert.AreEqual(무광버섯, d.item.id, "무광버섯이 아닌 것이 방호복 설계를 연다");

        // 연구대 쪽에서도 열리면 채널이 둘이 되고, 어느 쪽으로 왔는지 알 수 없어진다.
        foreach (var res in Load연구())
            Assert.IsFalse(res.unlocks != null && res.unlocks.Any(b => b != null && b.id == 청사진),
                           $"연구 {res.id}도 방호복 설계를 연다 — 채널이 둘이다");
    }

    static ResearchEntrySO[] Load연구() =>
        AssetDatabase.FindAssets("t:ResearchEntrySO", new[] { "Assets/08.Data" })
                     .Select(g => AssetDatabase.LoadAssetAtPath<ResearchEntrySO>(AssetDatabase.GUIDToAssetPath(g)))
                     .Where(r => r != null)
                     .ToArray();

    // ── 제작에서 판정까지 한 흐름 ────────────────────────────

    [Test]
    public void 제작하면_잠수_구간을_지날_수_있게_된다()
    {
        // 레시피 → 제작 → 인벤토리 → 장비 목록 → 판정. 다섯이 한 줄로 이어지는지 본다.
        var r = 레시피();
        var inv = new Inventory(20);
        foreach (var i in r.ingredients) inv.TryAdd(i.item, i.count);

        var 통로 = new HazardZone(EnvironmentHazard.Submersion, DiveRule.FirstDiveSeconds);

        Assert.IsFalse(DiveRule.CanEnter(통로, TraversalLoadout.From(inv)),
                       "만들기 전에는 막혀 있어야 한다");

        Assert.IsTrue(CraftingService.Craft(r, inv, StationType.Bench),
                      "재료를 다 갖췄는데 제작이 실패했다");
        Assert.AreEqual(1, inv.CountOf(방호복), "만들었는데 인벤토리에 없다");

        Assert.IsTrue(DiveRule.CanEnter(통로, TraversalLoadout.From(inv)),
                      "만들었는데도 막힌다면 제작과 판정이 이어져 있지 않은 것이다");
    }

    [Test]
    public void 재료가_모자라면_못_만든다()
    {
        Assert.IsFalse(CraftingService.CanCraft(레시피(), new Inventory(20), StationType.Bench));
    }
}
