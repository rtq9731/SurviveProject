using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Survive.Building;
using Survive.Crafting;
using Survive.Harvesting;
using Survive.Items;

/// <summary>
/// 버섯 목재가 실제로 게임 데이터에 들어가 있는가.
///
/// 규칙 상수만 맞아도 소용이 없다. 목재가 <see cref="ItemDatabaseSO"/>에 없으면
/// 벌목 노드는 세워지지도 않고, 건축 비용이 여전히 스크랩뿐이면 "다리가 벌목을
/// 요구한다"는 설계가 데이터에서 사라진 채 코드에만 남는다. 그래서 에셋을
/// 직접 열어 본다.
///
/// <b>밸런스 축.</b> 관문 2→3의 18m 간격과 +6.6m 높이차(HANDOFF §2.2)를
/// 4m 격자·3m 층고(E2EModularBuild.Cell, Piece_Wall/Ramp 프리팹)로 나누면
/// 토대 1 + 바닥 5 + 경사로 3 = 아홉 조각이다. 그 목재 값이 거대 버섯
/// 몇 그루인지가 이 게임의 벌목 압력이다.
/// </summary>
public class MushroomWoodDataTests
{
    const string DatabasePath = "Assets/08.Data/Items/ItemDatabase.asset";
    const string CatalogPath = "Assets/08.Data/Buildables/BuildCatalog.asset";
    const string BookPath = "Assets/08.Data/Recipes/RecipeBook.asset";

    static ItemDatabaseSO Database =>
        AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(DatabasePath);

    static BuildCatalogSO Catalog =>
        AssetDatabase.LoadAssetAtPath<BuildCatalogSO>(CatalogPath);

    static RecipeBookSO Book =>
        AssetDatabase.LoadAssetAtPath<RecipeBookSO>(BookPath);

    static int CostOf(BuildableSO b, string itemId)
    {
        if (b?.cost == null) return 0;
        return b.cost.Where(c => c.item != null && c.item.id == itemId).Sum(c => c.count);
    }

    // ── 아이템 ───────────────────────────────────────────────

    [Test]
    public void 목재가_아이템_데이터베이스에_있다()
    {
        var db = Database;
        Assert.IsNotNull(db, DatabasePath + "를 찾지 못했다");

        var wood = db.GetById(MushroomLumberRule.WoodItemId);
        Assert.IsNotNull(wood,
            "목재가 목록에 없으면 벌목 노드가 세워지지 않는다 (MushroomLumberService)");
        Assert.AreEqual(ItemCategory.Resource, wood.category);
        Assert.Greater(wood.maxStack, 1, "다리 한 채분을 지고 다녀야 한다");
    }

    // ── 건축 ─────────────────────────────────────────────────

    [Test]
    public void 모든_조각이_목재를_요구한다()
    {
        var catalog = Catalog;
        Assert.IsNotNull(catalog, CatalogPath + "를 찾지 못했다");

        var pieces = catalog.entries.Where(b => b != null && b.IsModular).ToList();
        Assert.IsNotEmpty(pieces, "조립 조각이 하나도 없다");

        foreach (var p in pieces)
            Assert.Greater(CostOf(p, MushroomLumberRule.WoodItemId), 0,
                           $"{p.id}이(가) 목재 없이 지어진다");
    }

    [Test]
    public void 조각_비용은_목재_중심이고_스크랩은_소량이다()
    {
        foreach (var p in Catalog.entries.Where(b => b != null && b.IsModular))
        {
            int wood = CostOf(p, MushroomLumberRule.WoodItemId);
            int scrap = CostOf(p, "scrap");
            Assert.Greater(wood, scrap,
                           $"{p.id}: 목재({wood})가 스크랩({scrap})보다 많아야 목재 중심이다");
        }
    }

    [Test]
    public void 다리_하나는_거대_버섯_열_그루_안쪽이다()
    {
        var catalog = Catalog;
        int 토대 = CostOf(catalog.GetById("piece_foundation"), MushroomLumberRule.WoodItemId);
        int 바닥 = CostOf(catalog.GetById("piece_floor"), MushroomLumberRule.WoodItemId);
        int 경사로 = CostOf(catalog.GetById("piece_ramp"), MushroomLumberRule.WoodItemId);

        // 18m ÷ 4m 격자 = 바닥 5장(20m, 2m는 물림), +6.6m ÷ 3m 층고 = 경사로 3개,
        // 그리고 섬2 쪽 발판이 될 토대 1개.
        int 다리목재 = 토대 + 바닥 * 5 + 경사로 * 3;

        float 한그루평균 = (MushroomLumberRule.MinYield + MushroomLumberRule.MaxYield) / 2f;
        float 그루수 = 다리목재 / 한그루평균;

        Assert.Greater(그루수, 5f, $"다리 하나가 {그루수:F1}그루면 벌목이 관문이 되지 않는다");
        Assert.LessOrEqual(그루수, 10f, $"다리 하나가 {그루수:F1}그루면 노동이다");
    }

    // ── 레시피 ───────────────────────────────────────────────

    [Test]
    public void 수리_키트에_목재가_들어간다()
    {
        var book = Book;
        Assert.IsNotNull(book, BookPath + "를 찾지 못했다");

        var kit = book.recipes.FirstOrDefault(r => r != null && r.id == "repair_kit");
        Assert.IsNotNull(kit, "수리 키트 레시피를 찾지 못했다");

        Assert.IsTrue(
            kit.ingredients.Any(i => i.item != null && i.item.id == MushroomLumberRule.WoodItemId),
            "부목이 될 단단한 것이 있어야 부러진 데를 고칠 수 있다");
    }

    [Test]
    public void 도끼_레시피가_있고_목재를_요구하지_않는다()
    {
        // 순환 의존 방지. 목재를 얻으려면 도끼가 있어야 하는데
        // 도끼에 목재가 들어가면 아무도 첫 도끼를 만들 수 없다.
        var axe = Book.recipes.FirstOrDefault(r => r != null && r.id == "axe");
        Assert.IsNotNull(axe, "도끼 레시피가 없으면 벌목이 영영 잠긴다");
        Assert.AreEqual(StationType.None, axe.requiredStation,
                        "도끼는 손에서 만들 수 있어야 한다 — 잃어도 언제든 다시 만든다");
        Assert.IsFalse(
            axe.ingredients.Any(i => i.item != null && i.item.id == MushroomLumberRule.WoodItemId),
            "도끼에 목재가 들어가면 벌목과 순환 의존이 된다");
        Assert.IsTrue(axe.ingredients.All(i => i.item != null && i.item.id != "axe"));
    }

    [Test]
    public void 도끼는_곡괭이와_같은_자릿수로_만든다()
    {
        var axe = Book.recipes.First(r => r != null && r.id == "axe");
        var pickaxe = Book.recipes.First(r => r != null && r.id == "pickaxe");

        int a = axe.ingredients.Sum(i => i.count);
        int p = pickaxe.ingredients.Sum(i => i.count);
        Assert.LessOrEqual(a, p * 2, $"도끼 {a}개 vs 곡괭이 {p}개 — 자릿수가 벌어졌다");
        Assert.GreaterOrEqual(a * 2, p, $"도끼 {a}개 vs 곡괭이 {p}개 — 자릿수가 벌어졌다");
    }

    [Test]
    public void 도끼가_아이템_데이터베이스에_있다()
    {
        var axe = Database.GetById("axe") as ToolItemSO;
        Assert.IsNotNull(axe, "도끼가 목록에 없으면 만들어도 손에 들어오지 않는다");
        Assert.AreEqual(Survive.Items.ToolType.Axe, axe.toolType);
        Assert.GreaterOrEqual(axe.tier, MushroomLumberRule.RequiredTier,
                              "세계에 있는 유일한 도끼가 거대 버섯을 못 베면 안 된다");
        Assert.IsNotEmpty(axe.socketChildName, "손에 들었을 때 보일 것이 있어야 한다");
    }

    [Test]
    public void 곡괭이는_목재를_요구하지_않는다()
    {
        var pickaxe = Book.recipes.FirstOrDefault(r => r != null && r.id == "pickaxe");
        Assert.IsNotNull(pickaxe);
        Assert.IsFalse(
            pickaxe.ingredients.Any(i => i.item != null && i.item.id == MushroomLumberRule.WoodItemId),
            "곡괭이에 목재가 들어가면 벌목과 순환 의존이 된다");
    }

    [Test]
    public void 벌목이_하드락되지_않는다()
    {
        // 도끼를 잃어도 막히지 않는다는 논리를 데이터로 확인한다:
        // ① 도끼는 손 제작이고 ② 그 재료는 전부 맨손 채집물(스크랩·기계 부품)이다.
        var axe = Book.recipes.First(r => r != null && r.id == "axe");
        var 맨손자원 = new[] { "scrap", "machine_part", "mushroom_cap", "fern_fiber" };

        foreach (var i in axe.ingredients)
            Assert.Contains(i.item.id, 맨손자원,
                            $"{i.item.id}는 맨손으로 얻을 수 없다 — 도끼를 잃으면 다시 못 만든다");
    }

    // ── 화톳불 ───────────────────────────────────────────────

    [Test]
    public void 화톳불은_목재_없이도_세울_수_있다()
    {
        // 첫 불은 곡괭이보다 먼저 세워질 수 있어야 한다. 세우는 데 목재가 들면
        // 목재를 얻을 도구가 없는 동안 거점 자체가 막힌다.
        // (그 대신 불을 <b>지키는</b> 데는 목재가 든다 — CampfireFuelRule.)
        var fire = Catalog.GetById("campfire");
        Assert.IsNotNull(fire);
        Assert.AreEqual(0, CostOf(fire, MushroomLumberRule.WoodItemId),
                        "화톳불 건축 비용에 목재가 들어가면 초반이 막힌다");
    }
}
