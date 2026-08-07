using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Survive.Crafting;
using Survive.Harvesting;
using Survive.Items;
using Survive.World;

/// <summary>
/// 지상에서 나오는 것 — <b>매크로늄은 0건, 이종 합금은 근거를 단 예외</b> (스펙 §9).
///
/// <b>왜 예외에 근거를 매다는가.</b> 「지상에는 매크로늄이 없다」는 규칙에는 예외가 하나
/// 있다 — 이종 합금은 지상에 남는다. 예외를 목록으로만 적어 두면 그 목록은 늘어나기만
/// 하고, 몇 달 뒤에는 아무도 왜 거기 있는지 모른다. 그래서 여기서는 <b>예외의 근거인
/// 사슬을 한 마디씩 실제로 밟는다</b>: 「액면 보행 장비」가 이종 합금을 요구하고 →
/// 그 결과물이 액면을 뚫는 장비이고 → 그 장비가 여는 것이 먼바다다.
/// <b>사슬 중 한 마디라도 끊기면 이 검사가 빨개진다</b> — 근거가 사라졌으니 예외도 함께.
///
/// <b>2026-08-07에 논증을 다시 세웠다.</b> 옛 근거는 「등급 사슬의 첫 칸」과 「군도의
/// 이쪽 섬」이었는데 둘 다 폐기됐다(세계관 §8). 지금 서 있는 근거는
/// <see cref="SurfaceResources"/>의 주석에 적어 두었다 — <b>지상에 있는 것은 이 행성이
/// 만든 것이 아니라 들여온 것의 잔해다.</b>
/// </summary>
public class SurfaceResourceTests
{
    const string 씬 = "Assets/01.Scenes/MainScene.unity";
    const string 채집물폴더 = "Assets/08.Data/HarvestNodes";
    const string 예외레시피 = "Assets/08.Data/Recipes/surface_walker.asset";

    // ── 지상 배치표 — 씬에 실제로 서 있는 것 ────────────────

    /// <summary>
    /// 씬에 서 있는 채집물의 정의들.
    ///
    /// <b>왜 씬을 열지 않고 본문을 읽는가.</b> EditMode에서 씬을 열면 사람이 편집 중이던
    /// 씬을 밀어낸다. 필요한 것은 "어떤 채집물이 몇 개 서 있는가"뿐이고 그것은
    /// YAML 본문에 <c>definition: {fileID: 11400000, guid: ...}</c>로 적혀 있다.
    /// </summary>
    static List<HarvestNodeSO> 씬에_선_채집물들()
    {
        string 본문 = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), 씬));
        var 나온것 = new List<HarvestNodeSO>();

        foreach (Match m in Regex.Matches(본문, @"definition:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-f]{32})"))
        {
            string 경로 = AssetDatabase.GUIDToAssetPath(m.Groups[1].Value);
            var 정의 = AssetDatabase.LoadAssetAtPath<HarvestNodeSO>(경로);
            if (정의 != null) 나온것.Add(정의);
        }
        return 나온것;
    }

    static IEnumerable<string> 떨구는것(HarvestNodeSO 노드)
    {
        if (노드 == null || 노드.drops == null || 노드.drops.entries == null) yield break;
        foreach (var e in 노드.drops.entries)
            if (e != null && e.item != null) yield return e.item.id;
    }

    [Test]
    public void 씬에_채집물이_실제로_서_있다()
    {
        // 훑개가 아무것도 못 찾았는데 통과하면 아래 검사들이 전부 빈 검사가 된다.
        var 노드들 = 씬에_선_채집물들();
        Assert.Greater(노드들.Count, 20, "씬에서 채집물을 못 찾았다 — 훑는 무늬가 낡았는가");
    }

    /// <summary>게이트 — <b>지상 어느 자리에도 매크로늄이 없다.</b></summary>
    [Test]
    public void 지상에_선_채집물이_매크로늄을_떨구지_않는다()
    {
        var 걸린것 = 씬에_선_채집물들()
            .SelectMany(n => 떨구는것(n).Select(id => (n, id)))
            .Where(p => SurfaceResources.IsBanned(p.id))
            .Select(p => $"{p.n.name} → {p.id}")
            .Distinct()
            .ToList();

        Assert.IsEmpty(걸린것,
            "지상에 매크로늄을 떨구는 채집물이 서 있다. 매크로늄은 지하의 것이다:\n  " +
            string.Join("\n  ", 걸린것));
    }

    /// <summary>지상에서 나오는 것은 배치표에 적힌 셋뿐이다.</summary>
    [Test]
    public void 지상에서_나오는_것은_배치표에_적힌_셋뿐이다()
    {
        var 낯선것 = 씬에_선_채집물들()
            .SelectMany(n => 떨구는것(n).Select(id => (n, id)))
            .Where(p => !SurfaceResources.Allows(p.id))
            .Select(p => $"{p.n.name} → {p.id}")
            .Distinct()
            .ToList();

        Assert.IsEmpty(낯선것,
            "배치표에 없는 것이 지상에서 나온다:\n  " + string.Join("\n  ", 낯선것));
    }

    /// <summary>
    /// 매크로늄을 떨구는 채집물 자체는 <b>있어야 한다</b> — 지하에서 쓸 것이므로.
    /// 없으면 위 검사가 "아무것도 없어서 통과"가 되어 버린다.
    /// </summary>
    [Test]
    public void 매크로늄을_떨구는_채집물은_저장소에_남아_있다()
    {
        var 매크로늄것 = AssetDatabase.FindAssets("t:HarvestNodeSO", new[] { 채집물폴더 })
            .Select(g => AssetDatabase.LoadAssetAtPath<HarvestNodeSO>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(n => n != null && 떨구는것(n).Any(SurfaceResources.IsBanned))
            .ToList();

        Assert.IsNotEmpty(매크로늄것,
            "매크로늄을 떨구는 채집물이 아예 없다 — 그러면 지상 검사가 빈 검사가 된다");
    }

    // ── 이종 합금이라는 예외와 그 근거 ──────────────────────

    [Test]
    public void 이종_합금은_지상_배치표에_들어_있다()
    {
        CollectionAssert.Contains(SurfaceResources.Materials, SurfaceResources.ExceptionMaterial);
    }

    /// <summary>
    /// <b>예외의 근거 — 사슬 한 마디: 「액면 보행 장비」가 이종 합금을 요구한다.</b>
    /// </summary>
    [Test]
    public void 예외의_근거_레시피가_이종_합금을_요구한다()
    {
        var 레시피 = AssetDatabase.LoadAssetAtPath<RecipeSO>(예외레시피);
        Assert.IsNotNull(레시피, $"{예외레시피}를 못 읽었다");
        Assert.AreEqual(SurfaceResources.ExceptionRecipe, 레시피.id);

        var 이종합금 = 레시피.ingredients
            .Where(i => i != null && i.item != null && i.item.id == SurfaceResources.ExceptionMaterial)
            .ToList();

        Assert.IsNotEmpty(이종합금,
            "「액면 보행 장비」가 이종 합금을 더 이상 요구하지 않는다. " +
            "그러면 이종 합금이 지상에 남을 이유도 사라진다");
        Assert.Greater(이종합금.Sum(i => i.count), 0, "요구량이 0이면 요구하지 않는 것과 같다");
    }

    /// <summary>
    /// <b>사슬 두 마디: 그 레시피가 만드는 것이 액면을 뚫는 장비다.</b>
    /// 결과물이 다른 물건으로 바뀌면 사슬이 끊긴다.
    /// </summary>
    [Test]
    public void 예외의_근거_레시피가_만드는_것이_액면을_뚫는_장비다()
    {
        var 레시피 = AssetDatabase.LoadAssetAtPath<RecipeSO>(예외레시피);
        var 장비 = 레시피.result?.item as TraversalGearItemSO;

        Assert.IsNotNull(장비, "결과물이 이동 장비가 아니다 — 사슬이 끊겼다");
        Assert.AreEqual(TraversalGear.SurfaceWalker, 장비.gear);
    }

    /// <summary>
    /// <b>사슬 세 마디: 그 장비가 여는 것이 먼바다다.</b> 넓은 액면을 여는 장비가
    /// 다른 것으로 바뀌면, 지상에서 이종 합금을 모을 이유가 사라진다.
    /// </summary>
    [Test]
    public void 그_장비가_여는_것이_먼바다다()
    {
        var 먼바다 = new HazardZone(EnvironmentHazard.MacroniumSurface,
                                  SurfaceZones.LiquidAt(SurfaceResources.ExceptionOpens).Width);

        Assert.AreEqual(TraversalGear.SurfaceWalker, EnvironmentThreat.RequiredGear(먼바다.Hazard));
        Assert.AreEqual(SurfaceZone.OpenWater, SurfaceResources.ExceptionOpens);

        // 그리고 그 먼바다는 맨몸으로는 못 건넌다. 건널 수 있으면 장비가 아무것도 열지
        // 않고, 그러면 이종 합금을 지상에 둘 이유도 없다.
        Assert.AreEqual(CrossingVerdict.Fatal,
            SurfaceZones.Verdict(SurfaceZone.OpenWater, new List<GearCapability>()));
    }

    /// <summary>
    /// <b>순환이 아니다.</b> 내려가는 장비의 재료가 내려가야만 나오는 것이면
    /// 챕터가 영영 닫힌다. 재료 어느 하나도 매크로늄이 아니어야 한다.
    /// </summary>
    [Test]
    public void 그_장비의_재료에_지하_물질이_없다()
    {
        var 레시피 = AssetDatabase.LoadAssetAtPath<RecipeSO>(예외레시피);
        var 걸린것 = 레시피.ingredients
            .Where(i => i?.item != null && SurfaceResources.IsBanned(i.item.id))
            .Select(i => i.item.id)
            .ToList();

        Assert.IsEmpty(걸린것,
            "내려가는 장비가 지하 물질을 요구한다 — 순환이다: " + string.Join(", ", 걸린것));
    }

    /// <summary>
    /// <b>지상에서 이종 합금이 실제로 나온다.</b> 근거가 있어도 나오는 자리가 없으면
    /// 장비를 만들 수 없다. 출처는 「합금 더미」와 낫 둘이다(기획서 §2.1·§4.5).
    /// </summary>
    [Test]
    public void 합금_더미와_낫이_이종_합금을_흘린다()
    {
        foreach (var 이름 in SurfaceResources.ExceptionSources)
        {
            var 표 = AssetDatabase.LoadAssetAtPath<LootTableSO>($"Assets/08.Data/LootTables/{이름}.asset");
            Assert.IsNotNull(표, $"{이름} 전리품 표를 못 읽었다");

            bool 흘린다 = 표.entries != null && 표.entries.Any(
                e => e?.item != null && e.item.id == SurfaceResources.ExceptionMaterial);

            Assert.IsTrue(흘린다, $"{이름}이 이종 합금을 흘리지 않는다 — 지상 출처가 하나 사라졌다");
        }
    }

    /// <summary>
    /// 「합금 더미」가 씬에 실제로 서 있는가. 열세 곳이라고 기획서가 적었지만
    /// 개수는 지형이 다시 서면서 옮길 수 있으므로 <b>0이 아니라는 것</b>만 못 박는다.
    /// </summary>
    [Test]
    public void 합금_더미가_씬에_서_있다()
    {
        int 수 = 씬에_선_채집물들().Count(
            n => 떨구는것(n).Contains(SurfaceResources.ExceptionMaterial));

        Assert.Greater(수, 0, "이종 합금이 나오는 채집물이 씬에 하나도 없다");
    }
}
