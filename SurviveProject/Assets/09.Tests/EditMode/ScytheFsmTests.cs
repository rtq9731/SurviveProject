using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Survive.Creatures;

/// <summary>
/// 낫의 4상태 FSM (기획서 §4.5 "상태머신 — 넷", 스펙 §20).
///
/// <b>여기서 재는 것의 알맹이는 「없는 전이가 없다」다.</b> 상태 기계의 버그는 대개
/// 있어야 할 전이가 빠진 것이 아니라 <b>있으면 안 될 전이가 몰래 생긴 것</b>이다 —
/// 순찰에서 곧바로 교전으로 뛰거나, 회수 중인 개체가 스스로 빠져나오거나.
/// 그래서 조건의 곱을 전부 돌려 <b>관측된 전이 집합</b>을 만들고, 그것이 표와
/// 글자 그대로 같은지를 본다.
/// </summary>
public class ScytheFsmTests
{
    const float 감지반경 = 14f;   // 낫 정의의 실제 값
    const float 사거리 = 2.2f;    // 낫 정의의 실제 값

    static CreatureTraits 낫 =>
        new CreatureTraits(BehaviorProfile.Aggressive, 감지반경, 사거리, avoidsLight: true);

    static readonly ScytheState[] 상태들 =
        (ScytheState[])Enum.GetValues(typeof(ScytheState));

    static readonly LightVerdict[] 빛판정들 =
        (LightVerdict[])Enum.GetValues(typeof(LightVerdict));

    static readonly CreatureState[] 범용상태들 =
        (CreatureState[])Enum.GetValues(typeof(CreatureState));

    /// <summary>조건의 곱 전부. 전이표 전수 확인이 이 위에서 돈다.</summary>
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

    [SetUp]
    public void 등급을_평시로() => ScytheWatch.Reset();

    [TearDown]
    public void 등급을_되돌린다() => ScytheWatch.Reset();

    // ── 전이표 전수 ────────────────────────────────────────────

    [Test]
    public void 전이표에_없는_전이는_하나도_나오지_않는다()
    {
        // 표(스펙 §20)를 그대로 옮긴 것이다. 여기 없는 쌍이 한 번이라도 나오면 실패다.
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

        var 관측 = new HashSet<(ScytheState, ScytheState)>();

        foreach (var 이전 in 상태들)
        foreach (var 상황 in 모든상황())
        {
            var 다음 = ScytheFsm.Next(이전, 상황);
            관측.Add((이전, 다음));

            Assert.IsTrue(허용.Contains((이전, 다음)),
                          $"표에 없는 전이: {이전} → {다음} ({설명(상황)})");
        }

        // 반대 방향도 본다 — 표에 적어 두고 한 번도 안 나오는 전이가 있으면
        // 그것은 규칙이 아니라 <b>죽은 글</b>이다.
        var 안나온것 = 허용.Except(관측).ToList();
        Assert.IsEmpty(안나온것,
                       "표에 적혔는데 한 번도 나오지 않은 전이: " +
                       string.Join(", ", 안나온것.Select(p => $"{p.Item1}→{p.Item2}")));
    }

    [Test]
    public void 어떤_조합에도_답이_있다()
    {
        // 정의되지 않은 조합이 0건이라는 것. 열거형 밖의 값이 나오면 실패다.
        foreach (var 이전 in 상태들)
        foreach (var 상황 in 모든상황())
        {
            var 다음 = ScytheFsm.Next(이전, 상황);
            Assert.Contains(다음, 상태들, $"{이전} · {설명(상황)}");
        }
    }

    [Test]
    public void 순찰에서_교전으로_한_번에_뛰지_못한다()
    {
        // 따라붙기를 한 프레임이라도 거쳐야 한다. 이것이 없으면 "꼬리를 드는 경고가
        // 공격보다 먼저 온다"는 §4의 약속이 어떤 프레임에서는 지켜지지 않는다.
        foreach (var 상황 in 모든상황())
            Assert.AreNotEqual(ScytheState.Attack, ScytheFsm.Next(ScytheState.Patrol, 상황),
                               설명(상황));
    }

    [Test]
    public void 교전에서_순찰로_한_번에_내려가지_못한다()
    {
        foreach (var 상황 in 모든상황())
            Assert.AreNotEqual(ScytheState.Patrol, ScytheFsm.Next(ScytheState.Attack, 상황),
                               설명(상황));
    }

    // ── 따라붙기는 시간으로 풀리지 않는다 ────────────────────────

    [Test]
    public void 감지가_살아_있으면_몇_프레임을_돌려도_따라붙기가_풀리지_않는다()
    {
        // 시간이 아니라 사건이 푼다. 여기서 「시간」은 같은 상황을 반복해서 넣는 것이다 —
        // 규칙에 타이머가 있었다면 어느 반복에선가 Patrol이 나온다.
        var 붙어있다 = new ScytheSituation(detected: true, LightVerdict.Blocked, closing: false);

        var 상태 = ScytheState.Beware;
        for (int i = 0; i < 10000; i++)
        {
            상태 = ScytheFsm.Next(상태, 붙어있다);
            Assert.AreEqual(ScytheState.Beware, 상태, $"{i}번째 프레임");
        }
    }

    [Test]
    public void 감지에서_벗어나면_풀린다()
    {
        var 놓쳤다 = new ScytheSituation(detected: false, LightVerdict.Clear, closing: false);
        Assert.AreEqual(ScytheState.Patrol, ScytheFsm.Next(ScytheState.Beware, 놓쳤다));
    }

    // ── 고정 조명과 임시 조명이 갈린다 ──────────────────────────

    [Test]
    public void 고정_조명에_들어가면_따라붙기가_풀린다()
    {
        // 화톳불·빛기둥. 앞뒤가 없고 사람과 함께 돌지도 않으므로 내주는 쪽이 없다.
        var 화톳불 = new ScytheSituation(detected: true, LightVerdict.Blocked, closing: true,
                                        playerNearFixedLight: true);
        Assert.AreEqual(ScytheState.Patrol, ScytheFsm.Next(ScytheState.Beware, 화톳불));
    }

    [Test]
    public void 랜턴만으로는_따라붙기가_풀리지_않는다()
    {
        // 이 대조가 이 라운드의 핵심 갈림이다. 빛에 막혀 있다(Blocked)는 점은 위와
        // 똑같은데 <b>고정 조명이 아니라는 것 하나</b>로 답이 갈린다.
        var 랜턴 = new ScytheSituation(detected: true, LightVerdict.Blocked, closing: true,
                                      playerNearFixedLight: false);
        Assert.AreEqual(ScytheState.Beware, ScytheFsm.Next(ScytheState.Beware, 랜턴));

        // 내가 빛 안이라 물러나는 프레임도 마찬가지다 — 물러나는 것은 푸는 것이 아니다.
        var 물러남 = new ScytheSituation(detected: true, LightVerdict.Retreat, closing: false);
        Assert.AreEqual(ScytheState.Beware, ScytheFsm.Next(ScytheState.Beware, 물러남));
    }

    // ── 교전 진입에 등 뒤 사각이 필수다 ─────────────────────────

    [Test]
    public void 빛에_막힌_채로는_이동속도_조건을_만족해도_교전에_못_간다()
    {
        // §19와 물린 계약이다. 랜턴이 켜져 있는 동안 JudgeLight가 Clear를 내는 길은
        // 등 뒤 사각뿐이므로(아래 테스트가 그것을 확인한다), 여기서 Blocked·Retreat을
        // 막는 것이 곧 "사각 밖에서는 못 온다"이다.
        foreach (var 빛 in new[] { LightVerdict.Blocked, LightVerdict.Retreat })
        {
            var 막힘 = new ScytheSituation(detected: true, 빛, closing: true);
            Assert.AreNotEqual(ScytheState.Attack, ScytheFsm.Next(ScytheState.Beware, 막힘),
                               빛.ToString());
        }
    }

    [Test]
    public void 랜턴이_켜져_있으면_사각만이_교전을_연다()
    {
        // 고르는 쪽·판정하는 쪽·전이하는 쪽이 <b>같은 함수 하나</b>를 보고 있다는 확인.
        // 상대가 빛 안일 때 Clear에 이르는 길은 사각뿐이고, 그래서 전이 규칙이 사각을
        // 따로 판정하지 않아도 사각 조건이 성립한다.
        var 정면 = new CreatureSenses(5f, 0f, 0f, false, threatInLight: true, threatBlindSide: false);
        var 사각 = new CreatureSenses(5f, 0f, 0f, false, threatInLight: true, threatBlindSide: true);

        Assert.AreEqual(LightVerdict.Blocked, CreatureDecision.JudgeLight(낫, 정면));
        Assert.AreEqual(LightVerdict.Clear, CreatureDecision.JudgeLight(낫, 사각));

        var 정면상황 = new ScytheSituation(true, CreatureDecision.JudgeLight(낫, 정면), true);
        var 사각상황 = new ScytheSituation(true, CreatureDecision.JudgeLight(낫, 사각), true);

        Assert.AreEqual(ScytheState.Beware, ScytheFsm.Next(ScytheState.Beware, 정면상황));
        Assert.AreEqual(ScytheState.Attack, ScytheFsm.Next(ScytheState.Beware, 사각상황));
    }

    [Test]
    public void 따라잡지_못하면_사각이어도_교전에_못_간다()
    {
        // "이동속도 조건". 달아나는 사람과 멈춰 선 사람이 같은 대접을 받으면 안 된다.
        var 놓치는중 = new ScytheSituation(detected: true, LightVerdict.Clear, closing: false);
        Assert.AreEqual(ScytheState.Beware, ScytheFsm.Next(ScytheState.Beware, 놓치는중));
    }

    [Test]
    public void 빛이_다시_막으면_붙어_있던_것이_떨어진다()
    {
        foreach (var 빛 in new[] { LightVerdict.Blocked, LightVerdict.Retreat })
        {
            var 막힘 = new ScytheSituation(detected: true, 빛, closing: true);
            Assert.AreEqual(ScytheState.Beware, ScytheFsm.Next(ScytheState.Attack, 막힘),
                            빛.ToString());
        }
    }

    [Test]
    public void 조명탄은_사각에_붙어_있는_것도_떼어낸다()
    {
        // §8-3. 랜턴으로는 안 되는 일이 조명탄으로는 된다 — 이 대조가 조명탄이
        // 존재할 이유다.
        var 사각에붙음 = new ScytheSituation(detected: true, LightVerdict.Clear, closing: true);
        Assert.AreEqual(ScytheState.Attack, ScytheFsm.Next(ScytheState.Attack, 사각에붙음));

        var 조명탄 = new ScytheSituation(detected: true, LightVerdict.Clear, closing: true,
                                        playerNearFixedLight: false, pushedByFlare: true);
        Assert.AreEqual(ScytheState.Beware, ScytheFsm.Next(ScytheState.Attack, 조명탄));
    }

    // ── 회수는 월드만이 지정한다 ────────────────────────────────

    [Test]
    public void 개체는_회수로_스스로_들어가지_못한다()
    {
        foreach (var 이전 in new[] { ScytheState.Patrol, ScytheState.Beware, ScytheState.Attack })
        foreach (var 상황 in 모든상황())
            Assert.AreNotEqual(ScytheState.Retrieve, ScytheFsm.Next(이전, 상황),
                               $"{이전} · {설명(상황)}");
    }

    [Test]
    public void 개체는_회수에서_스스로_빠져나오지_못한다()
    {
        foreach (var 상황 in 모든상황())
            Assert.AreEqual(ScytheState.Retrieve, ScytheFsm.Next(ScytheState.Retrieve, 상황),
                            설명(상황));
    }

    [Test]
    public void 교전_중이어도_회수_지정이_이긴다()
    {
        var 한창붙었다 = new ScytheSituation(detected: true, LightVerdict.Clear, closing: true);

        foreach (var 이전 in 상태들)
            Assert.AreEqual(ScytheState.Retrieve,
                            ScytheFsm.Apply(이전, ScytheDirective.Retrieve, 한창붙었다),
                            이전.ToString());
    }

    [Test]
    public void 회수가_끝나면_월드가_순찰로_돌려보낸다()
    {
        var 한창붙었다 = new ScytheSituation(detected: true, LightVerdict.Clear, closing: true);

        foreach (var 이전 in 상태들)
            Assert.AreEqual(ScytheState.Patrol,
                            ScytheFsm.Apply(이전, ScytheDirective.Release, 한창붙었다),
                            이전.ToString());
    }

    [Test]
    public void 지정이_없으면_전이표와_글자_그대로_같다()
    {
        // 통로를 따로 둔 것이 개체의 평소 판단에 아무것도 더하지 않아야 한다.
        foreach (var 이전 in 상태들)
        foreach (var 상황 in 모든상황())
            Assert.AreEqual(ScytheFsm.Next(이전, 상황),
                            ScytheFsm.Apply(이전, ScytheDirective.None, 상황),
                            $"{이전} · {설명(상황)}");
    }

    [Test]
    public void 회수_중인_개체는_공격_판정에서_빠진다()
    {
        // 꼬리가 무기인데 짐을 들었다는 것이 <b>규칙으로도</b> 참이어야
        // 형태로 읽는 것이 거짓말이 아니다 (기획서 §4.5 회수 연출).
        Assert.IsFalse(ScytheFsm.CanAttack(ScytheState.Retrieve));
        Assert.IsFalse(ScytheFsm.CanAttack(ScytheState.Patrol));
        Assert.IsFalse(ScytheFsm.CanAttack(ScytheState.Beware));
        Assert.IsTrue(ScytheFsm.CanAttack(ScytheState.Attack));
    }

    [Test]
    public void 순찰만_물_위에_묶인다()
    {
        Assert.IsFalse(ScytheFsm.RangesInland(ScytheState.Patrol));
        Assert.IsTrue(ScytheFsm.RangesInland(ScytheState.Beware));
        Assert.IsTrue(ScytheFsm.RangesInland(ScytheState.Attack));
        Assert.IsTrue(ScytheFsm.RangesInland(ScytheState.Retrieve));

        // 다만 실제로 올라갈 수 있는지는 여전히 등급이 가른다. 둘을 하나로 접으면
        // "따라붙는 중이면 평시에도 육지로 온다"가 되어 A섬 내륙이 안전하지 않다.
        Assert.IsFalse(ScytheHabitat.CanEnter(HabitatZone.Inland, ScytheAlert.Calm));
        Assert.IsTrue(ScytheHabitat.CanEnter(HabitatZone.Inland, ScytheAlert.Alarmed));
    }

    // ── 발령은 월드가 깐 바닥이다 ───────────────────────────────

    [Test]
    public void 발령이면_감지가_없어도_작업으로_돌아가지_않는다()
    {
        var 아무도없다 = new ScytheSituation(detected: false, LightVerdict.Clear, closing: false,
                                           playerNearFixedLight: false, pushedByFlare: false,
                                           alert: ScytheAlert.Alarmed);

        Assert.AreEqual(ScytheState.Beware, ScytheFsm.Next(ScytheState.Patrol, 아무도없다));
        Assert.AreEqual(ScytheState.Beware, ScytheFsm.Next(ScytheState.Beware, 아무도없다));
    }

    [Test]
    public void 발령_중에는_고정_조명도_풀지_못한다()
    {
        // 등급은 월드가 건 것이고 고정 조명은 개체가 읽는 것이다. 개체 판단이 월드가
        // 깐 바닥을 뚫으면 등급을 월드가 소유하는 뜻이 없어진다.
        var 화톳불 = new ScytheSituation(detected: true, LightVerdict.Blocked, closing: false,
                                        playerNearFixedLight: true, pushedByFlare: false,
                                        alert: ScytheAlert.Alarmed);
        Assert.AreEqual(ScytheState.Beware, ScytheFsm.Next(ScytheState.Beware, 화톳불));
    }

    // ── 경계 등급의 소유자는 월드 하나다 ────────────────────────

    /// <summary>
    /// 낫의 몸. <b>어셈블리 참조가 아니라 반사로 집는다</b> — 이 검증 어셈블리는
    /// <c>Survive.Domain</c>만 참조한다(순수 로직만 씬 없이 돌린다는 이 저장소의 결).
    /// 소유권 게이트는 몸 쪽 API의 모양을 봐야 하므로 여기서만 그 선을 넘는다.
    /// 못 찾으면 실패다 — 조용히 통과하면 게이트가 있으나 마나다.
    /// </summary>
    static Type 몸타입()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType("Survive.Creatures.HoverDrifter", false);
            if (t != null) return t;
        }

        Assert.Fail("Survive.Creatures.HoverDrifter를 찾지 못했다");
        return null;
    }

    [Test]
    public void 개체_쪽에는_등급을_쓰는_API가_없다()
    {
        // 게이트를 <b>글로 적지 않고 형(型)으로</b> 세운다. 누군가 편의로 setter를
        // 되살리면 이 테스트가 먼저 깨진다.
        var 몸 = 몸타입();
        var alert = 몸.GetProperty("Alert", BindingFlags.Public | BindingFlags.Instance);

        Assert.IsNotNull(alert, "몸이 등급을 읽을 수는 있어야 한다");
        Assert.AreEqual(typeof(ScytheAlert), alert.PropertyType);
        Assert.IsNull(alert.SetMethod, "몸에 등급 setter가 있으면 소유권이 다시 갈라진다");

        var 쓰는것 = 몸.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                      .Where(m => m.GetParameters().Any(p => p.ParameterType == typeof(ScytheAlert)))
                      .Select(m => m.Name)
                      .ToList();
        Assert.IsEmpty(쓰는것, "등급을 받아 넣는 개체 API: " + string.Join(", ", 쓰는것));

        // 필드로 몰래 들고 있지도 않아야 한다. 값을 개체가 보관하는 순간
        // 둘이 갈라질 자리가 생긴다.
        var 보관 = 몸.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                BindingFlags.Instance)
                     .Where(f => f.FieldType == typeof(ScytheAlert))
                     .Select(f => f.Name)
                     .ToList();
        Assert.IsEmpty(보관, "개체가 등급을 보관하는 필드: " + string.Join(", ", 보관));
    }

    [Test]
    public void 개체_둘의_등급이_서로_달라질_수_없다()
    {
        // 값이 개체 바깥 한 곳에 있다는 것을 값으로 확인한다.
        var 몸 = 몸타입();
        var alert = 몸.GetProperty("Alert", BindingFlags.Public | BindingFlags.Instance);

        var 갑오브젝트 = new GameObject("낫몸-갑");
        var 을오브젝트 = new GameObject("낫몸-을");
        try
        {
            var 갑 = 갑오브젝트.AddComponent(몸);
            var 을 = 을오브젝트.AddComponent(몸);

            ScytheWatch.Set(ScytheAlert.Alarmed);
            Assert.AreEqual(ScytheAlert.Alarmed, alert.GetValue(갑));
            Assert.AreEqual(ScytheAlert.Alarmed, alert.GetValue(을));

            ScytheWatch.Set(ScytheAlert.Calm);
            Assert.AreEqual(ScytheAlert.Calm, alert.GetValue(갑));
            Assert.AreEqual(alert.GetValue(갑), alert.GetValue(을), "둘의 등급이 갈렸다");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(갑오브젝트);
            UnityEngine.Object.DestroyImmediate(을오브젝트);
        }
    }

    [Test]
    public void 등급은_평시로_시작하고_되돌릴_수_있다()
    {
        Assert.AreEqual(ScytheAlert.Calm, ScytheWatch.Alert);

        ScytheWatch.Set(ScytheAlert.Alarmed);
        Assert.AreEqual(ScytheAlert.Alarmed, ScytheWatch.Alert);

        ScytheWatch.Reset();
        Assert.AreEqual(ScytheAlert.Calm, ScytheWatch.Alert);
    }

    // ── ③의 회귀선: 후보 하나 · 사각 없음 ───────────────────────

    [Test]
    public void 후보_하나에_사각이_없으면_전이가_옛_판단과_같은_말을_한다()
    {
        // 이 라운드가 붙인 것은 <b>어휘</b>이지 감촉이 아니다. 사각이 없는 세계
        // (=랜턴 오프셋 이전과 같은 상황)에서, 새 상태가 말하는 것과 옛 판단이
        // 말하는 것이 어긋나지 않아야 한다.
        foreach (float d in new[] { 1f, 사거리, 감지반경, 감지반경 + 0.01f, 100f })
        foreach (bool 내가밝음 in new[] { false, true })
        foreach (bool 대상밝음 in new[] { false, true })
        {
            var 감각 = new CreatureSenses(d, 0f, 0f, 내가밝음, 대상밝음, threatBlindSide: false);
            var 빛 = CreatureDecision.JudgeLight(낫, 감각);
            bool 감지 = CreatureDecision.IsDetected(d, 감지반경);
            var 의도 = CreatureDecision.NextIntent(낫, 감각);

            var 상황 = new ScytheSituation(감지, 빛, closing: true);
            var 다음 = ScytheFsm.Next(ScytheState.Beware, 상황);
            string 자리 = $"d={d} self={내가밝음} threat={대상밝음}";

            // 옛 판단이 덤비라고 하면 새 상태도 교전이고, 아니면 교전이 아니다.
            bool 옛것이덤빈다 = 의도 == CreatureIntent.Chase || 의도 == CreatureIntent.Attack;
            Assert.AreEqual(옛것이덤빈다, 다음 == ScytheState.Attack, 자리);

            // 옛 판단이 물러나라고 하면 새 상태는 절대 교전이 아니다.
            if (의도 == CreatureIntent.Flee)
                Assert.AreNotEqual(ScytheState.Attack, 다음, 자리);
        }
    }

    // ── 자세는 상태에서 유도한다 ────────────────────────────────

    [Test]
    public void 상태마다_꼬리가_하나로_정해진다()
    {
        Assert.AreEqual(ScythePosture.Trailing,
                        ScytheStance.PostureFrom(ScytheState.Patrol, CreatureState.Wander,
                                                 HabitatZone.Liquid));
        Assert.AreEqual(ScythePosture.Raised,
                        ScytheStance.PostureFrom(ScytheState.Beware, CreatureState.Wander,
                                                 HabitatZone.Liquid));
        Assert.AreEqual(ScythePosture.Furled,
                        ScytheStance.PostureFrom(ScytheState.Attack, CreatureState.Chase,
                                                 HabitatZone.Liquid));
        Assert.AreEqual(ScythePosture.Laden,
                        ScytheStance.PostureFrom(ScytheState.Retrieve, CreatureState.Wander,
                                                 HabitatZone.Liquid));
    }

    [Test]
    public void 짐을_든_꼬리는_육지에서도_공격_태세로_보이지_않는다()
    {
        // 둥지로 가는 길은 액면이 육지를 파고든 수로다(기획서 §2.1). 그 길에서
        // 육지 규칙에 걸려 Furled로 보이면, 때리지 못하는 개체가 가장 위험한
        // 실루엣이 된다 — 형태로 읽는 것이 정확히 거꾸로가 된다.
        foreach (var 구역 in new[] { HabitatZone.Liquid, HabitatZone.Shore, HabitatZone.Inland })
            Assert.AreEqual(ScythePosture.Laden,
                            ScytheStance.PostureFrom(ScytheState.Retrieve, CreatureState.Wander, 구역),
                            구역.ToString());
    }

    [Test]
    public void 짐을_든_꼬리는_수면을_긋지_않는다()
    {
        // 멀리서 보이는 빛줄기가 곧 "순찰 중"이라는 신호다. 회수 중에도 줄기가
        // 남으면 어둠 속에서 둘을 구별할 수 없다.
        Assert.IsFalse(ScytheStance.Skims(ScythePosture.Laden, HabitatZone.Liquid));
        Assert.AreEqual(0f, ScytheStance.GlowFor(ScythePosture.Laden, HabitatZone.Liquid).Contact);

        // 그리고 날이 공격 태세만큼 응축되지 않는다.
        var 짐 = ScytheStance.GlowFor(ScythePosture.Laden, HabitatZone.Liquid);
        var 공격 = ScytheStance.GlowFor(ScythePosture.Furled, HabitatZone.Liquid);
        Assert.Less(짐.Blade, 공격.Blade);
    }

    [Test]
    public void 자세_유도가_옛_규칙과_한_글자도_다르지_않다()
    {
        // <b>회귀선이다.</b> 우선순위 if 체인을 4상태 위로 옮겼으므로, 옮기기 전의
        // 답과 옮긴 뒤의 답이 입력 전수에서 같아야 한다. 하나라도 갈리면 이 라운드는
        // 꼬리의 감촉을 바꾼 것이고, 그것은 곧 경고의 감촉을 바꾼 것이다.
        foreach (var 범용 in 범용상태들)
        foreach (float d in new[] { 0f, 사거리, 감지반경, 감지반경 + 0.01f, 100f, float.MaxValue })
        foreach (float 어그로 in new[] { 0f, 3f })
        foreach (var 구역 in new[] { HabitatZone.Liquid, HabitatZone.Shore, HabitatZone.Inland })
        foreach (var 등급 in new[] { ScytheAlert.Calm, ScytheAlert.Alarmed })
        {
            var 감각 = new CreatureSenses(d, 어그로, 0f);
            var 지금 = ScytheStance.PostureFor(낫, 감각, 범용, 구역, 등급);
            var 옛것 = 옛규칙(낫, 감각, 범용, 구역, 등급);

            Assert.AreEqual(옛것, 지금,
                            $"{범용} d={d} aggro={어그로} {구역} {등급}");
        }
    }

    /// <summary>
    /// 이 라운드 <b>이전</b>의 <c>ScytheStance.PostureFor</c>를 그대로 옮겨 적은 것.
    /// 회귀선을 코드로 들고 있어야 "바뀌지 않았다"를 기계가 판정할 수 있다.
    /// </summary>
    static ScythePosture 옛규칙(in CreatureTraits traits, in CreatureSenses senses,
                                CreatureState state, HabitatZone zone, ScytheAlert alert)
    {
        if (state == CreatureState.Dead) return ScythePosture.Trailing;
        if (zone == HabitatZone.Inland) return ScythePosture.Furled;
        if (state == CreatureState.Chase || state == CreatureState.Attack)
            return ScythePosture.Furled;
        if (alert == ScytheAlert.Alarmed) return ScythePosture.Raised;
        if (CreatureDecision.IsDetected(senses.DistanceToThreat, traits.DetectRadius))
            return ScythePosture.Raised;
        if (senses.AggroLeft > 0f) return ScythePosture.Raised;
        if (state == CreatureState.Flee) return ScythePosture.Raised;
        return ScythePosture.Trailing;
    }

    [Test]
    public void 통역은_회수를_절대_내놓지_않는다()
    {
        // 몸이 아는 것으로는 코어 사건을 알 수 없고, 알 수 있어서도 안 된다.
        foreach (var 범용 in 범용상태들)
        foreach (bool 감지 in new[] { false, true })
        foreach (float 어그로 in new[] { 0f, 3f })
        foreach (var 등급 in new[] { ScytheAlert.Calm, ScytheAlert.Alarmed })
            Assert.AreNotEqual(ScytheState.Retrieve,
                               ScytheFsm.FromCreatureState(범용, 감지, 어그로, 등급),
                               $"{범용} 감지={감지} 어그로={어그로} {등급}");
    }

    static string 설명(in ScytheSituation s) =>
        $"감지={s.Detected} 빛={s.Light} 따라잡음={s.Closing} " +
        $"고정조명={s.PlayerNearFixedLight} 조명탄={s.PushedByFlare} 등급={s.Alert}";
}
