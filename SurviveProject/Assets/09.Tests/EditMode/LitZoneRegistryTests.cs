using NUnit.Framework;
using UnityEngine;
using Survive.World;

/// <summary>
/// §4.7/§4.8 후속 — 화톳불을 "조회 가능한 밝은 구역"으로 승격하는 작업의 핵심.
/// LitZoneRegistry는 순수 C# 정적 등록부이므로 MonoBehaviour나 씬 없이 검증한다.
/// </summary>
public class LitZoneRegistryTests
{
    class 가짜광원 : ILitZoneSource
    {
        public Vector3 LitZoneCenter { get; set; }
        public float LitZoneRadius { get; set; }
        public bool IsLit { get; set; } = true;
    }

    [SetUp]
    public void 초기화() => LitZoneRegistry.Clear();

    [TearDown]
    public void 정리() => LitZoneRegistry.Clear();

    [Test]
    public void 반경_안의_위치는_밝다()
    {
        var 광원 = new 가짜광원 { LitZoneCenter = Vector3.zero, LitZoneRadius = 5f, IsLit = true };
        LitZoneRegistry.Register(광원);

        Assert.IsTrue(LitZoneRegistry.IsLit(new Vector3(3f, 0f, 0f)));
    }

    [Test]
    public void 반경_밖의_위치는_어둡다()
    {
        var 광원 = new 가짜광원 { LitZoneCenter = Vector3.zero, LitZoneRadius = 5f, IsLit = true };
        LitZoneRegistry.Register(광원);

        Assert.IsFalse(LitZoneRegistry.IsLit(new Vector3(10f, 0f, 0f)));
    }

    [Test]
    public void 연료가_떨어진_구역은_반경_안이어도_어둡다()
    {
        // IsLit = false는 "연료가 다 탔다"를 흉내낸다 — 배치만으로는 밝다고 치지 않는다.
        var 광원 = new 가짜광원 { LitZoneCenter = Vector3.zero, LitZoneRadius = 5f, IsLit = false };
        LitZoneRegistry.Register(광원);

        Assert.IsFalse(LitZoneRegistry.IsLit(Vector3.zero));
    }

    [Test]
    public void 겹치는_구역은_하나만_켜져_있어도_밝다()
    {
        var 꺼진광원 = new 가짜광원 { LitZoneCenter = new Vector3(0f, 0f, 0f), LitZoneRadius = 5f, IsLit = false };
        var 켜진광원 = new 가짜광원 { LitZoneCenter = new Vector3(2f, 0f, 0f), LitZoneRadius = 5f, IsLit = true };
        LitZoneRegistry.Register(꺼진광원);
        LitZoneRegistry.Register(켜진광원);

        Assert.IsTrue(LitZoneRegistry.IsLit(new Vector3(1f, 0f, 0f)));
    }

    [Test]
    public void 등록되지_않았으면_어디도_밝지_않다()
    {
        Assert.IsFalse(LitZoneRegistry.IsLit(Vector3.zero));
    }

    [Test]
    public void 해제한_소스는_더_이상_영향을_주지_않는다()
    {
        // 철거·비활성화·씬 언로드 모두 Unregister 한 번으로 귀결된다.
        var 광원 = new 가짜광원 { LitZoneCenter = Vector3.zero, LitZoneRadius = 5f, IsLit = true };
        LitZoneRegistry.Register(광원);
        LitZoneRegistry.Unregister(광원);

        Assert.IsFalse(LitZoneRegistry.IsLit(Vector3.zero));
    }

    [Test]
    public void 경계값은_반경_안으로_친다()
    {
        var 광원 = new 가짜광원 { LitZoneCenter = Vector3.zero, LitZoneRadius = 5f, IsLit = true };
        LitZoneRegistry.Register(광원);

        Assert.IsTrue(LitZoneRegistry.IsLit(new Vector3(5f, 0f, 0f)));
    }

    // ── 등 뒤 사각 (기획서 §9) ─────────────────────────────────
    // 낫이 물어보는 창구다. 랜턴처럼 사람을 따라다니되 가운데 두지 않는 광원만
    // 사각을 만들고, 고정 광원은 그 사각을 <b>메운다</b>.

    /// <summary>사람을 따라다니는 광원. 랜턴을 씬 없이 흉내낸다.</summary>
    class 가짜랜턴 : IOffsetLitSource
    {
        public Vector3 LitAnchor { get; set; }
        public Vector3 LitForward { get; set; } = Vector3.forward;
        public float 오프셋 { get; set; } = 3f;
        public float LitZoneRadius { get; set; } = 8f;
        public bool IsLit { get; set; } = true;

        public Vector3 LitZoneCenter => LanternRule.LitCenter(LitAnchor, LitForward, 오프셋);
    }

    [Test]
    public void 등_뒤는_통째로_내준_쪽이다()
    {
        // 뒤로 새는 빛은 사람이 <b>보기</b> 위한 것이지 지키는 것이 아니다
        // (기획서 §5 "랜턴은 앞쪽만 지키므로 등 뒤를 내준다").
        LitZoneRegistry.Register(new 가짜랜턴 { LitAnchor = Vector3.zero });

        Assert.IsTrue(LitZoneRegistry.IsBlindSide(new Vector3(0f, 0f, -6f)), "멀리 뒤");
        Assert.IsTrue(LitZoneRegistry.IsBlindSide(new Vector3(0f, 0f, -2f)), "코앞 뒤");
        Assert.IsFalse(LitZoneRegistry.IsBlindSide(new Vector3(0f, 0f, 30f)), "앞은 멀어도 사각이 아니다");
        Assert.IsFalse(LitZoneRegistry.IsBlindSide(new Vector3(9f, 0f, 0f)), "정확히 옆도 아니다");
    }

    [Test]
    public void 화톳불이_뒤를_채우면_사각이_사라진다()
    {
        // 기획서 §3의 "Beware → Patrol 전이는 고정 조명 접근"을 기하로 말한 것이다.
        LitZoneRegistry.Register(new 가짜랜턴 { LitAnchor = Vector3.zero });
        var 뒤 = new Vector3(0f, 0f, -6f);
        Assert.IsTrue(LitZoneRegistry.IsBlindSide(뒤));

        LitZoneRegistry.Register(new 가짜광원 { LitZoneCenter = 뒤, LitZoneRadius = 5f });
        Assert.IsFalse(LitZoneRegistry.IsBlindSide(뒤), "불을 등지고 섰는데 등 뒤가 여전히 사각이다");
    }

    [Test]
    public void 밀리지_않은_랜턴은_사각을_만들지_않는다()
    {
        // <b>회귀선이다.</b> 오프셋을 0으로 되돌리면 등 뒤가 어두운 것은 그냥
        // 먼 것이고, 빛을 꺼리는 개체는 예전처럼 어느 방향에서도 막혀야 한다.
        LitZoneRegistry.Register(new 가짜랜턴 { LitAnchor = Vector3.zero, 오프셋 = 0f });

        for (float d = 1f; d <= 30f; d += 1f)
            Assert.IsFalse(LitZoneRegistry.IsBlindSide(new Vector3(0f, 0f, -d)), $"등 뒤 {d}m");
    }

    [Test]
    public void 꺼진_랜턴은_사각을_만들지_않는다()
    {
        // 불이 없으면 사방이 어둡다. 그것은 사각이 아니라 그냥 밤이다.
        LitZoneRegistry.Register(new 가짜랜턴 { LitAnchor = Vector3.zero, IsLit = false });
        Assert.IsFalse(LitZoneRegistry.IsBlindSide(new Vector3(0f, 0f, -6f)));
    }

    [Test]
    public void 고정_광원만_있으면_사각이_없다()
    {
        LitZoneRegistry.Register(new 가짜광원 { LitZoneCenter = Vector3.zero, LitZoneRadius = 5f });
        Assert.IsFalse(LitZoneRegistry.IsBlindSide(new Vector3(0f, 0f, -20f)),
            "앞뒤가 없는 광원이 사각을 만들었다");
    }

    [Test]
    public void 사각의_주인을_찾아_방향까지_알려준다()
    {
        // 낫이 재등장 위치를 고를 때(기획서 §3) 판정만으로는 모자란다 —
        // 어디가 뒤인지 알아야 그리로 갈 수 있다.
        LitZoneRegistry.Register(new 가짜랜턴
        {
            LitAnchor = new Vector3(4f, 1f, 2f),
            LitForward = new Vector3(0f, 5f, 1f),   // 위아래는 버려진다
        });

        Assert.IsTrue(LitZoneRegistry.TryGetOffsetSource(out var 자리, out var 앞));
        Assert.AreEqual(new Vector3(4f, 1f, 2f), 자리);
        Assert.AreEqual(Vector3.forward, 앞);
    }

    [Test]
    public void 켜진_것이_없으면_주인도_없다()
    {
        LitZoneRegistry.Register(new 가짜랜턴 { LitAnchor = Vector3.zero, IsLit = false });
        Assert.IsFalse(LitZoneRegistry.TryGetOffsetSource(out _, out _));
    }
}
