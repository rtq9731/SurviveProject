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
}
