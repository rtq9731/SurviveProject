using NUnit.Framework;
using UnityEngine;
using Survive.World;

/// <summary>
/// 발광 버섯 군락의 밝기·재생 규칙. 순수 함수라 씬도 라이트도 없이 검증한다.
///
/// 여기서 지키려는 계약은 둘이다.
/// <b>"딸수록 어두워진다"</b>와 <b>"전부 따야 꺼진다"</b>.
/// 두 번째가 특히 중요하다 — 반쯤 딴 군락이 조용히 포식자에게 열리면
/// 플레이어는 무엇 때문에 죽었는지 알 수 없다.
/// </summary>
public class GlowGroveRuleTests
{
    // ── 밝기 ────────────────────────────────────────────────

    [Test]
    public void 갓이_온전하면_밝기는_1이다()
    {
        Assert.AreEqual(1f, GlowGroveRule.Brightness(3, 3), 1e-5f);
    }

    [Test]
    public void 하나_딸_때마다_비례해서_어두워진다()
    {
        Assert.AreEqual(2f / 3f, GlowGroveRule.Brightness(2, 3), 1e-5f);
        Assert.AreEqual(1f / 3f, GlowGroveRule.Brightness(1, 3), 1e-5f);
    }

    [Test]
    public void 전부_따면_밝기가_0이다()
    {
        Assert.AreEqual(0f, GlowGroveRule.Brightness(0, 3), 1e-5f);
    }

    [Test]
    public void 군락마다_갓_수가_달라도_같은_비율로_읽힌다()
    {
        // 씬의 세 군락은 갓 무더기가 3·4·2로 고르지 않다.
        // 절대 개수가 아니라 비율이 밝기라는 것이 규칙의 내용이다.
        Assert.AreEqual(GlowGroveRule.Brightness(2, 4), GlowGroveRule.Brightness(1, 2), 1e-5f);
    }

    [Test]
    public void 갓이_하나도_없는_군락은_어둡다()
    {
        // 0으로 나누지 않는다. 설치가 어긋나 갓을 하나도 못 붙였을 때
        // NaN이 라이트 세기로 흘러들어 가면 화면 전체가 무너진다.
        Assert.AreEqual(0f, GlowGroveRule.Brightness(0, 0), 1e-5f);
        Assert.AreEqual(0f, GlowGroveRule.Brightness(2, 0), 1e-5f);
    }

    [Test]
    public void 밝기는_0과_1_사이를_벗어나지_않는다()
    {
        Assert.AreEqual(1f, GlowGroveRule.Brightness(5, 3), 1e-5f);
        Assert.AreEqual(0f, GlowGroveRule.Brightness(-1, 3), 1e-5f);
    }

    // ── 켜짐 판정 ───────────────────────────────────────────

    [Test]
    public void 하나라도_남아_있으면_켜져_있다()
    {
        Assert.IsTrue(GlowGroveRule.IsLit(1));
        Assert.IsTrue(GlowGroveRule.IsLit(3));
    }

    [Test]
    public void 마지막_하나를_따야_꺼진다()
    {
        // 낫이 들어올 수 있는 조건이 정확히 이 경계다.
        Assert.IsTrue(GlowGroveRule.IsLit(1));
        Assert.IsFalse(GlowGroveRule.IsLit(0));
    }

    // ── 재생 ────────────────────────────────────────────────

    [Test]
    public void 재생_시간이_지나기_전에는_돌아오지_않는다()
    {
        Assert.IsFalse(GlowGroveRule.HasRegrown(harvestedAt: 100f, now: 279f, regrowSeconds: 180f));
    }

    [Test]
    public void 정확히_재생_시간이_되면_돌아온다()
    {
        // 경계는 안쪽으로 친다 — 감지·사거리 판정과 같은 관례다.
        Assert.IsTrue(GlowGroveRule.HasRegrown(harvestedAt: 100f, now: 280f, regrowSeconds: 180f));
    }

    [Test]
    public void 시간이_더_지나도_돌아온_것은_그대로다()
    {
        Assert.IsTrue(GlowGroveRule.HasRegrown(harvestedAt: 0f, now: 10000f, regrowSeconds: 180f));
    }

    [Test]
    public void 재생_시간이_0이면_곧바로_돌아온다()
    {
        Assert.IsTrue(GlowGroveRule.HasRegrown(harvestedAt: 5f, now: 5f, regrowSeconds: 0f));
    }

    [Test]
    public void 남은_시간은_흐른_만큼_줄어든다()
    {
        Assert.AreEqual(180f, GlowGroveRule.RegrowRemaining(100f, 100f, 180f), 1e-4f);
        Assert.AreEqual(80f, GlowGroveRule.RegrowRemaining(100f, 200f, 180f), 1e-4f);
    }

    [Test]
    public void 남은_시간은_음수가_되지_않는다()
    {
        // 프롬프트에 그대로 찍히는 값이다. "-42초 남음"이 뜨면 안 된다.
        Assert.AreEqual(0f, GlowGroveRule.RegrowRemaining(100f, 9999f, 180f), 1e-4f);
    }

    // ── 정한 값 ─────────────────────────────────────────────

    [Test]
    public void 재생은_랜턴_한_통보다_길다()
    {
        // 랜턴은 완충 100에 초당 1씩 닳는다(실측) — 한 통에 100초.
        // 재생이 그보다 짧으면 "꺼진 군락 옆에서 랜턴 켜고 기다리기"가 정답이 되고,
        // 군락을 비운 대가가 사라진다.
        const float 랜턴한통초 = 100f;
        Assert.Greater(GlowGroveRule.RegrowSeconds, 랜턴한통초);
    }

    [Test]
    public void 재생은_영구가_아니다()
    {
        // 한 번의 탐사 왕복 안에 돌아온다. 십 분짜리가 되면 사실상 영구 소멸이라
        // 플레이어는 군락에 손대지 않는 쪽을 고르고, 이 기능은 없는 것이 된다.
        Assert.Less(GlowGroveRule.RegrowSeconds, 600f);
    }
}
