using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Survive.Crafting;
using Survive.Items;

/// <summary>
/// RecipeBookSO와, 실제로 제작 UI가 읽는 RecipeBook 에셋의 무결성.
///
/// RecipeBookSO 자체에는 조회 메서드가 없다(ItemDatabaseSO·BuildCatalogSO와
/// 다른 점). CraftingUI가 recipes 배열을 그대로 훑기 때문에, 배열의 형태
/// 자체가 계약이다 — null이 아닐 것, 빈 칸이 없을 것, id가 겹치지 않을 것.
/// 겹친 id는 저장·목표 판정이 레시피를 id로 가리키기 시작하는 순간 조용히
/// 엉뚱한 것을 가리킨다.
/// </summary>
public class RecipeBookTests
{
    const string BookPath = "Assets/08.Data/Recipes/RecipeBook.asset";

    static ItemDataSO 아이템(string id, int maxStack = 99)
    {
        var it = ScriptableObject.CreateInstance<ItemDataSO>();
        it.id = id;
        it.displayName = id;
        it.maxStack = maxStack;
        return it;
    }

    static RecipeSO 레시피(string id)
    {
        var r = ScriptableObject.CreateInstance<RecipeSO>();
        r.id = id;
        r.displayName = id;
        r.result = new ItemStack(아이템(id), 1);
        return r;
    }

    static RecipeBookSO 레시피북(params RecipeSO[] recipes)
    {
        var b = ScriptableObject.CreateInstance<RecipeBookSO>();
        b.recipes = recipes;
        return b;
    }

    // ── 배열 계약 ────────────────────────────────────────────

    [Test]
    public void 새_레시피북은_빈_목록으로_시작한다()
    {
        var b = ScriptableObject.CreateInstance<RecipeBookSO>();
        Assert.IsNotNull(b.recipes, "null이면 제작 UI가 열리자마자 터진다");
        Assert.AreEqual(0, b.recipes.Length);
    }

    [Test]
    public void 담은_레시피를_순서대로_돌려준다()
    {
        var 곡괭이 = 레시피("pickaxe");
        var 랜턴 = 레시피("lantern");
        var b = 레시피북(곡괭이, 랜턴);

        Assert.AreEqual(2, b.recipes.Length);
        Assert.AreSame(곡괭이, b.recipes[0], "표시 순서는 배열 순서가 정한다");
        Assert.AreSame(랜턴, b.recipes[1]);
    }

    // ── 실제 에셋 무결성 ─────────────────────────────────────

    static RecipeBookSO 실제_레시피북()
    {
        var b = AssetDatabase.LoadAssetAtPath<RecipeBookSO>(BookPath);
        Assert.IsNotNull(b, $"{BookPath}를 못 읽었다 — 경로가 바뀌었는가");
        return b;
    }

    [Test]
    public void 실제_레시피북에_빈_항목이_없다()
    {
        var b = 실제_레시피북();
        Assert.Greater(b.recipes.Length, 0, "레시피북이 비어 있으면 아무것도 못 만든다");

        for (int i = 0; i < b.recipes.Length; i++)
            Assert.IsNotNull(b.recipes[i], $"recipes[{i}]가 비어 있다");
    }

    [Test]
    public void 실제_레시피북의_id가_전부_채워져_있다()
    {
        foreach (var r in 실제_레시피북().recipes)
            Assert.IsFalse(string.IsNullOrWhiteSpace(r.id), $"{r.name}의 id가 비어 있다");
    }

    [Test]
    public void 실제_레시피북에_중복_id가_없다()
    {
        var 본것 = new HashSet<string>();
        var 중복 = new List<string>();

        foreach (var r in 실제_레시피북().recipes)
            if (!본것.Add(r.id)) 중복.Add(r.id);

        Assert.IsEmpty(중복, "중복 id: " + string.Join(", ", 중복));
    }

    [Test]
    public void 실제_레시피북에_같은_레시피가_두_번_담기지_않았다()
    {
        var 본것 = new HashSet<RecipeSO>();

        foreach (var r in 실제_레시피북().recipes)
            Assert.IsTrue(본것.Add(r), $"{r.id}가 목록에 두 번 들어 있다 — UI에도 두 줄 뜬다");
    }

    [Test]
    public void 실제_레시피북의_모든_레시피에_결과물이_있다()
    {
        foreach (var r in 실제_레시피북().recipes)
        {
            Assert.IsNotNull(r.result, $"{r.id}에 result가 없다");
            Assert.IsNotNull(r.result.item, $"{r.id}의 result에 아이템이 없다");
            Assert.Greater(r.result.count, 0, $"{r.id}의 result 수량이 0 이하다 — 만들어도 안 생긴다");
        }
    }

    [Test]
    public void 실제_레시피북의_재료가_유효하다()
    {
        foreach (var r in 실제_레시피북().recipes)
        {
            Assert.IsNotNull(r.ingredients, $"{r.id}의 ingredients가 null이다");

            for (int i = 0; i < r.ingredients.Length; i++)
            {
                ItemStack 재료 = r.ingredients[i];
                Assert.IsNotNull(재료, $"{r.id}의 ingredients[{i}]가 비어 있다");
                Assert.IsNotNull(재료.item, $"{r.id}의 ingredients[{i}]에 아이템이 없다");
                Assert.Greater(재료.count, 0, $"{r.id}의 ingredients[{i}] 수량이 0 이하다");
            }
        }
    }

    [Test]
    public void 실제_레시피북의_한_레시피_안에서_같은_재료가_두_번_나오지_않는다()
    {
        // 같은 아이템이 두 줄이면 CraftingService가 각각 차감하므로 총량은
        // 맞지만, UI에는 같은 재료가 두 번 뜬다. 데이터 실수의 신호다.
        foreach (var r in 실제_레시피북().recipes)
        {
            var 본것 = new HashSet<string>();
            foreach (var 재료 in r.ingredients)
                Assert.IsTrue(본것.Add(재료.item.id),
                    $"{r.id}의 재료에 {재료.item.id}가 두 번 있다");
        }
    }
}
