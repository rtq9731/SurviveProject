using NUnit.Framework;
using UnityEngine;
using Survive.Creatures;
using Survive.World;

/// <summary>
/// 랜턴 오프셋 — <b>등 뒤 사각</b> (기획서 §9).
///
/// 랜턴이 상시 점등이 된 뒤로 빛 웅덩이가 사람을 정확히 가운데 놓고 따라다녔다.
/// 그러면 플레이어는 걸어다니는 안전지대이고, 죽는 길이 배터리 소진 하나로 줄어
/// <b>조명탄이 존재할 이유가 사라진다.</b> 원을 앞으로 밀어 등 뒤를 내주는 것이
/// 이 파일이 지키는 규칙이다.
///
/// 재는 것은 셋이다 — <b>앞이 길어졌는가 · 뒤가 어두워졌는가 · 오프셋 0이면
/// 예전 그대로인가.</b> 마지막 것이 회귀선이다: 이 변경은 판정의 모양을 바꾸지
/// 않고 중심만 옮긴 것이어야 한다.
/// </summary>
public class LanternOffsetTests
{
    const float 반경 = 8f;
    const float 오프셋 = 3f;

    static readonly Vector3 사람 = Vector3.zero;
    static readonly Vector3 정면 = Vector3.forward;

    static bool 밝은가(Vector3 자리, float r = 반경, float o = 오프셋) =>
        LanternRule.Covers(사람, 정면, r, o, 자리);

    // ── 회귀: 오프셋 0이면 예전 그대로다 ────────────────────────

    [Test]
    public void 오프셋이_0이면_중심이_사람_자리_그대로다()
    {
        Assert.AreEqual(사람, LanternRule.LitCenter(사람, 정면, 0f));
    }

    [Test]
    public void 오프셋이_0이면_사방_어디로도_반경까지_밝다()
    {
        // 예전 판정은 방향을 보지 않는 구였다. 그 성질이 남아 있어야
        // "중심만 옮겼다"는 말이 참이 된다.
        foreach (var d in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
                                  Vector3.up, Vector3.down })
        {
            Assert.IsTrue(밝은가(d * (반경 - 0.01f), 반경, 0f), $"{d} 안쪽");
            Assert.IsFalse(밝은가(d * (반경 + 0.01f), 반경, 0f), $"{d} 바깥");
        }
    }

    [Test]
    public void 오프셋이_0이면_어느_거리에도_사각이_없다()
    {
        // 원이 사람을 가운데 두면 사방이 대칭이다. 그때 등 뒤가 어두운 것은
        // 그냥 <b>먼 것</b>이지 사각이 아니다. 이 성질이 깨지면 오프셋을 0으로
        // 되돌려도 예전 동작으로 돌아가지 않는다.
        Assert.AreEqual(0f, LanternRule.BlindSpotDepth(반경, 0f), 1e-5f);

        for (float d = 0.5f; d <= 40f; d += 0.5f)
            Assert.IsFalse(LanternRule.IsBlindSpot(사람, 정면, 반경, 0f, Vector3.back * d),
                $"오프셋이 없는데 등 뒤 {d:F1}m가 사각이다");
    }

    [Test]
    public void 사각의_크기가_오프셋의_단조_함수다()
    {
        // 기획서 §9가 "랜턴 오프셋 (= 등 뒤 사각의 크기)"라고 한 등호다.
        float 앞값 = LanternRule.BlindSpotDepth(반경, 0f);
        for (float o = 0.5f; o <= 반경; o += 0.5f)
        {
            float 값 = LanternRule.BlindSpotDepth(반경, o);
            Assert.AreEqual(o, 값, 1e-4f, $"오프셋 {o}");
            Assert.Greater(값, 앞값, $"오프셋 {o}에서 사각이 커지지 않았다");
            앞값 = 값;

            // 그리고 그 크기만큼 등 뒤가 실제로 짧아진다.
            Assert.IsFalse(LanternRule.Covers(사람, 정면, 반경, o, Vector3.back * (반경 - o + 0.01f)));
            Assert.IsTrue(LanternRule.Covers(사람, 정면, 반경, o, Vector3.back * (반경 - o - 0.01f)));
        }
    }

    [Test]
    public void 같은_거리인데_앞은_밝고_뒤는_어두운_d가_있다()
    {
        // 스펙 §19 게이트 1. 그런 d가 하나라도 있어야 "사각이 있다"가 성립한다.
        bool 찾았다 = false;
        for (float d = 0.1f; d <= 20f; d += 0.1f)
        {
            if (!밝은가(Vector3.forward * d) || 밝은가(Vector3.back * d)) continue;
            찾았다 = true;
            break;
        }
        Assert.IsTrue(찾았다, "같은 거리에서 앞뒤가 갈리는 자리가 없다");
    }

    [Test]
    public void 시선이_돌면_같은_좌표의_밝기가_뒤집힌다()
    {
        // 스펙 §19 게이트 3. 세계 좌표는 그대로 두고 바라보는 쪽만 돌린다.
        // 옆 유효 거리(7.42m)보다 멀리 잡는다. 그보다 가까우면 옆을 봐도 닿으므로
        // "반 바퀴를 돌아야 지켜진다"를 재지 못한다.
        var 자리 = Vector3.back * 7.6f;

        Assert.IsFalse(LanternRule.Covers(사람, Vector3.forward, 반경, 오프셋, 자리), "등질 때");
        Assert.IsTrue(LanternRule.Covers(사람, Vector3.back, 반경, 오프셋, 자리), "마주볼 때");
        Assert.IsFalse(LanternRule.Covers(사람, Vector3.right, 반경, 오프셋, 자리), "옆을 볼 때");
    }

    // ── 앞으로 민 원 ────────────────────────────────────────────

    [Test]
    public void 중심이_바라보는_쪽으로_오프셋만큼_간다()
    {
        var 중심 = LanternRule.LitCenter(사람, 정면, 오프셋);
        Assert.AreEqual(오프셋, 중심.z, 1e-4f);
        Assert.AreEqual(0f, 중심.x, 1e-4f);
    }

    [Test]
    public void 정면_유효거리는_반경_더하기_오프셋이다()
    {
        Assert.IsTrue(밝은가(Vector3.forward * (반경 + 오프셋 - 0.01f)), "경계 바로 안");
        Assert.IsFalse(밝은가(Vector3.forward * (반경 + 오프셋 + 0.01f)), "경계 바로 밖");
    }

    [Test]
    public void 등_뒤는_반경_빼기_오프셋에서_끊긴다()
    {
        float 뒤 = 반경 - 오프셋;
        Assert.IsTrue(밝은가(Vector3.back * (뒤 - 0.01f)), "아직 빛 안");
        Assert.IsFalse(밝은가(Vector3.back * (뒤 + 0.01f)), "여기부터 사각");
    }

    [Test]
    public void 경계값은_밝은_쪽으로_친다()
    {
        // LitZoneRegistry가 <=로 판정한다. 같은 규칙이어야 두 창구가 갈리지 않는다.
        Assert.IsTrue(밝은가(Vector3.back * (반경 - 오프셋)));
        Assert.IsTrue(밝은가(Vector3.forward * (반경 + 오프셋)));
    }

    [Test]
    public void 옆도_함께_줄어든다()
    {
        // 피타고라스가 정하는 값이라 따로 돌릴 손잡이가 없다.
        float 옆 = Mathf.Sqrt(반경 * 반경 - 오프셋 * 오프셋);
        Assert.IsTrue(밝은가(Vector3.right * (옆 - 0.01f)));
        Assert.IsFalse(밝은가(Vector3.right * (옆 + 0.01f)));
        Assert.Less(옆, 반경, "원을 앞으로 밀었는데 옆이 안 줄었다");
    }

    [Test]
    public void 서_있는_자리는_언제나_밝다()
    {
        // 오프셋이 반경을 넘으면 제 발밑이 캄캄해진다. 티어 규칙이 그 일을
        // 만들지 않는지 전 티어에서 확인한다.
        for (int t = 1; t <= LanternRule.MaxTier; t++)
            Assert.Less(LanternRule.OffsetForTier(t), LanternRule.RadiusForTier(t),
                $"티어 {t}: 빛이 사람을 앞질렀다");
    }

    // ── 방향 ────────────────────────────────────────────────────

    [Test]
    public void 몸을_돌리면_사각도_따라_돈다()
    {
        // 정확히 반대로 서면 아까 어둡던 자리가 밝아진다.
        var 뒤쪽 = Vector3.back * (반경 - 오프셋 + 1f);
        Assert.IsFalse(LanternRule.Covers(사람, Vector3.forward, 반경, 오프셋, 뒤쪽));
        Assert.IsTrue(LanternRule.Covers(사람, Vector3.back, 반경, 오프셋, 뒤쪽));
    }

    [Test]
    public void 고개의_위아래는_사각을_바꾸지_못한다()
    {
        // 발밑을 보는 순간 사각이 사라지면, 위험해야 할 채집 자세가
        // 가장 안전한 자세가 된다 (기획서 §9의 의도가 거꾸로 뒤집힌다).
        var 내려다봄 = new Vector3(0f, -1f, 1f);
        var 올려다봄 = new Vector3(0f, 3f, 1f);

        var 기준 = LanternRule.LitCenter(사람, 정면, 오프셋);
        Assert.AreEqual(기준, LanternRule.LitCenter(사람, 내려다봄, 오프셋));
        Assert.AreEqual(기준, LanternRule.LitCenter(사람, 올려다봄, 오프셋));
    }

    [Test]
    public void 앞을_정할_수_없으면_밀지_않는다()
    {
        // 정확히 위를 보면 수평 성분이 없다. 아무 방향이나 고르면 사각이 순간이동한다.
        Assert.AreEqual(Vector3.zero, LanternRule.Facing(Vector3.up));
        Assert.AreEqual(사람, LanternRule.LitCenter(사람, Vector3.up, 오프셋));
    }

    [Test]
    public void 등_뒤_판정은_수평으로만_본다()
    {
        Assert.IsTrue(LanternRule.IsBehind(사람, 정면, new Vector3(0f, 20f, -1f)), "머리 위여도 뒤는 뒤다");
        Assert.IsFalse(LanternRule.IsBehind(사람, 정면, new Vector3(0f, -20f, 1f)), "발밑이어도 앞은 앞이다");
        Assert.IsFalse(LanternRule.IsBehind(사람, 정면, Vector3.right), "정확히 옆은 뒤가 아니다");
        Assert.IsFalse(LanternRule.IsBehind(사람, 정면, 사람), "제자리는 뒤가 아니다");
    }

    // ── 사각 ────────────────────────────────────────────────────

    [Test]
    public void 사각은_뒤이면서_어두운_자리다()
    {
        Assert.IsTrue(LanternRule.IsBlindSpot(사람, 정면, 반경, 오프셋, Vector3.back * 6f),
            "등 뒤 6m가 사각이 아니다");
        Assert.IsFalse(LanternRule.IsBlindSpot(사람, 정면, 반경, 오프셋, Vector3.back * 2f),
            "등 뒤라도 아직 빛 안이다");
        Assert.IsFalse(LanternRule.IsBlindSpot(사람, 정면, 반경, 오프셋, Vector3.forward * 30f),
            "멀어도 앞은 사각이 아니다");
    }

    [Test]
    public void 낫의_공격_거리가_사각_안에_들어와야_설계가_성립한다()
    {
        // 낫의 공격 거리는 2.2m다. 등 뒤 어둠이 시작되는 거리(반경 - 오프셋)가
        // 그보다 훨씬 멀면 낫은 사각에 <b>대기</b>할 수는 있어도 거기서 곧장
        // 때리지는 못한다 — 그때 사각은 "빠르면 파고들 수 있는 자리"가 된다.
        // 이 관계가 무너지는 것(뒤가 앞보다 길어지는 것)만은 막는다.
        Assert.Less(LanternRule.BackReachForTier(1), LanternRule.ForwardReachForTier(1),
            "등 뒤가 정면보다 멀리 밝다 — 오프셋의 부호가 뒤집혔다");
        Assert.Greater(LanternRule.ForwardReachForTier(1), LanternRule.RadiusForTier(1));
    }

    // ── 티어 ────────────────────────────────────────────────────

    [Test]
    public void 티어_0은_밀_빛이_없다()
    {
        Assert.AreEqual(0f, LanternRule.OffsetForTier(0), 1e-5f);
        Assert.AreEqual(0f, LanternRule.ForwardReachForTier(0), 1e-5f);
        Assert.AreEqual(0f, LanternRule.BackReachForTier(0), 1e-5f);
        Assert.IsFalse(LanternRule.Covers(사람, 정면, 0f, 0f, 사람), "랜턴이 없는데 밝다");
    }

    [Test]
    public void 티어를_올려도_등_뒤_사각은_그대로다()
    {
        // 등 뒤를 돈으로 사게 두면 상위 티어가 순수 상향이 되어 고를 것이 없어진다.
        float 기준 = LanternRule.BackReachForTier(1);
        for (int t = 2; t <= LanternRule.MaxTier; t++)
            Assert.AreEqual(기준, LanternRule.BackReachForTier(t), 1e-4f,
                $"티어 {t}에서 등 뒤가 밝아졌다");
    }

    [Test]
    public void 티어를_올리면_앞만_길어진다()
    {
        for (int t = 2; t <= LanternRule.MaxTier; t++)
            Assert.Greater(LanternRule.ForwardReachForTier(t),
                           LanternRule.ForwardReachForTier(t - 1),
                           $"티어 {t}가 앞을 넓히지 못했다");
    }

    [Test]
    public void 티어_반경이_등_뒤_몫보다_작으면_밀_것이_없다()
    {
        // 사람이 반경을 등 뒤 몫 아래로 내리면 오프셋이 0이 되어 예전 원으로 돌아간다.
        // 음수 오프셋(빛이 뒤로 가는 것)은 어떤 값을 넣어도 나오지 않아야 한다.
        for (int t = 0; t <= LanternRule.MaxTier + 2; t++)
            Assert.GreaterOrEqual(LanternRule.OffsetForTier(t), 0f, $"티어 {t}");
    }

    // ── 빛 판정이 방향을 안다 ───────────────────────────────────
    // 오프셋만 만들고 판정을 그대로 두면 사각은 화면에만 있고 규칙에는 없다.
    // 여기가 그 판정이 실제로 갈리는지 보는 자리다.

    static readonly CreatureTraits 낫 =
        new CreatureTraits(BehaviorProfile.Aggressive, 14f, 2.2f, avoidsLight: true);

    static CreatureSenses 감각(bool 내가빛속, bool 사람이빛속, bool 내준쪽) =>
        new CreatureSenses(9f, 0f, 0f, 내가빛속, 사람이빛속, 내준쪽);

    [Test]
    public void 내준_쪽에_서_있으면_빛이_막지_못한다()
    {
        // 기획서 §5 — 랜턴은 앞쪽만 지키고, 붙어 있는 개체를 떼어내지도 못한다.
        // 이 줄이 없으면 랜턴이 켜진 동안 어떤 경로로도 사람에게 닿을 수 없다.
        Assert.AreEqual(LightVerdict.Clear,
                        CreatureDecision.JudgeLight(낫, 감각(false, true, true)), "멀찍이 뒤");
        Assert.AreEqual(LightVerdict.Clear,
                        CreatureDecision.JudgeLight(낫, 감각(true, true, true)),
                        "이미 붙었으면 빛도 못 떼어낸다 — 그것은 조명탄의 몫이다");
    }

    [Test]
    public void 몸을_돌리면_그_자리가_다시_막힌다()
    {
        // 플레이어의 방어는 조작이다. 돌아서는 순간 내준 쪽이 아니게 되고
        // 예전 규칙이 그대로 되살아난다.
        Assert.AreEqual(LightVerdict.Blocked,
                        CreatureDecision.JudgeLight(낫, 감각(false, true, false)));
        Assert.AreEqual(LightVerdict.Retreat,
                        CreatureDecision.JudgeLight(낫, 감각(true, true, false)));
    }

    [Test]
    public void 오프셋이_0이면_판정이_예전과_같다()
    {
        // 밀린 것이 없으면 내준 쪽도 없다(false 고정). 그 아래에서 네 조합 전부가
        // 예전 표와 글자 그대로 같아야 한다.
        Assert.AreEqual(LightVerdict.Clear,
                        CreatureDecision.JudgeLight(낫, 감각(false, false, false)));
        Assert.AreEqual(LightVerdict.Blocked,
                        CreatureDecision.JudgeLight(낫, 감각(false, true, false)));
        Assert.AreEqual(LightVerdict.Retreat,
                        CreatureDecision.JudgeLight(낫, 감각(true, false, false)));
        Assert.AreEqual(LightVerdict.Retreat,
                        CreatureDecision.JudgeLight(낫, 감각(true, true, false)));
    }

    [Test]
    public void 빛을_꺼리지_않는_생물은_무엇도_달라지지_않았다()
    {
        // 기존 4종(눈·공·날개·열매게). 어떤 조합에서도 판정이 통과여야 한다.
        var 순한것 = new CreatureTraits(BehaviorProfile.Aggressive, 14f, 2.2f, avoidsLight: false);

        foreach (bool 내가빛속 in new[] { false, true })
        foreach (bool 사람이빛속 in new[] { false, true })
        foreach (bool 내준쪽 in new[] { false, true })
            Assert.AreEqual(LightVerdict.Clear,
                            CreatureDecision.JudgeLight(순한것, 감각(내가빛속, 사람이빛속, 내준쪽)),
                            $"내빛={내가빛속} 사람빛={사람이빛속} 내준쪽={내준쪽}");
    }

    [Test]
    public void 옆_거리는_반경과_오프셋에서_바로_나온다()
    {
        float r = LanternRule.RadiusForTier(1);
        float o = LanternRule.OffsetForTier(1);
        Assert.AreEqual(Mathf.Sqrt(r * r - o * o), LanternRule.SideReachForTier(1), 1e-4f);
        Assert.AreEqual(0f, LanternRule.SideReachForTier(0), 1e-5f);
    }
}
