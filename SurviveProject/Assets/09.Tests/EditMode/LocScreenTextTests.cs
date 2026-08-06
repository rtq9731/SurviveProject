using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Survive.Crafting;
using Survive.Items;
using Survive.Localization;
using Survive.UI;

/// <summary>
/// 이 라운드에 화면에서 표로 옮긴 것들이 <b>실제로 표에서 나오는가</b>.
///
/// 문장 게이트(<c>LocSentenceGateTests</c>)는 "코드에 한글이 없다"까지만 본다.
/// 그것만으로는 값을 엉뚱한 키에서 꺼내도 초록불이다 — 게이트를 통과하는
/// 제일 쉬운 길이 "옮긴 척하기"가 되면 안 된다. 그래서 옮긴 자리마다
/// 표의 값과 화면이 낼 값을 직접 맞춘다.
///
/// 두 번째로 보는 것은 <b>데이터 에셋 이름이 로케일을 따라오는가</b>다.
/// 소지품 쪽지와 제작 목록이 <see cref="DataText"/>를 우회해
/// <c>ItemDataSO.displayName</c>을 직접 읽던 자리를 이번에 없앴는데,
/// 그 구멍은 그 화면을 그 로케일로 열어 보기 전까지 아무 신호도 내지 않는다.
/// </summary>
public class LocScreenTextTests
{
    StringCatalog _catalog;

    [SetUp]
    public void 표를_읽는다()
    {
        _catalog = LocalizationTestBootstrap.LoadCatalogFromDisk();
        Loc.Load(_catalog);
        Loc.SetLocale(StringCatalog.DefaultLocale);
    }

    [TearDown]
    public void 로케일을_되돌린다() => Loc.SetLocale(StringCatalog.DefaultLocale);

    // ── ① 옮긴 자리에 쓰는 이름표가 표에 있다 ────────────────

    /// <summary>
    /// 이 라운드에 화면(UI/·Domain/UI/·Art/)에서 표로 옮긴 이름표 전부.
    /// 누락 키 게이트가 소스를 긁어 이미 대조하지만, 여기 적어 두면
    /// 실수로 줄을 지웠을 때 <b>어느 화면이 비는지</b>가 실패 메시지에 남는다.
    /// </summary>
    static readonly string[] 옮긴_이름표 =
    {
        "codex_title", "codex_caption", "codex_footer", "codex_tab", "codex_summary",
        "codex_empty_title", "codex_empty_body",
        "hint_basics", "hint_pickaxe", "hint_axe", "hint_lantern",
        "battery_amount", "scrap_count", "objective_line", "objective_line_progress",
        "queue_slot_full", "queue_slot_initial_count",
        "gamma_title", "gamma_guide", "gamma_patch_hidden", "gamma_patch_barely",
        "gamma_patch_clear", "gamma_confirm", "gamma_footer",
    };

    [Test]
    public void 옮긴_화면_문구가_전부_표에_있다()
    {
        var 없는것 = new List<string>();
        foreach (var key in 옮긴_이름표)
            if (!_catalog.Contains(new LocKey("UI", key))) 없는것.Add("UI/" + key);

        Assert.IsEmpty(없는것,
            "표에서 줄이 사라졌다. 그 자리는 화면에 키가 그대로 뜬다:\n  " +
            string.Join("\n  ", 없는것));
    }

    [Test]
    public void 옮긴_문구는_키를_그대로_내지_않는다()
    {
        // 폴백 3단의 마지막은 "키 자체"다. 표에 줄이 있어도 카테고리를 잘못
        // 적으면 화면에 codex_title이 뜬다 — 사람이 보기 전까지 아무도 모른다.
        foreach (var key in 옮긴_이름표)
            Assert.AreNotEqual(key, Loc.T("UI", key), $"UI/{key}가 키를 그대로 냈다");
    }

    // ── ② 소지품 쪽지가 로케일을 따라온다 ────────────────────

    [Test]
    public void 소지품_쪽지의_이름이_로케일을_따라온다()
    {
        // 표에 en 칸이 차 있는 아이템으로 본다. 에셋 원문은 한국어 그대로다 —
        // 화면 값이 바뀌었다면 그것은 표를 거쳤다는 뜻이다.
        var item = 아이템("scrap", "스크랩");

        Assert.AreEqual("스크랩", ItemTooltipContent.Title(item), "ko는 표의 ko 칸이다");

        Loc.SetLocale("en");
        Assert.AreEqual("Scrap", ItemTooltipContent.Title(item),
            "쪽지가 DataText를 거치지 않으면 여기서 한국어가 그대로 나온다");
    }

    [Test]
    public void 이름이_비면_쪽지는_여전히_id를_보여_준다()
    {
        // DataText는 표에도 원문에도 값이 없으면 빈 글자를 낸다. 그때 쪽지가
        // 빈 줄로 뜨면 안 된다는 옛 계약은 그대로다.
        Assert.AreEqual("무명_아이템", ItemTooltipContent.Title(아이템("무명_아이템", "")));
    }

    // ── ③ 제작 목록의 재료 이름이 로케일을 따라온다 ──────────

    [Test]
    public void 제작_쪽지의_재료_이름이_로케일을_따라온다()
    {
        var recipe = 레시피("mushroom_wood", 3);

        StringAssert.Contains("버섯 목재", MenuListing.IngredientLine(recipe));

        Loc.SetLocale("en");
        string en = MenuListing.IngredientLine(recipe);

        StringAssert.Contains("Mushroom Wood", en,
            "제작 목록이 DataText를 거치지 않으면 재료 이름만 한국어로 남는다");
        StringAssert.DoesNotContain("버섯 목재", en);
    }

    [Test]
    public void 제작물_이름도_로케일을_따라온다()
    {
        // 레시피 자신에게 이름이 없으면 결과 아이템의 이름을 쓴다.
        // 그 자리도 DataText를 거쳐야 한다.
        var recipe = 레시피("mushroom_wood", 1);
        recipe.displayName = "";

        Assert.AreEqual("버섯 목재", MenuListing.NameOf(recipe));

        Loc.SetLocale("en");
        Assert.AreEqual("Mushroom Wood", MenuListing.NameOf(recipe));
    }

    // ── 도우미 ───────────────────────────────────────────────

    static ItemDataSO 아이템(string id, string 이름)
    {
        var it = ScriptableObject.CreateInstance<ItemDataSO>();
        it.id = id;
        it.displayName = 이름;
        it.maxStack = 1;
        return it;
    }

    /// <summary>결과와 재료가 같은 아이템인 한 줄짜리 레시피. 이름 경로 둘을 한 번에 본다.</summary>
    static RecipeSO 레시피(string itemId, int count)
    {
        var it = 아이템(itemId, "");
        var r = ScriptableObject.CreateInstance<RecipeSO>();
        r.id = "test_" + itemId;
        r.displayName = "시험용";
        r.ingredients = new[] { new ItemStack(it, count) };
        r.result = new ItemStack(it, 1);
        return r;
    }
}
