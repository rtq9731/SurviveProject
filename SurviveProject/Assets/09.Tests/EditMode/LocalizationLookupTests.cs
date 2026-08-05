using System.Linq;
using NUnit.Framework;
using Survive.Localization;

/// <summary>
/// 꺼내 쓰는 부분 — 폴백 사슬, 로케일 전환, 서식, 의사 번역.
///
/// 여기서 제일 중요한 단언은 "<b>절대 빈 문자열이 나오지 않는다</b>"이다.
/// 번역이 빠진 자리가 화면에서 사라지면 무엇이 빠졌는지조차 보이지 않는다.
///
/// <see cref="Loc"/>은 전역이라 표를 갈아 끼우는 시험은 끝나고 반드시 되돌린다.
/// 안 그러면 이 파일 다음에 도는 화면 문자열 검사들이 엉뚱한 표를 본다.
/// </summary>
public class LocalizationLookupTests
{
    const string Table =
        "Category,Key,ko,en\n" +
        "UI,both,한국어,English\n" +
        "UI,ko_only,한국어만,\n" +
        "UI,fmt,남은 {0}초,{0}s left\n" +
        "Item,mushroom_wood.name,버섯 목재,\n";

    StringCatalog _saved;
    string _savedLocale;

    [SetUp]
    public void SetUp()
    {
        _saved = Loc.Catalog;
        _savedLocale = Loc.CurrentLocale;
        Loc.Load(StringCatalog.Parse(Table));
        Loc.SetLocale(StringCatalog.DefaultLocale);
    }

    [TearDown]
    public void TearDown()
    {
        Loc.Load(_saved);
        Loc.SetLocale(_savedLocale);
    }

    // ── 폴백 사슬 세 단 ──────────────────────────────────────

    [Test]
    public void 첫째_단은_지금_로케일이다()
    {
        Loc.SetLocale("en");
        Assert.AreEqual("English", Loc.T("UI", "both"));
    }

    [Test]
    public void 둘째_단은_기본_로케일이다()
    {
        Loc.SetLocale("en");
        Assert.AreEqual("한국어만", Loc.T("UI", "ko_only"),
            "en 칸이 비었으면 ko로 흐른다 — 화면이 비면 안 된다");
    }

    [Test]
    public void 셋째_단은_키_자체다()
    {
        Assert.AreEqual("없는키", Loc.T("UI", "없는키"));
        Loc.SetLocale("en");
        Assert.AreEqual("없는키", Loc.T("UI", "없는키"));
    }

    [Test]
    public void 어떤_경우에도_빈_문자열이_나오지_않는다()
    {
        foreach (var locale in new[] { "ko", "en", "de", StringCatalog.PseudoLocale })
        {
            Loc.SetLocale(locale);
            foreach (var key in new[] { "both", "ko_only", "없는키", "" })
                Assert.IsNotEmpty(Loc.T("UI", key), $"{locale}/{key}가 빈 문자열로 나왔다");
        }
    }

    [Test]
    public void 표에_없는_로케일은_통째로_기본_로케일로_흐른다()
    {
        Loc.SetLocale("de");
        Assert.AreEqual("한국어", Loc.T("UI", "both"));
        Assert.AreEqual("한국어만", Loc.T("UI", "ko_only"));
    }

    [Test]
    public void 표를_못_읽었어도_예외가_아니라_키가_나온다()
    {
        Loc.Load(StringCatalog.Empty);
        Assert.AreEqual("craft_empty", Loc.T("UI", "craft_empty"));
        Assert.IsFalse(Loc.IsLoaded);
    }

    [Test]
    public void 빈_이름표로_불러도_물음표를_낸다()
    {
        Assert.AreEqual("?", Loc.T("", ""));
        Assert.AreEqual("?", Loc.T(null, null));
    }

    // ── 로케일 전환 ──────────────────────────────────────────

    [Test]
    public void 로케일을_바꾸면_알림이_한_번_간다()
    {
        int calls = 0;
        void Handler() => calls++;
        Loc.LocaleChanged += Handler;
        try
        {
            Loc.SetLocale("en");
            Assert.AreEqual(1, calls);

            Loc.SetLocale("en");
            Assert.AreEqual(1, calls, "같은 로케일로 다시 바꾸면 알리지 않는다");

            Loc.SetLocale("ko");
            Assert.AreEqual(2, calls);
        }
        finally { Loc.LocaleChanged -= Handler; }
    }

    [Test]
    public void 표를_갈아_끼워도_알림이_간다()
    {
        int calls = 0;
        void Handler() => calls++;
        Loc.LocaleChanged += Handler;
        try
        {
            Loc.Load(StringCatalog.Parse(Table));
            Assert.AreEqual(1, calls, "표가 늦게 도착해도 이미 그려진 화면이 따라와야 한다");
        }
        finally { Loc.LocaleChanged -= Handler; }
    }

    [Test]
    public void 로케일_이름의_앞뒤_공백과_대소문자는_같은_것으로_본다()
    {
        Loc.SetLocale("  EN  ");
        Assert.AreEqual("English", Loc.T("UI", "both"));
        Assert.AreEqual("EN", Loc.CurrentLocale);
    }

    [Test]
    public void 빈_로케일을_주면_기본_로케일로_돌아간다()
    {
        Loc.SetLocale("en");
        Loc.SetLocale("");
        Assert.AreEqual(StringCatalog.DefaultLocale, Loc.CurrentLocale);
    }

    [Test]
    public void 고를_수_있는_로케일에_의사_번역이_들어_있다()
    {
        CollectionAssert.Contains(Loc.AvailableLocales.ToList(), StringCatalog.PseudoLocale);
        CollectionAssert.Contains(Loc.AvailableLocales.ToList(), "ko");
        CollectionAssert.Contains(Loc.AvailableLocales.ToList(), "en");
    }

    // ── 서식 ─────────────────────────────────────────────────

    [Test]
    public void 서식_인자가_끼워진다()
    {
        Assert.AreEqual("남은 12초", Loc.F("UI", "fmt", 12));
        Loc.SetLocale("en");
        Assert.AreEqual("12s left", Loc.F("UI", "fmt", 12));
    }

    [Test]
    public void 서식이_틀려도_번역문을_그대로_낸다()
    {
        // 번역가가 {0}을 {}로 잘못 옮기는 일은 실제로 일어난다.
        Loc.Load(StringCatalog.Parse("Category,Key,ko\nUI,bad,남은 {0 초\n"));
        Assert.AreEqual("남은 {0 초", Loc.F("UI", "bad", 12),
            "예외가 올라오면 그 프레임의 UI가 통째로 죽는다");
    }

    [Test]
    public void 없는_키에_서식을_걸어도_키가_나온다()
    {
        Assert.AreEqual("없는키", Loc.F("UI", "없는키", 12));
    }

    // ── 의사 번역 ────────────────────────────────────────────

    [Test]
    public void 의사_번역은_눈에_띄게_감싼다()
    {
        Loc.SetLocale(StringCatalog.PseudoLocale);
        string s = Loc.T("Item", "mushroom_wood.name");

        StringAssert.StartsWith(PseudoLocalizer.Prefix, s);
        StringAssert.EndsWith(PseudoLocalizer.Suffix, s);
        StringAssert.Contains("버섯 목재", s, "원문이 안 보이면 무엇이 바뀐 자리인지 알 수 없다");
    }

    [Test]
    public void 의사_번역은_길이를_삼십오_퍼센트_이상_늘린다()
    {
        foreach (var source in new[] { "가", "버섯 목재", "아직 아는 제작법이 없다", "짧" })
        {
            string s = PseudoLocalizer.Transform(source);
            Assert.GreaterOrEqual(s.Length, (int)(source.Length * 1.35f),
                $"\"{source}\"가 충분히 부풀지 않았다 — 긴 언어에서 판이 깨지는지 못 본다");
        }
    }

    [Test]
    public void 의사_번역은_빈_값을_건드리지_않는다()
    {
        Assert.AreEqual("", PseudoLocalizer.Transform(""));
        Assert.IsNull(PseudoLocalizer.Transform(null));
    }

    [Test]
    public void 의사_번역은_두_번_감싸지_않는다()
    {
        string once = PseudoLocalizer.Transform("버섯 목재");
        Assert.AreEqual(once, PseudoLocalizer.Transform(once));
    }

    [Test]
    public void 의사_번역은_글꼴에_있는_글자만_쓴다()
    {
        // ChosunGu SDF는 Dynamic 아틀라스라 원본 TTF에 있는 글자만 찍힌다.
        // 실측 결과 악센트 라틴(À É Ü Ž Đ)과 겹화살괄호(« » ‹ › ⟦ ⟧)는 없다.
        string affixes = PseudoLocalizer.Prefix + PseudoLocalizer.Suffix + PseudoLocalizer.Padding;
        foreach (char c in affixes)
            Assert.Less((int)c, 128, $"U+{(int)c:X4} '{c}'는 ASCII가 아니다 — 두부(□)로 뜰 수 있다");
    }

    [Test]
    public void 의사_번역에도_서식_자리가_남는다()
    {
        Loc.SetLocale(StringCatalog.PseudoLocale);
        string s = Loc.F("UI", "fmt", 12);
        StringAssert.Contains("12", s, "감싸는 과정에서 {0}이 깨지면 숫자가 화면에서 사라진다");
        StringAssert.StartsWith(PseudoLocalizer.Prefix, s);
    }

    [Test]
    public void 의사_번역은_기본_로케일을_바탕으로_만든다()
    {
        // en 칸이 차 있는 줄이라도 의사 번역은 ko를 부풀린다 — 무엇이 원문인지
        // 한 가지로 정해 두지 않으면 "부풀지 않은 글자 = 하드코딩"이 성립하지 않는다.
        Loc.SetLocale(StringCatalog.PseudoLocale);
        StringAssert.Contains("한국어", Loc.T("UI", "both"));
    }

    [Test]
    public void 의사_번역에서도_없는_키는_키_그대로다()
    {
        Loc.SetLocale(StringCatalog.PseudoLocale);
        Assert.AreEqual("없는키", Loc.T("UI", "없는키"),
            "표 밖의 키까지 부풀리면 '부풀지 않은 것이 하드코딩'이라는 신호가 흐려진다");
    }

    // ── 이름표 ───────────────────────────────────────────────

    [Test]
    public void 이름표는_앞뒤_공백을_떼고_비교한다()
    {
        Assert.AreEqual(new LocKey("UI", "both"), new LocKey(" UI ", " both "));
        Assert.AreEqual("한국어", Loc.T(" UI ", " both "));
    }

    [Test]
    public void 이름표는_대소문자를_구별한다()
    {
        Assert.AreNotEqual(new LocKey("UI", "both"), new LocKey("ui", "Both"));
        Assert.AreEqual("Both", Loc.T("ui", "Both"), "다른 키이므로 폴백이 키를 낸다");
    }

    [Test]
    public void 이름표는_사전_열쇠로_박싱_없이_쓰인다()
    {
        var dict = new System.Collections.Generic.Dictionary<LocKey, string>
        {
            [new LocKey("UI", "a")] = "가"
        };
        Assert.IsTrue(dict.TryGetValue(new LocKey("UI", "a"), out var v));
        Assert.AreEqual("가", v);
        Assert.IsFalse(dict.ContainsKey(new LocKey("Item", "a")),
            "Category가 다르면 다른 줄이다");
    }
}
