using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Survive.Localization;
using Survive.Vitals;
using Survive.World;

/// <summary>
/// A섬 구역 다섯 — <b>세 액체가 서로 다른 답을 낸다</b> (스펙 §21).
///
/// <b>이 파일이 못 박는 것은 역전이다.</b> 아프다(강) → 무해(얕은 바다) → 죽음(바다).
/// 계속 세지기만 하면 플레이어가 배우는 것은 "다음 물은 더 아프겠지" 하나뿐이고
/// 그것은 규칙이 아니라 경사다. 한 번 풀어 주면 <b>"이 물이 어떤 물인지 봐야 한다"</b>를
/// 배우게 된다. 나중에 누가 「정리」하면서 이 역전을 없애지 못하도록 여기서 막는다.
///
/// <b>세 특례가 아니라는 것도 검사한다.</b> 같은 함수에 같은 종류의 값을 넣어
/// 셋이 갈리는지를 보고, 구역 이름이 판정에 등장하지 않는 것을 코드 본문으로 확인한다.
/// </summary>
public class IslandZoneTests
{
    static List<GearCapability> 장비(params GearCapability[] 목록) => new List<GearCapability>(목록);
    static List<GearCapability> 맨몸 => new List<GearCapability>();
    static GearCapability 보행기(float 용량) => new GearCapability(TraversalGear.SurfaceWalker, 용량);

    // ── ① 세 액체가 서로 다른 답을 낸다 ─────────────────────

    /// <summary>
    /// <b>같은 판정 함수에 셋을 넣으면 셋이 갈린다.</b>
    /// 게이트 1 — 깎인다 / 무해하다 / 못 건넌다.
    /// </summary>
    [Test]
    public void 강은_깎이고_얕은_바다는_무해하고_바다는_못_건넌다()
    {
        Assert.AreEqual(CrossingVerdict.Costly,
            IslandZones.Verdict(IslandZone.River, 맨몸),
            "강은 잠기는 동안 살이 깎이지만 건널 수는 있어야 한다");

        Assert.AreEqual(CrossingVerdict.Harmless,
            IslandZones.Verdict(IslandZone.ShallowSea, 맨몸),
            "얕은 바다는 발을 딛고 걸어서 건넌다 — 값이 없다");

        Assert.AreEqual(CrossingVerdict.Fatal,
            IslandZones.Verdict(IslandZone.Sea, 맨몸),
            "바다는 맨몸으로 헤엄쳐 건널 수 없다");
    }

    /// <summary>
    /// <b>셋은 한 규칙의 세 값이다.</b> 판정에 들어가는 것은 깊이와 폭 두 수뿐이고,
    /// 그 둘을 바꾸면 같은 함수가 세 답을 다 낸다 — 구역을 거치지 않고도.
    /// </summary>
    [Test]
    public void 깊이와_폭_두_수만_바꾸면_같은_함수가_세_답을_다_낸다()
    {
        // 얕다 → 무해
        Assert.AreEqual(CrossingVerdict.Harmless,
            LiquidCrossing.Judge(new LiquidBody(LiquidKind.Macronium, 0.6f, 24f), 맨몸, 100f));

        // 깊고 좁다 → 깎인다
        Assert.AreEqual(CrossingVerdict.Costly,
            LiquidCrossing.Judge(new LiquidBody(LiquidKind.Macronium, 2f, 24f), 맨몸, 100f));

        // 깊고 넓다 → 못 건넌다
        Assert.AreEqual(CrossingVerdict.Fatal,
            LiquidCrossing.Judge(new LiquidBody(LiquidKind.Macronium, 2f, 200f), 맨몸, 100f));
    }

    /// <summary>
    /// <b>판정에 구역 이름이 없다.</b> 세 개의 특례였다면 규칙 안에 "강이라면"이
    /// 적혀 있을 것이다. 실제로 그렇지 않은지 코드 본문으로 확인한다 —
    /// 나중에 누가 특례를 하나 끼워 넣으면 여기서 걸린다.
    /// </summary>
    [Test]
    public void 판정_규칙_안에_구역_이름이_한_번도_나오지_않는다()
    {
        string 규칙 = File.ReadAllText(Path.Combine(
            Application.dataPath, "02.Scripts/Domain/World/LiquidCrossing.cs"));

        // 주석은 뺀다 — 왜 그런지를 적은 글에는 구역 이름이 나와야 한다.
        string 본문 = 주석을_지운다(규칙);

        // Sea만 빼고 본다 — 그 세 글자는 MacroniumSea·SeaImmersion처럼 물질과 상태의
        // 이름이기도 해서, 구역을 가리키는지 물질을 가리키는지 글자로는 갈리지 않는다.
        // 그 자리는 바로 아래 IslandZone 검사가 대신 막는다.
        foreach (var 구역 in System.Enum.GetNames(typeof(IslandZone)))
        {
            if (구역 == nameof(IslandZone.Sea)) continue;
            StringAssert.DoesNotContain(구역, 본문,
                $"판정 안에 {구역}이 이름으로 등장한다 — 그 순간 이것은 규칙이 아니라 특례다");
        }

        StringAssert.DoesNotContain("IslandZone", 본문,
            "판정이 구역을 알고 있으면 구역마다 답을 따로 적을 수 있게 된다");
    }

    /// <summary>
    /// <b>액면 보행 장비는 셋 다 무해하게 만든다.</b> 잠기지 않으니 값이 없다 —
    /// 이것도 새 규칙이 아니라 <see cref="EnvironmentThreat"/>가 이미 답하던 물음이다.
    /// </summary>
    [Test]
    public void 액면_보행_장비가_폭을_감당하면_잠기지_않는다()
    {
        var 넉넉히 = 장비(보행기(IslandZones.SeaWidth + 1f));

        foreach (var 구역 in new[] { IslandZone.River, IslandZone.ShallowSea, IslandZone.Sea })
            Assert.AreEqual(CrossingVerdict.Supported, IslandZones.Verdict(구역, 넉넉히),
                $"{구역}: 액면 위를 걷는 사람은 값을 치르지 않는다");
    }

    // ── ② 순서가 단조 증가가 아니다 ─────────────────────────

    /// <summary>
    /// <b>강 &gt; 얕은 바다.</b> 게이트 2 — 이 역전이 설계다.
    /// </summary>
    [Test]
    public void 위험도가_강에서_얕은_바다로_가며_떨어진다()
    {
        float 강 = IslandZones.Risk(IslandZone.River);
        float 얕은바다 = IslandZones.Risk(IslandZone.ShallowSea);

        Assert.Greater(강, 얕은바다,
            $"강({강:F1})이 얕은 바다({얕은바다:F1})보다 아파야 한다. " +
            "이 역전이 사라지면 플레이어가 배우는 것은 규칙이 아니라 경사다");
        Assert.AreEqual(0f, 얕은바다, 0.001f, "얕은 바다는 값이 아예 0이어야 한다");
    }

    /// <summary>
    /// <b>걸어 나가는 순서로 늘어놓으면 한 번 내려갔다 올라간다.</b>
    /// 단조 증가라면 이 검사가 실패한다.
    /// </summary>
    [Test]
    public void 걸어_나가는_순서의_위험도가_단조_증가가_아니다()
    {
        var 위험 = IslandZones.Order.Select(IslandZones.Risk).ToArray();

        bool 내려간적있다 = false;
        for (int i = 1; i < 위험.Length; i++)
            if (위험[i] < 위험[i - 1]) { 내려간적있다 = true; break; }

        Assert.IsTrue(내려간적있다,
            "위험도가 한 번도 내려가지 않는다 — 순서가 경사가 됐다: " +
            string.Join(" → ", IslandZones.Order.Select(z => $"{z}({IslandZones.Risk(z):F0})")));

        // 그리고 끝은 제일 세다. 풀어 준 다음에 조이는 것이 요점이므로.
        Assert.AreEqual(IslandZone.Sea, IslandZones.Order[IslandZones.Order.Length - 2],
            "바다가 B섬 바로 앞에 있어야 마지막 조임이 된다");
        Assert.Greater(IslandZones.Risk(IslandZone.Sea), IslandZones.Risk(IslandZone.River),
            "바다가 강보다 세지 않으면 마지막 조임이 없다");
    }

    // ── ③ 규칙이 요구하는 하한 ──────────────────────────────

    /// <summary>
    /// <b>바다가 죽이는 것은 바다라서가 아니라 넓어서다.</b>
    /// 하한보다 좁으면 헤엄쳐 건널 수 있게 되고, 그러면 액면 보행 장비가 아무것도 열지 않는다.
    /// </summary>
    [Test]
    public void 바다는_맨몸으로_헤엄쳐_건널_수_없는_폭_이상이다()
    {
        Assert.Greater(IslandZones.SeaWidth, IslandZones.SeaMinimumWidth,
            $"바다 폭 {IslandZones.SeaWidth:F1}m가 하한 {IslandZones.SeaMinimumWidth:F1}m를 넘지 못한다");

        // 하한 자체가 규칙에서 나온 수인지. 손으로 적은 상수였다면 여기서 갈라진다.
        float 손계산 = IslandZones.FullHealth / MacroniumSea.CorrosionPerSecond * LiquidCrossing.SwimSpeed;
        Assert.AreEqual(손계산, IslandZones.SeaMinimumWidth, 0.01f);
    }

    /// <summary>강을 두 배로 넓히면 값도 두 배다 — 폭이 값을 정한다.</summary>
    [Test]
    public void 강은_폭이_값을_정한다()
    {
        var 강 = IslandZones.LiquidAt(IslandZone.River);
        Assert.AreEqual(LiquidCrossing.Toll(강) * 2f,
                        LiquidCrossing.Toll(강.Widened(강.Width * 2f)), 0.01f);
    }

    /// <summary>
    /// <b>얕은 바다는 폭이 값을 바꾸지 않는다.</b> 무해한 이유가 "좁아서"가 아니라
    /// "발이 바닥에 닿아서"라는 것을 이 검사가 보인다.
    /// </summary>
    [Test]
    public void 얕은_바다는_열_배로_넓혀도_무해하다()
    {
        var 얕은바다 = IslandZones.LiquidAt(IslandZone.ShallowSea);
        Assert.AreEqual(0f, LiquidCrossing.Toll(얕은바다.Widened(얕은바다.Width * 10f)), 0.001f,
            "폭이 값을 바꿨다면 무해의 근거가 footing이 아니라는 뜻이다");
    }

    /// <summary>
    /// 발이 바닥에서 떨어지는 순간 같은 얕은 바다가 아프기 시작한다 —
    /// 무해의 근거를 반대쪽에서 확인한다.
    /// </summary>
    [Test]
    public void 발이_바닥에서_떨어지면_같은_폭도_아프다()
    {
        var 얕은바다 = IslandZones.LiquidAt(IslandZone.ShallowSea);
        var 깊어진것 = new LiquidBody(얕은바다.Kind, LiquidCrossing.SwimDepth, 얕은바다.Width);

        Assert.AreEqual(0f, LiquidCrossing.Toll(얕은바다), 0.001f);
        Assert.Greater(LiquidCrossing.Toll(깊어진것), 0f);
    }

    // ── ④ 수의 출처 ─────────────────────────────────────────

    /// <summary>
    /// Domain이 들고 있는 임계값이 <b>실제 몸의 값과 같은가</b>.
    /// 둘이 갈라지면 규칙이 "얕다"고 답하는 자리에서 몸은 헤엄치고 있게 된다.
    /// </summary>
    [Test]
    public void 잠김_임계값이_PlayerSwimming의_기본값과_같다()
    {
        string 원문 = File.ReadAllText(Path.Combine(
            Application.dataPath, "02.Scripts/Player/PlayerSwimming.cs"));

        Assert.AreEqual(LiquidCrossing.SwimDepth, 기본값을_읽는다(원문, "swimDepth"), 0.001f);
        Assert.AreEqual(LiquidCrossing.WadeDepth, 기본값을_읽는다(원문, "wadeDepth"), 0.001f);
    }

    /// <summary>수영 속도도 같은 이유로 몸에서 온다.</summary>
    [Test]
    public void 수영_속도가_PlayerLocomotion의_기본값과_같다()
    {
        string 원문 = File.ReadAllText(Path.Combine(
            Application.dataPath, "02.Scripts/Player/PlayerLocomotion.cs"));

        Assert.AreEqual(LiquidCrossing.SwimSpeed, 기본값을_읽는다(원문, "swimSpeed"), 0.001f);
    }

    /// <summary>체력의 최대치도 손으로 적은 수가 아니라 에셋에서 온 수여야 한다.</summary>
    [Test]
    public void 체력_최대치가_Health_에셋과_같다()
    {
        var 체력 = AssetDatabase.LoadAssetAtPath<VitalDefinitionSO>("Assets/08.Data/Vitals/Health.asset");
        Assert.IsNotNull(체력, "Health.asset을 못 읽었다");
        Assert.AreEqual(체력.maxValue, IslandZones.FullHealth, 0.001f,
            "체력 최대치가 바뀌었는데 바다의 하한이 따라가지 않았다");
    }

    // ── ⑤ B섬 도착 판정 ─────────────────────────────────────

    /// <summary>게이트 4 — <b>한 번만 참이 되고, 되돌아가도 취소되지 않는다.</b></summary>
    [Test]
    public void B섬_도착은_한_번만_참이_된다()
    {
        var 도착 = new IslandArrival();

        Assert.IsFalse(도착.Arrived, "시작할 때는 도착하지 않았다");
        Assert.IsFalse(도착.Observe(IslandZone.A1));
        Assert.IsFalse(도착.Observe(IslandZone.Sea), "바다 위는 아직 B섬이 아니다");

        Assert.IsTrue(도착.Observe(IslandZone.IslandB), "처음 들어선 순간에만 참이다");
        Assert.IsTrue(도착.Arrived);

        Assert.IsFalse(도착.Observe(IslandZone.IslandB), "두 번째부터는 방아쇠가 아니다");
    }

    [Test]
    public void B섬에서_되돌아가도_도착은_취소되지_않는다()
    {
        var 도착 = new IslandArrival();
        도착.Observe(IslandZone.IslandB);

        foreach (var 구역 in IslandZones.Order)
        {
            도착.Observe(구역);
            Assert.IsTrue(도착.Arrived, $"{구역}으로 물러섰더니 도착이 풀렸다 — 되돌릴 수 없는 진행도여야 한다");
        }
    }

    /// <summary>
    /// <b>「도착」은 지면을 밟는 것이 아니라 구역에 드는 것이다.</b>
    /// 액면 위를 걸어 들어오는 사람도 도착해야 하므로, 판정에 지면이 들어오면 안 된다.
    /// </summary>
    [Test]
    public void 도착_판정에_지면_접촉이_들어오지_않는다()
    {
        string 원문 = 주석을_지운다(File.ReadAllText(Path.Combine(
            Application.dataPath, "02.Scripts/Domain/World/IslandArrival.cs")));

        foreach (var 말 in new[] { "grounded", "Grounded", "ground", "Ground" })
            StringAssert.DoesNotContain(말, 원문,
                "도착이 지면 접촉을 보면 액면 보행 장비로 걸어 들어온 사람은 영영 도착하지 않는다");
    }

    // ── ⑥ 자리 조회 ─────────────────────────────────────────

    class 가짜구역 : IIslandZoneSource
    {
        public Vector3 IslandZoneCenter { get; set; }
        public float IslandZoneRadius { get; set; }
        public IslandZone Zone { get; set; }
    }

    [TearDown]
    public void 등록부를_비운다() => IslandZoneRegistry.Clear();

    [Test]
    public void 아무_구역에도_들지_않으면_모른다고_답한다()
    {
        IslandZoneRegistry.Clear();
        Assert.IsFalse(IslandZoneRegistry.TryAt(Vector3.zero, out _),
            "구역이 안 놓인 세계에서 A-1이라고 답하면 §5가 엉뚱한 자리에서 각성한다");
    }

    /// <summary>강이 A섬 안을 가로지르려면 안쪽의 좁은 구역이 바깥을 덮을 수 있어야 한다.</summary>
    [Test]
    public void 나중에_등록된_좁은_구역이_넓은_구역을_덮는다()
    {
        IslandZoneRegistry.Clear();
        IslandZoneRegistry.Register(new 가짜구역
        { IslandZoneCenter = Vector3.zero, IslandZoneRadius = 100f, Zone = IslandZone.A2 });
        IslandZoneRegistry.Register(new 가짜구역
        { IslandZoneCenter = Vector3.zero, IslandZoneRadius = 5f, Zone = IslandZone.River });

        Assert.IsTrue(IslandZoneRegistry.TryAt(Vector3.zero, out var 안쪽));
        Assert.AreEqual(IslandZone.River, 안쪽);

        Assert.IsTrue(IslandZoneRegistry.TryAt(new Vector3(50f, 0f, 0f), out var 바깥));
        Assert.AreEqual(IslandZone.A2, 바깥);
    }

    [Test]
    public void B섬_구역에_들어서면_도착이_켜진다()
    {
        IslandZoneRegistry.Clear();
        IslandZoneRegistry.Register(new 가짜구역
        { IslandZoneCenter = new Vector3(400f, 0f, 0f), IslandZoneRadius = 60f, Zone = IslandZone.IslandB });

        var 도착 = new IslandArrival();

        if (IslandZoneRegistry.TryAt(Vector3.zero, out var 여기)) 도착.Observe(여기);
        Assert.IsFalse(도착.Arrived);

        Assert.IsTrue(IslandZoneRegistry.TryAt(new Vector3(400f, 0f, 0f), out var 거기));
        Assert.IsTrue(도착.Observe(거기));
    }

    // ── ⑦ 이름 ──────────────────────────────────────────────

    /// <summary>게이트 9 — 구역 이름이 화면에 나간다면 번역 표에 자리가 있다.</summary>
    [Test]
    public void 구역마다_번역_키가_표에_있다()
    {
        var 표 = LocalizationTestBootstrap.LoadCatalogFromDisk();

        foreach (var 구역 in IslandZones.Order)
        {
            string 키 = IslandZones.NameKey(구역);
            Assert.IsNotEmpty(키, $"{구역}에 번역 키가 없다");
            Assert.IsTrue(표.Contains(new LocKey(IslandZones.NameCategory, 키)),
                $"번역 표에 {IslandZones.NameCategory}/{키}가 없다");
        }
    }

    /// <summary>
    /// 게이트 8 — <b>화면에 「작은 섬」이 0건.</b>
    ///
    /// 내부 id <c>a_islet</c>과 에셋 이름은 그대로 둔다(회신 ⑬) — 그것까지 바꾸는 것은
    /// 비용 대비 이득이 없다. 다만 <b>사람 눈에 닿는 글자</b>는 새 지도를 가리켜야 한다.
    /// </summary>
    [Test]
    public void 화면에_나가는_글자에_옛_지도_이름이_없다()
    {
        string csv = File.ReadAllText(LocalizationTestBootstrap.CsvPath);
        var 걸린줄 = new List<string>();

        var 줄들 = csv.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < 줄들.Length; i++)
        {
            string 줄 = 줄들[i];
            if (줄.TrimStart().StartsWith("#")) continue;   // 주석은 화면에 안 나간다
            if (줄.Contains("작은 섬")) 걸린줄.Add($"strings.csv:{i + 1}  {줄.Trim()}");
        }

        Assert.IsEmpty(걸린줄,
            "화면에 나가는 글자가 아직 옛 지도를 가리킨다:\n  " + string.Join("\n  ", 걸린줄));
    }

    // ── 도구 ────────────────────────────────────────────────

    static string 주석을_지운다(string 원문) =>
        Regex.Replace(Regex.Replace(원문, @"/\*.*?\*/", "", RegexOptions.Singleline),
                      @"//.*?$", "", RegexOptions.Multiline);

    /// <summary><c>[SerializeField] float 이름 = 1.23f;</c>에서 그 수를 읽는다.</summary>
    static float 기본값을_읽는다(string 원문, string 이름)
    {
        var m = Regex.Match(원문, @"\b" + Regex.Escape(이름) + @"\s*=\s*(-?[0-9.]+)f?\s*;");
        Assert.IsTrue(m.Success, $"{이름}의 기본값을 원문에서 찾지 못했다 — 필드 이름이 바뀌었는가");
        return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }
}
