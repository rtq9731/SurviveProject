using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
/// 켜고 떠나는 것이 아니라, 스스로 지은 돌파정으로 짙어진 매크로늄 구간을 뚫고 내려간다
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

    // ── ③ 폐기된 세계관 (2026-08-06 전면 개정) ──────────────────

    /// <summary>
    /// <b>세계관이 통째로 갈렸다.</b> 2026-08-06 개정으로 무대도 종족도 물성도
    /// 바뀌었는데(<c>Plan/세계관.md</c> §8), 그 표 왼쪽 칸의 낱말들이 저장소 곳곳에
    /// 그대로 남아 있다. 사람 눈으로 훑어 만든 목록은 클론 셋이 동시에 도는 동안
    /// 금세 낡는다 — <b>누가 어디서 되살리든 기계가 잡아야 한다.</b>
    /// 개별 문구를 고치는 것보다 이것이 먼저인 이유가 그것이다.
    ///
    /// <b>낱말은 조각으로 짓는다.</b> 이 파일도 훑는 자리 안에 있다
    /// (<see cref="AssetTextScan.검사범위"/>에 <c>Assets/09.Tests</c>가 있다).
    /// 파일 하나를 예외로 빼는 편이 읽기는 쉽지만, 그 예외가 곧 구멍이 된다.
    ///
    /// <b>갈래로 묶는 이유.</b> 다음 라운드가 갈래 단위로 나뉘어 돌고, 보고도
    /// 갈래별 건수로 나간다. 하나의 평평한 배열이면 "무엇이 몇 건 남았는가"를
    /// 세지 못한다.
    ///
    /// <b>여기 없는 것 셋과 그 이유.</b>
    /// <list type="bullet">
    /// <item><b>「지하」</b> — 폐기된 것이 아니라 <b>목적지</b>다. 어긋난 것은 낱말이
    /// 아니라 초반 무대를 아래로 서술하는 <b>문장</b>인데, 낱말 검사로는 그 둘을
    /// 가를 수 없다. 게다가 두 글자로 찾으면 「감지하며」 같은 멀쩡한 말이 통째로
    /// 걸린다 — 실제로 열매게 도감 문장이 그렇다. 사람이 문장을 보고 고친다(스펙 §2)</item>
    /// <item><b>「큰 육지」(<c>kind_island</c>)</b> — 새 세계관에서도 참이다.
    /// 육지는 몇 개 더 있지만 전부 더 작거나 비슷하다(세계관 §2)</item>
    /// <item><b><c>alien_alloy</c>·<c>IslandZone</c> 같은 식별자</b> — 금지하는 것은
    /// <b>화면에 나가는 표시 이름</b>이지 아이디가 아니다. 아이디까지 잡으면
    /// 열아홉 파일 마흔 자리가 한꺼번에 빨개지고, 그 빨강은 아무것도 알려 주지 않는다</item>
    /// </list>
    /// </summary>
    static readonly (string 갈래, string[] 말들, bool 대소문자를_가린다, int 지금_남은_자리)[]
        폐기된_세계관 =
    {
        // 존재하지 않는다. MARSO를 보낸 것은 지구인이다 (세계관 §4)
        ("행성의 원주민",       new[] { "화성" + "인" },                       false,  0),

        // 지표를 덮은 것은 기상 현상이 아니라 매크로늄 바다다 (세계관 §3)
        // 2026-08-07 3 -> 2. ArtPalette의 포그 색 주석이 폐기된 프롤로그를
        // 가리키고 있었다. 색값은 남고 뜻만 옮겼다
        ("지표를 덮은 기상",     new[] { "모래" + "폭풍", "모래 " + "폭풍" },     false,  2),

        // 스스로 측량하고 골라서 내려앉았다 (세계관 §6). 「비상 …」도 같은 갈래다 —
        // 낱말은 다르지만 폐기된 것은 「떨어져서 도착했다」는 사실 쪽이다
        ("떨어져 부서진 도착",   new[] { "불시" + "착", "격" + "추", "비상 " + "착륙" }, false, 0),

        // 바깥에서 온 것이 아니라 인류의 기반기술과 닮았다 (세계관 §8).
        // 표시 이름만 잡는다 — 아이디 alien_alloy는 살아 있다
        ("바깥에서 온 금속",     new[] { "외계 " + "합금", "외계" + "합금" },     false, 0),

        // 층이 둘로 나뉜 것이 아니라 하나의 농도 구배다 (세계관 §3)
        ("층이 둘이라는 어휘",   new[] { "묽은 " + "층",       "묽은" + "층",
                                        "진한 " + "층",       "진한" + "층",
                                        "짙은 " + "층",       "짙은" + "층",
                                        "묽은 " + "매크로늄", "진한 " + "매크로늄",
                                        "짙은 " + "매크로늄" },                 false,  0),

        // 무대는 지상 한 덩어리 + 호수 + 착륙선이다 (세계관 §8)
        // 2026-08-07 2 -> 1. 위와 같은 자리(ArtPalette). 남은 하나는
        // Interaction/ItemDropper의 거리 주석이다
        ("떠 있는 땅덩이",       new[] { "부유" + "섬", "부유 " + "섬" },         false,  1),

        // 군도가 없으므로 군도의 구역 이름도 없다.
        //
        // <b>41이 아니라 44인 이유.</b> 앞 라운드가 41을 잴 때 채집물 재생과 유물
        // 공급 갈래가 아직 main에 들어오지 않았고, 그 둘이 세 자리를 더하며 병합됐다.
        // 그래서 상한이 세워진 그날 이미 빨갰다 — 2026-08-07에 main 88e49264에서
        // 다시 재어 44로 맞춘다. <b>상한을 올린 것이 아니라 실측으로 되돌린 것이다.</b>
        ("군도 구역 이름",       new[] { "A" + "섬", "B" + "섬" },               false, 43),

        // 라틴 글자 한 자 + 숫자다. 대소문자를 안 가리면 언젠가 delta-1 같은
        // 멀쩡한 말의 꼬리를 문다
        ("군도 구역 번호",       new[] { "A-" + "1", "A-" + "2", "A-" + "3" },   true,  12),
    };

    /// <summary>
    /// <b>오늘부터 막는 것.</b> 저장소에는 아직 폐기어가 남아 있고(스펙 §2가 고친다),
    /// 그것을 지금 빨갛게 두면 함께 도는 다른 갈래들이 <b>자기 게이트를 못 읽는다.</b>
    /// 그래서 "사라졌는가"가 아니라 <b>"늘지 않았는가"</b>를 묻는다.
    ///
    /// <b>이 편이 Ignore보다 낫다.</b> 검사를 통째로 꺼 두면 다음 라운드가 끝날 때까지
    /// 아무것도 막지 못하고, 그동안 세 클론이 새 폐기어를 들여올 수 있다.
    /// 상한을 걸어 두면 <b>오늘 당장</b> 되살리는 것을 잡는다.
    ///
    /// <b>같음이 아니라 이하로 재는 이유.</b> 지금 다른 클론 둘이 채집물과 낫을
    /// 손보고 있고, 그 파일들에도 폐기어가 들어 있다. 정확히 같기를 요구하면
    /// 저쪽이 <b>고쳐서</b> 빨개진다. 줄어드는 것은 언제나 옳다.
    ///
    /// <b>2026-08-07 두 번째로 내렸다.</b> 63 -> 57. 액체 종류 라운드(스펙 §3)가
    /// 자기 파일들을 만지는 김에 <b>「층이 둘이라는 어휘」를 0으로 끝냈다</b> -
    /// <c>World/DescentZone</c>·<c>World/MacroniumSeaService</c> 두 자리가 마지막이었다.
    /// 「지표를 덮은 기상」은 <c>World/OxygenZone</c> 한 자리가 빠져 2가 되었고,
    /// 「군도 구역 이름」은 <c>Domain/World/LiquidCrossing</c>의 주석을 새 무대에 맞춰
    /// 다시 쓰며 한 자리가 빠졌다.
    ///
    /// <b>0이 된 갈래는 이제 진짜 게이트다.</b> 되살리면 그 자리에서 빨개진다.
    ///
    /// <b>2026-08-07 처음 내렸을 때.</b> 130 -> 63. 갈래 셋(행성의 원주민·떨어져
    /// 부서진 도착·바깥에서 온 금속)이 0이 되었고, 남은 것은 <b>둘로 갈린다</b>.
    /// <list type="bullet">
    /// <item><b>다른 클론이 들고 있는 파일</b> — <c>Domain/Art</c>·<c>World/</c>·
    ///       <c>Creatures/</c>. 갈래 넷에 여섯 자리가 거기 있다</item>
    /// <item><b>군도 구역 이름·번호 쉰여섯 자리</b> — 이것은 문구 고치기가 아니라
    ///       <b>스펙 §9(지상 구역 판정)</b>의 일이다. <c>IslandZone</c> 열거값과
    ///       <c>Zone,*</c> 일곱 줄이 살아 있는 코드를 가리키고 있어서, 새 무대가
    ///       정해지기 전에 주석만 고치면 <b>주석이 코드를 두고 거짓말을 한다</b></item>
    /// </list>
    /// </summary>
    [Test]
    public void 폐기된_세계관이_지금보다_더_늘지_않는다()
    {
        var 늘어난것 = new List<string>();

        foreach (var (갈래, 말들, 대소문자를_가린다, 지금) in 폐기된_세계관)
        {
            var 자리들 = 갈래가_걸린_자리(말들, 대소문자를_가린다);
            if (자리들.Count > 지금)
                늘어난것.Add($"[{갈래}] {지금}군데였는데 {자리들.Count}군데가 됐다:\n    " +
                            string.Join("\n    ", 자리들));
            else if (자리들.Count < 지금)
                Debug.Log($"[폐기어] {갈래}가 {지금} -> {자리들.Count}군데로 줄었다. " +
                          "상한도 같이 내려 주면 다음 사람이 되살리는 것을 더 촘촘히 막는다.");
        }

        Assert.IsEmpty(늘어난것,
            "폐기된 세계관이 되살아났다. 2026-08-06에 갈린 설정이고 " +
            "정본은 Plan/세계관.md §8이다 — 새로 쓴 글이 옛 설정을 참고했는지 보라:\n  " +
            string.Join("\n  ", 늘어난것));
    }

    /// <summary>
    /// <b>스펙 §2가 도달해야 할 자리.</b> 지금은 빨갛다. 저장소를 빨간 채로 두면
    /// 함께 도는 다른 갈래들이 <b>자기 게이트를 못 읽으므로</b> 꺼 둔다.
    ///
    /// <b><c>Ignore</c>를 쓰는 이유.</b> <c>Explicit</c>은 이 저장소에서 안 통한다 —
    /// 어셈블리 단위로 훑으면 <c>Explicit</c>도 같이 도는 것을 2026-08-07에 실측했다
    /// (1820개 중 이 하나가 빨갰다). 껐다고 믿었는데 안 꺼진 것이 제일 나쁘다.
    ///
    /// <b>끄는 대신 위에 상한을 걸어 두었다.</b> 그래서 오늘도 되살리는 것은 잡힌다.
    ///
    /// <b>0건이 되는 날 할 일:</b> <c>Ignore</c>를 떼고, 위의 상한 검사를 지운다.
    /// 그때부터는 상한이 아니라 0이 기준이다.
    /// </summary>
    [Test]
    [Ignore("스펙 §2가 문구를 고치기 전까지는 빨갛다. 남은 자리는 폐기된_세계관이_남은_자리를_센다 가 콘솔에 적는다")]
    public void 폐기된_세계관이_저장소에서_사라졌다()
    {
        var 걸린것 = new List<string>();
        foreach (var (갈래, 말들, 대소문자를_가린다, _) in 폐기된_세계관)
            foreach (var 자리 in 갈래가_걸린_자리(말들, 대소문자를_가린다))
                걸린것.Add($"[{갈래}] {자리}");

        Assert.IsEmpty(걸린것,
            $"폐기된 세계관이 {걸린것.Count}군데 남아 있다 (문서의 역사 서술은 예외다):\n  " +
            string.Join("\n  ", 걸린것));
    }

    /// <summary>
    /// <b>보고용.</b> 갈래별로 몇 군데가 남았는지 콘솔에 적는다. 그 수가 곧
    /// 스펙 §2의 작업량이므로 사람이 읽을 수 있는 꼴로 한 번에 뽑을 자리가 필요하다.
    ///
    /// <b>판정하지 않는다.</b> 위의 진짜 게이트는 꺼 두었으니 남은 자리를 알려면
    /// 어딘가는 세어야 하고, 그 자리가 여기다. 늘 초록이므로 평소 훑기에 섞여
    /// 있어도 아무도 막지 않는다.
    /// </summary>
    [Test]
    public void 폐기된_세계관이_남은_자리를_센다()
    {
        var 줄 = new List<string> { "갈래\t자리\t파일" };
        var 모두 = new HashSet<string>();

        foreach (var (갈래, 말들, 대소문자를_가린다, _) in 폐기된_세계관)
        {
            var 자리들 = 갈래가_걸린_자리(말들, 대소문자를_가린다);
            foreach (var 자리 in 자리들) 모두.Add(자리);

            int 파일수 = 자리들.Select(자리 => 자리.Substring(0, 자리.LastIndexOf(':'))).Distinct().Count();
            줄.Add($"{갈래}\t{자리들.Count}\t{파일수}");

            foreach (var 말 in 말들)
            {
                int n = AssetTextScan.찾는다(new[] { 말 }, 대소문자를_가린다).Count;
                if (n > 0) 줄.Add($"    └ 「{말}」\t{n}");
            }
        }

        줄.Add($"합계\t{모두.Count}");
        Debug.Log("[폐기어 현황]\n" + string.Join("\n", 줄));
    }

    // ── ④ 검사기가 비어 돌지 않는다 (음성 확인) ──────────────────

    /// <summary>
    /// <b>금지어마다 음성 확인.</b> 낱말을 조각으로 지으면 오타 하나가
    /// <b>아무것도 물지 않는 말</b>을 만드는데, 파일을 훑는 검사만으로는 그것이
    /// 0건과 구별되지 않는다 — 게이트는 초록불인 채로 눈이 먼다.
    ///
    /// 그래서 말마다 <b>그 말이 들어 있는 문장</b>과 <b>없는 문장</b>을 만들어
    /// 양쪽을 다 본다. 붙는 것만 보면 빈 문자열도 통과한다.
    /// </summary>
    [Test]
    public void 금지어마다_실제로_무는지_확인한다()
    {
        var 이상한것 = new List<string>();

        foreach (var (갈래, 말들, 대소문자를_가린다, _) in 폐기된_세계관)
        {
            Assert.IsNotEmpty(말들, $"[{갈래}]에 말이 하나도 없다");

            foreach (var 말 in 말들)
            {
                if (string.IsNullOrWhiteSpace(말) || 말.Length < 2)
                {
                    이상한것.Add($"[{갈래}] 「{말}」 — 조각을 잇다 만 것 같다");
                    continue;
                }

                if (!AssetTextScan.걸리는가($"앞말 {말} 뒷말", 말, 대소문자를_가린다))
                    이상한것.Add($"[{갈래}] 「{말}」 — 자기가 들어 있는 문장도 못 잡는다");

                if (AssetTextScan.걸리는가("여기에는 아무 폐기어도 들어 있지 않다", 말, 대소문자를_가린다))
                    이상한것.Add($"[{갈래}] 「{말}」 — 없는 문장까지 잡는다");
            }
        }

        Assert.IsEmpty(이상한것,
            "금지어가 제구실을 못 한다. 조각으로 짓다 실수하면 이렇게 된다:\n  " +
            string.Join("\n  ", 이상한것));
    }

    /// <summary>
    /// <b>디코딩이 실제로 일한다.</b> 이 저장소의 데이터 에셋은 한글을
    /// <c>\uXXXX</c>로 적는다. 평범한 훑개는 <b>한 건도</b> 못 잡고, 그래서
    /// "한글로 찾아 0건"이 아무 뜻도 없다 — 실제로 두 번 헛짚었다.
    ///
    /// 여기서는 <b>같은 파일을 두 번</b> 본다. 원문 그대로에는 없고, 유니코드를
    /// 푼 뒤에는 있고, 훑개의 결과 목록에 그 에셋이 실제로 들어 있다.
    /// 셋을 한꺼번에 보아야 "훑개가 표에서만 찾은 것"과 구별된다.
    /// </summary>
    [Test]
    public void 훑개는_에셋에_유니코드로_숨은_한글을_찾아낸다()
    {
        const string 에셋 = "Assets/08.Data/Items/Scrap.asset";
        const string 숨은말 = "스크랩";

        string 원문 = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), 에셋));

        StringAssert.DoesNotContain(숨은말, 원문,
            $"{에셋}이 한글을 그대로 적고 있다. 이 검사의 전제가 무너졌으니 " +
            "다른 에셋으로 표본을 옮겨라 — 유니코드로 숨은 파일이라야 디코딩을 증명한다");

        StringAssert.Contains(숨은말, AssetTextScan.유니코드를_푼다(원문),
            "유니코드를 풀었는데도 한글이 안 나온다. 푸는 규칙이 망가졌다");

        var 찾은것 = AssetTextScan.찾는다(new[] { 숨은말 });
        Assert.IsTrue(찾은것.Any(자리 => 자리.StartsWith(에셋 + ":")),
            $"훑개가 {에셋} 안의 「{숨은말}」을 못 찾았다. 표에서만 찾고 에셋은 " +
            "통째로 놓치고 있다는 뜻이다:\n  " + string.Join("\n  ", 찾은것));
    }

    /// <summary>
    /// <b>접힌 줄 너머까지 본다.</b> YAML은 긴 문자열을 줄바꿈과 들여쓰기로 접는데,
    /// 접힌 자리를 건너지 못하면 <b>두 낱말짜리 구절이 통째로 사라진다.</b>
    /// 2026-08-07에 재 보니 폐기어 두 건이 정확히 그렇게 숨어 있었고, 그중 하나는
    /// 스펙이 콕 집어 고치라고 적어 둔 프롤로그 자막이었다.
    ///
    /// <b>표본을 손으로 적지 않는다.</b> 접힌 자리는 에셋을 다시 저장할 때마다
    /// 옮겨 다니므로, 특정 파일의 특정 구절을 적어 두면 그 문구가 바뀌는 날
    /// 검사가 조용히 무의미해진다. 그래서 <b>지금 접혀 있는 구절을 직접 찾아</b>
    /// 그것으로 확인한다.
    /// </summary>
    [Test]
    public void 훑개는_YAML이_접어_놓은_구절도_찾아낸다()
    {
        var (에셋, 접힌구절) = 접힌_구절을_하나_찾는다();
        Assert.IsNotNull(에셋,
            "08.Data 어디에도 접힌 한글 구절이 없다. 접기 처리가 정말 필요 없어졌는지 " +
            "확인하고, 그렇다면 AssetTextScan에서 접기를 걷어내라");

        string 푼것 = AssetTextScan.유니코드를_푼다(
            File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), 에셋)));
        StringAssert.DoesNotContain(접힌구절, 푼것,
            "표본이 접혀 있지 않다. 이 검사의 전제가 무너졌다");

        var 찾은것 = AssetTextScan.찾는다(new[] { 접힌구절 });
        Assert.IsTrue(찾은것.Any(자리 => 자리.StartsWith(에셋 + ":")),
            $"훑개가 {에셋}에 접혀 있는 「{접힌구절}」을 못 찾았다. " +
            "두 낱말짜리 폐기어는 전부 이렇게 샌다");
    }

    /// <summary>
    /// <b>살아 있는 개념은 걸리지 않는다.</b> 잘못 잡는 게이트는 없느니만 못하다 —
    /// 아이디까지 무는 검사는 저장소를 통째로 빨갛게 만들고, 그 빨강은
    /// 아무것도 알려 주지 않는다.
    ///
    /// 「큰 육지」는 새 세계관에서도 참이고(육지는 몇 개 더 있다),
    /// <c>alien_alloy</c>·<c>IslandZone</c>은 화면에 나가지 않는 식별자이며,
    /// 「지하」는 폐기어가 아니라 목적지다.
    /// </summary>
    [Test]
    public void 살아_있는_개념은_폐기어에_걸리지_않는다()
    {
        var 참인문장 = new[]
        {
            "case RadarContactKind.Island: return \"kind_island\";",
            "Radar,kind_island,큰 육지,Large landmass,결과 화면에 적히는 이름",
            "Assert.IsTrue(재료.ContainsKey(\"alien_alloy\"));",
            "public enum IslandZone { Unknown, Landing, Shoal }",
            "잘리고 남은 절반인 지하로 내려간다",
            "레이더가 지하 구조물을 짚어낸다",
            "주변을 감지하며 먹이를 찾는다",
        };

        var 오탐 = new List<string>();
        foreach (var 문장 in 참인문장)
            foreach (var (갈래, 말들, 대소문자를_가린다, _) in 폐기된_세계관)
                foreach (var 말 in 말들)
                    if (AssetTextScan.걸리는가(문장, 말, 대소문자를_가린다))
                        오탐.Add($"[{갈래}] 「{말}」이 참인 문장을 물었다: \"{문장}\"");

        Assert.IsEmpty(오탐,
            "살아 있는 개념이 폐기어에 걸린다. 금지어를 좁히거나 갈래를 나눠라:\n  " +
            string.Join("\n  ", 오탐));
    }

    /// <summary>
    /// 위 검사의 짝. <b>살아 있는 식별자가 실제로 저장소에 있다</b>는 것까지 본다 —
    /// 아이디가 이미 사라졌다면 "안 걸린다"는 말은 아무 뜻이 없다.
    /// </summary>
    [Test]
    public void 살아_있는_식별자는_저장소에_그대로_있다()
    {
        foreach (var 살아있는것 in new[] { "alien_alloy", "kind_island", "IslandZone" })
            Assert.IsNotEmpty(AssetTextScan.찾는다(new[] { 살아있는것 }),
                $"{살아있는것}이 저장소에서 사라졌다. 표시 이름만 고치기로 한 " +
                "약속이 깨졌거나, 훑개가 눈이 멀었다");
    }

    // ── 도구 ────────────────────────────────────────────────

    /// <summary>
    /// 한 갈래의 말들이 걸린 자리. 같은 줄에 두 말이 있으면 한 자리로 센다 —
    /// 고치는 사람이 가야 할 곳의 수가 곧 작업량이기 때문이다.
    /// </summary>
    static List<string> 갈래가_걸린_자리(string[] 말들, bool 대소문자를_가린다) =>
        AssetTextScan.찾는다(말들, 대소문자를_가린다).Distinct().OrderBy(자리 => 자리).ToList();

    /// <summary>
    /// 지금 08.Data에서 실제로 접혀 있는 한글 두 낱말을 하나 집어 온다.
    /// 돌려주는 구절은 <b>공백 하나로 이은 꼴</b>이라, 접기를 되돌리지 않는
    /// 훑개에게는 보이지 않는다.
    /// </summary>
    static (string 에셋, string 구절) 접힌_구절을_하나_찾는다()
    {
        string 뿌리 = Directory.GetCurrentDirectory();
        var 접힘 = new Regex(@"([가-힣]{2,})\n[ \t]+([가-힣]{2,})");

        foreach (var 파일 in Directory
                     .GetFiles(Path.Combine(뿌리, "Assets/08.Data"), "*.asset",
                               SearchOption.AllDirectories)
                     .OrderBy(p => p))
        {
            string 푼것 = AssetTextScan.유니코드를_푼다(File.ReadAllText(파일));
            var m = 접힘.Match(푼것);
            if (!m.Success) continue;

            string 구절 = m.Groups[1].Value + " " + m.Groups[2].Value;
            if (푼것.Contains(구절)) continue;   // 다른 자리에 펴진 채로도 있으면 증명이 안 된다

            return (파일.Substring(뿌리.Length + 1).Replace('\\', '/'), 구절);
        }

        return (null, null);
    }

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
