using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Survive.Creatures;
using Survive.Domain.Art;
using Survive.World;

/// <summary>
/// 조명탄 — <b>빛이 방어에서 공격으로 승격하는 지점</b> (기획서 §5.2).
///
/// <b>여기서 지키는 것 셋.</b>
/// <list type="number">
/// <item><b>조명탄이 밝은 구역을 만든다.</b> 랜턴보다 넓고, 시간이 지나면 사라진다</item>
/// <item><b>붙어 있던 개체가 물러난다.</b> <c>Attack → Beware</c>가 실제로 일어난다.
///       그것도 <b>낫 규칙을 한 줄도 새로 쓰지 않고</b> — 조명탄은 등록부에
///       고정 광원으로 들어갈 뿐이고, 그 뒤는 이미 서 있던 길이 전부 한다</item>
/// <item><b>랜턴과 역할이 안 겹친다.</b> 조명탄으로 랜턴을 대신할 수 없다는 것을
///       규칙으로 보인다 — 지속이 유한하고, 들고 다니는 빛이 아니고,
///       보려고 쏘면 언제나 손해다</item>
/// </list>
///
/// <b>씬을 띄우지 않는다.</b> <see cref="FlareZone"/>이 순수 C#이라 시계를 손으로
/// 돌려 경계값을 전수로 볼 수 있고, 낫 쪽도 순수 함수(<see cref="ScytheFsm"/>)라
/// 같은 자리에서 이어 붙는다.
/// </summary>
public class FlareRuleTests
{
    /// <summary>사람을 따라다니는 광원. 랜턴을 씬 없이 흉내낸다 — 값은 실제 티어 1이다.</summary>
    class 가짜랜턴 : IOffsetLitSource
    {
        public Vector3 LitAnchor { get; set; }
        public Vector3 LitForward { get; set; } = Vector3.forward;
        public float LitZoneRadius { get; set; } = LanternRule.RadiusForTier(1);
        public bool IsLit { get; set; } = true;

        public Vector3 LitZoneCenter =>
            LanternRule.LitCenter(LitAnchor, LitForward, LanternRule.OffsetForTier(1));
    }

    [SetUp]
    public void 초기화() => LitZoneRegistry.Clear();

    [TearDown]
    public void 정리() => LitZoneRegistry.Clear();

    static CreatureDefinitionSO 낫정의()
    {
        const string 경로 = "Assets/08.Data/Creatures/낫.asset";
        var def = AssetDatabase.LoadAssetAtPath<CreatureDefinitionSO>(경로);
        Assert.IsNotNull(def, 경로 + "를 못 읽었다");
        return def;
    }

    // ══ ① 조명탄이 밝은 구역을 만든다 ═══════════════════════

    [Test]
    public void 조명탄은_랜턴보다_넓다()
    {
        // <b>어느 티어의 랜턴보다도 넓어야 한다.</b> 조명탄은 티어 3 제작물이라
        // 랜턴 업그레이드보다 뒤에 오므로, 티어 1만 이기게 두면 정작 쓰는 자리에서
        // 「랜턴보다 범위가 크다」가 거짓이 된다.
        Assert.IsTrue(FlareRule.OutgrowsEveryLantern,
            $"조명탄 {FlareRule.Radius}m가 랜턴 최고 티어 " +
            $"{LanternRule.RadiusForTier(LanternRule.MaxTier)}m를 못 넘는다");

        for (int tier = 1; tier <= LanternRule.MaxTier; tier++)
            Assert.Greater(FlareRule.Radius, LanternRule.RadiusForTier(tier),
                $"티어 {tier} 랜턴보다 좁다");
    }

    [Test]
    public void 갓_터진_조명탄은_반경_안을_밝힌다()
    {
        var 자리 = new Vector3(10f, 0f, -4f);
        var 탄 = new FlareZone(자리);
        LitZoneRegistry.Register(탄);

        Assert.IsTrue(LitZoneRegistry.IsLit(자리), "터진 자리가 어둡다");
        Assert.IsTrue(LitZoneRegistry.IsLit(자리 + new Vector3(FlareRule.Radius - 0.1f, 0f, 0f)),
                      "가장자리 안쪽이 어둡다");
    }

    [Test]
    public void 경계는_안으로_치고_그_너머는_어둡다()
    {
        // 등록부(LitZoneRegistry.IsLit)와 같은 부등호라야 규칙과 화면이 같은 답을 낸다.
        var 탄 = new FlareZone(Vector3.zero);
        LitZoneRegistry.Register(탄);

        Assert.IsTrue(LitZoneRegistry.IsLit(new Vector3(FlareRule.Radius, 0f, 0f)),
                      "정확히 반경 위는 안이다");
        Assert.IsFalse(LitZoneRegistry.IsLit(new Vector3(FlareRule.Radius + 0.01f, 0f, 0f)),
                       "반경 너머는 밖이다");
    }

    [Test]
    public void 다_타면_사라진다()
    {
        var 탄 = new FlareZone(Vector3.zero);
        LitZoneRegistry.Register(탄);

        // 다 타기 직전까지는 켜져 있다.
        탄.Tick(FlareRule.BurnSeconds - 0.01f);
        Assert.IsTrue(탄.IsLit, "다 타기 전에 꺼졌다");
        Assert.IsTrue(LitZoneRegistry.IsLit(Vector3.zero));
        Assert.Greater(탄.SecondsLeft, 0f);

        // 정확히 지속 시간이면 다 탄 것이다.
        탄.Tick(0.01f);
        Assert.IsFalse(탄.IsLit, $"{FlareRule.BurnSeconds}초를 넘겼는데 아직 탄다");
        Assert.AreEqual(0f, 탄.SecondsLeft, 0.0001f);
        Assert.IsFalse(LitZoneRegistry.IsLit(Vector3.zero),
                       "다 탄 조명탄이 등록부에서 안 빠졌다 — 자리가 영영 밝아진다");
    }

    [Test]
    public void 다_탄_조명탄은_어디도_밝히지_않는다()
    {
        var 탄 = new FlareZone(Vector3.zero);
        탄.Snuff();

        Assert.IsFalse(탄.Covers(Vector3.zero), "중심조차 밝히면 안 된다");
        Assert.IsFalse(탄.IsLit);
    }

    [Test]
    public void 시계는_뒤로_가지_않는다()
    {
        var 탄 = new FlareZone(Vector3.zero);
        탄.Tick(5f);
        탄.Tick(-3f);
        Assert.AreEqual(5f, 탄.Age, 0.0001f, "음수 시간이 조명탄을 되살렸다");
    }

    // ══ ② 붙어 있던 개체가 물러난다 ═════════════════════════
    //
    // <b>이 절이 이 라운드의 알맹이다.</b> 낫 규칙을 한 줄도 새로 쓰지 않았다는
    // 것을 여기서 보인다 — 조명탄은 등록부에 고정 광원으로 들어갈 뿐이고,
    // 그 뒤는 이미 서 있던 세 길이 이어 붙는다.
    //   LitZoneRegistry.IsBlindSide (고정 광원이 사각을 메운다)
    //     → CreatureDecision.JudgeLight (빛 안이면 Retreat)
    //       → ScytheFsm.Next (Clear가 아니면 Attack에서 Beware로)

    /// <summary>사람 등 뒤 사각에 붙은 낫의 자리. 랜턴이 지켜 주지 못하는 그 자리다.</summary>
    static Vector3 등뒤사각(float attackRange) =>
        new Vector3(0f, 0f, -Mathf.Max(LanternRule.BackReachForTier(1) + 0.2f, attackRange * 0.5f));

    [Test]
    public void 조명탄은_붙어_있는_거리까지_삼킨다()
    {
        // 사람 발밑에 터진 조명탄이 공격 거리 안을 다 덮지 못하면, 등에 붙은 개체가
        // 여전히 어둠에 서 있게 되고 조명탄은 <b>이미 붙은 것을 떼어내지 못한다</b>.
        // 그러면 랜턴과 하는 일이 같아진다.
        float 사거리 = 낫정의().attackRange;
        Assert.IsTrue(FlareRule.PeelsOffAttacker(사거리),
            $"조명탄 {FlareRule.Radius}m가 낫 공격 거리 {사거리}m를 못 덮는다");
    }

    [Test]
    public void 조명탄이_터지기_전에는_등_뒤가_사각이다()
    {
        // 이 절의 전제다. 사각이 애초에 없으면 "떼어냈다"를 잴 수가 없다.
        var def = 낫정의();
        LitZoneRegistry.Register(new 가짜랜턴 { LitAnchor = Vector3.zero });

        var 낫자리 = 등뒤사각(def.attackRange);
        Assert.IsTrue(LitZoneRegistry.IsBlindSide(낫자리),
                      $"랜턴만 켜져 있는데 등 뒤 {낫자리.z:F2}m가 사각이 아니다");

        var 상황 = 상황을_읽는다(def, 낫자리, Vector3.zero);
        Assert.AreEqual(LightVerdict.Clear, 상황.Light, "사각인데 빛이 앞을 막는다고 나온다");
        Assert.AreEqual(ScytheState.Attack, ScytheFsm.Next(ScytheState.Attack, 상황),
                        "랜턴만으로 교전이 풀렸다 — 그것은 조명탄의 몫이다");
    }

    [Test]
    public void 조명탄이_터지면_붙어_있던_개체가_물러난다()
    {
        var def = 낫정의();
        LitZoneRegistry.Register(new 가짜랜턴 { LitAnchor = Vector3.zero });

        var 낫자리 = 등뒤사각(def.attackRange);
        var 탄 = new FlareZone(Vector3.zero);
        LitZoneRegistry.Register(탄);

        // 고정 광원이 사각을 메운다. 이 한 줄이 랜턴과 조명탄을 가른다.
        Assert.IsFalse(LitZoneRegistry.IsBlindSide(낫자리),
                       "조명탄이 터졌는데 등 뒤가 여전히 내준 쪽이다");
        Assert.IsTrue(LitZoneRegistry.IsLitByFixed(낫자리), "조명탄이 고정 광원으로 안 잡힌다");

        var 상황 = 상황을_읽는다(def, 낫자리, Vector3.zero);
        Assert.AreEqual(LightVerdict.Retreat, 상황.Light,
                        "빛 안에 선 개체가 물러나지 않는다");

        Assert.AreEqual(ScytheState.Beware, ScytheFsm.Next(ScytheState.Attack, 상황),
                        "Attack → Beware가 일어나지 않았다 — 붙은 것이 안 떨어진다");
    }

    [Test]
    public void 사람이_조명탄_안에_서_있으면_따라붙기까지_풀린다()
    {
        // 「쫓아낸다」의 끝은 교전을 내리는 것이 아니라 <b>순찰로 돌려보내는 것</b>이다.
        // 고정 광원만이 따라붙기를 푼다는 규칙(ScytheFsm.Releases)에 조명탄이
        // 그대로 올라탄다 — 랜턴은 아무리 켜도 이 자리에 못 온다.
        var def = 낫정의();
        LitZoneRegistry.Register(new 가짜랜턴 { LitAnchor = Vector3.zero });
        LitZoneRegistry.Register(new FlareZone(Vector3.zero));

        var 상황 = 상황을_읽는다(def, 등뒤사각(def.attackRange), Vector3.zero);
        Assert.IsTrue(상황.PlayerNearFixedLight, "사람이 조명탄 안인데 고정 조명이 아니라고 한다");
        Assert.AreEqual(ScytheState.Patrol, ScytheFsm.Next(ScytheState.Beware, 상황),
                        "조명탄 안에 선 사람을 낫이 계속 따라붙는다");
    }

    [Test]
    public void 조명탄이_다_타면_사각이_돌아온다()
    {
        // 유한하다는 것이 값을 하는 자리다. 꺼지면 등 뒤는 다시 내준 쪽이 되고,
        // 그래서 조명탄은 「한 번의 창」이지 거점이 아니다.
        var def = 낫정의();
        LitZoneRegistry.Register(new 가짜랜턴 { LitAnchor = Vector3.zero });

        var 탄 = new FlareZone(Vector3.zero);
        LitZoneRegistry.Register(탄);

        var 낫자리 = 등뒤사각(def.attackRange);
        Assert.IsFalse(LitZoneRegistry.IsBlindSide(낫자리));

        탄.Tick(FlareRule.BurnSeconds);

        Assert.IsTrue(LitZoneRegistry.IsBlindSide(낫자리), "다 탄 조명탄이 아직 등 뒤를 메우고 있다");
        Assert.AreEqual(ScytheState.Attack,
                        ScytheFsm.Next(ScytheState.Attack, 상황을_읽는다(def, 낫자리, Vector3.zero)),
                        "다 탄 뒤에도 낫이 못 붙는다");
    }

    [Test]
    public void 밀려날_자리는_빛의_가장자리다()
    {
        // 「밀어내기 거리」가 곧 반경이라는 판단이 여기서 값을 한다. 밀려나야 하는
        // 자리는 <b>빛이 끝나는 선</b>이고, 그 선을 한 뼘이라도 넘어서면 어둡다 —
        // 두 값을 따로 두면 "빛은 여기까지인데 밀리는 것은 저기까지"가 되어
        // 플레이어가 화면으로 규칙을 읽을 수 없다.
        var 탄 = new FlareZone(Vector3.zero);

        foreach (var 시작 in new[]
                 {
                     new Vector3(0.5f, 0f, 0f),
                     new Vector3(0f, 0f, -1.2f),
                     new Vector3(3f, 0f, 4f),
                     new Vector3(FlareRule.Radius - 0.5f, 0f, 0f),
                 })
        {
            var 밀린곳 = 탄.PushTargetFor(시작);
            Assert.AreEqual(FlareRule.PushDistance,
                            new Vector2(밀린곳.x, 밀린곳.z).magnitude, 0.001f,
                            $"{시작}에서 밀린 자리가 밀어내기 거리에 안 선다");

            // 안쪽은 아직 빛이고 바깥은 어둡다. 물러나는 개체가 실제로 벗어나는
            // 문턱이 정확히 여기라는 것.
            Vector3 안쪽 = Vector3.MoveTowards(밀린곳, Vector3.zero, 0.05f);
            Vector3 바깥 = Vector3.MoveTowards(밀린곳, 밀린곳 * 2f, 0.05f);
            Assert.IsTrue(FlareRule.Covers(Vector3.zero, 안쪽), $"{시작}: 문턱 안쪽이 어둡다");
            Assert.IsFalse(FlareRule.Covers(Vector3.zero, 바깥), $"{시작}: 문턱 바깥이 아직 밝다");
        }
    }

    [Test]
    public void 중심과_겹쳐_있으면_밀_방향이_없다()
    {
        // 도주 규칙(CreatureNavigation.FleeDestination)이 같은 처리를 한다.
        // 다음 프레임이면 조금이라도 어긋나 방향이 생긴다.
        var 탄 = new FlareZone(Vector3.zero);
        Assert.AreEqual(Vector3.zero, 탄.PushTargetFor(Vector3.zero));
    }

    /// <summary>
    /// 낫이 이번 프레임에 보는 것. <b>여기서 새로 판정하는 것은 하나도 없다</b> —
    /// 전부 등록부(<see cref="LitZoneRegistry"/>)와 판단
    /// (<see cref="CreatureDecision"/>)에 그대로 묻는다. 그래야 이 테스트가
    /// 게임이 아니라 흉내를 재는 일이 없다.
    /// </summary>
    static ScytheSituation 상황을_읽는다(CreatureDefinitionSO def, Vector3 낫자리, Vector3 사람자리)
    {
        var traits = CreatureTraits.From(def);
        float 거리 = Vector3.Distance(낫자리, 사람자리);

        var senses = new CreatureSenses(
            거리, def.aggroSeconds, 0f,
            selfInLight: LitZoneRegistry.IsLit(낫자리),
            threatInLight: LitZoneRegistry.IsLit(사람자리),
            threatBlindSide: LitZoneRegistry.IsBlindSide(낫자리));

        return new ScytheSituation(
            CreatureDecision.IsDetected(거리, traits.DetectRadius),
            CreatureDecision.JudgeLight(traits, senses),
            closing: true,
            playerNearFixedLight: LitZoneRegistry.IsLitByFixed(사람자리));
    }

    // ══ ③ 랜턴과 역할이 겹치지 않는다 ═══════════════════════

    [Test]
    public void 조명탄은_랜턴을_대신할_수_없다()
    {
        // <b>지속이 유한하다.</b> 랜턴은 배터리가 있는 한 계속 켜져 있고,
        // 조명탄은 탄다. 그 차이가 「거점」과 「한 번의 창」을 가른다.
        Assert.IsFalse(FlareRule.CanReplaceLantern);
        Assert.Greater(FlareRule.BurnSeconds, 0f, "지속이 0이면 터지는 순간 꺼진다");
        Assert.Less(FlareRule.BurnSeconds, LanternRule.FullBatterySecondsAtTier1,
            $"조명탄 {FlareRule.BurnSeconds}초가 랜턴 한 셀 " +
            $"{LanternRule.FullBatterySecondsAtTier1}초만큼 간다 — 그러면 던지는 랜턴이다");
    }

    [Test]
    public void 조명탄은_들고_다니는_빛이_아니다()
    {
        // <b>형이 못 박는다.</b> IOffsetLitSource가 아니므로 사람을 따라오지도
        // 않고 앞뒤도 없다. 그 두 성질이 곧 「등 뒤 사각을 메우는 자격」이고
        // (LitZoneRegistry.IsBlindSide), 랜턴이 절대 가질 수 없는 것이다.
        var 탄 = new FlareZone(new Vector3(3f, 0f, 7f));

        Assert.IsFalse(typeof(IOffsetLitSource).IsAssignableFrom(typeof(FlareZone)),
            "조명탄이 사람을 따라다니는 광원으로 등록된다 — 그러면 그것은 랜턴이다");

        // 중심은 바뀌지 않는다. 사람이 어디로 가든 빛은 박힌 자리에 남는다.
        Assert.AreEqual(new Vector3(3f, 0f, 7f), 탄.LitZoneCenter);
        탄.Tick(3f);
        Assert.AreEqual(new Vector3(3f, 0f, 7f), 탄.LitZoneCenter, "타는 동안 자리가 움직였다");
    }

    [Test]
    public void 보려고_쏘면_언제나_손해다()
    {
        // 이것이 뒤집히면 조명탄이 <b>더 싼 랜턴</b>이 되고, 쏘아 두고 그 안에서
        // 지내는 것이 최적해가 된다 — 그 순간 두 물건의 역할이 겹친다.
        Assert.IsTrue(FlareRule.ForfeitsMoreThanItBurns,
            $"한 발이 랜턴 {FlareRule.LanternSecondsForfeited:F1}초를 태우는데 " +
            $"{FlareRule.BurnSeconds}초를 밝힌다 — 밝히려고 쏘는 것이 이득이 된다");
    }

    [Test]
    public void 배터리는_랜턴과_같은_통에서_먹는다()
    {
        // "빛을 지키는 데 쓸 것인가 쫓아내는 데 쓸 것인가"가 선택이 되려면
        // 같은 눈금을 깎아야 한다. 한 발이 가득 찬 배터리를 넘으면 그 선택 자체가 없다.
        Assert.Greater(FlareRule.BatteryCost, 0f, "공짜로 쏘면 선택이 사라진다");
        Assert.LessOrEqual(FlareRule.BatteryCost, LanternRule.MaxBattery,
                           "가득 채워도 한 발을 못 쏜다");
        Assert.Greater(FlareRule.ShotsPerFullBattery, 1,
                       "한 셀에 한 발이면 「몇 발 들고 나갈 것인가」가 셀 수와 같은 말이 된다");
    }

    [TestCase(0f, false, TestName = "빈_배터리로는_못_쏜다")]
    [TestCase(FlareRule.BatteryCost - 0.01f, false, TestName = "한_발이_모자라면_못_쏜다")]
    [TestCase(FlareRule.BatteryCost, true, TestName = "정확히_한_발_값이면_쏜다")]
    [TestCase(100f, true, TestName = "가득_차_있으면_쏜다")]
    public void 배터리_경계값(float 배터리, bool 쏘는가)
    {
        Assert.AreEqual(쏘는가, FlareRule.CanFire(배터리));
    }

    [Test]
    public void 쏘면_배터리가_그만큼_줄고_0_아래로는_안_간다()
    {
        Assert.AreEqual(LanternRule.MaxBattery - FlareRule.BatteryCost,
                        FlareRule.AfterFire(LanternRule.MaxBattery), 0.0001f);
        Assert.AreEqual(0f, FlareRule.AfterFire(0f), 0.0001f);
    }

    // ══ 색과 사거리 ═════════════════════════════════════════

    [Test]
    public void 조명탄은_자홍이다()
    {
        // 매크로늄 석영으로 만들었으므로 재료의 색이 그대로 간다(기획서 §7).
        // 다섯 번째 광원 색을 만들지 않는다.
        Assert.IsTrue(LightRule.IsAllowedColor(FlareRule.Color),
            "조명탄 색이 광원 4색 밖이다: #" + ColorUtility.ToHtmlStringRGB(FlareRule.Color));
        Assert.AreEqual(ColorUtility.ToHtmlStringRGB(ArtPalette.Macronium),
                        ColorUtility.ToHtmlStringRGB(FlareRule.Color),
                        "석영으로 만든 것이 매크로늄의 색이 아니다");
    }

    [Test]
    public void 날아가서_박힌다()
    {
        // 「총」이므로 발밑에 놓는 물건이 아니다. 먼저 비우고 들어가려면
        // 내가 아직 없는 자리를 밝힐 수 있어야 한다(기획서 §5.2).
        Assert.Greater(FlareRule.MaxThrowDistance, FlareRule.Radius,
            $"사거리 {FlareRule.MaxThrowDistance}m가 반경 {FlareRule.Radius}m보다 짧다 — " +
            "최대로 쏴도 제 발밑이 밝아지므로 「저쪽을 비운다」가 성립하지 않는다");

        var 눈 = new Vector3(0f, 1.6f, 0f);
        Assert.IsFalse(FlareRule.Covers(FlareRule.FarEnd(눈, Vector3.forward), 눈),
                       "최대 사거리로 쏜 조명탄이 쏜 사람까지 밝힌다");
    }

    [Test]
    public void 맞은_자리에_박힌다()
    {
        // 벽이든 바닥이든 맞은 면에서 법선 쪽으로 조금 띄운다 — 파묻히면 빛이 잘린다.
        var 눈 = new Vector3(0f, 1.6f, 0f);
        var 맞은곳 = new Vector3(0f, 1.6f, 12f);

        var 박힌곳 = FlareRule.ImpactPoint(눈, Vector3.forward,
                                          hitAhead: true, hitPoint: 맞은곳, hitNormal: Vector3.back,
                                          foundGround: false, groundPoint: Vector3.zero);

        Assert.AreEqual(맞은곳 + Vector3.back * FlareRule.GroundClearance, 박힌곳);
    }

    [Test]
    public void 아무것도_못_맞히면_날아간_끝의_발밑이다()
    {
        var 눈 = new Vector3(0f, 1.6f, 0f);
        var 바닥 = new Vector3(0f, 0f, FlareRule.MaxThrowDistance);

        var 박힌곳 = FlareRule.ImpactPoint(눈, Vector3.forward,
                                          hitAhead: false, hitPoint: Vector3.zero, hitNormal: Vector3.zero,
                                          foundGround: true, groundPoint: 바닥);

        Assert.AreEqual(바닥 + Vector3.up * FlareRule.GroundClearance, 박힌곳);
    }

    [Test]
    public void 발밑도_없으면_날아간_끝이다()
    {
        // 바다 위로 쏘면 이 자리다. 허공에서 타는 것이 옳은 답인 유일한 경우다.
        var 눈 = new Vector3(0f, 1.6f, 0f);

        var 박힌곳 = FlareRule.ImpactPoint(눈, Vector3.forward,
                                          hitAhead: false, hitPoint: Vector3.zero, hitNormal: Vector3.zero,
                                          foundGround: false, groundPoint: Vector3.zero);

        Assert.AreEqual(눈 + Vector3.forward * FlareRule.MaxThrowDistance, 박힌곳);
    }

    [Test]
    public void 겨눈_쪽이_없으면_총구_자리다()
    {
        // 정확히 위나 아래를 볼 때 랜턴이 미는 것을 그만두는 것과 같은 처리다.
        var 눈 = new Vector3(2f, 1.6f, 3f);
        Assert.AreEqual(눈, FlareRule.FarEnd(눈, Vector3.zero));
    }

    [Test]
    public void 맞은_면의_법선이_없으면_위로_띄운다()
    {
        var 맞은곳 = new Vector3(1f, 0f, 5f);
        var 박힌곳 = FlareRule.ImpactPoint(Vector3.zero, Vector3.forward,
                                          hitAhead: true, hitPoint: 맞은곳, hitNormal: Vector3.zero,
                                          foundGround: false, groundPoint: Vector3.zero);

        Assert.AreEqual(맞은곳 + Vector3.up * FlareRule.GroundClearance, 박힌곳);
    }
}
