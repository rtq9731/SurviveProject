using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Survive.Creatures;
using Survive.World;

/// <summary>
/// <b>경계 등급이 개체수를 정한다</b> (기획서 §4.5 "경계 상태" — 평시 1 · 각성 2 · 발령 5).
///
/// <b>여기서 재는 것 중 하나는 「아직 안 정해졌다」다.</b> 각성의 방아쇠 구역은
/// 지형이 선 뒤에 사람이 정하기로 되어 있다. 비어 있다는 사실을 테스트가 값으로
/// 들고 있어야, 정해지는 날 그 테스트가 <b>먼저 빨개져서</b> 방아쇠가 생긴 것이
/// 사고가 아니라 결정이었음을 남긴다.
/// </summary>
public class ScytheCensusTests
{
    static readonly ScytheAlert[] 등급들 = (ScytheAlert[])Enum.GetValues(typeof(ScytheAlert));

    [SetUp]
    public void 되돌린다() => ScytheWatch.Reset();

    [TearDown]
    public void 치운다() => ScytheWatch.Reset();

    // ── 등급 → 개체수 ──────────────────────────────────────────

    [Test]
    public void 등급마다_마릿수가_정해져_있다()
    {
        Assert.AreEqual(1, ScytheCensus.CountFor(ScytheAlert.Calm), "평시");
        Assert.AreEqual(2, ScytheCensus.CountFor(ScytheAlert.Awake), "각성");
        Assert.AreEqual(5, ScytheCensus.CountFor(ScytheAlert.Alarmed), "발령");
    }

    [Test]
    public void 등급이_오르면_수도_오른다()
    {
        // 단조여야 한다. 중간이 더 많으면 "깊이 들어갈수록 위험하다"가 거짓이 된다.
        Assert.Less(ScytheCensus.CountFor(ScytheAlert.Calm),
                    ScytheCensus.CountFor(ScytheAlert.Awake));
        Assert.Less(ScytheCensus.CountFor(ScytheAlert.Awake),
                    ScytheCensus.CountFor(ScytheAlert.Alarmed));
    }

    [Test]
    public void 발령에서_계단이_가팔라진다()
    {
        // 둘에서 다섯으로 뛰는 것이 의도다. 셋·넷을 거치면 종막의 사건이
        // 곡선의 한 점이 된다.
        int 첫걸음 = ScytheCensus.CountFor(ScytheAlert.Awake) -
                     ScytheCensus.CountFor(ScytheAlert.Calm);
        int 둘째걸음 = ScytheCensus.CountFor(ScytheAlert.Alarmed) -
                       ScytheCensus.CountFor(ScytheAlert.Awake);

        Assert.Greater(둘째걸음, 첫걸음, "발령이 각성보다 크게 뛰어야 한다");
    }

    [Test]
    public void 어떤_등급에도_답이_있다()
    {
        foreach (var 등급 in 등급들)
            Assert.Greater(ScytheCensus.CountFor(등급), 0, 등급.ToString());
    }

    [Test]
    public void 모자란_수와_남는_수는_음수가_되지_않는다()
    {
        Assert.AreEqual(4, ScytheCensus.ShortfallUnder(1, ScytheAlert.Alarmed));
        Assert.AreEqual(0, ScytheCensus.ShortfallUnder(5, ScytheAlert.Alarmed));
        Assert.AreEqual(0, ScytheCensus.ShortfallUnder(9, ScytheAlert.Alarmed));

        Assert.AreEqual(4, ScytheCensus.SurplusOver(5, ScytheAlert.Calm));
        Assert.AreEqual(0, ScytheCensus.SurplusOver(1, ScytheAlert.Calm));
        Assert.AreEqual(0, ScytheCensus.SurplusOver(0, ScytheAlert.Calm));
    }

    // ── 월드가 소유하고 개체는 읽기만 한다 ──────────────────────

    [Test]
    public void 개체수는_등급의_함수라_따로_저장되지_않는다()
    {
        // <b>원장을 새로 두지 않은 근거다.</b> 값을 두 군데 두면 "등급은 발령인데
        // 수는 하나"인 상태가 만들어질 수 있고, 그것을 막는 코드를 또 써야 한다.
        foreach (var 등급 in 등급들)
        {
            ScytheWatch.Set(등급);
            Assert.AreEqual(ScytheCensus.CountFor(등급), ScytheWatch.Population, 등급.ToString());
        }

        var 저장하는것 = typeof(ScytheWatch)
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(int))
            .Select(f => f.Name)
            .ToList();
        Assert.IsEmpty(저장하는것, "개체수를 따로 들고 있는 필드: " + string.Join(", ", 저장하는것));
    }

    [Test]
    public void 개체_쪽에는_여전히_등급을_쓰는_API가_없다()
    {
        // 각성이 생겼다고 소유권이 흔들리면 안 된다. 앞 라운드가 세운 선이다.
        Type 몸 = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
             몸 = asm.GetType("Survive.Creatures.HoverDrifter", false);
            if (몸 != null) break;
        }
        Assert.IsNotNull(몸);

        var alert = 몸.GetProperty("Alert", BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(alert);
        Assert.IsNull(alert.SetMethod, "몸에 등급 setter가 생기면 소유권이 갈라진다");
    }

    [Test]
    public void 각성은_서식_범위를_바꾸지_않는다()
    {
        // <b>ScytheAlert가 셋이 된 뒤의 회귀선이다.</b> 각성은 수만 늘리고
        // 어디까지 가는가는 평시와 같아야 한다 — 아래 규칙들이 전부
        // `== Alarmed`만 묻기 때문에 성립한다.
        foreach (HabitatZone 구역 in Enum.GetValues(typeof(HabitatZone)))
        {
            Assert.AreEqual(ScytheHabitat.CanEnter(구역, ScytheAlert.Calm),
                            ScytheHabitat.CanEnter(구역, ScytheAlert.Awake),
                            $"{구역} — 각성이 평시와 다른 곳까지 간다");
        }

        // 시간 축도 마찬가지다. 발령만 낮을 이긴다.
        Assert.AreEqual(ScytheHabitat.IsAbroad(0.5f, ScytheAlert.Calm),
                        ScytheHabitat.IsAbroad(0.5f, ScytheAlert.Awake),
                        "각성이 낮을 이기면 안 된다");
        Assert.IsTrue(ScytheHabitat.IsAbroad(0.5f, ScytheAlert.Alarmed));
    }

    // ── 각성 방아쇠: 아직 비어 있다 ─────────────────────────────

    [Test]
    public void 각성_방아쇠가_아직_정해지지_않았다()
    {
        // <b>이 테스트는 「미결」을 값으로 들고 있는 자리다.</b> 지형이 서고 사람이
        // 구역을 고르는 날, 여기가 먼저 빨개진다. 그때 이 테스트를 고치는 것이
        // 곧 "방아쇠를 정했다"고 기록하는 일이다.
        Assert.IsFalse(ScytheWatch.AwakeningArmed,
                       "방아쇠 구역이 정해졌다 — 정했다면 이 테스트를 함께 고쳐라");
    }

    [Test]
    public void 방아쇠가_비어_있으면_어디를_밟아도_각성하지_않는다()
    {
        // 아무 구역이나 임시로 박아 두면 지형이 서는 날 「왜 여기서 낫이 늘지」를
        // 아무도 설명하지 못한다. 비어 있는 동안에는 아무 일도 없어야 한다.
        foreach (var p in new[] { Vector3.zero, new Vector3(100f, 0f, 100f),
                                  new Vector3(-40f, 5f, 12f) })
        {
            Assert.IsFalse(ScytheWatch.ObserveAt(p), p.ToString());
            Assert.AreEqual(ScytheAlert.Calm, ScytheWatch.Alert);
            Assert.AreEqual(1, ScytheWatch.Population);
        }
    }

    [Test]
    public void 방아쇠를_세우면_한_줄로_동작한다()
    {
        // <b>정해지는 날 고칠 곳이 한 줄이라는 것</b>을 보여 두는 자리다.
        // 구역 볼륨이 없는 지금은 위치로 물을 수 없으므로 판정만 눌러 본다.
        ScytheWatch.ArmAwakening(SurfaceZone.Inland);
        Assert.IsTrue(ScytheWatch.AwakeningArmed);

        ScytheWatch.Reset();
        Assert.IsFalse(ScytheWatch.AwakeningArmed, "되돌리면 다시 비어 있다");
    }

    [Test]
    public void 발령은_각성으로_내려오지_않는다()
    {
        ScytheWatch.Set(ScytheAlert.Alarmed);
        ScytheWatch.ArmAwakening(SurfaceZone.Inland);

        // 방아쇠가 걸려도 발령을 덮지 않는다.
        ScytheWatch.ObserveAt(Vector3.zero);
        Assert.AreEqual(ScytheAlert.Alarmed, ScytheWatch.Alert);
        Assert.AreEqual(5, ScytheWatch.Population);
    }

    // ── 수가 줄 때 누가 남는가 ─────────────────────────────────

    [Test]
    public void 먼_것부터_사라진다()
    {
        // 기획서 §4.5: "한 개체만 코어를 들고 둥지로 향하고, 나머지는 주변으로
        // 흩어져 디스폰한다." 흩어진다는 것은 멀어진다는 뜻이고, 눈앞에서
        // 사라지는 것은 흩어지는 것이 아니라 지워지는 것이다.
        var 거리 = new List<float> { 5f, 30f, 12f, 40f, 1f };

        var 물릴것 = ScytheCensus.PickDespawn(거리, 3);

        CollectionAssert.AreEqual(new[] { 3, 1, 2 }, 물릴것, "40 · 30 · 12 순서");
    }

    [Test]
    public void 가장_가까운_것이_남는다()
    {
        var 거리 = new List<float> { 5f, 30f, 12f, 40f, 1f };
        var 물릴것 = ScytheCensus.PickDespawn(거리, 4);

        CollectionAssert.DoesNotContain(물릴것, 4, "가장 가까운 것(1m)이 남아야 한다");
        Assert.AreEqual(4, 물릴것.Count);
    }

    [Test]
    public void 같은_거리면_뒤_자리부터_물린다()
    {
        // 결정적이어야 한다 — 매번 답이 뒤집히면 어느 개체가 남았는지 검증이
        // 말할 수 없다. 앞선 자리가 살아남는 것은 타겟 선정·재등장과 같은 문법이다.
        var 거리 = new List<float> { 10f, 10f, 10f };

        CollectionAssert.AreEqual(new[] { 2 }, ScytheCensus.PickDespawn(거리, 1));
        CollectionAssert.AreEqual(new[] { 2, 1 }, ScytheCensus.PickDespawn(거리, 2));
    }

    [Test]
    public void 같은_입력이면_같은_답이다()
    {
        var 거리 = new List<float> { 7f, 3f, 7f, 22f, 3f };

        var 첫답 = ScytheCensus.PickDespawn(거리, 3);
        for (int i = 0; i < 5; i++)
            CollectionAssert.AreEqual(첫답, ScytheCensus.PickDespawn(거리, 3));
    }

    [Test]
    public void 있는_것보다_많이_물리라고_해도_엉키지_않는다()
    {
        var 거리 = new List<float> { 1f, 2f };

        Assert.AreEqual(2, ScytheCensus.PickDespawn(거리, 9).Count);
        Assert.IsEmpty(ScytheCensus.PickDespawn(거리, 0));
        Assert.IsEmpty(ScytheCensus.PickDespawn(거리, -1));
        Assert.IsEmpty(ScytheCensus.PickDespawn(null, 3));
    }

    [Test]
    public void 발령에서_평시로_내려오면_넷이_흩어진다()
    {
        // 실제로 쓰이는 모양 그대로 한 번 눌러 본다.
        var 거리 = new List<float> { 9f, 2f, 25f, 14f, 6f };
        int 남는수 = ScytheCensus.SurplusOver(거리.Count, ScytheAlert.Calm);

        Assert.AreEqual(4, 남는수);

        var 물릴것 = ScytheCensus.PickDespawn(거리, 남는수);
        Assert.AreEqual(4, 물릴것.Count);
        CollectionAssert.DoesNotContain(물릴것, 1, "가장 가까운 2m가 남는다");
    }
}
