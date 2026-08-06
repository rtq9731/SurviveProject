using NUnit.Framework;
using Survive.Creatures;

/// <summary>
/// 낫의 꼬리가 상태를 말한다 (기획서 §4.5 "상태 표현 — 꼬리" · 스펙 §4).
///
/// <b>이 규칙은 UI가 아니라 경고다.</b> 게이지도 알림도 없이, 꼬리를 거두는 동작
/// 하나로 플레이어가 위험을 먼저 알아챈다. 그래서 여기서 재는 것은 "예쁘게 움직이는가"가
/// 아니라 <b>무엇이 꼬리를 올리고 무엇이 내리는가</b>다 — 늦게 올라가면 경고가 아니고,
/// 아무 때나 올라가면 경고가 소음이 된다.
///
/// 자세는 저장되지 않는 순수 함수라, 되돌아오는 조건은 <b>올리는 조건이 전부 사라지는
/// 것</b> 하나뿐이다. 그 사실 자체를 아래에서 못 박는다.
/// </summary>
public class ScytheStanceTests
{
    const float 감지반경 = 14f;   // 낫 정의의 실제 값
    const float 어그로 = 6f;      // 낫 정의의 실제 값

    static CreatureTraits 낫 =>
        new CreatureTraits(BehaviorProfile.Aggressive, 감지반경, 2.2f, true);

    /// <summary>위협도 어그로도 없는 평온한 감각.</summary>
    static CreatureSenses 조용함 => CreatureSenses.NoThreat(0f, 5f);

    static CreatureSenses 거리(float d, float aggro = 0f) => new CreatureSenses(d, aggro, 5f);

    static ScythePosture 자세(CreatureSenses senses, CreatureState state,
                              HabitatZone zone = HabitatZone.Liquid,
                              ScytheAlert alert = ScytheAlert.Calm) =>
        ScytheStance.PostureFor(낫, senses, state, zone, alert);

    // ── 평상시: 작업 중 ─────────────────────────────────────

    [Test]
    public void 액면_위에서_아무_일도_없으면_꼬리를_늘어뜨린다()
    {
        Assert.AreEqual(ScythePosture.Trailing, 자세(조용함, CreatureState.Wander));
        Assert.AreEqual(ScythePosture.Trailing, 자세(조용함, CreatureState.Idle));
    }

    [Test]
    public void 해안선에서도_작업_중이다()
    {
        // 훑을 액면이 발밑에 없다고 경계에 들어가는 것이 아니다.
        // 지형의 생김새가 경고를 내기 시작하면 그 경고는 소음이 된다.
        Assert.AreEqual(ScythePosture.Trailing,
                        자세(조용함, CreatureState.Wander, HabitatZone.Shore));
    }

    [Test]
    public void 감지_반경_밖의_사람은_작업을_멈추게_하지_못한다()
    {
        Assert.AreEqual(ScythePosture.Trailing, 자세(거리(감지반경 + 0.01f), CreatureState.Wander));
    }

    // ── 경계로 올리는 것 ────────────────────────────────────

    [Test]
    public void 감지_반경_안에_들어오면_그_프레임에_꼬리를_든다()
    {
        // "AI 경고보다 먼저 온다"는 말의 내용이다. 쫓기 시작하기 전에 이미 올라간다.
        Assert.AreEqual(ScythePosture.Raised, 자세(거리(감지반경 - 0.01f), CreatureState.Wander));
    }

    [Test]
    public void 감지_반경_경계_위는_들어온_것으로_친다()
    {
        // CreatureDecision.IsDetected와 같은 규칙이어야 한다. 두 곳의 경계가 갈리면
        // 쫓기 시작하는 순간과 꼬리가 올라가는 순간이 한 프레임 어긋난다.
        Assert.AreEqual(ScythePosture.Raised, 자세(거리(감지반경), CreatureState.Wander));
    }

    [Test]
    public void 시야를_벗어나도_어그로가_남아_있으면_내리지_않는다()
    {
        // 여운이 없으면 경고가 깜빡이는 등처럼 보인다.
        Assert.AreEqual(ScythePosture.Raised, 자세(거리(감지반경 + 30f, 어그로), CreatureState.Wander));
    }

    [Test]
    public void 물러나는_중에는_작업_중이_아니다()
    {
        // 빛에 밀려 도는 프레임이 여기 걸린다 — 사람이 멀리 있어도 일을 멈춘 것이다.
        Assert.AreEqual(ScythePosture.Raised, 자세(조용함, CreatureState.Flee));
    }

    [Test]
    public void 발령이면_아직_물_위여도_꼬리를_든다()
    {
        Assert.AreEqual(ScythePosture.Raised,
                        자세(조용함, CreatureState.Wander, HabitatZone.Liquid, ScytheAlert.Alarmed));
    }

    // ── 공격 태세로 올리는 것 ───────────────────────────────

    [Test]
    public void 육지에_올라오면_공격_태세다()
    {
        // 스펙 §4의 E2E가 재는 바로 그 규칙이다.
        Assert.AreEqual(ScythePosture.Furled,
                        자세(조용함, CreatureState.Wander, HabitatZone.Inland, ScytheAlert.Alarmed));
    }

    [Test]
    public void 육지에_있으면_무엇을_하던_중이든_공격_태세다()
    {
        // 밀려 들어가 돌아가는 중이어도 마찬가지다. 있다는 사실 자체가 신호다.
        Assert.AreEqual(ScythePosture.Furled,
                        자세(조용함, CreatureState.Flee, HabitatZone.Inland));
        Assert.AreEqual(ScythePosture.Furled,
                        자세(조용함, CreatureState.Idle, HabitatZone.Inland));
    }

    [Test]
    public void 쫓거나_때리면_공격_태세다()
    {
        Assert.AreEqual(ScythePosture.Furled, 자세(거리(8f, 어그로), CreatureState.Chase));
        Assert.AreEqual(ScythePosture.Furled, 자세(거리(1.5f, 어그로), CreatureState.Attack));
    }

    [Test]
    public void 교전은_액면_위에서도_공격_태세다()
    {
        // 육지 진입만 공격 태세면 액면 위에서 죽는 플레이어가 경고를 못 받는다.
        Assert.AreEqual(ScythePosture.Furled,
                        자세(거리(2f, 어그로), CreatureState.Attack, HabitatZone.Liquid));
    }

    [Test]
    public void 공격_태세가_경계보다_세다()
    {
        // 발령이면서 교전 중인 순간 — 낮은 쪽으로 떨어지면 종막에서 경고가 약해진다.
        Assert.AreEqual(ScythePosture.Furled,
                        자세(거리(2f, 어그로), CreatureState.Chase, HabitatZone.Liquid,
                             ScytheAlert.Alarmed));
    }

    // ── 되돌아오는 조건 ─────────────────────────────────────

    [Test]
    public void 올리던_것이_전부_사라지면_그대로_작업으로_돌아간다()
    {
        // 되돌아오는 규칙을 따로 두지 않았다는 것을 여기서 못 박는다.
        // 따로 두면 올리는 규칙과 갈라져 한쪽에 걸린 채 굳는 상태가 생긴다.
        var 쫓는중 = 자세(거리(3f, 어그로), CreatureState.Chase);
        Assert.AreEqual(ScythePosture.Furled, 쫓는중);

        var 놓친직후 = 자세(거리(감지반경 + 20f, 0.01f), CreatureState.Wander);
        Assert.AreEqual(ScythePosture.Raised, 놓친직후, "어그로가 남아 있는 동안은 경계다");

        var 식은뒤 = 자세(거리(감지반경 + 20f), CreatureState.Wander);
        Assert.AreEqual(ScythePosture.Trailing, 식은뒤, "어그로가 식으면 작업으로 돌아간다");
    }

    [Test]
    public void 어그로가_정확히_0이면_식은_것이다()
    {
        Assert.AreEqual(ScythePosture.Trailing, 자세(거리(감지반경 + 20f, 0f), CreatureState.Wander));
    }

    [Test]
    public void 육지에서_내려오면_공격_태세가_풀린다()
    {
        Assert.AreEqual(ScythePosture.Furled,
                        자세(조용함, CreatureState.Wander, HabitatZone.Inland, ScytheAlert.Alarmed));
        Assert.AreEqual(ScythePosture.Raised,
                        자세(조용함, CreatureState.Wander, HabitatZone.Shore, ScytheAlert.Alarmed));
        Assert.AreEqual(ScythePosture.Trailing,
                        자세(조용함, CreatureState.Wander, HabitatZone.Shore));
    }

    [Test]
    public void 죽으면_늘어진다()
    {
        // 시체가 공격 태세로 굳어 있으면 실루엣이 거짓말을 한다.
        Assert.AreEqual(ScythePosture.Trailing,
                        자세(거리(1f, 어그로), CreatureState.Dead, HabitatZone.Inland,
                             ScytheAlert.Alarmed));
    }

    // ── 빛 ──────────────────────────────────────────────────

    [Test]
    public void 액면을_훑을_때만_접점이_빛난다()
    {
        Assert.IsTrue(ScytheStance.Skims(ScythePosture.Trailing, HabitatZone.Liquid));
        Assert.IsFalse(ScytheStance.Skims(ScythePosture.Trailing, HabitatZone.Shore),
                       "해안선에는 훑을 액면이 발밑에 없다");
        Assert.IsFalse(ScytheStance.Skims(ScythePosture.Raised, HabitatZone.Liquid),
                       "들어 올린 꼬리는 닿지 않는다");
        Assert.IsFalse(ScytheStance.Skims(ScythePosture.Furled, HabitatZone.Liquid));
    }

    [Test]
    public void 육지에_오르면_빛줄기가_사라지고_날만_남는다()
    {
        var 물위 = ScytheStance.GlowFor(ScythePosture.Trailing, HabitatZone.Liquid);
        var 육지 = ScytheStance.GlowFor(ScythePosture.Furled, HabitatZone.Inland);

        Assert.Greater(물위.Contact, 0f, "물 위에서는 수면을 긋는 빛줄기가 있다");
        Assert.AreEqual(0f, 육지.Contact, "육지에서는 빛줄기가 사라진다");
        Assert.Greater(육지.Blade, 물위.Blade, "대신 날의 에미션이 응축된다");
    }

    [Test]
    public void 넓게_퍼진_빛에서_뭉친_빛으로_간다()
    {
        var 작업 = ScytheStance.GlowFor(ScythePosture.Trailing, HabitatZone.Liquid);
        var 경계 = ScytheStance.GlowFor(ScythePosture.Raised, HabitatZone.Liquid);
        var 공격 = ScytheStance.GlowFor(ScythePosture.Furled, HabitatZone.Liquid);

        // 호는 줄고 날은 는다. 위험도가 그 교차로 읽힌다.
        Assert.Greater(작업.Arc, 경계.Arc);
        Assert.Greater(경계.Arc, 공격.Arc);
        Assert.Less(작업.Blade, 경계.Blade);
        Assert.Less(경계.Blade, 공격.Blade);
    }
}
