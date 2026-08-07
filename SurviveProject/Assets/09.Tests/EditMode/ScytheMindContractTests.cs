using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Survive.Creatures;

/// <summary>
/// <b>몸이 상태를 들기 시작하면서 새로 생긴 위험</b>을 재는 자리 (스펙 §20 배선).
///
/// 지난 라운드까지 낫의 상태는 매 프레임 다시 유도되는 값이라 어긋날 자리가 없었다.
/// 이제 <c>ScytheMind</c>가 그것을 저장한다 — 저장하는 순간 셋이 위험해진다.
/// <list type="number">
/// <item><b>호출 횟수.</b> <see cref="ScytheFsm.Next"/>가 이력을 보므로 한 프레임에 두 번
///       부르면 한 단계를 건너뛴다</item>
/// <item><b>바깥에서 쓰기.</b> 저장된 값에 setter가 생기면 규칙을 거치지 않은 상태가 생긴다</item>
/// <item><b>되살아나기.</b> 직렬화되어 세이브를 타고 돌아오면 규칙이 만든 적 없는
///       상태로 시작할 수 있다</item>
/// </list>
/// </summary>
public class ScytheMindContractTests
{
    static readonly ScytheState[] 상태들 = (ScytheState[])Enum.GetValues(typeof(ScytheState));
    static readonly LightVerdict[] 빛판정들 = (LightVerdict[])Enum.GetValues(typeof(LightVerdict));

    static IEnumerable<ScytheSituation> 모든상황()
    {
        foreach (bool 감지 in new[] { false, true })
        foreach (var 빛 in 빛판정들)
        foreach (bool 따라잡음 in new[] { false, true })
        foreach (bool 고정조명 in new[] { false, true })
        foreach (bool 조명탄 in new[] { false, true })
        foreach (var 등급 in new[] { ScytheAlert.Calm, ScytheAlert.Alarmed })
            yield return new ScytheSituation(감지, 빛, 따라잡음, 고정조명, 조명탄, 등급);
    }

    static Type 마음타입()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType("Survive.Creatures.ScytheMind", false);
            if (t != null) return t;
        }

        Assert.Fail("Survive.Creatures.ScytheMind를 찾지 못했다");
        return null;
    }

    // ── 한 프레임에 몇 번 바뀔 수 있는가 ─────────────────────────

    [Test]
    public void 한_번_부르면_한_단계만_간다()
    {
        // 순찰에서 따라붙기까지가 한 단계다. 교전은 다음 프레임의 몫이다.
        var 덤빌만하다 = new ScytheSituation(detected: true, LightVerdict.Clear, closing: true);

        Assert.AreEqual(ScytheState.Beware, ScytheFsm.Next(ScytheState.Patrol, 덤빌만하다));
    }

    [Test]
    public void 두_번_부르면_한_단계를_건너뛴다()
    {
        // <b>이 테스트가 「한 프레임에 한 번」이라는 계약의 근거다.</b> 두 번 부른 결과가
        // 한 번 부른 결과와 다르다는 것이 곧 호출 횟수가 결과를 바꾼다는 뜻이고,
        // 그래서 몸은 Update에서 정확히 한 번만 부른다.
        var 덤빌만하다 = new ScytheSituation(detected: true, LightVerdict.Clear, closing: true);

        var 한번 = ScytheFsm.Next(ScytheState.Patrol, 덤빌만하다);
        var 두번 = ScytheFsm.Next(한번, 덤빌만하다);

        Assert.AreEqual(ScytheState.Beware, 한번);
        Assert.AreEqual(ScytheState.Attack, 두번);
        Assert.AreNotEqual(한번, 두번, "횟수가 결과를 바꾸지 않으면 이 계약은 필요 없다");
    }

    [Test]
    public void 경고가_공격보다_먼저_온다()
    {
        // 한 프레임에 한 번이라는 계약이 지키는 것. 순찰에서 교전까지는 반드시
        // 따라붙기를 지나므로, 꼬리를 드는 프레임이 항상 존재한다.
        var 덤빌만하다 = new ScytheSituation(detected: true, LightVerdict.Clear, closing: true);

        var 상태 = ScytheState.Patrol;
        var 지나온것 = new List<ScytheState>();
        for (int i = 0; i < 5; i++)
        {
            상태 = ScytheFsm.Apply(상태, ScytheDirective.None, 덤빌만하다);
            지나온것.Add(상태);
        }

        Assert.AreEqual(ScytheState.Beware, 지나온것[0]);
        Assert.AreEqual(ScytheState.Attack, 지나온것[1]);
        Assert.Less(지나온것.IndexOf(ScytheState.Beware), 지나온것.IndexOf(ScytheState.Attack));
    }

    // ── 저장된 상태가 규칙과 어긋날 수 없다 ──────────────────────

    [Test]
    public void 어떤_상황_열을_접어도_상태는_규칙_안에_머문다()
    {
        // 몸이 드는 값은 언제나 Apply가 낸 것이다. 그 접기를 길게 돌려도 열거형 밖으로
        // 나가지 않고, 매 단계가 전이표 안이어야 한다.
        var 허용 = new HashSet<(ScytheState, ScytheState)>
        {
            (ScytheState.Patrol,   ScytheState.Patrol),
            (ScytheState.Patrol,   ScytheState.Beware),
            (ScytheState.Beware,   ScytheState.Patrol),
            (ScytheState.Beware,   ScytheState.Beware),
            (ScytheState.Beware,   ScytheState.Attack),
            (ScytheState.Attack,   ScytheState.Attack),
            (ScytheState.Attack,   ScytheState.Beware),
            (ScytheState.Retrieve, ScytheState.Retrieve),
        };

        var 상황들 = 모든상황().ToList();
        var rng = new System.Random(20260807);   // 씨앗을 박아 재현되게 둔다

        foreach (var 시작 in 상태들)
        {
            var 상태 = 시작;
            for (int i = 0; i < 2000; i++)
            {
                var 이전 = 상태;
                var 상황 = 상황들[rng.Next(상황들.Count)];
                상태 = ScytheFsm.Apply(이전, ScytheDirective.None, 상황);

                Assert.Contains(상태, 상태들);
                Assert.IsTrue(허용.Contains((이전, 상태)), $"{이전} → {상태}");
            }
        }
    }

    [Test]
    public void 같은_열을_두_번_접으면_같은_끝에_닿는다()
    {
        var 상황들 = 모든상황().ToList();

        ScytheState 접기()
        {
            var s = ScytheState.Patrol;
            foreach (var 상황 in 상황들) s = ScytheFsm.Apply(s, ScytheDirective.None, 상황);
            return s;
        }

        Assert.AreEqual(접기(), 접기());
    }

    [Test]
    public void 지시는_어느_상태에서_시작해도_같은_곳으로_데려간다()
    {
        // 몸이 어떤 상태를 들고 있었든 월드의 지시가 이긴다 — 저장된 값이 지시의
        // 결과를 바꾸면 그것이 곧 "몸이 규칙과 어긋난다"이다.
        var 아무상황 = new ScytheSituation(detected: true, LightVerdict.Clear, closing: true);

        foreach (var 시작 in 상태들)
        {
            Assert.AreEqual(ScytheState.Retrieve,
                            ScytheFsm.Apply(시작, ScytheDirective.Retrieve, 아무상황), 시작.ToString());
            Assert.AreEqual(ScytheState.Patrol,
                            ScytheFsm.Apply(시작, ScytheDirective.Release, 아무상황), 시작.ToString());
        }
    }

    // ── 몸이 든 값에 손댈 수 없다 ───────────────────────────────

    [Test]
    public void 저장된_상태에_setter가_없다()
    {
        var 마음 = 마음타입();
        var state = 마음.GetProperty("State", BindingFlags.Public | BindingFlags.Instance);

        Assert.IsNotNull(state, "상태를 읽을 수는 있어야 한다");
        Assert.AreEqual(typeof(ScytheState), state.PropertyType);
        Assert.IsNull(state.SetMethod, "규칙을 거치지 않고 상태를 넣을 길이 생긴다");

        // 상태를 통째로 받아 넣는 공개 메서드도 없어야 한다. 월드가 상태를 바꾸는
        // 길은 지시(ScytheDirective) 하나뿐이다 — 그것이 §20이 통로를 따로 둔 뜻이다.
        var 넣는것 = 마음.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                       .Where(m => m.GetParameters().Any(p => p.ParameterType == typeof(ScytheState)))
                       .Select(m => m.Name)
                       .ToList();
        Assert.IsEmpty(넣는것, "상태를 받아 넣는 공개 API: " + string.Join(", ", 넣는것));
    }

    [Test]
    public void 월드가_상태를_바꾸는_길은_지시_하나뿐이다()
    {
        var 마음 = 마음타입();
        var order = 마음.GetMethod("Order", BindingFlags.Public | BindingFlags.Instance,
                                   null, new[] { typeof(ScytheDirective) }, null);

        Assert.IsNotNull(order, "월드가 회수를 지정할 통로가 있어야 한다");
    }

    [Test]
    public void 상태는_직렬화되지_않는다()
    {
        // 세이브를 타고 돌아오면 규칙이 만든 적 없는 상태로 시작할 수 있다. 낫의
        // 상태는 <b>매 판 순찰에서 시작</b>하는 것이 옳다 — 사람이 저장하고 나간 사이의
        // 따라붙기를 다음 판이 물려받을 이유가 없다.
        var 마음 = 마음타입();
        var 저장되는것 = 마음.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                        BindingFlags.Instance)
                            .Where(f => f.FieldType == typeof(ScytheState))
                            .Where(f => f.IsPublic ||
                                        f.GetCustomAttributes(typeof(UnityEngine.SerializeField), true).Any())
                            .Select(f => f.Name)
                            .ToList();

        Assert.IsEmpty(저장되는것, "직렬화되는 상태 필드: " + string.Join(", ", 저장되는것));
    }

    // ── 재등장 리듬 ────────────────────────────────────────────

    [Test]
    public void 간격이_차야_자리를_옮긴다()
    {
        Assert.IsFalse(ScytheReappearance.DueToMove(0f, 6f));
        Assert.IsFalse(ScytheReappearance.DueToMove(5.99f, 6f));
        Assert.IsTrue(ScytheReappearance.DueToMove(6f, 6f));
        Assert.IsTrue(ScytheReappearance.DueToMove(20f, 6f));
    }

    [Test]
    public void 간격이_0이면_옮기지_않는다()
    {
        // 정의의 aggroSeconds가 0인 종에 이 부품이 붙어도 매 프레임 순간이동하지 않는다.
        Assert.IsFalse(ScytheReappearance.DueToMove(100f, 0f));
        Assert.IsFalse(ScytheReappearance.DueToMove(100f, -1f));
    }
}
