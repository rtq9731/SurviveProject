using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Survive.World;

/// <summary>
/// 거점이 둘이 되었다 — 전진 기지(화톳불)와 본거지(착륙선).
///
/// <b>이 파일의 절반은 회귀선이다.</b> 착륙선을 들이면서 가장 조용히 깨질 수 있는 것이
/// 화톳불의 존재 이유이고, 그것이 깨지면 <b>아무 검사도 실패하지 않은 채</b>
/// "목재 재고가 곧 안전 재고"(기획서 §5.3)라는 축만 사라진다.
/// 그래서 규칙 자체보다 <b>화톳불이 여전히 이기는가</b>를 더 많이 묻는다.
/// </summary>
public class HomeBaseRuleTests
{
    static RespawnAnchor 불(float x, float 붙인시각, bool 타는중 = true)
        => new RespawnAnchor(new Vector3(x, 0f, 0f), 붙인시각, 타는중);

    static HomeBase 배(float x) => new HomeBase(new Vector3(x, 0f, 0f));

    static readonly Vector3 시작 = new Vector3(-100f, 0f, 0f);

    static RespawnSite 고른다(IReadOnlyList<RespawnAnchor> 불들, HomeBase 배, out Vector3 자리)
        => HomeBaseRule.Choose(불들, 배, 시작, out 자리, out _);

    // ── 경계값 셋 ────────────────────────────────────────────

    [Test]
    public void 착륙선과_화톳불이_둘_다_있으면_화톳불이다()
    {
        var 자리 = Vector3.zero;
        var site = 고른다(new List<RespawnAnchor> { 불(10f, 5f) }, 배(0f), out 자리);

        Assert.AreEqual(RespawnSite.ForwardCamp, site);
        Assert.AreEqual(new Vector3(10f, 0f, 0f), 자리);
    }

    [Test]
    public void 화톳불의_불이_꺼져_있으면_착륙선이다()
    {
        // 잿더미는 후보가 아니다(RespawnRule). 예전에는 그때 시작 지점이었고,
        // 이제는 배가 받는다.
        var 자리 = Vector3.zero;
        var site = 고른다(new List<RespawnAnchor> { 불(10f, 99f, 타는중: false) }, 배(0f), out 자리);

        Assert.AreEqual(RespawnSite.Home, site);
        Assert.AreEqual(new Vector3(0f, 0f, 0f), 자리);
    }

    [Test]
    public void 착륙선만_있으면_착륙선이다()
    {
        var 자리 = Vector3.zero;
        var site = 고른다(new List<RespawnAnchor>(), 배(7f), out 자리);

        Assert.AreEqual(RespawnSite.Home, site);
        Assert.AreEqual(new Vector3(7f, 0f, 0f), 자리);
    }

    [Test]
    public void 둘_다_없으면_시작_지점이다()
    {
        // 지상 지형이 아직 안 서서 배를 놓을 자리가 없는 동안에도
        // 오늘과 똑같이 굴러야 한다.
        var 자리 = Vector3.zero;
        var site = 고른다(new List<RespawnAnchor>(), HomeBase.None, out 자리);

        Assert.AreEqual(RespawnSite.StartPoint, site);
        Assert.AreEqual(시작, 자리);
    }

    [Test]
    public void 후보가_null이어도_배가_있으면_배로_간다()
    {
        var 자리 = Vector3.zero;
        var site = HomeBaseRule.Choose(null, 배(3f), 시작, out 자리, out int 첨자);

        Assert.AreEqual(RespawnSite.Home, site);
        Assert.AreEqual(-1, 첨자);
        Assert.AreEqual(new Vector3(3f, 0f, 0f), 자리);
    }

    [Test]
    public void 배가_아직_안_섰으면_없는_것과_같다()
    {
        // 프롤로그처럼 배가 세계에 있으나 거점이 아닌 국면이 있을 수 있다.
        var 아직 = new HomeBase(new Vector3(5f, 0f, 0f), standing: false);

        var 자리 = Vector3.zero;
        Assert.AreEqual(RespawnSite.StartPoint, 고른다(new List<RespawnAnchor>(), 아직, out 자리));
        Assert.AreEqual(시작, 자리);
    }

    [Test]
    public void 기본값_HomeBase는_서_있지_않다()
    {
        // 구조체는 기본값으로 만들어지는 일이 잦다. 그때 배가 서 있다고 답하면
        // 아무도 배를 놓지 않은 세계에서 원점으로 부활한다.
        Assert.IsFalse(default(HomeBase).Standing);
        Assert.IsFalse(HomeBase.None.Standing);
    }

    [Test]
    public void 고른_첨자는_화톳불일_때만_유효하다()
    {
        HomeBaseRule.Choose(new List<RespawnAnchor> { 불(10f, 5f), 불(20f, 50f) }, 배(0f),
                            시작, out _, out int 화톳불첨자);
        Assert.AreEqual(1, 화톳불첨자, "실행부는 첨자로 원래 화톳불을 되찾는다");

        HomeBaseRule.Choose(new List<RespawnAnchor>(), 배(0f), 시작, out _, out int 배첨자);
        Assert.AreEqual(-1, 배첨자, "배로 갈 때 화톳불 첨자를 그대로 두면 엉뚱한 불을 집는다");
    }

    // ── 회귀선 — 화톳불의 역할이 안 죽는다 ────────────────────

    [Test]
    public void 배가_바로_옆에_있어도_타는_불이_이긴다()
    {
        // <b>이것이 이 라운드에서 제일 깨지기 쉬운 한 줄이다.</b>
        // 거리로 재면 호수 근방에서는 화톳불을 아무리 세워도 소용이 없고,
        // 플레이어가 정한 "여기가 내 자리다"를 규칙이 뒤집는다.
        var 죽은자리 = new Vector3(1f, 0f, 0f);
        var 코앞의배 = 배(0f);
        var 멀리있는불 = new List<RespawnAnchor> { 불(500f, 5f) };

        var 자리 = Vector3.zero;
        Assert.AreEqual(RespawnSite.ForwardCamp, 고른다(멀리있는불, 코앞의배, out 자리));
        Assert.AreEqual(new Vector3(500f, 0f, 0f), 자리);

        // 대가가 커지는 쪽을 골랐다는 것까지 못 박는다 — 거리 최적화가 아니다.
        Assert.Greater(HomeBaseRule.Setback(죽은자리, 멀리있는불, 코앞의배, 시작),
                       HomeBaseRule.Setback(죽은자리, new List<RespawnAnchor>(), 코앞의배, 시작));
    }

    [Test]
    public void 목재가_있으면_밀려나는_거리가_줄어든다()
    {
        // "목재 재고가 곧 안전 재고"를 규칙으로 옮긴 것이 이 부등식이다.
        // 원정지에서 죽었을 때, 불을 지켜 두었으면 그 자리 근처에서 일어서고
        // 꺼뜨렸으면 호수 근방의 배까지 통째로 밀려난다.
        var 원정지 = new Vector3(200f, 0f, 0f);
        var 호수의배 = 배(0f);

        var 지킨불 = new List<RespawnAnchor> { 불(195f, 10f) };
        var 꺼진불 = new List<RespawnAnchor> { 불(195f, 10f, 타는중: false) };

        float 지켰을때 = HomeBaseRule.Setback(원정지, 지킨불, 호수의배, 시작);
        float 꺼뜨렸을때 = HomeBaseRule.Setback(원정지, 꺼진불, 호수의배, 시작);

        Assert.AreEqual(5f, 지켰을때, 0.001f);
        Assert.AreEqual(200f, 꺼뜨렸을때, 0.001f);
        Assert.Less(지켰을때, 꺼뜨렸을때, "불을 지킨 값이 없으면 목재를 벨 이유가 없다");
    }

    [Test]
    public void 전진_기지를_더_세울수록_밀려나는_거리가_늘지_않는다()
    {
        // 벌목량이 곧 세울 수 있는 전진 기지 수다(§5.3). 하나 더 세웠는데
        // 오히려 더 멀리 밀려나는 일이 있으면 그 축이 무너진다.
        var 원정지 = new Vector3(300f, 0f, 0f);
        var 호수의배 = 배(0f);

        var 하나도없다 = new List<RespawnAnchor>();
        var 하나 = new List<RespawnAnchor> { 불(100f, 1f) };
        var 둘 = new List<RespawnAnchor> { 불(100f, 1f), 불(250f, 2f) };
        var 셋 = new List<RespawnAnchor> { 불(100f, 1f), 불(250f, 2f), 불(295f, 3f) };

        float a = HomeBaseRule.Setback(원정지, 하나도없다, 호수의배, 시작);
        float b = HomeBaseRule.Setback(원정지, 하나, 호수의배, 시작);
        float c = HomeBaseRule.Setback(원정지, 둘, 호수의배, 시작);
        float d = HomeBaseRule.Setback(원정지, 셋, 호수의배, 시작);

        Assert.LessOrEqual(b, a);
        Assert.LessOrEqual(c, b);
        Assert.LessOrEqual(d, c);
        Assert.AreEqual(5f, d, 0.001f, "가장 나중에 지핀 앞선 기지에서 일어선다");
    }

    [Test]
    public void 배가_생겼다고_불_사이의_우열이_바뀌지_않는다()
    {
        // RespawnRule은 한 글자도 안 바뀐다. 배를 놓아도 "나중에 지핀 불이 이긴다"가
        // 그대로여야 이 라운드가 위 규칙을 건드리지 않았다고 말할 수 있다.
        var 불들 = new List<RespawnAnchor> { 불(10f, 30f), 불(20f, 5f), 불(30f, 12f) };

        var 배없이 = RespawnRule.Choose(불들, 시작);

        var 자리 = Vector3.zero;
        고른다(불들, 배(0f), out 자리);

        Assert.AreEqual(배없이, 자리);
        Assert.AreEqual(new Vector3(10f, 0f, 0f), 자리);
    }

    [Test]
    public void 배가_없던_세계의_답은_한_글자도_안_바뀐다()
    {
        // 배를 놓기 전까지 이 라운드는 게임을 아무것도 바꾸지 않아야 한다.
        var 표본 = new[]
        {
            new List<RespawnAnchor>(),
            new List<RespawnAnchor> { 불(10f, 5f) },
            new List<RespawnAnchor> { 불(10f, 5f, 타는중: false) },
            new List<RespawnAnchor> { 불(10f, 30f), 불(20f, 5f) },
            new List<RespawnAnchor> { 불(10f, 0f), 불(20f, 0f) },
        };

        foreach (var 불들 in 표본)
        {
            var 자리 = Vector3.zero;
            고른다(불들, HomeBase.None, out 자리);
            Assert.AreEqual(RespawnRule.Choose(불들, 시작), 자리);
        }
    }

    [Test]
    public void 밀려나는_거리는_높이를_세지_않는다()
    {
        // 되돌아가는 것은 걸어서 하는 일이다. 절벽 아래에서 죽었다고 해서
        // 대가가 더 커지지는 않는다 — 그 값은 낙하 피해가 이미 받았다.
        var 낮은데서죽음 = new Vector3(10f, -40f, 0f);
        var 배 = new HomeBase(new Vector3(0f, 60f, 0f));

        Assert.AreEqual(10f, HomeBaseRule.Setback(낮은데서죽음, null, 배, 시작), 0.001f);
    }
}
