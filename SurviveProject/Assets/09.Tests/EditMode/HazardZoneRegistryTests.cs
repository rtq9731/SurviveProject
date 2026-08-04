using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Survive.World;

/// <summary>
/// P2 스펙 §4 "판정은 Domain에" — 위치로 묻는 쪽. LitZoneRegistry와 같은 정적 등록부이므로
/// 씬이나 MonoBehaviour 없이 검증한다.
/// </summary>
public class HazardZoneRegistryTests
{
    class 가짜구역 : IHazardZoneSource
    {
        public Vector3 HazardZoneCenter { get; set; }
        public float HazardZoneRadius { get; set; }
        public EnvironmentHazard Hazard { get; set; }
        public float Magnitude { get; set; }
    }

    static List<GearCapability> 장비(params GearCapability[] 목록) => new List<GearCapability>(목록);

    [SetUp]
    public void 초기화() => HazardZoneRegistry.Clear();

    [TearDown]
    public void 정리() => HazardZoneRegistry.Clear();

    [Test]
    public void 등록된_것이_없으면_어디든_지날_수_있다()
    {
        Assert.IsTrue(HazardZoneRegistry.CanEnter(Vector3.zero, null));
    }

    [Test]
    public void 구역_밖은_맨몸으로_지난다()
    {
        HazardZoneRegistry.Register(new 가짜구역
        {
            HazardZoneCenter = Vector3.zero,
            HazardZoneRadius = 5f,
            Hazard = EnvironmentHazard.Depth,
            Magnitude = 20f
        });

        Assert.IsTrue(HazardZoneRegistry.CanEnter(new Vector3(10f, 0f, 0f), null));
    }

    [Test]
    public void 구역_안은_그_구역이_요구하는_장비가_있어야_지난다()
    {
        HazardZoneRegistry.Register(new 가짜구역
        {
            HazardZoneCenter = Vector3.zero,
            HazardZoneRadius = 5f,
            Hazard = EnvironmentHazard.Depth,
            Magnitude = 20f
        });

        var 안쪽 = new Vector3(3f, 0f, 0f);
        var 판정 = HazardZoneRegistry.Evaluate(안쪽, null);

        Assert.AreEqual(PassageResult.MissingGear, 판정.Result);
        Assert.AreEqual(TraversalGear.Swimming, 판정.RequiredGear);
        Assert.IsTrue(HazardZoneRegistry.CanEnter(안쪽, 장비(new GearCapability(TraversalGear.Swimming, 20f))));
    }

    [Test]
    public void 경계값은_구역_안으로_친다()
    {
        HazardZoneRegistry.Register(new 가짜구역
        {
            HazardZoneCenter = Vector3.zero,
            HazardZoneRadius = 5f,
            Hazard = EnvironmentHazard.MacroniumSurface,
            Magnitude = 30f
        });

        Assert.IsFalse(HazardZoneRegistry.CanEnter(new Vector3(5f, 0f, 0f), null));
        Assert.IsTrue(HazardZoneRegistry.CanEnter(new Vector3(5.001f, 0f, 0f), null));
    }

    [Test]
    public void 겹치는_구역은_하나라도_막으면_막힌다()
    {
        HazardZoneRegistry.Register(new 가짜구역
        {
            HazardZoneCenter = Vector3.zero,
            HazardZoneRadius = 5f,
            Hazard = EnvironmentHazard.Depth,
            Magnitude = 20f
        });
        HazardZoneRegistry.Register(new 가짜구역
        {
            HazardZoneCenter = new Vector3(2f, 0f, 0f),
            HazardZoneRadius = 5f,
            Hazard = EnvironmentHazard.Darkness,
            Magnitude = 12f
        });

        var 겹친자리 = new Vector3(1f, 0f, 0f);
        var 수영만 = 장비(new GearCapability(TraversalGear.Swimming, 20f));

        var 판정 = HazardZoneRegistry.Evaluate(겹친자리, 수영만);
        Assert.AreEqual(EnvironmentHazard.Darkness, 판정.Hazard);
        Assert.IsFalse(판정.CanPass);

        Assert.IsTrue(HazardZoneRegistry.CanEnter(겹친자리, 장비(
            new GearCapability(TraversalGear.Swimming, 20f),
            new GearCapability(TraversalGear.Lantern, 12f))));
    }

    [Test]
    public void 해제한_구역은_더_이상_막지_않는다()
    {
        var 구역 = new 가짜구역
        {
            HazardZoneCenter = Vector3.zero,
            HazardZoneRadius = 5f,
            Hazard = EnvironmentHazard.Gap,
            Magnitude = 18f
        };
        HazardZoneRegistry.Register(구역);
        Assert.IsFalse(HazardZoneRegistry.CanEnter(Vector3.zero, null));

        HazardZoneRegistry.Unregister(구역);
        Assert.IsTrue(HazardZoneRegistry.CanEnter(Vector3.zero, null));
    }

    [Test]
    public void 같은_구역을_두_번_등록해도_한_번만_센다()
    {
        var 구역 = new 가짜구역
        {
            HazardZoneCenter = Vector3.zero,
            HazardZoneRadius = 5f,
            Hazard = EnvironmentHazard.Gap,
            Magnitude = 18f
        };
        HazardZoneRegistry.Register(구역);
        HazardZoneRegistry.Register(구역);
        HazardZoneRegistry.Unregister(구역);

        Assert.IsTrue(HazardZoneRegistry.CanEnter(Vector3.zero, null));
    }

    [Test]
    public void 막는_것이_없는_구역은_등록돼_있어도_지나간다()
    {
        HazardZoneRegistry.Register(new 가짜구역
        {
            HazardZoneCenter = Vector3.zero,
            HazardZoneRadius = 5f,
            Hazard = EnvironmentHazard.None,
            Magnitude = 0f
        });

        Assert.IsTrue(HazardZoneRegistry.CanEnter(Vector3.zero, null));
    }
}
