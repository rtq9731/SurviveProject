using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Survive.Creatures;
using Survive.Harvesting;
using Survive.Items;
using Survive.Localization;
using Survive.Narrative;
using Survive.Progression;
using Survive.UI;
using UnityEngine;

/// <summary>
/// ⑪ 도감의 식물 탭 — <b>식물만 갈래를 갖는 구조</b>.
///
/// <b>무엇을 지키는가.</b> ⑩ 라운드는 도감에서 계층명을 통째로 걷어냈다
/// (분해자·생산자·소비자·미분류…). 그 위에 탭을 다시 얹는 것이므로, 이 탭이
/// <b>계층명을 다시 들여오는 통로</b>가 되면 실패다. 그래서 여기서 재는 것은 셋이다.
/// <list type="number">
/// <item><b>갈래가 새지 않는가.</b> 식물만 식물 탭에 들어가고, 식물이 아닌 것이
///       섞이지 않고, 식물이 일반 탭에도 겹쳐 나오지 않는다</item>
/// <item><b>빈 자리에 무엇이 적히는가.</b> 목록이 서지 않았을 때의 한 줄</item>
/// <item><b>예외가 본문 행세를 하지 않는가.</b> 도감을 열면 여전히 물질 분석이 먼저다</item>
/// </list>
/// 계층명이 되살아나지 않았다는 확인은 <c>CodexUnclassifiedGateTests</c>가 한다 —
/// 그쪽은 <see cref="CodexSection"/> 전수를 훑으므로 이 탭도 자동으로 걸린다.
/// </summary>
public class CodexPlantTabTests
{
    readonly List<CodexEntry> _rows = new List<CodexEntry>();
    UnlockLedger _ledger;

    PlantBookSO _plantBook;
    DiscoveryBookSO _discoveryBook;
    ResearchBookSO _researchBook;
    CreatureBookSO _creatureBook;

    const string 재양치슬러그 = "ash_fern";
    const string 불씨버섯슬러그 = "ember_fungus";

    // ── 만들기 ───────────────────────────────────────────────────

    /// <summary>
    /// <see cref="PlantNodeSO"/>에는 id 필드가 없다. 열쇠는 <b>에셋 이름</b>에서
    /// 만들어지므로 가짜에도 이름을 준다 — 이름이 없으면 열쇠가 null이 되어
    /// 목록에서 조용히 빠진다.
    /// </summary>
    static PlantNodeSO 식물(string assetName, string displayName,
                            int stages = 2, float grow = 40f,
                            float harvest = 1.2f, float wither = 90f)
    {
        var p = ScriptableObject.CreateInstance<PlantNodeSO>();
        p.name = assetName;
        p.displayName = displayName;
        p.maxStage = stages;
        p.growSeconds = grow;
        p.harvestSeconds = harvest;
        p.witherSeconds = wither;
        return p;
    }

    static ItemDataSO 아이템(string id, string name)
    {
        var it = ScriptableObject.CreateInstance<ItemDataSO>();
        it.id = id;
        it.displayName = name;
        return it;
    }

    static DiscoverySO 발견(string id, ItemDataSO item, string text)
    {
        var d = ScriptableObject.CreateInstance<DiscoverySO>();
        d.id = id;
        d.item = item;
        d.unlocks = new BlueprintSO[0];
        d.line = new SequenceSO.Line { speaker = "우주복 AI", text = text };
        return d;
    }

    static CreatureDefinitionSO 생물(string id, string name)
    {
        var c = ScriptableObject.CreateInstance<CreatureDefinitionSO>();
        c.id = id;
        c.displayName = name;
        c.maxHealth = 30f;
        c.moveSpeed = 2f;
        c.detectRadius = 8f;
        c.attackDamage = 4f;
        return c;
    }

    [SetUp]
    public void SetUp()
    {
        // CodexCatalogTests와 같은 이유로 손으로 지은 구역만 싣는다 —
        // 데이터 에셋 구역이 실리면 가짜 슬러그가 진짜 문장을 끌어온다.
        Loc.Load(손으로_지은_구역만());
        Loc.SetLocale(StringCatalog.DefaultLocale);

        _ledger = new UnlockLedger();

        _plantBook = ScriptableObject.CreateInstance<PlantBookSO>();
        _plantBook.plants = new[]
        {
            식물(재양치슬러그, "재양치", stages: 2, grow: 40f, harvest: 1.2f, wither: 90f),
            식물(불씨버섯슬러그, "불씨버섯", stages: 2, grow: 55f, harvest: 1.4f, wither: 90f),
        };

        var scrap = 아이템("scrap", "스크랩");
        _discoveryBook = ScriptableObject.CreateInstance<DiscoveryBookSO>();
        _discoveryBook.discoveries = new[] { 발견("disc_scrap", scrap, "외계 금속물체 분석...") };

        _researchBook = ScriptableObject.CreateInstance<ResearchBookSO>();
        _researchBook.entries = new ResearchEntrySO[0];

        _creatureBook = ScriptableObject.CreateInstance<CreatureBookSO>();
        _creatureBook.creatures = new[] { 생물("scythe", "낫") };
    }

    [TearDown]
    public void TearDown()
    {
        Loc.Load(LocalizationTestBootstrap.LoadCatalogFromDisk());
        Loc.SetLocale(StringCatalog.DefaultLocale);
    }

    static StringCatalog 손으로_지은_구역만()
    {
        string text = System.IO.File.ReadAllText(LocalizationTestBootstrap.CsvPath);
        int cut = text.IndexOf(DataTextDraft.Marker, System.StringComparison.Ordinal);

        Assert.Greater(cut, 0, "표에서 데이터 에셋 구역의 경계선을 못 찾았다");
        return StringCatalog.Parse(text.Substring(0, cut));
    }

    // ── 갈래가 새지 않는가 ────────────────────────────────────────

    [Test]
    public void 식물만_식물_탭에_들어간다()
    {
        CodexCatalog.BuildPlants(_plantBook, _ledger, _rows);

        Assert.AreEqual(2, _rows.Count);
        foreach (var e in _rows)
        {
            Assert.AreEqual(CodexSection.Plant, e.Section, $"{e.Key}의 갈래가 식물이 아니다");
            StringAssert.StartsWith(CodexCatalog.PlantKeyPrefix, e.Key);
        }
        CollectionAssert.AreEqual(
            new[] { "plant_" + 재양치슬러그, "plant_" + 불씨버섯슬러그 },
            _rows.Select(e => e.Key).ToArray());
    }

    [Test]
    public void 식물이_아닌_것은_새어_들어오지_않는다()
    {
        // 목록에 아무리 넣어도 PlantBookSO는 PlantNodeSO만 담는다 —
        // 갈래는 형(type)이 정하지 이름표가 정하는 것이 아니다.
        // 여기서는 그 형이 실제로 다른 갈래의 열쇠를 만들지 않는지 본다.
        CodexCatalog.BuildPlants(_plantBook, _ledger, _rows);

        var 생물열쇠 = _creatureBook.creatures.Select(CodexCatalog.CreatureKey).ToHashSet();
        var 발견열쇠 = _discoveryBook.discoveries.Select(FieldDiscovery.KeyOf).ToHashSet();

        foreach (var e in _rows)
        {
            Assert.IsFalse(생물열쇠.Contains(e.Key), $"생물 열쇠가 식물 탭에 있다: {e.Key}");
            Assert.IsFalse(발견열쇠.Contains(e.Key), $"발견 열쇠가 식물 탭에 있다: {e.Key}");
            Assert.IsFalse(e.Key.StartsWith(CodexCatalog.CreatureKeyPrefix),
                           $"생물 접두를 쓰고 있다: {e.Key}");
        }
    }

    [Test]
    public void 식물은_일반_탭에_겹쳐_나오지_않는다()
    {
        // 전부 열어 둔다 — 잠긴 채로는 이름이 ???라서 겹침을 못 본다.
        foreach (var p in _plantBook.plants) _ledger.Unlock(CodexCatalog.PlantKey(p));
        _ledger.Unlock(FieldDiscovery.KeyOf(_discoveryBook.discoveries[0]));
        _ledger.Unlock(CodexCatalog.CreatureKey(_creatureBook.creatures[0]));

        var 식물줄 = new List<CodexEntry>();
        CodexCatalog.BuildPlants(_plantBook, _ledger, 식물줄);
        var 식물열쇠 = 식물줄.Select(e => e.Key).ToHashSet();
        var 식물이름 = 식물줄.Select(e => e.Title).ToHashSet();

        var 나머지 = new List<CodexEntry>();
        void 훑는다(CodexSection 갈래)
        {
            var 담을곳 = new List<CodexEntry>();
            switch (갈래)
            {
                case CodexSection.Discovery:
                    CodexCatalog.BuildDiscoveries(_discoveryBook, _ledger, 담을곳); break;
                case CodexSection.Blueprint:
                    CodexCatalog.BuildBlueprints(_discoveryBook, _researchBook, _ledger, 담을곳); break;
                case CodexSection.Research:
                    CodexCatalog.BuildResearch(_researchBook, _ledger, 담을곳); break;
                case CodexSection.Creature:
                    CodexCatalog.BuildCreatures(_creatureBook, _ledger, 담을곳); break;
            }
            나머지.AddRange(담을곳);
        }
        훑는다(CodexSection.Discovery);
        훑는다(CodexSection.Blueprint);
        훑는다(CodexSection.Research);
        훑는다(CodexSection.Creature);

        foreach (var e in 나머지)
        {
            Assert.IsFalse(식물열쇠.Contains(e.Key), $"식물 열쇠가 {e.Section} 탭에도 있다: {e.Key}");
            Assert.IsFalse(식물이름.Contains(e.Title), $"식물 이름이 {e.Section} 탭에도 있다: {e.Title}");
            Assert.AreNotEqual(CodexSection.Plant, e.Section);
        }
    }

    // ── 경계값 ────────────────────────────────────────────────────

    [Test]
    public void 목록이_없으면_담을_그릇을_비워_돌려준다()
    {
        _rows.Add(new CodexEntry { Key = "찌꺼기" });
        CodexCatalog.BuildPlants(null, _ledger, _rows);
        Assert.IsEmpty(_rows, "앞 갈래의 줄이 남아 있으면 탭을 바꿨을 때 두 벌로 겹친다");

        _rows.Add(new CodexEntry { Key = "찌꺼기" });
        var 빈책 = ScriptableObject.CreateInstance<PlantBookSO>();
        빈책.plants = null;
        CodexCatalog.BuildPlants(빈책, _ledger, _rows);
        Assert.IsEmpty(_rows);
    }

    [Test]
    public void 담을_그릇이_없으면_아무_일도_하지_않는다()
    {
        Assert.DoesNotThrow(() => CodexCatalog.BuildPlants(_plantBook, _ledger, null));
    }

    [Test]
    public void 원장이_없으면_아무것도_열리지_않는다()
    {
        CodexCatalog.BuildPlants(_plantBook, null, _rows);
        Assert.AreEqual(2, _rows.Count);
        foreach (var e in _rows)
        {
            Assert.IsFalse(e.Unlocked);
            Assert.AreEqual(CodexCatalog.UnknownTitle, e.Title);
        }
    }

    [Test]
    public void 이름_없는_식물은_열쇠가_서지_않아_실리지_않는다()
    {
        var 이름없음 = ScriptableObject.CreateInstance<PlantNodeSO>();
        이름없음.name = "";
        var 책 = ScriptableObject.CreateInstance<PlantBookSO>();
        책.plants = new[] { 이름없음, null, _plantBook.plants[0] };

        CodexCatalog.BuildPlants(책, _ledger, _rows);

        Assert.AreEqual(1, _rows.Count, "열쇠가 없는 줄과 빈 칸이 목록에 들어왔다");
        Assert.AreEqual("plant_" + 재양치슬러그, _rows[0].Key);
    }

    [Test]
    public void 열쇠는_접두에_에셋_이름_슬러그를_붙인_것이다()
    {
        Assert.AreEqual("plant_ash_fern", CodexCatalog.PlantKey(_plantBook.plants[0]));
        Assert.IsNull(CodexCatalog.PlantKey((PlantNodeSO)null));
        Assert.IsNull(CodexCatalog.PlantKey(""));
        Assert.IsNull(CodexCatalog.PlantKey("   "));
    }

    // ── 무엇이 적히는가 ───────────────────────────────────────────

    [Test]
    public void 못_본_식물은_실루엣으로만_남는다()
    {
        CodexCatalog.BuildPlants(_plantBook, _ledger, _rows);

        Assert.AreEqual(CodexCatalog.UnknownTitle, _rows[0].Title, "이름을 주면 아는 것이 된다");
        Assert.AreEqual(CodexCatalog.UnknownPlantBody, _rows[0].Body);
        Assert.IsFalse(_rows[0].Body.Contains("재양치"), "잠긴 줄이 이름을 흘렸다");
    }

    [Test]
    public void 따_본_식물은_잰_값을_보여_준다()
    {
        _ledger.Unlock(CodexCatalog.PlantKey(_plantBook.plants[0]));
        CodexCatalog.BuildPlants(_plantBook, _ledger, _rows);

        Assert.IsTrue(_rows[0].Unlocked);
        Assert.AreEqual("재양치", _rows[0].Title);

        var body = _rows[0].Body;
        StringAssert.Contains("40", body, "자라는 데 걸리는 시간이 없다");
        StringAssert.Contains("1.2", body, "따는 데 걸리는 시간이 없다");
        StringAssert.Contains("90", body, "시드는 시간이 없다");
        Assert.AreEqual(3, body.Split('\n').Length, "줄 수가 다르다");
    }

    [Test]
    public void 시들지_않는_식물에는_시든다고_적지_않는다()
    {
        var 안시듦 = 식물("stone_moss", "돌이끼", wither: 0f);
        var 책 = ScriptableObject.CreateInstance<PlantBookSO>();
        책.plants = new[] { 안시듦 };
        _ledger.Unlock(CodexCatalog.PlantKey(안시듦));

        CodexCatalog.BuildPlants(책, _ledger, _rows);
        Assert.AreEqual(2, _rows[0].Body.Split('\n').Length, "거짓말 한 줄이 붙었다");
    }

    [Test]
    public void 식물_기록도_로케일을_따라온다()
    {
        _ledger.Unlock(CodexCatalog.PlantKey(_plantBook.plants[0]));

        string 갈래_ko = CodexCatalog.SectionTitle(CodexSection.Plant);
        string 안내_ko = CodexCatalog.UnknownPlantBody;
        string 기록_ko = CodexCatalog.DescribePlant(_plantBook.plants[0]);

        try
        {
            Loc.SetLocale("en");
            Assert.AreNotEqual(갈래_ko, CodexCatalog.SectionTitle(CodexSection.Plant),
                               "갈래 이름이 const로 박혀 있다");
            Assert.AreNotEqual(안내_ko, CodexCatalog.UnknownPlantBody);

            string 기록_en = CodexCatalog.DescribePlant(_plantBook.plants[0]);
            Assert.AreNotEqual(기록_ko, 기록_en);
            StringAssert.Contains("40", 기록_en, "수치는 로케일이 바뀌어도 그대로여야 한다");
            Assert.AreEqual(기록_ko.Split('\n').Length, 기록_en.Split('\n').Length,
                            "줄바꿈은 배치라서 언어에 따라 달라지면 안 된다");
        }
        finally { Loc.SetLocale(StringCatalog.DefaultLocale); }
    }

    [Test]
    public void 화면에_나가는_식물_문구에_글꼴이_모르는_줄표가_없다()
    {
        var 금지 = new[] { '—', '–', '−' };
        var 문구 = new List<string>
        {
            CodexCatalog.SectionTitle(CodexSection.Plant),
            CodexCatalog.UnknownPlantBody,
            CodexCatalog.DescribePlant(_plantBook.plants[0]),
            MenuListing.NothingSeenAmongPlants,
        };
        foreach (var line in 문구)
            foreach (var bad in 금지)
                Assert.IsFalse(line.IndexOf(bad) >= 0,
                    $"\"{line}\"에 글꼴이 모르는 문자 U+{(int)bad:X4}가 있다");
    }

    // ── 빈 자리 ───────────────────────────────────────────────────

    [Test]
    public void 식물_탭이_비면_아직_본_것이_없다고_적는다()
    {
        CodexCatalog.BuildPlants(null, _ledger, _rows);
        Assert.IsEmpty(_rows);

        var 문구 = MenuListing.CodexEmptyBody(CodexSection.Plant);
        Assert.AreEqual(MenuListing.NothingSeenAmongPlants, 문구);
        Assert.IsNotEmpty(문구);
        Assert.AreNotEqual("plant_empty", 문구, "표에 줄이 없어 키가 그대로 떴다");

        // 몇 개가 잠겨 있는지는 세지 않는다 — 감추기로 한 것을 옆문으로 흘리지 않는다.
        foreach (var c in 문구) Assert.IsFalse(char.IsDigit(c), $"빈 자리에 숫자가 있다: {문구}");
    }

    [Test]
    public void 다른_갈래의_빈_자리는_원래_문구_그대로다()
    {
        var 식물빈자리 = MenuListing.CodexEmptyBody(CodexSection.Plant);
        foreach (CodexSection s in System.Enum.GetValues(typeof(CodexSection)))
        {
            if (s == CodexSection.Plant) continue;
            Assert.AreNotEqual(식물빈자리, MenuListing.CodexEmptyBody(s),
                               $"{s}의 빈 자리까지 식물 문구로 바뀌었다");
        }
    }

    [Test]
    public void 빈_자리_문구도_로케일을_따라온다()
    {
        string ko = MenuListing.NothingSeenAmongPlants;
        try
        {
            Loc.SetLocale("en");
            Assert.AreNotEqual(ko, MenuListing.NothingSeenAmongPlants);
        }
        finally { Loc.SetLocale(StringCatalog.DefaultLocale); }
    }

    // ── 예외가 본문 행세를 하지 않는가 ─────────────────────────────

    [Test]
    public void 도감을_열면_여전히_물질_분석이_먼저다()
    {
        Assert.AreEqual(CodexSection.Discovery, CodexCatalog.DefaultSection,
                        "식물은 「전부 미분류, 단 식물만 따로」의 예외다. 예외가 첫 화면이 되면 안 된다");
    }

    [Test]
    public void 식물은_갈래_목록의_맨_끝이다()
    {
        var 값들 = System.Enum.GetValues(typeof(CodexSection)).Cast<CodexSection>().ToArray();
        Assert.AreEqual(CodexSection.Plant, 값들.Last(), "예외는 본문 뒤에 선다");
        Assert.AreEqual(4, (int)CodexSection.Plant, "앞의 값들을 밀면 저장된 원장과 어긋난다");
    }
}
