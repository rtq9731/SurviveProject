using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Survive.Localization;

/// <summary>
/// 표를 읽는 부분. 여기서 지키는 것은 하나다 — <b>한국어 문장에 쉼표는 흔하다.</b>
///
/// 번역가가 "재료가 없다, 아직은"이라고 적어 보낸 줄을 파서가 두 칸으로 쪼개면
/// 그 자리는 화면에서 반 토막이 나고, 그 고장은 그 화면을 열어 보기 전까지
/// 아무 신호도 내지 않는다. 그래서 쉼표·따옴표·줄바꿈·BOM·CRLF를 못 박아 둔다.
/// </summary>
public class LocalizationCsvTests
{
    // ── 값 안의 특수 문자 ────────────────────────────────────

    [Test]
    public void 따옴표로_감싼_값_안의_쉼표는_칸을_나누지_않는다()
    {
        var rows = Csv.Parse("Category,Key,ko\nUI,greet,\"재료가 없다, 아직은\"");
        Assert.AreEqual(3, rows[1].Length, "쉼표에서 칸이 쪼개졌다");
        Assert.AreEqual("재료가 없다, 아직은", rows[1][2]);
    }

    [Test]
    public void 겹쳐_쓴_따옴표는_따옴표_한_개다()
    {
        var rows = Csv.Parse("Category,Key,ko\nUI,quote,\"그는 \"\"안 된다\"\"고 했다\"");
        Assert.AreEqual("그는 \"안 된다\"고 했다", rows[1][2]);
    }

    [Test]
    public void 따옴표_안의_줄바꿈은_한_칸_안에_남는다()
    {
        var rows = Csv.Parse("Category,Key,ko\nUI,two_lines,\"첫 줄\n둘째 줄\"\nUI,after,뒤");
        Assert.AreEqual(3, rows.Count, "줄바꿈이 든 값 때문에 레코드가 갈라졌다");
        Assert.AreEqual("첫 줄\n둘째 줄", rows[1][2]);
        Assert.AreEqual("뒤", rows[2][2]);
    }

    [Test]
    public void 따옴표로_감싸면_앞뒤_공백이_살아_있다()
    {
        var rows = Csv.Parse("Category,Key,ko\nUI,pad,\"  가운데  \"");
        Assert.AreEqual("  가운데  ", rows[1][2], "배치용 공백이 값에 들어가는 경우가 있다");
    }

    [Test]
    public void 값_중간의_따옴표는_그냥_글자다()
    {
        var rows = Csv.Parse("Category,Key,ko\nUI,inch,5\" 배관");
        Assert.AreEqual("5\" 배관", rows[1][2]);
    }

    // ── 줄과 파일의 생김새 ──────────────────────────────────

    [Test]
    public void CRLF와_LF가_섞여도_같은_표를_읽는다()
    {
        var lf   = Csv.Parse("Category,Key,ko\nUI,a,가\nUI,b,나");
        var crlf = Csv.Parse("Category,Key,ko\r\nUI,a,가\r\nUI,b,나");
        var mixed = Csv.Parse("Category,Key,ko\r\nUI,a,가\nUI,b,나\r\n");

        Assert.AreEqual(3, lf.Count);
        Assert.AreEqual(3, crlf.Count);
        Assert.AreEqual(3, mixed.Count);
        Assert.AreEqual("가", crlf[1][2]);
        Assert.AreEqual("나", mixed[2][2], "CR이 값 끝에 눌어붙었다");
    }

    [Test]
    public void 앞머리_BOM은_첫_칸_이름을_더럽히지_않는다()
    {
        var rows = Csv.Parse(Csv.ByteOrderMark + "Category,Key,ko\nUI,a,가");
        Assert.AreEqual("Category", rows[0][0], "엑셀이 저장하면 BOM이 붙는다");
    }

    [Test]
    public void 마지막_줄에_줄바꿈이_없어도_읽는다()
    {
        var withNewline = Csv.Parse("Category,Key,ko\nUI,a,가\n");
        var without     = Csv.Parse("Category,Key,ko\nUI,a,가");
        Assert.AreEqual(2, withNewline.Count, "끝의 줄바꿈이 빈 레코드를 만들면 안 된다");
        Assert.AreEqual(2, without.Count);
    }

    [Test]
    public void 빈_줄은_레코드로_세지_않는다()
    {
        var rows = Csv.Parse("Category,Key,ko\n\nUI,a,가\n\n\nUI,b,나\n");
        Assert.AreEqual(3, rows.Count);
    }

    [Test]
    public void 우물_정으로_시작하는_줄은_주석이다()
    {
        var rows = Csv.Parse("# 이 표는 진실의 원천이다\nCategory,Key,ko\n# 여기부터 UI\nUI,a,가");
        Assert.AreEqual(2, rows.Count);
        Assert.AreEqual("Category", rows[0][0]);
        Assert.AreEqual("a", rows[1][1]);
    }

    [Test]
    public void 빈_칸은_빈_문자열로_읽힌다()
    {
        var rows = Csv.Parse("Category,Key,ko,en\nUI,a,가,\nUI,b,,나");
        Assert.AreEqual(4, rows[1].Length);
        Assert.AreEqual("", rows[1][3], "en이 비었다");
        Assert.AreEqual("", rows[2][2]);
    }

    [Test]
    public void 빈_글줄은_빈_표다()
    {
        Assert.AreEqual(0, Csv.Parse("").Count);
        Assert.AreEqual(0, Csv.Parse(null).Count);
        Assert.AreEqual(0, Csv.Parse("\n\n").Count);
    }

    [Test]
    public void 레코드가_시작한_줄_번호를_돌려준다()
    {
        var lines = new List<int>();
        Csv.Parse("# 주석\n\nCategory,Key,ko\nUI,a,\"두\n줄\"\nUI,b,나", lines);

        // 주석과 빈 줄을 건너뛰므로 레코드 번호와 줄 번호가 어긋난다.
        Assert.AreEqual(new[] { 3, 4, 6 }, lines.ToArray(),
            "오류 보고에 레코드 번호를 적으면 사람이 파일에서 그 줄을 못 찾는다");
    }

    [Test]
    public void 감싼_값은_다시_읽으면_원래_값이다()
    {
        foreach (var value in new[] { "쉼표, 있음", "따옴표 \"있음\"", "줄\n바꿈", "  공백  ", "평범" })
        {
            var rows = Csv.Parse("Category,Key,ko\nUI,k," + Csv.Escape(value));
            Assert.AreEqual(value, rows[1][2], $"왕복에서 값이 바뀌었다: {value}");
        }
    }

    // ── 표 세우기 ────────────────────────────────────────────

    [Test]
    public void 겹치는_이름표는_문제로_적힌다()
    {
        var c = StringCatalog.Parse("Category,Key,ko\nUI,a,가\nUI,a,다시");
        Assert.IsTrue(c.Problems.Any(p => p.Contains("겹친다")), string.Join(" / ", c.Problems));
        Assert.AreEqual(1, c.Keys.Count, "먼저 나온 것을 남긴다");
        Assert.AreEqual("가", c.TableFor("ko")[new LocKey("UI", "a")]);
    }

    [Test]
    public void 헤더와_칸_수가_다른_줄은_문제로_적힌다()
    {
        var c = StringCatalog.Parse("Category,Key,ko,en\nUI,a,가,A,군더더기");
        Assert.IsTrue(c.Problems.Any(p => p.Contains("헤더는")), string.Join(" / ", c.Problems));
    }

    [Test]
    public void 기본_로케일_칸이_비면_문제로_적힌다()
    {
        var c = StringCatalog.Parse("Category,Key,ko,en\nUI,a,,A only");
        Assert.IsTrue(c.Problems.Any(p => p.Contains("기본 로케일")), string.Join(" / ", c.Problems));
    }

    [Test]
    public void 헤더_첫_두_칸의_이름을_확인한다()
    {
        var c = StringCatalog.Parse("Cat,Name,ko\nUI,a,가");
        Assert.AreEqual(2, c.Problems.Count(p => p.Contains("헤더의")), string.Join(" / ", c.Problems));
    }

    [Test]
    public void 의사_번역은_표에_열로_둘_수_없다()
    {
        var c = StringCatalog.Parse("Category,Key,ko,pseudo\nUI,a,가,[!! 가~ !!]");
        Assert.IsTrue(c.Problems.Any(p => p.Contains("pseudo")), string.Join(" / ", c.Problems));
        Assert.IsNull(c.TableFor("pseudo"), "만들어 내는 로케일에 표가 생기면 진실이 둘이 된다");
    }

    [Test]
    public void 빈_칸은_표에_담지_않는다()
    {
        var c = StringCatalog.Parse("Category,Key,ko,en\nUI,a,가,");
        Assert.IsFalse(c.TableFor("en").ContainsKey(new LocKey("UI", "a")),
            "빈 칸을 담으면 폴백이 돌 자리가 사라지고 화면이 빈다");
    }

    [Test]
    public void 이름표의_앞뒤_공백은_떼어_낸다()
    {
        var c = StringCatalog.Parse("Category,Key,ko\n  UI  ,  a  ,가");
        Assert.IsTrue(c.Contains(new LocKey("UI", "a")));
    }

    [Test]
    public void 망가진_표에도_예외를_내지_않는다()
    {
        Assert.DoesNotThrow(() => StringCatalog.Parse(""));
        Assert.DoesNotThrow(() => StringCatalog.Parse(null));
        Assert.DoesNotThrow(() => StringCatalog.Parse("쓰레기"));
        Assert.DoesNotThrow(() => StringCatalog.Parse("Category,Key\nUI,a"));
    }
}
