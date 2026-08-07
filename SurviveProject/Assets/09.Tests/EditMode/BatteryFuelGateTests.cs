using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Survive.Building;
using Survive.Crafting;
using Survive.Harvesting;
using Survive.Items;
using Survive.Progression;
using Survive.World;

/// <summary>
/// 빛의 연료는 <b>스크랩</b>이다 — 2026-08-07 검토 회신 ①.
///
/// <b>무엇이 뒤집혔나.</b> 델타 <c>기획서-갱신점-2026-08-06_3</c>은 배터리 재료를
/// 스크랩에서 <b>매크로늄의 표면부</b>라는 새 재료로 옮기고, 매크로늄을 두 등급
/// (무른 표면부 / 단단한 안쪽)으로 갈랐다. 회신이 그것을 통째로 취소했다.
///
/// 그 재료의 온전한 이름은 이 파일 안에서 <see cref="폐기한이름"/>의 조각으로만
/// 적는다 — 훑개가 저장소 본문에서 그 말을 찾으므로, 여기 통째로 적어 두면
/// 이 파일 자신이 걸린다. 이름은 <c>Plan/</c>의 역사 서술에 남아 있다.
///
/// <b>근거는 설정 정합성이다.</b> 스크랩은 원래 "그 자체로 에너지가 되는 물질"이고
/// 생태계 전체의 연료다(<c>disc_scrap</c>의 AI 대사가 그대로 그렇게 말한다 —
/// "에너지로 사용가능한 것으로 보입니다"). 매크로늄은 열을 실어 나르고 섬을 띄우는
/// <b>구조체</b>이지 저장매체가 아니다. 에너지 매체를 둘로 두면 스크랩의 정체성이
/// 흐려지고, 그러면 "탐사 1분당 순증가"를 어느 자원으로 재야 하는지도 갈린다.
///
/// 따라서 <b>"빛의 연료가 곧 낫이 지키는 물질"이라는 논증도 폐기다.</b> 낫이
/// 플레이어를 적대하는 이유는 원래대로 은폐 프로토콜이다.
///
/// <b>왜 게이트가 필요한가 — 코드는 안 뒤집혔는데.</b> 2026-08-07 실측으로
/// 그 재료는 <b>코드·데이터 어디에도 만들어진 적이 없었다.</b> 문서만 앞서 갔고
/// 구현이 안 따라간 상태였다. 그래서 이 파일이 지우는 것은 없다 —
/// <b>다시 들어오지 못하게 막는 것</b>이 전부다. 지울 것이 없다는 사실 자체가
/// 기계가 말해 주지 않으면 다음 사람에게는 보이지 않고, 델타 문서는 저장소에
/// 그대로 남아 있어 읽는 사람마다 "아직 안 했구나" 하고 다시 시작하게 된다.
/// </summary>
public class BatteryFuelGateTests
{
    const string ItemDatabasePath = "Assets/08.Data/Items/ItemDatabase.asset";
    const string RecipeBookPath = "Assets/08.Data/Recipes/RecipeBook.asset";
    const string BuildCatalogPath = "Assets/08.Data/Buildables/BuildCatalog.asset";
    const string DiscoveryBookPath = "Assets/08.Data/Progression/Resources/DiscoveryBook.asset";
    const string HarvestNodeFolder = "Assets/08.Data/HarvestNodes";

    const string 스크랩 = "scrap";
    const string 배터리셀 = "battery_cell";
    const string 매크로늄 = "macronium";

    // ── ① 배터리는 스크랩으로 만든다 ─────────────────────────

    [Test]
    public void 배터리는_스크랩으로_만든다()
    {
        var r = 레시피(배터리셀);

        Assert.IsNotNull(r.ingredients, "배터리 레시피에 재료 칸이 없다");
        Assert.AreEqual(1, r.ingredients.Length,
            "배터리는 스크랩 하나만 먹는다. 재료가 늘면 「스크랩에서 에너지를 꺼낸다」가 " +
            "「이것저것으로 전지를 조립한다」로 바뀐다:\n  " + 재료설명(r));

        var 재료 = r.ingredients[0];
        Assert.IsNotNull(재료?.item, "배터리 레시피의 재료 칸이 비었다");
        Assert.AreEqual(스크랩, 재료.item.id,
            $"배터리 재료가 {재료.item.id}다. 회신 ①에 따라 스크랩이어야 한다 — " +
            "매크로늄은 열을 실어 나르는 구조체이지 저장매체가 아니다");
        Assert.GreaterOrEqual(재료.count, 1, "스크랩 0개로 전지가 나오면 연료 축이 없는 것이다");

        Assert.IsNotNull(r.result?.item, "배터리 레시피에 산출물이 없다");
        Assert.AreEqual(배터리셀, r.result.item.id);
        Assert.AreEqual(1, r.result.count, "셀 하나 = 배터리 가득이라는 대응이 깨진다");
    }

    /// <summary>
    /// 화톳불이라는 자리가 이 결정의 절반이다. 스크랩에 갇힌 에너지를 꺼내려면
    /// <b>열</b>이 든다 — 그래서 현장에서 즉시 바꿔 넣던 것이 거점으로 돌아갈
    /// 이유가 되었다(<c>LanternController.TryInsertBatteryCell</c>의 주석). 재료만 스크랩으로
    /// 두고 자리를 손 제작으로 옮기면 그 이유가 사라진다.
    /// </summary>
    [Test]
    public void 배터리는_불_곁에서만_뽑힌다()
    {
        var r = 레시피(배터리셀);
        Assert.AreEqual(StationType.Campfire, r.requiredStation,
            "스크랩에 갇힌 에너지를 꺼내는 데는 열이 든다. 손에서 만들면 거점이 필요 없어진다");
        Assert.Greater(r.craftSeconds, 0f, "즉시 나오면 「돌아갈 이유」가 시간이 아니라 절차가 된다");
    }

    /// <summary>
    /// <b>셀 하나가 배터리를 가득 채운다.</b> 이 대응이 깨지면 화톳불 추출 레시피가
    /// 먹는 스크랩 수와 배터리 눈금이 서로 다른 말을 하게 되고, 플레이어는
    /// 스크랩 몇 개어치를 태우고 있는지 셀 수 없게 된다
    /// (<see cref="LanternRule.MaxBattery"/>의 주석).
    ///
    /// 여기가 <b>실측 보고의 창구</b>이기도 하다 — 스크랩 몇 개가 랜턴 몇 초인지.
    /// </summary>
    [Test]
    public void 스크랩_몇_개가_랜턴_몇_초인지_한_줄로_이어진다()
    {
        var r = 레시피(배터리셀);
        int 스크랩수 = r.ingredients[0].count;

        Assert.AreEqual(LanternRule.MaxBattery, LanternRule.BatteryPerCell,
            "셀 하나가 가득을 채우지 않는다 — 눈금과 레시피가 다른 말을 한다");

        float 초 = LanternRule.SecondsOfLight(LanternRule.BatteryPerCell, 1);
        Assert.Greater(초, 0f, "셀을 끼워도 켜지는 시간이 0이다");

        UnityEngine.Debug.Log(
            $"[실측] 스크랩 {스크랩수}개 → 배터리 셀 1개 → 티어 1 랜턴 {초:F1}초 " +
            $"(스크랩 1개당 {LanternRule.BatteryPerCell / 스크랩수:F0} 전하, " +
            $"초당 소모 {LanternRule.DrainForTier(1):F2}, 스크랩 1개당 {초 / 스크랩수:F1}초)");
    }

    /// <summary>
    /// 레이더도 같은 축이다. <c>_3</c>이 레이더 재료로 폐기된 그 재료를 적었으므로
    /// 여기도 함께 못 박는다 — 배터리만 되돌리고 레이더를 놓치면 매크로늄이
    /// 뒷문으로 들어온다.
    /// </summary>
    [Test]
    public void 레이더도_스크랩_계열로_만든다()
    {
        var r = 레시피("radar");
        var ids = 재료ids(r);

        Assert.Contains(스크랩, ids.ToArray(),
            "레이더가 스크랩을 안 먹는다 — 「나가는 값」의 축이 하나여야 한다:\n  " + 재료설명(r));
        Assert.IsEmpty(ids.Where(매크로늄계열).ToList(),
            "레이더가 매크로늄 계열을 요구한다. 회신 ①에서 취소된 항목이다:\n  " + 재료설명(r));
    }

    // ── ② 폐기한 재료는 만들어진 적이 없고, 만들어져서도 안 된다 ──

    /// <summary>
    /// 폐기한 이름들. <b>조각으로 짓는다</b> — 이 파일 자신이 검사에 걸리면 안 되기
    /// 때문이다(<see cref="RenamedNameGateTests"/>와 같은 이유). 파일 하나를 예외로
    /// 두는 편이 읽기는 쉽지만 그 예외가 곧 구멍이 된다.
    ///
    /// <b>「고강도」만 따로 찾지 않는 이유.</b> 이종 합금 설명문이 이미 그 낱말을
    /// 쓴다("성분이 다른 고강도 구조재"). 폐기된 것은 낱말이 아니라
    /// <b>매크로늄의 등급 구분</b>이므로 붙은 꼴로 찾는다.
    /// </summary>
    static readonly string[] 폐기한이름 =
    {
        "겉" + "수정",                  // 새로 세우려던 재료의 한국어 이름
        "macronium_" + "outcrop",       // 그 재료가 가졌을 id
        "고강도 " + "매크로늄",          // 등급 구분의 다른 쪽
    };

    [Test]
    public void 폐기한_이름이_코드에도_데이터에도_표에도_없다()
    {
        var 걸린것 = AssetTextScan.찾는다(폐기한이름);

        Assert.IsEmpty(걸린것,
            $"폐기한 재료·등급의 이름이 {걸린것.Count}군데 있다. " +
            "회신 ①이 그 재료와 매크로늄 등급 구분을 통째로 취소했다 " +
            "(문서의 역사 서술은 훑는 자리 밖이다):\n  " + string.Join("\n  ", 걸린것));
    }

    /// <summary>
    /// <b>음성 확인.</b> 위 검사는 초록불일 때 아무 말도 하지 않는다. 훑개가 조용히
    /// 망가져 늘 빈 목록을 내도 통과한다. 그래서 <b>있는 것은 찾아내는지</b>를 같이
    /// 본다 — 살아 있는 이름 둘(스크랩·매크로늄)은 코드·데이터·표 세 곳에 다 있다.
    /// </summary>
    [Test]
    public void 훑개는_살아_있는_이름을_실제로_찾아낸다()
    {
        foreach (var 산이름 in new[] { 스크랩, 매크로늄 })
        {
            var 찾은것 = AssetTextScan.찾는다(new[] { 산이름 });
            Assert.IsNotEmpty(찾은것,
                $"훑개가 {산이름}조차 못 찾는다면 폐기한 이름 0건은 아무 뜻이 없다");

            foreach (var 갈래 in new[] { "Assets/02.Scripts", "Assets/08.Data",
                                         "Assets/Resources/Localization" })
                Assert.IsTrue(찾은것.Any(자리 => 자리.StartsWith(갈래)),
                    $"{갈래} 아래에서 {산이름}을 못 찾았다. 훑는 자리가 좁아졌다:\n  " +
                    string.Join("\n  ", 찾은것));
        }
    }

    [Test]
    public void 어떤_레시피도_어떤_건축물도_폐기한_재료를_요구하지_않는다()
    {
        var 걸린것 = new List<string>();

        foreach (var r in 제작법들())
            foreach (var id in 재료ids(r))
                if (폐기한재료인가(id)) 걸린것.Add($"레시피 {r.id} <- {id}");

        foreach (var b in 건축물들())
            foreach (var c in b.cost ?? new ItemStack[0])
                if (c?.item != null && 폐기한재료인가(c.item.id)) 걸린것.Add($"건축 {b.id} <- {c.item.id}");

        Assert.IsEmpty(걸린것,
            "폐기한 재료를 요구하는 것이 남아 있다:\n  " + string.Join("\n  ", 걸린것));
    }

    [Test]
    public void 폐기한_재료는_아이템_DB에도_채집_노드에도_없다()
    {
        var db = 아이템DB();
        var 걸린것 = new List<string>();

        foreach (var it in db.items)
        {
            if (it == null) continue;
            if (폐기한재료인가(it.id)) 걸린것.Add($"아이템 {it.id}");
            if (폐기한이름.Any(말 => (it.displayName ?? "").Contains(말)))
                걸린것.Add($"아이템 {it.id}의 이름 \"{it.displayName}\"");
        }

        foreach (var n in 채집노드들())
            if (폐기한이름.Any(말 => (n.displayName ?? "").Contains(말)))
                걸린것.Add($"채집 노드 \"{n.displayName}\"");

        Assert.IsEmpty(걸린것,
            "그 재료는 만들어진 적이 없다. 다시 세우려면 회신 ①부터 뒤집어야 한다:\n  " +
            string.Join("\n  ", 걸린것));
    }

    // ── ③ 매크로늄은 등급이 하나다 ───────────────────────────

    /// <summary>
    /// <b>등급 구분이 없앤 것은 낱말이 아니라 규칙이다.</b> 표면부/안쪽 두 등급은
    /// "무른 쪽은 초반 도구로 깨지고 단단한 쪽은 안 깨진다"는 <b>도구 등급의 축</b>을
    /// 매크로늄 위에 세우자는 제안이었다. 그러니 검사도 이름이 아니라 그 축을 본다 —
    /// 매크로늄이라는 말이 붙은 채집물이 <b>서로 다른 도구·등급을 요구하기 시작하면</b>
    /// 이름을 뭐라 붙였든 등급 구분이 되살아난 것이다.
    ///
    /// 지금 걸리는 것은 매크로늄 석영 하나다 — 곡괭이 1등급. 그래서 이 검사는
    /// 비어 돌지 않는다. (지상에 서 있던 채집물은 2026-08-07에 「합금 더미」로
    /// 개명했다. 매크로늄이 아니라 이종 합금을 떨구는 물건이었다.)
    /// </summary>
    [Test]
    public void 매크로늄은_등급이_하나다()
    {
        var 매크로늄것들 = 채집노드들()
            .Where(n => (n.displayName ?? "").Contains("매크로늄"))
            .ToList();

        Assert.IsNotEmpty(매크로늄것들,
            "매크로늄이 붙은 채집물이 하나도 없다 — 이 검사가 빈 채로 돌고 있다. " +
            "노드 이름이 바뀌었으면 여기도 함께 고쳐야 한다");

        var 등급들 = 매크로늄것들
            .Select(n => (도구: n.requiredTool, 등급: n.requiredTier))
            .Distinct()
            .ToList();

        Assert.AreEqual(1, 등급들.Count,
            "매크로늄이 도구 등급으로 갈렸다. 회신 ①이 두 등급 구분을 없앴다:\n  " +
            string.Join("\n  ", 매크로늄것들.Select(
                n => $"{n.displayName}: {n.requiredTool} tier {n.requiredTier}")));
    }

    /// <summary>
    /// <b>매크로늄 자체는 남는다 — 최종 티어 재료로.</b> 없앤 것은 그 새 재료와 등급
    /// 구분뿐이다. 그래서 이 검사는 매크로늄이 <b>여전히 무언가의 재료</b>인지,
    /// 그리고 그것이 <b>초반 물건이 아닌지</b>를 본다. 매크로늄이 배터리처럼
    /// 초반부터 쓰이기 시작하면 등급을 갈라야 했던 이유가 그대로 돌아온다.
    /// </summary>
    [Test]
    public void 매크로늄은_최종_티어_재료로_남는다()
    {
        var db = 아이템DB();
        Assert.IsNotNull(db.GetById(매크로늄), "매크로늄 아이템이 사라졌다 — 폐기한 것은 표면부 재료뿐이다");

        var 쓰는것 = 제작법들().Where(r => 재료ids(r).Contains(매크로늄)).Select(r => r.id).ToList();

        Assert.IsNotEmpty(쓰는것, "매크로늄으로 만들 수 있는 것이 하나도 없다 — 쓸모 없는 재료가 됐다");
        Assert.AreEqual(new[] { "breach_pod" }, 쓰는것.OrderBy(x => x).ToArray(),
            "매크로늄이 돌파정 말고 다른 것에도 쓰인다. 초반 재료가 되면 등급을 " +
            "갈라야 했던 이유가 돌아온다:\n  " + string.Join(", ", 쓰는것));
    }

    /// <summary>
    /// <b>지상은 스크랩과 기계 부품만이다.</b> 새 물질은 지하부터 — 그 재료를 물 건너
    /// 뭍에 둔 것은 "건너갈 이유가 필요하다"는 걱정에서 나왔는데, 물가에는 이미
    /// 레이더가 있고 그것이 다음 목표를 여는 유일한 물건이므로 이유로 충분하다.
    ///
    /// 배치 자체는 씬의 일이라 여기서 못 본다. 대신 <b>배치할 것이 생겼는지</b>를
    /// 본다 — 매크로늄을 떨구는 채집물이 세계에 생기는 순간 그것은 어딘가에 심긴다.
    /// </summary>
    [Test]
    public void 매크로늄을_떨구는_채집물은_아직_없다()
    {
        var 떨구는것 = 채집노드들()
            .Where(n => n.drops?.entries != null &&
                        n.drops.entries.Any(e => e?.item != null && e.item.id == 매크로늄))
            .Select(n => n.displayName)
            .ToList();

        Assert.IsEmpty(떨구는것,
            "매크로늄을 캘 수 있는 것이 생겼다. 그 자리가 지상이면 회신 ①과 어긋난다 " +
            "(지상은 학습·준비 구간이고 새 물질은 지하부터다):\n  " +
            string.Join("\n  ", 떨구는것));
    }

    // ── ④ 빛의 연료를 알리는 것은 스크랩 하나다 ──────────────

    /// <summary>
    /// 직전 라운드가 세운 게이트("AI가 말했는데 목록이 안 자라는 발견이 없다")의
    /// 짝이다. 그쪽은 <b>말했으면 열려야 한다</b>를 보고, 이쪽은 <b>배터리를 여는
    /// 것이 스크랩의 발견 하나</b>임을 본다.
    ///
    /// 이 배선이 곧 픽션이다. 스크랩을 처음 쥐면 AI가 "에너지로 사용가능한 것으로
    /// 보입니다"라고 말하고, 그 순간 스크랩 재활용이 열리고, 그 설계가 배터리를 연다.
    /// 재료를 매크로늄으로 옮기면 이 세 마디가 전부 거짓말이 된다.
    /// </summary>
    [Test]
    public void 빛의_연료를_알리는_발견은_스크랩_하나다()
    {
        var book = AssetDatabase.LoadAssetAtPath<DiscoveryBookSO>(DiscoveryBookPath);
        Assert.IsNotNull(book, DiscoveryBookPath + "를 못 읽었다");

        var 배터리 = 레시피(배터리셀);
        Assert.IsNotNull(배터리.requiredBlueprint,
            "배터리가 처음부터 열려 있다 — 스크랩의 발견이 여는 것이 없어진다");

        var 여는발견 = book.discoveries
            .Where(d => d?.unlocks != null &&
                        d.unlocks.Any(b => b != null && b.id == 배터리.requiredBlueprint.id))
            .ToList();

        Assert.AreEqual(1, 여는발견.Count,
            $"배터리 설계({배터리.requiredBlueprint.id})를 여는 발견이 하나가 아니다:\n  " +
            string.Join("\n  ", 여는발견.Select(d => d.id)));

        var d0 = 여는발견[0];
        Assert.IsNotNull(d0.item, $"{d0.id}에 물건이 안 걸려 있다");
        Assert.AreEqual(스크랩, d0.item.id,
            $"배터리를 여는 발견이 {d0.item.id}에 걸려 있다. 빛의 연료는 스크랩이다");
        Assert.IsTrue((d0.line?.text ?? "").Contains("에너지"),
            $"스크랩의 발견이 에너지를 말하지 않는다 — \"{d0.line?.text}\". " +
            "스크랩이 「그 자체로 에너지가 되는 물질」이라는 것이 이 결정의 근거다");
    }

    // ── 도구 ─────────────────────────────────────────────────

    static bool 매크로늄계열(string id) =>
        !string.IsNullOrEmpty(id) && id.StartsWith(매크로늄);

    /// <summary>폐기한 재료의 id인가. 이름 조각을 그대로 쓴다.</summary>
    static bool 폐기한재료인가(string id) =>
        !string.IsNullOrEmpty(id) &&
        폐기한이름.Any(말 => id.IndexOf(말, System.StringComparison.OrdinalIgnoreCase) >= 0);

    static IEnumerable<string> 재료ids(RecipeSO r) =>
        (r.ingredients ?? new ItemStack[0])
            .Where(i => i?.item != null)
            .Select(i => i.item.id);

    static string 재료설명(RecipeSO r) =>
        string.Join(" + ", (r.ingredients ?? new ItemStack[0])
            .Select(i => i?.item == null ? "(빈 칸)" : $"{i.item.id}x{i.count}"));

    static ItemDatabaseSO 아이템DB()
    {
        var db = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(ItemDatabasePath);
        Assert.IsNotNull(db, ItemDatabasePath + "를 못 읽었다");
        return db;
    }

    static List<RecipeSO> 제작법들()
    {
        var book = AssetDatabase.LoadAssetAtPath<RecipeBookSO>(RecipeBookPath);
        Assert.IsNotNull(book, RecipeBookPath + "를 못 읽었다");
        return book.recipes.Where(r => r != null).ToList();
    }

    static List<BuildableSO> 건축물들()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<BuildCatalogSO>(BuildCatalogPath);
        Assert.IsNotNull(catalog, BuildCatalogPath + "를 못 읽었다");
        return catalog.entries.Where(b => b != null).ToList();
    }

    /// <summary>
    /// 채집 노드 전부. 목록을 손으로 적지 않는다 — 새로 만든 노드가 검사에
    /// 안 걸리면 폐기한 재료는 <b>새 파일 하나로</b> 되돌아온다.
    /// </summary>
    static List<HarvestNodeSO> 채집노드들()
    {
        var nodes = Directory
            .GetFiles(Path.Combine(Directory.GetCurrentDirectory(), HarvestNodeFolder),
                      "*.asset", SearchOption.AllDirectories)
            .Select(p => p.Substring(Directory.GetCurrentDirectory().Length + 1).Replace('\\', '/'))
            .Select(AssetDatabase.LoadAssetAtPath<HarvestNodeSO>)
            .Where(n => n != null)
            .ToList();

        Assert.IsNotEmpty(nodes, $"{HarvestNodeFolder} 아래에서 채집 노드를 하나도 못 읽었다");
        return nodes;
    }

    static RecipeSO 레시피(string id)
    {
        var r = 제작법들().FirstOrDefault(x => x.id == id);
        Assert.IsNotNull(r, $"{id} 레시피가 제작법 목록에 없다");
        return r;
    }
}
