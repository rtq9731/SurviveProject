using NUnit.Framework;
using Survive.Creatures;

/// <summary>
/// ⑥ 낫 몸통의 상시 자홍 발광 정리 — 꼬리 세 자세가 화면에서 갈리게 만드는 규칙.
///
/// <b>재는 것은 "몸통이 꼬리를 이기지 않는가"다.</b> 몸통이 상시로 열두 군데를 태우고
/// 있으면 꼬리가 무슨 자세를 잡아도 총량이 거의 같고, 그때 상태 표시등이 꼬리 하나라는
/// 설계가 화면에서는 성립하지 않는다.
/// </summary>
public class ScytheBodyEmissionTests
{
    // 프리팹에서 Consumer_Blade를 쓰는 부품 열둘 (꼬리의 TailSpike는 뺀 것).
    static readonly string[] 몸통부품 =
    {
        "Jaw", "Fang", "FinL", "FinTipL", "FinR", "FinTipR",
        "ScytheL", "ScytheTipL", "ScytheR", "ScytheTipR", "ClawL", "ClawR",
    };

    /// <summary>정리 전 값. Consumer_Blade의 발광은 자홍 × 2.2였다.</summary>
    const float 정리전 = 2.2f;

    [Test]
    public void 끝단만_잔광을_남긴다()
    {
        Assert.IsTrue(ScytheBodyEmission.KeepsLine("ScytheTipL"));
        Assert.IsTrue(ScytheBodyEmission.KeepsLine("ScytheTipR"));
        Assert.IsTrue(ScytheBodyEmission.KeepsLine("FinTipL"));
        Assert.IsTrue(ScytheBodyEmission.KeepsLine("FinTipR"));
    }

    [Test]
    public void 몸통의_나머지는_검다()
    {
        Assert.IsFalse(ScytheBodyEmission.KeepsLine("ScytheL"));
        Assert.IsFalse(ScytheBodyEmission.KeepsLine("FinR"));
        Assert.IsFalse(ScytheBodyEmission.KeepsLine("Jaw"));
        Assert.IsFalse(ScytheBodyEmission.KeepsLine("Fang"));
        Assert.IsFalse(ScytheBodyEmission.KeepsLine("ClawL"));
    }

    [Test]
    public void 완전히_끄지는_않는다()
    {
        // 환경광 0인 세계에서 전부 끄면 낫은 검은 실루엣이 되고 금속 재질이 안 읽힌다.
        float 합 = 0f;
        foreach (var p in 몸통부품) 합 += ScytheBodyEmission.LevelFor(p);
        Assert.Greater(합, 0f, "몸통이 통째로 꺼졌다");
    }

    [Test]
    public void 남는_것은_소수다()
    {
        int 남는수 = 0;
        foreach (var p in 몸통부품) if (ScytheBodyEmission.KeepsLine(p)) 남는수++;

        Assert.AreEqual(4, 남는수);
        Assert.Less(남는수, 몸통부품.Length / 2, "「응축된 소수의 라인」이 아니다");
    }

    [Test]
    public void 몸통_총량이_한_자릿수_배로_줄었다()
    {
        float 전 = 몸통부품.Length * 정리전;

        float 후 = 0f;
        foreach (var p in 몸통부품) 후 += ScytheBodyEmission.LevelFor(p);

        Assert.Less(후 * 10f, 전, $"정리 전 {전:F1} → 후 {후:F1}");
    }

    [Test]
    public void 몸통_잔광은_꼬리의_가장_어두운_자세보다도_약하다()
    {
        // 이것이 "꼬리가 상태 표시등"이라는 말의 수치다. 공격 태세의 호(0.1)는
        // 꼬리에서 가장 어두운 값인데, 그때조차 몸통 한 줄이 그보다 세면
        // 눈은 꼬리가 아니라 몸통을 따라간다.
        float 가장어두운_꼬리 = ScytheStance.ArcFurled * 3.2f;   // arcIntensity
        Assert.Less(ScytheBodyEmission.TipLine, 가장어두운_꼬리);
    }

    [Test]
    public void 이름이_없으면_검다()
    {
        Assert.AreEqual(ScytheBodyEmission.Dark, ScytheBodyEmission.LevelFor(null));
        Assert.AreEqual(ScytheBodyEmission.Dark, ScytheBodyEmission.LevelFor(""));
    }
}
