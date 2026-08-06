using NUnit.Framework;
using UnityEngine;
using Survive.Instruments;

/// <summary>
/// 챕터 1 재건 스펙 §8-2 — 레이더가 <b>무엇을 잡고 무엇을 못 잡는가</b>.
///
/// <b>이 파일이 지키는 것은 규칙이 아니라 규칙의 출처다.</b> 기획이 정한 원리는
/// 한 줄이다 — 낮은 주파수의 전자파. 느리고 반응성이 낮다. 그 하나에서
/// 제약 넷이 전부 따라 나와야 하고, 따라 나오지 않고 손으로 적힌 예외가 하나라도
/// 생기면 "그럼 큰 낫은?"이라는 질문에 아무도 답할 수 없게 된다.
///
/// 그래서 여기서 되묻는 것은 "낫이 안 잡히는가"만이 아니다.
/// <b>낫이 커져도 안 잡히고, 멈춰도 안 잡히는가</b>를 함께 묻는다. 두 이유가
/// 각각 살아 있어야 그것이 물성이지 목록이 아니다.
///
/// 순수 C#이라 Unity 실행 없이 돈다.
/// </summary>
public class RadarDetectionTests
{
    const float 허용오차 = 1e-3f;

    /// <summary>에셋에 들어간 값 그대로. 파장 8m 하나가 아래를 전부 정한다.</summary>
    static RadarBand 대역() => new RadarBand
    {
        wavelengthMeters = 8f,
        sweepSeconds = 8f,
        rangeMeters = 4000f,
    };

    static RadarContact 접촉(RadarContactKind kind, float size, float speed,
                             float distance, float depth = 0f) =>
        new RadarContact
        {
            id = kind.ToString(),
            kind = kind,
            sizeMeters = size,
            speedMps = speed,
            distanceMeters = distance,
            depthMeters = depth,
        };

    // 세계에 실제로 놓일 것들. 숫자는 기획의 크기 감각이다.
    static RadarContact 다른_섬() => 접촉(RadarContactKind.Island, 600f, 0f, 2600f);
    static RadarContact 바다_아래_공동() => 접촉(RadarContactKind.Cavity, 90f, 0f, 1200f, 180f);
    static RadarContact 낫() => 접촉(RadarContactKind.Creature, 2.4f, 6f, 30f);
    static RadarContact 발밑_균열() => 접촉(RadarContactKind.Fissure, 0.6f, 0f, 4f);

    // ── 파장 하나가 정하는 값들 ──────────────────────────────

    [Test]
    public void 파장_하나가_해상도와_추적_한계와_투과_깊이를_정한다()
    {
        var band = 대역();

        Assert.AreEqual(8f, band.ResolutionMeters, 허용오차, "해상도는 파장만 하다");
        Assert.AreEqual(1f, band.MaxTrackableSpeedMps, 허용오차, "한 장 쌓는 사이에 한 칸을 벗어나면 번진다");
        Assert.AreEqual(320f, band.PenetrationDepthMeters, 허용오차, "저주파는 깊이 든다");
        Assert.AreEqual(1f, band.CoherenceRadiusMeters, 허용오차, "위상 기준은 파장의 8분의 1이다");
    }

    [Test]
    public void 파장을_늘리면_더_깊이_들어가고_더_많이_놓친다()
    {
        // 이 맞바꿈이 실제 지표투과 레이더의 성질이고, 이 장치가 큰 것만 보는 이유다.
        var 낮은주파수 = new RadarBand { wavelengthMeters = 16f, sweepSeconds = 8f };
        var 높은주파수 = new RadarBand { wavelengthMeters = 4f, sweepSeconds = 8f };

        Assert.Greater(낮은주파수.PenetrationDepthMeters, 높은주파수.PenetrationDepthMeters,
            "파장이 길수록 깊이 들어가야 한다");
        Assert.Greater(낮은주파수.ResolutionMeters, 높은주파수.ResolutionMeters,
            "파장이 길수록 더 큰 것만 보여야 한다");
        Assert.Greater(낮은주파수.CoherenceRadiusMeters, 높은주파수.CoherenceRadiusMeters,
            "파장이 길수록 위상이 어긋나기까지 더 움직일 수 있다");
    }

    [Test]
    public void 갱신이_느릴수록_느린_것까지만_따라간다()
    {
        // <b>추적 한계는 파장이 아니라 갱신 주기에서 나온다.</b> 파장만 늘리면
        // 오히려 한 칸이 넓어져 빠른 것도 칸 안에 남는다 — 대신 어디 있는지를 모른다.
        // "낫을 못 따라간다"를 만드는 것은 <b>반응성이 낮다</b>는 쪽이고, 그것도
        // 저주파에서 나온다: 되돌아오는 신호가 약해 한 장을 얻는 데 오래 걸린다.
        var 느린갱신 = new RadarBand { wavelengthMeters = 8f, sweepSeconds = 16f };
        var 빠른갱신 = new RadarBand { wavelengthMeters = 8f, sweepSeconds = 4f };

        Assert.Less(느린갱신.MaxTrackableSpeedMps, 빠른갱신.MaxTrackableSpeedMps);
        Assert.AreEqual(느린갱신.ResolutionMeters, 빠른갱신.ResolutionMeters, 허용오차,
            "갱신 주기는 해상도를 바꾸지 않는다 — 둘은 서로 다른 축이다");
    }

    // ── 잡히는 것 ────────────────────────────────────────────

    [Test]
    public void 다른_섬은_잡힌다()
    {
        // §14 4단계 — 레이더에 다른 섬이 잡히는 것이 챕터 1의 다음 걸음을 연다.
        Assert.AreEqual(RadarVerdict.Detected, RadarDetection.Evaluate(대역(), 다른_섬()));
    }

    [Test]
    public void 바다_아래_공동은_잡힌다()
    {
        // 투과력이 높다는 것이 이 장치의 값어치다. 깊다고 사라지면 지하로 갈 이유를
        // 알려 줄 방법이 없어진다.
        Assert.AreEqual(RadarVerdict.Detected, RadarDetection.Evaluate(대역(), 바다_아래_공동()));
    }

    // ── 안 잡히는 것 ─────────────────────────────────────────

    [Test]
    public void 낫은_잡히지_않는다()
    {
        Assert.AreNotEqual(RadarVerdict.Detected, RadarDetection.Evaluate(대역(), 낫()),
            "낫이 잡히면 어둠 속에서 무엇이 다가오는지 화면이 대신 봐 준다");
    }

    [Test]
    public void 낫이_안_잡히는_이유는_둘이고_둘_다_저주파에서_나온다()
    {
        // 이 테스트가 이 파일의 알맹이다. 이유가 하나뿐이면 그 하나를 비껴가는
        // 개체가 나오는 순간 규칙이 무너진다.
        var 이유 = RadarDetection.Reasons(대역(), 낫());

        Assert.Contains(RadarVerdict.BelowResolution, 이유, "작아서 뭉개진다 (해상도)");
        Assert.Contains(RadarVerdict.FasterThanRefresh, 이유, "빨라서 번진다 (갱신 속도)");
    }

    [Test]
    public void 낫이_멈춰_서도_크기_때문에_안_잡힌다()
    {
        var 멈춘낫 = 낫();
        멈춘낫.speedMps = 0f;

        Assert.AreEqual(RadarVerdict.BelowResolution, RadarDetection.Evaluate(대역(), 멈춘낫));
    }

    [Test]
    public void 낫이_커져도_속도_때문에_안_잡힌다()
    {
        var 큰낫 = 낫();
        큰낫.sizeMeters = 40f;   // 해상도의 다섯 배

        Assert.AreEqual(RadarVerdict.FasterThanRefresh, RadarDetection.Evaluate(대역(), 큰낫));
    }

    [Test]
    public void 크고_느린_것은_낫이라도_잡힌다()
    {
        // 예외를 두지 않는다는 것의 뜻이다. 물성이 규칙이므로 물성이 바뀌면 결과도
        // 바뀌어야 하고, 그것이 옳다 — 큰 것은 잡힌다.
        var 거대한_붙박이 = 접촉(RadarContactKind.Creature, 300f, 0f, 500f);

        Assert.AreEqual(RadarVerdict.Detected, RadarDetection.Evaluate(대역(), 거대한_붙박이));
    }

    [Test]
    public void 발밑_균열은_잡히지_않는다()
    {
        // 어둠이 지형을 감춘다는 축을 지킨다. 발밑이 보이기 시작하면 랜턴도
        // 조심스러운 걸음도 값을 잃는다.
        Assert.AreEqual(RadarVerdict.BelowResolution, RadarDetection.Evaluate(대역(), 발밑_균열()));
    }

    [Test]
    public void 균열은_코앞에_있어도_안_잡힌다()
    {
        // 거리를 좁혀도 해상도는 좋아지지 않는다. 가까우면 보인다면 그것은
        // 해상도가 아니라 감도의 이야기가 되어 원리가 흐려진다.
        var 발밑 = 발밑_균열();
        발밑.distanceMeters = 0.5f;

        Assert.AreEqual(RadarVerdict.BelowResolution, RadarDetection.Evaluate(대역(), 발밑));
    }

    // ── 경계값 ───────────────────────────────────────────────

    [Test]
    public void 해상도와_꼭_같은_크기는_잡힌다()
    {
        var band = 대역();
        var 딱맞음 = 접촉(RadarContactKind.Structure, band.ResolutionMeters, 0f, 100f);

        Assert.AreEqual(RadarVerdict.Detected, RadarDetection.Evaluate(band, 딱맞음));
    }

    [Test]
    public void 해상도보다_한_뼘_작으면_안_잡힌다()
    {
        var band = 대역();
        var 조금작음 = 접촉(RadarContactKind.Structure, band.ResolutionMeters - 0.01f, 0f, 100f);

        Assert.AreEqual(RadarVerdict.BelowResolution, RadarDetection.Evaluate(band, 조금작음));
    }

    [Test]
    public void 추적_한계_속도까지는_잡히고_넘으면_안_잡힌다()
    {
        var band = 대역();
        float 한계 = band.MaxTrackableSpeedMps;

        var 딱맞음 = 접촉(RadarContactKind.Island, 200f, 한계, 100f);
        var 조금빠름 = 접촉(RadarContactKind.Island, 200f, 한계 + 0.01f, 100f);

        Assert.AreEqual(RadarVerdict.Detected, RadarDetection.Evaluate(band, 딱맞음));
        Assert.AreEqual(RadarVerdict.FasterThanRefresh, RadarDetection.Evaluate(band, 조금빠름));
    }

    [Test]
    public void 투과_깊이까지는_잡히고_넘으면_안_잡힌다()
    {
        var band = 대역();
        float 한계 = band.PenetrationDepthMeters;

        var 딱맞음 = 접촉(RadarContactKind.Cavity, 90f, 0f, 100f, 한계);
        var 조금깊음 = 접촉(RadarContactKind.Cavity, 90f, 0f, 100f, 한계 + 0.01f);

        Assert.AreEqual(RadarVerdict.Detected, RadarDetection.Evaluate(band, 딱맞음));
        Assert.AreEqual(RadarVerdict.TooDeep, RadarDetection.Evaluate(band, 조금깊음));
    }

    [Test]
    public void 닿는_거리_끝은_잡히고_그_너머는_안_잡힌다()
    {
        var band = 대역();

        var 끝 = 접촉(RadarContactKind.Island, 600f, 0f, band.rangeMeters);
        var 너머 = 접촉(RadarContactKind.Island, 600f, 0f, band.rangeMeters + 0.01f);

        Assert.AreEqual(RadarVerdict.Detected, RadarDetection.Evaluate(band, 끝));
        Assert.AreEqual(RadarVerdict.OutOfRange, RadarDetection.Evaluate(band, 너머));
    }

    // ── 판정은 종류를 보지 않는다 ────────────────────────────

    [Test]
    public void 물리량이_같으면_종류가_달라도_판정이_같다()
    {
        // 종류가 판정에 끼는 순간 "낫이라서 안 잡힌다"가 되고, 물성에서 나온 규칙이
        // 종족 목록으로 타락한다.
        var band = 대역();

        foreach (RadarContactKind kind in System.Enum.GetValues(typeof(RadarContactKind)))
        {
            var 큰것 = 접촉(kind, 600f, 0f, 1000f);
            var 작은것 = 접촉(kind, 1f, 0f, 10f);

            Assert.AreEqual(RadarVerdict.Detected, RadarDetection.Evaluate(band, 큰것),
                $"{kind}: 크고 느린데 안 잡힌다");
            Assert.AreEqual(RadarVerdict.BelowResolution, RadarDetection.Evaluate(band, 작은것),
                $"{kind}: 작은데 잡힌다");
        }
    }

    // ── 한 번 훑기 ───────────────────────────────────────────

    [Test]
    public void 훑으면_잡힌_것만_가까운_순으로_선다()
    {
        var band = 대역();
        var 결과 = RadarDetection.Sweep(band, new[]
        {
            다른_섬(), 낫(), 바다_아래_공동(), 발밑_균열(),
        });

        Assert.AreEqual(2, 결과.Count, "잡히는 것은 섬과 공동 둘뿐이다");
        Assert.AreEqual(RadarContactKind.Cavity, 결과[0].kind, "가까운 것이 앞이다");
        Assert.AreEqual(RadarContactKind.Island, 결과[1].kind);
    }

    [Test]
    public void 빈_입력에도_버틴다()
    {
        Assert.IsEmpty(RadarDetection.Sweep(대역(), null));
        Assert.IsEmpty(RadarDetection.Sweep(null, new[] { 다른_섬() }));
        Assert.IsEmpty(RadarDetection.Reasons(null, null));
        Assert.IsFalse(RadarDetection.CanDetect(대역(), null));

        var 빈칸섞임 = RadarDetection.Sweep(대역(), new RadarContact[] { null, 다른_섬(), null });
        Assert.AreEqual(1, 빈칸섞임.Count);
    }

    // ── 방위 ─────────────────────────────────────────────────

    [Test]
    public void 방위는_북쪽이_0이고_시계_방향이다()
    {
        Assert.AreEqual(0f, RadarContactRegistry.Bearing(Vector3.forward), 허용오차);
        Assert.AreEqual(90f, RadarContactRegistry.Bearing(Vector3.right), 허용오차);
        Assert.AreEqual(180f, RadarContactRegistry.Bearing(Vector3.back), 허용오차);
        Assert.AreEqual(270f, RadarContactRegistry.Bearing(Vector3.left), 허용오차);
        Assert.AreEqual(0f, RadarContactRegistry.Bearing(Vector3.zero), 허용오차, "제자리는 방위가 없다");
    }

    // ══ 관측 한 번의 상태 전이 ═══════════════════════════════

    const float 관측시간 = 6f;
    const float 초당소모 = 4f;

    static RadarScan 관측() => new RadarScan(대역(), 관측시간, 초당소모);

    [Test]
    public void 걸기_전에는_아무것도_아니다()
    {
        var scan = 관측();

        Assert.AreEqual(RadarScanState.Idle, scan.State);
        Assert.AreEqual(RadarCancelReason.None, scan.CancelReason);
        Assert.AreEqual(24f, scan.FullCost, 허용오차, "6초 × 초당 4 = 24");
        Assert.AreEqual(0f, scan.Tick(1f, 0f, 100f), 허용오차, "걸지 않았는데 배터리를 먹으면 안 된다");
    }

    [Test]
    public void 전원이_모자라면_걸리지도_않는다()
    {
        // 반쯤 하다 꺼지게 두면 장치가 조용히 배터리만 먹고 아무것도 안 준다.
        var scan = 관측();

        Assert.IsFalse(scan.Begin(scan.FullCost - 0.01f));
        Assert.AreEqual(RadarScanState.Idle, scan.State);
        Assert.IsTrue(scan.Begin(scan.FullCost), "꼭 맞으면 걸린다");
    }

    [Test]
    public void 걸면_시간이_흐르고_배터리가_준다()
    {
        var scan = 관측();
        scan.Begin(100f);

        float 먹은것 = scan.Tick(1f, 0f, 100f);

        Assert.AreEqual(초당소모, 먹은것, 허용오차, "1초에 초당 소모만큼 먹는다");
        Assert.AreEqual(초당소모, scan.Drawn, 허용오차);
        Assert.AreEqual(1f, scan.Elapsed, 허용오차);
        Assert.AreEqual(1f / 관측시간, scan.Progress, 허용오차);
        Assert.AreEqual(RadarScanState.Scanning, scan.State);
    }

    [Test]
    public void 다_쌓으면_완료가_되고_값이_다_치러진다()
    {
        var scan = 관측();
        scan.Begin(100f);

        float 남음 = 100f;
        for (int i = 0; i < 6; i++) 남음 -= scan.Tick(1f, 0f, 남음);

        Assert.AreEqual(RadarScanState.Complete, scan.State);
        Assert.AreEqual(1f, scan.Progress, 허용오차);
        Assert.AreEqual(0f, scan.SecondsLeft, 허용오차);
        Assert.AreEqual(scan.FullCost, scan.Drawn, 허용오차);
        Assert.AreEqual(100f - scan.FullCost, 남음, 허용오차);
    }

    [Test]
    public void 끝난_관측은_시간이_더_흘러도_배터리를_먹지_않는다()
    {
        var scan = 관측();
        scan.Begin(100f);
        for (int i = 0; i < 6; i++) scan.Tick(1f, 0f, 100f);

        Assert.AreEqual(0f, scan.Tick(5f, 0f, 100f), 허용오차);
        Assert.AreEqual(scan.FullCost, scan.Drawn, 허용오차);
    }

    [Test]
    public void 결맞음_반경_안에서_몸을_뒤척이는_것은_괜찮다()
    {
        // 반경 자체가 걸음 한 번보다 좁으므로 여기서 너그러울 이유는 없지만,
        // 서 있는 동안의 미세한 흔들림까지 끊으면 장치를 쓸 수가 없다.
        var scan = 관측();
        scan.Begin(100f);

        scan.Tick(1f, scan.Band.CoherenceRadiusMeters, 100f);

        Assert.AreEqual(RadarScanState.Scanning, scan.State, "반경 위는 아직 안이다");
    }

    [Test]
    public void 반경을_벗어나면_끊긴다()
    {
        var scan = 관측();
        scan.Begin(100f);
        scan.Tick(1f, 0f, 100f);

        float 먹은것 = scan.Tick(1f, scan.Band.CoherenceRadiusMeters + 0.01f, 100f);

        Assert.AreEqual(RadarScanState.Cancelled, scan.State);
        Assert.AreEqual(RadarCancelReason.Moved, scan.CancelReason);
        Assert.AreEqual(0f, 먹은것, 허용오차, "이미 못 쓰게 된 장을 위해 더 먹으면 안 된다");
    }

    [Test]
    public void 끊겨도_쓴_배터리는_돌아오지_않는다()
    {
        // 정보를 사는 값이 배터리 + 정지 시간이다. 물러도 값을 돌려주면
        // 서 있는 것에 아무 무게가 없어진다.
        var scan = 관측();
        scan.Begin(100f);
        scan.Tick(2f, 0f, 100f);
        scan.Tick(0.1f, 5f, 100f);

        Assert.AreEqual(RadarScanState.Cancelled, scan.State);
        Assert.AreEqual(초당소모 * 2f, scan.Drawn, 허용오차);
    }

    [Test]
    public void 도중에_전원이_다하면_끊긴다()
    {
        // 랜턴이 같은 통을 함께 먹으므로 실제로 일어난다.
        var scan = 관측();
        scan.Begin(100f);
        scan.Tick(1f, 0f, 100f);

        float 먹은것 = scan.Tick(1f, 0f, 초당소모 - 0.01f);

        Assert.AreEqual(RadarScanState.Cancelled, scan.State);
        Assert.AreEqual(RadarCancelReason.PowerOut, scan.CancelReason);
        Assert.AreEqual(0f, 먹은것, 허용오차);
    }

    [Test]
    public void 사람이_끄면_끊긴다()
    {
        var scan = 관측();
        scan.Begin(100f);
        scan.Tick(1f, 0f, 100f);
        scan.Abort();

        Assert.AreEqual(RadarScanState.Cancelled, scan.State);
        Assert.AreEqual(RadarCancelReason.Aborted, scan.CancelReason);
    }

    [Test]
    public void 끊긴_뒤에는_시간이_흘러도_아무_일도_없다()
    {
        var scan = 관측();
        scan.Begin(100f);
        scan.Tick(1f, 99f, 100f);

        Assert.AreEqual(0f, scan.Tick(10f, 0f, 100f), 허용오차);
        Assert.AreEqual(RadarScanState.Cancelled, scan.State);
        scan.Abort();
        Assert.AreEqual(RadarCancelReason.Moved, scan.CancelReason, "끊긴 이유가 덮이면 안 된다");
    }

    [Test]
    public void 되돌리면_다시_걸_수_있다()
    {
        var scan = 관측();
        scan.Begin(100f);
        scan.Tick(1f, 99f, 100f);
        scan.Reset();

        Assert.AreEqual(RadarScanState.Idle, scan.State);
        Assert.AreEqual(RadarCancelReason.None, scan.CancelReason);
        Assert.AreEqual(0f, scan.Drawn, 허용오차);
        Assert.IsTrue(scan.Begin(100f));
    }

    [Test]
    public void 돌고_있는_관측을_또_걸_수_없다()
    {
        var scan = 관측();
        Assert.IsTrue(scan.Begin(100f));
        scan.Tick(2f, 0f, 100f);

        Assert.IsFalse(scan.Begin(100f), "다시 걸면 쌓아 둔 것이 사라진다");
        Assert.AreEqual(2f, scan.Elapsed, 허용오차);
    }

    [Test]
    public void 진행률은_0에서_1_사이를_벗어나지_않는다()
    {
        var scan = 관측();
        scan.Begin(1000f);
        scan.Tick(관측시간 * 3f, 0f, 1000f);

        Assert.AreEqual(1f, scan.Progress, 허용오차);
        Assert.AreEqual(관측시간, scan.Elapsed, 허용오차, "넘겨 쌓은 시간이 남으면 안 된다");
    }

    // ══ 전원 — 셀을 몇 개 끼워야 하는가 ══════════════════════

    [Test]
    public void 전하가_넉넉하면_셀을_끼우지_않는다()
    {
        Assert.AreEqual(0, RadarPowerRule.CellsNeeded(24f, 24f, 100f), "꼭 맞으면 끼울 것이 없다");
        Assert.AreEqual(0, RadarPowerRule.CellsNeeded(80f, 24f, 100f));
    }

    [Test]
    public void 모자라면_모자란_만큼만_끼운다()
    {
        // 남은 전하를 세지 않고 늘 한 개를 끼우면, 쓸 수 있는 전하가 통에서 넘쳐 버려진다.
        Assert.AreEqual(1, RadarPowerRule.CellsNeeded(0f, 24f, 100f));
        Assert.AreEqual(1, RadarPowerRule.CellsNeeded(23.9f, 24f, 100f));
        Assert.AreEqual(3, RadarPowerRule.CellsNeeded(0f, 250f, 100f), "한 개로 모자라면 여러 개다");
    }

    [Test]
    public void 셀이_채우지_못하는_통이면_영영_모자란다()
    {
        Assert.AreEqual(int.MaxValue, RadarPowerRule.CellsNeeded(0f, 24f, 0f),
            "채우는 양이 0인 셀로는 아무리 끼워도 못 채운다");
    }

    [Test]
    public void 끼워도_통보다_많이_담기지_않는다()
    {
        Assert.AreEqual(100f, RadarPowerRule.AfterInserting(80f, 3, 100f, 100f), 허용오차);
        Assert.AreEqual(50f, RadarPowerRule.AfterInserting(0f, 1, 50f, 100f), 허용오차);
        Assert.AreEqual(0f, RadarPowerRule.AfterInserting(0f, 0, 100f, 100f), 허용오차);
    }

    [Test]
    public void 대역이_없어도_기본값으로_선다()
    {
        // 에셋이 비어 있어도 조용히 터지지 않는다. 값은 기본 대역의 것이다.
        var scan = new RadarScan(null, 1f, 1f);

        Assert.IsNotNull(scan.Band);
        Assert.Greater(scan.Band.ResolutionMeters, 0f);
    }
}
