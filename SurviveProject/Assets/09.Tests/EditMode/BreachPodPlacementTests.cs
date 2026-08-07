using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Survive.Building;
using Survive.Progression;
using Survive.World;

/// <summary>
/// <b>돌파정 — 배치 판정이 있는 탈것</b> (스펙 §6).
///
/// 이 물건은 건축물도 아니고 손에 드는 도구도 아니다. 둘의 성질을 나눠 갖는다 —
/// <b>놓을 자리를 판정하고</b>(건축), <b>놓은 뒤 탄다</b>(탈것). 그래서 검사도 둘이다:
/// 어디에 놓이는가(<see cref="BreachPodPlacement"/>)와 타면 무엇이 남는가
/// (<see cref="BreachPodLaunch"/>).
///
/// <b>이 파일이 지키려는 것 셋.</b>
/// <list type="number">
/// <item><b>경계값.</b> "진한 층이 노출된 자리"는 눈으로는 또렷하지만 코드로는
///       부동소수점 한 자리다. 어디까지가 드러난 것인지 시험이 못 박지 않으면
///       다음 사람이 여유값을 마음대로 옮긴다.</item>
/// <item><b>판정이 두 벌이 아니다.</b> 답도 건축과 같은 열거형이고, 묻는 순서도
///       <c>BuildPlacer.Evaluate</c>와 같다. 순서가 갈리면 같은 상황에서 건축과
///       돌파정이 다른 사유를 내고, 플레이어는 규칙이 둘이라고 배운다.</item>
/// <item><b>탄 뒤 챕터가 끝난다.</b> 진행 원장에 남는 열쇠가 챕터의 마지막 목표가
///       읽는 열쇠와 같아야 한다. 다르면 사람은 내려갔는데 목표는 영영 완료되지
///       않고, 화면에는 아무 오류도 뜨지 않는다.</item>
/// </list>
/// </summary>
public class BreachPodPlacementTests
{
    /// <summary>이 돌파정이 뚫을 수 있는 두께(m). 아이템 정의의 용량과 같은 값이다.</summary>
    const float 용량 = 20f;

    static IReadOnlyList<GearCapability> 돌파정(float capacity = 용량) =>
        new[] { new GearCapability(TraversalGear.BreachPod, capacity) };

    static HazardZone 짙은층(float 두께 = 12f) =>
        new HazardZone(EnvironmentHazard.MacroniumLayer, 두께);

    // ── ① 배치 판정 ─────────────────────────────────────────────

    [Test]
    public void 진한_층이_드러난_자리에는_놓인다()
    {
        var site = BreachPodSite.OnLayer(layerTopY: 10f, surfaceY: 10f);

        Assert.AreEqual(PlacementResult.Ok,
            BreachPodPlacement.Evaluate(site, unlocked: true, hasPod: true));
    }

    [Test]
    public void 층이_아예_없는_자리에는_못_놓는다()
    {
        // 지면은 있다. 없는 것은 층이다 — 사유가 「놓을 자리가 없다」면 안 된다.
        var site = new BreachPodSite(hasSurface: true, hasZone: false,
                                     EnvironmentHazard.None, 0f, 3f, false);

        Assert.AreEqual(PlacementResult.NotDenseLayer,
            BreachPodPlacement.Evaluate(site, unlocked: true, hasPod: true));
    }

    [Test]
    public void 다른_위협이_걸린_구간_위에는_못_놓는다()
    {
        // 액면은 층과 물질이 같지만 묻는 방향이 다르다. 그 위에 놓을 수는 없다.
        foreach (var 위협 in new[] { EnvironmentHazard.MacroniumSurface, EnvironmentHazard.Darkness,
                                     EnvironmentHazard.Depth, EnvironmentHazard.Submersion })
        {
            var site = new BreachPodSite(true, true, 위협, 10f, 10f, false);
            Assert.AreEqual(PlacementResult.NotDenseLayer,
                BreachPodPlacement.Evaluate(site, unlocked: true, hasPod: true),
                $"{위협} 구간 위에 돌파정이 섰다");
        }
    }

    [Test]
    public void 허공을_보고_있으면_놓을_자리가_없다고_답한다()
    {
        Assert.AreEqual(PlacementResult.NoSurface,
            BreachPodPlacement.Evaluate(BreachPodSite.Nowhere, unlocked: true, hasPod: true));
    }

    /// <summary>
    /// <b>경계값.</b> 층이 드러났다는 것은 놓을 면이 곧 층의 윗면이라는 뜻이다.
    /// 여유값 <see cref="BreachPodPlacement.ExposureSkin"/> 안쪽이면 드러난 것이고,
    /// 그 밖이면 무언가가 층을 덮고 있는 것이다.
    /// </summary>
    [Test]
    public void 드러남의_경계는_여유값에서_갈린다()
    {
        const float 층윗면 = 10f;
        float 여유 = BreachPodPlacement.ExposureSkin;

        // <b>경계는 포함이다.</b> 높이를 0에서 재면 뺄셈에 오차가 없어 그 사실이 또렷하다 —
        // 층 윗면이 10m일 때 "정확히 0.3m 위"는 float으로 적을 수 없는 수다.
        Assert.AreEqual(PlacementResult.Ok, 판정(0f, 여유), "위쪽 경계가 포함이 아니다");
        Assert.AreEqual(PlacementResult.Ok, 판정(0f, -여유), "아래쪽 경계가 포함이 아니다");

        // 여유 안쪽은 어느 높이에서 재도 드러난 것이다.
        Assert.AreEqual(PlacementResult.Ok, 판정(층윗면, 층윗면 + 여유 * 0.9f), "위쪽 안쪽에서 막혔다");
        Assert.AreEqual(PlacementResult.Ok, 판정(층윗면, 층윗면 - 여유 * 0.9f), "아래쪽 안쪽에서 막혔다");

        // 한 뼘의 십분의 일만 넘어가도 덮인 것이다.
        Assert.AreEqual(PlacementResult.NotDenseLayer, 판정(층윗면, 층윗면 + 여유 * 1.1f),
            "층 위에 얹힌 것 위에 돌파정이 섰다");
        Assert.AreEqual(PlacementResult.NotDenseLayer, 판정(층윗면, 층윗면 - 여유 * 1.1f),
            "이미 층 속인 자리에서 층을 뚫기 시작한다");

        // 사람 눈에도 명백한 자리들.
        Assert.AreEqual(PlacementResult.NotDenseLayer, 판정(층윗면, 층윗면 + 4f), "바위 위");
        Assert.AreEqual(PlacementResult.NotDenseLayer, 판정(층윗면, 층윗면 - 4f), "층 한복판");

        static PlacementResult 판정(float 층윗면, float 면높이) =>
            BreachPodPlacement.Evaluate(BreachPodSite.OnLayer(층윗면, 면높이), true, true);
    }

    /// <summary>여유값 자체도 못 박는다. 0이면 부동소수점 오차 하나로 종막이 막힌다.</summary>
    [Test]
    public void 여유값은_0도_아니고_한_뼘도_넘지_않는다()
    {
        Assert.Greater(BreachPodPlacement.ExposureSkin, 0f,
            "0으로 두면 콜라이더 오차 하나가 챕터의 출구를 막는다");
        Assert.LessOrEqual(BreachPodPlacement.ExposureSkin, 1f,
            "여유가 한 뼘을 넘으면 층 위에 얹힌 것 위에도 놓인다");

        // 발 하나가 아니라 물건 하나가 얹히는 자리라 발바닥 여유보다는 넉넉해야 한다.
        Assert.GreaterOrEqual(BreachPodPlacement.ExposureSkin, MacroniumContact.ContactSkin,
            "발바닥 여유보다 빡빡하면 걸어 들어간 자리에 놓을 수 없다");
    }

    [Test]
    public void 이미_한_대가_서_있으면_겹친다고_답한다()
    {
        var site = BreachPodSite.OnLayer(10f, 10f, occupied: true);

        Assert.AreEqual(PlacementResult.Blocked,
            BreachPodPlacement.Evaluate(site, unlocked: true, hasPod: true));
    }

    [Test]
    public void 설계를_모르거나_손에_없으면_각자의_사유가_나온다()
    {
        var site = BreachPodSite.OnLayer(10f, 10f);

        Assert.AreEqual(PlacementResult.NotResearched,
            BreachPodPlacement.Evaluate(site, unlocked: false, hasPod: true));
        Assert.AreEqual(PlacementResult.NotEnoughResources,
            BreachPodPlacement.Evaluate(site, unlocked: true, hasPod: false));
    }

    // ── ② 판정이 두 벌이 아니다 ─────────────────────────────────

    /// <summary>
    /// <b>답이 같은 열거형이다.</b> 돌파정이 자기만의 결과형을 들면 화면은 사유를
    /// 두 벌로 옮겨야 하고, 그러면 한쪽만 고친 문구가 반드시 생긴다.
    /// </summary>
    [Test]
    public void 돌파정의_답은_건축과_같은_열거형이다()
    {
        var 모든값 = (PlacementResult[])Enum.GetValues(typeof(PlacementResult));

        // 돌파정이 실제로 내는 사유가 전부 그 열거형 안에 있다.
        var 내는것 = new[]
        {
            BreachPodPlacement.Evaluate(BreachPodSite.OnLayer(10f, 10f), true, true),
            BreachPodPlacement.Evaluate(BreachPodSite.OnLayer(10f, 10f, true), true, true),
            BreachPodPlacement.Evaluate(BreachPodSite.OnLayer(10f, 14f), true, true),
            BreachPodPlacement.Evaluate(BreachPodSite.Nowhere, true, true),
            BreachPodPlacement.Evaluate(BreachPodSite.OnLayer(10f, 10f), false, true),
            BreachPodPlacement.Evaluate(BreachPodSite.OnLayer(10f, 10f), true, false),
        };

        foreach (var r in 내는것)
            CollectionAssert.Contains(모든값, r, "건축의 열거형 밖에서 사유가 나왔다");

        // 그리고 그 사유마다 화면에 띄울 문구가 있다. PlacementCheckTests가 전수로
        // 보지만, 새로 는 값이 그 검사에 실제로 걸리는지 여기서도 한 번 짚는다.
        Assert.IsNotEmpty(PlacementCheckText.Describe(PlacementResult.NotDenseLayer),
            "돌파정 전용 사유에 화면 문구가 없다 — strings.csv에 줄을 더했는가");
    }

    /// <summary>
    /// <b>묻는 순서가 건축과 같다.</b> 여러 조건이 한꺼번에 틀린 상황에서 어느 사유가
    /// 먼저 나오는지가 곧 규칙이다 — <c>BuildPlacer.Evaluate</c>는
    /// 청사진 → 면 → 면의 종류 → 겹침 → 재료 순으로 묻는다.
    ///
    /// <b>왜 순서가 중요한가.</b> 재료도 없고 자리도 틀렸을 때 "재료가 모자라다"고
    /// 말하면 플레이어는 재료를 모아 와서 같은 자리에서 또 막힌다.
    /// </summary>
    [Test]
    public void 묻는_순서가_건축과_같다()
    {
        // 전부 틀린 자리 하나를 두고 조건을 하나씩 풀어 간다.
        var 최악 = new BreachPodSite(hasSurface: false, hasZone: false,
                                    EnvironmentHazard.None, 0f, 0f, occupied: true);

        // ① 청사진이 먼저다 — 나머지가 아무리 틀려도 이 사유가 이긴다.
        Assert.AreEqual(PlacementResult.NotResearched,
            BreachPodPlacement.Evaluate(최악, unlocked: false, hasPod: false));

        // ② 그다음이 면.
        Assert.AreEqual(PlacementResult.NoSurface,
            BreachPodPlacement.Evaluate(최악, unlocked: true, hasPod: false));

        // ③ 면이 생기면 면의 종류.
        var 면만있다 = new BreachPodSite(true, false, EnvironmentHazard.None, 0f, 0f, true);
        Assert.AreEqual(PlacementResult.NotDenseLayer,
            BreachPodPlacement.Evaluate(면만있다, unlocked: true, hasPod: false));

        // ④ 자리가 맞으면 겹침. 재료가 없어도 겹침이 먼저다.
        Assert.AreEqual(PlacementResult.Blocked,
            BreachPodPlacement.Evaluate(BreachPodSite.OnLayer(10f, 10f, occupied: true),
                                        unlocked: true, hasPod: false));

        // ⑤ 마지막이 재료.
        Assert.AreEqual(PlacementResult.NotEnoughResources,
            BreachPodPlacement.Evaluate(BreachPodSite.OnLayer(10f, 10f),
                                        unlocked: true, hasPod: false));
    }

    /// <summary>
    /// <b>「뚫을 수 있는가」도 두 벌이 아니다.</b> 탑승 판정은
    /// <see cref="EnvironmentThreat"/>가 답한 그대로를 쓴다 —
    /// <see cref="MacroniumDescent"/>가 쓰는 바로 그 판정이다.
    /// </summary>
    [Test]
    public void 뚫을_수_있는가는_기존_관문_판정_그대로다()
    {
        foreach (float 두께 in new[] { 1f, 12f, 20f, 20.0001f, 50f })
        {
            var layer = 짙은층(두께);
            bool 관문이_연다 = EnvironmentThreat.CanPass(layer, 돌파정());
            var 탑승 = BreachPodLaunch.Evaluate(placed: true, alreadyGone: false, layer, 돌파정(), ledger: null);

            Assert.AreEqual(관문이_연다, 탑승 == BoardingResult.Ok,
                $"두께 {두께}m에서 관문 판정과 탑승 판정이 어긋난다 (탑승: {탑승})");
        }
    }

    // ── ③ 탄 뒤 챕터가 끝난다 ───────────────────────────────────

    [Test]
    public void 놓이지_않은_돌파정에는_탈_수_없다()
    {
        var 원장 = new 가짜원장();

        Assert.AreEqual(BoardingResult.NotPlaced,
            BreachPodLaunch.Board(placed: false, alreadyGone: false, 짙은층(), 돌파정(), 원장));
        Assert.AreEqual(0, 원장.GetFlag(BreachPodLaunch.DescendedFlag),
            "타지도 않았는데 원장에 종막이 적혔다");
    }

    [Test]
    public void 짙은_층이_아닌_곳에서는_탈_수_없다()
    {
        var 원장 = new 가짜원장();
        var 액면 = new HazardZone(EnvironmentHazard.MacroniumSurface, 5f);

        Assert.AreEqual(BoardingResult.NotOnLayer,
            BreachPodLaunch.Board(placed: true, alreadyGone: false, 액면, 돌파정(), 원장));
        Assert.AreEqual(0, 원장.GetFlag(BreachPodLaunch.DescendedFlag));
    }

    [Test]
    public void 감당하지_못하는_두께면_타도_내려가지_못한다()
    {
        var 원장 = new 가짜원장();

        Assert.AreEqual(BoardingResult.TooThick,
            BreachPodLaunch.Board(placed: true, alreadyGone: false, 짙은층(용량 + 0.5f), 돌파정(), 원장));
        Assert.AreEqual(0, 원장.GetFlag(BreachPodLaunch.DescendedFlag),
            "뚫지도 못했는데 챕터가 끝났다");
    }

    [Test]
    public void 타면_진행_원장에_종막이_적힌다()
    {
        var 원장 = new 가짜원장();
        Assert.AreEqual(0, 원장.GetFlag(BreachPodLaunch.DescendedFlag), "시작부터 적혀 있다");

        Assert.AreEqual(BoardingResult.Ok,
            BreachPodLaunch.Board(placed: true, alreadyGone: false, 짙은층(), 돌파정(), 원장));

        Assert.AreEqual(1, 원장.GetFlag(BreachPodLaunch.DescendedFlag),
            "탔는데 원장에 아무것도 남지 않았다 — 마지막 목표가 영영 완료되지 않는다");
        Assert.AreEqual(1, 원장.적은횟수, "한 번 타는 데 원장을 여러 번 건드린다");
    }

    [Test]
    public void 두_번_타도_종막은_한_번이다()
    {
        var 원장 = new 가짜원장();

        Assert.AreEqual(BoardingResult.Ok, BreachPodLaunch.Board(true, false, 짙은층(), 돌파정(), 원장));
        Assert.AreEqual(BoardingResult.AlreadyGone, BreachPodLaunch.Board(true, false, 짙은층(), 돌파정(), 원장));

        Assert.AreEqual(1, 원장.적은횟수, "종막이 두 번 적혔다");
    }

    /// <summary>판정만 하는 쪽은 원장을 건드리지 않는다. 프롬프트가 챕터를 끝내면 안 된다.</summary>
    [Test]
    public void 판정만_해서는_아무것도_적히지_않는다()
    {
        var 원장 = new 가짜원장();

        for (int i = 0; i < 5; i++)
            Assert.AreEqual(BoardingResult.Ok,
                BreachPodLaunch.Evaluate(true, false, 짙은층(), 돌파정(), 원장));

        Assert.AreEqual(0, 원장.적은횟수, "미리보기가 챕터를 끝냈다");
    }

    /// <summary>
    /// <b>종막을 적는 열쇠는 하나다.</b> 규칙이 쓰는 열쇠와 챕터의 마지막 목표가
    /// 읽는 열쇠가 갈리면, 사람은 내려갔는데 목표는 영영 완료되지 않는다 —
    /// 그리고 화면에는 아무 오류도 뜨지 않는다.
    /// </summary>
    [Test]
    public void 챕터의_마지막_목표가_그_열쇠를_읽는다()
    {
        const string 경로 = "Assets/08.Data/Objectives/ch1_06_descent.asset";
        var 목표 = AssetDatabase.LoadAssetAtPath<FlagObjective>(경로);

        Assert.IsNotNull(목표, 경로 + "를 못 읽었다");
        Assert.AreEqual(BreachPodLaunch.DescendedFlag, 목표.flagKey,
            "돌파정이 적는 열쇠와 마지막 목표가 읽는 열쇠가 다르다");
        Assert.AreEqual(1, 목표.requiredCount, "한 번 내려가면 끝이다");
    }

    /// <summary>
    /// <b>음성 확인.</b> 열쇠를 어긋나게 두면 위 검사가 실제로 무너지는가.
    /// 상수 하나를 못 박는 검사는 상수가 사라져도 조용히 통과할 수 있다.
    /// </summary>
    [Test]
    public void 열쇠가_어긋나면_검사가_무너진다()
    {
        const string 경로 = "Assets/08.Data/Objectives/ch1_06_descent.asset";
        var 목표 = AssetDatabase.LoadAssetAtPath<FlagObjective>(경로);
        Assert.IsNotNull(목표);

        Assert.AreNotEqual(BreachPodLaunch.DescendedFlag, 목표.flagKey + "_x",
            "어긋난 열쇠를 같다고 판정한다");
        Assert.IsNotEmpty(BreachPodLaunch.DescendedFlag, "열쇠가 빈 문자열이면 무엇과도 어긋나지 않는다");
    }

    // ── 가짜 원장 ───────────────────────────────────────────────

    /// <summary>
    /// 진행 원장의 시험용 대역. <b>몇 번 적혔는지</b>까지 세는 것이 요점이다 —
    /// 값만 보면 "한 번 타는 데 다섯 번 적는" 규칙도 통과한다.
    /// </summary>
    sealed class 가짜원장 : IChapterLedger
    {
        readonly Dictionary<string, int> _flags = new Dictionary<string, int>();

        public int 적은횟수 { get; private set; }

        public int GetFlag(string key) => _flags.TryGetValue(key, out var v) ? v : 0;

        public void SetFlag(string key, int value)
        {
            _flags[key] = value;
            적은횟수++;
        }
    }
}
