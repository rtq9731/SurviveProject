using NUnit.Framework;
using Survive.Player;

/// <summary>
/// 백로그 31번 — "높은 데서 떨어지면 아프다"의 규칙부.
///
/// 여기서 지키려는 것은 두 가지다.
/// <b>일상 이동은 절대 아프지 않다</b> — 점프도, 턱도, 완만한 내리막도 0이어야 한다.
/// 이게 무너지면 걷는 것 자체가 벌이 되고, 아무도 그것을 낙하 피해라고 부르지 않는다.
/// <b>깊은 균열은 죽인다</b> — 임계 위로는 속도에 비례해 오르다가 확실히 치명에 닿는다.
/// </summary>
public class FallImpactTests
{
    // ── 일상 이동 (PlayerLocomotion 기준: 중력 9.81, 점프력 2) ──

    [Test]
    public void 가만히_서_있으면_아프지_않다()
    {
        Assert.AreEqual(0f, FallImpact.DamageFor(0f));
    }

    [Test]
    public void 평지_점프는_아프지_않다()
    {
        // jumpPower 2로 뛰면 같은 높이로 내려앉는다 — 착지 속도도 2다.
        Assert.AreEqual(0f, FallImpact.DamageFor(2f));
    }

    [Test]
    public void 흔한_지형_낙차는_아프지_않다()
    {
        // 1m 턱, 2m 언덕, 3m 바위. 섬에서 걷다 마주치는 낙차다.
        foreach (float h in new[] { 1f, 2f, 3f })
            Assert.AreEqual(0f, FallImpact.DamageFor(FallImpact.ImpactSpeedFromHeight(h)),
                            $"{h}m 낙차는 무해해야 한다");
    }

    [Test]
    public void 무해_구간의_끝은_아직_아프지_않다()
    {
        Assert.AreEqual(0f, FallImpact.DamageFor(FallImpact.SafeImpactSpeed));
    }

    [Test]
    public void 무해_구간_안의_어떤_속도도_0이다()
    {
        for (float v = 0f; v <= FallImpact.SafeImpactSpeed; v += 0.5f)
            Assert.AreEqual(0f, FallImpact.DamageFor(v), $"{v} m/s");
    }

    // ── 임계 넘어서기 ──

    [Test]
    public void 임계를_막_넘으면_아주_조금_아프다()
    {
        float d = FallImpact.DamageFor(FallImpact.SafeImpactSpeed + 0.01f);
        Assert.Greater(d, 0f);
        Assert.Less(d, 1f);
    }

    [Test]
    public void 중간_속도에서는_부분_피해다()
    {
        // 무해와 치명의 한가운데(16.5 m/s, 자유낙하 13.9m)는 절반이다.
        float mid = (FallImpact.SafeImpactSpeed + FallImpact.LethalImpactSpeed) * 0.5f;
        Assert.AreEqual(FallImpact.LethalDamage * 0.5f, FallImpact.DamageFor(mid), 0.001f);

        // 죽지는 않는다 — 온전한 체력이라면 살아남아야 부분 피해라 할 수 있다.
        Assert.Less(FallImpact.DamageFor(mid), FallImpact.LethalDamage);
    }

    [Test]
    public void 속도가_빠를수록_더_아프다()
    {
        float prev = -1f;
        for (float v = FallImpact.SafeImpactSpeed; v <= FallImpact.LethalImpactSpeed; v += 0.5f)
        {
            float d = FallImpact.DamageFor(v);
            Assert.GreaterOrEqual(d, prev, $"{v} m/s에서 오히려 덜 아프다");
            prev = d;
        }
    }

    // ── 치명 ──

    [Test]
    public void 치명_속도에서는_온전한_체력도_남지_않는다()
    {
        Assert.AreEqual(FallImpact.LethalDamage,
                        FallImpact.DamageFor(FallImpact.LethalImpactSpeed), 0.001f);
    }

    [Test]
    public void 치명_속도_위로는_더_깎지_않고_잘린다()
    {
        // 이미 죽은 사람에게 더 깎아 봐야 뜻이 없다. 값이 발산하면
        // 방어구·저항 같은 후속 계산이 엉뚱해진다.
        Assert.AreEqual(FallImpact.LethalDamage, FallImpact.DamageFor(60f), 0.001f);
        Assert.AreEqual(FallImpact.LethalDamage, FallImpact.DamageFor(1000f), 0.001f);
        Assert.AreEqual(FallImpact.LethalDamage,
                        FallImpact.DamageFor(float.PositiveInfinity), 0.001f);
    }

    // ── 망가진 입력 ──

    [Test]
    public void 위로_솟는_속도는_낙하가_아니다()
    {
        // 부르는 쪽이 음수를 넘겨도 피해로 뒤집히지 않아야 한다.
        Assert.AreEqual(0f, FallImpact.DamageFor(-12f));
    }

    [Test]
    public void NaN은_피해가_되지_않는다()
    {
        // NaN은 어떤 비교에도 false라, 걸러 두지 않으면 조용히 NaN 피해가 나간다.
        Assert.AreEqual(0f, FallImpact.DamageFor(float.NaN));
    }

    // ── 높이 환산 ──

    [Test]
    public void 무해_임계는_자유낙하_4미터쯤이다()
    {
        // 임계를 미터로 읽을 수 있어야 지형을 놓을 때 판단이 선다.
        Assert.AreEqual(FallImpact.SafeImpactSpeed,
                        FallImpact.ImpactSpeedFromHeight(4.13f), 0.05f);
    }

    [Test]
    public void 치명_임계는_자유낙하_30미터쯤이다()
    {
        Assert.AreEqual(FallImpact.LethalImpactSpeed,
                        FallImpact.ImpactSpeedFromHeight(29.36f), 0.05f);
    }

    [Test]
    public void 떨어지지_않았으면_속도도_없다()
    {
        Assert.AreEqual(0f, FallImpact.ImpactSpeedFromHeight(0f));
        Assert.AreEqual(0f, FallImpact.ImpactSpeedFromHeight(-5f));
    }

    [Test]
    public void 임계는_점프_착지_속도보다_충분히_위에_있다()
    {
        // 이 검사가 이 항목의 존재 이유다. 튜닝하다 임계를 내려도
        // 점프의 세 배 아래로는 못 내려가게 막는다.
        const float 점프착지속도 = 2f;
        Assert.Greater(FallImpact.SafeImpactSpeed, 점프착지속도 * 3f);
        Assert.Greater(FallImpact.LethalImpactSpeed, FallImpact.SafeImpactSpeed);
    }
}
