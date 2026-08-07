using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Survive.Building;
using Survive.Crafting;
using Survive.Items;
using Survive.Progression;
using Survive.World;

/// <summary>
/// 다리가 관문에서 빠졌다는 것을 <b>에셋을 열어</b> 확인한다 (기획서 §6.4).
///
/// <b>왜 에셋인가.</b> 열거값을 지우는 것만으로는 절반이다. 진행을 실제로 정하는 것은
/// 08.Data의 청사진·연구·목표이고, 거기에 다리가 물려 있으면 코드가 아무리 깨끗해도
/// 플레이어는 여전히 다리를 세워야 한다. 그래서 여기서는 코드를 보지 않고 에셋을 연다.
///
/// <b>지우는 것이 아니라 떼어내는 것이다.</b> 다리 노릇을 하던 조립 조각
/// (<c>piece_foundation</c>)은 자유 건축 목록에 그대로 남는다 — 수로 위에 놓으면
/// 왕복이 공짜가 되는 편의시설이다. 강제가 아니라 선택이 된 것뿐이므로,
/// "목록에서 사라지지 않았다"까지 함께 못 박는다.
/// </summary>
public class BridgeRetirementTests
{
    const string CatalogPath = "Assets/08.Data/Buildables/BuildCatalog.asset";
    const string BookPath = "Assets/08.Data/Recipes/RecipeBook.asset";
    const string ResearchPath = "Assets/08.Data/Progression/Resources/ResearchBook.asset";
    const string ChapterPath = "Assets/08.Data/Chapters/Chapter1_FloatingIsland.asset";

    /// <summary>
    /// 다리를 가리키는 말. 한쪽만 보면 새는 곳이 생긴다 —
    /// id는 영어로 적히고 화면에 뜨는 이름은 한국어로 적히기 때문이다.
    /// </summary>
    static readonly string[] 다리라는말 = { "bridge", "다리" };

    static T Load<T>(string path) where T : UnityEngine.Object =>
        AssetDatabase.LoadAssetAtPath<T>(path);

    static IEnumerable<T> LoadAll<T>(string folder) where T : UnityEngine.Object =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder })
                     .Select(AssetDatabase.GUIDToAssetPath)
                     .Select(AssetDatabase.LoadAssetAtPath<T>)
                     .Where(a => a != null);

    static bool 다리를_가리킨다(params string[] 글자들) =>
        글자들.Where(s => !string.IsNullOrEmpty(s))
              .Any(s => 다리라는말.Any(w => s.ToLowerInvariant().Contains(w)));

    // ── 진행 배선 (청사진 · 연구 · 목표) ────────────────────────────────────

    [Test]
    public void 어떤_건축물도_다리가_아니다()
    {
        var catalog = Load<BuildCatalogSO>(CatalogPath);
        Assert.IsNotNull(catalog, CatalogPath + "를 찾지 못했다");

        // 검사가 헛돌지 않는다는 것부터 본다 — 목록이 비어 있으면 무엇을 넣어도 통과한다.
        Assert.IsNotEmpty(catalog.entries, "건축 목록이 비어 있다 — 검사가 헛돈다");

        foreach (var b in catalog.entries.Where(x => x != null))
            Assert.IsFalse(다리를_가리킨다(b.id, b.displayName),
                           $"{b.id}가 다리 건축물이다 — 관문 원칙(§6.4)에 걸린다");
    }

    [Test]
    public void 어떤_청사진도_다리를_열지_않는다()
    {
        var 청사진들 = LoadAll<BlueprintSO>("Assets/08.Data").ToList();
        Assert.IsNotEmpty(청사진들, "청사진이 하나도 없다 — 검사가 헛돈다");

        foreach (var bp in 청사진들)
            Assert.IsFalse(다리를_가리킨다(bp.id, bp.displayName, bp.hint),
                           $"청사진 {bp.id}가 다리를 가리킨다");
    }

    [Test]
    public void 어떤_연구도_다리를_요구하지_않는다()
    {
        var book = Load<ResearchBookSO>(ResearchPath);
        Assert.IsNotNull(book, ResearchPath + "를 찾지 못했다");
        Assert.IsNotEmpty(book.entries, "연구 목록이 비어 있다 — 검사가 헛돈다");

        foreach (var e in book.entries.Where(x => x != null))
        {
            Assert.IsFalse(다리를_가리킨다(e.id, e.displayName),
                           $"연구 {e.id}가 다리를 가리킨다");

            foreach (var u in (e.unlocks ?? new BlueprintSO[0]).Where(u => u != null))
                Assert.IsFalse(다리를_가리킨다(u.id, u.displayName),
                               $"연구 {e.id}가 다리 청사진 {u.id}를 연다");
        }
    }

    [Test]
    public void 어떤_레시피도_다리를_만들지_않는다()
    {
        var book = Load<RecipeBookSO>(BookPath);
        Assert.IsNotNull(book, BookPath + "를 찾지 못했다");
        Assert.IsNotEmpty(book.recipes, "레시피가 하나도 없다 — 검사가 헛돈다");

        foreach (var r in book.recipes.Where(x => x != null))
            Assert.IsFalse(다리를_가리킨다(r.id, r.displayName),
                           $"레시피 {r.id}가 다리를 만든다");
    }

    [Test]
    public void 챕터_1의_어떤_목표도_다리를_요구하지_않는다()
    {
        var chapter = Load<ChapterSO>(ChapterPath);
        Assert.IsNotNull(chapter, ChapterPath + "를 찾지 못했다");
        Assert.IsNotEmpty(chapter.objectives, "챕터 1에 목표가 하나도 없다 — 검사가 헛돈다");

        foreach (var o in chapter.objectives.Where(x => x != null))
        {
            Assert.IsFalse(다리를_가리킨다(o.id, o.displayText),
                           $"목표 {o.id}가 다리를 요구한다");

            // 목표가 무엇을 세우라고 지목할 길 자체가 없어야 한다.
            // 열쇠·아이템 칸에 건축물 id가 들어가면 그것이 곧 건축 관문이다.
            if (o is FlagObjective f)
                Assert.IsFalse(다리를_가리킨다(f.flagKey), $"목표 {o.id}의 열쇠가 다리다");
            if (o is CollectItemObjective c)
                Assert.IsFalse(다리를_가리킨다(c.itemId), $"목표 {o.id}가 다리를 모으라고 한다");
        }
    }

    [Test]
    public void 챕터_1의_목표는_어떤_건축물도_지목하지_않는다()
    {
        // 다리뿐 아니라 <b>세우는 일 자체</b>가 진행 조건이 아니다.
        // 이름 검사만 두면 "bridge"가 아닌 이름으로 같은 관문이 되살아난다.
        var chapter = Load<ChapterSO>(ChapterPath);
        var 건축물id = Load<BuildCatalogSO>(CatalogPath).entries
                        .Where(b => b != null && !string.IsNullOrEmpty(b.id))
                        .Select(b => b.id).ToList();
        Assert.IsNotEmpty(건축물id);

        foreach (var o in chapter.objectives.Where(x => x != null))
        {
            if (o is CollectItemObjective c)
                CollectionAssert.DoesNotContain(건축물id, c.itemId,
                    $"목표 {o.id}가 건축물 {c.itemId}를 모으라고 한다");
            if (o is FlagObjective f)
                CollectionAssert.DoesNotContain(건축물id, f.flagKey,
                    $"목표 {o.id}가 건축물 {f.flagKey}를 세우라고 한다");
        }
    }

    // ── 지운 것이 아니다 — 자유 건축으로 남는다 ─────────────────────────────

    [Test]
    public void 토대는_자유_건축_목록에_그대로_있다()
    {
        // 다리 노릇을 하던 물건이다. 관문에서 뺐다고 목록에서까지 빠지면
        // 강 위에 놓아 왕복을 아끼는 선택 자체가 사라진다.
        var 토대 = Load<BuildCatalogSO>(CatalogPath).GetById("piece_foundation");
        Assert.IsNotNull(토대, "토대가 건축 목록에서 사라졌다 — 편의시설까지 지운 것이다");
        Assert.IsTrue(토대.IsModular, "토대가 조립 조각이 아니게 됐다");
        Assert.IsNotEmpty(토대.cost, "토대에 비용이 없다");
        Assert.IsNotNull(토대.prefab, "토대에 세울 것이 없다");
    }

    [Test]
    public void 조립_조각이_전부_남아_있다()
    {
        var catalog = Load<BuildCatalogSO>(CatalogPath);
        foreach (var id in new[] { "piece_foundation", "piece_floor", "piece_wall",
                                   "piece_ramp", "piece_doorway" })
            Assert.IsNotNull(catalog.GetById(id), $"{id}이(가) 건축 목록에서 사라졌다");
    }

    // ── 장비 에셋 정합 — 열거값을 지운 뒤에도 제 것을 가리키는가 ────────────

    /// <summary>
    /// id → 그 에셋이 가리켜야 할 장비. <b>이 표가 진실이다.</b>
    ///
    /// <see cref="TraversalGearItemSO.gear"/>는 정수로 직렬화되므로,
    /// 열거형 중간 값을 지우면 에셋이 조용히 옆 장비를 가리킨다
    /// (다리를 뺄 때 실제로 액면 보행 장비 4→3, 돌파정 5→4로 밀렸다.
    /// 매크로늄 방호복을 사이에 끼울 때는 반대로 돌파정이 4→5로 되밀렸다).
    /// 컴파일도 다른 테스트도 그것을 잡아 주지 못해서 여기 표로 못 박는다.
    /// </summary>
    static readonly Dictionary<string, TraversalGear> 의도한장비 =
        new Dictionary<string, TraversalGear>
        {
            { "surface_walker", TraversalGear.SurfaceWalker },
            { "macronium_suit", TraversalGear.MacroniumSuit },
            { "breach_pod", TraversalGear.BreachPod },
        };

    static List<TraversalGearItemSO> 장비에셋들 =>
        LoadAll<TraversalGearItemSO>("Assets/08.Data").ToList();

    [Test]
    public void 장비_에셋이_전부_표에_있다()
    {
        var 실제 = 장비에셋들;
        Assert.IsNotEmpty(실제, "통과 장비 에셋이 하나도 없다 — 검사가 헛돈다");

        CollectionAssert.AreEquivalent(
            의도한장비.Keys.OrderBy(x => x).ToList(),
            실제.Select(a => a.id).OrderBy(x => x).ToList(),
            "장비 에셋이 늘거나 줄었다 — 표를 함께 고쳐야 정합을 지킬 수 있다");
    }

    [Test]
    public void 장비_에셋이_의도한_장비를_가리킨다()
    {
        foreach (var a in 장비에셋들)
        {
            Assert.IsTrue(의도한장비.ContainsKey(a.id), $"표에 없는 장비 에셋 {a.id}");
            Assert.AreEqual(의도한장비[a.id], a.gear,
                $"{a.id}의 gear가 {a.gear}를 가리킨다 — 열거값이 밀린 것이다");
            Assert.AreNotEqual(TraversalGear.None, a.gear,
                $"{a.id}가 아무것도 뚫지 못한다");
            Assert.Greater(a.capacity, 0f, $"{a.id}의 용량이 0이다");
        }
    }

    [Test]
    public void 장비_에셋_둘이_같은_장비를_가리키지_않는다()
    {
        // 열거값이 밀리면 서로 다른 두 에셋이 같은 값으로 겹치는 모양으로도 나타난다.
        var 겹침 = 장비에셋들.GroupBy(a => a.gear).Where(g => g.Count() > 1).ToList();
        Assert.IsEmpty(겹침,
            "장비 둘이 같은 것을 뚫는다: " +
            string.Join(", ", 겹침.Select(g => g.Key + "=" + string.Join("/", g.Select(a => a.id)))));
    }

    [Test]
    public void 직렬화되는_정수가_지금_배치_그대로다()
    {
        // 에셋의 gear:는 이 정수로 적혀 있다. 여기가 바뀌면 08.Data의 값도
        // 함께 밀어야 하고, 그 사실을 이 검사가 실패로 알린다.
        Assert.AreEqual(0, (int)TraversalGear.None);
        Assert.AreEqual(1, (int)TraversalGear.Lantern);
        Assert.AreEqual(2, (int)TraversalGear.Swimming);
        Assert.AreEqual(3, (int)TraversalGear.SurfaceWalker);
        Assert.AreEqual(4, (int)TraversalGear.MacroniumSuit);
        Assert.AreEqual(5, (int)TraversalGear.BreachPod);
        Assert.AreEqual(6, System.Enum.GetValues(typeof(TraversalGear)).Length,
                        "장비가 늘거나 줄었다 — 08.Data의 gear: 값을 대조해라");
    }
}
