using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Survive.Building;
using Survive.Crafting;
using Survive.Domain.Art;
using Survive.Harvesting;
using Survive.Items;
using Survive.Localization;
using Survive.Progression;

/// <summary>
/// 챕터 1 재건 스펙 §7 — 신규 재료 둘.
///
/// <b>무광버섯</b>은 B섬 지상에 나고 매크로늄을 빨아들여 차폐한다.
/// <b>매크로늄 석영</b>은 B섬 지하 동굴에 작게 자라며, 지하를 밝히는 조명과
/// 같은 성분이다 — 그래서 평야 천장 석영의 복선이 되고, 나중에 조명탄이
/// 자홍으로 터져도 광원 4색 규칙 안에 그대로 머문다.
///
/// <b>여기서 지키는 것은 데이터다.</b> 세계에 심는 것(배치)은 사람의 몫이라
/// 이 라운드가 만든 것은 아이템 SO 둘, 전리품 표 둘, 채집 노드 둘, 노드 프리팹
/// 둘뿐이다. 그 넷이 서로 물려 있지 않으면 사람이 씬에 갖다 놓는 순간
/// 아무 오류 없이 조용히 아무것도 안 나온다.
/// </summary>
public class NewMaterialDataTests
{
    const string DatabasePath = "Assets/08.Data/Items/ItemDatabase.asset";
    const string RecipeBookPath = "Assets/08.Data/Recipes/RecipeBook.asset";
    const string CatalogPath = "Assets/08.Data/Buildables/BuildCatalog.asset";
    const string DiscoveryBookPath = "Assets/08.Data/Progression/Resources/DiscoveryBook.asset";

    const string 무광버섯 = "matte_mushroom";
    const string 석영 = "macronium_quartz";

    const string 무광노드경로 = "Assets/08.Data/HarvestNodes/MatteMushroom.asset";
    const string 석영노드경로 = "Assets/08.Data/HarvestNodes/MacroniumQuartz.asset";
    const string 광맥경로 = "Assets/08.Data/HarvestNodes/OreVein.asset";

    const string 무광프리팹 = "Assets/05.Prefabs/Harvesting/Node_MatteMushroom.prefab";
    const string 석영프리팹 = "Assets/05.Prefabs/Harvesting/Node_MacroniumQuartz.prefab";

    static ItemDatabaseSO Database =>
        AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(DatabasePath);

    static HarvestNodeSO 노드(string path)
    {
        var n = AssetDatabase.LoadAssetAtPath<HarvestNodeSO>(path);
        Assert.IsNotNull(n, path + "를 못 읽었다");
        return n;
    }

    static ItemDataSO 아이템(string id)
    {
        var db = Database;
        Assert.IsNotNull(db, DatabasePath + "를 못 읽었다");
        var it = db.GetById(id);
        Assert.IsNotNull(it, $"ItemDatabase에 {id}가 없다 — 세이브를 다시 불러오면 조용히 사라진다");
        return it;
    }

    // ── 아이템 ───────────────────────────────────────────────

    [TestCase(무광버섯)]
    [TestCase(석영)]
    public void 새_재료가_아이템_DB에_등록돼_있다(string id)
    {
        var it = 아이템(id);
        Assert.IsNotEmpty(it.displayName, $"{id}에 이름이 없다");
        Assert.IsNotEmpty(it.description, $"{id}에 설명이 없다");
        Assert.AreEqual(ItemCategory.Resource, it.category, $"{id}는 재료다");
        Assert.Greater(it.maxStack, 1, $"{id}를 한 칸에 하나씩 지고 다니면 원정이 성립하지 않는다");
    }

    [TestCase(무광버섯)]
    [TestCase(석영)]
    public void 새_재료에_아이콘이_있다(string id)
    {
        // 아이콘이 없으면 바닥에서 무엇인지 알 수 없는 덩어리로 떨어진다
        // (DropVisualRule). 전수 검사는 DropVisualTests가 하고, 여기서는
        // 이 둘을 이름으로 못 박아 실패 문구가 무엇이 빠졌는지 바로 말하게 한다.
        var it = 아이템(id);
        Assert.IsNotNull(it.icon, $"{id}에 아이콘이 없다");
        Assert.AreEqual(DropVisualKind.IconBillboard, DropVisualRule.Choose(it, false),
                        $"{id}가 알 수 없는 덩어리로 떨어진다");
    }

    [TestCase(무광버섯)]
    [TestCase(석영)]
    public void 새_재료의_이름과_설명이_번역_표에_있다(string id)
    {
        // 전수 게이트는 DataTextGateTests가 든다. 이 둘만은 여기서도 못 박는다 —
        // 새 아이템이 표를 거치지 않으면 로케일을 바꾼 화면에서만 티가 난다.
        var it = 아이템(id);
        Assert.IsTrue(Loc.TryT(DataText.NameKey(it), out _), $"Item/{id}.name이 표에 없다");
        Assert.IsTrue(Loc.TryT(DataText.DescKey(it), out _), $"Item/{id}.desc가 표에 없다");
    }

    // ── 채집 노드가 제 재료를 떨군다 ─────────────────────────

    [TestCase(무광노드경로, 무광버섯)]
    [TestCase(석영노드경로, 석영)]
    public void 노드가_제_재료를_떨군다(string 노드경로, string 아이템id)
    {
        var n = 노드(노드경로);
        Assert.IsNotNull(n.drops, $"{노드경로}에 전리품 표가 없다 — 캐도 아무것도 안 나온다");

        var 항목 = n.drops.entries.FirstOrDefault(e => e?.item != null && e.item.id == 아이템id);
        Assert.IsNotNull(항목, $"{노드경로}가 {아이템id}를 떨구지 않는다");
        Assert.AreEqual(1f, 항목.chance, 0.001f,
                        $"{아이템id}가 확률로 나오면 캐는 것이 도박이 된다");
        Assert.GreaterOrEqual(항목.minCount, 1, $"{아이템id}의 최소 수량이 0이다");
        Assert.GreaterOrEqual(항목.maxCount, 항목.minCount);
    }

    [TestCase(무광노드경로)]
    [TestCase(석영노드경로)]
    public void 노드_이름이_번역_표에_있다(string 노드경로)
    {
        var n = 노드(노드경로);
        Assert.IsNotEmpty(n.displayName, $"{노드경로}에 이름이 없다");
        Assert.IsTrue(Loc.TryT(DataText.NameKey(n), out _),
                      $"Harvest/{DataText.IdOf(n)}.name이 표에 없다 — 프롬프트가 키로 뜬다");
    }

    // ── 채집 규칙 경계값 ─────────────────────────────────────

    [Test]
    public void 무광버섯은_맨손으로_딴다()
    {
        // B섬 지상에 나 있는 버섯이다. 여기에 도구를 요구하면 방호복이
        // "도구를 만든 다음에야 볼 수 있는 것"이 되어 동선이 한 겹 늘어난다.
        var n = 노드(무광노드경로);
        Assert.AreEqual(ToolType.None, n.requiredTool);
        Assert.IsTrue(ToolMatch.Satisfies(n.requiredTool, n.requiredTier, ToolType.None, 0),
                      "맨손으로 못 딴다");
        Assert.IsTrue(ToolMatch.Satisfies(n.requiredTool, n.requiredTier, ToolType.Pickaxe, 1),
                      "도구를 들고 있으면 오히려 못 딴다");
        Assert.Greater(n.baseDuration, 0f, "홀드 시간이 0이면 눌리자마자 사라진다");
    }

    [TestCase(ToolType.None, 0, false, TestName = "석영_경계값_맨손은_안된다")]
    [TestCase(ToolType.Axe, 3, false, TestName = "석영_경계값_도끼는_아무리_좋아도_안된다")]
    [TestCase(ToolType.Hammer, 3, false, TestName = "석영_경계값_망치도_안된다")]
    [TestCase(ToolType.Pickaxe, 0, false, TestName = "석영_경계값_등급이_모자란_곡괭이는_안된다")]
    [TestCase(ToolType.Pickaxe, 1, true, TestName = "석영_경계값_돌곡괭이면_된다")]
    [TestCase(ToolType.Pickaxe, 2, true, TestName = "석영_경계값_더_좋은_곡괭이도_된다")]
    public void 석영은_곡괭이라야_깨진다(ToolType 든것, int 등급, bool 되는가)
    {
        var n = 노드(석영노드경로);
        Assert.AreEqual(ToolType.Pickaxe, n.requiredTool,
                        "결정으로 굳은 매크로늄이다. 맨손으로 쥘 것이 아니다");
        Assert.AreEqual(되는가, ToolMatch.Satisfies(n.requiredTool, n.requiredTier, 든것, 등급));
    }

    [Test]
    public void 석영은_세계에_이미_있는_곡괭이로_캘_수_있다()
    {
        // 요구 등급이 지금 만들 수 있는 곡괭이보다 높으면, 캘 수 없는 자원을
        // 동굴에 심어 두는 셈이 된다. 그 사실은 지하까지 내려가 봐야 드러난다.
        var n = 노드(석영노드경로);
        var pickaxe = Database.GetById("pickaxe") as ToolItemSO;
        Assert.IsNotNull(pickaxe, "곡괭이가 아이템 DB에 없다");
        Assert.IsTrue(ToolMatch.Satisfies(n.requiredTool, n.requiredTier,
                                          pickaxe.toolType, pickaxe.tier),
                      $"세계의 유일한 곡괭이(tier {pickaxe.tier})로 석영(tier {n.requiredTier})을 못 캔다");
    }

    // ── "작게 자란다" — 광맥과 다른 물건이다 ─────────────────

    [Test]
    public void 석영은_광맥이_아니라_자라는_것이다()
    {
        var 석영노드 = 노드(석영노드경로);
        var 광맥 = 노드(광맥경로);

        Assert.Greater(석영노드.respawnSeconds, 0f,
                       "자란다고 한 것이 한 번 캐면 끝나면 그것은 광맥이다");
        Assert.AreEqual(0f, 광맥.respawnSeconds,
                        "광맥은 여전히 돌아오지 않는다 — 둘을 가르는 축이 이것이다");
        Assert.Less(석영노드.durability, 광맥.durability,
                    $"작게 자란 결정({석영노드.durability})이 광맥({광맥.durability})만큼 단단하면 크기가 거짓말이다");
    }

    [Test]
    public void 무광버섯도_다시_난다()
    {
        // 차폐재를 한 번 걷으면 그만인 소모품으로 두면, 방호복을 잃었을 때
        // 되돌아갈 길이 없어진다.
        Assert.Greater(노드(무광노드경로).respawnSeconds, 0f, "무광버섯이 다시 나지 않는다");
    }

    [Test]
    public void 새_노드는_식물이_아니라_채집물이다()
    {
        // PlantNodeSO를 쓰지 않은 이유를 데이터로 못 박는다. 생산자(CreatureFeeding)는
        // 반경 안의 PlantNode를 가리지 않고 먹는다 — 광물이 초식 대상이 되고,
        // 차폐재가 생태계에 볼모로 잡힌다. "작게 자란다"는 성장 단계가 아니라
        // 재생(respawnSeconds)으로 표현한다.
        foreach (var path in new[] { "Assets/08.Data/Plants/AshFern.asset",
                                     "Assets/08.Data/Plants/EmberFungus.asset" })
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<PlantNodeSO>(path),
                             "식물 노드가 사라졌다면 이 판단의 전제부터 다시 봐야 한다");

        Assert.IsNull(AssetDatabase.LoadAssetAtPath<PlantNodeSO>(무광노드경로));
        Assert.IsNull(AssetDatabase.LoadAssetAtPath<PlantNodeSO>(석영노드경로));
    }

    // ── 사람이 심을 수 있게 서 있는가 (프리팹) ───────────────
    //
    // HarvestNode는 Assembly-CSharp에 있어 이 어셈블리가 형으로 참조할 수 없다.
    // ResearchWiringTests가 ResearchStation을 다룬 방식 그대로 이름으로 찾는다.

    static Component 채집컴포넌트(GameObject go) =>
        go == null ? null
        : go.GetComponents<Component>().FirstOrDefault(c => c != null && c.GetType().Name == "HarvestNode");

    static GameObject 프리팹(string path)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.IsNotNull(go, $"{path}가 없다 — 사람이 B섬에 심을 것이 없다");
        return go;
    }

    [TestCase(무광프리팹, 무광노드경로)]
    [TestCase(석영프리팹, 석영노드경로)]
    public void 노드_프리팹이_제_정의를_물고_있다(string 프리팹경로, string 노드경로)
    {
        var go = 프리팹(프리팹경로);
        var comp = 채집컴포넌트(go);
        Assert.IsNotNull(comp, $"{프리팹경로}에 HarvestNode가 없다 — 세워도 캘 수 없다");

        var def = new SerializedObject(comp).FindProperty("definition").objectReferenceValue;
        Assert.AreSame(노드(노드경로), def,
                       $"{프리팹경로}의 정의가 비었거나 다른 것을 가리킨다");

        Assert.IsNotNull(go.GetComponentInChildren<Collider>(true),
                         "맞을 몸이 없으면 조준도 타격도 되지 않는다");
        Assert.IsNotNull(go.GetComponentInChildren<Renderer>(true),
                         "보이지 않는 자원은 없는 자원이다");
    }

    [Test]
    public void 석영_프리팹의_빛은_광원_4색_안이다()
    {
        // 다섯 번째 광원 색을 만들지 않는다. 전수 검사는 AutonomousGate가 하지만,
        // 이 개체는 "자홍이어야 한다"는 이유가 픽션에 있으므로 여기서 색을 못 박는다.
        var light = 프리팹(석영프리팹).GetComponentInChildren<Light>(true);
        Assert.IsNotNull(light, "지하 조명과 같은 성분이라는 것이 화면에 드러나지 않는다");
        Assert.AreEqual(LightType.Point, light.type);
        Assert.IsTrue(LightRule.IsAllowedColor(light.color),
                      "석영 빛이 광원 4색 밖이다: #" + ColorUtility.ToHtmlStringRGB(light.color));
        Assert.AreEqual(ColorUtility.ToHtmlStringRGB(ArtPalette.Macronium),
                        ColorUtility.ToHtmlStringRGB(light.color),
                        "석영은 매크로늄이 굳은 것이다. 매크로늄의 색으로 빛나야 한다");

        // 어둠을 지킨다. 이것은 길을 밝히는 등이 아니라 표식이다.
        Assert.LessOrEqual(light.range, 3f, $"반경 {light.range}m면 주변을 밝히기 시작한다");
        Assert.LessOrEqual(light.intensity, 1f, $"세기 {light.intensity}면 랜턴과 겨룬다");
    }

    [Test]
    public void 무광버섯_프리팹은_빛나지_않는다()
    {
        // 이름이 곧 성질이다. 빛을 빨아들이는 것이 발광하면 재료가 거짓말이 된다.
        var go = 프리팹(무광프리팹);
        Assert.IsNull(go.GetComponentInChildren<Light>(true), "무광버섯이 빛을 내고 있다");

        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var mat = r.sharedMaterial;
            Assert.IsNotNull(mat, $"{r.name}에 머티리얼이 없다");
            Assert.IsFalse(mat.IsKeywordEnabled("_EMISSION"),
                           $"{mat.name}이(가) 발광한다 — 무광이라는 이름과 어긋난다");
        }
    }

    [Test]
    public void 석영_프리팹의_발광이_자홍_계열_안이다()
    {
        foreach (var r in 프리팹(석영프리팹).GetComponentsInChildren<Renderer>(true))
        {
            var mat = r.sharedMaterial;
            Assert.IsNotNull(mat, $"{r.name}에 머티리얼이 없다");
            Assert.IsTrue(MaterialRule.IsAllowedShader(mat.shader.name),
                          $"{mat.name}: 허용 목록 밖 셰이더 {mat.shader.name}");
            Assert.IsTrue(MaterialRule.IsBandedSmoothness(mat.GetFloat("_Smoothness")),
                          $"{mat.name}: 스무스니스가 3단 밖 {mat.GetFloat("_Smoothness")}");
            Assert.IsTrue(mat.IsKeywordEnabled("_EMISSION"),
                          $"{mat.name}이(가) 빛나지 않는다 — 지하 조명과 같은 성분이라는 복선이 사라진다");
            Assert.IsTrue(MaterialRule.IsAllowedEmission(mat.GetColor("_EmissionColor")),
                          $"{mat.name}: 광원 4색 밖 발광 #" +
                          ColorUtility.ToHtmlStringRGB(mat.GetColor("_EmissionColor")));
        }
    }

    // ── 발견 대사는 제작법과 함께 온다 ───────────────────────

    /// <summary>
    /// <b>이 라운드의 설계 판단이 여기 걸려 있다.</b>
    ///
    /// 현장 발견의 마지막 문장은 정형구다 — "해당 물질을 사용한 제작법이 있습니다."
    /// 그런데 이 둘이 여는 제작법(매크로늄 방호복 · 조명탄 총)은 <b>다음 라운드</b>의
    /// 것이라 지금은 세계에 없다. 지금 대사를 넣으면 AI가 없는 목록을 가리키게 되고,
    /// 그것을 <c>RetiredContentGateTests.말을_거는_발견은_반드시_목록을_자라게_한다</c>가
    /// 이미 막는다. 그래서 <b>대사를 미뤘다.</b>
    ///
    /// 미룬 것을 잊지 않기 위해 <b>양방향</b>으로 못 박는다.
    /// <list type="bullet">
    /// <item>쓸 데가 없는 동안에는 발견이 <b>없어야</b> 한다 (거짓말 금지)</item>
    /// <item>제작법이 생기는 순간 발견이 <b>있어야</b> 한다 (침묵 금지)</item>
    /// </list>
    /// 다음 라운드가 방호복·조명탄 제작법을 넣으면 이 테스트가 곧바로 빨개지며
    /// 대사를 요구한다. 초안은 이미 있다 (문체 규격은 <c>DiscoverySO</c>의 문서 주석):
    /// <list type="number">
    /// <item>무광버섯: "빛과 방사를 흡수하는 유기 조직 분석... 차폐재로 사용가능한
    ///       것으로 보입니다. 해당 물질을 사용한 제작법이 있습니다."</item>
    /// <item>매크로늄 석영: "지하 조명과 동일한 성분의 결정 분석... 빛을 저장해
    ///       방출할 수 있는 것으로 보입니다. 해당 물질을 사용한 제작법이 있습니다."</item>
    /// </list>
    /// </summary>
    [TestCase(무광버섯)]
    [TestCase(석영)]
    public void 재료가_제작에_쓰이기_시작하면_발견도_함께_온다(string id)
    {
        var book = AssetDatabase.LoadAssetAtPath<DiscoveryBookSO>(DiscoveryBookPath);
        Assert.IsNotNull(book, DiscoveryBookPath + "를 못 읽었다");

        var d = book.Find(id);

        if (!쓰이는가(id))
        {
            Assert.IsNull(d,
                $"{id}로 만들 수 있는 것이 아직 없는데 발견이 걸려 있다. " +
                "AI가 \"제작법이 있습니다\"라고 말하는데 목록이 자라지 않으면 그것은 거짓말이다");
            return;
        }

        Assert.IsNotNull(d,
            $"{id}로 만드는 것이 생겼는데 현장 발견이 없다. " +
            "새 재료를 처음 쥐어도 AI가 아무 말도 하지 않고 제작법이 조용히 열린다");
        기계의_말투인가(d);
    }

    /// <summary>이 재료로 만들 수 있는 것이 세계에 하나라도 있는가.</summary>
    static bool 쓰이는가(string id)
    {
        var recipes = AssetDatabase.LoadAssetAtPath<RecipeBookSO>(RecipeBookPath);
        Assert.IsNotNull(recipes, RecipeBookPath + "를 못 읽었다");

        bool 제작 = recipes.recipes.Any(r => r != null && r.ingredients != null &&
            r.ingredients.Any(i => i?.item != null && i.item.id == id));

        var catalog = AssetDatabase.LoadAssetAtPath<BuildCatalogSO>(CatalogPath);
        Assert.IsNotNull(catalog, CatalogPath + "를 못 읽었다");

        bool 건축 = catalog.entries.Any(b => b != null && b.cost != null &&
            b.cost.Any(c => c?.item != null && c.item.id == id));

        return 제작 || 건축;
    }

    /// <summary>
    /// 우주복 AI의 세 문장 규격. 원본은 <see cref="DiscoverySO"/>의 문서 주석이다.
    /// </summary>
    static void 기계의_말투인가(DiscoverySO d)
    {
        var line = d.line;
        Assert.IsNotNull(line, $"{d.id}의 대사가 null이다");
        Assert.IsNotEmpty(line.text, $"{d.id}가 아무 말도 하지 않는다");
        Assert.AreEqual("우주복 AI", line.speaker, $"{d.id}의 화자가 다르다");

        Assert.IsTrue(line.text.Contains("분석..."),
                      $"{d.id}: 첫 문장이 '분석...'으로 닫히지 않는다 — \"{line.text}\"");
        Assert.IsTrue(line.text.Contains("것으로 보입니다") || line.text.Contains("판단됩니다"),
                      $"{d.id}: 용도 판정이 단정이다(관찰투가 아니다) — \"{line.text}\"");
        Assert.IsTrue(line.text.EndsWith("해당 물질을 사용한 제작법이 있습니다."),
                      $"{d.id}: 현장 발견의 정형구로 닫히지 않는다 — \"{line.text}\"");

        foreach (var 금지 in new[] { "!", "?", "겠어", "네요", "흥미" })
            Assert.IsFalse(line.text.Contains(금지),
                           $"{d.id}: 기계가 하지 않는 말이 섞였다({금지}) — \"{line.text}\"");

        // 분석기는 라벨을 모른다. 아이템 이름을 되뇌면 그것은 이해가 아니다.
        if (d.item != null && !string.IsNullOrWhiteSpace(d.item.displayName))
            Assert.IsFalse(line.text.Contains(d.item.displayName),
                           $"{d.id}: 아이템 이름을 되뇐다 — \"{line.text}\"");
    }
}
