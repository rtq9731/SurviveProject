using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Survive.Building;
using Survive.Crafting;
using Survive.Items;
using Survive.Progression;
using Survive.UI;

/// <summary>
/// 모르는 제작법은 목록에서 지운다.
///
/// 잠긴 줄을 회색으로 남겨 두던 시절, 그 줄에는 이렇게 적혔다 —
/// "잠항구 · [잠김] 잠항 설계 청사진이 필요하다: 낫의 핵을 연구대에서 분석하면 알게 된다".
/// 낫을 본 적도 없는 사람에게 아이템 이름과 후반 전개가 통째로 새어 나간 셈이다.
///
/// 여기서 지키는 것은 넷이다.
/// ① 잠긴 항목은 목록에 없다 ② 해금하면 나타난다 ③ 전부 잠겨도 무너지지 않는다
/// ④ <b>화면에 나갈 수 있는 어떤 문자열에도</b> 청사진 이름과 hint가 섞이지 않는다.
///
/// ④는 지어낸 데이터가 아니라 실제 에셋(RecipeBook·BuildCatalog·Blueprints)을 열어서 본다.
/// 코드가 맞아도 새로 만든 청사진 하나가 hint를 흘리면 게임 안에서는 다시 새기 때문이다.
/// </summary>
public class MenuListingTests
{
    const string BookPath = "Assets/08.Data/Recipes/RecipeBook.asset";
    const string CatalogPath = "Assets/08.Data/Buildables/BuildCatalog.asset";
    const string BlueprintDir = "Assets/08.Data/Progression/Blueprints";

    UnlockLedger _ledger;

    [SetUp]
    public void SetUp() => _ledger = new UnlockLedger();

    // ── ① 잠긴 것은 실리지 않는다 ────────────────────────────

    [Test]
    public void 모르는_레시피는_목록에_실리지_않는다()
    {
        var r = 레시피("submersible", 청사진("bp_submersible"));
        Assert.IsFalse(MenuListing.ShouldList(r, StationType.None, _ledger));
    }

    [Test]
    public void 모르는_건축물은_목록에_실리지_않는다()
    {
        var b = 건축물("fence", 청사진("bp_scrapworks"));
        Assert.IsFalse(MenuListing.ShouldList(b, _ledger));
    }

    [Test]
    public void 요구_청사진이_없으면_처음부터_실린다()
    {
        Assert.IsTrue(MenuListing.ShouldList(레시피("axe", null), StationType.None, _ledger),
            "생존의 바닥에 깔린 것까지 감추면 첫 일 분에 할 수 있는 일이 없다");
        Assert.IsTrue(MenuListing.ShouldList(건축물("campfire", null), _ledger));
    }

    [Test]
    public void 원장이_서기_전에는_감추지_않는다()
    {
        var r = 레시피("submersible", 청사진("bp_submersible"));
        Assert.IsTrue(MenuListing.ShouldList(r, StationType.None, null),
            "원장이 없을 때 감추면 판이 통째로 빈 목록이 된다 — 실패는 개방 쪽으로");
    }

    [Test]
    public void 다른_자리에서만_만드는_것은_아는_것이라도_실리지_않는다()
    {
        var bp = 청사진("bp_alloy_shaping");
        _ledger.Unlock("bp_alloy_shaping");

        var r = 레시피("portal_key", bp, StationType.Bench);
        Assert.IsFalse(MenuListing.ShouldList(r, StationType.None, _ledger), "손에서는 안 보인다");
        Assert.IsTrue(MenuListing.ShouldList(r, StationType.Bench, _ledger), "제작대에서는 보인다");
        Assert.IsFalse(MenuListing.ShouldList(r, StationType.Campfire, _ledger),
            "다른 스테이션 전용은 여기서도 감춘다");
    }

    [Test]
    public void 손으로_되는_것은_제작대에서도_실린다()
    {
        var r = 레시피("axe", null);
        Assert.IsTrue(MenuListing.ShouldList(r, StationType.Bench, _ledger),
            "제작대 앞에서 곡괭이를 만들려고 창을 두 번 여는 일은 없어야 한다");
    }

    [Test]
    public void 빈_항목은_실리지_않는다()
    {
        Assert.IsFalse(MenuListing.ShouldList((RecipeSO)null, StationType.None, _ledger));
        Assert.IsFalse(MenuListing.ShouldList((BuildableSO)null, _ledger));
    }

    // ── ② 해금하면 나타난다 ──────────────────────────────────

    [Test]
    public void 해금하면_그_자리에서_목록에_나타난다()
    {
        var r = 레시피("submersible", 청사진("bp_submersible"), StationType.Bench);
        Assert.IsFalse(MenuListing.ShouldList(r, StationType.Bench, _ledger));

        _ledger.Unlock("bp_submersible");

        Assert.IsTrue(MenuListing.ShouldList(r, StationType.Bench, _ledger),
            "원장이 열렸는데 창을 다시 열어야 보이면 실패다");
    }

    [Test]
    public void 해금은_그_청사진을_요구하는_것만_연다()
    {
        var 잠항 = 레시피("submersible", 청사진("bp_submersible"), StationType.Bench);
        var 액면 = 레시피("surface_walker", 청사진("bp_surface_walker"), StationType.Bench);

        _ledger.Unlock("bp_submersible");

        Assert.IsTrue(MenuListing.ShouldList(잠항, StationType.Bench, _ledger));
        Assert.IsFalse(MenuListing.ShouldList(액면, StationType.Bench, _ledger),
            "하나를 열었다고 옆것까지 새면 감춘 보람이 없다");
    }

    [Test]
    public void 목록은_해금을_따라_자란다()
    {
        var 책 = new[]
        {
            레시피("axe", null),
            레시피("pickaxe", 청사진("bp_salvaged_mechanism")),
            레시피("lantern", 청사진("bp_salvaged_mechanism")),
            레시피("oxygen_filter", 청사진("bp_spore_filter")),
        };

        Assert.AreEqual(1, 실린수(책, StationType.None, _ledger), "처음에는 도끼 하나뿐이다");

        _ledger.Unlock("bp_salvaged_mechanism");
        Assert.AreEqual(3, 실린수(책, StationType.None, _ledger), "부품 재조립 하나로 둘이 늘었다");

        _ledger.Unlock("bp_spore_filter");
        Assert.AreEqual(4, 실린수(책, StationType.None, _ledger));
    }

    // ── ③ 전부 잠겨도 무너지지 않는다 ────────────────────────

    [Test]
    public void 전부_잠기면_실리는_줄이_없다()
    {
        var 책 = new[]
        {
            레시피("pickaxe", 청사진("bp_salvaged_mechanism")),
            레시피("oxygen_filter", 청사진("bp_spore_filter")),
        };
        Assert.AreEqual(0, 실린수(책, StationType.None, _ledger));
    }

    [Test]
    public void 실린_줄이_없어도_판때기는_한_줄만큼_선다()
    {
        Assert.AreEqual(1, MenuListing.PanelRows(0), "0으로 재면 판때기가 여백만 남은 띠로 찌그러진다");
        Assert.AreEqual(1, MenuListing.PanelRows(-3), "음수가 흘러들어와도 마찬가지다");
        Assert.AreEqual(1, MenuListing.PanelRows(1));
        Assert.AreEqual(7, MenuListing.PanelRows(7), "실린 줄이 있으면 그대로 센다");
    }

    [Test]
    public void 빈_목록_안내문은_몇_개가_잠겼는지_세어_주지_않는다()
    {
        foreach (var 문구 in new[] { MenuListing.NothingKnownToCraft, MenuListing.NothingKnownToBuild })
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(문구), "빈 목록에도 할 말은 있어야 한다");
            Assert.IsFalse(문구.Any(char.IsDigit),
                $"수를 세어 주면 앞으로 무엇이 얼마나 남았는지가 새어 나간다 — \"{문구}\"");
            StringAssert.DoesNotContain("잠", 문구, $"잠긴 것이 있다는 말조차 하지 않는다 — \"{문구}\"");
            StringAssert.DoesNotContain("청사진", 문구);
        }
    }

    // ── ④ 화면에 나갈 수 있는 모든 문자열 ────────────────────

    [Test]
    public void 아무것도_모르는_제작_목록에_청사진_힌트가_한_조각도_없다()
    {
        새어나가지_않는다(제작_화면_문자열(_ledger, StationType.None));
        새어나가지_않는다(제작_화면_문자열(_ledger, StationType.Bench));
        새어나가지_않는다(제작_화면_문자열(_ledger, StationType.Campfire));
    }

    [Test]
    public void 아무것도_모르는_건축_목록에_청사진_힌트가_한_조각도_없다()
    {
        새어나가지_않는다(건축_화면_문자열(_ledger));
    }

    [Test]
    public void 전부_해금해도_청사진_이름과_힌트는_줄에_적히지_않는다()
    {
        // 잠금이 풀린 뒤에도 마찬가지다. 줄에 적히는 것은 만드는 데 드는 것뿐이고,
        // "어떻게 알아냈는가"는 도감이 맡는다.
        foreach (var bp in 청사진_에셋들()) _ledger.Unlock(bp.id);

        새어나가지_않는다(제작_화면_문자열(_ledger, StationType.Bench));
        새어나가지_않는다(건축_화면_문자열(_ledger));
    }

    [Test]
    public void 초반_목록에는_잠김이라는_말이_없다()
    {
        foreach (var s in 제작_화면_문자열(_ledger, StationType.None).Concat(건축_화면_문자열(_ledger)))
        {
            StringAssert.DoesNotContain("잠김", s, $"자물쇠 표시가 남아 있다 — \"{s}\"");
            StringAssert.DoesNotContain("청사진", s, $"청사진을 입에 올린다 — \"{s}\"");
        }
    }

    [Test]
    public void 화면에_나가는_문자열에_줄표가_없다()
    {
        // 본문 글꼴(ChosunGu)에 U+2014가 없어 화면에는 두부(□)로 찍힌다.
        var 전부 = 제작_화면_문자열(_ledger, StationType.Bench)
                   .Concat(건축_화면_문자열(_ledger))
                   .Concat(new[] { MenuListing.NothingKnownToCraft, MenuListing.NothingKnownToBuild });

        foreach (var s in 전부)
            Assert.IsFalse(s.Contains('—'), $"줄표가 섞였다 — \"{s}\"");
    }

    [Test]
    public void 실제_에셋에서_초반에_보이는_것은_요구가_없는_것뿐이다()
    {
        var book = 레시피북();
        foreach (var r in book.recipes)
        {
            if (r == null) continue;
            if (!MenuListing.ShouldList(r, StationType.Bench, _ledger)) continue;

            Assert.IsTrue(r.requiredBlueprint == null ||
                          string.IsNullOrWhiteSpace(r.requiredBlueprint.id),
                $"{r.id}가 원장이 빈 채로 목록에 실렸다");
        }

        foreach (var b in 건축목록().entries)
        {
            if (b == null) continue;
            if (!MenuListing.ShouldList(b, _ledger)) continue;

            Assert.IsTrue(b.requiredBlueprint == null ||
                          string.IsNullOrWhiteSpace(b.requiredBlueprint.id),
                $"{b.id}가 원장이 빈 채로 목록에 실렸다");
        }
    }

    [Test]
    public void 청사진_에셋에는_감출_hint가_실제로_들어_있다()
    {
        // 이 테스트가 지키는 것은 위 검사들의 <b>의미</b>다. hint가 전부 비어 있으면
        // "새지 않는다"는 단언이 아무것도 증명하지 않는 채로 초록불이 된다.
        var 힌트있는것 = 청사진_에셋들().Count(bp => !string.IsNullOrWhiteSpace(bp.hint));
        Assert.Greater(힌트있는것, 0, "감출 힌트가 하나도 없으면 위 검사들이 공회전한다");
    }

    // ── 도우미 ───────────────────────────────────────────────

    /// <summary>지금 원장으로 제작 목록이 화면에 낼 수 있는 문자열 전부.</summary>
    static List<string> 제작_화면_문자열(UnlockLedger ledger, StationType station)
    {
        var inv = new Inventory(30);
        var text = new List<string>();
        int listed = 0;

        foreach (var r in 레시피북().recipes)
        {
            if (!MenuListing.ShouldList(r, station, ledger)) continue;
            listed++;
            text.Add(MenuListing.NameOf(r));
            text.Add(MenuListing.RecipeLine(r, inv, 1, true));
            text.Add(MenuListing.RecipeLine(r, inv, 5, false));   // 대기열이 찬 경우
            text.Add(MenuListing.IngredientLine(r));              // 커서를 올렸을 때의 쪽지
        }

        if (listed == 0) text.Add(MenuListing.NothingKnownToCraft);
        return text;
    }

    static List<string> 건축_화면_문자열(UnlockLedger ledger)
    {
        var inv = new Inventory(30);
        var text = new List<string>();
        int listed = 0;

        foreach (var b in 건축목록().entries)
        {
            if (!MenuListing.ShouldList(b, ledger)) continue;
            listed++;
            text.Add(MenuListing.NameOf(b));
            text.Add(MenuListing.BuildableLine(b, inv));
        }

        if (listed == 0) text.Add(MenuListing.NothingKnownToBuild);
        return text;
    }

    /// <summary>어떤 문자열에도 청사진의 이름이나 hint 통짜가 섞이지 않았다.</summary>
    static void 새어나가지_않는다(List<string> 화면)
    {
        Assert.IsNotEmpty(화면, "화면에 아무것도 없으면 검사가 공회전한다");

        foreach (var bp in 청사진_에셋들())
        foreach (var s in 화면)
        {
            if (!string.IsNullOrWhiteSpace(bp.hint))
                StringAssert.DoesNotContain(bp.hint.Trim(), s,
                    $"{bp.id}의 힌트가 화면 문자열에 섞였다 — \"{s}\"");

            if (!string.IsNullOrWhiteSpace(bp.displayName))
                StringAssert.DoesNotContain(bp.displayName.Trim(), s,
                    $"{bp.id}의 청사진 이름이 화면 문자열에 섞였다 — \"{s}\"");
        }
    }

    static RecipeBookSO 레시피북()
    {
        var book = AssetDatabase.LoadAssetAtPath<RecipeBookSO>(BookPath);
        Assert.IsNotNull(book, $"{BookPath}를 못 읽었다");
        return book;
    }

    static BuildCatalogSO 건축목록()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<BuildCatalogSO>(CatalogPath);
        Assert.IsNotNull(catalog, $"{CatalogPath}를 못 읽었다");
        return catalog;
    }

    static List<BlueprintSO> 청사진_에셋들()
    {
        var found = AssetDatabase.FindAssets("t:" + nameof(BlueprintSO), new[] { BlueprintDir })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BlueprintSO>)
            .Where(bp => bp != null && !string.IsNullOrWhiteSpace(bp.id))
            .ToList();

        Assert.IsNotEmpty(found, $"{BlueprintDir}에서 청사진을 하나도 못 읽었다");
        return found;
    }

    static int 실린수(IEnumerable<RecipeSO> 책, StationType station, UnlockLedger ledger) =>
        책.Count(r => MenuListing.ShouldList(r, station, ledger));

    static BlueprintSO 청사진(string id)
    {
        var bp = ScriptableObject.CreateInstance<BlueprintSO>();
        bp.id = id;
        bp.displayName = id + " 이름";
        bp.hint = id + " 힌트";
        return bp;
    }

    static RecipeSO 레시피(string id, BlueprintSO bp, StationType station = StationType.None)
    {
        var r = ScriptableObject.CreateInstance<RecipeSO>();
        r.id = id;
        r.displayName = id;
        r.requiredBlueprint = bp;
        r.requiredStation = station;
        return r;
    }

    static BuildableSO 건축물(string id, BlueprintSO bp)
    {
        var b = ScriptableObject.CreateInstance<BuildableSO>();
        b.id = id;
        b.displayName = id;
        b.requiredBlueprint = bp;
        return b;
    }
}
