using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Survive.Crafting;
using Survive.Harvesting;
using Survive.Items;
using Survive.Localization;
using Survive.Progression;
using Survive.UI;
using Survive.World;

/// <summary>
/// 조명탄 총의 <b>데이터</b> (기획서 §5.2 · §5.4 티어 3).
///
/// 규칙은 <c>FlareRuleTests</c>가 지킨다. 여기서 지키는 것은 <b>세계에 실제로
/// 서 있는가</b>다 — 코드가 맞아도 에셋이 틀리면 제작 화면에 아무것도 안 뜬다.
/// <c>MacroniumSuitRecipeTests</c>와 같은 자세다.
///
/// <b>재료는 아직 세계에 없다.</b> 매크로늄 석영은 아이템도 채집 노드도 프리팹도
/// 서 있지만 <b>씬에 심긴 인스턴스가 0개</b>다 — 지하가 아직 없기 때문이다.
/// 그래서 이 라운드가 세우는 것은 <b>레시피까지</b>이고, 재료가 세계에 놓이는
/// 것은 지형이 만들어질 때다(스펙 §16). 그 사이에 이 게이트가 지키는 것은
/// <b>재료가 놓이는 날 아무도 배선을 다시 하지 않아도 된다</b>는 것이다.
/// </summary>
public class FlareGunDataTests
{
    const string BookPath = "Assets/08.Data/Recipes/RecipeBook.asset";
    const string DbPath = "Assets/08.Data/Items/ItemDatabase.asset";
    const string DiscoveryBookPath = "Assets/08.Data/Progression/Resources/DiscoveryBook.asset";

    const string 석영 = "macronium_quartz";
    const string 청사진 = "bp_flare_gun";

    static ItemDatabaseSO DB()
    {
        var db = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(DbPath);
        Assert.IsNotNull(db, $"{DbPath}를 못 읽었다");
        return db;
    }

    static RecipeSO 레시피()
    {
        var book = AssetDatabase.LoadAssetAtPath<RecipeBookSO>(BookPath);
        Assert.IsNotNull(book, $"{BookPath}를 못 읽었다");

        var r = book.recipes.FirstOrDefault(x => x != null && x.id == FlareRule.ItemId);
        Assert.IsNotNull(r, $"레시피북에 {FlareRule.ItemId}이 없다 — 제작 화면에 안 뜬다");
        return r;
    }

    // ── 아이템 ───────────────────────────────────────────────

    [Test]
    public void 아이템_DB에_등록돼_있다()
    {
        // 등록이 빠지면 세이브를 다시 불러왔을 때 조용히 사라진다.
        Assert.IsTrue(DB().TryGetById(FlareRule.ItemId, out var item),
                      $"ItemDatabase에 {FlareRule.ItemId}이 없다");
        Assert.AreSame(레시피().result.item, item, "레시피 결과와 DB의 것이 다른 에셋이다");
    }

    [Test]
    public void 손에_드는_도구다()
    {
        // <b>손에 들어야 쏠 수 있다.</b> 쏘는 길이 <c>Combat.MeleeSwing</c>을 지나고
        // 그쪽은 <c>PlayerToolHolder.EquippedTool</c>을 본다 — ToolItemSO가 아니면
        // Q 순환에도 안 들어오고 손에 잡히지도 않는다.
        DB().TryGetById(FlareRule.ItemId, out var item);
        var tool = item as ToolItemSO;
        Assert.IsNotNull(tool, "조명탄 총이 도구가 아니다 — 손에 들 수가 없다");

        Assert.AreEqual(ItemCategory.Tool, item.category);
        Assert.AreEqual(1, item.maxStack, "총을 여러 자루 겹쳐 드는 물건이 아니다");
        Assert.IsNotNull(item.icon, "아이콘이 없으면 바닥에서 알 수 없는 덩어리로 떨어진다");
        Assert.AreEqual(DropVisualKind.IconBillboard, DropVisualRule.Choose(item, false));
    }

    [Test]
    public void 랜턴_자리를_뺏지_않는다()
    {
        // 조명 장비 칸에 걸리면 <c>LanternRule.TierOf</c>가 이것을 랜턴으로 세고,
        // 그 순간 배터리를 태우는 주체가 둘이 된다. 조명탄은 손에 드는 것이지
        // 몸에 다는 것이 아니다.
        DB().TryGetById(FlareRule.ItemId, out var item);

        Assert.AreEqual(EquipmentSlotKind.None, item.equipSlot);
        Assert.AreEqual(0, LanternRule.TierOf(item),
                        "조명탄 총이 랜턴으로 읽힌다 — 배터리를 태우는 주체가 둘이 된다");
    }

    [Test]
    public void 캐는_도구가_아니다()
    {
        // "도구는 전용이다"(§5.3)의 반대편. 총으로 광맥을 깨거나 나무를 베면
        // 티어 3 물건 하나가 곡괭이와 도끼를 대신하게 된다.
        DB().TryGetById(FlareRule.ItemId, out var item);
        var tool = (ToolItemSO)item;

        Assert.AreEqual(ToolType.None, tool.toolType);
        Assert.AreEqual(0f, tool.damage, "쏘는 물건이 때리기까지 하면 전투 균형이 여기서 갈린다");
        Assert.IsFalse(ToolMatch.Satisfies(ToolType.Pickaxe, 1, tool.toolType, tool.tier),
                       "조명탄 총으로 광맥이 깨진다");
        Assert.IsFalse(ToolMatch.Satisfies(ToolType.Axe, 1, tool.toolType, tool.tier),
                       "조명탄 총으로 나무가 베인다");
    }

    [Test]
    public void 난사를_막는_간격이_있다()
    {
        // 쿨타임이 0이면 한 프레임에 배터리가 통째로 사라진다.
        var tool = (ToolItemSO)DB().GetById(FlareRule.ItemId);
        Assert.Greater(tool.attackCooldown, 0f);
    }

    // ── 레시피 ───────────────────────────────────────────────

    [Test]
    public void 매크로늄_석영으로_만든다()
    {
        // 기획서 §5.4 — 티어 3, 매크로늄 석영. 이 재료가 아니면 「조명탄이
        // 자홍으로 터진다」는 근거도 사라진다(재료의 색이 그대로 간다).
        var 재료 = 레시피().ingredients;
        var 석영칸 = 재료.FirstOrDefault(i => i?.item != null && i.item.id == 석영);

        Assert.IsNotNull(석영칸, $"조명탄 총이 {석영} 없이 만들어진다");
        Assert.Greater(석영칸.count, 0);
    }

    [Test]
    public void 티어_3_물건이다()
    {
        var tool = (ToolItemSO)DB().GetById(FlareRule.ItemId);
        Assert.AreEqual(3, tool.tier, "기획서 §5.4의 티어 3이 아니다");
    }

    [Test]
    public void 손에서_뚝딱_나오지_않는다()
    {
        // 티어 3 장비가 휴대 제작이면 진행의 계단이 하나 사라진다.
        var r = 레시피();
        Assert.AreNotEqual(StationType.None, r.requiredStation, "총이 맨손에서 나온다");
        Assert.Greater(r.craftSeconds, 0f, "즉시 완성이면 「어디서 기다릴 것인가」를 묻지 않는다");
    }

    [Test]
    public void 청사진_없이는_못_만든다()
    {
        // 재료를 처음 쥐기 전에 목록에 떠 있으면, 발견 대사가 알려 줄 것이 없어진다.
        var r = 레시피();
        Assert.IsNotNull(r.requiredBlueprint, "처음부터 열려 있다");
        Assert.AreEqual(청사진, r.requiredBlueprint.id);

        var 잠긴것 = new UnlockLedger();
        Assert.IsFalse(MenuListing.ShouldList(r, r.requiredStation, 잠긴것),
                       "아무것도 모르는 상태에서 목록에 뜬다");

        var 아는것 = new UnlockLedger();
        아는것.Unlock(청사진);
        Assert.IsTrue(MenuListing.ShouldList(r, r.requiredStation, 아는것),
                      "청사진을 얻어도 목록에 안 뜬다");
    }

    // ── 현장 발견 ────────────────────────────────────────────

    [Test]
    public void 석영을_처음_쥐면_설계가_열린다()
    {
        // <b>사슬 전체를 여기서 본다.</b> 석영 → 발견 → 청사진 → 레시피.
        // 한 칸이라도 끊기면 AI가 "제작법이 있습니다"라고 말하고 목록은 그대로다
        // (RetiredContentGateTests가 그 상태를 막지만, 어디가 끊겼는지는 말해 주지 않는다).
        var book = AssetDatabase.LoadAssetAtPath<DiscoveryBookSO>(DiscoveryBookPath);
        Assert.IsNotNull(book, $"{DiscoveryBookPath}를 못 읽었다");

        var d = book.Find(석영);
        Assert.IsNotNull(d, $"{석영}을 쥐어도 아무 일도 일어나지 않는다");
        Assert.IsNotNull(d.unlocks, $"{d.id}가 아무것도 열지 않는다");
        Assert.IsTrue(d.unlocks.Any(b => b != null && b.id == 청사진),
                      $"{d.id}가 {청사진}을 열지 않는다");
    }

    [Test]
    public void 발견_대사와_청사진_힌트가_번역_표에_있다()
    {
        // 전수 게이트는 DataTextGateTests가 든다. 이 셋만은 여기서도 못 박는다 —
        // 표를 거치지 않으면 로케일을 바꾼 화면에서만 티가 난다.
        var item = DB().GetById(FlareRule.ItemId);
        Assert.IsTrue(Loc.TryT(DataText.NameKey(item), out _), "Item/flare_gun.name이 표에 없다");
        Assert.IsTrue(Loc.TryT(DataText.DescKey(item), out _), "Item/flare_gun.desc가 표에 없다");

        var bp = 레시피().requiredBlueprint;
        Assert.IsNotEmpty(bp.hint, "청사진에 힌트가 없다 — 어디서 얻는지 알 길이 없다");
        Assert.IsTrue(Loc.TryT(DataText.HintKey(bp), out _), $"Blueprint/{청사진}.hint가 표에 없다");
    }
}
