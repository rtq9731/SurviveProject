using NUnit.Framework;
using UnityEngine;
using Survive.Creatures;
using Survive.World;

/// <summary>
/// <b>「등 뒤 사각」이 지금 무슨 뜻인가</b>를 못 박는 자리.
///
/// 이 저장소에는 사각을 판정하는 곳이 <b>둘</b>이고, 둘의 뜻이 다르다.
/// <list type="bullet">
/// <item><see cref="LanternRule.IsBlindSpot"/> — <b>뒤이면서 빛 원 밖</b>.
///       경계가 있는 좁은 영역이다</item>
/// <item><see cref="LitZoneRegistry.IsBlindSide"/> — <b>뒤이기만 하면 된다</b>.
///       거리를 보지 않는 <b>반평면</b>이고, <b>낫이 실제로 읽는 것은 이쪽</b>이다</item>
/// </list>
///
/// <b>왜 갈렸는가.</b> 레지스트리 쪽이 일부러 원 밖 조건을 뺐다. 그 주석이 근거를
/// 적어 두었다 — 넣었더니 등 뒤 5m 언저리로 새는 빛 때문에 낫이 영영 닿지 못하고
/// 2.77m에서 정체했고, 기획서 §5는 "랜턴은 앞쪽만 지키고 등 뒤를 내준다"이지
/// "등 뒤 5m는 지킨다"가 아니다.
///
/// <b>결과로 나온 실측</b>(클론 A 튜닝 측량, 2026-08-07): 반경 8 · 오프셋 3 ·
/// 사거리 2.2에서 <b>기하학적 사각(빛 밖 ∩ 사거리 안)은 0.00 m²</b>인데
/// <see cref="LitZoneRegistry.IsBlindSide"/>가 참인 넓이는 사거리 원의 정확히 절반인
/// 7.70 m²였고, 낫이 때리는 시간의 96.4%를 「빛 안」에 서 있었다.
///
/// <b>어느 뜻이 참인가는 사람이 정한다.</b> 이 파일이 하는 일은 <b>지금 무엇이
/// 참인지를 값으로 적어 두는 것</b>뿐이다. 누가 반평면을 반경 제한으로 바꾸면
/// 여기가 먼저 빨개져서, 그것이 사고가 아니라 <b>결정</b>이었음을 남긴다.
/// 조용히 바뀌는 것이 최악이다.
/// </summary>
public class BlindSideMeaningTests
{
    /// <summary>사람이 든 랜턴 하나를 흉내 낸다. 값만 들고 있는 그릇이다.</summary>
    sealed class 랜턴흉내 : IOffsetLitSource
    {
        public Vector3 Anchor;
        public Vector3 Forward = Vector3.forward;
        public float Radius = 8f;      // 티어 1 실측
        public float Offset = 3f;      // 티어 1 실측

        public Vector3 LitZoneCenter => Anchor + Forward.normalized * Offset;
        public float LitZoneRadius => Radius;
        public bool IsLit => true;
        public Vector3 LitAnchor => Anchor;
        public Vector3 LitForward => Forward;
    }

    /// <summary>앞뒤가 없는 광원 — 화톳불·빛기둥.</summary>
    sealed class 화톳불흉내 : ILitZoneSource
    {
        public Vector3 Center;
        public float Radius = 9f;
        public Vector3 LitZoneCenter => Center;
        public float LitZoneRadius => Radius;
        public bool IsLit => true;
    }

    랜턴흉내 _랜턴;

    [SetUp]
    public void 세운다()
    {
        LitZoneRegistry.Clear();
        _랜턴 = new 랜턴흉내 { Anchor = Vector3.zero, Forward = Vector3.forward };
        LitZoneRegistry.Register(_랜턴);
    }

    [TearDown]
    public void 치운다() => LitZoneRegistry.Clear();

    static Vector3 뒤로(float m) => new Vector3(0f, 0f, -m);
    static Vector3 앞으로(float m) => new Vector3(0f, 0f, m);

    // ── 지금 참인 것: 반평면 ────────────────────────────────────

    [Test]
    public void 낫이_읽는_사각은_거리를_보지_않는_반평면이다()
    {
        // <b>이것이 지금의 뜻이다.</b> 사람 바로 뒤 30cm도, 100m 뒤도 똑같이 사각이다.
        Assert.IsTrue(LitZoneRegistry.IsBlindSide(뒤로(0.3f)), "코앞 뒤");
        Assert.IsTrue(LitZoneRegistry.IsBlindSide(뒤로(2.2f)), "사거리 위");
        Assert.IsTrue(LitZoneRegistry.IsBlindSide(뒤로(5f)), "등 뒤 도달선(반경 8 − 오프셋 3)");
        Assert.IsTrue(LitZoneRegistry.IsBlindSide(뒤로(100f)), "아주 멀리");

        // 앞쪽은 어디든 사각이 아니다.
        Assert.IsFalse(LitZoneRegistry.IsBlindSide(앞으로(0.3f)));
        Assert.IsFalse(LitZoneRegistry.IsBlindSide(앞으로(100f)));
    }

    [Test]
    public void 반평면이라_사거리_안의_절반이_사각이다()
    {
        // 클론 A의 실측(사거리 원의 정확히 절반)을 격자로 다시 센다.
        // 반평면이 반경 제한으로 바뀌면 이 비율이 0에 가까워진다.
        const float 사거리 = 2.2f;
        int 안 = 0, 사각 = 0;

        for (int ix = -40; ix <= 40; ix++)
        for (int iz = -40; iz <= 40; iz++)
        {
            var p = new Vector3(ix * 0.05f, 0f, iz * 0.05f);
            if (p.sqrMagnitude > 사거리 * 사거리) continue;

            안++;
            if (LitZoneRegistry.IsBlindSide(p)) 사각++;
        }

        float 비율 = (float)사각 / 안;
        Assert.That(비율, Is.EqualTo(0.5f).Within(0.02f),
                    $"사거리 안에서 사각인 비율 {비율:P1} — 반평면이면 절반이다");
    }

    [Test]
    public void 같은_자리를_다른_규칙은_사각이_아니라고_답한다()
    {
        // <b>둘의 뜻이 갈려 있다는 사실 자체</b>를 적어 둔다.
        // 사거리(2.2m) 안의 등 뒤 한 점: 레지스트리는 사각, 랜턴 규칙은 아니다.
        var p = 뒤로(2f);

        Assert.IsTrue(LitZoneRegistry.IsBlindSide(p),
                      "낫이 읽는 쪽 — 뒤이므로 사각");
        Assert.IsFalse(LanternRule.IsBlindSpot(_랜턴.Anchor, _랜턴.Forward,
                                               _랜턴.Radius, _랜턴.Offset, p),
                       "랜턴 규칙 쪽 — 아직 빛 원 안이므로 사각이 아니다");
    }

    [Test]
    public void 오프셋을_아무리_밀어도_반평면은_바뀌지_않는다()
    {
        // <b>「오프셋이 사각의 크기를 정한다」가 지금 코드에서 참이 아니다.</b>
        // 기획서 §9의 등호는 LanternRule.BlindSpotDepth 쪽에서만 성립한다.
        var p = 뒤로(2f);

        foreach (float 오프셋 in new[] { 0.5f, 3f, 7.9f })
        {
            _랜턴.Offset = 오프셋;
            Assert.IsTrue(LitZoneRegistry.IsBlindSide(p), $"오프셋 {오프셋}");
        }

        // 다만 오프셋이 0이면 사각이 아예 없다. 그 가지는 살아 있다 —
        // 웅덩이가 사람을 가운데 두면 내준 쪽이 없기 때문이다.
        _랜턴.Offset = 0f;
        Assert.IsFalse(LitZoneRegistry.IsBlindSide(p), "오프셋 0이면 내준 쪽도 없다");
    }

    [Test]
    public void 고정_조명은_반평면을_통째로_메운다()
    {
        // 이쪽은 반평면이어도 성립하는 규칙이다 — 화톳불 안이면 뒤라도 사각이 아니다.
        // 낫의 따라붙기를 푸는 것과 같은 사실의 다른 얼굴이다(스펙 §20).
        _랜턴.Offset = 3f;
        Assert.IsTrue(LitZoneRegistry.IsBlindSide(뒤로(2f)));

        LitZoneRegistry.Register(new 화톳불흉내 { Center = Vector3.zero, Radius = 9f });
        Assert.IsFalse(LitZoneRegistry.IsBlindSide(뒤로(2f)),
                       "고정 조명 안이면 누구의 등 뒤라도 내준 쪽이 아니다");
    }

    // ── 이 뜻이 FSM에 무엇을 하는가 ─────────────────────────────

    [Test]
    public void 반평면이라서_교전이_열린다()
    {
        // <b>Pounces가 기대는 것이 정확히 이 뜻이다.</b> 사거리 안의 등 뒤가 사각이므로
        // JudgeLight가 Clear를 내고, 그래서 낫이 랜턴을 켠 사람을 때릴 수 있다.
        // 반경 제한이 들어오면 사거리 안 사각이 0 m²가 되어 이 전이가 사실상 사라진다.
        var 낫 = new CreatureTraits(BehaviorProfile.Aggressive, 14f, 2.2f, avoidsLight: true);

        var 등뒤 = new CreatureSenses(2f, 0f, 0f, false,
                                     threatInLight: true,
                                     threatBlindSide: LitZoneRegistry.IsBlindSide(뒤로(2f)));
        Assert.AreEqual(LightVerdict.Clear, CreatureDecision.JudgeLight(낫, 등뒤));

        var 상황 = new ScytheSituation(detected: true, CreatureDecision.JudgeLight(낫, 등뒤),
                                      closing: true);
        Assert.AreEqual(ScytheState.Attack, ScytheFsm.Next(ScytheState.Beware, 상황));

        // 정면은 그대로 막힌다. 사람의 방어는 여전히 몸을 돌리는 것이다.
        var 정면 = new CreatureSenses(2f, 0f, 0f, false,
                                     threatInLight: true,
                                     threatBlindSide: LitZoneRegistry.IsBlindSide(앞으로(2f)));
        Assert.AreEqual(LightVerdict.Blocked, CreatureDecision.JudgeLight(낫, 정면));

        var 정면상황 = new ScytheSituation(detected: true, CreatureDecision.JudgeLight(낫, 정면),
                                          closing: true);
        Assert.AreEqual(ScytheState.Beware, ScytheFsm.Next(ScytheState.Beware, 정면상황));
    }

    [Test]
    public void 반경_제한이_들어오면_무엇이_깨지는지_적어_둔다()
    {
        // <b>이 테스트는 미래의 결정을 위한 계산이다.</b> 반평면 대신
        // LanternRule.IsBlindSpot(= 뒤 && 빛 원 밖)을 쓰면 사거리 안에서 사각이
        // 몇 점이나 남는지를 센다. 답이 0이면, 그 규칙으로 바꾸는 순간
        // Beware → Attack 전이가 <b>랜턴이 켜져 있는 한 영영 일어나지 않는다</b>.
        const float 사거리 = 2.2f;
        int 사각 = 0;

        for (int ix = -40; ix <= 40; ix++)
        for (int iz = -40; iz <= 40; iz++)
        {
            var p = new Vector3(ix * 0.05f, 0f, iz * 0.05f);
            if (p.sqrMagnitude > 사거리 * 사거리) continue;

            if (LanternRule.IsBlindSpot(_랜턴.Anchor, _랜턴.Forward,
                                        _랜턴.Radius, _랜턴.Offset, p)) 사각++;
        }

        Assert.AreEqual(0, 사각,
                        "반경 제한을 쓰면 사거리 안에 사각이 한 점도 없다 — " +
                        "그 규칙으로 바꾸려면 반경이나 오프셋을 함께 고쳐야 한다");
    }
}
