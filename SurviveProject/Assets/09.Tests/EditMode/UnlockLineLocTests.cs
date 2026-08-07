using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Survive.Localization;
using Survive.Progression;

/// <summary>
/// <b>현장 발견·연구 대사가 번역 표를 거친다</b> (2026-08-07).
///
/// <b>무엇이 뚫려 있었는가.</b> 프롤로그 자막을 표에 얹은 바로 그 라운드에 같은
/// 구멍이 하나 더 남아 있었다. <c>UnlockService.Announce</c>가 대사 줄
/// (<c>SequenceSO.Line</c>)을 <b>날것으로</b> 받아 큐에 세웠다. 큐가 글자를 물고
/// 있으니 화면에 나가는 것이 에셋에서 곧장 왔고, 그래서 재료를 처음 주울 때마다
/// 유물을 밝힐 때마다 나오는 그 대사만 표 밖에 있었다.
///
/// <b>프롤로그보다 훨씬 자주 나온다.</b> 프롤로그는 판당 한 번이고 이쪽은 재료
/// 종류마다 한 번, 연구 항목마다 한 번이다. 영어를 켜면 그 전부가 한국어로 남는다.
///
/// <b>지금은 티가 안 난다는 것이 함정이다.</b> 표와 에셋의 값이 같아서 한국어
/// 화면에서는 아무 신호도 나지 않는다. 그래서 여기서 재는 것은 "글자가 맞는가"가
/// 아니라 <b>어느 길로 왔는가</b>다.
///
/// 우회가 되살아나는 것 자체는
/// <c>DataTextGateTests.대사_한_줄을_직접_읽는_곳이_없다</c>(예외 목록 0건)와
/// 아래 <see cref="대사_줄을_Announce에_넘기는_곳이_없다"/>가 막는다.
/// 여기서는 <b>값이 실제로 표에서 나오는지</b>를 본다.
/// </summary>
public class UnlockLineLocTests
{
    /// <summary>E2E가 무는 짝. 이 둘만 <c>en</c> 칸이 차 있다.</summary>
    const string 스크랩발견 = "Assets/08.Data/Progression/Discoveries/disc_scrap.asset";
    const string 액면연구 = "Assets/08.Data/Progression/Research/res_surface_walker.asset";

    DiscoverySO _발견;
    ResearchEntrySO _연구;
    string _처음로케일;

    [SetUp]
    public void 준비한다()
    {
        _발견 = AssetDatabase.LoadAssetAtPath<DiscoverySO>(스크랩발견);
        _연구 = AssetDatabase.LoadAssetAtPath<ResearchEntrySO>(액면연구);
        Assert.IsNotNull(_발견, $"{스크랩발견}를 못 읽었다");
        Assert.IsNotNull(_연구, $"{액면연구}를 못 읽었다");

        _처음로케일 = Loc.CurrentLocale;
        Loc.Load(LocalizationTestBootstrap.LoadCatalogFromDisk());
        Loc.SetLocale(StringCatalog.DefaultLocale);
    }

    [TearDown]
    public void 되돌린다()
    {
        Loc.Load(LocalizationTestBootstrap.LoadCatalogFromDisk());
        Loc.SetLocale(_처음로케일 ?? StringCatalog.DefaultLocale);
    }

    // ── ① 표가 이긴다 ───────────────────────────────────────────

    /// <summary>
    /// <b>에셋과 표가 갈라지면 표가 이긴다.</b> 두 값이 같은 동안에는 같은 답이 두
    /// 길에서 나오므로, 일부러 갈라 놓지 않으면 어느 길로 왔는지 알 수 없다.
    /// </summary>
    [Test]
    public void 에셋과_표가_갈라지면_표가_이긴다()
    {
        const string 딴글 = "표에서만 고친 발견 대사";
        var 열쇠 = DataText.LineKey(_발견);

        Loc.Load(표를_고쳐서(열쇠, 딴글));

        Assert.AreEqual(딴글, SpokenLine.Of(_발견).Text,
            "표를 고쳤는데 대사가 안 바뀌었다 — 큐가 에셋 원문을 직접 물고 있다");
    }

    /// <summary>
    /// <b>표를 통째로 치우면 에셋 원문이 선다.</b> 배포본에서 CSV가 빠져도 게임이
    /// 한국어로 돌아가게 하는 마지막 그물이고, 키를 그대로 내주는
    /// <see cref="Loc"/>의 계약과 다른 자리다.
    /// </summary>
    [Test]
    public void 표가_없으면_에셋_원문으로_물러선다()
    {
        Loc.Load(StringCatalog.Empty);

        Assert.AreEqual(_발견.line.text.Trim(), SpokenLine.Of(_발견).Text,
            "표 없이 원문을 내지 않는다");
        Assert.AreEqual(_발견.line.speaker.Trim(), SpokenLine.Of(_발견).Speaker,
            "화자가 표 없이 원문을 내지 않는다");
        Assert.AreEqual(_연구.line.text.Trim(), SpokenLine.Of(_연구).Text,
            "연구 대사가 표 없이 원문을 내지 않는다");
    }

    // ── ② 로케일을 따라온다 ─────────────────────────────────────

    /// <summary>
    /// <b>로케일을 바꾸면 현장 발견 대사가 바뀐다.</b> 배선이 없던 시절에는 여기가
    /// 통과할 수 없었다 — 에셋에는 로케일이라는 개념이 없다.
    /// </summary>
    [Test]
    public void 로케일을_바꾸면_현장_발견_대사가_바뀐다()
    {
        int 확인 = 로케일을_따라오는_줄_수(모든_에셋<DiscoverySO>(), d => DataText.LineKey(d),
                                          d => SpokenLine.Of(d).Text);

        Assert.Greater(확인, 0,
            "Discovery의 en 칸이 하나도 안 차 있다 — 로케일 전환을 잴 수 없으므로 표에 채워라");
    }

    /// <summary><b>연구 대사도 같다.</b> 화자가 같으면 지나는 길도 같아야 한다.</summary>
    [Test]
    public void 로케일을_바꾸면_연구_대사가_바뀐다()
    {
        int 확인 = 로케일을_따라오는_줄_수(모든_에셋<ResearchEntrySO>(), e => DataText.LineKey(e),
                                          e => SpokenLine.Of(e).Text);

        Assert.Greater(확인, 0,
            "Research의 en 칸이 하나도 안 차 있다 — 로케일 전환을 잴 수 없으므로 표에 채워라");
    }

    /// <summary>
    /// <b>의사 번역이 부푼다.</b> 로케일 전환보다 이쪽이 넓게 잡는다 —
    /// <c>en</c> 칸이 비어 있는 줄까지 판정할 수 있다(<c>docs/번역-체계.md</c> §6).
    /// 대사 하나가 표 밖에 있으면 그 하나만 부풀지 않는다.
    /// </summary>
    [Test]
    public void 의사_번역에서_모든_발견과_연구_대사가_부푼다()
    {
        var 표 = LocalizationTestBootstrap.LoadCatalogFromDisk();
        Loc.SetLocale(StringCatalog.PseudoLocale);

        var 안부푼것 = new List<string>();
        int 본것 = 0;

        foreach (var d in 모든_에셋<DiscoverySO>())
            본것 += 부풀었는지_본다(표, d.name, DataText.SpeakerKey(d), DataText.LineKey(d),
                                   SpokenLine.Of(d), 안부푼것);

        foreach (var e in 모든_에셋<ResearchEntrySO>())
            본것 += 부풀었는지_본다(표, e.name, DataText.SpeakerKey(e), DataText.LineKey(e),
                                   SpokenLine.Of(e), 안부푼것);

        Assert.Greater(본것, 0, "표에 발견·연구 대사가 하나도 없다 — 검사가 헛돈다");
        Assert.IsEmpty(안부푼것,
            "부풀지 않은 대사가 있다 — 그 자리는 표를 거치지 않는다:\n  " +
            string.Join("\n  ", 안부푼것));
    }

    /// <summary>표에 있는 줄만 본다. 표 밖의 줄은 일부러 부풀리지 않는다(§6).</summary>
    static int 부풀었는지_본다(StringCatalog 표, string 이름, LocKey 화자열쇠, LocKey 대사열쇠,
                              SpokenLine 줄, List<string> 안부푼것)
    {
        int 본것 = 0;

        if (표.Contains(대사열쇠))
        {
            본것++;
            if (!PseudoLocalizer.IsTransformed(줄.Text)) 안부푼것.Add($"{이름} 대사 \"{줄.Text}\"");
        }
        if (표.Contains(화자열쇠))
        {
            본것++;
            if (!PseudoLocalizer.IsTransformed(줄.Speaker)) 안부푼것.Add($"{이름} 화자 \"{줄.Speaker}\"");
        }

        return 본것;
    }

    // ── ③ 언제 조회하는가 ───────────────────────────────────────

    /// <summary>
    /// <b>큐에서 기다리는 동안 로케일이 바뀌면 따라온다.</b> 이것이 큐가 글자 대신
    /// 주인을 무는 진짜 이유다.
    ///
    /// 대사는 앞줄이 끝나기를 몇 초씩 기다리는 물건이다. 상자 하나를 열어 새 재료가
    /// 셋 들어오면 세 줄이 줄을 서고, 마지막 줄은 십여 초 뒤에 뜬다. 큐에 넣는
    /// 순간 문자열을 만들어 두면 그 사이의 전환이 이 줄들만 비껴간다 — 화면에
    /// 두 언어가 섞여 뜬다.
    /// </summary>
    [Test]
    public void 큐에_세워_둔_줄이_나중에_바뀐_로케일을_따라온다()
    {
        var 표 = LocalizationTestBootstrap.LoadCatalogFromDisk();
        Assert.IsTrue(표.TryGet("en", DataText.LineKey(_발견), out var 영어),
            "시험체의 en 칸이 비었다 — 이 검사를 세울 수 없다");

        // 큐에 넣는 순간(ko)
        var 줄 = SpokenLine.Of(_발견);
        string 넣을때 = 줄.Text;

        // 앞줄을 기다리는 사이에 사람이 언어를 바꿨다
        Loc.SetLocale("en");

        Assert.AreEqual(영어, 줄.Text,
            "큐에 서 있던 줄이 옛 로케일 그대로다 — 넣을 때 문자열을 굳혔다는 뜻이다");
        Assert.AreNotEqual(넣을때, 줄.Text, "로케일을 바꿨는데 같은 글이 나온다");
    }

    /// <summary>
    /// <b>유지 시간은 번역하지 않는다.</b> 배치 값이지 글자가 아니다.
    /// 표에 실으면 번역가가 초를 고칠 수 있게 되고, 그것은 아무도 원하지 않는다.
    /// </summary>
    [Test]
    public void 유지_시간은_로케일과_무관하다()
    {
        float ko = SpokenLine.Of(_발견).HoldSeconds;
        Loc.SetLocale("en");
        Assert.AreEqual(ko, SpokenLine.Of(_발견).HoldSeconds, "유지 시간이 로케일을 탄다");
        Assert.Greater(ko, 0f, "에셋의 유지 시간이 0이다 — 시험체가 틀렸다");
    }

    /// <summary>
    /// 주인이 없으면 조용히 빈 줄이다. <c>default(LocKey)</c>는 두 필드가 null이라
    /// 만지면 터진다 — 그 자리를 실제로 지나가는지 본다.
    /// </summary>
    [Test]
    public void 주인이_없으면_빈_줄이고_터지지_않는다()
    {
        Assert.AreEqual("", SpokenLine.Of((DiscoverySO)null).Text);
        Assert.AreEqual("", SpokenLine.Of((ResearchEntrySO)null).Speaker);
        Assert.AreEqual("", default(SpokenLine).Text);
        Assert.IsTrue(default(SpokenLine).IsEmpty);
        Assert.AreEqual(0f, default(SpokenLine).HoldSeconds);
    }

    // ── ④ 호출자가 주인을 넘긴다 ────────────────────────────────

    /// <summary>
    /// 대사 줄을 <c>Announce</c>에 넘기는 꼴. <c>DataTextGateTests</c>의 대사 검사는
    /// <c>.line.text</c>를 <b>읽는</b> 자리를 잡는데, <c>Announce(d.line)</c>은 읽지
    /// 않고 <b>넘기기만</b> 해서 그 그물을 빠져나간다. 그것이 이 구멍의 실제 꼴이었다.
    /// </summary>
    static readonly Regex 줄을_넘기는_꼴 = new Regex(@"Announce\s*\([^)]*\.\s*line\b");

    [Test]
    public void 대사_줄을_Announce에_넘기는_곳이_없다()
    {
        var root = Path.Combine(Application.dataPath, "02.Scripts");
        var 위반 = new List<string>();
        int 검사한파일 = 0;

        foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            검사한파일++;
            var 줄들 = LocSourceScanner.StripComments(File.ReadAllText(path)).Split('\n');
            for (int i = 0; i < 줄들.Length; i++)
                if (줄을_넘기는_꼴.IsMatch(줄들[i]))
                    위반.Add($"{Relative(path)}:{i + 1}  {줄들[i].Trim()}");
        }

        Assert.Greater(검사한파일, 100, "코드를 못 찾았다 — 검사가 공회전한다");

        Assert.IsEmpty(위반,
            "대사 줄을 Announce에 날것으로 넘긴다. 큐는 줄의 주인을 들어야 하고 " +
            "(SpokenLine.Of), 그래야 화면에 띄우는 순간 표를 뒤질 수 있다:\n  " +
            string.Join("\n  ", 위반));
    }

    /// <summary>
    /// <b>음성 확인.</b> 고치기 전의 호출자 셋이 쓰던 꼴을 그대로 먹여 본다.
    /// 게이트가 그것을 못 물면 0건은 아무 뜻이 없다.
    /// </summary>
    [Test]
    public void 배선을_걷어낸_꼴을_먹이면_잡힌다()
    {
        var 물어야하는것 = new[]
        {
            "service.Announce(d.line);",
            "Announce(discovery.line);",
            "if (UnlockService.Instance != null) UnlockService.Instance.Announce(done.line);",
            "svc.Announce( entry . line );",
        };

        foreach (var 줄 in 물어야하는것)
            Assert.IsTrue(줄을_넘기는_꼴.IsMatch(줄), $"게이트가 이 꼴을 못 문다: {줄}");

        var 물면_안_되는것 = new[]
        {
            "service.Announce(d);",
            "Announce(SpokenLine.Of(discovery));",
            "public void Announce(SpokenLine line)",
            "_pending.Enqueue(line);",
            "Announce(entry);",
        };

        foreach (var 줄 in 물면_안_되는것)
            Assert.IsFalse(줄을_넘기는_꼴.IsMatch(줄), $"게이트가 멀쩡한 꼴을 문다: {줄}");
    }

    /// <summary>
    /// 대사 줄을 받는 과부하 자체가 없어야 한다. 남겨 두면 다음 사람이 그쪽으로
    /// 부르고, 그 호출은 위 게이트를 지나간다(<c>Announce(만든줄)</c>은 <c>.line</c>이
    /// 안 보인다).
    /// </summary>
    [Test]
    public void 대사_줄을_받는_과부하가_없다()
    {
        string 소스 = File.ReadAllText(Path.Combine(
            Application.dataPath, "02.Scripts", "Progression", "UnlockService.cs"));

        StringAssert.DoesNotContain("Announce(SequenceSO.Line", 소스,
            "대사 줄을 받는 문이 남아 있다 — 그 문으로 들어오면 표를 우회한다");
        StringAssert.Contains("Announce(SpokenLine", 소스, "주인을 받는 문이 없다");
    }

    // ── 훑개 ────────────────────────────────────────────────────

    /// <summary>
    /// <c>en</c> 칸이 찬 줄에 대해 "표의 en 값이 그대로 나오고 ko와 다르다"를 본다.
    /// 몇 줄을 실제로 확인했는지 돌려준다 — 0이면 검사가 헛돈 것이다.
    /// </summary>
    static int 로케일을_따라오는_줄_수<T>(IEnumerable<T> 에셋들,
                                          System.Func<T, LocKey> 열쇠짓기,
                                          System.Func<T, string> 읽기)
    {
        var 표 = LocalizationTestBootstrap.LoadCatalogFromDisk();
        int 확인 = 0;

        foreach (var a in 에셋들)
        {
            var 열쇠 = 열쇠짓기(a);
            if (!표.TryGet("en", 열쇠, out var 영어)) continue;

            Loc.SetLocale(StringCatalog.DefaultLocale);
            string 한국어 = 읽기(a);

            Loc.SetLocale("en");
            Assert.AreEqual(영어, 읽기(a), $"{열쇠}가 en 칸을 따라오지 않는다");
            Assert.AreNotEqual(한국어, 읽기(a), $"{열쇠}가 로케일을 바꿔도 한국어 그대로다");
            확인++;
        }

        Loc.SetLocale(StringCatalog.DefaultLocale);
        return 확인;
    }

    static List<T> 모든_에셋<T>() where T : Object =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name)
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(a => a != null)
            .ToList();

    /// <summary>
    /// 디스크의 표에서 한 줄만 바꿔 다시 읽는다. <b>CSV 글자를 고쳐서 다시 파싱한다</b> —
    /// 표를 손으로 지어 넣으면 파서를 건너뛰게 되고, 그러면 진짜 조회 경로를 밟지 않는다.
    /// </summary>
    static StringCatalog 표를_고쳐서(LocKey 열쇠, string 새값)
    {
        var 원본 = File.ReadAllLines(LocalizationTestBootstrap.CsvPath);
        var 나온것 = new List<string>(원본.Length);
        string 머리 = 열쇠.Category + "," + 열쇠.Key + ",";
        bool 찾았다 = false;

        foreach (var 줄 in 원본)
        {
            if (!줄.StartsWith(머리, System.StringComparison.Ordinal)) { 나온것.Add(줄); continue; }

            찾았다 = true;
            if (새값 == null) continue;
            나온것.Add(머리 + 새값);
        }

        Assert.IsTrue(찾았다, $"표에서 {열쇠} 줄을 못 찾았다 — 시험체가 틀렸다");

        var 표 = StringCatalog.Parse(string.Join("\n", 나온것));
        Assert.IsEmpty(표.Problems, "고쳐 만든 표가 건전하지 않다:\n  " + string.Join("\n  ", 표.Problems));
        return 표;
    }

    static string Relative(string full) =>
        "Assets" + full.Substring(Application.dataPath.Length).Replace('\\', '/');
}
