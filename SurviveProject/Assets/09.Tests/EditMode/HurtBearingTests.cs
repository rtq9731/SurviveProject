using NUnit.Framework;
using UnityEngine;
using Survive.Combat;
using Survive.Domain.Art;

/// <summary>
/// 피격 방향 신호 (기획서 §9).
///
/// 랜턴이 앞으로 밀려 등 뒤가 사각이 된 뒤로, 사람이 맞는 자리는 대개 보이지 않는
/// 곳이다. 방향을 말해 주지 않으면 그것은 난이도가 아니라 <b>억울함</b>이 된다.
/// 여기서 재는 것은 둘이다 — <b>어느 쪽인지 맞히는가</b>, 그리고
/// <b>못 본 것일수록 크게 말하는가.</b>
/// </summary>
public class HurtBearingTests
{
    static readonly Vector3 나 = Vector3.zero;
    static readonly Vector3 앞 = Vector3.forward;

    static float 각(Vector3 가해자) => HurtBearing.Degrees(앞, 나, 가해자);

    [Test]
    public void 정면에서_맞으면_0도다()
    {
        Assert.AreEqual(0f, 각(Vector3.forward * 5f), 1e-3f);
    }

    [Test]
    public void 오른쪽이_양수_왼쪽이_음수다()
    {
        Assert.AreEqual(90f, 각(Vector3.right * 5f), 1e-3f);
        Assert.AreEqual(-90f, 각(Vector3.left * 5f), 1e-3f);
    }

    [Test]
    public void 등_뒤는_180도_언저리다()
    {
        Assert.AreEqual(180f, Mathf.Abs(각(Vector3.back * 5f)), 1e-3f);
        Assert.AreEqual(135f, 각(new Vector3(1f, 0f, -1f)), 1e-3f);
        Assert.AreEqual(-135f, 각(new Vector3(-1f, 0f, -1f)), 1e-3f);
    }

    [Test]
    public void 높이는_보지_않는다()
    {
        // 위아래는 몸을 돌려 대응할 수 있는 축이 아니다.
        Assert.AreEqual(90f, HurtBearing.Degrees(앞, 나, new Vector3(5f, 40f, 0f)), 1e-3f);
    }

    [Test]
    public void 겹쳐_있으면_방향이_없다()
    {
        Assert.AreEqual(HurtBearing.Unknown, HurtBearing.Degrees(앞, 나, 나), 1e-5f);
        Assert.AreEqual(HurtBearing.Unknown, HurtBearing.Degrees(Vector3.up, 나, Vector3.right), 1e-5f);
    }

    [Test]
    public void 뒤일수록_1에_가깝다()
    {
        Assert.AreEqual(0f, HurtBearing.Behindness(0f), 1e-4f);
        Assert.AreEqual(0.5f, HurtBearing.Behindness(90f), 1e-4f);
        Assert.AreEqual(1f, HurtBearing.Behindness(180f), 1e-4f);
        Assert.AreEqual(HurtBearing.Behindness(120f), HurtBearing.Behindness(-120f), 1e-4f);
    }

    [Test]
    public void 직각에서_이미_화면_끝까지_간다()
    {
        // 정확한 각도를 그리려는 것이 아니라 어느 쪽으로 돌아야 하는가만 전한다.
        Assert.AreEqual(1f, HurtBearing.ScreenSide(90f), 1e-4f);
        Assert.AreEqual(1f, HurtBearing.ScreenSide(179f), 1e-4f);
        Assert.AreEqual(-1f, HurtBearing.ScreenSide(-91f), 1e-4f);
        Assert.AreEqual(0f, HurtBearing.ScreenSide(0f), 1e-4f);
    }

    // ── 화면까지 이어지는가 ────────────────────────────────────

    [Test]
    public void 맞지_않았으면_비네트가_한가운데다()
    {
        var look = PostFxGrade.Evaluate(PostFxState.Default);
        Assert.AreEqual(Vector2.zero, look.VignetteCenterOffset);
    }

    [Test]
    public void 오른쪽에서_맞으면_비네트_중심이_왼쪽으로_간다()
    {
        // 비네트는 중심에서 먼 쪽을 어둡게 한다. 오른쪽을 어둡게 하려면
        // 중심이 왼쪽으로 가야 한다.
        var look = PostFxGrade.Evaluate(
            new PostFxState(false, false, 0f, 0f, 1f, GammaGrade.Neutral, 90f));

        Assert.Less(look.VignetteCenterOffset.x, 0f);
        Assert.AreEqual(0f, look.VignetteCenterOffset.y, 1e-5f);
    }

    [Test]
    public void 등_뒤에서_맞으면_정면보다_세게_조인다()
    {
        var 앞에서 = PostFxGrade.Evaluate(
            new PostFxState(false, false, 0f, 0f, 1f, GammaGrade.Neutral, 0f));
        var 뒤에서 = PostFxGrade.Evaluate(
            new PostFxState(false, false, 0f, 0f, 1f, GammaGrade.Neutral, 180f));

        Assert.Greater(뒤에서.Vignette, 앞에서.Vignette,
            "못 본 쪽에서 맞았는데 화면이 더 크게 말하지 않는다");
    }

    [Test]
    public void 잔향이_가시면_신호도_가신다()
    {
        var look = PostFxGrade.Evaluate(
            new PostFxState(false, false, 0f, 0f, 0f, GammaGrade.Neutral, 180f));

        Assert.AreEqual(Vector2.zero, look.VignetteCenterOffset);
        Assert.AreEqual(PostFxGrade.VignetteBase + PostFxGrade.VignetteDarkBonus,
                        look.Vignette, 1e-4f);
    }

    [Test]
    public void 방향_신호가_화면을_밝히지는_않는다()
    {
        // 이 게임에서 어둠은 지켜야 할 것이다. 신호는 가리는 쪽으로만 낸다.
        foreach (float deg in new[] { -180f, -90f, 0f, 90f, 180f })
        {
            var look = PostFxGrade.Evaluate(
                new PostFxState(false, false, 0f, 0f, 1f, GammaGrade.Neutral, deg));

            Assert.AreEqual(0f, look.PostExposure, 1e-5f, $"{deg}도");
            Assert.GreaterOrEqual(look.Vignette, PostFxGrade.VignetteBase, $"{deg}도");
        }
    }
}
