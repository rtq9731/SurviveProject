using NUnit.Framework;
using UnityEngine;
using Survive.Creatures;

/// <summary>
/// 백로그 15 — CreatureBrain에서 뽑아낸 판단 규칙.
///
/// 생물이 언제 도망치고 언제 덤비는가는 전부 경계 비교 하나에 달려 있다.
/// 감지 반경 위에 정확히 선 플레이어가 감지되는지 아닌지, 어그로가 0이 된
/// 그 순간에 물러서는지 아닌지 — 이런 것은 씬에서 눈으로 확인할 수 없다.
/// 그래서 판단만 Domain으로 내려 놓고 여기서 값으로 확인한다.
/// </summary>
public class CreatureDecisionTests
{
    const float 감지반경 = 8f;
    const float 사거리 = 1.5f;
    const float 눈금 = 0.001f;

    static CreatureTraits 성향(BehaviorProfile b) => new CreatureTraits(b, 감지반경, 사거리);

    /// <summary>거리·어그로·상태시간을 그대로 넣는다.</summary>
    static CreatureSenses 감각(float 거리, float 어그로 = 0f, float 상태시간 = 1f) =>
        new CreatureSenses(거리, 어그로, 상태시간);

    // ── 감지·사거리·쿨다운의 경계 ──────────────────────────────────────

    [Test]
    public void 감지반경_위에_정확히_선_것은_감지된다()
    {
        Assert.IsTrue(CreatureDecision.IsDetected(감지반경, 감지반경));
    }

    [Test]
    public void 감지반경_바깥은_감지되지_않는다()
    {
        Assert.IsFalse(CreatureDecision.IsDetected(감지반경 + 눈금, 감지반경));
    }

    [Test]
    public void 위협이_없으면_어떤_반경으로도_감지되지_않는다()
    {
        Assert.IsFalse(CreatureDecision.IsDetected(float.MaxValue, float.MaxValue / 2f));
    }

    [Test]
    public void 사거리_위는_닿고_그_바깥은_닿지_않는다()
    {
        Assert.IsTrue(CreatureDecision.IsWithinRange(사거리, 사거리));
        Assert.IsFalse(CreatureDecision.IsWithinRange(사거리 + 눈금, 사거리));
    }

    [Test]
    public void 쿨다운은_정확히_끝난_시각에_풀린다()
    {
        Assert.IsFalse(CreatureDecision.IsReady(4.999f, 5f));
        Assert.IsTrue(CreatureDecision.IsReady(5f, 5f));
        Assert.IsTrue(CreatureDecision.IsReady(5.001f, 5f));
    }

    // ── Passive — 위협을 보지 않는다 ─────────────────────────────────────

    [Test]
    public void Passive는_코앞에_붙어도_반응하지_않는다()
    {
        var 결과 = CreatureDecision.NextIntent(성향(BehaviorProfile.Passive), 감각(0f, 상태시간: 1f));
        Assert.AreEqual(CreatureIntent.Hold, 결과);
    }

    [Test]
    public void Passive는_상태시간이_다하면_생태행동을_고른다()
    {
        var 결과 = CreatureDecision.NextIntent(성향(BehaviorProfile.Passive), 감각(0f, 상태시간: 0f));
        Assert.AreEqual(CreatureIntent.Ecology, 결과);
    }

    [Test]
    public void 상태시간_경계는_0이다()
    {
        var 성향값 = 성향(BehaviorProfile.Passive);
        Assert.AreEqual(CreatureIntent.Hold, CreatureDecision.NextIntent(성향값, 감각(99f, 상태시간: 눈금)));
        Assert.AreEqual(CreatureIntent.Ecology, CreatureDecision.NextIntent(성향값, 감각(99f, 상태시간: 0f)));
    }

    // ── Skittish — 보이면 도망 ──────────────────────────────────────────

    [Test]
    public void Skittish는_감지반경_위에서부터_도망친다()
    {
        var 성향값 = 성향(BehaviorProfile.Skittish);
        Assert.AreEqual(CreatureIntent.Flee, CreatureDecision.NextIntent(성향값, 감각(감지반경)));
        Assert.AreNotEqual(CreatureIntent.Flee, CreatureDecision.NextIntent(성향값, 감각(감지반경 + 눈금)));
    }

    [Test]
    public void Skittish는_상태시간이_남아도_감지되면_도망친다()
    {
        var 결과 = CreatureDecision.NextIntent(성향(BehaviorProfile.Skittish), 감각(1f, 상태시간: 99f));
        Assert.AreEqual(CreatureIntent.Flee, 결과);
    }

    [Test]
    public void Skittish는_보이지_않으면_생태행동으로_돌아간다()
    {
        var 결과 = CreatureDecision.NextIntent(성향(BehaviorProfile.Skittish),
                                             감각(감지반경 + 눈금, 상태시간: 0f));
        Assert.AreEqual(CreatureIntent.Ecology, 결과);
    }

    [Test]
    public void Skittish는_보이지_않고_상태시간이_남으면_하던_것을_계속한다()
    {
        var 결과 = CreatureDecision.NextIntent(성향(BehaviorProfile.Skittish),
                                             감각(감지반경 + 눈금, 상태시간: 1f));
        Assert.AreEqual(CreatureIntent.Hold, 결과);
    }

    // ── Defensive — 먼저 덤비지 않는다 ──────────────────────────────────

    [Test]
    public void Defensive는_코앞이라도_맞기_전에는_덤비지_않는다()
    {
        var 결과 = CreatureDecision.NextIntent(성향(BehaviorProfile.Defensive),
                                             감각(0.1f, 어그로: 0f, 상태시간: 0f));
        Assert.AreEqual(CreatureIntent.Ecology, 결과);
    }

    [Test]
    public void Defensive는_어그로가_남아_있고_사거리_밖이면_쫓는다()
    {
        var 결과 = CreatureDecision.NextIntent(성향(BehaviorProfile.Defensive),
                                             감각(사거리 + 눈금, 어그로: 3f));
        Assert.AreEqual(CreatureIntent.Chase, 결과);
    }

    [Test]
    public void Defensive는_어그로가_남아_있고_사거리_위면_때린다()
    {
        var 결과 = CreatureDecision.NextIntent(성향(BehaviorProfile.Defensive),
                                             감각(사거리, 어그로: 3f));
        Assert.AreEqual(CreatureIntent.Attack, 결과);
    }

    [Test]
    public void Defensive의_어그로_경계는_0이다()
    {
        var 성향값 = 성향(BehaviorProfile.Defensive);
        Assert.AreEqual(CreatureIntent.Attack, CreatureDecision.NextIntent(성향값, 감각(0.5f, 어그로: 눈금)));
        Assert.AreNotEqual(CreatureIntent.Attack, CreatureDecision.NextIntent(성향값, 감각(0.5f, 어그로: 0f)));
    }

    [Test]
    public void Defensive는_어그로가_식으면_상태시간이_남는_동안_하던_것을_계속한다()
    {
        var 결과 = CreatureDecision.NextIntent(성향(BehaviorProfile.Defensive),
                                             감각(0.5f, 어그로: 0f, 상태시간: 1f));
        Assert.AreEqual(CreatureIntent.Hold, 결과);
    }

    [Test]
    public void Defensive는_어그로가_있는데_위협이_사라지면_쫓는_쪽으로_남는다()
    {
        // 플레이어를 놓친 순간 거리는 무한대다. 사거리 안이 아니니 추격이 된다.
        var 결과 = CreatureDecision.NextIntent(성향(BehaviorProfile.Defensive),
                                             CreatureSenses.NoThreat(3f, 1f));
        Assert.AreEqual(CreatureIntent.Chase, 결과);
    }

    // ── Aggressive — 보이면 덤빈다 ──────────────────────────────────────

    [Test]
    public void Aggressive는_맞지_않아도_감지만_되면_쫓는다()
    {
        var 결과 = CreatureDecision.NextIntent(성향(BehaviorProfile.Aggressive),
                                             감각(감지반경, 어그로: 0f));
        Assert.AreEqual(CreatureIntent.Chase, 결과);
    }

    [Test]
    public void Aggressive는_감지되고_사거리_안이면_때린다()
    {
        var 결과 = CreatureDecision.NextIntent(성향(BehaviorProfile.Aggressive),
                                             감각(사거리 - 눈금, 어그로: 0f));
        Assert.AreEqual(CreatureIntent.Attack, 결과);
    }

    [Test]
    public void Aggressive는_감지_밖이라도_어그로가_남으면_쫓는다()
    {
        var 결과 = CreatureDecision.NextIntent(성향(BehaviorProfile.Aggressive),
                                             감각(감지반경 + 100f, 어그로: 1f));
        Assert.AreEqual(CreatureIntent.Chase, 결과);
    }

    [Test]
    public void Aggressive는_한가하면_생태행동이_아니라_배회한다()
    {
        var 결과 = CreatureDecision.NextIntent(성향(BehaviorProfile.Aggressive),
                                             감각(감지반경 + 눈금, 어그로: 0f, 상태시간: 0f));
        Assert.AreEqual(CreatureIntent.Wander, 결과);
    }

    // ── 피격 반응 ───────────────────────────────────────────────────────

    [Test]
    public void 맞았을_때의_반응은_성향이_정한다()
    {
        Assert.AreEqual(DamageReaction.Ignore, CreatureDecision.ReactToDamage(BehaviorProfile.Passive));
        Assert.AreEqual(DamageReaction.Flee, CreatureDecision.ReactToDamage(BehaviorProfile.Skittish));
        Assert.AreEqual(DamageReaction.Retaliate, CreatureDecision.ReactToDamage(BehaviorProfile.Defensive));
        Assert.AreEqual(DamageReaction.Retaliate, CreatureDecision.ReactToDamage(BehaviorProfile.Aggressive));
    }

    // ── 생태 행동의 우선순위 ────────────────────────────────────────────

    /// <summary>목표를 하나 내놓는 척하는 대역. 몇 번 불렸는지 센다.</summary>
    class 탐침
    {
        readonly bool _찾음;
        readonly Vector3 _위치;
        public int 호출횟수;

        public 탐침(bool 찾음, Vector3 위치 = default)
        {
            _찾음 = 찾음;
            _위치 = 위치;
        }

        public bool Try(out Vector3 목적지)
        {
            호출횟수++;
            목적지 = _찾음 ? _위치 : Vector3.zero;
            return _찾음;
        }

        public TryGetDestination 델리게이트 => Try;
    }

    [Test]
    public void 먹이가_있으면_먹으러_간다()
    {
        var 먹이 = new 탐침(true, new Vector3(3f, 0f, 4f));
        var 결과 = CreatureDecision.PickEcology(먹이.델리게이트, new 탐침(true).델리게이트);

        Assert.AreEqual(CreatureState.Feed, 결과.State);
        Assert.IsTrue(결과.HasDestination);
        Assert.AreEqual(new Vector3(3f, 0f, 4f), 결과.Destination);
    }

    [Test]
    public void 먹이가_잡히면_수집은_아예_묻지_않는다()
    {
        var 수집 = new 탐침(true, Vector3.one);
        CreatureDecision.PickEcology(new 탐침(true).델리게이트, 수집.델리게이트);

        Assert.AreEqual(0, 수집.호출횟수, "먹이가 먼저 잡혔는데 수집까지 물었다 — 물리 질의가 한 번 더 돈다");
    }

    [Test]
    public void 먹이가_없으면_주우러_간다()
    {
        var 결과 = CreatureDecision.PickEcology(new 탐침(false).델리게이트,
                                              new 탐침(true, new Vector3(1f, 2f, 3f)).델리게이트);

        Assert.AreEqual(CreatureState.Scavenge, 결과.State);
        Assert.AreEqual(new Vector3(1f, 2f, 3f), 결과.Destination);
    }

    [Test]
    public void 할_일이_없으면_배회하고_목적지는_비운다()
    {
        var 결과 = CreatureDecision.PickEcology(new 탐침(false).델리게이트, new 탐침(false).델리게이트);

        Assert.AreEqual(CreatureState.Wander, 결과.State);
        Assert.IsFalse(결과.HasDestination, "배회 목적지는 전이할 때 새로 뽑는다 — 여기서 덮어쓰면 안 된다");
    }

    [Test]
    public void 먹지도_줍지도_못하는_생물은_배회한다()
    {
        // 생산자도 분해자도 아닌 개체 — 두 컴포넌트가 아예 없다.
        var 결과 = CreatureDecision.PickEcology(null, null);
        Assert.AreEqual(CreatureState.Wander, 결과.State);
    }

    // ── 전이 허용 ───────────────────────────────────────────────────────

    [Test]
    public void 다른_상태로는_언제든_전이한다()
    {
        Assert.IsTrue(CreatureDecision.ShouldTransition(CreatureState.Wander, CreatureState.Flee, 99f));
    }

    [Test]
    public void 같은_상태로_다시_들어가는_것은_시간이_다한_뒤에만_된다()
    {
        Assert.IsFalse(CreatureDecision.ShouldTransition(CreatureState.Wander, CreatureState.Wander, 눈금));
        Assert.IsTrue(CreatureDecision.ShouldTransition(CreatureState.Wander, CreatureState.Wander, 0f));
        Assert.IsTrue(CreatureDecision.ShouldTransition(CreatureState.Wander, CreatureState.Wander, -1f));
    }

    // ── 상태별 행동 ─────────────────────────────────────────────────────

    [Test]
    public void 목적지를_잡은_상태들은_모두_그리로_움직인다()
    {
        Assert.AreEqual(CreatureAction.MoveToDestination, CreatureDecision.ActionFor(CreatureState.Wander));
        Assert.AreEqual(CreatureAction.MoveToDestination, CreatureDecision.ActionFor(CreatureState.Flee));
        Assert.AreEqual(CreatureAction.MoveToDestination, CreatureDecision.ActionFor(CreatureState.Feed));
        Assert.AreEqual(CreatureAction.MoveToDestination, CreatureDecision.ActionFor(CreatureState.Scavenge));
    }

    [Test]
    public void 추격은_위협의_지금_위치를_따라간다()
    {
        Assert.AreEqual(CreatureAction.PursueThreat, CreatureDecision.ActionFor(CreatureState.Chase));
    }

    [Test]
    public void 공격은_멈춰_서서_한다()
    {
        Assert.AreEqual(CreatureAction.AttackThreat, CreatureDecision.ActionFor(CreatureState.Attack));
    }

    [Test]
    public void 대기와_사망은_움직이지_않는다()
    {
        Assert.AreEqual(CreatureAction.Idle, CreatureDecision.ActionFor(CreatureState.Idle));
        Assert.AreEqual(CreatureAction.Idle, CreatureDecision.ActionFor(CreatureState.Dead));
    }

    [Test]
    public void 멈춰_서는_상태는_대기와_사망뿐이다()
    {
        // switch에서 상태를 빠뜨리면 default로 떨어져 그 생물이 제자리에 굳는다.
        // 새 상태를 추가하고 ActionFor를 잊으면 이 테스트가 잡는다.
        foreach (CreatureState 상태 in System.Enum.GetValues(typeof(CreatureState)))
        {
            bool 멈춤 = CreatureDecision.ActionFor(상태) == CreatureAction.Idle;
            bool 멈춰야_함 = 상태 == CreatureState.Idle || 상태 == CreatureState.Dead;
            Assert.AreEqual(멈춰야_함, 멈춤, $"{상태}의 행동이 정해지지 않아 제자리에 굳는다");
        }
    }
}
