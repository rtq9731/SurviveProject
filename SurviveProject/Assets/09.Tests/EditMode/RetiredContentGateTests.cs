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
/// 폐기한 것이 정말로 폐기됐는지 못 박는 게이트.
///
/// <b>왜 필요한가.</b> 챕터 1의 종막은 2026-08-05에 바뀌었다 — 남이 놔둔 장치를
/// 켜고 떠나는 것이 아니라, 스스로 지은 돌파정으로 짙은 매크로늄층을 뚫고 내려간다
/// (기획서 §6.2). 그런데 옛 종막의 데이터와 코드는 그대로 살아 있었고, 그 결과
/// <b>플레이어가 처음 배우는 제작법이 죽은 분기 하나뿐</b>이었다. 지운 것을 지웠다고
/// 적어 두지 않으면 다음 사람이 "옛날에 이런 게 있었지" 하며 되살린다.
///
/// <b>두 번째 게이트가 더 중요하다.</b> 옛 종막을 지우면서 그것만 여는 청사진과
/// 그 청사진만 여는 발견도 함께 지웠다. 그 자리에 남을 수 있었던 최악의 상태는
/// <b>AI가 "제작법이 있습니다"라고 말했는데 목록이 자라지 않는 것</b>이다.
/// 화면에는 아무 오류도 뜨지 않고, 플레이어만 거짓말을 듣는다.
/// 그래서 실제 에셋으로 그 상태를 검사한다.
/// </summary>
public class RetiredContentGateTests
{
    const string DiscoveryBookPath = "Assets/08.Data/Progression/Resources/DiscoveryBook.asset";
    const string RecipeBookPath = "Assets/08.Data/Recipes/RecipeBook.asset";
    const string BuildCatalogPath = "Assets/08.Data/Buildables/BuildCatalog.asset";
    const string ItemDatabasePath = "Assets/08.Data/Items/ItemDatabase.asset";

    // ── ① 지운 것이 남아 있지 않다 ────────────────────────────

    /// <summary>
    /// 찾을 말. <b>조각으로 짓는다</b> — 이 파일 자신이 검사에 걸리면 안 되기 때문이다.
    /// 파일 하나를 예외 목록에 넣는 편이 읽기는 쉽지만, 그 예외가 곧 구멍이 된다
    /// (예외 파일에 되살려 두면 게이트가 못 본다).
    /// </summary>
    static readonly string[] 금지어 = { "port" + "al", "포" + "탈" };

    [Test]
    public void 폐기한_종막_장치가_코드에도_데이터에도_씬에도_없다()
    {
        // 훑는 자리와 유니코드 푸는 규칙은 AssetTextScan이 든다 — 같은 훑개를
        // 옛 이름 게이트도 쓰므로, 목록이 하나여야 어느 쪽을 고쳐도 둘 다 강해진다.
        var 걸린것 = AssetTextScan.찾는다(금지어);

        Assert.IsEmpty(걸린것,
            $"폐기한 종막 장치의 흔적이 {걸린것.Count}군데 남아 있다. " +
            "코드·데이터·씬에서는 전부 사라져야 한다 (문서의 역사 서술은 예외다):\n  " +
            string.Join("\n  ", 걸린것));
    }

    [Test]
    public void 아이템_설명문이_폐기한_장치를_가리키지_않는다()
    {
        var db = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(ItemDatabasePath);
        Assert.IsNotNull(db, $"{ItemDatabasePath}를 못 읽었다");

        var 걸린것 = new List<string>();
        foreach (var item in db.items)
        {
            if (item == null) continue;
            foreach (var 글 in new[] { item.displayName, item.description })
            {
                if (string.IsNullOrEmpty(글)) continue;
                if (금지어.Any(말 => 글.IndexOf(말, System.StringComparison.OrdinalIgnoreCase) >= 0))
                    걸린것.Add($"{item.id}: \"{글}\"");
            }
        }

        Assert.IsEmpty(걸린것,
            "아이템 설명문이 이제 없는 것을 가리킨다. 쓰임이 바뀌면 설명도 바뀌어야 한다:\n  " +
            string.Join("\n  ", 걸린것));
    }

    // ── ② AI가 말했으면 목록이 자란다 ─────────────────────────

    /// <summary>
    /// <b>이 게이트가 이 파일의 알맹이다.</b>
    ///
    /// 현장 발견은 AI가 "해당 물질을 사용한 제작법이 있습니다"라고 말하고 끝난다.
    /// 그 말이 참이려면 그 순간 <b>만들 수 있는 것의 목록이 실제로 늘어야</b> 한다.
    /// 청사진만 열고 그 청사진을 요구하는 제작법이 하나도 없으면, AI는 거짓말을
    /// 하고 화면은 아무 신호도 내지 않는다 — 폐기한 종막 열쇠를 지울 때 실제로
    /// 그 상태가 될 뻔했다.
    ///
    /// 판정은 화면이 쓰는 규칙(<see cref="MenuListing.ShouldList(RecipeSO, StationType, UnlockLedger)"/>)을
    /// 그대로 굴린다. 여기서 따로 규칙을 적으면 화면과 어긋나도 초록불이 켜진다.
    /// </summary>
    [Test]
    public void 말을_거는_발견은_반드시_목록을_자라게_한다()
    {
        var book = AssetDatabase.LoadAssetAtPath<DiscoveryBookSO>(DiscoveryBookPath);
        Assert.IsNotNull(book, $"{DiscoveryBookPath}를 못 읽었다");

        var recipes = 제작법들();
        var buildables = 건축물들();

        var 거짓말 = new List<string>();
        foreach (var d in book.discoveries)
        {
            if (d == null) continue;
            bool 말한다 = !string.IsNullOrWhiteSpace(d.line?.text);
            if (!말한다) continue;

            var 전 = new UnlockLedger();
            var 후 = new UnlockLedger();
            foreach (var bp in d.unlocks ?? new BlueprintSO[0])
                if (bp != null) 후.Unlock(bp.id);

            int 전에 = 실리는것(recipes, buildables, 전);
            int 후에 = 실리는것(recipes, buildables, 후);

            if (후에 <= 전에)
                거짓말.Add($"{d.id} — AI가 \"{d.line.text}\"라고 말하는데 " +
                          $"목록은 {전에}줄 그대로다 " +
                          $"(여는 청사진: {청사진이름(d)})");
        }

        Assert.IsEmpty(거짓말,
            "말은 걸었는데 목록이 자라지 않는 발견이 있다. " +
            "대사를 지우든 제작법을 물리든 하나는 해야 한다 — " +
            "쓸모없는 재료를 알리는 대사는 노이즈이고, 열리지 않는 해금 안내는 거짓말이다:\n  " +
            string.Join("\n  ", 거짓말));
    }

    /// <summary>
    /// 발견이 여는 청사진은 실제로 무언가에 물려 있어야 한다.
    /// 위 검사와 겹쳐 보이지만 실패 메시지가 다르다 — 이쪽은 <b>어느 청사진이</b>
    /// 비어 있는지 짚어 준다.
    /// </summary>
    [Test]
    public void 발견이_여는_청사진은_전부_어딘가에_물려_있다()
    {
        var book = AssetDatabase.LoadAssetAtPath<DiscoveryBookSO>(DiscoveryBookPath);
        Assert.IsNotNull(book, $"{DiscoveryBookPath}를 못 읽었다");

        var 쓰이는청사진 = new HashSet<string>(
            제작법들().Where(r => r.requiredBlueprint != null).Select(r => r.requiredBlueprint.id)
            .Concat(건축물들().Where(b => b.requiredBlueprint != null)
                              .Select(b => b.requiredBlueprint.id)));

        var 빈것 = new List<string>();
        foreach (var d in book.discoveries)
        {
            if (d?.unlocks == null) continue;
            foreach (var bp in d.unlocks)
                if (bp != null && !쓰이는청사진.Contains(bp.id))
                    빈것.Add($"{d.id} -> {bp.id}");
        }

        Assert.IsEmpty(빈것,
            "아무것도 열지 않는 청사진을 발견이 열고 있다:\n  " + string.Join("\n  ", 빈것));
    }

    [Test]
    public void 목록에_빈_칸이나_중복이_없다()
    {
        var book = AssetDatabase.LoadAssetAtPath<DiscoveryBookSO>(DiscoveryBookPath);
        Assert.IsNotNull(book, $"{DiscoveryBookPath}를 못 읽었다");

        Assert.IsFalse(book.discoveries.Any(d => d == null),
            "DiscoveryBook에 빈 칸이 있다 — 에셋을 지우고 목록을 안 고쳤을 때 이렇게 된다");

        var ids = book.discoveries.Select(d => d.id).ToList();
        Assert.AreEqual(ids.Count, ids.Distinct().Count(), "발견 id가 겹친다");
        Assert.IsFalse(ids.Any(string.IsNullOrWhiteSpace), "id가 빈 발견이 있다");
    }

    // ── 도구 ────────────────────────────────────────────────

    static int 실리는것(List<RecipeSO> recipes, List<BuildableSO> buildables, UnlockLedger ledger) =>
        recipes.Count(r => MenuListing.ShouldList(r, r.requiredStation, ledger)) +
        buildables.Count(b => MenuListing.ShouldList(b, ledger));

    static List<RecipeSO> 제작법들()
    {
        var book = AssetDatabase.LoadAssetAtPath<RecipeBookSO>(RecipeBookPath);
        Assert.IsNotNull(book, $"{RecipeBookPath}를 못 읽었다");
        return book.recipes.Where(r => r != null).ToList();
    }

    static List<BuildableSO> 건축물들()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<BuildCatalogSO>(BuildCatalogPath);
        Assert.IsNotNull(catalog, $"{BuildCatalogPath}를 못 읽었다");
        return catalog.entries.Where(b => b != null).ToList();
    }

    static string 청사진이름(DiscoverySO d) =>
        d.unlocks == null || d.unlocks.Length == 0
            ? "없음"
            : string.Join(", ", d.unlocks.Where(b => b != null).Select(b => b.id));
}
