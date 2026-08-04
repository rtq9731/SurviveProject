using System;
using System.Collections.Generic;
using NUnit.Framework;
using Survive.Building;

/// <summary>
/// PlacementCheckText.Describe()가 PlacementResult 전 값에 대해 답을 갖고 있는지
/// 전수 검사한다.
///
/// P1에서 실제로 났던 결함: enum에 거부 사유를 하나 추가했는데 switch에 arm을
/// 안 넣어서 default("")로 떨어졌고, 화면에는 아무 안내도 안 뜬 채 배치만
/// 막혔다. 사람은 "왜 안 지어지지"만 알 수 있었다.
///
/// 그래서 값을 하나씩 열거하지 않고 Enum.GetValues로 훑는다 — 나중에 enum이
/// 늘어나면 이 테스트가 자동으로 그 값까지 따진다.
/// </summary>
public class PlacementCheckTests
{
    static PlacementResult[] 모든_결과값
        => (PlacementResult[])Enum.GetValues(typeof(PlacementResult));

    [Test]
    public void 모든_결과값에_대해_null이_아닌_설명을_돌려준다()
    {
        foreach (var r in 모든_결과값)
            Assert.IsNotNull(PlacementCheckText.Describe(r), $"{r}의 설명이 null이다");
    }

    [Test]
    public void 성공은_사유가_없으므로_빈_문자열이다()
    {
        Assert.AreEqual("", PlacementCheckText.Describe(PlacementResult.Ok),
            "성공했는데 화면에 사유가 뜨면 안 된다");
    }

    [Test]
    public void 성공을_뺀_모든_결과값이_비어있지_않은_설명을_갖는다()
    {
        var 빠진값 = new List<PlacementResult>();

        foreach (var r in 모든_결과값)
        {
            if (r == PlacementResult.Ok) continue;
            if (string.IsNullOrWhiteSpace(PlacementCheckText.Describe(r))) 빠진값.Add(r);
        }

        Assert.IsEmpty(빠진값,
            "거부 사유에 안내 문구가 없다 — Describe의 switch에 arm을 추가하라: "
            + string.Join(", ", 빠진값));
    }

    [Test]
    public void 결과값마다_설명이_서로_다르다()
    {
        var 본것 = new Dictionary<string, PlacementResult>();

        foreach (var r in 모든_결과값)
        {
            if (r == PlacementResult.Ok) continue;
            string 설명 = PlacementCheckText.Describe(r);

            Assert.IsFalse(본것.ContainsKey(설명),
                $"{r}과 {(본것.ContainsKey(설명) ? 본것[설명].ToString() : "")}의 설명이 같다 "
                + $"(\"{설명}\") — 사유가 둘인데 화면은 하나만 말한다");

            본것[설명] = r;
        }
    }

    [Test]
    public void 정의되지_않은_값은_빈_설명을_돌려준다()
    {
        // 캐스팅으로 만들어 낸 값에는 답이 없다. 예외 대신 빈 문자열이어야
        // UI가 터지지 않는다.
        Assert.AreEqual("", PlacementCheckText.Describe((PlacementResult)9999));
        Assert.AreEqual("", PlacementCheckText.Describe((PlacementResult)(-1)));
    }

    [Test]
    public void 알려진_거부_사유가_그대로_유지된다()
    {
        // 전수 검사가 "비어있지만 않으면 통과"라서, 자주 보는 문구 몇 개는
        // 따로 못박아 둔다. 문구를 고칠 일이 생기면 여기가 먼저 걸린다.
        Assert.AreEqual("놓을 자리가 없다", PlacementCheckText.Describe(PlacementResult.NoSurface));
        Assert.AreEqual("재료가 모자라다", PlacementCheckText.Describe(PlacementResult.NotEnoughResources));
        Assert.AreEqual("붙일 곳이 없다 — 토대부터 놓아라", PlacementCheckText.Describe(PlacementResult.NoAnchor));
    }
}
