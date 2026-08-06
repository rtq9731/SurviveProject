using NUnit.Framework;
using UnityEngine;
using Survive.Creatures;
using Survive.World;

/// <summary>
/// 백로그 30 — 소비자(포식자) 1종.
///
/// 소비자는 기존 4종과 두 가지가 다르다. <b>먼저 덤빈다</b>는 것과
/// <b>빛을 피한다</b>는 것이다. 둘 다 경계 하나에 게임의 감촉이 걸려 있다 —
/// 감지 반경을 한 발짝 벗어난 순간 흥미를 잃으면 도망칠 이유가 없고,
/// 랜턴을 켰는데 한 프레임이라도 더 다가오면 "빛이 방어 수단이다"가 거짓이 된다.
///
/// 그 경계는 씬에서 눈으로 확인할 수 없으므로 여기서 값으로 확인한다.
/// 기존 성향(Passive·Skittish·Defensive)이 그대로인지는
/// <see cref="CreatureDecisionTests"/>가 이미 지키고 있다 — 이 파일은 손대지 않는다.
/// </summary>
public class ConsumerDecisionTests
{
    const float 감지반경 = 14f;
    const float 사거리 = 2.2f;
    const float 눈금 = 0.001f;

    /// <summary>빛을 꺼리는 선공 성향 — 소비자가 쓰는 조합.</summary>
    static CreatureTraits 소비자 =>
        new CreatureTraits(BehaviorProfile.Aggressive, 감지반경, 사거리, avoidsLight: true);

    /// <summary>빛을 보지 않는 선공 성향. 빛 규칙이 이쪽으로 새지 않는지 볼 때 쓴다.</summary>
    static CreatureTraits 빛에무관한선공 =>
        new CreatureTraits(BehaviorProfile.Aggressive, 감지반경, 사거리);

    static CreatureSenses 감각(float 거리, float 어그로 = 0f, float 상태시간 = 1f,
                              bool 내가밝음 = false, bool 대상이밝음 = false) =>
        new CreatureSenses(거리, 어그로, 상태시간, 내가밝음, 대상이밝음);

    // ── 어둠 속 — 선공이 성립한다 ───────────────────────────────────────

    [Test]
    public void 어둠에서는_감지반경_위에서부터_쫓는다()
    {
        Assert.AreEqual(CreatureIntent.Chase,
                        CreatureDecision.NextIntent(소비자, 감각(감지반경)));
        Assert.AreEqual(CreatureIntent.Wander,
                        CreatureDecision.NextIntent(소비자, 감각(감지반경 + 눈금, 상태시간: 0f)));
    }

    [Test]
    public void 어둠에서_사거리_위면_때린다()
    {
        Assert.AreEqual(CreatureIntent.Attack, CreatureDecision.NextIntent(소비자, 감각(사거리)));
        Assert.AreEqual(CreatureIntent.Chase, CreatureDecision.NextIntent(소비자, 감각(사거리 + 눈금)));
    }

    // ── 어그로 갱신 — 놓쳐도 곧바로 포기하지 않는다 ─────────────────────

    [Test]
    public void 시야에_담고_있는_동안_어그로를_다시_채운다()
    {
        Assert.IsTrue(CreatureDecision.ShouldRenewAggro(소비자, 감각(감지반경)));
    }

    [Test]
    public void 감지_경계_바깥에서는_어그로를_채우지_않는다()
    {
        // 이 경계가 곧 추격 지속시간의 시작점이다. 여기서 한 번 더 채워지면
        // 생물이 영원히 쫓아온다.
        Assert.IsFalse(CreatureDecision.ShouldRenewAggro(소비자, 감각(감지반경 + 눈금)));
    }

    [Test]
    public void 놓친_뒤에도_어그로가_남아_있는_동안은_쫓는다()
    {
        Assert.AreEqual(CreatureIntent.Chase,
                        CreatureDecision.NextIntent(소비자, 감각(감지반경 + 100f, 어그로: 눈금)));
    }

    [Test]
    public void 어그로가_다하면_배회로_돌아간다()
    {
        Assert.AreEqual(CreatureIntent.Wander,
                        CreatureDecision.NextIntent(소비자, 감각(감지반경 + 100f, 어그로: 0f, 상태시간: 0f)));
    }

    [Test]
    public void 어그로가_다해도_상태시간이_남으면_하던_것을_계속한다()
    {
        Assert.AreEqual(CreatureIntent.Hold,
                        CreatureDecision.NextIntent(소비자, 감각(감지반경 + 100f, 어그로: 0f, 상태시간: 1f)));
    }

    [Test]
    public void 선공이_아닌_성향은_어그로를_스스로_채우지_않는다()
    {
        foreach (BehaviorProfile 성향 in System.Enum.GetValues(typeof(BehaviorProfile)))
        {
            if (성향 == BehaviorProfile.Aggressive) continue;
            var t = new CreatureTraits(성향, 감지반경, 사거리, avoidsLight: true);
            Assert.IsFalse(CreatureDecision.ShouldRenewAggro(t, 감각(0.5f)),
                           $"{성향}이 스스로 어그로를 채웠다 — 기존 4종의 감촉이 바뀐다");
        }
    }

    // ── 빛 — 대상이 밝으면 다가오지 못한다 ──────────────────────────────

    [Test]
    public void 대상이_빛_안이면_코앞이라도_때리지_않는다()
    {
        var 결과 = CreatureDecision.NextIntent(소비자, 감각(0.1f, 대상이밝음: true));
        Assert.AreEqual(CreatureIntent.Wander, 결과);
    }

    [Test]
    public void 대상이_빛_안이면_어그로가_타고_있어도_끊는다()
    {
        // 이것이 깨지면 "랜턴을 켜면 다가오지 못한다"가 거짓이 된다 —
        // 한 대 맞은 뒤에는 빛이 아무 소용이 없어진다.
        var 결과 = CreatureDecision.NextIntent(소비자, 감각(사거리, 어그로: 99f, 대상이밝음: true));
        Assert.AreEqual(CreatureIntent.Wander, 결과);
    }

    [Test]
    public void 대상이_빛_안이면_어그로도_채워지지_않는다()
    {
        // 빛 앞에서 어그로가 계속 갱신되면 불을 끄는 순간 추격이 그대로 이어진다.
        Assert.IsFalse(CreatureDecision.ShouldRenewAggro(소비자, 감각(1f, 대상이밝음: true)));
    }

    [Test]
    public void 빛이_꺼지면_같은_거리에서_다시_쫓는다()
    {
        Assert.AreEqual(CreatureIntent.Wander, CreatureDecision.NextIntent(소비자, 감각(5f, 대상이밝음: true)));
        Assert.AreEqual(CreatureIntent.Chase, CreatureDecision.NextIntent(소비자, 감각(5f, 대상이밝음: false)));
    }

    // ── 빛 — 내가 밝으면 물러난다 ───────────────────────────────────────

    [Test]
    public void 내가_빛_안이면_물러난다()
    {
        Assert.AreEqual(CreatureIntent.Flee, CreatureDecision.NextIntent(소비자, 감각(5f, 내가밝음: true)));
    }

    [Test]
    public void 내가_빛_안이면_위협이_없어도_물러난다()
    {
        // 화톳불 옆을 배회하다 들어간 경우. 쫓을 대상이 없어도 나와야 한다.
        var 결과 = CreatureDecision.NextIntent(소비자, CreatureSenses.NoThreat(0f, 1f, selfInLight: true));
        Assert.AreEqual(CreatureIntent.Flee, 결과);
    }

    [Test]
    public void 내가_밝은_것이_대상이_밝은_것보다_우선한다()
    {
        var 결과 = CreatureDecision.NextIntent(소비자, 감각(5f, 내가밝음: true, 대상이밝음: true));
        Assert.AreEqual(CreatureIntent.Flee, 결과, "빛 속에 서서 배회할 수는 없다");
    }

    // ── 빛 판정 자체 ────────────────────────────────────────────────────

    [Test]
    public void 빛_판정은_세_경우뿐이다()
    {
        Assert.AreEqual(LightVerdict.Clear, CreatureDecision.JudgeLight(소비자, 감각(5f)));
        Assert.AreEqual(LightVerdict.Blocked, CreatureDecision.JudgeLight(소비자, 감각(5f, 대상이밝음: true)));
        Assert.AreEqual(LightVerdict.Retreat, CreatureDecision.JudgeLight(소비자, 감각(5f, 내가밝음: true)));
    }

    [Test]
    public void 빛을_꺼리지_않으면_어떤_빛도_판단을_막지_않는다()
    {
        var 밝음 = 감각(5f, 내가밝음: true, 대상이밝음: true);
        Assert.AreEqual(LightVerdict.Clear, CreatureDecision.JudgeLight(빛에무관한선공, 밝음));
        Assert.AreEqual(CreatureIntent.Chase, CreatureDecision.NextIntent(빛에무관한선공, 밝음));
    }

    [Test]
    public void 빛_플래그를_주지_않으면_예전과_같은_판단이다()
    {
        // 기존 4종은 avoidsLight가 꺼져 있고 CreatureBrain이 빛을 묻지도 않는다.
        // 3인자 생성자가 예전 의미 그대로인지 확인한다.
        foreach (BehaviorProfile 성향 in System.Enum.GetValues(typeof(BehaviorProfile)))
        {
            var 옛것 = new CreatureTraits(성향, 감지반경, 사거리);
            Assert.IsFalse(옛것.AvoidsLight, $"{성향}: 3인자 생성자가 빛을 꺼리게 만들었다");

            var 옛감각 = new CreatureSenses(5f, 1f, 1f);
            Assert.IsFalse(옛감각.SelfInLight);
            Assert.IsFalse(옛감각.ThreatInLight);
            Assert.IsFalse(옛감각.ThreatBlindSide);
            Assert.AreEqual(LightVerdict.Clear, CreatureDecision.JudgeLight(옛것, 옛감각));
        }
    }

    // ── 물러날 방향 — 어느 빛에서 멀어질 것인가 ─────────────────────────

    class 가짜광원 : ILitZoneSource
    {
        public Vector3 LitZoneCenter { get; set; }
        public float LitZoneRadius { get; set; } = 5f;
        public bool IsLit { get; set; } = true;
    }

    [SetUp]
    public void 초기화() => LitZoneRegistry.Clear();

    [TearDown]
    public void 정리() => LitZoneRegistry.Clear();

    [Test]
    public void 밝지_않은_자리에서는_멀어질_빛이_없다()
    {
        LitZoneRegistry.Register(new 가짜광원 { LitZoneCenter = Vector3.zero, LitZoneRadius = 5f });
        Assert.IsFalse(LitZoneRegistry.TryGetLitCenter(new Vector3(5.001f, 0f, 0f), out _));
    }

    [Test]
    public void 밝은_자리에서는_그_광원의_중심을_알려_준다()
    {
        LitZoneRegistry.Register(new 가짜광원 { LitZoneCenter = new Vector3(2f, 0f, 0f), LitZoneRadius = 5f });
        Assert.IsTrue(LitZoneRegistry.TryGetLitCenter(new Vector3(3f, 0f, 0f), out var 중심));
        Assert.AreEqual(new Vector3(2f, 0f, 0f), 중심);
    }

    [Test]
    public void 겹친_빛_속에서는_가장_가까운_중심에서_멀어진다()
    {
        LitZoneRegistry.Register(new 가짜광원 { LitZoneCenter = new Vector3(-4f, 0f, 0f), LitZoneRadius = 6f });
        LitZoneRegistry.Register(new 가짜광원 { LitZoneCenter = new Vector3(1f, 0f, 0f), LitZoneRadius = 6f });

        Assert.IsTrue(LitZoneRegistry.TryGetLitCenter(Vector3.zero, out var 중심));
        Assert.AreEqual(new Vector3(1f, 0f, 0f), 중심);
    }

    [Test]
    public void 꺼진_광원의_중심은_알려_주지_않는다()
    {
        LitZoneRegistry.Register(new 가짜광원 { LitZoneCenter = Vector3.zero, IsLit = false });
        Assert.IsFalse(LitZoneRegistry.TryGetLitCenter(Vector3.zero, out _));
    }

    [Test]
    public void 물러나는_방향은_빛의_반대쪽이다()
    {
        // CreatureBrain이 이 조합을 그대로 쓴다. 빛 중심에서 멀어지는 자리가
        // 나오는지, 원래 자리보다 실제로 멀어졌는지 확인한다.
        var 빛중심 = new Vector3(2f, 0f, 0f);
        var 내자리 = new Vector3(3f, 0f, 0f);

        var 목적지 = CreatureNavigation.FleeDestination(내자리, 빛중심, 12f, 0f);

        Assert.Greater(Vector3.Distance(목적지, 빛중심), Vector3.Distance(내자리, 빛중심),
                       "빛에서 멀어지지 않는 도주 목적지가 나왔다");
        Assert.Greater(목적지.x, 내자리.x, "빛 반대쪽으로 가야 한다");
    }
}
