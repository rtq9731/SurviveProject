using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using Survive.Creatures;
using Survive.Items;
using Survive.Localization;
using Survive.Narrative;
using Survive.Progression;

/// <summary>
/// 백로그 43 — 분석 기록(도감) 목록을 짓는 규칙.
///
/// 여기서 지키는 것은 셋이다.
/// <list type="number">
/// <item><b>도감은 아무것도 새로 기억하지 않는다.</b> 열렸는지 여부는 언제나
///       <see cref="UnlockLedger"/> 하나에서만 나오고, 연구는 연구대가 쓰는
///       판정(<see cref="ResearchService.IsKnown"/>)과 같은 답을 낸다.</item>
/// <item><b>모르는 것도 목록에 남는다.</b> 다만 이름조차 주지 않는다 —
///       이름을 아는 것이 곧 아는 것이기 때문이다.</item>
/// <item><b>설명문을 새로 쓰지 않는다.</b> 발견은 그때 AI가 한 말 그대로,
///       생물은 정의가 이미 들고 있던 설명과 수치 그대로다.</item>
/// </list>
/// </summary>
public class CodexCatalogTests
{
    readonly List<CodexEntry> _rows = new List<CodexEntry>();
    UnlockLedger _ledger;

    DiscoveryBookSO _discoveryBook;
    ResearchBookSO _researchBook;
    CreatureBookSO _creatureBook;

    BlueprintSO _mechanism;   // 현장 발견이 연다
    BlueprintSO _walker;      // 연구가 연다
    BlueprintSO _shared;      // 둘 다 연다 — 중복 제거를 본다

    const string 발견대사 = "외계 금속물체 분석... 에너지로 사용가능한 것으로 보입니다. " +
                            "해당 물질을 사용한 제작법이 있습니다.";
    const string 연구대사 = "젖지 않는 막 구조 분석... 액면 보행에 쓸 수 있는 것으로 보입니다. " +
                            "해당 구조를 적용한 제작법을 확보했습니다.";

    // ── 만들기 ───────────────────────────────────────────────────

    static ItemDataSO 아이템(string id, string name)
    {
        var it = ScriptableObject.CreateInstance<ItemDataSO>();
        it.id = id;
        it.displayName = name;
        return it;
    }

    static BlueprintSO 청사진(string id, string name, string hint)
    {
        var bp = ScriptableObject.CreateInstance<BlueprintSO>();
        bp.id = id;
        bp.displayName = name;
        bp.hint = hint;
        return bp;
    }

    static DiscoverySO 발견(string id, ItemDataSO item, string text, params BlueprintSO[] unlocks)
    {
        var d = ScriptableObject.CreateInstance<DiscoverySO>();
        d.id = id;
        d.item = item;
        d.unlocks = unlocks;
        d.line = new SequenceSO.Line { speaker = "우주복 AI", text = text };
        return d;
    }

    static ResearchEntrySO 연구(string id, string name, string text, params BlueprintSO[] unlocks)
    {
        var e = ScriptableObject.CreateInstance<ResearchEntrySO>();
        e.id = id;
        e.displayName = name;
        e.unlocks = unlocks;
        e.line = new SequenceSO.Line { speaker = "우주복 AI", text = text };
        return e;
    }

    static CreatureDefinitionSO 생물(string id, string name, string description)
    {
        var c = ScriptableObject.CreateInstance<CreatureDefinitionSO>();
        c.id = id;
        c.displayName = name;
        c.codexDescription = description;
        c.tier = TrophicTier.Consumer1;
        c.locomotion = LocomotionType.Ground;
        c.behavior = BehaviorProfile.Aggressive;
        c.maxHealth = 60f;
        c.moveSpeed = 6.2f;
        c.detectRadius = 14f;
        c.attackDamage = 28f;
        return c;
    }

    [SetUp]
    public void SetUp()
    {
        // 표를 절반만 싣는다.
        //
        // 여기 세우는 가짜 에셋들은 진짜와 같은 id를 쓰는데(bp_surface_walker 등),
        // 데이터 에셋 구역이 실려 있으면 DataText가 그 id로 진짜 게임의 문장을 꺼내 와
        // 가짜 문장을 덮는다. 그것은 조회 경로가 제대로 돈다는 증거이지 도감의 규칙이
        // 아니다 (표가 없을 때의 폴백은 DataTextGateTests가 지킨다).
        //
        // 그렇다고 표를 통째로 비울 수는 없다. 도감이 자기 목소리로 쓰는 말
        // (갈래 이름, 미확인 안내, 관측 보고 틀)은 이제 Codex/* 로 표에 있고,
        // 표가 비면 그 자리에 키가 그대로 뜬다.
        Loc.Load(손으로_지은_구역만());
        Loc.SetLocale(StringCatalog.DefaultLocale);

        _ledger = new UnlockLedger();

        _mechanism = 청사진("bp_salvaged_mechanism", "부품 재조립", "기계 부품을 손에 넣으면 알게 된다");
        _walker = 청사진("bp_surface_walker", "액면 보행 장치", "");
        _shared = 청사진("bp_shared", "공통 청사진", "두 경로가 함께 연다");

        var scrap = 아이템("scrap", "스크랩");
        var part = 아이템("machine_part", "기계 부품");

        _discoveryBook = ScriptableObject.CreateInstance<DiscoveryBookSO>();
        _discoveryBook.discoveries = new[]
        {
            발견("disc_scrap", scrap, 발견대사, _mechanism, _shared),
            발견("disc_machine_part", part, "구조물 파편 분석... 재조립이 가능한 것으로 보입니다.", _mechanism),
        };

        _researchBook = ScriptableObject.CreateInstance<ResearchBookSO>();
        _researchBook.entries = new[]
        {
            연구("res_surface_walker", "젖지 않는 막 분석", 연구대사, _walker, _shared),
        };

        _creatureBook = ScriptableObject.CreateInstance<CreatureBookSO>();
        _creatureBook.creatures = new[] { 생물("scythe", "낫", "이족보행의 빠른 기계.") };
    }

    [TearDown]
    public void TearDown()
    {
        // 다음 파일의 검사가 반쪽 표를 물려받으면 영문 모를 이유로 무너진다.
        Loc.Load(LocalizationTestBootstrap.LoadCatalogFromDisk());
        Loc.SetLocale(StringCatalog.DefaultLocale);
    }

    /// <summary>
    /// 표에서 <b>손으로 지은 구역</b>(경계선 위쪽)만 잘라 싣는다.
    /// 경계선은 초안 생성 도구가 쓰는 것과 같은 한 줄이라, 구역이 옮겨 다녀도
    /// 여기가 따라간다.
    /// </summary>
    static StringCatalog 손으로_지은_구역만()
    {
        string text = System.IO.File.ReadAllText(LocalizationTestBootstrap.CsvPath);
        int cut = text.IndexOf(DataTextDraft.Marker, System.StringComparison.Ordinal);

        Assert.Greater(cut, 0, "표에서 데이터 에셋 구역의 경계선을 못 찾았다");
        return StringCatalog.Parse(text.Substring(0, cut));
    }

    CodexEntry 줄(string key)
    {
        foreach (var r in _rows) if (r.Key == key) return r;
        return null;
    }

    // ── 물질 분석 ────────────────────────────────────────────────

    [Test]
    public void 모르는_물질도_목록에_남되_이름을_주지_않는다()
    {
        CodexCatalog.BuildDiscoveries(_discoveryBook, _ledger, _rows);

        Assert.AreEqual(2, _rows.Count, "잠겼다고 줄을 지우면 무엇을 찾아 나설지 알 수 없다");

        var row = 줄("disc_scrap");
        Assert.IsFalse(row.Unlocked);
        Assert.AreEqual(CodexCatalog.UnknownTitle, row.Title, "이름을 아는 것이 곧 아는 것이다");
        Assert.AreEqual(CodexCatalog.UnknownDiscoveryBody, row.Body);
        StringAssert.DoesNotContain("스크랩", row.Body, "잠긴 줄이 물질 이름을 흘리면 안 된다");
    }

    [Test]
    public void 열린_물질은_그때_AI가_한_말을_그대로_보여_준다()
    {
        _ledger.Unlock("disc_scrap");
        CodexCatalog.BuildDiscoveries(_discoveryBook, _ledger, _rows);

        var row = 줄("disc_scrap");
        Assert.IsTrue(row.Unlocked);
        Assert.AreEqual("스크랩", row.Title);
        Assert.AreEqual(발견대사, row.Body,
            "도감용 문장을 따로 쓰면 같은 물질을 두 목소리가 설명하게 된다");
    }

    [Test]
    public void 원장이_없으면_아무것도_열리지_않는다()
    {
        CodexCatalog.BuildDiscoveries(_discoveryBook, null, _rows);

        Assert.AreEqual(2, _rows.Count);
        foreach (var r in _rows) Assert.IsFalse(r.Unlocked, "원장이 없다고 다 아는 것이 되면 안 된다");
    }

    [Test]
    public void 목록이_없으면_담을_그릇을_비워_돌려준다()
    {
        _rows.Add(new CodexEntry { Key = "찌꺼기" });
        CodexCatalog.BuildDiscoveries(null, _ledger, _rows);

        Assert.AreEqual(0, _rows.Count, "지난 갈래의 줄이 남아 있으면 화면이 뒤섞인다");
    }

    // ── 확보 제작법 ──────────────────────────────────────────────

    [Test]
    public void 청사진은_두_해금_채널을_모두_훑고_중복은_하나로_센다()
    {
        CodexCatalog.BuildBlueprints(_discoveryBook, _researchBook, _ledger, _rows);

        Assert.AreEqual(3, _rows.Count, "부품 재조립 · 공통 · 액면 보행");
        Assert.IsNotNull(줄("bp_salvaged_mechanism"), "발견 둘이 같은 것을 열어도 줄은 하나다");
        Assert.IsNotNull(줄("bp_shared"));
        Assert.IsNotNull(줄("bp_surface_walker"));
    }

    [Test]
    public void 잠긴_청사진은_여는_방법을_그대로_적는다()
    {
        CodexCatalog.BuildBlueprints(_discoveryBook, _researchBook, _ledger, _rows);

        Assert.AreEqual("기계 부품을 손에 넣으면 알게 된다", 줄("bp_salvaged_mechanism").Body,
            "청사진 에셋이 이미 들고 있던 힌트를 다시 쓰지 않는다");
        Assert.AreEqual(CodexCatalog.UnknownBlueprintBody, 줄("bp_surface_walker").Body,
            "힌트가 비어 있으면 기본 안내로 떨어진다");
    }

    [Test]
    public void 열린_청사진은_어느_경로로_알게_됐는지를_적는다()
    {
        _ledger.Unlock("bp_salvaged_mechanism");
        _ledger.Unlock("bp_surface_walker");
        CodexCatalog.BuildBlueprints(_discoveryBook, _researchBook, _ledger, _rows);

        StringAssert.Contains("현장 분석", 줄("bp_salvaged_mechanism").Body);
        StringAssert.Contains("스크랩", 줄("bp_salvaged_mechanism").Body,
            "첫 번째로 여는 발견의 물질 이름이 출처가 된다");

        StringAssert.Contains("정밀 분석", 줄("bp_surface_walker").Body,
            "주운 것과 밝혀낸 것은 같은 문장으로 말할 수 없다");
        StringAssert.Contains("젖지 않는 막 분석", 줄("bp_surface_walker").Body);
    }

    [Test]
    public void 이름_없는_청사진은_id로라도_부른다()
    {
        var 이름없음 = 청사진("bp_nameless", "", "");
        _discoveryBook.discoveries = new[] { 발견("disc_x", 아이템("x", "엑스"), "", 이름없음) };
        _ledger.Unlock("bp_nameless");

        CodexCatalog.BuildBlueprints(_discoveryBook, null, _ledger, _rows);
        Assert.AreEqual("bp_nameless", 줄("bp_nameless").Title);
    }

    // ── 정밀 분석 ────────────────────────────────────────────────

    [Test]
    public void 연구는_연구대와_같은_판정을_쓴다()
    {
        var entry = _researchBook.entries[0];

        CodexCatalog.BuildResearch(_researchBook, _ledger, _rows);
        Assert.IsFalse(줄("res_surface_walker").Unlocked);
        Assert.IsFalse(ResearchService.IsKnown(entry, _ledger));

        // 이 항목이 여는 것 중 하나만 열려 있으면 아직 다 밝혀낸 것이 아니다.
        _ledger.Unlock("bp_surface_walker");
        CodexCatalog.BuildResearch(_researchBook, _ledger, _rows);
        Assert.IsFalse(줄("res_surface_walker").Unlocked, "연구대의 답과 어긋나면 안 된다");
        Assert.IsFalse(ResearchService.IsKnown(entry, _ledger));

        _ledger.Unlock("bp_shared");
        CodexCatalog.BuildResearch(_researchBook, _ledger, _rows);
        Assert.IsTrue(줄("res_surface_walker").Unlocked);
        Assert.IsTrue(ResearchService.IsKnown(entry, _ledger));
        Assert.AreEqual(연구대사, 줄("res_surface_walker").Body);
    }

    [Test]
    public void 잠긴_연구는_이름도_대사도_주지_않는다()
    {
        CodexCatalog.BuildResearch(_researchBook, _ledger, _rows);

        var row = 줄("res_surface_walker");
        Assert.AreEqual(CodexCatalog.UnknownTitle, row.Title);
        Assert.AreEqual(CodexCatalog.UnknownResearchBody, row.Body);
        StringAssert.DoesNotContain("액면", row.Body);
    }

    // ── 개체 관측 ────────────────────────────────────────────────

    [Test]
    public void 생물_열쇠는_codex_접두에_정의_id를_붙인_것이다()
    {
        Assert.AreEqual("codex_scythe", CodexCatalog.CreatureKey("scythe"));
        Assert.AreEqual("codex_scythe", CodexCatalog.CreatureKey(_creatureBook.creatures[0]));
        Assert.IsNull(CodexCatalog.CreatureKey(""), "id가 없으면 열쇠를 지어내지 않는다");
        Assert.IsNull(CodexCatalog.CreatureKey((CreatureDefinitionSO)null));

        CodexCatalog.BuildCreatures(_creatureBook, _ledger, _rows);
        Assert.AreEqual("codex_scythe", _rows[0].Key, "열쇠를 여는 쪽과 읽는 쪽의 계약이다");
    }

    [Test]
    public void 못_본_개체는_실루엣으로만_남는다()
    {
        CodexCatalog.BuildCreatures(_creatureBook, _ledger, _rows);

        var row = _rows[0];
        Assert.IsFalse(row.Unlocked);
        Assert.AreEqual(CodexCatalog.UnknownTitle, row.Title);
        Assert.AreEqual(CodexCatalog.UnknownCreatureBody, row.Body);
        StringAssert.DoesNotContain("이족보행", row.Body, "설명이 새어 나가면 실루엣이 아니다");
    }

    [Test]
    public void 관측된_개체는_정의가_들고_있던_설명과_수치를_보여_준다()
    {
        _ledger.Unlock("codex_scythe");
        CodexCatalog.BuildCreatures(_creatureBook, _ledger, _rows);

        var row = _rows[0];
        Assert.IsTrue(row.Unlocked);
        Assert.AreEqual("낫", row.Title);
        StringAssert.Contains("이족보행의 빠른 기계.", row.Body, "서사를 새로 쓰지 않는다");
        StringAssert.Contains("지상", row.Body, "어떻게 움직였는가는 눈으로 본 것이다");
        StringAssert.Contains("적대", row.Body);
        StringAssert.DoesNotContain("소비자", row.Body,
            "도감은 계층을 알려 주지 않는다 (기획서 §4.7) — " +
            "무엇인지 판정하는 순간 관찰로 발견할 것이 없어진다");
        StringAssert.Contains("60", row.Body);
        StringAssert.Contains("6.2", row.Body);
        StringAssert.Contains("14", row.Body);
    }

    [Test]
    public void 설명이_비어_있어도_잰_값은_남는다()
    {
        var 무명 = 생물("blank", "무명", "");
        _creatureBook.creatures = new[] { 무명 };
        _ledger.Unlock("codex_blank");

        CodexCatalog.BuildCreatures(_creatureBook, _ledger, _rows);
        StringAssert.Contains("지상", _rows[0].Body);
        Assert.IsFalse(_rows[0].Body.StartsWith("\n"), "빈 설명 자리에 빈 줄만 남으면 안 된다");
    }

    [Test]
    public void 광원_기피는_따로_적힌다()
    {
        var 낫 = _creatureBook.creatures[0];
        Assert.IsFalse(CodexCatalog.DescribeCreature(낫).Contains("광원 기피"));

        낫.avoidsLight = true;
        StringAssert.Contains("광원 기피", CodexCatalog.DescribeCreature(낫),
            "빛을 꺼린다는 것은 이 세계에서 대응 방법 그 자체다");
    }

    [Test]
    public void 수치는_기계의_지역_설정을_타지_않는다()
    {
        var 이전 = Thread.CurrentThread.CurrentCulture;
        try
        {
            // 소수점이 쉼표인 곳에서 돌려도 "6.2"여야 한다.
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            StringAssert.Contains("6.2", CodexCatalog.DescribeCreature(_creatureBook.creatures[0]));
        }
        finally { Thread.CurrentThread.CurrentCulture = 이전; }
    }

    // ── 세는 것과 적는 것 ────────────────────────────────────────

    [Test]
    public void 열린_줄의_수를_센다()
    {
        CodexCatalog.BuildDiscoveries(_discoveryBook, _ledger, _rows);
        Assert.AreEqual(0, CodexCatalog.CountUnlocked(_rows));

        _ledger.Unlock("disc_scrap");
        CodexCatalog.BuildDiscoveries(_discoveryBook, _ledger, _rows);
        Assert.AreEqual(1, CodexCatalog.CountUnlocked(_rows));
        Assert.AreEqual(0, CodexCatalog.CountUnlocked(null));
    }

    [Test]
    public void 갈래마다_이름이_있다()
    {
        foreach (CodexSection s in System.Enum.GetValues(typeof(CodexSection)))
            Assert.IsNotEmpty(CodexCatalog.SectionTitle(s), $"{s}의 탭 이름이 비었다");
    }

    /// <summary>
    /// 도감이 <b>자기 목소리로</b> 쓰는 말이 표를 거치는지.
    ///
    /// 이 검사가 없으면 누가 <c>const string</c>으로 되돌려 놓아도 아무 신호가 없다 —
    /// const는 컴파일할 때 값이 박혀 로케일을 영영 따라오지 못하는데,
    /// 한국어로 도는 동안에는 화면이 멀쩡해 보인다.
    /// </summary>
    [Test]
    public void 도감이_짓는_말은_로케일을_따라온다()
    {
        string 갈래_ko = CodexCatalog.SectionTitle(CodexSection.Discovery);
        string 안내_ko = CodexCatalog.UnknownDiscoveryBody;
        string 성향_ko = CodexCatalog.BehaviorName(BehaviorProfile.Aggressive);

        try
        {
            Loc.SetLocale("en");

            Assert.AreNotEqual(갈래_ko, CodexCatalog.SectionTitle(CodexSection.Discovery),
                "갈래 이름이 로케일을 안 따라온다");
            Assert.AreNotEqual(안내_ko, CodexCatalog.UnknownDiscoveryBody,
                "잠긴 항목의 안내가 로케일을 안 따라온다 — const로 박혀 있지 않은지 보라");
            Assert.AreNotEqual(성향_ko, CodexCatalog.BehaviorName(BehaviorProfile.Aggressive),
                "성향 이름이 로케일을 안 따라온다");
        }
        finally { Loc.SetLocale(StringCatalog.DefaultLocale); }
    }

    /// <summary>
    /// 관측 보고는 줄마다 표의 한 칸이다. 줄바꿈은 코드가 넣되,
    /// <b>말은 하나도 코드에 남아 있지 않아야</b> 한다.
    /// </summary>
    [Test]
    public void 관측_보고도_로케일을_따라온다()
    {
        var 낫 = _creatureBook.creatures[0];
        낫.avoidsLight = true;

        string ko = CodexCatalog.DescribeCreature(낫);
        StringAssert.Contains("내구", ko);
        StringAssert.Contains("광원 기피", ko);

        try
        {
            Loc.SetLocale("en");
            string en = CodexCatalog.DescribeCreature(낫);

            Assert.IsFalse(en.Contains("내구"), $"잰 값 줄이 한국어로 남았다: {en}");
            Assert.IsFalse(en.Contains("광원 기피"), $"광원 기피 줄이 한국어로 남았다: {en}");
            StringAssert.Contains("6.2", en, "수치는 로케일이 바뀌어도 그대로여야 한다");

            // 줄 수는 언어와 무관하다. 설명문 + 빈 줄 + 성질 + 잰 값 + 광원 기피.
            Assert.AreEqual(ko.Split('\n').Length, en.Split('\n').Length,
                "줄바꿈은 배치라서 언어에 따라 달라지면 안 된다");
        }
        finally { Loc.SetLocale(StringCatalog.DefaultLocale); }
    }

    /// <summary>
    /// 영양 단계는 여기 없다. 도감이 계층을 말로 옮기는 길을 아예 없앴기 때문이다
    /// (기획서 §4.7) — <see cref="CodexUnclassifiedGateTests"/>가 그것을 지킨다.
    /// </summary>
    [Test]
    public void 성향과_이동방식은_enum_전수에_이름이_있다()
    {
        foreach (BehaviorProfile b in System.Enum.GetValues(typeof(BehaviorProfile)))
            Assert.IsNotEmpty(CodexCatalog.BehaviorName(b));
        foreach (LocomotionType l in System.Enum.GetValues(typeof(LocomotionType)))
            Assert.IsNotEmpty(CodexCatalog.LocomotionName(l));
    }

    /// <summary>
    /// 본문 글꼴(ChosunGu)에 줄표가 없다. 화면에 나가면 두부(□)로 찍힌다 —
    /// 제작 목록에서 실제로 겪은 일이고, 도감은 글자가 훨씬 많다.
    /// </summary>
    [Test]
    public void 화면에_나가는_문구에_글꼴이_모르는_줄표가_없다()
    {
        var 금지 = new[] { '—', '–', '−' };

        CodexCatalog.BuildDiscoveries(_discoveryBook, _ledger, _rows);
        var 문구 = new List<string>
        {
            CodexCatalog.UnknownTitle,
            CodexCatalog.UnknownDiscoveryBody,
            CodexCatalog.UnknownBlueprintBody,
            CodexCatalog.UnknownResearchBody,
            CodexCatalog.UnknownCreatureBody,
            CodexCatalog.DescribeCreature(_creatureBook.creatures[0]),
        };
        foreach (CodexSection s in System.Enum.GetValues(typeof(CodexSection)))
            문구.Add(CodexCatalog.SectionTitle(s));

        foreach (var line in 문구)
            foreach (var bad in 금지)
                Assert.IsFalse(line.IndexOf(bad) >= 0,
                    $"\"{line}\"에 글꼴이 모르는 문자 U+{(int)bad:X4}가 있다");
    }
}
