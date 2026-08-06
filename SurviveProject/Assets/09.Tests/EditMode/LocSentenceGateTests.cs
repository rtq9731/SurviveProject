using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Survive.Localization;

/// <summary>
/// <b>문장을 조각으로 짓지 못하게 막는 게이트. 이 파일이 이 라운드의 핵심 산출물이다.</b>
///
/// 규칙 한 줄: 한 줄에 보이는 문장은 표의 한 칸에 통째로 들어가고, 그 안의
/// <c>{0} {1} {2}</c>가 값이 들어갈 자리를 표시한다. <b>인자로 넘길 수 있는 것은 값뿐</b>이고
/// 코드에 적은 문자열 리터럴("개", ", ")은 말이라 표 안에 있어야 한다.
/// 조각을 이어 붙이면 번역가가 어순을 바꿀 수 없다 — 그것이 전부다.
///
/// 여기서 <b>실패</b>하는 것.
/// <list type="number">
/// <item><c>Loc.F</c>의 값 자리에 문자열 리터럴이 있다</item>
/// <item>화면에 나가는 범위의 코드에 한글 리터럴이 남아 있다 (범위는 아래 목록으로 관리한다)</item>
/// <item><c>Loc</c>이 낸 글을 한글 조각과 <c>+</c>나 <c>Append</c>로 이었다</item>
/// <item>표의 <c>{n}</c>과 호출부 인자 개수가 어긋난다 (<b>로케일마다</b> 본다)</item>
/// <item><c>{n}</c> 번호가 0부터 이어지지 않는다</item>
/// <item>자리표가 있는데 번역가에게 줄 설명(Comment 칸)이 없다</item>
/// </list>
///
/// <b>사각지대를 세어 남긴다.</b> 변수로 만든 이름표, 배열로 넘긴 인자, 범위 밖 파일.
/// 무엇을 검사할 수 없는지 아는 게이트가 모르는 게이트보다 낫다.
/// </summary>
public class LocSentenceGateTests
{
    static string ScriptRoot => Path.Combine(Application.dataPath, "02.Scripts");

    /// <summary>
    /// "화면에 나가는 코드"로 지정한 범위. <b>여기에 경로를 더할 때마다 게이트가 강해진다.</b>
    /// 다음 라운드는 <c>Prompt</c>·<c>Codex</c>·HUD가 있는 곳을 더하게 된다.
    /// </summary>
    static readonly string[] ScreenScopes =
    {
        "Assets/02.Scripts/Domain/UI/",
        "Assets/02.Scripts/UI/",

        // 밝기 조절 화면(GammaCalibrationScreen)이 여기 있다. 첫 실행에 실제로 뜨는
        // 화면이라 번역 대상이다.
        //
        // <b>Domain/Art/는 일부러 넣지 않았다.</b> 그쪽의 한글은 아트 규칙 위반을
        // 적는 진단문("광원 4색 밖 라이트: ...")이고 콘솔로만 나간다. 화면이 아니므로
        // 범위에 넣을 것이 아니다. NotScreenCode에 넣지도 않았다 — 그 목록은 규칙
        // ①③(Loc 인자·조각 이어 붙이기)의 면제이고, Domain/Art/는 Loc을 아예
        // 부르지 않으므로 넣어 봐야 검사만 약해진다.
        "Assets/02.Scripts/Art/",
    };

    /// <summary>
    /// 아직 표로 못 옮긴 파일. <b>이 목록이 곧 다음 라운드의 할 일 목록이다.</b>
    ///
    /// 여기 이름을 더하는 것은 규칙을 어기는 것이 아니라 <b>빚을 적어 두는 것</b>이다.
    /// 대신 아래 <c>못_옮긴_파일_목록에_이유_없이_이름이_남아_있지_않다</c>가
    /// 이미 옮긴 파일이 목록에 남아 있는 것을 막는다 — 목록은 줄기만 해야 한다.
    /// </summary>
    /// <summary>
    /// 규칙을 적용하지 않는 곳.
    ///
    /// <b>검증 하네스는 화면이 아니다.</b> E2E는 <c>Loc.T(...)를 부른 결과가 맞는가</c>를
    /// 검사하면서 그 옆에 한국어로 <b>검사 이름</b>을 적는다 — 콘솔에 나가는 글이고,
    /// 번역할 것이 아니다. 여기까지 규칙을 밀면 하네스가 자기가 무엇을 검사하는지
    /// 말하지 못하게 되고, 그러면 게이트를 끄는 쪽이 편해진다.
    /// </summary>
    static readonly string[] NotScreenCode =
    {
        "Assets/02.Scripts/Testing/",
    };

    // 비었다. 화면 범위(위 ScreenScopes) 안에 아직 표로 못 옮긴 파일이 하나도 없다.
    // 범위를 넓히면 여기에 다시 이름이 생길 수 있다 — 그때도 목록은 줄기만 해야 한다.
    static readonly string[] NotYetMoved = { };

    static readonly List<(string Path, string Source)> Files = new List<(string, string)>();
    static readonly List<LocCall> Calls = new List<LocCall>();
    static StringCatalog _catalog;
    static int _dynamicNames;
    static int _unknownArgCounts;

    [OneTimeSetUp]
    public void 코드와_표를_한_번씩_읽는다()
    {
        _catalog = LocalizationTestBootstrap.LoadCatalogFromDisk();

        Files.Clear();
        Calls.Clear();
        _dynamicNames = 0;
        _unknownArgCounts = 0;

        foreach (var full in Directory.EnumerateFiles(ScriptRoot, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Relative(full);
            string source = File.ReadAllText(full);
            Files.Add((relative, source));

            foreach (var call in LocSentenceRules.FindCalls(source))
            {
                Calls.Add(call);
                if (!call.NameIsLiteral) _dynamicNames++;
                else if (!call.ArgCountKnown) _unknownArgCounts++;
            }
        }
    }

    static string Relative(string full) =>
        "Assets" + full.Substring(Application.dataPath.Length).Replace('\\', '/');

    static bool InScope(string path) => ScreenScopes.Any(path.StartsWith);

    static bool IsScreenCode(string path) => !NotScreenCode.Any(path.StartsWith);

    // ── ① 값 자리에 말을 넘기지 않는다 ───────────────────────

    [Test]
    public void Loc_F의_값_자리에_문자열_리터럴이_없다()
    {
        var broken = Findings(LocRule.LiteralArgument);

        Assert.IsEmpty(broken,
            "말을 인자로 넘겼다. \"개\"·\", \"·\"  \" 같은 것은 값이 아니라 말이고, " +
            "말은 표 안에 있어야 번역가가 어순을 바꿀 수 있다:\n  " +
            string.Join("\n  ", broken));
    }

    // ── ② 화면 코드에 한글 리터럴 ────────────────────────────

    [Test]
    public void 화면에_나가는_코드에_한글_리터럴이_남아_있지_않다()
    {
        var broken = new List<string>();

        foreach (var (path, source) in Files)
        {
            if (!InScope(path) || NotYetMoved.Contains(path)) continue;
            foreach (var f in LocSentenceRules.Inspect(source, banKoreanLiterals: true))
                if (f.Rule == LocRule.KoreanLiteral) broken.Add($"{path}:{f.Line} {f.Detail}");
        }

        Assert.IsEmpty(broken,
            "화면 코드에 한글이 박혀 있다. 표로 옮기거나, 못 옮겼으면 NotYetMoved에 " +
            "적어 다음 라운드의 할 일로 남겨라:\n  " + string.Join("\n  ", broken));
    }

    [Test]
    public void 못_옮긴_파일_목록에_이유_없이_이름이_남아_있지_않다()
    {
        var stale = new List<string>();

        foreach (string path in NotYetMoved)
        {
            var file = Files.FirstOrDefault(f => f.Path == path);
            if (file.Source == null) { stale.Add($"{path} (파일이 없다)"); continue; }

            bool stillHasKorean = LocSentenceRules
                .Inspect(file.Source, banKoreanLiterals: true)
                .Any(f => f.Rule == LocRule.KoreanLiteral);

            if (!stillHasKorean) stale.Add($"{path} (이미 다 옮겼다)");
        }

        Assert.IsEmpty(stale,
            "빚 목록은 줄기만 해야 한다. 다 옮긴 파일은 NotYetMoved에서 지워라:\n  " +
            string.Join("\n  ", stale));
    }

    // ── ③ 조각 이어 붙이기 ───────────────────────────────────

    [Test]
    public void Loc이_낸_글에_한글_조각을_이어_붙이지_않는다()
    {
        var broken = Findings(LocRule.Concatenation);

        Assert.IsEmpty(broken,
            "번역문 옆에 한글을 이어 붙였다. 이러면 그 조각의 자리를 번역가가 못 옮긴다:\n  " +
            string.Join("\n  ", broken));
    }

    static List<string> Findings(LocRule rule)
    {
        var broken = new List<string>();
        foreach (var (path, source) in Files)
        {
            if (!IsScreenCode(path)) continue;
            foreach (var f in LocSentenceRules.Inspect(source, banKoreanLiterals: false))
                if (f.Rule == rule) broken.Add($"{path}:{f.Line} {f.Detail}");
        }
        return broken;
    }

    // ── ④⑤⑥ 표와 호출부 ─────────────────────────────────────

    [Test]
    public void 표의_자리표_번호는_0부터_이어진다()
    {
        var broken = 끊긴_자리표(_catalog);
        Assert.IsEmpty(broken,
            "번호가 빠지면 그 인자는 화면에 닿지 못한다:\n  " + string.Join("\n  ", broken));
    }

    [Test]
    public void 모든_로케일이_같은_자리표를_쓴다()
    {
        var broken = 로케일마다_다른_자리표(_catalog);
        Assert.IsEmpty(broken,
            "ko에는 {2}가 있는데 en에는 없으면 그 언어에서 정보가 통째로 사라진다:\n  " +
            string.Join("\n  ", broken));
    }

    [Test]
    public void 호출부의_인자_개수가_표의_자리표와_맞는다()
    {
        var broken = 어긋난_인자_개수(_catalog, Calls);
        Assert.IsEmpty(broken,
            "코드가 주는 값의 수와 표가 요구하는 자리 수가 다르다:\n  " +
            string.Join("\n  ", broken));
    }

    [Test]
    public void 자리표가_있는_줄에는_번역가에게_줄_설명이_있다()
    {
        var broken = 설명_없는_자리표(_catalog);
        Assert.IsEmpty(broken,
            "{0}만 보고는 무엇이 들어가는지 알 수 없어 번역가가 어순을 못 바꾼다 — " +
            "어순을 바꾸라고 만든 규칙인데 못 바꾸면 헛일이다. Comment 칸에 " +
            "\"{0}=... {1}=...\" 꼴로 적어라:\n  " + string.Join("\n  ", broken));
    }

    // ── 사각지대 ─────────────────────────────────────────────

    [Test]
    public void 검사할_수_없는_자리를_세어_남긴다()
    {
        int outOfScope = Files.Count(f => !InScope(f.Path));

        Debug.Log($"[문장 게이트] 훑은 파일 {Files.Count}개, Loc 호출 {Calls.Count}개.\n" +
                  $"  검사 불가: 변수로 만든 이름표 {_dynamicNames}곳, " +
                  $"인자 개수를 셀 수 없는 자리 {_unknownArgCounts}곳.\n" +
                  $"  한글 리터럴 검사 범위 밖 파일 {outOfScope}개 " +
                  $"(범위: {string.Join(", ", ScreenScopes)}).\n" +
                  $"  화면 코드로 세지 않는 곳: {string.Join(", ", NotScreenCode)}\n" +
                  $"  범위 안이지만 아직 못 옮긴 파일 {NotYetMoved.Length}개:\n    " +
                  string.Join("\n    ", NotYetMoved));

        Assert.Greater(Calls.Count, 0, "Loc 호출을 하나도 못 긁었다 — 게이트가 공회전한다");
        Assert.Pass();
    }

    // ── 검사 자체 (음성 확인) ────────────────────────────────
    //
    // 게이트는 초록불일 때 아무 말도 하지 않는다. 그래서 "정말 잡는가"를
    // 일부러 어긴 입력으로 확인한다. 이것이 없으면 정규식이 조용히 망가진
    // 게이트도 통과한다.

    [Test]
    public void 세는_단위를_인자로_넘기면_잡는다()
    {
        var f = LocSentenceRules.Inspect("var s = Loc.F(\"UI\", \"count\", n, \"개\");", false);
        Assert.AreEqual(1, f.Count(x => x.Rule == LocRule.LiteralArgument),
            "\"개\"는 값이 아니라 말이다");
    }

    [Test]
    public void 구분자를_인자로_넘겨도_잡는다()
    {
        // 한글이 아니어도 말이다. 중국어·일본어는 여기에 、를 쓴다.
        var f = LocSentenceRules.Inspect("Loc.F(\"UI\", \"list\", a, \", \", b);", false);
        Assert.AreEqual(1, f.Count(x => x.Rule == LocRule.LiteralArgument));
    }

    [Test]
    public void 이름표_두_개는_인자로_세지_않는다()
    {
        var f = LocSentenceRules.Inspect("Loc.F(\"UI\", \"k\", count);", false);
        Assert.IsEmpty(f, "Category와 Key는 리터럴이어야 한다 — 그것까지 잡으면 게이트를 못 쓴다");
    }

    [Test]
    public void 중첩된_Loc_호출의_이름표도_세지_않는다()
    {
        var f = LocSentenceRules.Inspect("Loc.F(\"UI\", \"outer\", Loc.T(\"UI\", \"inner\"));", false);
        Assert.IsEmpty(f);
    }

    [Test]
    public void 번역문에_한글을_더하면_잡는다()
    {
        var f = LocSentenceRules.Inspect("var s = Loc.T(\"UI\", \"k\") + \"개\";", false);
        Assert.AreEqual(1, f.Count(x => x.Rule == LocRule.Concatenation));
    }

    [Test]
    public void 번역문_앞에_한글을_붙여도_잡는다()
    {
        var f = LocSentenceRules.Inspect("sb.Append(\"  재료 \").Append(Loc.T(\"UI\", \"k\"));", false);
        Assert.AreEqual(1, f.Count(x => x.Rule == LocRule.Concatenation));
    }

    [Test]
    public void 화면_코드의_한글_리터럴을_잡는다()
    {
        var f = LocSentenceRules.Inspect("label.text = \"분석 중\";", banKoreanLiterals: true);
        Assert.AreEqual(1, f.Count(x => x.Rule == LocRule.KoreanLiteral));
    }

    [Test]
    public void 주석과_인스펙터_속성과_콘솔_출력은_세지_않는다()
    {
        const string 소스 =
            "// 한글 주석이다\n" +
            "/// <summary>이것도 주석이다</summary>\n" +
            "[Tooltip(\"인스펙터에만 뜨는 글이다\")]\n" +
            "[SerializeField] int x;\n" +
            "void F() { Debug.LogWarning(\"콘솔에만 나가는 글이다\"); }\n";

        Assert.IsEmpty(LocSentenceRules.Inspect(소스, banKoreanLiterals: true),
            "번역 대상이 아닌 자리까지 잡으면 게이트를 끄게 된다");
    }

    [Test]
    public void 규칙대로_쓴_코드는_아무것도_잡히지_않는다()
    {
        const string 소스 =
            "var line = Loc.F(\"UI\", \"recipe_line\", NameOf(r), items, time);\n" +
            "var sep = Loc.T(\"UI\", \"list_separator\");\n" +
            "sb.Append(sep).Append(Loc.F(\"UI\", \"ingredient_entry\", name, held, need));\n";

        Assert.IsEmpty(LocSentenceRules.Inspect(소스, banKoreanLiterals: true));
    }

    [Test]
    public void 인자_개수를_실제로_센다()
    {
        var calls = LocSentenceRules.FindCalls("Loc.F(\"UI\", \"k\", a, b, c);\nLoc.T(\"UI\", \"t\");");

        Assert.AreEqual(2, calls.Count);
        Assert.IsTrue(calls[0].IsFormat);
        Assert.AreEqual(3, calls[0].ArgCount);
        Assert.AreEqual("k", calls[0].Key);
        Assert.IsFalse(calls[1].IsFormat);
        Assert.AreEqual(0, calls[1].ArgCount);
    }

    [Test]
    public void 변수로_만든_이름표는_판정_불가로_둔다()
    {
        var calls = LocSentenceRules.FindCalls("Loc.T(\"Item\", id + \".name\");");
        Assert.AreEqual(1, calls.Count);
        Assert.IsFalse(calls[0].NameIsLiteral, "판정할 수 없는 자리는 세어 두어야 사각지대가 보인다");
    }

    // ── 표 검사의 음성 확인 ──────────────────────────────────

    [Test]
    public void 끊긴_자리표를_잡는다()
    {
        var c = StringCatalog.Parse("Category,Key,ko,en,Comment\nUI,a,{0} {2},{0} {2},{0}=하나 {2}=셋");
        Assert.IsNotEmpty(끊긴_자리표(c), "{1}이 빠졌는데 통과했다");
    }

    [Test]
    public void 로케일마다_자리표가_다르면_잡는다()
    {
        var c = StringCatalog.Parse(
            "Category,Key,ko,en,Comment\nUI,a,{0} {1},{0},{0}=하나 {1}=둘");
        var broken = 로케일마다_다른_자리표(c);
        Assert.IsNotEmpty(broken, "en에서 {1}이 사라졌는데 통과했다");
    }

    [Test]
    public void 인자_개수가_모자라면_잡는다()
    {
        var c = StringCatalog.Parse("Category,Key,ko,en,Comment\nUI,a,{0} {1},{0} {1},{0}=하나 {1}=둘");
        var calls = LocSentenceRules.FindCalls("Loc.F(\"UI\", \"a\", one);");
        Assert.IsNotEmpty(어긋난_인자_개수(c, calls));
    }

    [Test]
    public void 인자_개수가_남아도_잡는다()
    {
        var c = StringCatalog.Parse("Category,Key,ko,en,Comment\nUI,a,{0},{0},{0}=하나");
        var calls = LocSentenceRules.FindCalls("Loc.F(\"UI\", \"a\", one, two);");
        Assert.IsNotEmpty(어긋난_인자_개수(c, calls));
    }

    [Test]
    public void 자리표가_있는_틀을_Loc_T로_부르면_잡는다()
    {
        var c = StringCatalog.Parse("Category,Key,ko,en,Comment\nUI,a,{0}개,{0} items,{0}=수");
        var calls = LocSentenceRules.FindCalls("Loc.T(\"UI\", \"a\");");
        Assert.IsNotEmpty(어긋난_인자_개수(c, calls),
            "자리표가 있는 틀을 값 없이 부르면 화면에 {0}이 그대로 뜬다");
    }

    [Test]
    public void 설명이_없는_자리표를_잡는다()
    {
        var c = StringCatalog.Parse("Category,Key,ko,en,Comment\nUI,a,{0} {1},{0} {1},{0}=하나만 적었다");
        Assert.IsNotEmpty(설명_없는_자리표(c), "{1}의 설명이 없는데 통과했다");
    }

    [Test]
    public void Comment_칸은_로케일이_아니다()
    {
        var c = StringCatalog.Parse("Category,Key,ko,en,Comment\nUI,a,가,A,설명이다");
        CollectionAssert.AreEqual(new[] { "ko", "en" }, c.Locales.ToList());
        Assert.AreEqual("설명이다", c.CommentFor(new LocKey("UI", "a")));
        Assert.IsEmpty(c.Problems, string.Join(" / ", c.Problems));
    }

    [Test]
    public void 뒤쪽_칸이_모자란_줄은_문제로_적지_않는다()
    {
        // 다른 작업자가 옛 꼴(Comment 없이)로 줄을 더해도 값이 어긋나지 않아야 한다.
        var c = StringCatalog.Parse("Category,Key,ko,en,Comment\nItem,mushroom_wood.name,버섯 목재,Mushroom Wood");
        Assert.IsEmpty(c.Problems, string.Join(" / ", c.Problems));
        Assert.AreEqual("버섯 목재", c.TableFor("ko")[new LocKey("Item", "mushroom_wood.name")]);
        Assert.AreEqual("Mushroom Wood", c.TableFor("en")[new LocKey("Item", "mushroom_wood.name")]);
    }

    // ── 검사 알맹이 ──────────────────────────────────────────

    static List<string> 끊긴_자리표(StringCatalog catalog)
    {
        var broken = new List<string>();
        foreach (string locale in catalog.Locales)
        {
            var table = catalog.TableFor(locale);
            if (table == null) continue;

            foreach (var pair in table)
                if (!LocFormat.IsContiguousFromZero(pair.Value, out var missing))
                    broken.Add($"{locale} {pair.Key}: {{{string.Join("}, {", missing)}}}이 빠졌다 " +
                               $"— \"{pair.Value}\"");
        }
        return broken;
    }

    static List<string> 로케일마다_다른_자리표(StringCatalog catalog)
    {
        var broken = new List<string>();
        var basis = catalog.TableFor(StringCatalog.DefaultLocale);
        if (basis == null) return broken;

        foreach (string locale in catalog.Locales)
        {
            if (locale == StringCatalog.DefaultLocale) continue;
            var table = catalog.TableFor(locale);
            if (table == null) continue;

            foreach (var pair in table)
            {
                if (!basis.TryGetValue(pair.Key, out string reference)) continue;

                var expected = Distinct(reference);
                var actual = Distinct(pair.Value);
                if (expected.SequenceEqual(actual)) continue;

                broken.Add($"{pair.Key}: {StringCatalog.DefaultLocale}는 " +
                           $"[{string.Join(",", expected)}], {locale}는 [{string.Join(",", actual)}] " +
                           $"— \"{pair.Value}\"");
            }
        }
        return broken;
    }

    static List<string> 어긋난_인자_개수(StringCatalog catalog, IEnumerable<LocCall> calls)
    {
        var broken = new List<string>();

        foreach (var call in calls)
        {
            if (!call.NameIsLiteral || !call.ArgCountKnown) continue;

            foreach (string locale in catalog.Locales)
            {
                var table = catalog.TableFor(locale);
                if (table == null || !table.TryGetValue(call.AsKey(), out string value)) continue;

                int required = LocFormat.RequiredArgCount(value);
                if (required == call.ArgCount) continue;

                broken.Add($"{call.AsKey()} ({locale}): 표는 값 {required}개를 요구하는데 " +
                           $"코드는 {call.ArgCount}개를 준다 — \"{value}\"");
            }
        }
        return broken;
    }

    static List<string> 설명_없는_자리표(StringCatalog catalog)
    {
        var broken = new List<string>();
        var basis = catalog.TableFor(StringCatalog.DefaultLocale);
        if (basis == null) return broken;

        foreach (var pair in basis)
        {
            var indices = Distinct(pair.Value);
            if (indices.Count == 0) continue;

            string comment = catalog.CommentFor(pair.Key);
            var missing = indices.Where(n => !comment.Contains("{" + n + "}=")).ToList();
            if (missing.Count == 0) continue;

            broken.Add($"{pair.Key}: {{{string.Join("}, {", missing)}}}의 설명이 없다 " +
                       $"— \"{pair.Value}\" / Comment: \"{comment}\"");
        }
        return broken;
    }

    static List<int> Distinct(string value) =>
        LocFormat.Indices(value).Distinct().OrderBy(n => n).ToList();
}
