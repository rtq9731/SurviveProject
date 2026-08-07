using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Survive.Crafting;
using Survive.Items;
using Survive.World;

/// <summary>
/// P2 스펙 §8-2의 실제 데이터. 코드가 맞아도 에셋이 틀리면 게임 안에서는 아무 일도 일어나지 않으므로,
/// 제작 UI가 실제로 읽는 <c>RecipeBook</c>과 <c>ItemDatabase</c>를 그대로 열어 본다.
///
/// 여기서 지키는 것은 스펙 §5의 티어 규칙이다 — <b>섬4를 여는 장비를 섬4의 자원으로 만들면 순환</b>이라
/// 영원히 못 만든다. 그 실수는 눈으로 데이터를 봐서는 잘 안 보인다.
/// </summary>
public class SurfaceWalkerRecipeTests
{
    const string BookPath = "Assets/08.Data/Recipes/RecipeBook.asset";
    const string DbPath = "Assets/08.Data/Items/ItemDatabase.asset";
    const string GearId = "surface_walker";

    /// <summary>액면섬 관문의 폭(m). HANDOFF §2.2 "3 → 4 구간 30m".</summary>
    const float 관문폭 = 30f;

    static RecipeSO 레시피()
    {
        var book = AssetDatabase.LoadAssetAtPath<RecipeBookSO>(BookPath);
        Assert.IsNotNull(book, $"{BookPath}를 못 읽었다");

        foreach (var r in book.recipes)
            if (r != null && r.id == GearId) return r;

        Assert.Fail($"레시피북에 {GearId}가 없다 — 제작 UI에 안 뜬다");
        return null;
    }

    static ItemDatabaseSO DB()
    {
        var db = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(DbPath);
        Assert.IsNotNull(db, $"{DbPath}를 못 읽었다");
        return db;
    }

    // ── 레시피 조회 ──────────────────────────────────────────

    [Test]
    public void 레시피북에서_액면_보행_장비를_찾을_수_있다()
    {
        var r = 레시피();
        Assert.AreEqual(GearId, r.id);
        Assert.IsFalse(string.IsNullOrWhiteSpace(r.displayName), "이름이 없으면 제작 목록에 빈 줄이 뜬다");
    }

    [Test]
    public void 레시피의_결과물이_액면_보행_장비다()
    {
        var r = 레시피();
        Assert.IsNotNull(r.result, "결과물이 없다");
        Assert.IsNotNull(r.result.item, "결과 아이템이 비었다");
        Assert.AreEqual(GearId, r.result.item.id);
        Assert.AreEqual(1, r.result.count);
    }

    [Test]
    public void 결과물이_액면을_뚫는_장비로_선언돼_있다()
    {
        var gear = 레시피().result.item as TraversalGearItemSO;
        Assert.IsNotNull(gear, "TraversalGearItemSO가 아니면 인벤토리에 넣어도 판정에 잡히지 않는다");
        Assert.AreEqual(TraversalGear.SurfaceWalker, gear.gear);
    }

    [Test]
    public void 용량이_실제_관문을_건널_만큼_된다()
    {
        var gear = (TraversalGearItemSO)레시피().result.item;
        Assert.GreaterOrEqual(gear.capacity, 관문폭,
            $"섬3→섬4는 {관문폭}m다. 이보다 작으면 만들어도 못 건넌다");
    }

    [Test]
    public void 아이템_DB에_등록돼_있다()
    {
        // 등록이 빠지면 세이브를 다시 불러왔을 때 조용히 사라진다(PlayerInventory.RestoreState).
        Assert.IsTrue(DB().TryGetById(GearId, out var item), $"ItemDatabase에 {GearId}가 없다");
        Assert.AreSame(레시피().result.item, item, "레시피 결과와 DB의 것이 다른 에셋이다");
    }

    [Test]
    public void 한_개만_들고_다닌다()
    {
        Assert.AreEqual(1, 레시피().result.item.maxStack, "몸에 걸치는 장비를 쌓을 이유가 없다");
    }

    // ── 티어 규칙 (스펙 §5) ─────────────────────────────────

    [Test]
    public void 재료에_매크로늄이_들어가지_않는다()
    {
        // 매크로늄은 섬4에서만 나온다. 섬4를 여는 장비의 재료로 쓰면 순환이라 영원히 못 만든다.
        foreach (var 재료 in 레시피().ingredients)
            Assert.AreNotEqual("macronium", 재료.item.id,
                "액면 보행 장비를 매크로늄으로 만들면 섬4에 갈 방법이 없다");
    }

    [Test]
    public void 주재료가_섬3의_자원인_외계_합금이다()
    {
        // 스펙 §5: 스크랩 → 기계 부품 → 이종 합금 → 매크로늄.
        // 섬4를 여는 장비는 바로 앞 티어인 이종 합금을 요구해야 진행이 한 방향이 된다.
        var 재료 = new Dictionary<string, int>();
        foreach (var i in 레시피().ingredients) 재료[i.item.id] = i.count;

        Assert.IsTrue(재료.ContainsKey("alien_alloy"), "이종 합금이 없으면 섬3을 건너뛰고 만들 수 있다");
        Assert.Greater(재료["alien_alloy"], 0);
    }

    [Test]
    public void 제작대에서만_만든다()
    {
        Assert.AreEqual(StationType.Bench, 레시피().requiredStation,
            "최상위 이동 장비를 맨손으로 만들면 거점을 세울 이유가 준다");
    }

    // ── 제작에서 판정까지 한 흐름 ────────────────────────────

    static Inventory 재료를_채운_인벤토리(RecipeSO r)
    {
        var inv = new Inventory(15);
        foreach (var 재료 in r.ingredients) inv.TryAdd(재료.item, 재료.count);
        return inv;
    }

    static readonly HazardZone 액면 = new HazardZone(EnvironmentHazard.MacroniumSurface, 관문폭);

    [Test]
    public void 재료가_모자라면_못_만든다()
    {
        var r = 레시피();
        var inv = new Inventory(15);
        Assert.IsFalse(CraftingService.CanCraft(r, inv, StationType.Bench));
    }

    [Test]
    public void 맨손으로는_못_만든다()
    {
        var r = 레시피();
        Assert.IsFalse(CraftingService.CanCraft(r, 재료를_채운_인벤토리(r), StationType.None));
    }

    [Test]
    public void 제작하면_액면을_지날_수_있게_된다()
    {
        // 레시피 → 제작 → 인벤토리 → 장비 목록 → 판정. 이 다섯이 한 줄로 이어지는지 본다.
        var r = 레시피();
        var inv = 재료를_채운_인벤토리(r);

        Assert.IsFalse(EnvironmentThreat.CanPass(액면, TraversalLoadout.From(inv)),
            "만들기 전에는 막혀 있어야 한다");

        Assert.IsTrue(CraftingService.Craft(r, inv, StationType.Bench), "재료를 다 갖췄는데 제작이 실패했다");
        Assert.AreEqual(1, inv.CountOf(GearId), "만들었는데 인벤토리에 없다");

        Assert.IsTrue(EnvironmentThreat.CanPass(액면, TraversalLoadout.From(inv)),
            "만들었는데도 막힌다면 제작과 판정이 이어져 있지 않은 것이다");
    }

    [Test]
    public void 제작하면_재료가_사라진다()
    {
        var r = 레시피();
        var inv = 재료를_채운_인벤토리(r);
        CraftingService.Craft(r, inv, StationType.Bench);

        foreach (var 재료 in r.ingredients)
            Assert.AreEqual(0, inv.CountOf(재료.item.id), $"{재료.item.id}가 남아 있다");
    }
}
