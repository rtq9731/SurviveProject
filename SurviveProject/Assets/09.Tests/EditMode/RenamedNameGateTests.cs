using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Survive.Crafting;
using Survive.Harvesting;
using Survive.Items;
using Survive.Localization;
using Survive.Progression;

/// <summary>
/// 옛 이름이 저장소에 남아 있지 않은지 못 박는 게이트.
///
/// <b>왜 필요한가.</b> 2026-08-06에 짙은 매크로늄 층을 뚫고 내려가는 탈것의 이름을
/// <b>돌파정</b>으로 바꾸면서 id까지 함께 바꿨다(<c>breach_pod</c>). 이름만 바꾸고
/// id를 옛것으로 두면 표·에셋·코드가 서로 다른 말을 하게 되고, 그 어긋남은
/// 화면에 아무 오류도 내지 않는다 — 이름은 새것인데 저장된 것은 옛것이다.
///
/// <b>이 물건은 이름을 두 번 버렸다.</b> 처음 잡았던 새 id에는 <c>craft</c>가 들어
/// 있었는데, 이 저장소에서 <c>craft</c>는 이미 "제작"이다(<c>CraftingService</c>·
/// <c>craftSeconds</c>·<c>CanCraft</c>). 레시피 파일 이름이 "돌파 제작법"으로도
/// 읽히는 자리가 생겨 하루 만에 다시 바꿨다. 그래서 <see cref="옛이름"/>은
/// <b>쌓이는 목록</b>이다 — 한 번 버린 이름은 다시 들어오면 안 된다.
///
/// <b>왜 이름 검사가 게이트일 만한가.</b> 개명은 스무 군데를 한꺼번에 건드린다.
/// 한 군데를 빠뜨리면 그 자리만 조용히 옛 이름으로 남고, 다음 사람은 그것이
/// 살아 있는 이름인 줄 안다. 사람 눈으로 훑어 "다 바꿨다"고 말하는 것과
/// 기계가 0건이라고 말하는 것은 다르다.
///
/// <b>역사 서술은 예외다.</b> <c>Plan/</c>·<c>docs/</c>는 훑는 자리 밖이다
/// (<see cref="AssetTextScan.검사범위"/>). 거기 남은 옛 이름은 왜 바뀌었는지를
/// 적어 둔 것이고, 그것까지 지우면 개명의 이유가 사라진다.
/// </summary>
public class RenamedNameGateTests
{
    /// <summary>
    /// 버린 이름들. <b>목록은 쌓이기만 한다</b> — 여기서 이름을 빼는 순간 그것은
    /// 다시 들어와도 되는 이름이 된다.
    ///
    /// <b>조각으로 짓는 이유.</b> 이 파일 자신이 검사에 걸리면 안 된다. 파일 하나를
    /// 예외 목록에 넣는 편이 읽기는 쉽지만, 그 예외가 곧 구멍이 된다.
    ///
    /// 한국어 이름은 앞 두 글자만 본다. 파생 낱말(그 이름의 "…설계", "…구")까지
    /// 한 번에 걸리기 때문이다.
    /// </summary>
    static readonly string[] 옛이름 =
    {
        "submer" + "sible",   // 첫 id
        "잠" + "항",           // 첫 한국어 이름
        "breach_" + "craft",  // 두 번째 id. craft가 이 저장소에서 "제작"과 부딪혔다

        // A섬에 서 있던 채집물의 옛 이름(2026-08-07 개명). 여기만은 <b>구절 전체</b>를
        // 본다 — "매크로늄"이라는 낱말 자체는 살아 있는 말이고(매크로늄 석영·매크로늄
        // 아이템), 앞 두 글자만 잡으면 멀쩡한 것이 통째로 걸린다.
        "매크로늄" + " 광맥",
    };

    /// <summary>새 이름. 훑개가 눈을 뜨고 있는지 확인하는 데 쓴다.</summary>
    const string 새id = "breach_pod";

    // ── A섬 채집물의 개명 (2026-08-07 회신 ①) ────────────────────
    //
    // 회신 ①이 A섬에서 매크로늄을 없앴다. A섬은 스크랩과 기계 부품만 나오는
    // 학습·준비 구간이고 새 물질은 B섬부터다. 그런데 A섬 씬에는 매크로늄의
    // 이름을 단 채집물이 열세 곳 서 있었다 — 그것이 실제로 떨구는 것은
    // 매크로늄이 아니라 <b>외계 합금</b>인데도.
    //
    // 물건은 어긋나지 않았고 화면만 어긋났다. 그래서 고친 것도 표시 이름뿐이다.
    // id(<c>ore_vein</c>)와 에셋 파일 이름은 그대로 둔다 — E2E까지 걸린 문자열을
    // 지금 바꾸는 것은 비용 대비 이득이 없다(회신 ⑬과 같은 판단).

    const string 채집물에셋 = "Assets/08.Data/HarvestNodes/OreVein.asset";
    const string 잔해에셋 = "Assets/08.Data/HarvestNodes/Debris.asset";
    const string 새채집물이름 = "합금 더미";
    const string 채집물폴더 = "Assets/08.Data/HarvestNodes";

    /// <summary>
    /// <b>새 이름이 에셋에도 표에도 있고, 조회 경로 끝에서 그것이 나온다.</b>
    ///
    /// 에셋만 고치면 화면은 안 바뀐다 — 표가 있으면 표가 이기기 때문이다
    /// (<c>DataTextGateTests.표에_있으면_표가_이긴다</c>). 그래서 조회 경로를
    /// 실제로 밟아 본다. 초안 갱신 메뉴를 안 돌렸으면 여기서 옛 이름이 나온다.
    /// </summary>
    [Test]
    public void 채집물의_새_이름이_에셋에도_표에도_있다()
    {
        var 노드 = AssetDatabase.LoadAssetAtPath<HarvestNodeSO>(채집물에셋);
        Assert.IsNotNull(노드, 채집물에셋 + "를 못 읽었다");

        Assert.AreEqual(새채집물이름, 노드.displayName,
            "에셋 원문이 새 이름이 아니다. 표가 없는 배포본에서는 이 값이 그대로 화면에 나간다");

        Assert.AreEqual(새채집물이름, DataText.Name(노드),
            "표를 거친 값이 새 이름이 아니다 — Tools/Survive/번역/데이터 에셋 초안 갱신 을 돌렸는가");
    }

    /// <summary>
    /// <b>이름이 서로 달라야 이름이다.</b> 처음 잡았던 후보는 「부품 더미」였는데,
    /// 이미 있는 「기계 잔해」와 결이 너무 가까워 플레이어가 둘을 구별하지 못하고,
    /// 게다가 <c>machine_part</c>(기계 부품)는 <b>그쪽이 떨구는 물건의 이름</b>이다.
    /// 이 검사는 그런 겹침이 다시 들어오는 것을 막는다.
    /// </summary>
    [Test]
    public void 채집물_이름은_서로_겹치지_않는다()
    {
        var 노드들 = 채집노드들();

        var 겹친것 = 노드들
            .GroupBy(n => (n.displayName ?? "").Trim())
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.IsEmpty(겹친것, "채집물 이름이 겹친다:\n  " + string.Join("\n  ", 겹친것));

        var 잔해 = AssetDatabase.LoadAssetAtPath<HarvestNodeSO>(잔해에셋);
        Assert.IsNotNull(잔해, 잔해에셋 + "를 못 읽었다");
        Assert.AreNotEqual(잔해.displayName, 새채집물이름,
            "기계 잔해와 새 이름이 같아졌다 — 둘은 떨구는 것이 다르다 " +
            "(잔해: 스크랩·기계 부품 / 이쪽: 외계 합금)");
    }

    /// <summary>
    /// <b>음성 확인.</b> 옛 이름 0건은 훑개가 죽어 있어도 나온다. 그래서 훑개가
    /// <b>한글을</b> — 그것도 <c>\u...</c>로 감춰진 에셋 본문과 표 양쪽에서 —
    /// 실제로 찾아내는지 같이 본다. 위의 <see cref="훑개는_있는_이름을_실제로_찾아낸다"/>는
    /// 라틴 문자 id로만 확인하므로 유니코드를 푸는 쪽이 망가지면 조용히 눈이 먼다.
    /// </summary>
    [Test]
    public void 훑개는_한글_새_이름도_실제로_찾아낸다()
    {
        var 찾은것 = AssetTextScan.찾는다(new[] { 새채집물이름 });

        Assert.IsNotEmpty(찾은것, $"훑개가 「{새채집물이름}」조차 못 찾는다면 옛 이름 0건은 아무 뜻이 없다");

        foreach (var 갈래 in new[] { "Assets/08.Data", "Assets/Resources/Localization" })
            Assert.IsTrue(찾은것.Any(자리 => 자리.StartsWith(갈래)),
                          $"{갈래} 아래에서 새 이름을 못 찾았다:\n  " + string.Join("\n  ", 찾은것));
    }

    /// <summary>채집 노드 전부. 목록을 손으로 적으면 새로 만든 노드가 검사에서 샌다.</summary>
    static List<HarvestNodeSO> 채집노드들()
    {
        var nodes = Directory
            .GetFiles(Path.Combine(Directory.GetCurrentDirectory(), 채집물폴더),
                      "*.asset", SearchOption.AllDirectories)
            .Select(p => p.Substring(Directory.GetCurrentDirectory().Length + 1).Replace('\\', '/'))
            .Select(AssetDatabase.LoadAssetAtPath<HarvestNodeSO>)
            .Where(n => n != null)
            .ToList();

        Assert.IsNotEmpty(nodes, $"{채집물폴더} 아래에서 채집 노드를 하나도 못 읽었다");
        return nodes;
    }

    [Test]
    public void 옛_이름이_코드에도_데이터에도_표에도_없다()
    {
        var 걸린것 = AssetTextScan.찾는다(옛이름);

        Assert.IsEmpty(걸린것,
            $"옛 이름이 {걸린것.Count}군데 남아 있다. 코드·데이터·표에서는 전부 " +
            "새 이름이어야 한다 (문서의 역사 서술은 예외다):\n  " +
            string.Join("\n  ", 걸린것));
    }

    /// <summary>
    /// <b>음성 확인.</b> 위 검사는 초록불일 때 아무 말도 하지 않는다. 훑개가
    /// 조용히 망가져 늘 빈 목록을 내도 통과한다. 그래서 <b>있는 것은 찾아내는지</b>를
    /// 같이 본다 — 새 id는 코드·데이터·표 세 곳에 모두 있어야 한다.
    /// </summary>
    [Test]
    public void 훑개는_있는_이름을_실제로_찾아낸다()
    {
        var 찾은것 = AssetTextScan.찾는다(new[] { 새id });

        Assert.IsNotEmpty(찾은것, "훑개가 새 id조차 못 찾는다면 옛 이름 0건은 아무 뜻이 없다");

        foreach (var 갈래 in new[] { "Assets/02.Scripts", "Assets/08.Data",
                                     "Assets/Resources/Localization" })
            Assert.IsTrue(찾은것.Any(자리 => 자리.StartsWith(갈래)),
                          $"{갈래} 아래에서 새 id를 못 찾았다. 훑는 자리가 좁아졌다:\n  " +
                          string.Join("\n  ", 찾은것));
    }

    /// <summary>
    /// 에셋 <b>파일 이름</b>과 <b>id</b>가 같이 갔는지 본다. 본문만 고치고 파일 이름을
    /// 옛것으로 두면 위 검사는 통과하지만(파일 이름은 본문이 아니다) 저장소를 여는
    /// 사람은 옛 이름을 먼저 본다.
    /// </summary>
    [Test]
    public void 에셋의_id가_새_이름이다()
    {
        var 아이템 = AssetDatabase.LoadAssetAtPath<ItemDataSO>(
            "Assets/08.Data/Items/돌파정.asset");
        var 레시피 = AssetDatabase.LoadAssetAtPath<RecipeSO>(
            "Assets/08.Data/Recipes/breach_pod.asset");
        var 청사진 = AssetDatabase.LoadAssetAtPath<BlueprintSO>(
            "Assets/08.Data/Progression/Blueprints/bp_breach_pod.asset");
        var 연구 = AssetDatabase.LoadAssetAtPath<ResearchEntrySO>(
            "Assets/08.Data/Progression/Research/res_breach_pod.asset");

        var 없는것 = new List<string>();
        if (아이템 == null) 없는것.Add("Assets/08.Data/Items/돌파정.asset");
        if (레시피 == null) 없는것.Add("Assets/08.Data/Recipes/breach_pod.asset");
        if (청사진 == null) 없는것.Add("Assets/08.Data/Progression/Blueprints/bp_breach_pod.asset");
        if (연구 == null) 없는것.Add("Assets/08.Data/Progression/Research/res_breach_pod.asset");
        Assert.IsEmpty(없는것, "새 이름의 에셋을 못 읽었다 (파일 이름을 같이 바꿨는가):\n  " +
                              string.Join("\n  ", 없는것));

        Assert.AreEqual(새id, 아이템.id);
        Assert.AreEqual(새id, 레시피.id);
        Assert.AreEqual("bp_" + 새id, 청사진.id);
        Assert.AreEqual("res_" + 새id, 연구.id);

        // 화면에 뜨는 이름도 같이 갔는가. 표가 없어도 이 원문이 폴백으로 나간다.
        Assert.AreEqual("돌파정", 아이템.displayName);
        Assert.AreEqual("돌파정", 레시피.displayName);
        Assert.AreEqual("돌파 설계", 청사진.displayName);
    }
}
