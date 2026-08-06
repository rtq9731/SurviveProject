using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Survive.Items;
using Survive.Vitals;
using Survive.World;

/// <summary>
/// 실행 스펙 §8-1 — 매크로늄 방호복과 잠수 구간.
///
/// 여기서 지키는 것이 셋이다.
/// <list type="number">
/// <item><b>장비 유무로 잠수 가부가 갈린다.</b> 관문이 실제로 관문인가</item>
/// <item><b>죽지 않는다.</b> 위협 계층 원칙 — 환경은 죽이지 않고 생물만 죽인다.
///       거절의 결과에 죽음이 없다는 것을 열거형 전수로 못 박는다</item>
/// <item><b>다른 관문과 섞이지 않는다.</b> 방호복으로 액면 위를 걷거나 층을 뚫을 수
///       없고, 액면 보행 장비·돌파정으로 잠수할 수 없다. 셋이 서로 다른 동사다</item>
/// </list>
///
/// 그리고 <b>실측</b> — 첫 잠수 통로의 길이는 눈대중이 아니라 역산이다.
/// 통로가 아직 씬에 없으므로(배치는 사람의 몫) 여기서 내는 숫자가
/// 그대로 "몇 미터짜리로 파야 하는가"의 답이 된다.
/// </summary>
public class DiveRuleTests
{
    static List<GearCapability> 장비(params GearCapability[] 목록) => new List<GearCapability>(목록);

    /// <summary>규격에 맞게 튜닝된 첫 잠수 통로.</summary>
    static HazardZone 첫통로 => new HazardZone(EnvironmentHazard.Submersion, DiveRule.FirstDiveSeconds);

    static GearCapability 방호복(float 초 = -1f) =>
        new GearCapability(TraversalGear.MacroniumSuit, 초 < 0f ? DiveRule.SuitAirSeconds : 초);

    // ── 1. 장비 유무로 갈린다 ────────────────────────────────

    [Test]
    public void 방호복이_없으면_잠수_구간에_못_들어간다()
    {
        Assert.AreEqual(DiveOutcome.NoSuit, DiveRule.Resolve(true, 첫통로, 장비()));
        Assert.IsFalse(DiveRule.CanEnter(첫통로, 장비()));
    }

    [Test]
    public void 목록이_아예_없어도_거절이지_예외가_아니다()
    {
        Assert.AreEqual(DiveOutcome.NoSuit, DiveRule.Resolve(true, 첫통로, null));
        Assert.IsFalse(DiveRule.HasSuit(null));
    }

    [Test]
    public void 방호복을_걸치면_들어간다()
    {
        Assert.AreEqual(DiveOutcome.Sealed, DiveRule.Resolve(true, 첫통로, 장비(방호복())));
        Assert.IsTrue(DiveRule.CanEnter(첫통로, 장비(방호복())));
    }

    [Test]
    public void 들어가려_하지_않으면_아무_일도_없다()
    {
        // 통로 곁을 지나가는 것만으로 판정이 걸리면 물가를 걷는 것이 사건이 된다.
        Assert.AreEqual(DiveOutcome.None, DiveRule.Resolve(false, 첫통로, 장비()));
        Assert.AreEqual(DiveOutcome.None, DiveRule.Resolve(false, 첫통로, 장비(방호복())));
    }

    [Test]
    public void 보유가_곧_장착이다()
    {
        // 별도의 착용 슬롯을 두지 않는다 — TraversalLoadout이 잡은 규칙 그대로다.
        Assert.IsTrue(DiveRule.HasSuit(장비(방호복())));
        Assert.IsTrue(DiveRule.HasSuit(장비(
            new GearCapability(TraversalGear.Lantern, 99f),
            방호복(),
            new GearCapability(TraversalGear.BreachPod, 99f))));
    }

    [Test]
    public void 용량이_0인_방호복도_걸친_것으로_센다()
    {
        // "장비가 없다"와 "숨이 모자라다"는 플레이어에게 다른 말이다.
        Assert.IsTrue(DiveRule.HasSuit(장비(방호복(0f))));
        Assert.AreEqual(DiveOutcome.NotEnoughAir, DiveRule.Resolve(true, 첫통로, 장비(방호복(0f))));
    }

    // ── 2. 경계값 ────────────────────────────────────────────

    [Test]
    public void 용량이_통로와_정확히_같으면_지난다()
    {
        var 통로 = new HazardZone(EnvironmentHazard.Submersion, 30f);
        Assert.AreEqual(DiveOutcome.Sealed, DiveRule.Resolve(true, 통로, 장비(방호복(30f))));
    }

    [Test]
    public void 용량이_한_톨_모자라면_막힌다()
    {
        var 통로 = new HazardZone(EnvironmentHazard.Submersion, 30f);
        Assert.AreEqual(DiveOutcome.NotEnoughAir, DiveRule.Resolve(true, 통로, 장비(방호복(29.999f))));
    }

    [Test]
    public void 용량이_한_톨_넉넉하면_지난다()
    {
        var 통로 = new HazardZone(EnvironmentHazard.Submersion, 30f);
        Assert.AreEqual(DiveOutcome.Sealed, DiveRule.Resolve(true, 통로, 장비(방호복(30.001f))));
    }

    [Test]
    public void 길이가_0인_통로는_방호복이_있으면_그냥_지난다()
    {
        var 통로 = new HazardZone(EnvironmentHazard.Submersion, 0f);
        Assert.AreEqual(DiveOutcome.Sealed, DiveRule.Resolve(true, 통로, 장비(방호복())));
    }

    [Test]
    public void 길이가_0이어도_방호복이_없으면_못_들어간다()
    {
        // 짧다고 열리는 것이 아니다 — 관문은 크기가 아니라 수단을 묻는다.
        var 통로 = new HazardZone(EnvironmentHazard.Submersion, 0f);
        Assert.AreEqual(DiveOutcome.NoSuit, DiveRule.Resolve(true, 통로, 장비()));
    }

    [Test]
    public void 방호복이_여럿이면_가장_좋은_것을_쓴다()
    {
        // 합산하지 않는다. EnvironmentThreat·OxygenRate가 잡은 규칙과 같다.
        var 통로 = new HazardZone(EnvironmentHazard.Submersion, 40f);
        Assert.AreEqual(DiveOutcome.Sealed,
                        DiveRule.Resolve(true, 통로, 장비(방호복(10f), 방호복(40f))));
        Assert.AreEqual(DiveOutcome.NotEnoughAir,
                        DiveRule.Resolve(true, 통로, 장비(방호복(10f), 방호복(20f))));
    }

    // ── 3. 죽지 않는다 ───────────────────────────────────────

    [Test]
    public void 잠수의_결과에_죽음이_없다()
    {
        // 위협 계층 원칙(기획서 갱신점 _3 §2) — 환경은 죽이지 않고 생물만 죽인다.
        // 이름이 되살아나면 원칙이 무너진 것이므로 열거형 전수로 막는다.
        var 이름들 = System.Enum.GetNames(typeof(DiveOutcome));
        CollectionAssert.Contains(이름들, "NoSuit", "검사 자체가 망가졌다");

        foreach (var 금지 in new[] { "Lethal", "Death", "Drown" })
            CollectionAssert.DoesNotContain(이름들, 금지,
                $"잠수가 사람을 죽이기 시작했다 ({금지}) — 액면과 헷갈린 것이다");
    }

    [Test]
    public void 액면은_죽이고_잠수는_밀어낸다()
    {
        // 같은 맨몸이 액면 앞에서는 죽고 잠수 통로 앞에서는 막히기만 한다.
        // 두 관문이 갈리는 자리를 한 검사 안에 나란히 둔다.
        var 액면 = new HazardZone(EnvironmentHazard.MacroniumSurface, 30f);
        Assert.AreEqual(MacroniumContactOutcome.Lethal,
                        MacroniumContact.Resolve(true, 액면, 장비()));
        Assert.AreEqual(DiveOutcome.NoSuit, DiveRule.Resolve(true, 첫통로, 장비()));
    }

    // ── 4. 다른 관문과 섞이지 않는다 ─────────────────────────

    [TestCase(EnvironmentHazard.None)]
    [TestCase(EnvironmentHazard.Darkness)]
    [TestCase(EnvironmentHazard.Depth)]
    [TestCase(EnvironmentHazard.MacroniumSurface)]
    [TestCase(EnvironmentHazard.MacroniumLayer)]
    public void 잠수가_아닌_구간은_잠수로_묻지_않는다(EnvironmentHazard 위협)
    {
        var 구간 = new HazardZone(위협, 30f);
        Assert.AreEqual(DiveOutcome.None, DiveRule.Resolve(true, 구간, 장비()));
        Assert.AreEqual(DiveOutcome.None, DiveRule.Resolve(true, 구간, 장비(방호복())));
    }

    [Test]
    public void 방호복으로는_액면_위를_걷지_못한다()
    {
        var 액면 = new HazardZone(EnvironmentHazard.MacroniumSurface, 30f);
        Assert.IsFalse(EnvironmentThreat.CanPass(액면, 장비(방호복(999f))));
    }

    [Test]
    public void 방호복으로는_짙은_층을_뚫지_못한다()
    {
        var 층 = new HazardZone(EnvironmentHazard.MacroniumLayer, 12f);
        Assert.IsFalse(EnvironmentThreat.CanPass(층, 장비(방호복(999f))));
    }

    [Test]
    public void 액면_보행_장비로도_돌파정으로도_잠수할_수_없다()
    {
        var 앞뒤 = 장비(
            new GearCapability(TraversalGear.SurfaceWalker, 999f),
            new GearCapability(TraversalGear.BreachPod, 999f));

        Assert.AreEqual(DiveOutcome.NoSuit, DiveRule.Resolve(true, 첫통로, 앞뒤));
    }

    [Test]
    public void 수영은_잠수를_대신하지_못한다()
    {
        // 수영은 관문이 아니라 학습 장치다(기획서 갱신점 _3 §2).
        // 숨을 아무리 오래 참아도 통로는 장비 없이 열리지 않는다.
        var 수영만 = 장비(new GearCapability(TraversalGear.Swimming, 9999f));
        Assert.AreEqual(DiveOutcome.NoSuit, DiveRule.Resolve(true, 첫통로, 수영만));
    }

    [Test]
    public void 관문_셋이_한_줄로_이어진다()
    {
        // 위를 걷는다 → 안으로 들어간다 → 뚫는다. 앞의 것으로 뒤를 대신할 수 없다.
        var 액면 = new HazardZone(EnvironmentHazard.MacroniumSurface, 30f);
        var 층 = new HazardZone(EnvironmentHazard.MacroniumLayer, 12f);

        var 보행까지 = 장비(new GearCapability(TraversalGear.SurfaceWalker, 30f));
        Assert.IsTrue(EnvironmentThreat.CanPass(액면, 보행까지));
        Assert.IsFalse(DiveRule.CanEnter(첫통로, 보행까지));

        var 방호복까지 = 장비(new GearCapability(TraversalGear.SurfaceWalker, 30f), 방호복());
        Assert.IsTrue(DiveRule.CanEnter(첫통로, 방호복까지));
        Assert.IsFalse(EnvironmentThreat.CanPass(층, 방호복까지));

        var 돌파정까지 = 장비(new GearCapability(TraversalGear.SurfaceWalker, 30f), 방호복(),
                          new GearCapability(TraversalGear.BreachPod, 12f));
        Assert.IsTrue(EnvironmentThreat.CanPass(층, 돌파정까지));
    }

    [Test]
    public void 잠수를_뚫는_장비는_방호복_하나다()
    {
        Assert.AreEqual(TraversalGear.MacroniumSuit,
                        EnvironmentThreat.RequiredGear(EnvironmentHazard.Submersion));
    }

    [Test]
    public void 판정을_두_벌로_두지_않았다()
    {
        // DiveRule은 EnvironmentThreat의 답을 잠수의 말로 옮길 뿐이다.
        // 둘이 갈라지면 관문은 열리는데 들어가면 막히는 상태가 생긴다.
        foreach (float 용량 in new[] { 0f, 10f, 35.999f, 36f, 36.001f, 100f })
        {
            var 목록 = 장비(방호복(용량));
            bool 통과 = EnvironmentThreat.CanPass(첫통로, 목록);
            bool 들어감 = DiveRule.Resolve(true, 첫통로, 목록) == DiveOutcome.Sealed;
            Assert.AreEqual(통과, 들어감, $"용량 {용량}에서 두 판정이 갈렸다");
        }
    }

    // ── 5. 숨 ────────────────────────────────────────────────

    [Test]
    public void 잠기면_줄고_나오면_찬다()
    {
        Assert.Less(DiveRule.OxygenDeltaPerSecond(true, true), 0f);
        Assert.Less(DiveRule.OxygenDeltaPerSecond(true, false), 0f);
        Assert.Greater(DiveRule.OxygenDeltaPerSecond(false, true), 0f);
        Assert.Greater(DiveRule.OxygenDeltaPerSecond(false, false), 0f);
    }

    [Test]
    public void 방호복을_걸치면_숨이_더_간다()
    {
        // 이 부등호가 뒤집히면 장비를 만들 이유가 없어진다.
        Assert.Greater(DiveRule.OxygenDeltaPerSecond(true, true),
                       DiveRule.OxygenDeltaPerSecond(true, false),
                       "방호복이 맨몸보다 숨을 빨리 먹는다");
        Assert.Greater(DiveRule.SuitAirSeconds, DiveRule.BareAirSeconds);
    }

    [Test]
    public void 남은_산소로_버티는_시간이_나온다()
    {
        Assert.AreEqual(DiveRule.SuitAirSeconds, DiveRule.SecondsOfAir(DiveRule.OxygenMax, true), 0.001f);
        Assert.AreEqual(DiveRule.BareAirSeconds, DiveRule.SecondsOfAir(DiveRule.OxygenMax, false), 0.001f);
        Assert.AreEqual(0f, DiveRule.SecondsOfAir(0f, true), 0.001f);
        Assert.AreEqual(0f, DiveRule.SecondsOfAir(-50f, true), 0.001f, "음수 산소로 시간을 벌면 안 된다");
    }

    // ── 6. 실측 — 첫 잠수 통로의 길이 역산 ───────────────────

    [Test]
    public void 첫_잠수는_도착할_때_바닥_근처다()
    {
        // 실행 스펙 §8-1의 실측 기준 그대로 — 0보다 크되 20% 아래.
        float 잔량 = DiveRule.ArrivalRatio(DiveRule.FirstDiveSeconds);
        Assert.Greater(잔량, 0f, "도착과 동시에 숨이 다한다 — 한 프레임 늦으면 죽는다");
        Assert.Less(잔량, DiveRule.ArrivalRatioCeiling,
                    $"도착 잔량 {잔량:P0}이면 여유가 눈에 보인다 — 아슬아슬하지 않다");
        Assert.IsTrue(DiveRule.IsFirstDiveTuned(DiveRule.FirstDiveSeconds));
    }

    [Test]
    public void 규격을_벗어난_통로는_규격_밖으로_잡힌다()
    {
        // 검사가 헛돌지 않는다는 것부터 본다.
        Assert.IsFalse(DiveRule.IsFirstDiveTuned(1f), "너무 짧은 통로가 규격 안으로 잡힌다");
        Assert.IsFalse(DiveRule.IsFirstDiveTuned(DiveRule.SuitAirSeconds),
                       "도착과 동시에 0이 되는 통로가 규격 안으로 잡힌다");
        Assert.IsFalse(DiveRule.IsFirstDiveTuned(DiveRule.SuitAirSeconds + 10f),
                       "숨이 모자란 통로가 규격 안으로 잡힌다");
    }

    [Test]
    public void 통로_길이와_시간이_서로를_되돌린다()
    {
        foreach (float 초 in new[] { 0f, 5f, 36f, 120f })
            Assert.AreEqual(초, DiveRule.PassageSecondsFor(DiveRule.PassageMetersFor(초)), 0.001f);
    }

    [Test]
    public void 규격을_지키는_통로_길이의_창이_열려_있다()
    {
        Assert.Less(DiveRule.ShortestTunedPassageMeters, DiveRule.LongestSurvivablePassageMeters);
        Assert.Greater(DiveRule.FirstDivePassageMeters, DiveRule.ShortestTunedPassageMeters);
        Assert.Less(DiveRule.FirstDivePassageMeters, DiveRule.LongestSurvivablePassageMeters);
    }

    [Test]
    public void 역산_표를_찍는다()
    {
        // 통로는 아직 씬에 없다(배치는 사람의 몫). 그러므로 이 라운드가 낼 수 있는
        // 실측은 "몇 미터짜리로 파야 하는가"뿐이고, 그 값을 여기서 콘솔에 남긴다.
        Debug.Log(
            "[잠수 실측] 산소 총량 " + DiveRule.OxygenMax.ToString("F0") +
            " / 방호복 초당 " + DiveRule.SuitDrainPerSecond.ToString("F2") +
            " / 맨몸 초당 " + DiveRule.BareDrainPerSecond.ToString("F2") +
            "\n  방호복 지속 " + DiveRule.SuitAirSeconds.ToString("F1") + "초" +
            " · 맨몸 지속 " + DiveRule.BareAirSeconds.ToString("F1") + "초" +
            "\n  수영 속도 " + DiveRule.SwimSpeedMetersPerSecond.ToString("F2") + "m/s" +
            "\n  첫 잠수 " + DiveRule.FirstDiveSeconds.ToString("F1") + "초 = " +
            DiveRule.FirstDivePassageMeters.ToString("F1") + "m (편도)" +
            " · 도착 잔량 " + DiveRule.ArrivalRatio(DiveRule.FirstDiveSeconds).ToString("P0") +
            "\n  규격을 지키는 길이 " + DiveRule.ShortestTunedPassageMeters.ToString("F1") + "m ~ " +
            DiveRule.LongestSurvivablePassageMeters.ToString("F1") + "m (편도)");

        Assert.Pass();
    }

    // ── 7. 손잡이가 실제 값과 어긋나지 않는가 ────────────────

    [Test]
    public void 산소_총량이_게이지_에셋과_같다()
    {
        // 어긋나면 여기서 역산한 통로 길이가 게임 안의 게이지와 다른 말을 한다.
        var def = AssetDatabase.LoadAssetAtPath<VitalDefinitionSO>("Assets/08.Data/Vitals/Oxygen.asset");
        Assert.IsNotNull(def, "Oxygen.asset을 못 읽었다");
        Assert.AreEqual(def.maxValue, DiveRule.OxygenMax, 0.001f,
                        "DiveRule.OxygenMax와 Oxygen.asset의 maxValue가 다르다");
        Assert.AreEqual(def.maxValue, def.startValue, 0.001f,
                        "가득 찬 상태로 시작하지 않으면 첫 잠수의 역산이 어긋난다");
    }

    [Test]
    public void 헤엄_속도가_플레이어가_실제로_내는_속도와_같다()
    {
        // 길이를 재는 자가 실제 이동 속도와 다르면 역산이 통째로 거짓말이 된다.
        //
        // 형을 이름으로 찾는 이유: 이동 코드는 기본 어셈블리에 있고 이 테스트
        // 어셈블리에서는 참조할 수 없다. 직렬화된 값만 필요하므로 형은 몰라도 된다.
        var loco = 프리팹의_컴포넌트("PlayerLocomotion");
        var prop = new SerializedObject(loco).FindProperty("swimSpeed");
        Assert.IsNotNull(prop, "PlayerLocomotion.swimSpeed를 못 찾았다 — 이름이 바뀌었다");
        Assert.AreEqual(prop.floatValue, DiveRule.SwimSpeedMetersPerSecond, 0.001f,
                        "DiveRule의 헤엄 속도가 프리팹의 값과 다르다 — 역산이 거짓이 된다");
    }

    [Test]
    public void 방호복_에셋의_용량이_손잡이에서_나온_값이다()
    {
        // 에셋에 사본이 남으면 상수를 돌려도 게임이 안 바뀐다
        // (CampfireFuelRule·LanternRule에서 실제로 겪은 일이다).
        var db = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>("Assets/08.Data/Items/ItemDatabase.asset");
        Assert.IsNotNull(db, "ItemDatabase를 못 읽었다");

        var suit = db.GetById("macronium_suit") as TraversalGearItemSO;
        Assert.IsNotNull(suit, "ItemDatabase에 macronium_suit가 없다");
        Assert.AreEqual(TraversalGear.MacroniumSuit, suit.gear);
        Assert.AreEqual(DiveRule.SuitAirSeconds, suit.capacity, 0.001f,
                        "방호복 에셋의 용량이 DiveRule.SuitAirSeconds와 다르다");
    }

    const string 플레이어경로 = "Assets/05.Prefabs/Player.prefab";

    static Component 프리팹의_컴포넌트(string 형이름)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(플레이어경로);
        Assert.IsNotNull(go, 플레이어경로 + "를 못 읽었다");

        foreach (var c in go.GetComponentsInChildren<Component>(true))
            if (c != null && c.GetType().Name == 형이름) return c;

        Assert.Fail($"플레이어 프리팹에 {형이름}이 없다");
        return null;
    }

    // ── 8. 연출 — 압박은 이쪽이 진다 ─────────────────────────

    [Test]
    public void 숨이_넉넉하면_화면이_조이지_않는다()
    {
        Assert.AreEqual(0f, DiveRule.EdgeDarkness(1f), 0.0001f, "잠기자마자 위험 신호가 뜬다");
        Assert.AreEqual(0f, DiveRule.EdgeDarkness(DiveRule.EdgeOnsetRatio), 0.0001f);
    }

    [Test]
    public void 숨이_줄수록_화면이_조여든다()
    {
        float 이전 = -1f;
        for (float o = DiveRule.EdgeOnsetRatio; o >= 0f; o -= 0.05f)
        {
            float 지금 = DiveRule.EdgeDarkness(o);
            Assert.GreaterOrEqual(지금, 이전, $"산소 {o:F2}에서 오히려 열렸다");
            이전 = 지금;
        }
        Assert.AreEqual(DiveRule.EdgeMaxDarkness, DiveRule.EdgeDarkness(0f), 0.0001f);
    }

    [Test]
    public void 화면이_완전히_닫히지는_않는다()
    {
        // 앞이 아예 안 보이면 연출이 아니라 조작 불능이다.
        for (float o = 0f; o <= 1f; o += 0.05f)
            Assert.Less(DiveRule.EdgeDarkness(o), 1f, $"산소 {o:F2}에서 화면이 닫힌다");
    }

    [Test]
    public void 숨이_줄수록_심박이_빨라진다()
    {
        Assert.AreEqual(DiveRule.CalmBeatSeconds, DiveRule.BeatSeconds(1f), 0.0001f);
        Assert.AreEqual(DiveRule.PanicBeatSeconds, DiveRule.BeatSeconds(0f), 0.0001f);
        Assert.Less(DiveRule.BeatSeconds(0.2f), DiveRule.BeatSeconds(0.8f));
        Assert.Greater(DiveRule.PanicBeatSeconds, 0f, "간격이 0이면 나눗셈이 터진다");
    }
}
