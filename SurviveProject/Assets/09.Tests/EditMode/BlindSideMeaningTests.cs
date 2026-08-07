using NUnit.Framework;
using UnityEngine;
using Survive.Creatures;
using Survive.World;

/// <summary>
/// <b>「등 뒤 사각」이 무슨 뜻인가</b>를 못 박는 자리.
///
/// <b>2026-08-07에 뒤집혔다. 이 파일은 그 전후를 둘 다 기록한다</b> — 조용히 바뀌는
/// 것이 최악이라고 적어 두고 세운 파일이므로, 뒤집힌 사실 자체가 여기 남아야 한다.
///
/// <b>전(前) — 반평면.</b> <see cref="LitZoneRegistry.IsBlindSide"/>가 거리를 보지 않고
/// "사람보다 뒤이기만 하면 사각"이었다. 그렇게 둔 데에는 이유가 있었다: 원 조건을
/// 넣었더니 낫이 등 뒤 5m 언저리에서 오르내리기만 하고 끝내 닿지 못했다(실측 2.77m 정체).
///
/// <b>진단이 절반이었다.</b> 닿지 못한 진짜 원인은 원 조건이 아니라 <b>오프셋이
/// 작았던 것</b>이다. 반경 8 · 오프셋 3이면 등 뒤 도달이 5m인데 낫 공격 거리는 2.2m라,
/// 어두운 자리에 서서는 애초에 팔이 닿지 않았다. 반평면은 그 부호를 가려 준 것뿐이고,
/// 대가로 <b>낫이 때리는 시간의 96.4%를 빛 안에 서 있게</b> 했다(튜닝 라운드 실측).
/// 그리고 뒤쪽이 통째로 사각이라 낫의 재등장이 고를 자리가 없어져,
/// <b>§19「마주 보면 안전」과 §20「자리를 바꿔 가며 나타난다」가 동시에 성립하지 못했다.</b>
///
/// <b>후(後) — 원.</b> 사용자 판단(2026-08-07): <b>"내 약간 뒤까지 커버하는 원형 범위가
/// 있다고 보면 됨."</b> 그래서 원 조건을 되살리고 <b>오프셋을 3m에서 6.5m로 함께 올렸다</b>.
/// 둘 중 하나만 하면 사각이 사라지거나(원만) 뒤가 통째로 열린 채로 남는다(오프셋만).
/// 지금은 <see cref="LitZoneRegistry.IsBlindSide"/>와 <see cref="LanternRule.IsBlindSpot"/>이
/// <b>같은 뜻</b>이다 — 앞 판본에서 둘이 갈려 있던 것이 이 라운드에 합쳐졌다.
/// </summary>
public class BlindSideMeaningTests
{
    /// <summary>사람이 든 랜턴 하나를 흉내 낸다. <b>수는 게임에서 읽어 온다.</b></summary>
    sealed class 랜턴흉내 : IOffsetLitSource
    {
        public Vector3 Anchor;
        public Vector3 Forward = Vector3.forward;
        public float Radius = LanternRule.Tier1Radius;
        public float Offset = LanternRule.Tier1ForwardOffset;

        public Vector3 LitZoneCenter => Anchor + Forward.normalized * Offset;
        public float LitZoneRadius => Radius;
        public bool IsLit => true;
        public Vector3 LitAnchor => Anchor;
        public Vector3 LitForward => Forward;
    }

    /// <summary>앞뒤가 없는 광원 — 화톳불.</summary>
    sealed class 화톳불흉내 : ILitZoneSource
    {
        public Vector3 Center;
        public float Radius = 9f;
        public Vector3 LitZoneCenter => Center;
        public float LitZoneRadius => Radius;
        public bool IsLit => true;
    }

    const float 낫사거리 = 2.2f;   // 08.Data/Creatures/낫.asset

    랜턴흉내 _랜턴;

    /// <summary>원이 등 뒤로 덮는 거리 = 반경 − 오프셋.</summary>
    static float 뒤덮개 => Mathf.Max(0f, LanternRule.Tier1Radius - LanternRule.Tier1ForwardOffset);

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

    // ── 지금 참인 것: 원 ───────────────────────────────────────

    [Test]
    public void 낫이_읽는_사각은_원_밖이다()
    {
        // <b>이것이 지금의 뜻이다.</b> 사람 바로 뒤는 원이 덮으므로 사각이 아니고,
        // 원이 끝나는 데부터 사각이다.
        Assert.Less(뒤덮개, 낫사거리,
                    "덮개가 사거리보다 멀면 낫이 어둠에 선 채로 닿을 수 없다");

        Assert.IsFalse(LitZoneRegistry.IsBlindSide(뒤로(뒤덮개 * 0.5f)), "덮개 안쪽");
        Assert.IsTrue(LitZoneRegistry.IsBlindSide(뒤로(뒤덮개 + 0.2f)), "덮개 바로 너머");
        Assert.IsTrue(LitZoneRegistry.IsBlindSide(뒤로(100f)), "아주 멀리");

        // 앞쪽은 어디든 사각이 아니다. 이쪽은 뒤집히지 않았다.
        Assert.IsFalse(LitZoneRegistry.IsBlindSide(앞으로(0.3f)));
        Assert.IsFalse(LitZoneRegistry.IsBlindSide(앞으로(100f)));
    }

    [Test]
    public void 두_판정이_이제_같은_뜻이다()
    {
        // <b>앞 판본에서 여기가 갈려 있었다.</b> 레지스트리는 "뒤이기만 하면 사각",
        // 랜턴 규칙은 "뒤이고 원 밖". 이제 레지스트리가 랜턴 규칙을 그대로 부른다.
        foreach (float d in new[] { 0.2f, 0.5f, 1f, 뒤덮개, 뒤덮개 + 0.5f, 3f, 8f, 40f })
        foreach (float x in new[] { -6f, -1f, 0f, 1f, 6f })
        foreach (float z in new[] { -d, d })
        {
            var p = new Vector3(x, 0f, z);

            Assert.AreEqual(
                LanternRule.IsBlindSpot(_랜턴.Anchor, _랜턴.Forward,
                                        _랜턴.Radius, _랜턴.Offset, p),
                LitZoneRegistry.IsBlindSide(p),
                $"두 판정이 갈렸다 {p}");
        }
    }

    [Test]
    public void 사거리_안에서_사각이_유한하고_양수다()
    {
        // 반평면이던 시절 이 비율은 정확히 0.5였고, 원 조건만 넣고 오프셋을 그대로
        // 두면 <b>0</b>이었다(그래서 Beware → Attack이 사라졌다). 지금은 그 사이다.
        int 안 = 0, 사각 = 0;

        for (int ix = -40; ix <= 40; ix++)
        for (int iz = -40; iz <= 40; iz++)
        {
            var p = new Vector3(ix * 0.05f, 0f, iz * 0.05f);
            if (p.sqrMagnitude > 낫사거리 * 낫사거리) continue;

            안++;
            if (LitZoneRegistry.IsBlindSide(p)) 사각++;
        }

        float 비율 = (float)사각 / 안;
        Assert.Greater(사각, 0, "사거리 안에 사각이 한 점도 없으면 낫이 영영 못 때린다");
        Assert.Less(비율, 0.5f, "원인데 반평면만큼 넓으면 원이 아니다");
    }

    [Test]
    public void 오프셋을_밀면_사각이_넓어진다()
    {
        // <b>이쪽이 반평면 시절과 가장 크게 갈리는 자리다.</b> 예전에는 오프셋을
        // 아무리 밀어도 반평면이라 답이 그대로였다. 이제 기획서 §9의 등호
        // "랜턴 오프셋 = 등 뒤 사각의 크기"가 실제로 성립한다.
        var p = 뒤로(2f);

        _랜턴.Offset = 1f;    // 덮개 7m — 2m 뒤는 아직 원 안
        Assert.IsFalse(LitZoneRegistry.IsBlindSide(p), "덜 밀면 아직 지켜 준다");

        _랜턴.Offset = 6.5f;  // 덮개 1.5m — 2m 뒤는 원 밖
        Assert.IsTrue(LitZoneRegistry.IsBlindSide(p), "밀면 내준다");

        // 오프셋 0이면 사각이 아예 없다. 이 회귀선은 그대로 살아 있다 —
        // 웅덩이가 사람을 가운데 두면 내준 쪽이 없기 때문이다.
        _랜턴.Offset = 0f;
        Assert.IsFalse(LitZoneRegistry.IsBlindSide(p), "오프셋 0이면 내준 쪽도 없다");
    }

    [Test]
    public void 고정_조명은_원_밖까지_메운다()
    {
        // 이쪽은 뒤집히지 않았다 — 화톳불 안이면 뒤라도 사각이 아니다.
        // 낫의 따라붙기를 푸는 것과 같은 사실의 다른 얼굴이다(스펙 §20).
        Assert.IsTrue(LitZoneRegistry.IsBlindSide(뒤로(6f)));

        LitZoneRegistry.Register(new 화톳불흉내 { Center = Vector3.zero, Radius = 9f });
        Assert.IsFalse(LitZoneRegistry.IsBlindSide(뒤로(6f)),
                       "고정 조명 안이면 누구의 등 뒤라도 내준 쪽이 아니다");
    }

    // ── 이 뜻이 FSM에 무엇을 하는가 ─────────────────────────────

    [Test]
    public void 원이라도_교전이_열린다()
    {
        // <b>이 라운드가 지키려는 것.</b> 원으로 좁히면서 오프셋을 함께 올렸으므로,
        // 사거리 안 등 뒤에 여전히 어두운 자리가 있고 낫이 거기서 때릴 수 있다.
        // 오프셋을 안 올렸다면 여기가 0점이 되어 이 전이가 통째로 사라졌다.
        var 낫 = new CreatureTraits(BehaviorProfile.Aggressive, 14f, 낫사거리, avoidsLight: true);

        var 자리 = 뒤로(낫사거리 - 0.05f);
        Assert.IsTrue(LitZoneRegistry.IsBlindSide(자리), "사거리 끝의 등 뒤가 어둡다");

        var 등뒤 = new CreatureSenses(낫사거리 - 0.05f, 0f, 0f, false,
                                     threatInLight: true,
                                     threatBlindSide: LitZoneRegistry.IsBlindSide(자리));
        Assert.AreEqual(LightVerdict.Clear, CreatureDecision.JudgeLight(낫, 등뒤));

        var 상황 = new ScytheSituation(detected: true, CreatureDecision.JudgeLight(낫, 등뒤),
                                      closing: true);
        Assert.AreEqual(ScytheState.Attack, ScytheFsm.Next(ScytheState.Beware, 상황));
    }

    [Test]
    public void 정면은_그대로_막힌다()
    {
        // 사람의 방어는 여전히 몸을 돌리는 것이다(§19). 이쪽이 무너지면
        // 원으로 바꾼 것이 그냥 난이도를 올린 것이 된다.
        var 낫 = new CreatureTraits(BehaviorProfile.Aggressive, 14f, 낫사거리, avoidsLight: true);

        var 정면 = new CreatureSenses(2f, 0f, 0f, false,
                                     threatInLight: true,
                                     threatBlindSide: LitZoneRegistry.IsBlindSide(앞으로(2f)));
        Assert.AreEqual(LightVerdict.Blocked, CreatureDecision.JudgeLight(낫, 정면));

        var 상황 = new ScytheSituation(detected: true, CreatureDecision.JudgeLight(낫, 정면),
                                      closing: true);
        Assert.AreEqual(ScytheState.Beware, ScytheFsm.Next(ScytheState.Beware, 상황));
    }

    [Test]
    public void 옆도_원_밖이면_사각이_아니다()
    {
        // <b>원이 되면서 새로 참이 된 것.</b> 옆은 앞도 뒤도 아니므로, 원 밖이어도
        // 사각이 아니다 — 어두울 뿐이다. 반평면 시절에도 같은 답이었지만 이유가
        // 달랐다(그때는 "뒤가 아니라서", 지금은 "내준 쪽이 아니라서").
        // 낫의 재등장이 고를 수 있는 자리가 바로 여기다(§20 게이트 8).
        for (float d = 1f; d <= 20f; d += 1f)
            Assert.IsFalse(LitZoneRegistry.IsBlindSide(new Vector3(d, 0f, 0f)), $"옆 {d}m");
    }

    [Test]
    public void 재등장이_고를_자리가_실제로_남는다()
    {
        // <b>이 라운드의 목적이 값으로 보이는 자리.</b> 반평면이던 시절에는
        // 「앞(보임) 제외 + 뒤(사각) 제외」의 교집합이 비어 재등장이 멎었다.
        // 원이 되면 옆쪽 띠가 살아난다.
        int 남은자리 = 0;
        const int 방향수 = 24;

        for (int i = 0; i < 방향수; i++)
        {
            float r = i * (360f / 방향수) * Mathf.Deg2Rad;
            var p = new Vector3(Mathf.Sin(r) * 10f, 0f, Mathf.Cos(r) * 10f);

            if (LitZoneRegistry.IsLit(p)) continue;        // 빛 안이면 못 나타난다
            if (LitZoneRegistry.IsBlindSide(p)) continue;  // 사각은 걸어서 갈 자리다
            남은자리++;
        }

        Assert.Greater(남은자리, 0,
                       "재등장이 고를 자리가 하나도 없으면 §20 게이트 8이 설 수 없다");
    }
}
