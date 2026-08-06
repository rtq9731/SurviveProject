using NUnit.Framework;
using UnityEngine;
using Survive.Items;
using Survive.Localization;
using Survive.UI;

/// <summary>
/// <b>어순이 실제로 뒤집히는가.</b>
///
/// 앞의 게이트들은 "조각으로 짓지 않았다"까지만 증명한다. 그것만으로는
/// 규칙이 무엇을 위한 것이었는지 보이지 않는다 — 표에 <c>en</c> 열을 실제로
/// <b>다른 순서</b>로 채워 두고, 그 로케일에서 화면 문자열이 그대로 뒤집혀 나오는
/// 것을 여기서 보인다. 이것이 이 라운드의 증거다.
///
/// 표(strings.csv)의 값을 그대로 읽어 대조하지 않고 <b>눈에 보이는 결과</b>를
/// 못 박는 이유는, 표와 코드가 같이 틀려도 통과하는 검사를 만들지 않기 위해서다.
/// </summary>
public class LocWordOrderTests
{
    string _처음로케일;

    [SetUp]
    public void 로케일을_기억해_둔다() => _처음로케일 = Loc.CurrentLocale;

    [TearDown]
    public void 로케일을_되돌린다() => Loc.SetLocale(_처음로케일);

    static ItemDataSO 물건(string 이름, ItemCategory 분류 = ItemCategory.Resource, int 묶음 = 1)
    {
        var it = ScriptableObject.CreateInstance<ItemDataSO>();
        it.id = "test_item";
        it.displayName = 이름;
        it.category = 분류;
        it.maxStack = 묶음;
        return it;
    }

    [Test]
    public void 재료_한_항목의_어순이_로케일마다_다르다()
    {
        Loc.SetLocale("ko");
        Assert.AreEqual("버섯 목재 2/5",
            Loc.F("UI", "ingredient_entry", "버섯 목재", 2, 5),
            "한국어는 이름이 앞이다");

        Loc.SetLocale("en");
        Assert.AreEqual("2/5 Mushroom Wood",
            Loc.F("UI", "ingredient_entry", "Mushroom Wood", 2, 5),
            "영어는 수가 앞이다 — 같은 인자로 자리가 뒤집혔다");
    }

    [Test]
    public void 쪽지_아래_한_줄의_어순이_로케일마다_다르다()
    {
        var it = 물건("버섯 목재", ItemCategory.Resource, 200);

        Loc.SetLocale("ko");
        Assert.AreEqual("재료  ·  한 칸에 200개", ItemTooltipContent.Meta(it));

        Loc.SetLocale("en");
        Assert.AreEqual("200 per slot  ·  Material", ItemTooltipContent.Meta(it),
            "분류와 묶음 수의 앞뒤가 통째로 바뀌었다");
    }

    [Test]
    public void 자리표_순서를_바꿔도_같은_값이_같은_자리에_간다()
    {
        // 인자는 한 벌인데 두 언어가 서로 다른 순서로 쓴다. 코드는 한 줄도 다르지 않다.
        Loc.SetLocale("ko");
        string ko = Loc.F("UI", "ingredient_need", "스크랩", 3);

        Loc.SetLocale("en");
        string en = Loc.F("UI", "ingredient_need", "Scrap", 3);

        StringAssert.StartsWith("스크랩", ko);
        StringAssert.StartsWith("3", en);
        StringAssert.EndsWith("Scrap", en);
    }

    [Test]
    public void 한국어에서만_조사가_펴진다()
    {
        Loc.SetLocale("ko");
        StringAssert.Contains("을(를)", Loc.F("UI", "ingredients_line", "도끼", "스크랩 3"),
            "한국어 문장의 조사는 두 꼴 나란히로 나간다");

        Loc.SetLocale("en");
        string en = Loc.F("UI", "ingredients_line", "Axe", "Scrap 3");
        Assert.IsFalse(en.Contains("을"), $"영어 문장에 조사 처리가 돌았다 — \"{en}\"");
        StringAssert.Contains("Axe", en);
    }

    [Test]
    public void 번역이_없어_폴백한_문장에도_조사가_따라온다()
    {
        // en 칸이 빈 줄은 ko로 폴백한다. 그때 화면에 나온 글은 한국어인데
        // 조사만 안 펴지면 그것이 더 이상하다 — 조사는 로케일 이름이 아니라
        // 값이 나온 표를 따라야 한다.
        var 원래표 = Loc.Catalog;
        try
        {
            Loc.Load(StringCatalog.Parse("Category,Key,ko,en,Comment\nUI,fallback,{0}을 준다,,{0}=이름"));
            Loc.SetLocale("en");
            Assert.AreEqual("도끼을(를) 준다", Loc.F("UI", "fallback", "도끼"));
        }
        finally { Loc.Load(원래표); }
    }

    [Test]
    public void 의사_번역에서도_값이_제자리에_들어간다()
    {
        // 부풀린 글 안쪽에 자리표가 그대로 남아 있어야 판때기 검사가 뜻을 가진다.
        Loc.SetLocale(StringCatalog.PseudoLocale);

        string 줄 = Loc.F("UI", "ingredient_entry", "버섯 목재", 2, 5);
        Assert.IsTrue(PseudoLocalizer.IsTransformed(줄), $"부풀지 않았다 — \"{줄}\"");
        StringAssert.Contains("버섯 목재", 줄);
        StringAssert.Contains("2/5", 줄);
        Assert.IsFalse(줄.Contains("{0}"), "자리표가 채워지지 않았다");
    }
}
