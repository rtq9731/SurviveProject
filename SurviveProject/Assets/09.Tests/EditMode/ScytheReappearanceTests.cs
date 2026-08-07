using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Survive.Creatures;

/// <summary>
/// 재등장 위치 선정 (기획서 §4.5, 스펙 §20).
///
/// <b>조건 셋이 전부 필요하다는 것</b>이 여기서 재는 것의 절반이다. 하나씩만 빼 보면
/// 각각이 무엇을 막고 있었는지가 드러난다 — 각도를 빼면 제자리 맴돌이, 빛을 빼면
/// 웅덩이 한가운데 등장, 구간을 빼면 옆으로 도는 것이 보이는 원 그리기.
///
/// 나머지 절반은 <b>자리가 없을 때</b>다. 그때 억지로 옮기면 규칙이 스스로 어긴
/// 자리로 가게 되므로 머무른다.
/// </summary>
public class ScytheReappearanceTests
{
    /// <summary>사람이 선 자리. 각도의 꼭짓점이다.</summary>
    static readonly Vector3 사람 = new Vector3(0f, 51f, 0f);

    /// <summary>낫이 지금 있는 자리 — 사람의 정북(+z) 10m.</summary>
    static readonly Vector3 지금 = new Vector3(0f, 50.7f, 10f);

    static Vector3 방위(float 도, float 거리 = 10f)
    {
        float r = 도 * Mathf.Deg2Rad;
        return 사람 + new Vector3(Mathf.Sin(r) * 거리, -0.3f, Mathf.Cos(r) * 거리);
    }

    static ReappearSpot 자리(float 도, bool 밝음 = false, bool 보임 = false) =>
        new ReappearSpot(방위(도), 밝음, 보임);

    // ── 각도 차 ────────────────────────────────────────────────

    [Test]
    public void 사람을_꼭짓점으로_각을_잰다()
    {
        Assert.AreEqual(0f, ScytheReappearance.TurnDegrees(사람, 지금, 방위(0f)), 0.01f);
        Assert.AreEqual(90f, ScytheReappearance.TurnDegrees(사람, 지금, 방위(90f)), 0.01f);
        Assert.AreEqual(180f, ScytheReappearance.TurnDegrees(사람, 지금, 방위(180f)), 0.01f);
        Assert.AreEqual(90f, ScytheReappearance.TurnDegrees(사람, 지금, 방위(270f)), 0.01f);
    }

    [Test]
    public void 높이는_각도에_끼어들지_않는다()
    {
        // 사람이 고개를 돌리는 각은 수평각이다. 높이를 넣으면 같은 방위에 떠 있기만
        // 해도 각이 커져 조건이 헐거워진다.
        var 같은방위_높이만다름 = new Vector3(0f, 90f, 10f);
        Assert.AreEqual(0f, ScytheReappearance.TurnDegrees(사람, 지금, 같은방위_높이만다름), 0.01f);
    }

    [Test]
    public void 조금_도는_것으로는_자리를_바꾼_것이_아니다()
    {
        Assert.IsFalse(ScytheReappearance.IsAcceptable(사람, 지금, 자리(45f)));
        Assert.IsFalse(ScytheReappearance.IsAcceptable(사람, 지금, 자리(89f)));
        Assert.IsTrue(ScytheReappearance.IsAcceptable(사람, 지금, 자리(90f)));
        Assert.IsTrue(ScytheReappearance.IsAcceptable(사람, 지금, 자리(135f)));
    }

    [Test]
    public void 하한은_부르는_쪽이_정할_수_있다()
    {
        // 튜닝 표에 놓일 값이지 규칙의 상수가 아니다.
        Assert.IsTrue(ScytheReappearance.IsAcceptable(사람, 지금, 자리(45f), minTurnDegrees: 30f));
        Assert.IsFalse(ScytheReappearance.IsAcceptable(사람, 지금, 자리(100f), minTurnDegrees: 150f));
    }

    [Test]
    public void 사람과_겹쳐_방향이_서지_않으면_고르지_않는다()
    {
        // 애매한 자리는 통과시키지 않는다. 0도는 어떤 하한도 넘지 못한다.
        var 사람자리에겹침 = new ReappearSpot(사람);
        Assert.IsFalse(ScytheReappearance.IsAcceptable(사람, 지금, 사람자리에겹침));
        Assert.AreEqual(0f, ScytheReappearance.TurnDegrees(사람, 사람, 방위(180f)), 0.01f);
    }

    // ── 빛 밖 ──────────────────────────────────────────────────

    [Test]
    public void 밝은_자리에는_나타나지_않는다()
    {
        // 나타난 그 프레임에 도로 밀려나면 나타난 적이 없는 것과 같다.
        Assert.IsFalse(ScytheReappearance.IsAcceptable(사람, 지금, 자리(180f, 밝음: true)));
        Assert.IsTrue(ScytheReappearance.IsAcceptable(사람, 지금, 자리(180f, 밝음: false)));
    }

    // ── 이동 구간 비가시 ────────────────────────────────────────

    [Test]
    public void 가는_길이_보이면_그냥_원을_그리는_것이다()
    {
        Assert.IsFalse(ScytheReappearance.IsAcceptable(사람, 지금, 자리(180f, 보임: true)));
    }

    [Test]
    public void 조건_셋이_전부_필요하다()
    {
        // 하나씩만 어긋나게 해도 전부 떨어진다.
        Assert.IsTrue(ScytheReappearance.IsAcceptable(사람, 지금, 자리(180f)));
        Assert.IsFalse(ScytheReappearance.IsAcceptable(사람, 지금, 자리(10f)));
        Assert.IsFalse(ScytheReappearance.IsAcceptable(사람, 지금, 자리(180f, 밝음: true)));
        Assert.IsFalse(ScytheReappearance.IsAcceptable(사람, 지금, 자리(180f, 보임: true)));
    }

    // ── 고르기 ─────────────────────────────────────────────────

    [Test]
    public void 통과한_것_중_가장_크게_도는_자리를_고른다()
    {
        var 후보 = new List<ReappearSpot> { 자리(95f), 자리(180f), 자리(120f) };
        Assert.AreEqual(1, ScytheReappearance.Pick(사람, 지금, 후보));
    }

    [Test]
    public void 떨어진_후보는_아무리_크게_돌아도_고르지_않는다()
    {
        var 후보 = new List<ReappearSpot> { 자리(100f), 자리(180f, 밝음: true), 자리(175f, 보임: true) };
        Assert.AreEqual(0, ScytheReappearance.Pick(사람, 지금, 후보));
    }

    [Test]
    public void 같은_각이면_앞선_쪽이_이긴다()
    {
        // 매 프레임 답이 뒤집히면 낫이 두 자리 사이에서 떤다.
        // 타겟 선정(ThreatSelection)과 같은 문법이다.
        var 후보 = new List<ReappearSpot> { 자리(120f), 자리(240f) };
        Assert.AreEqual(0, ScytheReappearance.Pick(사람, 지금, 후보));
        Assert.AreEqual(ScytheReappearance.Pick(사람, 지금, 후보),
                        ScytheReappearance.Pick(사람, 지금, 후보));
    }

    [Test]
    public void 자리가_하나도_없으면_머무른다()
    {
        // 억지로 옮기면 규칙이 스스로 어긴 자리(빛 안이거나 보이는 경로)로 간다.
        var 전부막힘 = new List<ReappearSpot>
        {
            자리(180f, 밝음: true), 자리(150f, 보임: true), 자리(20f),
        };
        Assert.AreEqual(ScytheReappearance.Stay, ScytheReappearance.Pick(사람, 지금, 전부막힘));
        Assert.AreEqual(0, ScytheReappearance.CountAcceptable(사람, 지금, 전부막힘));
    }

    [Test]
    public void 후보가_아예_없어도_엉키지_않는다()
    {
        Assert.AreEqual(ScytheReappearance.Stay,
                        ScytheReappearance.Pick(사람, 지금, new List<ReappearSpot>()));
        Assert.AreEqual(ScytheReappearance.Stay, ScytheReappearance.Pick(사람, 지금, null));
        Assert.AreEqual(0, ScytheReappearance.CountAcceptable(사람, 지금, null));
    }

    [Test]
    public void 고른_자리는_언제나_조건_셋을_전부_만족한다()
    {
        // 고르는 함수와 검사하는 함수가 갈리지 않는다는 확인.
        // 갈리면 "골라 놓고 조건을 어긴 자리"가 나온다.
        var 후보 = new List<ReappearSpot>
        {
            자리(10f), 자리(95f, 밝음: true), 자리(170f, 보임: true),
            자리(200f), 자리(60f), 자리(300f),
        };

        int i = ScytheReappearance.Pick(사람, 지금, 후보);
        Assert.AreNotEqual(ScytheReappearance.Stay, i);
        Assert.IsTrue(ScytheReappearance.IsAcceptable(사람, 지금, 후보[i]));
    }

    [Test]
    public void 빛이_넓어질수록_고를_자리가_줄어든다()
    {
        // 고정 조명 앞에서 재등장이 막히는 것이 규칙에서 나온다는 확인.
        // 실측은 이 성질을 실제 좌표와 LitZoneRegistry로 다시 확인한다.
        var 어둠 = new List<ReappearSpot>
        {
            자리(100f), 자리(140f), 자리(180f), 자리(220f), 자리(260f),
        };
        Assert.AreEqual(5, ScytheReappearance.CountAcceptable(사람, 지금, 어둠));

        var 절반이밝다 = new List<ReappearSpot>
        {
            자리(100f, 밝음: true), 자리(140f, 밝음: true), 자리(180f),
            자리(220f), 자리(260f, 밝음: true),
        };
        Assert.AreEqual(2, ScytheReappearance.CountAcceptable(사람, 지금, 절반이밝다));

        var 전부밝다 = new List<ReappearSpot>
        {
            자리(100f, 밝음: true), 자리(140f, 밝음: true), 자리(180f, 밝음: true),
            자리(220f, 밝음: true), 자리(260f, 밝음: true),
        };
        Assert.AreEqual(0, ScytheReappearance.CountAcceptable(사람, 지금, 전부밝다));
        Assert.AreEqual(ScytheReappearance.Stay, ScytheReappearance.Pick(사람, 지금, 전부밝다));
    }
}
