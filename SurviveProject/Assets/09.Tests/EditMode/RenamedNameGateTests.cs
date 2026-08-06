using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Survive.Crafting;
using Survive.Items;
using Survive.Progression;

/// <summary>
/// 옛 이름이 저장소에 남아 있지 않은지 못 박는 게이트.
///
/// <b>왜 필요한가.</b> 2026-08-06에 짙은 매크로늄 층을 뚫고 내려가는 탈것의 이름을
/// <b>돌파정</b>으로 바꾸면서 id까지 함께 바꿨다(<c>breach_craft</c>). 이름만 바꾸고
/// id를 옛것으로 두면 표·에셋·코드가 서로 다른 말을 하게 되고, 그 어긋남은
/// 화면에 아무 오류도 내지 않는다 — 이름은 새것인데 저장된 것은 옛것이다.
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
    /// 찾을 말. <b>조각으로 짓는다</b> — 이 파일 자신이 검사에 걸리면 안 되기 때문이다.
    /// 파일 하나를 예외 목록에 넣는 편이 읽기는 쉽지만, 그 예외가 곧 구멍이 된다.
    ///
    /// 옛 영문 id 하나와 옛 한국어 이름의 앞 두 글자다. 두 글자만 보는 이유는
    /// 파생 낱말(옛 이름의 "…설계", "…구")까지 한 번에 걸리기 때문이다.
    /// </summary>
    static readonly string[] 옛이름 = { "submer" + "sible", "잠" + "항" };

    /// <summary>새 이름. 훑개가 눈을 뜨고 있는지 확인하는 데 쓴다.</summary>
    const string 새id = "breach_craft";

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
            "Assets/08.Data/Recipes/breach_craft.asset");
        var 청사진 = AssetDatabase.LoadAssetAtPath<BlueprintSO>(
            "Assets/08.Data/Progression/Blueprints/bp_breach_craft.asset");
        var 연구 = AssetDatabase.LoadAssetAtPath<ResearchEntrySO>(
            "Assets/08.Data/Progression/Research/res_breach_craft.asset");

        var 없는것 = new List<string>();
        if (아이템 == null) 없는것.Add("Assets/08.Data/Items/돌파정.asset");
        if (레시피 == null) 없는것.Add("Assets/08.Data/Recipes/breach_craft.asset");
        if (청사진 == null) 없는것.Add("Assets/08.Data/Progression/Blueprints/bp_breach_craft.asset");
        if (연구 == null) 없는것.Add("Assets/08.Data/Progression/Research/res_breach_craft.asset");
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
