using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Survive.Building;
using Survive.Crafting;
using Survive.Creatures;
using Survive.Items;
using Survive.Progression;

/// <summary>
/// 백로그 38의 <b>실제 데이터</b>. 코드가 맞아도 에셋이 틀리면 게임 안에서는 아무 일도
/// 일어나지 않는다 — 청사진 id 하나가 어긋나면 연구가 끝나도 잠긴 채로 남고,
/// 그 실수는 눈으로 데이터를 봐서는 잘 안 보인다.
///
/// 여기서 지키는 것은 <b>사슬</b>이다: 유물 → 연구 항목 → 청사진 → 레시피.
/// 한 마디라도 끊기면 진행 장비를 영영 만들 수 없다.
/// </summary>
public class ResearchWiringTests
{
    const string BookPath = "Assets/08.Data/Progression/Resources/ResearchBook.asset";
    const string RecipeBookPath = "Assets/08.Data/Recipes/RecipeBook.asset";
    const string CatalogPath = "Assets/08.Data/Buildables/BuildCatalog.asset";
    const string DbPath = "Assets/08.Data/Items/ItemDatabase.asset";

    /// <summary>백로그 38이 정한 id 계약. 작업 39(유물 드롭)가 이 이름으로 떨군다.</summary>
    const string Membrane = "relic_membrane";
    const string Core = "relic_core";

    static ResearchBookSO 연구목록()
    {
        var b = AssetDatabase.LoadAssetAtPath<ResearchBookSO>(BookPath);
        Assert.IsNotNull(b, $"{BookPath}를 못 읽었다 — 연구대가 목록을 못 찾으면 빈 화면이 뜬다");
        return b;
    }

    static ItemDatabaseSO 아이템DB()
    {
        var db = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(DbPath);
        Assert.IsNotNull(db, $"{DbPath}를 못 읽었다");
        return db;
    }

    static RecipeSO 레시피(string id)
    {
        var book = AssetDatabase.LoadAssetAtPath<RecipeBookSO>(RecipeBookPath);
        Assert.IsNotNull(book, $"{RecipeBookPath}를 못 읽었다");

        var r = book.recipes.FirstOrDefault(x => x != null && x.id == id);
        Assert.IsNotNull(r, $"레시피북에 {id}가 없다 — 제작 UI에 안 뜬다");
        return r;
    }

    static ResearchEntrySO 항목(string id)
    {
        var e = 연구목록().Find(id);
        Assert.IsNotNull(e, $"연구 목록에 {id}가 없다");
        return e;
    }

    // ── 연구 목록 ────────────────────────────────────────────

    /// <summary>
    /// <c>ResearchStation.BookResourceName</c>의 값. 그쪽은 Assembly-CSharp에 있어
    /// 이 테스트 어셈블리에서 참조할 수 없으므로 문자열을 여기 한 번 더 적는다 —
    /// 어긋나면 아래 테스트가 바로 잡는다.
    /// </summary>
    const string BookResourceName = "ResearchBook";

    [Test]
    public void 연구_목록이_Resources에서_읽힌다()
    {
        // ResearchStation.Awake가 이 이름으로 찾는다. 경로가 어긋나면 연구대를
        // 세워도 목록이 비어 조용히 아무 일도 일어나지 않는다.
        var loaded = Resources.Load<ResearchBookSO>(BookResourceName);
        Assert.IsNotNull(loaded, $"Resources/{BookResourceName}이 없다");
        Assert.AreSame(연구목록(), loaded, "연구대가 읽는 것과 다른 에셋이다");
    }

    [Test]
    public void 연구_목록에_빈_항목이_없다()
    {
        var b = 연구목록();
        Assert.Greater(b.entries.Length, 0, "목록이 비면 연구대가 쓸모없다");
        for (int i = 0; i < b.entries.Length; i++)
            Assert.IsNotNull(b.entries[i], $"entries[{i}]가 비어 있다");
    }

    [Test]
    public void 연구_목록의_id가_전부_채워져_있고_겹치지_않는다()
    {
        var 본것 = new HashSet<string>();
        foreach (var e in 연구목록().entries)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(e.id), $"{e.name}의 id가 비어 있다");
            Assert.IsTrue(본것.Add(e.id), $"id가 겹친다: {e.id}");
        }
    }

    [Test]
    public void 연구_항목의_id가_레시피_id와_겹치지_않는다()
    {
        // 화면의 줄 이름이 둘 다 "Row_<id>"라, 겹치면 검증 하네스도 사람도 엉뚱한
        // 줄을 누르게 된다.
        var 레시피들 = AssetDatabase.LoadAssetAtPath<RecipeBookSO>(RecipeBookPath)
                                    .recipes.Where(r => r != null).Select(r => r.id).ToHashSet();

        foreach (var e in 연구목록().entries)
            Assert.IsFalse(레시피들.Contains(e.id), $"연구 항목 {e.id}가 같은 이름의 레시피와 겹친다");
    }

    [Test]
    public void 태울_것이_스크랩으로_지정돼_있다()
    {
        var b = 연구목록();
        Assert.IsNotNull(b.energyItem, "태울 것이 없으면 환급할 수가 없어 모든 연구가 거절된다");
        Assert.AreEqual(ResearchService.DefaultEnergyItemId, b.energyItem.id,
            "스크랩은 에너지 저장 매체다 — 연구대가 태우는 것도 그것이다");
    }

    [Test]
    public void 모든_연구_항목이_대가와_시간과_산출을_갖췄다()
    {
        foreach (var e in 연구목록().entries)
        {
            Assert.IsNotEmpty(e.displayName, $"{e.id}에 이름이 없으면 빈 줄이 뜬다");
            Assert.IsNotNull(e.unlocks, $"{e.id}의 unlocks가 null이다");
            Assert.IsNotNull(e.unlockKeys, $"{e.id}의 unlockKeys가 null이다");
            Assert.IsTrue(ResearchService.HasAnyUnlock(e),
                $"{e.id}가 아무것도 열지 않는다 — 걸어 봐야 아무 일도 일어나지 않는다");

            foreach (var bp in e.unlocks)
            {
                Assert.IsNotNull(bp, $"{e.id}의 unlocks에 빈 칸이 있다");
                Assert.IsFalse(string.IsNullOrWhiteSpace(bp.id),
                    $"{e.id}가 여는 청사진에 id가 없다 — 원장은 문자열로만 기억한다");
            }

            foreach (var key in e.unlockKeys)
                Assert.IsFalse(string.IsNullOrWhiteSpace(key),
                    $"{e.id}의 unlockKeys에 빈 칸이 있다 — 원장에 빈 줄이 적힌다");

            Assert.Greater(e.researchSeconds, 0f, $"{e.id}가 즉시 끝나면 연구가 아니라 버튼이다");
            Assert.Greater(e.energyCost, 0, $"{e.id}가 공짜다 — 연구대의 대가가 사라진다");
            Assert.IsNotNull(e.materials, $"{e.id}의 materials가 null이다");
            Assert.Greater(e.materials.Length, 0, $"{e.id}가 소재를 요구하지 않는다");

            foreach (var m in e.materials)
            {
                Assert.IsNotNull(m?.item, $"{e.id}의 소재에 빈 칸이 있다");
                Assert.Greater(m.count, 0, $"{e.id}의 소재 수량이 0이다");
            }
        }
    }

    /// <summary>연구의 정형구 — 제작법을 얻은 항목이 닫는 말.</summary>
    const string 제작법정형구 = "제작법을 확보했습니다.";

    /// <summary>
    /// 도감의 정형구 — 만드는 법이 아니라 <b>개체가 무엇인가</b>를 알게 된 항목이 닫는 말.
    /// 백로그 43(도감 UI)이 이 문장을 항목 설명문으로 읽는다.
    /// </summary>
    const string 도감정형구 = "개체의 정보가 정리되었습니다.";

    [Test]
    public void AI_대사가_기계의_말투를_지킨다()
    {
        // DiscoverySO에 적어 둔 규격. 하나만 사람처럼 말해도 목소리가 무너진다.
        foreach (var e in 연구목록().entries)
        {
            var line = e.line;
            Assert.IsNotNull(line, $"{e.id}의 대사가 null이다");
            Assert.IsNotEmpty(line.text, $"{e.id}가 아무 말도 하지 않는다");
            Assert.AreEqual("우주복 AI", line.speaker, $"{e.id}의 화자가 다르다");

            Assert.IsTrue(line.text.Contains("분석..."),
                $"{e.id}: 첫 문장이 '분석...'으로 닫히지 않는다 — \"{line.text}\"");
            Assert.IsTrue(line.text.Contains("것으로 보입니다") || line.text.Contains("판단됩니다"),
                $"{e.id}: 용도 판정이 단정이다(관찰투가 아니다) — \"{line.text}\"");

            // 정형구는 산출물의 종류를 따른다. 청사진을 얻은 것과 개체를 알게 된 것은
            // 같은 문장으로 닫을 수 없다 — 주운 것과 밝혀낸 것을 가른 것과 같은 이유다.
            string 정형구 = e.unlocks.Length > 0 ? 제작법정형구 : 도감정형구;
            Assert.IsTrue(line.text.EndsWith(정형구),
                $"{e.id}: 정형구로 닫히지 않는다(기대 \"{정형구}\") — \"{line.text}\"");

            foreach (var 금지 in new[] { "!", "?", "겠어", "네요", "흥미" })
                Assert.IsFalse(line.text.Contains(금지),
                    $"{e.id}: 기계가 하지 않는 말이 섞였다({금지}) — \"{line.text}\"");
        }
    }

    [Test]
    public void 두_정형구가_서로_섞이지_않는다()
    {
        // 하나가 두 정형구를 다 달면 무엇을 얻었는지 문장으로는 구별할 수 없다.
        foreach (var e in 연구목록().entries)
        {
            bool 제작 = e.line.text.Contains(제작법정형구);
            bool 도감 = e.line.text.Contains(도감정형구);
            Assert.IsFalse(제작 && 도감, $"{e.id}가 두 정형구를 다 달고 있다");
            Assert.IsTrue(제작 || 도감, $"{e.id}가 어느 정형구로도 닫지 않는다");
        }
    }

    // ── 유물 아이템 ──────────────────────────────────────────

    [TestCase(Membrane)]
    [TestCase(Core)]
    public void 유물이_아이템_DB에_등록돼_있다(string id)
    {
        // 등록이 빠지면 세이브를 다시 불러왔을 때 조용히 사라진다.
        Assert.IsTrue(아이템DB().TryGetById(id, out var item), $"ItemDatabase에 {id}가 없다");
        Assert.IsNotEmpty(item.displayName, $"{id}에 이름이 없다");
        Assert.IsNotEmpty(item.description, $"{id}에 설명이 없다");
        Assert.AreEqual(ItemCategory.Resource, item.category);
    }

    [Test]
    public void 유물이_각각_다른_연구_항목의_소재다()
    {
        var 소재별 = new Dictionary<string, List<string>>();
        foreach (var e in 연구목록().entries)
            foreach (var m in e.materials)
                if (m?.item != null)
                {
                    if (!소재별.TryGetValue(m.item.id, out var list))
                        소재별[m.item.id] = list = new List<string>();
                    list.Add(e.id);
                }

        foreach (var id in new[] { Membrane, Core })
        {
            Assert.IsTrue(소재별.ContainsKey(id), $"{id}를 쓰는 연구 항목이 없다 — 주워도 쓸 데가 없다");
            Assert.AreEqual(1, 소재별[id].Count,
                $"{id}가 여러 항목의 소재다: {string.Join(", ", 소재별[id])} — 종별 고유 해금이 흐려진다");
        }
    }

    // ── 사슬: 유물 → 연구 → 청사진 → 레시피 ─────────────────

    [TestCase(Membrane, "res_surface_walker", "bp_surface_walker", "surface_walker")]
    [TestCase(Core, "res_submersible", "bp_submersible", "submersible")]
    public void 유물에서_장비까지_한_줄로_이어진다(string 유물, string 연구, string 청사진, string 장비)
    {
        var e = 항목(연구);

        Assert.IsTrue(e.materials.Any(m => m?.item != null && m.item.id == 유물),
            $"{연구}가 {유물}을 요구하지 않는다");
        Assert.IsTrue(e.unlocks.Any(bp => bp != null && bp.id == 청사진),
            $"{연구}가 {청사진}을 열지 않는다");

        var r = 레시피(장비);
        Assert.IsNotNull(r.requiredBlueprint,
            $"{장비} 레시피에 요구 청사진이 없다 — 연구를 건너뛰고 만들 수 있다");
        Assert.AreEqual(청사진, r.requiredBlueprint.id,
            $"{장비}가 요구하는 청사진이 연구가 여는 것과 다르다");
    }

    [TestCase("surface_walker")]
    [TestCase("submersible")]
    public void 진행_장비는_원장이_비면_잠겨_있다(string 장비)
    {
        var r = 레시피(장비);
        var 빈원장 = new UnlockLedger();

        Assert.IsFalse(BlueprintGate.IsUnlocked(r.requiredBlueprint, 빈원장),
            $"{장비}가 아무것도 모르는 상태에서 열려 있다");
    }

    [TestCase("res_surface_walker", "surface_walker")]
    [TestCase("res_submersible", "submersible")]
    public void 연구를_마치면_그_장비가_열린다(string 연구, string 장비)
    {
        var e = 항목(연구);
        var r = 레시피(장비);
        var ledger = new UnlockLedger();

        Assert.IsFalse(BlueprintGate.IsUnlocked(r.requiredBlueprint, ledger));

        ResearchService.Apply(e, ledger);

        Assert.IsTrue(BlueprintGate.IsUnlocked(r.requiredBlueprint, ledger),
            "연구를 마쳤는데도 잠겨 있다면 사슬이 끊긴 것이다");
        Assert.IsTrue(ResearchService.IsKnown(e, ledger), "끝난 연구가 목록에서 다시 걸린다");
    }

    [Test]
    public void 진행_장비는_현장_발견으로는_열리지_않는다()
    {
        // 스펙 §2: 진행 장비는 연구 채널 뒤에 선다. 아무 재료나 주워서 열리면
        // 유물을 구하러 낫의 영역에 갈 이유가 사라진다.
        var 현장해금 = AssetDatabase.LoadAssetAtPath<DiscoveryBookSO>(
            "Assets/08.Data/Progression/Resources/DiscoveryBook.asset");
        Assert.IsNotNull(현장해금, "DiscoveryBook을 못 읽었다");

        var 연구산출 = 연구목록().entries
            .SelectMany(e => e.unlocks).Where(bp => bp != null).Select(bp => bp.id).ToHashSet();

        foreach (var d in 현장해금.discoveries)
        {
            if (d?.unlocks == null) continue;
            foreach (var bp in d.unlocks)
                Assert.IsFalse(bp != null && 연구산출.Contains(bp.id),
                    $"{d.id}를 주우면 연구 산출물 {bp.id}가 공짜로 열린다");
        }
    }

    // ── 연구대 건축물 ────────────────────────────────────────

    static BuildableSO 연구대()
    {
        var c = AssetDatabase.LoadAssetAtPath<BuildCatalogSO>(CatalogPath);
        Assert.IsNotNull(c, $"{CatalogPath}를 못 읽었다");

        var b = c.GetById("research_bench");
        Assert.IsNotNull(b, "건설 목록에 research_bench가 없다 — 지을 방법이 없다");
        return b;
    }

    /// <summary>
    /// 컴포넌트를 이름으로 찾는다. <c>ResearchStation</c>·<c>BuiltStructure</c>는
    /// Assembly-CSharp에 있어 이 테스트 어셈블리가 형으로 참조할 수 없다 —
    /// 붙어 있는지를 보려는 것뿐이므로 이름이면 족하다.
    /// </summary>
    static bool 컴포넌트가_있다(GameObject go, string typeName) =>
        go != null && go.GetComponents<Component>()
                        .Any(c => c != null && c.GetType().Name == typeName);

    [Test]
    public void 연구대를_지을_수_있게_등록돼_있다()
    {
        var b = 연구대();
        Assert.IsNotEmpty(b.displayName);
        Assert.IsNotNull(b.prefab, "세울 프리팹이 없다");
        Assert.IsTrue(컴포넌트가_있다(b.prefab, "ResearchStation"),
            "프리팹에 ResearchStation이 없다 — 세워도 상호작용이 안 된다");
        Assert.IsTrue(컴포넌트가_있다(b.prefab, "BuiltStructure"),
            "BuiltStructure가 없으면 철거도 겹침 판정도 안 된다");

        var col = b.prefab.GetComponent<Collider>();
        Assert.IsNotNull(col, "콜라이더가 없으면 조준되지 않는다");
        Assert.IsFalse(col.isTrigger, "트리거면 NavMesh 장애물이 붙지 않는다");
    }

    [Test]
    public void 연구대는_부품을_겪어야_알_수_있다()
    {
        // 스펙: 부품을 주워봤어야 연구대를 안다. 처음부터 열려 있으면
        // 채널 1(현장 발견)을 건너뛴 사람도 연구대를 세울 수 있다.
        var b = 연구대();
        Assert.IsNotNull(b.requiredBlueprint, "요구 청사진이 없어 처음부터 열려 있다");
        Assert.AreEqual("bp_salvaged_mechanism", b.requiredBlueprint.id);
    }

    [Test]
    public void 연구대의_비용이_관례를_따른다()
    {
        // 제작대와 같은 계열의 건축물이다 — 스크랩·부품·목재.
        var 비용 = 연구대().cost.Where(c => c?.item != null)
                            .ToDictionary(c => c.item.id, c => c.count);

        foreach (var id in new[] { "scrap", "machine_part", Survive.Harvesting.MushroomLumberRule.WoodItemId })
        {
            Assert.IsTrue(비용.ContainsKey(id), $"연구대 비용에 {id}가 없다");
            Assert.Greater(비용[id], 0);
        }

        var bench = AssetDatabase.LoadAssetAtPath<BuildCatalogSO>(CatalogPath).GetById("bench");
        var 제작대비용 = bench.cost.Where(c => c?.item != null)
                                 .ToDictionary(c => c.item.id, c => c.count);

        Assert.Greater(비용["machine_part"], 제작대비용["machine_part"],
            "연구대가 제작대보다 싸면 순서가 뒤집힌다");
    }

    [Test]
    public void 연구대_비용에_유물이_들어가지_않는다()
    {
        // 유물은 연구대에서 쓰는 것이다. 연구대를 짓는 데 유물이 필요하면
        // 유물을 얻기 전에는 연구대가 없고, 연구대가 없으면 유물이 쓸모없다.
        foreach (var c in 연구대().cost)
            Assert.IsFalse(c?.item != null && (c.item.id == Membrane || c.item.id == Core),
                "연구대를 유물로 지으면 순환이라 영원히 못 짓는다");
    }

    // ══ 백로그 39 — 연구 소재 공급 ═══════════════════════════

    /// <summary>
    /// 다음 작업(백로그 43, 도감 UI)이 읽는 <b>계약</b>. 이 목록이 이름 그대로
    /// 원장에 적히지 않으면 도감 화면이 영원히 빈 채로 뜬다.
    /// </summary>
    static readonly string[] 도감열쇠들 =
    {
        "codex_ball", "codex_eye", "codex_wing", "codex_fruit_crab", "codex_scythe",
    };

    static CreatureDefinitionSO[] 생물들()
    {
        var found = AssetDatabase.FindAssets("t:CreatureDefinitionSO", new[] { "Assets/08.Data/Creatures" })
            .Select(g => AssetDatabase.LoadAssetAtPath<CreatureDefinitionSO>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(d => d != null)
            .ToArray();

        Assert.Greater(found.Length, 0, "생물 정의를 하나도 못 읽었다");
        return found;
    }

    static string 부품id(CreatureDefinitionSO d) => "part_" + d.id;
    static string 도감열쇠(CreatureDefinitionSO d) => "codex_" + d.id;
    static string 도감항목id(CreatureDefinitionSO d) => "res_codex_" + d.id;

    // ── 생물 부품 ────────────────────────────────────────────

    [Test]
    public void 모든_생물이_제_부품을_떨군다()
    {
        // 처치 드롭은 기존 LootTable 체계를 그대로 쓴다. 부품만 별도 경로로 떨구면
        // 스크랩과 부품이 서로 다른 규칙으로 나와 어느 쪽이 맞는지 알 수 없게 된다.
        foreach (var d in 생물들())
        {
            Assert.IsNotNull(d.drops, $"{d.id}에 전리품 표가 없다");

            var 부품 = d.drops.entries.FirstOrDefault(e => e?.item != null && e.item.id == 부품id(d));
            Assert.IsNotNull(부품, $"{d.id}의 전리품에 {부품id(d)}가 없다 — 잡아도 부품이 안 나온다");
            Assert.AreEqual(1f, 부품.chance, 0.001f,
                $"{부품id(d)}가 확률로 나오면 도감이 운에 걸린다 — 몸의 일부다");
            Assert.Greater(부품.maxCount, 0, $"{부품id(d)}의 수량이 0이다");
        }
    }

    [Test]
    public void 부품이_아이템_DB에_등록돼_있다()
    {
        foreach (var d in 생물들())
        {
            Assert.IsTrue(아이템DB().TryGetById(부품id(d), out var item),
                $"ItemDatabase에 {부품id(d)}가 없다 — 세이브를 다시 불러오면 조용히 사라진다");
            Assert.IsNotEmpty(item.displayName, $"{부품id(d)}에 이름이 없다");
            Assert.IsNotEmpty(item.description, $"{부품id(d)}에 설명이 없다");
            Assert.AreEqual(ItemCategory.Resource, item.category);
        }
    }

    [Test]
    public void 부품은_그_생물에서만_나온다()
    {
        // 아무 생물이나 잡아서 아무 도감이나 열리면 종별 고유 해금이 무너진다.
        var 전체 = 생물들();
        foreach (var 주인 in 전체)
        {
            foreach (var 남 in 전체)
            {
                if (남 == 주인 || 남.drops == null) continue;
                Assert.IsFalse(남.drops.entries.Any(e => e?.item != null && e.item.id == 부품id(주인)),
                    $"{남.id}가 {부품id(주인)}를 떨군다");
            }
        }
    }

    // ── 부품 연구 → 도감 열쇠 ────────────────────────────────

    [Test]
    public void 생물마다_도감_연구가_하나씩_있다()
    {
        foreach (var d in 생물들())
        {
            var e = 연구목록().Find(도감항목id(d));
            Assert.IsNotNull(e, $"연구 목록에 {도감항목id(d)}가 없다 — 부품을 모아도 쓸 데가 없다");

            Assert.AreEqual(1, e.materials.Length, $"{e.id}는 제 부품 하나만 요구한다");
            Assert.AreEqual(부품id(d), e.materials[0].item.id, $"{e.id}가 다른 것을 요구한다");
            Assert.Greater(e.materials[0].count, 1,
                $"{e.id}가 부품 하나로 끝난다 — 여러 마리를 겪어야 개체를 안다");
        }
    }

    [Test]
    public void 도감_연구가_여는_것은_청사진이_아니라_원장_열쇠다()
    {
        // 도감은 만드는 법이 아니다. 청사진으로 위장해 두면 잠긴 제작 줄에
        // "codex_공 청사진이 필요하다"가 뜬다.
        foreach (var d in 생물들())
        {
            var e = 연구목록().Find(도감항목id(d));
            Assert.AreEqual(0, e.unlocks.Length, $"{e.id}가 청사진을 연다");
            Assert.AreEqual(new[] { 도감열쇠(d) }, e.unlockKeys,
                $"{e.id}가 여는 열쇠가 계약과 다르다");
        }
    }

    [Test]
    public void 도감_열쇠_목록이_계약대로다()
    {
        // 백로그 43(도감 UI)이 이 이름들로 원장을 읽는다. 하나라도 바뀌면
        // 그쪽 화면이 조용히 빈 채로 뜬다.
        var 실제 = 연구목록().entries
            .SelectMany(e => e.unlockKeys)
            .Where(k => k.StartsWith("codex_"))
            .OrderBy(k => k)
            .ToArray();

        CollectionAssert.AreEqual(도감열쇠들.OrderBy(k => k).ToArray(), 실제);
    }

    [Test]
    public void 도감_연구를_마치면_그_열쇠가_원장에_적힌다()
    {
        foreach (var d in 생물들())
        {
            var e = 연구목록().Find(도감항목id(d));
            var ledger = new UnlockLedger();

            Assert.IsFalse(ledger.IsUnlocked(도감열쇠(d)));
            Assert.IsFalse(ResearchService.IsKnown(e, ledger));

            ResearchService.Apply(e, ledger);

            Assert.IsTrue(ledger.IsUnlocked(도감열쇠(d)), $"{e.id}를 마쳐도 {도감열쇠(d)}가 안 적힌다");
            Assert.IsTrue(ResearchService.IsKnown(e, ledger), "끝난 연구가 목록에서 다시 걸린다");
        }
    }

    [Test]
    public void 도감_연구는_유물_연구보다_싸다()
    {
        // 부품은 걸어 다니는 것을 잡으면 나오고, 유물은 낫의 영역에 머물러야 나온다.
        // 대가가 뒤집히면 위험을 감수할 이유가 사라진다.
        int 유물최소 = new[] { "res_surface_walker", "res_submersible" }
            .Select(id => 항목(id).energyCost).Min();

        foreach (var d in 생물들())
            Assert.Less(연구목록().Find(도감항목id(d)).energyCost, 유물최소,
                $"{도감항목id(d)}가 유물 연구만큼 비싸다");
    }

    // ── 낫의 유물 순찰 드롭 ──────────────────────────────────

    static Survive.Progression.RelicShedSO 흘림표()
    {
        var 낫 = 생물들().FirstOrDefault(d => d.id == "scythe");
        Assert.IsNotNull(낫, "낫 정의를 못 찾았다");
        Assert.IsNotNull(낫.relicShed, "낫이 유물을 흘리지 않는다 — 유물이 세계 어디에도 없게 된다");
        return 낫.relicShed;
    }

    [Test]
    public void 낫만_유물을_흘린다()
    {
        // 유물은 낫의 그림자에서 줍는 것이다(스펙 §1). 다른 종이 흘리면 그 픽션이 없어진다.
        foreach (var d in 생물들())
            if (d.id != "scythe")
                Assert.IsNull(d.relicShed, $"{d.id}가 유물을 흘린다");
    }

    [Test]
    public void 낫이_흘리는_유물이_각각_제_연구와_짝지어져_있다()
    {
        var 표 = 흘림표();
        Assert.AreEqual(2, 표.relics.Length, "유물은 둘이다 — 막과 핵");

        var 짝 = 표.relics.ToDictionary(r => r.item.id, r => r.researchUnlock.id);
        Assert.AreEqual("bp_surface_walker", 짝[Membrane]);
        Assert.AreEqual("bp_submersible", 짝[Core]);

        // pity가 "이미 밝혀낸 것"을 가진 것으로 치려면, 짝지은 열쇠가 실제로 그 연구가
        // 여는 것과 같아야 한다. 어긋나면 다 밝혀낸 유물이 영원히 다시 떨어진다.
        foreach (var r in 표.relics)
        {
            var 연구 = 연구목록().entries.First(e => e.materials.Any(m => m.item == r.item));
            Assert.IsTrue(연구.unlocks.Any(bp => bp == r.researchUnlock),
                $"{r.item.id}가 짝지은 열쇠를 그 연구가 열지 않는다");
        }
    }

    [Test]
    public void 유물은_처치_전리품이_아니다()
    {
        // 낫은 이기라고 놓은 상대가 아니다. 시체에서만 나오면 진행의 열쇠가
        // "낫을 죽여라"가 되고, 랜턴을 켜고 물러나는 것이 손해가 된다.
        foreach (var d in 생물들())
        {
            if (d.drops == null) continue;
            foreach (var e in d.drops.entries)
                Assert.IsFalse(e?.item != null && (e.item.id == Membrane || e.item.id == Core),
                    $"{d.id}의 전리품에 유물이 들어 있다");
        }
    }

    [Test]
    public void 유물을_한번_보려면_1분에서_2분쯤_걸린다()
    {
        // 스펙의 감각: "낫 영역에 1~2분 머물면 하나 볼 수 있다".
        // 굴림 간격 T·확률 p의 기하분포이므로 중앙값은 T·ln2/(-ln(1-p)), 평균은 T/p다.
        var 표 = 흘림표();
        Assert.Greater(표.chance, 0f, "확률이 0이면 영영 안 나온다");
        Assert.Less(표.chance, 1f, "확률이 1이면 시계처럼 나온다 — 가끔이 아니다");

        double 중앙값 = 표.intervalSeconds * System.Math.Log(2.0) / -System.Math.Log(1.0 - 표.chance);
        Assert.That(중앙값, Is.InRange(45.0, 120.0),
            $"절반이 기다리는 시간 {중앙값:F0}초가 1~2분 감각을 벗어난다");
        Assert.That(표.MeanSecondsToDrop, Is.InRange(60f, 180f),
            $"평균 {표.MeanSecondsToDrop:F0}초");
    }

    [Test]
    public void 사람이_곁에_있을_때만_굴린다()
    {
        // 아무도 없는 곳에 떨궈 봐야 아무도 못 본다. 반경이 낫의 감지 반경보다는
        // 넓어야 "쫓기지 않는 거리에서 기다린다"가 성립한다.
        var 낫 = 생물들().First(d => d.id == "scythe");
        Assert.Greater(낫.relicShed.witnessRadius, 낫.detectRadius,
            "관측 반경이 감지 반경보다 좁으면 반드시 쫓기면서만 유물이 나온다");
    }
}
