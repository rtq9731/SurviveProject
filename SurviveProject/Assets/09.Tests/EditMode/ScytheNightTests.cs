using System;
using NUnit.Framework;
using Survive.Creatures;
using Survive.Progression;
using Survive.World;

/// <summary>
/// <b>낫은 밤에 다닌다</b> (세계관 §5 · 기획서 §5.14 · 스펙 §8).
///
/// 근거는 적의가 아니라 <b>은폐</b>다: "일반 생물들에게 방해받지 않기 위해 눈에 띄지
/// 않게 다닌다. 밤에는 더 보이지 않으므로 밤에 움직인다."
///
/// <b>여기서 재는 것의 절반은 「사라지지 않는다」다.</b> 낮에 개체를 지웠다가 밤에
/// 다시 만들면 그것은 자리를 옮긴 것이 아니라 다른 물건이고, 도감·관측이 세는
/// 「본 적 있다」가 흔들린다. 그래서 규칙이 답하는 것은 <b>어디까지 나오는가</b>이지
/// <b>있는가 없는가</b>가 아니다.
/// </summary>
public class ScytheNightTests
{
    // 하루의 눈금. DayNightCycle의 상수를 그대로 읽는다 — 여기 수를 베껴 적으면
    // 박명 길이를 조정할 때 이 파일이 조용히 거짓말을 한다.
    static float 해질녘시작 => DayNightCycle.DuskStart;   // 0.72
    static float 해질녘끝   => DayNightCycle.DuskEnd;     // 0.78
    static float 해뜰녘시작 => DayNightCycle.DawnStart;   // 0.22
    static float 해뜰녘끝   => DayNightCycle.DawnEnd;     // 0.28

    [SetUp]
    public void 등급을_평시로() => ScytheWatch.Reset();

    [TearDown]
    public void 되돌린다() => ScytheWatch.Reset();

    // ── 언제 나와 있는가 ───────────────────────────────────────

    [Test]
    public void 한밤중에는_나와_있다()
    {
        Assert.IsTrue(ScytheHabitat.IsAbroad(0.00f));
        Assert.IsTrue(ScytheHabitat.IsAbroad(0.10f));
        Assert.IsTrue(ScytheHabitat.IsAbroad(0.95f));
    }

    [Test]
    public void 한낮에는_물러나_있다()
    {
        Assert.IsFalse(ScytheHabitat.IsAbroad(0.35f));   // 시작 시각
        Assert.IsFalse(ScytheHabitat.IsAbroad(0.50f));   // 정오
        Assert.IsFalse(ScytheHabitat.IsAbroad(0.65f));
    }

    [Test]
    public void 빛이_기울기_시작하면_나온다()
    {
        // 해질녘이 <b>시작되는</b> 순간이 경계다. 박명이 끝나기를 기다리지 않는다 —
        // 어두워지기 시작하면 이미 눈에 덜 띈다.
        Assert.IsFalse(ScytheHabitat.IsAbroad(해질녘시작 - 0.001f), "해질녘 직전");
        Assert.IsTrue(ScytheHabitat.IsAbroad(해질녘시작), "해질녘이 시작되는 그 순간");
        Assert.IsTrue(ScytheHabitat.IsAbroad(해질녘끝), "해질녘이 끝나면 당연히");
    }

    [Test]
    public void 빛이_돌아오기_시작하면_물러난다()
    {
        Assert.IsTrue(ScytheHabitat.IsAbroad(해뜰녘시작 - 0.001f), "해뜰녘 직전까지는 나와 있다");
        Assert.IsFalse(ScytheHabitat.IsAbroad(해뜰녘시작), "해뜰녘이 시작되는 그 순간");
        Assert.IsFalse(ScytheHabitat.IsAbroad(해뜰녘끝), "해뜰녘이 끝나면 당연히");
    }

    [Test]
    public void 활동_시간이_끊기지_않은_한_구간이다()
    {
        // <b>박명 둘을 따로 정하지 않은 것이 요점이다.</b> 따로 정하면 경계가 넷이
        // 되고 그중 하나에서 값이 튀면 낫이 깜빡인다. 해질녘 시작에서 해뜰녘
        // 시작까지 <b>한 번만</b> 참에서 거짓으로 바뀌어야 한다.
        int 바뀐횟수 = 0;
        bool 앞 = ScytheHabitat.IsAbroad(0f);

        for (int i = 1; i <= 20000; i++)
        {
            float t = i / 20000f;
            bool 지금 = ScytheHabitat.IsAbroad(t);
            if (지금 != 앞) 바뀐횟수++;
            앞 = 지금;
        }

        Assert.AreEqual(2, 바뀐횟수,
                        "하루에 나오고 물러나는 것이 한 번씩이어야 한다 (경계 둘)");
    }

    [Test]
    public void 되감아도_같은_답이다()
    {
        // 시계는 앞으로도 뒤로도 간다(DayNightService.Skip은 음수를 받는다).
        // 같은 시각이면 언제 물어도 같은 답이어야 한다.
        foreach (float t in new[] { 0f, 0.21f, 0.22f, 0.5f, 0.72f, 0.79f, 0.999f })
        {
            bool 첫답 = ScytheHabitat.IsAbroad(t);
            for (int i = 0; i < 5; i++)
                Assert.AreEqual(첫답, ScytheHabitat.IsAbroad(t), $"t={t}");
        }
    }

    [Test]
    public void 시각이_한_바퀴를_돌아도_같은_답이다()
    {
        // Wrap을 지난 값으로 물어도 하루 안의 같은 시각과 답이 같아야 한다.
        foreach (float t in new[] { 0.05f, 0.35f, 0.75f })
        {
            float 다음날 = DayNightCycle.Wrap(DayNightCycle.SecondsAt(t) +
                                              DayNightCycle.DayLengthSeconds * 3);
            Assert.AreEqual(ScytheHabitat.IsAbroad(t), ScytheHabitat.IsAbroad(다음날),
                            $"t={t} vs {다음날}");
        }
    }

    // ── 발령은 낮을 이긴다 ─────────────────────────────────────

    [Test]
    public void 발령이면_낮에도_나와_있다()
    {
        // 발령은 은폐를 <b>버렸다</b>는 뜻이다. 숨을 이유가 사라진 개체가 해가
        // 떴다고 물러나면 그 말이 거짓이 된다. 그리고 등급은 월드가 건 것이라
        // 개체가 읽는 시계가 그것을 뚫으면 소유권을 월드에 둔 뜻이 없어진다.
        Assert.IsFalse(ScytheHabitat.IsAbroad(0.5f, ScytheAlert.Calm));
        Assert.IsTrue(ScytheHabitat.IsAbroad(0.5f, ScytheAlert.Alarmed));
    }

    [Test]
    public void 평시_밤은_등급과_무관하게_나와_있다()
    {
        Assert.IsTrue(ScytheHabitat.IsAbroad(0.9f, ScytheAlert.Calm));
        Assert.IsTrue(ScytheHabitat.IsAbroad(0.9f, ScytheAlert.Alarmed));
    }

    // ── 물러난다는 것이 무엇인가 ────────────────────────────────

    [Test]
    public void 낮에는_액면_위로만_물러난다()
    {
        // "없어진다"가 아니라 "좁아진다". 해안선을 잃으므로 물가에 선 사람에게
        // 닿지 않는다 — 낮과 밤이 갈리는 자리가 전부 이 한 줄에서 나온다.
        Assert.IsTrue(ScytheHabitat.CanEnter(HabitatZone.Liquid, ScytheAlert.Calm, abroad: false));
        Assert.IsFalse(ScytheHabitat.CanEnter(HabitatZone.Shore, ScytheAlert.Calm, abroad: false));
        Assert.IsFalse(ScytheHabitat.CanEnter(HabitatZone.Inland, ScytheAlert.Calm, abroad: false));
    }

    [Test]
    public void 밤이면_예전_범위_그대로다()
    {
        // <b>회귀선이다.</b> 시간 축을 더한 것이 밤의 답을 한 글자도 바꾸면 안 된다.
        foreach (var 구역 in (HabitatZone[])Enum.GetValues(typeof(HabitatZone)))
        foreach (var 등급 in new[] { ScytheAlert.Calm, ScytheAlert.Alarmed })
            Assert.AreEqual(ScytheHabitat.CanEnter(구역, 등급),
                            ScytheHabitat.CanEnter(구역, 등급, abroad: true),
                            $"{구역} {등급}");
    }

    [Test]
    public void 발령이면_낮에도_육지까지_간다()
    {
        // 발령이 Abroad를 참으로 만들므로 범위도 밤과 같아진다.
        bool 나와있다 = ScytheHabitat.IsAbroad(0.5f, ScytheAlert.Alarmed);
        Assert.IsTrue(ScytheHabitat.CanEnter(HabitatZone.Inland, ScytheAlert.Alarmed, 나와있다));
    }

    // ── 낮에는 사람을 보지 않는다 ───────────────────────────────

    [Test]
    public void 낮이면_감지해도_순찰뿐이다()
    {
        // 물러난다는 것은 자리만이 아니라 <b>관심</b>도 거두는 것이다. 여기서
        // 끊지 않으면 낮에도 낫이 물 위에서 사람을 쫓아다니며 꼬리를 들고 있다.
        var 낮에딱붙음 = new ScytheSituation(detected: true, LightVerdict.Clear, closing: true,
                                            playerNearFixedLight: false, pushedByFlare: false,
                                            alert: ScytheAlert.Calm, abroad: false);

        foreach (var 이전 in new[] { ScytheState.Patrol, ScytheState.Beware, ScytheState.Attack })
            Assert.AreEqual(ScytheState.Patrol, ScytheFsm.Next(이전, 낮에딱붙음), 이전.ToString());
    }

    [Test]
    public void 낮이어도_회수는_월드의_것이다()
    {
        // 시계가 월드의 지시를 뚫으면 안 된다. 짐을 든 개체는 낮이 와도 짐을 든 채다.
        var 낮 = new ScytheSituation(true, LightVerdict.Clear, true,
                                     false, false, ScytheAlert.Calm, abroad: false);

        Assert.AreEqual(ScytheState.Retrieve, ScytheFsm.Next(ScytheState.Retrieve, 낮));
        Assert.AreEqual(ScytheState.Retrieve,
                        ScytheFsm.Apply(ScytheState.Patrol, ScytheDirective.Retrieve, 낮));
    }

    [Test]
    public void 시간_축이_없던_부름은_예전과_같다()
    {
        // <b>회귀선이다.</b> abroad 기본값이 참이라, 이 축을 모르고 부르는 자리는
        // 밤인 것으로 친다 — 축이 붙기 전에 쓰인 테스트와 부름이 그대로 산다.
        var 옛것 = new ScytheSituation(detected: true, LightVerdict.Clear, closing: true);
        Assert.IsTrue(옛것.Abroad);
        Assert.AreEqual(ScytheState.Attack, ScytheFsm.Next(ScytheState.Beware, 옛것));
    }

    // ── 유물: 칠흑이 조건이다 ───────────────────────────────────

    [Test]
    public void 랜턴이_켜져_있으면_유물을_흘리지_않는다()
    {
        Assert.IsFalse(RelicDropRule.CanShed(playerWithinWitness: true, playerInLight: true));
    }

    [Test]
    public void 끄면_흘린다()
    {
        Assert.IsTrue(RelicDropRule.CanShed(playerWithinWitness: true, playerInLight: false));
    }

    [Test]
    public void 어두워도_멀면_흘리지_않는다()
    {
        // 곁에 있는 것과 어두운 것이 <b>둘 다</b> 필요하다. 하나만으로는 안 된다 —
        // 멀리서 불만 꺼도 얻어지면 "낫 곁에 서 있는 시간"이 사라진다.
        Assert.IsFalse(RelicDropRule.CanShed(playerWithinWitness: false, playerInLight: false));
        Assert.IsFalse(RelicDropRule.CanShed(playerWithinWitness: false, playerInLight: true));
    }

    [Test]
    public void 조건_둘의_곱이다()
    {
        foreach (bool 곁 in new[] { false, true })
        foreach (bool 빛 in new[] { false, true })
            Assert.AreEqual(곁 && !빛, RelicDropRule.CanShed(곁, 빛), $"곁={곁} 빛={빛}");
    }
}
