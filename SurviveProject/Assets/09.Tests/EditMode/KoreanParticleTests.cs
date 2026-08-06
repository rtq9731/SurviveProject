using System.Collections.Generic;
using NUnit.Framework;
using Survive.Localization;

/// <summary>
/// 한국어 조사를 두 꼴 나란히로 내보낸다 — <c>{0}을</c> → <c>스크랩을(를)</c>.
///
/// <b>사용자 결정이다.</b> "보통 이(가) 을(를) 해야지". 받침을 보고 하나를 고르는
/// 것은 <b>정해진 다음 단계</b>이고, 그날 고칠 곳이 한 곳이 되도록
/// <see cref="IParticleResolver"/>가 값을 함께 받는다.
///
/// <b>이 모듈의 진짜 위험은 잘못 펴는 것이다.</b> "{0}이라고 부른다"가
/// "이(가)라고"가 되면 참사다. 그래서 손대면 안 되는 경우를 넉넉히 못 박는다.
/// </summary>
public class KoreanParticleTests
{
    static string 편다(string 틀, params object[] 값) =>
        LocFormat.Apply(틀, LocArgs.Of(값), KoreanParticles.Standard);

    // ── 짝 여섯. 어느 꼴로 적어도 같은 결과다 ────────────────

    [TestCase("{0}을", "스크랩을(를)")]
    [TestCase("{0}를", "스크랩을(를)")]
    [TestCase("{0}이", "스크랩이(가)")]
    [TestCase("{0}가", "스크랩이(가)")]
    [TestCase("{0}은", "스크랩은(는)")]
    [TestCase("{0}는", "스크랩은(는)")]
    [TestCase("{0}와", "스크랩와(과)")]
    [TestCase("{0}과", "스크랩와(과)")]
    [TestCase("{0}으로", "스크랩으로(로)")]
    [TestCase("{0}로", "스크랩으로(로)")]
    [TestCase("{0}아", "스크랩아(야)")]
    [TestCase("{0}야", "스크랩아(야)")]
    public void 어느_꼴로_적어도_두_꼴_나란히로_나온다(string 틀, string 기대)
    {
        Assert.AreEqual(기대, 편다(틀, "스크랩"),
            "표에 어느 꼴로 적든 같아야 한다 — 쓰는 사람이 표시자를 배울 필요가 없다는 것이 요점이다");
    }

    // ── 손대면 안 되는 것들 (이 모듈의 진짜 위험) ────────────

    [Test]
    public void 자리표와_무관한_글은_손대지_않는다()
    {
        const string 문장 = "이 세계에서 유일하게 타는 것이다";
        Assert.AreEqual(문장, 편다(문장));
    }

    [TestCase("{0}이라고 부른다", "낫이라고 부른다")]
    [TestCase("{0}은하계", "낫은하계")]
    [TestCase("{0}로서", "낫로서")]
    [TestCase("{0}과학", "낫과학")]
    [TestCase("{0}가족", "낫가족")]
    public void 조사_뒤에_글자가_이어지면_조사가_아니다(string 틀, string 기대)
    {
        // "이(가)라고"가 되면 참사다. 조사 다음은 공백·문장부호·끝이어야 한다.
        Assert.AreEqual(기대, 편다(틀, "낫"));
    }

    [Test]
    public void 자리표_뒤가_아닌_조사는_손대지_않는다()
    {
        // 값 자체가 "이"로 끝나도, 문장 가운데의 "이"도 마찬가지다.
        Assert.AreEqual("고사리 이 좋다", 편다("{0} 이 좋다", "고사리"));
    }

    // ── 경계 ─────────────────────────────────────────────────

    [Test]
    public void 문자열_끝에_온_조사도_편다()
    {
        Assert.AreEqual("도끼은(는)", 편다("{0}은", "도끼"));
    }

    [Test]
    public void 문장부호_앞에_온_조사도_편다()
    {
        Assert.AreEqual("도끼을(를).", 편다("{0}을.", "도끼"));
        Assert.AreEqual("도끼이(가), 스크랩", 편다("{0}이, 스크랩", "도끼"));
    }

    [Test]
    public void 으로는_두_글자를_통째로_본다()
    {
        Assert.AreEqual("칼으로(로) 벤다", 편다("{0}으로 벤다", "칼"),
            "으로를 먼저 보지 않으면 앞의 으가 남는다");
    }

    [Test]
    public void 자리표가_여럿이면_각각_편다()
    {
        Assert.AreEqual("스크랩을(를) 만들려면 도끼이(가) 3개 필요합니다",
            편다("{0}을 만들려면 {1}이 {2}개 필요합니다", "스크랩", "도끼", 3));
    }

    [Test]
    public void 조사가_없는_문장은_그대로다()
    {
        Assert.AreEqual("버섯 목재 2/5", 편다("{0} {1}/{2}", "버섯 목재", 2, 5));
    }

    // ── 로케일 ───────────────────────────────────────────────

    [Test]
    public void 해석기를_넘기지_않으면_아무_일도_없다()
    {
        // 한국어가 아닌 로케일에서는 Loc이 해석기를 아예 넘기지 않는다.
        Assert.AreEqual("스크랩을", LocFormat.Apply("{0}을", LocArgs.Of("스크랩")));
    }

    [Test]
    public void 로케일이_한국어인지_가린다()
    {
        Assert.IsTrue(KoreanParticles.IsKoreanLocale("ko"));
        Assert.IsTrue(KoreanParticles.IsKoreanLocale("ko-KR"));
        Assert.IsFalse(KoreanParticles.IsKoreanLocale("en"));
        Assert.IsFalse(KoreanParticles.IsKoreanLocale("ja"));
        Assert.IsFalse(KoreanParticles.IsKoreanLocale(null));
    }

    [Test]
    public void 영어_로케일에서는_문장이_그대로_나간다()
    {
        string 처음 = Loc.CurrentLocale;
        try
        {
            Loc.SetLocale("en");
            string en = Loc.F("UI", "ingredient_entry", "Scrap", 2, 5);
            Assert.IsFalse(en.Contains("("),
                $"en 문장에 조사 처리가 돌았다 — \"{en}\"");
        }
        finally { Loc.SetLocale(처음); }
    }

    // ── 나중에 받침으로 고를 수 있는가 (설계 보증) ───────────

    /// <summary>자리마다 어떤 값이 도착했는지 그대로 되돌려 주는 시험용 해석기.</summary>
    sealed class 값을_그대로_돌려주는_해석기 : IParticleResolver
    {
        public readonly List<string> 받은값 = new List<string>();
        public readonly List<string> 받은짝 = new List<string>();

        public string Resolve(string precedingValue, ParticlePair pair)
        {
            받은값.Add(precedingValue);
            받은짝.Add(pair.WithFinal + "/" + pair.WithoutFinal);
            return "<" + precedingValue + ">";
        }
    }

    [Test]
    public void 조사를_정하는_자리에_앞의_값이_실제로_도착한다()
    {
        // 오늘은 값을 쓰지 않으므로, 값을 안 넘겨도 모든 검사가 통과해 버린다.
        // 그러면 "나중에 받침으로 올릴 수 있다"가 지켜졌는지 아무도 모른다.
        // 값을 그대로 되돌려 주는 해석기를 끼워 자리마다 옳은 값이 오는지 본다.
        var 시험 = new 값을_그대로_돌려주는_해석기();

        string 결과 = LocFormat.Apply("{0}을 만들려면 {1}이 든다",
                                      LocArgs.Of("스크랩", "도끼"), 시험);

        Assert.AreEqual("스크랩<스크랩> 만들려면 도끼<도끼> 든다", 결과);
        CollectionAssert.AreEqual(new[] { "스크랩", "도끼" }, 시험.받은값,
            "조사 앞에 실제로 들어간 값이 그 자리에 도착해야 나중에 받침으로 고를 수 있다");
        CollectionAssert.AreEqual(new[] { "을/를", "이/가" }, 시험.받은짝);
    }

    [Test]
    public void 지금은_받침을_보지_않는다()
    {
        // 받침이 있든 없든 같은 꼴이 나온다. 이것이 오늘의 규격이고,
        // 바뀌는 날 고칠 곳은 BothFormsResolver.Resolve 하나다.
        Assert.AreEqual("스크랩을(를)", 편다("{0}을", "스크랩"));
        Assert.AreEqual("도끼을(를)", 편다("{0}을", "도끼"));
    }
}
