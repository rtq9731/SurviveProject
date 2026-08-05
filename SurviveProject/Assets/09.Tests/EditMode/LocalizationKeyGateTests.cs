using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using Survive.Localization;

/// <summary>
/// 번역 층의 게이트. <b>이 파일이 이 작업의 핵심 산출물이다.</b>
///
/// 번역 층에서 제일 흔한 고장은 키 오타이고, 그것은 그 화면을 실제로 열어 보기
/// 전까지 아무 신호도 내지 않는다. 화면 수가 늘수록 "전부 열어 봤다"가 불가능해진다.
/// 그래서 코드가 부르는 키를 긁어 표와 대조한다 — 컴파일 직후에 잡힌다.
///
/// 반대 방향(표에 있는데 아무도 안 쓰는 키)은 <b>경고로만</b> 남긴다.
/// 데이터 에셋 경로(<c>Item/{id}.name</c>)에서 쓰일 키는 코드에 리터럴로 나타나지 않는다.
/// </summary>
public class LocalizationKeyGateTests
{
    /// <summary>긁을 코드가 있는 곳. 테스트 코드는 제외한다 — 일부러 없는 키를 부른다.</summary>
    static string ScriptRoot => Path.Combine(Application.dataPath, "02.Scripts");

    static StringCatalog _catalog;
    static List<LocSourceScanner.Call> _calls;
    static int _dynamicCalls;
    static int _scannedFiles;

    [OneTimeSetUp]
    public void 표와_코드를_한_번씩_읽는다()
    {
        _catalog = LocalizationTestBootstrap.LoadCatalogFromDisk();
        _calls = new List<LocSourceScanner.Call>();
        _dynamicCalls = 0;
        _scannedFiles = 0;

        foreach (var path in Directory.EnumerateFiles(ScriptRoot, "*.cs", SearchOption.AllDirectories))
        {
            _scannedFiles++;
            var result = LocSourceScanner.Scan(File.ReadAllText(path));
            foreach (var call in result.Literals) _calls.Add(call);
            _dynamicCalls += result.DynamicLines.Count;

            foreach (var line in result.DynamicLines)
                Debug.Log($"[번역 게이트] 검사할 수 없는 호출(인자가 리터럴이 아니다): " +
                          $"{Relative(path)}:{line}");
        }
    }

    static string Relative(string full) =>
        "Assets" + full.Substring(Application.dataPath.Length).Replace('\\', '/');

    // ── ① 코드가 부르는 키 ──────────────────────────────────

    [Test]
    public void 코드가_부르는_키는_전부_표에_있다()
    {
        var missing = _calls
            .Where(c => !_catalog.Contains(c.AsKey()))
            .Select(c => c.AsKey().ToString())
            .Distinct()
            .ToList();

        Assert.IsEmpty(missing,
            "표에 없는 키를 부르는 자리가 있다. Assets/Resources/Localization/strings.csv에 줄을 더해라:\n  " +
            string.Join("\n  ", missing));
    }

    [Test]
    public void 긁힌_호출이_실제로_있다()
    {
        // 정규식이 조용히 아무것도 못 잡게 되면 위 검사가 공회전한다.
        Assert.Greater(_scannedFiles, 100, "코드를 못 찾았다");
        Assert.Greater(_calls.Count, 0, "Loc.T 호출을 하나도 못 긁었다 — 게이트가 아무것도 막지 못한다");
    }

    [Test]
    public void 아무도_안_쓰는_키는_경고로만_남긴다()
    {
        var used = new HashSet<LocKey>(_calls.Select(c => c.AsKey()));
        var unused = _catalog.Keys.Where(k => !used.Contains(k)).ToList();

        // 실패시키지 않는다. 데이터 에셋 경로에서 쓰일 키는 코드에 리터럴로 없다.
        if (unused.Count > 0)
            Debug.LogWarning($"[번역 게이트] 코드에서 부르지 않는 키 {unused.Count}개 " +
                             "(데이터 에셋 경로에서 쓰일 수 있다):\n  " +
                             string.Join("\n  ", unused.Select(k => k.ToString())));

        Assert.Pass($"코드가 부르는 키 {used.Count}개, 표에 {_catalog.Keys.Count}개, " +
                    $"검사 불가 호출 {_dynamicCalls}개");
    }

    // ── ② 표 자체의 건전성 ──────────────────────────────────

    [Test]
    public void 표에_건전성_문제가_없다()
    {
        Assert.IsEmpty(_catalog.Problems,
            "중복 이름표·헤더와 다른 칸 수·빈 기본 로케일 칸:\n  " +
            string.Join("\n  ", _catalog.Problems));
    }

    [Test]
    public void 표에_기본_로케일과_영어_열이_있다()
    {
        CollectionAssert.Contains(_catalog.Locales.ToList(), StringCatalog.DefaultLocale);
        CollectionAssert.Contains(_catalog.Locales.ToList(), "en");
    }

    [Test]
    public void 이름표는_정해진_모양을_지킨다()
    {
        // Category는 나중에 Unity Localization의 테이블 이름이 되므로 파일 이름으로
        // 쓸 수 있어야 한다. Key는 데이터 에셋 자동 키(Item/mushroom_wood.name)와
        // 같은 모양으로 맞춰 둔다 — 다음 라운드가 규칙을 새로 정하지 않아도 되게.
        var category = new Regex(@"^[A-Z][A-Za-z0-9]*$");
        var key = new Regex(@"^[a-z0-9]+([._][a-z0-9]+)*$");

        foreach (var k in _catalog.Keys)
        {
            Assert.IsTrue(category.IsMatch(k.Category), $"Category가 규약을 벗어났다: {k}");
            Assert.IsTrue(key.IsMatch(k.Key), $"Key가 규약을 벗어났다: {k}");
        }
    }

    [Test]
    public void 표의_값에_글꼴에_없는_글자가_없다()
    {
        // 본문 글꼴 ChosunGu에 없어 화면에서 두부(□)로 뜨는 것들. 실측으로 확인했다.
        const string 금지 = "—«»‹›⟦⟧ÀÉÜŽĐ−";

        foreach (var locale in _catalog.Locales)
        {
            var table = _catalog.TableFor(locale);
            foreach (var pair in table)
                foreach (char c in 금지)
                    Assert.IsFalse(pair.Value.Contains(c),
                        $"{locale} {pair.Key}에 글꼴에 없는 '{c}'가 섞였다 — \"{pair.Value}\"");
        }
    }

    [Test]
    public void 증명용으로_옮긴_문구가_실제로_표에서_나온다()
    {
        // 옮겼다고 말만 하고 값이 안 바뀌는 일이 없게, 표의 값과 화면 코드의 결과를 맞춘다.
        Assert.AreEqual(_catalog.TableFor("ko")[new LocKey("UI", "craft_empty")],
                        Survive.UI.MenuListing.NothingKnownToCraft);
        Assert.AreEqual(_catalog.TableFor("ko")[new LocKey("UI", "build_empty")],
                        Survive.UI.MenuListing.NothingKnownToBuild);
    }

    // ── ③ 긁는 장치 자체 ────────────────────────────────────

    [Test]
    public void 리터럴_두_개로_부른_자리를_긁는다()
    {
        var r = LocSourceScanner.Scan("var s = Loc.T(\"UI\", \"craft_empty\");");
        Assert.AreEqual(1, r.Literals.Count);
        Assert.AreEqual("UI", r.Literals[0].Category);
        Assert.AreEqual("craft_empty", r.Literals[0].Key);
        Assert.AreEqual(0, r.DynamicLines.Count);
    }

    [Test]
    public void 서식_호출도_같이_긁는다()
    {
        var r = LocSourceScanner.Scan("Loc.F(\"UI\", \"battery\", value)");
        Assert.AreEqual(1, r.Literals.Count);
        Assert.AreEqual("battery", r.Literals[0].Key);
    }

    [Test]
    public void 주석_안의_호출은_세지_않는다()
    {
        var r = LocSourceScanner.Scan(
            "/// <code>Loc.T(\"UI\", \"예시\")</code>\n" +
            "// Loc.T(\"UI\", \"옛날키\")\n" +
            "/* Loc.T(\"UI\", \"묵은키\") */\n" +
            "var s = Loc.T(\"UI\", \"진짜키\");");

        Assert.AreEqual(1, r.Literals.Count, "문서 주석의 예시가 진짜 호출로 세어졌다");
        Assert.AreEqual("진짜키", r.Literals[0].Key);
        Assert.AreEqual(4, r.Literals[0].Line, "주석을 지우면서 줄 번호가 밀렸다");
    }

    [Test]
    public void 변수로_만든_키는_검사_불가로_센다()
    {
        var r = LocSourceScanner.Scan("Loc.T(\"Item\", id + \".name\")\nLoc.T(category, key)");
        Assert.AreEqual(0, r.Literals.Count);
        Assert.AreEqual(2, r.DynamicLines.Count, "검사할 수 없는 자리는 세어 두어야 사각지대가 보인다");
    }

    [Test]
    public void 문자열_안의_슬래시는_주석이_아니다()
    {
        var r = LocSourceScanner.Scan("var url = \"http://x\"; Loc.T(\"UI\", \"뒤에오는키\");");
        Assert.AreEqual(1, r.Literals.Count);
        Assert.AreEqual("뒤에오는키", r.Literals[0].Key);
    }

    [Test]
    public void 줄바꿈이_섞인_호출도_긁는다()
    {
        var r = LocSourceScanner.Scan("Loc.T(\n    \"UI\",\n    \"여러줄키\");");
        Assert.AreEqual(1, r.Literals.Count);
        Assert.AreEqual("여러줄키", r.Literals[0].Key);
    }
}
