using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Survive.Items;
using Survive.Localization;

/// <summary>
/// 초안 생성 도구의 규칙. 도구가 사람의 번역을 덮으면 그 도구는 두 번 다시 쓰이지 않는다 —
/// 그것이 이 파일이 지키는 것이다.
///
/// 파일도 AssetDatabase도 건드리지 않는다. <see cref="DataTextDraft.Merge"/>가
/// 글 → 글 순수 함수라 그렇게 할 수 있다.
/// </summary>
public class DataTextDraftTests
{
    const string 머리 = "Category,Key,ko,en\nUI,craft_empty,아직 아는 제작법이 없다,\n";

    static DataTextEntry 칸(string category, string key, string ko, string note = null) =>
        new DataTextEntry
        {
            Category = category,
            Key = key,
            Korean = ko,
            AssetKorean = ko,
            Note = note,
            Resolve = () => ko,
        };

    static string 데이터구역(string csv)
    {
        int at = csv.IndexOf(DataTextDraft.Marker, System.StringComparison.Ordinal);
        return at < 0 ? "" : csv.Substring(at);
    }

    static List<string> 줄들(string csv) =>
        데이터구역(csv).Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Length > 0 && l[0] != '#')
            .ToList();

    // ── 사람의 구역을 건드리지 않는다 ─────────────────────────

    [Test]
    public void 표시줄_위쪽은_글자_하나_바뀌지_않는다()
    {
        var r = DataTextDraft.Merge(머리, new[] { 칸("Item", "scrap.name", "스크랩") });

        StringAssert.StartsWith(머리, r.Csv);
    }

    [Test]
    public void 두_번_돌려도_같은_글이_나온다()
    {
        var 칸들 = new[] { 칸("Item", "scrap.name", "스크랩"), 칸("Item", "axe.name", "도끼") };

        var 한번 = DataTextDraft.Merge(머리, 칸들);
        var 두번 = DataTextDraft.Merge(한번.Csv, 칸들);

        Assert.AreEqual(한번.Csv, 두번.Csv, "돌릴 때마다 파일이 달라지면 git diff를 읽을 수 없다");
        Assert.IsEmpty(두번.Added);
        Assert.AreEqual(2, 두번.Kept);
    }

    // ── 사람이 고친 것을 덮지 않는다 ──────────────────────────

    [Test]
    public void 사람이_채운_번역을_덮지_않는다()
    {
        var 칸들 = new[] { 칸("Item", "scrap.name", "스크랩") };
        var 처음 = DataTextDraft.Merge(머리, 칸들);

        // 번역가가 en 칸을 채웠다
        var 고친것 = 처음.Csv.Replace("Item,scrap.name,스크랩,", "Item,scrap.name,스크랩,Scrap");

        var 다시 = DataTextDraft.Merge(고친것, 칸들);

        StringAssert.Contains("Item,scrap.name,스크랩,Scrap", 다시.Csv);
        Assert.AreEqual(1, 다시.Kept);
        Assert.IsEmpty(다시.Added);
    }

    [Test]
    public void 에셋의_한국어가_바뀌어도_표를_덮지_않고_알리기만_한다()
    {
        var 처음 = DataTextDraft.Merge(머리, new[] { 칸("Item", "scrap.name", "스크랩") });
        var 다시 = DataTextDraft.Merge(처음.Csv, new[] { 칸("Item", "scrap.name", "고철") });

        StringAssert.Contains("Item,scrap.name,스크랩,", 다시.Csv);
        Assert.AreEqual(1, 다시.Drifted.Count, "어긋난 것을 알리지 않으면 아무도 모른다");
        StringAssert.Contains("고철", 다시.Drifted[0]);
    }

    // ── 새 키 / 사라진 키 ─────────────────────────────────────

    [Test]
    public void 새_키만_덧붙인다()
    {
        var 처음 = DataTextDraft.Merge(머리, new[] { 칸("Item", "scrap.name", "스크랩") });
        var 다시 = DataTextDraft.Merge(처음.Csv, new[]
        {
            칸("Item", "scrap.name", "스크랩"),
            칸("Item", "axe.name", "도끼"),
        });

        CollectionAssert.AreEqual(new[] { "Item/axe.name" }, 다시.Added);
        Assert.AreEqual(1, 다시.Kept);
    }

    [Test]
    public void 에셋이_사라진_키는_지우지_않고_알리기만_한다()
    {
        var 처음 = DataTextDraft.Merge(머리, new[]
        {
            칸("Item", "scrap.name", "스크랩"),
            칸("Item", "axe.name", "도끼"),
        });
        var 다시 = DataTextDraft.Merge(처음.Csv, new[] { 칸("Item", "scrap.name", "스크랩") });

        StringAssert.Contains("Item,axe.name,도끼,", 다시.Csv, "번역을 도구가 지우면 되살릴 길이 없다");
        CollectionAssert.AreEqual(new[] { "Item/axe.name" }, 다시.Orphans);
    }

    // ── 순서 ──────────────────────────────────────────────────

    [Test]
    public void 카테고리와_키로_정렬해_다시_쓴다()
    {
        var r = DataTextDraft.Merge(머리, new[]
        {
            칸("Recipe", "axe.name", "도끼"),
            칸("Item", "scrap.name", "스크랩"),
            칸("Item", "axe.name", "도끼"),
            칸("Blueprint", "bp_x.name", "엑스"),
        });

        CollectionAssert.AreEqual(
            new[]
            {
                "Blueprint,bp_x.name,엑스,",
                "Item,axe.name,도끼,",
                "Item,scrap.name,스크랩,",
                "Recipe,axe.name,도끼,",
            },
            줄들(r.Csv));
    }

    [Test]
    public void 넣는_순서가_달라도_결과가_같다()
    {
        var 앞 = new[] { 칸("Item", "a.name", "가"), 칸("Item", "b.name", "나") };
        var 뒤 = new[] { 칸("Item", "b.name", "나"), 칸("Item", "a.name", "가") };

        Assert.AreEqual(DataTextDraft.Merge(머리, 앞).Csv, DataTextDraft.Merge(머리, 뒤).Csv);
    }

    // ── 값의 모양 ─────────────────────────────────────────────

    [Test]
    public void 쉼표가_든_값은_따옴표로_감싼다()
    {
        var r = DataTextDraft.Merge(머리, new[] { 칸("Item", "x.desc", "가볍다, 그리고 질기다") });

        StringAssert.Contains("\"가볍다, 그리고 질기다\"", r.Csv);

        // 되읽어서 같은 값이 나와야 진짜다.
        var 되읽음 = StringCatalog.Parse(r.Csv);
        Assert.IsTrue(되읽음.TryGet("ko", new LocKey("Item", "x.desc"), out var v));
        Assert.AreEqual("가볍다, 그리고 질기다", v);
        Assert.IsEmpty(되읽음.Problems);
    }

    [Test]
    public void 맥락_주석은_그_줄_위에_붙는다()
    {
        var r = DataTextDraft.Merge(머리,
            new[] { 칸("Discovery", "d.line.text", "분석...", "대상 아이템: 스크랩") });

        var lines = 데이터구역(r.Csv).Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        int at = lines.FindIndex(l => l.StartsWith("Discovery,"));

        Assert.Greater(at, 0);
        StringAssert.Contains("대상 아이템: 스크랩", lines[at - 1]);
        Assert.AreEqual('#', lines[at - 1][0], "맥락은 주석이라야 파서가 로케일 열로 오해하지 않는다");

        // 주석이 늘어도 표는 멀쩡해야 한다.
        Assert.IsEmpty(StringCatalog.Parse(r.Csv).Problems);
    }

    [Test]
    public void 표시줄을_못_찾으면_표_끝에_구역을_만든다()
    {
        var r = DataTextDraft.Merge(머리, new[] { 칸("Item", "scrap.name", "스크랩") });

        StringAssert.Contains(DataTextDraft.Marker, r.Csv);
        Assert.AreEqual(1, r.Added.Count);
    }

    [Test]
    public void 헤더가_망가진_표는_아예_건드리지_않는다()
    {
        var r = DataTextDraft.Merge("이건,표가,아니다\n", new[] { 칸("Item", "x.name", "엑스") });

        Assert.IsNotNull(r.Abort, "모양이 이상한 표를 도구가 다시 쓰면 남은 것까지 잃는다");
        Assert.IsNull(r.Csv);
    }

    [Test]
    public void CRLF_표는_CRLF로_되돌려_쓴다()
    {
        var r = DataTextDraft.Merge(머리.Replace("\n", "\r\n"),
                                    new[] { 칸("Item", "scrap.name", "스크랩") });

        StringAssert.Contains("Item,scrap.name,스크랩,\r\n", r.Csv);
    }

    // ── 키 짓기 ───────────────────────────────────────────────

    [TestCase("LooseScrap", "loose_scrap")]
    [TestCase("OreVein", "ore_vein")]
    [TestCase("Debris", "debris")]
    [TestCase("AshFern", "ash_fern")]
    [TestCase("MainScene", "main_scene")]
    [TestCase("Scene_MainScene", "scene_main_scene")]
    [TestCase("HTTPServer", "httpserver")]
    [TestCase("Tier2Node", "tier2_node")]
    [TestCase("  spaced  name ", "spaced_name")]
    [TestCase("", "")]
    public void 에셋_이름을_키_규약에_맞게_바꾼다(string raw, string expected)
    {
        Assert.AreEqual(expected, DataText.Slug(raw));
    }

    [Test]
    public void 아이템_키는_문서에_적힌_그대로다()
    {
        var item = ScriptableObject.CreateInstance<ItemDataSO>();
        item.id = "mushroom_wood";

        Assert.AreEqual("Item/mushroom_wood.name", DataText.NameKey(item).ToString());
        Assert.AreEqual("Item/mushroom_wood.desc", DataText.DescKey(item).ToString());

        Object.DestroyImmediate(item);
    }

    // ── 글꼴 안전 ─────────────────────────────────────────────

    [Test]
    public void 글꼴에_없는_줄표는_가운뎃점으로_바꾼다()
    {
        Assert.AreEqual("챕터 1 · 부유섬", FontSafe.Sanitize("챕터 1 — 부유섬"));
        Assert.IsTrue(FontSafe.HasMissing("챕터 1 — 부유섬"));
    }

    [Test]
    public void 멀쩡한_글에는_손대지_않는다()
    {
        const string 그대로 = "스크랩 · 좌클릭으로 부순다";

        Assert.AreSame(그대로, FontSafe.Sanitize(그대로),
            "바꿀 것이 없으면 새 문자열을 만들지 않는다");
        Assert.IsFalse(FontSafe.HasMissing(그대로));
    }

    [Test]
    public void 대치할_짝이_없는_글자는_남겨_게이트가_잡게_한다()
    {
        // 악센트 라틴 문자에는 마땅한 대치가 없다. 아무 글자나 밀어 넣는 것보다
        // 게이트가 실패해 사람이 판단하는 편이 낫다.
        Assert.AreEqual("café É", FontSafe.Sanitize("café É"));
        Assert.IsTrue(FontSafe.HasMissing("café É"));
    }

    [Test]
    public void 대치한_값은_표에_들어가고_원문은_따로_남는다()
    {
        var chapter = ScriptableObject.CreateInstance<Survive.Progression.ChapterSO>();
        chapter.id = "ch_x";
        chapter.title = "챕터 1 — 부유섬";

        var entries = new List<DataTextEntry>();
        DataTextCatalog.Collect(new ScriptableObject[] { chapter }, entries);

        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual("챕터 1 · 부유섬", entries[0].Korean);
        Assert.AreEqual("챕터 1 — 부유섬", entries[0].AssetKorean);
        Assert.IsTrue(entries[0].FontFixed);
        StringAssert.Contains("에셋 원문", entries[0].Note, "왜 다른지가 표에 적혀야 한다");

        Object.DestroyImmediate(chapter);
    }

    [Test]
    public void 빈_칸은_표에_담지_않는다()
    {
        var item = ScriptableObject.CreateInstance<ItemDataSO>();
        item.id = "x";
        item.displayName = "엑스";
        item.description = "   ";   // 인스펙터에서 흔히 이 꼴로 남는다

        var entries = new List<DataTextEntry>();
        DataTextCatalog.Collect(new ScriptableObject[] { item }, entries);

        CollectionAssert.AreEqual(new[] { "Item/x.name" }, entries.Select(e => e.ToString()));

        Object.DestroyImmediate(item);
    }
}
