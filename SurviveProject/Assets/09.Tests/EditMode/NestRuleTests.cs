using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Survive.Creatures;

/// <summary>
/// <b>둥지와 코어</b> (기획서 §2.1 · §4.5).
///
/// 재는 것이 셋이다.
/// <list type="number">
/// <item><b>해제는 코어의 자리만 본다.</b> 사람이 어디 있는지도, 들고 있는지도 아니다 —
///       규칙이 하나라 배우는 데 한 번이면 된다</item>
/// <item><b>소프트락이 없다.</b> 어느 자리에서도 둥지로 돌아가는 길이 있다.
///       글로 적는 대신 기계가 걸어 본다</item>
/// <item><b>회수는 정확히 하나에만 배정된다.</b> 무리 지어 퇴각하는 것은 생물의
///       그림이고, 각자 복귀하는 것이 정비 유닛의 그림이다</item>
/// </list>
/// </summary>
public class NestRuleTests
{
    static readonly CoreWhere[] 자리들 = (CoreWhere[])Enum.GetValues(typeof(CoreWhere));
    static readonly CoreEvent[] 사건들 = (CoreEvent[])Enum.GetValues(typeof(CoreEvent));

    static readonly Vector3 둥지 = new Vector3(10f, 3f, -4f);

    // ── 해제는 코어의 자리만 본다 ───────────────────────────────

    [Test]
    public void 코어가_둥지에_있을_때만_평시다()
    {
        Assert.AreEqual(ScytheAlert.Calm, NestRule.AlertFor(CoreWhere.Nest));

        foreach (var 자리 in 자리들)
        {
            if (자리 == CoreWhere.Nest) continue;
            Assert.AreEqual(ScytheAlert.Alarmed, NestRule.AlertFor(자리), 자리.ToString());
        }
    }

    [Test]
    public void 해제_판정에_사람이_들어가지_않는다()
    {
        // <b>인자에 없다는 것이 곧 규칙이다.</b> 사람 위치나 소지 여부를 받는
        // 순간 "어디에 떨궜는지는 무관하다"가 거짓이 될 길이 생긴다.
        var m = typeof(NestRule).GetMethod("AlertFor", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(m);
        Assert.AreEqual(1, m.GetParameters().Length, "코어의 자리 하나만 받아야 한다");
        Assert.AreEqual(typeof(CoreWhere), m.GetParameters()[0].ParameterType);
    }

    [Test]
    public void 어디에_떨궜는지는_무관하다()
    {
        // 둥지에서 1m 밖이든 500m 밖이든 같은 답이다. 거리로 등급이 갈리면
        // "규칙 하나"가 아니라 "거리표"가 된다.
        foreach (var 먼곳 in new[] { new Vector3(11f, 3f, -4f), new Vector3(500f, 0f, 500f) })
        {
            Assert.IsFalse(NestRule.AtHome(먼곳, 둥지, radius: 0.5f));
            Assert.AreEqual(ScytheAlert.Alarmed, NestRule.AlertFor(CoreWhere.Dropped));
        }
    }

    [Test]
    public void 둥지는_점이_아니라_원이다()
    {
        // 되돌리는 것은 벌이 아니라 취소여야 하므로 너그러워야 한다.
        Assert.IsTrue(NestRule.AtHome(둥지, 둥지));
        Assert.IsTrue(NestRule.AtHome(둥지 + new Vector3(NestRule.HomeRadius - 0.1f, 0f, 0f), 둥지));
        Assert.IsFalse(NestRule.AtHome(둥지 + new Vector3(NestRule.HomeRadius + 0.1f, 0f, 0f), 둥지));
    }

    [Test]
    public void 높이는_둥지_판정에_끼어들지_않는다()
    {
        // 낫이 꼬리에 매달고 지나가든 바닥에 놓이든 같은 자리로 쳐야 한다.
        var 공중 = 둥지 + new Vector3(0f, 40f, 0f);
        Assert.IsTrue(NestRule.AtHome(공중, 둥지));
    }

    // ── 사건이 코어를 옮긴다 ────────────────────────────────────

    [Test]
    public void 사건이_자리를_옮긴다()
    {
        Assert.AreEqual(CoreWhere.Held, NestRule.Next(CoreWhere.Nest, CoreEvent.Taken));
        Assert.AreEqual(CoreWhere.Dropped, NestRule.Next(CoreWhere.Held, CoreEvent.Dropped));
        Assert.AreEqual(CoreWhere.Carried,
                        NestRule.Next(CoreWhere.Dropped, CoreEvent.PickedUpByScythe));
        Assert.AreEqual(CoreWhere.Nest, NestRule.Next(CoreWhere.Carried, CoreEvent.Delivered));
    }

    [Test]
    public void 물고_가던_것을_사람이_가로챌_수_있다()
    {
        // 놓친 판을 되돌리는 길이다 — 따라붙으면 끝까지 추적할 수 있고,
        // 놓치면 그걸로 끝난다. 길잡이를 주되 공짜로 주지는 않는다.
        Assert.AreEqual(CoreWhere.Held, NestRule.Next(CoreWhere.Carried, CoreEvent.Taken));
    }

    [Test]
    public void 사람_손에_있는_것을_낫이_뺏지_않는다()
    {
        // 뺏기 시작하면 "붙어 있으면 안 뺏긴다"는 약속이 또 필요해진다.
        Assert.AreEqual(CoreWhere.Held,
                        NestRule.Next(CoreWhere.Held, CoreEvent.PickedUpByScythe));
    }

    [Test]
    public void 어울리지_않는_사건은_아무것도_바꾸지_않는다()
    {
        // 세계가 사건을 두 번 보내는 일이 흔하다. 두 번째가 아무것도 하지 않으면 족하다.
        Assert.AreEqual(CoreWhere.Nest, NestRule.Next(CoreWhere.Nest, CoreEvent.Dropped));
        Assert.AreEqual(CoreWhere.Nest,
                        NestRule.Next(CoreWhere.Nest, CoreEvent.PickedUpByScythe));
    }

    [Test]
    public void 어떤_짝에도_답이_있다()
    {
        foreach (var 자리 in 자리들)
        foreach (var 사건 in 사건들)
            Assert.Contains(NestRule.Next(자리, 사건), 자리들, $"{자리} + {사건}");
    }

    // ── 소프트락이 없다 ────────────────────────────────────────

    [Test]
    public void 어느_자리에서도_둥지로_돌아갈_수_있다()
    {
        // <b>기계가 걸어 본다.</b> 글로 "소프트락이 없다"고 적는 것보다 낫다.
        foreach (var 시작 in 자리들)
        {
            var 자리 = 시작;
            var 걸어온길 = new List<string> { 자리.ToString() };

            for (int i = 0; i < 10 && 자리 != CoreWhere.Nest; i++)
            {
                var 다음걸음 = NestRule.StepHome(자리);
                Assert.IsNotNull(다음걸음, $"{자리}에서 돌아갈 길이 없다");

                자리 = NestRule.Next(자리, 다음걸음.Value);
                걸어온길.Add($"{다음걸음} -> {자리}");
            }

            Assert.AreEqual(CoreWhere.Nest, 자리,
                            $"{시작}에서 둥지에 닿지 못했다: {string.Join(" | ", 걸어온길)}");
        }
    }

    [Test]
    public void 둥지에서는_더_갈_곳이_없다()
    {
        Assert.IsNull(NestRule.StepHome(CoreWhere.Nest));
    }

    [Test]
    public void 몇_번을_훔쳐도_되돌릴_수_있다()
    {
        // "실패해도 낫이 되돌려 놓으므로 다시 훔치면 된다. 대가는 시간뿐이다."
        var 자리 = CoreWhere.Nest;

        for (int 판 = 0; 판 < 20; 판++)
        {
            자리 = NestRule.Next(자리, CoreEvent.Taken);
            Assert.AreEqual(ScytheAlert.Alarmed, NestRule.AlertFor(자리), $"{판}번째 훔침");

            자리 = NestRule.Next(자리, CoreEvent.Dropped);
            자리 = NestRule.Next(자리, CoreEvent.PickedUpByScythe);
            자리 = NestRule.Next(자리, CoreEvent.Delivered);

            Assert.AreEqual(CoreWhere.Nest, 자리, $"{판}번째 회수");
            Assert.AreEqual(ScytheAlert.Calm, NestRule.AlertFor(자리));
        }
    }

    // ── 회수는 정확히 하나에만 ──────────────────────────────────

    [Test]
    public void 코어에_가장_가까운_하나가_회수한다()
    {
        // 사람이 아니라 <b>코어</b>가 기준이다. 사람 기준으로 고르면 회수가
        // 사람을 쫓는 일이 되는데, 회수는 사람과 무관한 일이다.
        var 거리 = new List<float> { 30f, 4f, 12f, 40f };
        Assert.AreEqual(1, NestRule.PickRetriever(거리));
    }

    [Test]
    public void 같은_거리면_앞선_쪽이_회수한다()
    {
        var 거리 = new List<float> { 7f, 7f, 7f };
        Assert.AreEqual(0, NestRule.PickRetriever(거리));
        Assert.AreEqual(NestRule.PickRetriever(거리), NestRule.PickRetriever(거리));
    }

    [Test]
    public void 아무도_없으면_아무도_고르지_않는다()
    {
        Assert.AreEqual(NestRule.NoOne, NestRule.PickRetriever(new List<float>()));
        Assert.AreEqual(NestRule.NoOne, NestRule.PickRetriever(null));
    }

    [Test]
    public void 발령_다섯에서_회수_하나와_물러남_넷이_된다()
    {
        // 기획서 §4.5: "한 개체만 코어를 들고 둥지로 향하고, 나머지는 주변으로
        // 흩어져 디스폰한다." 그 셈이 실제로 1 + 4인지 본다.
        var 코어까지 = new List<float> { 22f, 9f, 31f, 15f, 40f };
        var 사람까지 = new List<float> { 5f, 26f, 12f, 33f, 2f };

        int 회수자 = NestRule.PickRetriever(코어까지);
        Assert.AreEqual(1, 회수자, "코어에 가장 가까운 것이 회수한다");

        int 물릴수 = ScytheCensus.SurplusOver(사람까지.Count, ScytheAlert.Calm);
        Assert.AreEqual(4, 물릴수);

        var 흩어질것 = ScytheCensus.PickDespawn(사람까지, 물릴수, keep: 회수자);

        Assert.AreEqual(4, 흩어질것.Count, "넷이 흩어진다");
        CollectionAssert.DoesNotContain(흩어질것, 회수자, "회수하는 개체는 남는다");
    }

    [Test]
    public void 회수하는_개체는_아무리_멀어도_남는다()
    {
        // <b>이것이 두 규칙을 맞춘 자리다.</b> 코어가 둥지에 닿는 순간 등급이
        // 내려가는데, 그때 물고 온 개체는 둥지에 있어 사람에게서 가장 멀다.
        // 거리만 보면 주인공이 먼저 지워진다.
        var 사람까지 = new List<float> { 1f, 2f, 3f, 4f, 99f };
        const int 회수자 = 4;   // 가장 먼 것이 회수자다

        var 흩어질것 = ScytheCensus.PickDespawn(사람까지, 4, keep: 회수자);

        CollectionAssert.DoesNotContain(흩어질것, 회수자);
        Assert.AreEqual(4, 흩어질것.Count);
        CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, 흩어질것);
    }

    [Test]
    public void 보호할_것이_없으면_예전과_같다()
    {
        // 회귀선. keep을 주지 않으면 이 규칙이 붙기 전과 글자 그대로 같아야 한다.
        var 거리 = new List<float> { 5f, 30f, 12f, 40f, 1f };

        CollectionAssert.AreEqual(ScytheCensus.PickDespawn(거리, 3),
                                  ScytheCensus.PickDespawn(거리, 3, ScytheCensus.NoOne));
    }

    // ── 회수 중에는 때리지 못한다 ───────────────────────────────

    [Test]
    public void 회수_중인_개체는_공격_판정에서_빠진다()
    {
        Assert.IsFalse(ScytheFsm.CanAttack(ScytheState.Retrieve));
        Assert.IsTrue(ScytheFsm.CanAttack(ScytheState.Attack));
    }

    [Test]
    public void 몸이_그것을_실제로_막는다()
    {
        // <b>두 라운드 전에 미룬 것이다.</b> 그때는 코어가 실전에 안 들어와
        // 위험 대비 이득이 없다고 적었는데, §9가 서면서 들어왔다.
        // 규칙이 아니라 <b>몸</b>이 막는지를 본문에서 확인한다.
        Type 두뇌 = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            두뇌 = asm.GetType("Survive.Creatures.CreatureBrain", false);
            if (두뇌 != null) break;
        }
        Assert.IsNotNull(두뇌);

        string 본문 = System.IO.File.ReadAllText(System.IO.Path.Combine(
            Application.dataPath, "02.Scripts/Creatures/CreatureBrain.cs"));

        int 때리는곳 = 본문.IndexOf("void Attack()", StringComparison.Ordinal);
        Assert.Greater(때리는곳, 0, "때리는 자리를 찾지 못했다");

        string 때리는몸통 = 본문.Substring(때리는곳,
            Math.Min(900, 본문.Length - 때리는곳));

        Assert.IsTrue(때리는몸통.Contains("CanAttack"),
                      "몸이 CanAttack을 묻지 않는다 — 규칙만 있고 강제가 없다");
    }
}
