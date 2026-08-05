using NUnit.Framework;
using UnityEngine;
using Survive.UI;

/// <summary>
/// 커서 옆 쪽지가 놓이는 자리 (아이템 설명 툴팁).
///
/// 이 규칙이 틀리면 화면 밖으로 반쯤 나간 쪽지가 뜨는데, 그건 눈으로만 잡히고
/// 하필 <b>모서리</b>에서만 난다. 네 모서리를 전부 여기서 밟는다 —
/// Unity를 띄우지 않고.
/// </summary>
public class TooltipPlacementTests
{
    static readonly Vector2 화면 = new Vector2(1920f, 1080f);
    static readonly Vector2 쪽지 = new Vector2(360f, 200f);

    const float 여백 = TooltipPlacement.Margin;
    const float 간격 = TooltipPlacement.Gap;

    static Rect 놓는다(Vector2 커서, Vector2 크기) => TooltipPlacement.Place(커서, 크기, 화면);

    static void 화면_안에_있다(Rect r)
    {
        Assert.GreaterOrEqual(r.xMin, 여백 - 0.001f, $"왼쪽으로 넘쳤다: {r}");
        Assert.GreaterOrEqual(r.yMin, 여백 - 0.001f, $"아래로 넘쳤다: {r}");
        Assert.LessOrEqual(r.xMax, 화면.x - 여백 + 0.001f, $"오른쪽으로 넘쳤다: {r}");
        Assert.LessOrEqual(r.yMax, 화면.y - 여백 + 0.001f, $"위로 넘쳤다: {r}");
    }

    // ── 기본 자리 ────────────────────────────────────────────────

    [Test]
    public void 한가운데서는_커서의_오른쪽_아래에_뜬다()
    {
        var 커서 = new Vector2(960f, 540f);
        var r = 놓는다(커서, 쪽지);

        Assert.AreEqual(커서.x + 간격, r.xMin, 0.001f, "오른쪽으로 간격만큼 떨어진다");
        Assert.AreEqual(커서.y - 간격, r.yMax, 0.001f, "커서 아래로 간격만큼 떨어진다");
        화면_안에_있다(r);
    }

    [Test]
    public void 왼쪽_위_모서리에서는_뒤집지_않는다()
    {
        // 오른쪽에도 아래에도 자리가 넉넉하다. 굳이 뒤집으면 눈이 쫓아가지 못한다.
        var 커서 = new Vector2(20f, 1060f);
        var r = 놓는다(커서, 쪽지);

        Assert.AreEqual(커서.x + 간격, r.xMin, 0.001f);
        Assert.AreEqual(커서.y - 간격, r.yMax, 0.001f);
        화면_안에_있다(r);
    }

    // ── 네 모서리에서 접힌다 ──────────────────────────────────────

    [Test]
    public void 오른쪽_끝에서는_왼쪽으로_접힌다()
    {
        var 커서 = new Vector2(1900f, 540f);
        var r = 놓는다(커서, 쪽지);

        Assert.AreEqual(커서.x - 간격, r.xMax, 0.001f, "커서 왼쪽에 놓인다");
        화면_안에_있다(r);
    }

    [Test]
    public void 아래쪽_끝에서는_위로_접힌다()
    {
        var 커서 = new Vector2(960f, 30f);
        var r = 놓는다(커서, 쪽지);

        Assert.AreEqual(커서.y + 간격, r.yMin, 0.001f, "커서 위에 놓인다");
        화면_안에_있다(r);
    }

    [Test]
    public void 오른쪽_아래_모서리에서는_두_방향_다_접힌다()
    {
        var 커서 = new Vector2(1900f, 30f);
        var r = 놓는다(커서, 쪽지);

        Assert.AreEqual(커서.x - 간격, r.xMax, 0.001f);
        Assert.AreEqual(커서.y + 간격, r.yMin, 0.001f);
        화면_안에_있다(r);
    }

    [Test]
    public void 왼쪽_아래_모서리에서는_위로만_접힌다()
    {
        var 커서 = new Vector2(20f, 30f);
        var r = 놓는다(커서, 쪽지);

        Assert.AreEqual(커서.x + 간격, r.xMin, 0.001f, "왼쪽은 자리가 있으니 그대로 오른쪽에 붙는다");
        Assert.AreEqual(커서.y + 간격, r.yMin, 0.001f);
        화면_안에_있다(r);
    }

    [Test]
    public void 오른쪽_위_모서리에서는_왼쪽으로만_접힌다()
    {
        var 커서 = new Vector2(1900f, 1060f);
        var r = 놓는다(커서, 쪽지);

        Assert.AreEqual(커서.x - 간격, r.xMax, 0.001f);
        Assert.AreEqual(커서.y - 간격, r.yMax, 0.001f, "아래는 자리가 있으니 그대로 아래에 붙는다");
        화면_안에_있다(r);
    }

    [Test]
    public void 네_모서리_어디서도_화면을_벗어나지_않는다()
    {
        Vector2[] 모서리 =
        {
            new Vector2(0f, 0f), new Vector2(1920f, 0f),
            new Vector2(0f, 1080f), new Vector2(1920f, 1080f),
        };

        foreach (var 커서 in 모서리)
            화면_안에_있다(놓는다(커서, 쪽지));
    }

    // ── 커서를 가리지 않는다 ──────────────────────────────────────

    [Test]
    public void 접혀도_커서를_덮지_않는다()
    {
        // 덮으면 지금 무엇을 가리키고 있는지가 사라진다.
        Vector2[] 자리 =
        {
            new Vector2(960f, 540f),
            new Vector2(1900f, 540f),
            new Vector2(960f, 30f),
            new Vector2(1900f, 30f),
            new Vector2(20f, 30f),
            new Vector2(1900f, 1060f),
        };

        foreach (var 커서 in 자리)
            Assert.IsFalse(놓는다(커서, 쪽지).Contains(커서), $"커서를 덮었다: {커서}");
    }

    [Test]
    public void 커서와의_간격이_지켜진다()
    {
        var 커서 = new Vector2(960f, 540f);
        var r = 놓는다(커서, 쪽지);

        Assert.AreEqual(간격, r.xMin - 커서.x, 0.001f);
        Assert.AreEqual(간격, 커서.y - r.yMax, 0.001f);
    }

    // ── 너비 ─────────────────────────────────────────────────────

    [Test]
    public void 너비는_최대치를_넘지_않는다()
    {
        Assert.AreEqual(TooltipPlacement.MaxWidth,
                        TooltipPlacement.ClampWidth(2000f, 1920f), 0.001f);
    }

    [Test]
    public void 좁은_화면에서는_화면이_최대치를_이긴다()
    {
        // 창을 줄이거나 세로 화면이면 380조차 넓다.
        Assert.AreEqual(300f - 여백 * 2f,
                        TooltipPlacement.ClampWidth(TooltipPlacement.MaxWidth, 300f), 0.001f);
    }

    [Test]
    public void 원하는_너비가_최대치보다_좁으면_그대로_쓴다()
    {
        Assert.AreEqual(120f, TooltipPlacement.ClampWidth(120f, 1920f), 0.001f);
    }

    [Test]
    public void 너비를_안_정하면_허용된_최대치를_준다()
    {
        Assert.AreEqual(TooltipPlacement.MaxWidth,
                        TooltipPlacement.ClampWidth(0f, 1920f), 0.001f);
    }

    [Test]
    public void 화면보다_넓은_쪽지는_화면_안으로_줄어든다()
    {
        var r = 놓는다(new Vector2(960f, 540f), new Vector2(4000f, 3000f));

        Assert.AreEqual(화면.x - 여백 * 2f, r.width, 0.001f);
        Assert.AreEqual(화면.y - 여백 * 2f, r.height, 0.001f);
        화면_안에_있다(r);
    }

    [Test]
    public void 뒤집을_자리도_없으면_화면_안으로_밀어_넣는다()
    {
        // 좌우 어느 쪽으로도 안 들어가는 큰 쪽지. 커서와 겹치더라도
        // 반쯤 잘려 나가는 것보다는 낫다.
        var r = 놓는다(new Vector2(960f, 540f), new Vector2(1800f, 1000f));
        화면_안에_있다(r);
    }
}
