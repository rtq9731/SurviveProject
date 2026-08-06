using System.Collections.Generic;
using NUnit.Framework;
using Survive.Creatures;

/// <summary>
/// 판단의 입구가 <b>위협 하나</b>에서 <b>위협 목록</b>으로 열렸다 (스펙 §22 코옵 사전 확인).
///
/// <b>여기서 재는 것의 절반은 「아무것도 바뀌지 않았다」다.</b> 이 작업은 코옵을 구현하는
/// 것이 아니라 나중에 구현할 수 있게 그릇만 여는 것이므로, 목록 길이가 1일 때 답이
/// 예전과 <b>필드 하나까지</b> 같아야 한다. 그것이 아래 "회귀" 절이다.
///
/// 나머지 절반은 <b>둘일 때 무엇이 답인가</b>다. 그중 알맹이는 빛이다 —
/// 둘이 등을 맞대면 한 명은 랜턴 안, 다른 한 명은 그 사각에 있을 수 있고
/// (기획서 §14 "코옵 확장 여지"), 그 둘에 <b>다른 답</b>이 나오지 않으면
/// 협동이 규칙에서 나올 자리가 없다.
/// </summary>
public class ThreatRosterTests
{
    const float 감지반경 = 14f;   // 낫 정의의 실제 값
    const float 사거리 = 2.2f;    // 낫 정의의 실제 값

    /// <summary>빛을 꺼리는 선공 성향 — 낫.</summary>
    static CreatureTraits 낫 =>
        new CreatureTraits(BehaviorProfile.Aggressive, 감지반경, 사거리, true);

    /// <summary>빛을 보지 않는 선공 성향. 빛 규칙을 빼고 거리만 볼 때 쓴다.</summary>
    static CreatureTraits 선공 =>
        new CreatureTraits(BehaviorProfile.Aggressive, 감지반경, 사거리);

    static ThreatRoster 목록(params ThreatSighting[] 위협들) =>
        ThreatRoster.From(new List<ThreatSighting>(위협들));

    static ThreatSighting 위협(float 거리, bool 빛안 = false) =>
        new ThreatSighting(거리, 빛안);

    // ── 고르는 규칙 ────────────────────────────────────────────

    [Test]
    public void 아무도_없으면_고를_것이_없다()
    {
        Assert.AreEqual(ThreatSelection.None, ThreatSelection.Select(new List<ThreatSighting>()));
        Assert.AreEqual(ThreatSelection.None, ThreatSelection.Select(null));
        Assert.AreEqual(ThreatSelection.None, ThreatRoster.Empty.SelectedIndex);
    }

    [Test]
    public void 하나뿐이면_고를_것도_없이_그것이다()
    {
        Assert.AreEqual(0, 목록(위협(9f)).SelectedIndex);
    }

    [Test]
    public void 둘이면_가까운_쪽을_고른다()
    {
        Assert.AreEqual(1, 목록(위협(20f), 위협(4f)).SelectedIndex);
    }

    [Test]
    public void 목록의_순서는_고르는_데_영향을_주지_않는다()
    {
        // 앞에 있어서 골라진 것인지 가까워서 골라진 것인지를 가른다.
        Assert.AreEqual(0, 목록(위협(4f), 위협(20f)).SelectedIndex);
        Assert.AreEqual(1, 목록(위협(20f), 위협(4f)).SelectedIndex);
    }

    [Test]
    public void 셋이어도_가장_가까운_하나를_고른다()
    {
        Assert.AreEqual(1, 목록(위협(30f), 위협(3f), 위협(12f)).SelectedIndex);
    }

    [Test]
    public void 같은_거리면_앞선_쪽이_이긴다()
    {
        // 임의의 규칙이지만 <b>흔들리지 않는 것</b>이 중요하다. 뒤에 오는 것으로
        // 갈아타면 둘이 똑같이 떨어져 있는 동안 매 프레임 대상이 뒤집히고,
        // 그러면 꼬리 자세가 깜빡여 경고가 경고이기를 그만둔다.
        Assert.AreEqual(0, 목록(위협(7f), 위협(7f)).SelectedIndex);
        Assert.AreEqual(0, 목록(위협(7f), 위협(7f), 위협(7f)).SelectedIndex);
    }

    [Test]
    public void 같은_입력이면_몇_번을_물어도_같은_답이다()
    {
        var 셋 = 목록(위협(7f), 위협(7f), 위협(7f));
        Assert.AreEqual(셋.SelectedIndex, 셋.SelectedIndex);
        Assert.AreEqual(셋.SelectedIndex, CreatureDecision.SelectThreat(셋));
    }

    [Test]
    public void 감지_반경_밖이어도_가장_가까운_것을_고른다()
    {
        // 고르기 전에 반경으로 거르지 않는다. 가장 가까운 것이 반경 밖이면
        // 나머지도 전부 밖이므로 거르는 단계는 답을 바꾸지 못하고, 규칙만 하나 는다.
        var 멀리 = 목록(위협(감지반경 + 50f), 위협(감지반경 + 10f));
        Assert.AreEqual(1, 멀리.SelectedIndex);
        Assert.AreEqual(감지반경 + 10f, 멀리.NearestDistance);
    }

    // ── 비어 있을 때 = 위협 없음 ────────────────────────────────

    [Test]
    public void 빈_목록의_감각은_위협_없음과_같다()
    {
        var 없음 = CreatureSenses.NoThreat(2f, 5f, selfInLight: true);
        var 빈것 = ThreatRoster.Empty.Senses(2f, 5f, selfInLight: true);

        Assert.AreEqual(없음.DistanceToThreat, 빈것.DistanceToThreat);
        Assert.AreEqual(없음.AggroLeft, 빈것.AggroLeft);
        Assert.AreEqual(없음.StateTimer, 빈것.StateTimer);
        Assert.AreEqual(없음.SelfInLight, 빈것.SelfInLight);
        Assert.AreEqual(없음.ThreatInLight, 빈것.ThreatInLight);
    }

    [Test]
    public void 채워지지_않은_그릇도_빈_목록이다()
    {
        // 몸 쪽이 아직 아무도 못 찾은 프레임이 이 모양이다.
        ThreatRoster 기본값 = default;
        Assert.IsFalse(기본값.HasThreat);
        Assert.AreEqual(0, 기본값.Count);
        Assert.AreEqual(float.MaxValue, 기본값.NearestDistance);
    }

    [Test]
    public void 빈_목록이면_어떤_반경으로도_감지되지_않는다()
    {
        Assert.AreEqual(CreatureIntent.Wander,
                        CreatureDecision.NextIntent(선공, ThreatRoster.Empty, 0f, 0f));
    }

    // ── 회귀: 길이 1이면 예전과 같다 ─────────────────────────────

    [Test]
    public void 하나짜리_목록의_감각은_손으로_만든_것과_같다()
    {
        // 예전 CreatureBrain이 만들던 그 값이다.
        var 예전 = new CreatureSenses(6f, 2f, 5f, true, true);
        var 지금 = 목록(위협(6f, 빛안: true)).Senses(2f, 5f, selfInLight: true);

        Assert.AreEqual(예전.DistanceToThreat, 지금.DistanceToThreat);
        Assert.AreEqual(예전.AggroLeft, 지금.AggroLeft);
        Assert.AreEqual(예전.StateTimer, 지금.StateTimer);
        Assert.AreEqual(예전.SelfInLight, 지금.SelfInLight);
        Assert.AreEqual(예전.ThreatInLight, 지금.ThreatInLight);
    }

    [Test]
    public void 하나짜리_목록의_전이가_성향마다_예전과_같다()
    {
        // 경계 위·안·밖을 전부 넣는다. 하나라도 갈리면 이 작업은 동작을 바꾼 것이다.
        float[] 거리들 = { 0f, 사거리, 사거리 + 0.01f, 감지반경, 감지반경 + 0.01f, 100f };
        var 성향들 = new[]
        {
            BehaviorProfile.Passive, BehaviorProfile.Skittish,
            BehaviorProfile.Defensive, BehaviorProfile.Aggressive,
        };

        foreach (var 성향 in 성향들)
        foreach (float d in 거리들)
        foreach (float 어그로 in new[] { 0f, 3f })
        foreach (float 상태시간 in new[] { 0f, 5f })
        foreach (bool 내가밝음 in new[] { false, true })
        foreach (bool 대상밝음 in new[] { false, true })
        {
            var 성질 = new CreatureTraits(성향, 감지반경, 사거리, avoidsLight: true);
            var 예전 = new CreatureSenses(d, 어그로, 상태시간, 내가밝음, 대상밝음);
            var 지금 = 목록(위협(d, 대상밝음));

            Assert.AreEqual(CreatureDecision.NextIntent(성질, 예전),
                            CreatureDecision.NextIntent(성질, 지금, 어그로, 상태시간, 내가밝음),
                            $"{성향} d={d} aggro={어그로} timer={상태시간} self={내가밝음} threat={대상밝음}");
        }
    }

    [Test]
    public void 하나짜리_목록의_어그로_갱신이_예전과_같다()
    {
        foreach (float d in new[] { 1f, 감지반경, 감지반경 + 0.01f })
        foreach (bool 내가밝음 in new[] { false, true })
        foreach (bool 대상밝음 in new[] { false, true })
        {
            var 예전 = new CreatureSenses(d, 0f, 0f, 내가밝음, 대상밝음);
            var 지금 = 목록(위협(d, 대상밝음));

            Assert.AreEqual(CreatureDecision.ShouldRenewAggro(낫, 예전),
                            CreatureDecision.ShouldRenewAggro(낫, 지금, 0f, 0f, 내가밝음),
                            $"d={d} self={내가밝음} threat={대상밝음}");
        }
    }

    [Test]
    public void 하나짜리_목록의_꼬리_자세가_예전과_같다()
    {
        foreach (float d in new[] { 1f, 감지반경, 감지반경 + 0.01f })
        foreach (float 어그로 in new[] { 0f, 3f })
        foreach (var 상태 in new[] { CreatureState.Idle, CreatureState.Wander,
                                     CreatureState.Chase, CreatureState.Flee, CreatureState.Dead })
        foreach (var 구역 in new[] { HabitatZone.Liquid, HabitatZone.Shore, HabitatZone.Inland })
        foreach (var 태세 in new[] { ScytheAlert.Calm, ScytheAlert.Alarmed })
        {
            var 예전 = new CreatureSenses(d, 어그로, 0f);
            var 지금 = 목록(위협(d));

            Assert.AreEqual(ScytheStance.PostureFor(낫, 예전, 상태, 구역, 태세),
                            ScytheStance.PostureFor(낫, 지금, 어그로, 상태, 구역, 태세),
                            $"d={d} aggro={어그로} {상태} {구역} {태세}");
        }
    }

    [Test]
    public void 하나뿐인_위협이_빛_안이면_예전처럼_막힌다()
    {
        // 랜턴을 켜면 낫이 다가오지 못한다 — 이 규칙이 살아 있는지부터 확인한다.
        Assert.AreEqual(CreatureIntent.Wander,
                        CreatureDecision.NextIntent(낫, 목록(위협(3f, 빛안: true)), 0f, 0f));
    }

    // ── 둘일 때: 거리 ──────────────────────────────────────────

    [Test]
    public void 가까운_쪽까지의_거리로_감지한다()
    {
        var 둘 = 목록(위협(감지반경 + 40f), 위협(감지반경 - 1f));
        Assert.AreEqual(감지반경 - 1f, 둘.NearestDistance);
        Assert.AreEqual(CreatureIntent.Chase, CreatureDecision.NextIntent(선공, 둘, 0f, 0f));
    }

    [Test]
    public void 가까운_쪽이_반경_밖이면_아무도_감지되지_않는다()
    {
        var 둘 = 목록(위협(감지반경 + 0.01f), 위협(감지반경 + 30f));
        Assert.AreEqual(CreatureIntent.Wander, CreatureDecision.NextIntent(선공, 둘, 0f, 0f));
    }

    [Test]
    public void 가까운_쪽이_사거리_안이면_그쪽을_때린다()
    {
        // 먼 쪽이 아무리 많아도 판단은 고른 하나만 본다.
        var 둘 = 목록(위협(50f), 위협(사거리));
        Assert.AreEqual(CreatureIntent.Attack, CreatureDecision.NextIntent(선공, 둘, 0f, 0f));
        Assert.AreEqual(1, CreatureDecision.SelectThreat(둘));
    }

    [Test]
    public void 둘_중_가까운_쪽이_꼬리를_올린다()
    {
        var 둘 = 목록(위협(100f), 위협(감지반경 - 1f));
        Assert.AreEqual(ScythePosture.Raised,
                        ScytheStance.PostureFor(낫, 둘, 0f, CreatureState.Wander,
                                                HabitatZone.Liquid, ScytheAlert.Calm));
    }

    // ── 둘일 때: 빛 — 이것이 코옵의 알맹이다 ─────────────────────

    [Test]
    public void 위협마다_빛_여부가_따로_실린다()
    {
        // 하나는 랜턴 안, 하나는 그 사각. 같은 개체·같은 프레임에 답이 갈려야 한다.
        var 둘 = 목록(위협(5f, 빛안: true), 위협(9f, 빛안: false));

        Assert.IsTrue(둘.SensesFor(0, 0f, 0f).ThreatInLight);
        Assert.IsFalse(둘.SensesFor(1, 0f, 0f).ThreatInLight);
        Assert.AreEqual(5f, 둘.SensesFor(0, 0f, 0f).DistanceToThreat);
        Assert.AreEqual(9f, 둘.SensesFor(1, 0f, 0f).DistanceToThreat);
    }

    [Test]
    public void 사각_판정이_위협마다_다른_답을_낸다()
    {
        // 이것이 §22 게이트 2다. 같은 개체가 위협 A에는 "막혔다", 위협 B에는
        // "열렸다"고 답할 수 있어야, 둘이 등을 맞대는 구도가 규칙에서 나온다.
        var 둘 = 목록(위협(5f, 빛안: true), 위협(9f, 빛안: false));

        Assert.AreEqual(LightVerdict.Blocked,
                        CreatureDecision.JudgeLight(낫, 둘.SensesFor(0, 0f, 0f)));
        Assert.AreEqual(LightVerdict.Clear,
                        CreatureDecision.JudgeLight(낫, 둘.SensesFor(1, 0f, 0f)));
    }

    [Test]
    public void 고른_위협이_빛_안이면_막힌다()
    {
        var 둘 = 목록(위협(5f, 빛안: true), 위협(9f, 빛안: false));
        Assert.AreEqual(CreatureIntent.Wander, CreatureDecision.NextIntent(낫, 둘, 0f, 0f));
    }

    [Test]
    public void 고른_위협이_사각이면_다른_쪽이_빛_안이어도_덤빈다()
    {
        // 지금 규칙(가장 가까운 것)의 직접적인 귀결이다. 뒤에 선 사람이 랜턴을
        // 켜고 있어도, 앞에 나온 사람이 사각이면 그쪽으로 온다.
        var 둘 = 목록(위협(5f, 빛안: false), 위협(9f, 빛안: true));
        Assert.AreEqual(CreatureIntent.Chase, CreatureDecision.NextIntent(낫, 둘, 0f, 0f));
    }

    [Test]
    public void 내가_빛_안인가는_위협과_무관하게_모두에_실린다()
    {
        // 개체 자신이 서 있는 자리는 어느 위협을 보든 같다. 목록 밖에 두는 이유다.
        var 둘 = 목록(위협(5f), 위협(9f));
        Assert.IsTrue(둘.SensesFor(0, 0f, 0f, selfInLight: true).SelfInLight);
        Assert.IsTrue(둘.SensesFor(1, 0f, 0f, selfInLight: true).SelfInLight);
    }

    [Test]
    public void 내가_빛_안이면_고른_상대가_사각이어도_물러난다()
    {
        var 둘 = 목록(위협(5f, 빛안: false), 위협(9f, 빛안: false));
        Assert.AreEqual(CreatureIntent.Flee,
                        CreatureDecision.NextIntent(낫, 둘, 0f, 0f, selfInLight: true));
    }

    [Test]
    public void 없는_자리를_물으면_위협_없음으로_답한다()
    {
        // 몸이 고른 자리를 잘못 넘겼을 때 조용히 엉뚱한 위협을 보는 것보다
        // "아무도 없다"가 낫다 — 그러면 개체가 배회로 돌아갈 뿐이다.
        var 하나 = 목록(위협(5f));
        Assert.AreEqual(float.MaxValue, 하나.SensesFor(-1, 0f, 0f).DistanceToThreat);
        Assert.AreEqual(float.MaxValue, 하나.SensesFor(7, 0f, 0f).DistanceToThreat);
    }

    // ── 목격 판정: 고르는 것이 아니라 있고 없음이다 ────────────────

    [Test]
    public void 아무도_없으면_목격도_없다()
    {
        Assert.IsFalse(ThreatRoster.Empty.AnyWithin(40f));
    }

    [Test]
    public void 목격_경계_위는_안이다()
    {
        // 감지·사거리와 같은 문법이다(<=). 경계 비교가 갈리면 감촉이 갈린다.
        Assert.IsTrue(목록(위협(40f)).AnyWithin(40f));
        Assert.IsFalse(목록(위협(40.01f)).AnyWithin(40f));
    }

    [Test]
    public void 둘_중_하나만_안에_있어도_목격이다()
    {
        Assert.IsTrue(목록(위협(200f), 위협(5f)).AnyWithin(40f));
        Assert.IsFalse(목록(위협(200f), 위협(41f)).AnyWithin(40f));
    }

    // ── 그릇 자체 ──────────────────────────────────────────────

    [Test]
    public void 담은_것을_그대로_읽는다()
    {
        var 둘 = 목록(위협(5f, 빛안: true), 위협(9f));

        Assert.AreEqual(2, 둘.Count);
        Assert.IsTrue(둘.HasThreat);
        Assert.AreEqual(5f, 둘[0].Distance);
        Assert.IsTrue(둘[0].InLight);
        Assert.AreEqual(9f, 둘[1].Distance);
        Assert.IsFalse(둘[1].InLight);
    }

    [Test]
    public void 하나짜리_창구도_같은_그릇을_만든다()
    {
        var 하나 = ThreatRoster.One(6f, inLight: true);
        Assert.AreEqual(1, 하나.Count);
        Assert.AreEqual(6f, 하나.NearestDistance);
        Assert.IsTrue(하나.Senses(0f, 0f).ThreatInLight);
    }

    [Test]
    public void 목록의_내용이_바뀌면_답도_따라_바뀐다()
    {
        // 몸 쪽이 목록 하나를 재사용해 매 프레임 갈아 끼운다. 그릇이 내용을
        // 복사해 버리면 그 방식이 조용히 깨진다.
        var 실제목록 = new List<ThreatSighting> { 위협(30f) };
        var 그릇 = ThreatRoster.From(실제목록);
        Assert.AreEqual(30f, 그릇.NearestDistance);

        실제목록[0] = 위협(4f);
        Assert.AreEqual(4f, 그릇.NearestDistance);

        실제목록.Clear();
        Assert.IsFalse(그릇.HasThreat);
        Assert.AreEqual(float.MaxValue, 그릇.NearestDistance);
    }
}
