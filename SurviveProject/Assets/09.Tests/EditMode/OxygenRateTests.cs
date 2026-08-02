using System.Collections.Generic;
using NUnit.Framework;
using Survive.Vitals;

public class OxygenRateTests
{
    class 보정 : IOxygenModifier
    {
        public float 값;
        public float OxygenDeltaPerSecond => 값;
    }

    [Test]
    public void 보정이_없으면_기본_감소율만_적용된다()
    {
        var 결과 = OxygenRate.Calculate(-1.5f, new List<IOxygenModifier>());
        Assert.AreEqual(-1.5f, 결과, 0.0001f);
    }

    [Test]
    public void 회복_지대에_들어가면_그_값이_적용된다()
    {
        var 목록 = new List<IOxygenModifier> { new 보정 { 값 = 5f } };
        Assert.AreEqual(5f, OxygenRate.Calculate(-1.5f, 목록), 0.0001f);
    }

    [Test]
    public void 여러_보정이_겹치면_가장_유리한_값만_쓴다()
    {
        var 목록 = new List<IOxygenModifier>
        {
            new 보정 { 값 = -8f },   // 모래폭풍
            new 보정 { 값 = 5f }     // 버섯 군락
        };
        // 합산(-3)이 아니라 최댓값(5)이어야 한다
        Assert.AreEqual(5f, OxygenRate.Calculate(-1.5f, 목록), 0.0001f);
    }

    [Test]
    public void 기본_감소율보다_불리한_보정도_최댓값_규칙을_따른다()
    {
        var 목록 = new List<IOxygenModifier> { new 보정 { 값 = -8f } };
        Assert.AreEqual(-8f, OxygenRate.Calculate(-1.5f, 목록), 0.0001f);
    }

    [Test]
    public void null_보정은_무시된다()
    {
        var 목록 = new List<IOxygenModifier> { null, new 보정 { 값 = 3f } };
        Assert.AreEqual(3f, OxygenRate.Calculate(-1.5f, 목록), 0.0001f);
    }

    [Test]
    public void null_목록은_기본값을_유지한다()
    {
        Assert.AreEqual(-2f, OxygenRate.Calculate(-2f, null), 0.0001f);
    }

    [Test]
    public void 전부_null인_목록은_기본값을_유지한다()
    {
        var 목록 = new List<IOxygenModifier> { null, null };
        Assert.AreEqual(-2f, OxygenRate.Calculate(-2f, 목록), 0.0001f);
    }
}
