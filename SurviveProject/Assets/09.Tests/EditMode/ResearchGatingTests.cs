using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Survive.Items;
using Survive.Progression;
using Survive.UI;

/// <summary>
/// 가져 본 적 없는 재료의 연구 항목은 띄우지 않는다.
///
/// 감추기 전에는 연구대 앞에 서는 순간 일곱 줄이 펼쳐졌다 —
/// "공 개체 분석 · 공의 껍질 0/3", "삼켜지지 않는 핵 분석 · 낫의 핵 0/1"...
/// 생물 다섯 종의 존재와 한 번도 본 적 없는 부위 일곱 종의 이름이 통째로 새어 나갔다.
/// 제작 목록에서 지운 정보가 옆문으로 돌아온 셈이다(<c>MenuListingTests</c>).
///
/// 여기서 지키는 것은 여섯이다.
/// ① 가져 본 적 없는 재료의 항목은 목록에 없다 ② 하나라도 가지면 나타난다
/// ③ 다 써 버려도 남아 있다 ④ 전부 못 가져 봤을 때 무너지지 않는다
/// ⑤ 저장 왕복 뒤에도 기록이 유지된다
/// ⑥ <b>실제 에셋</b>의 재료 이름·항목 이름이 초기 화면 문자열에 하나도 없다
///
/// ⑥은 지어낸 데이터가 아니라 실제 <c>ResearchBook.asset</c>을 열어서 본다.
/// 코드가 맞아도 새 연구 항목 하나가 이름을 흘리면 게임 안에서는 다시 새기 때문이다.
/// </summary>
public class ResearchGatingTests
{
    const string BookPath = "Assets/08.Data/Progression/Resources/ResearchBook.asset";

    UnlockLedger _ledger;

    [SetUp]
    public void SetUp() => _ledger = new UnlockLedger();

    // ── ① 가져 본 적 없는 것은 실리지 않는다 ─────────────────

    [Test]
    public void 가져_본_적_없는_재료의_항목은_목록에_실리지_않는다()
    {
        var e = 항목("res_submersible", 소재("relic_core", "낫의 핵", 1));
        Assert.IsFalse(MenuListing.ShouldList(e, _ledger),
            "있는지도 모르는 물체를 가져오라고 할 수는 없다");
    }

    [Test]
    public void 소재를_여럿_요구해도_하나도_못_겪었으면_실리지_않는다()
    {
        var e = 항목("res_two", 소재("part_ball", "공의 껍질", 3), 소재("part_eye", "눈의 렌즈", 3));
        Assert.IsFalse(MenuListing.ShouldList(e, _ledger));
    }

    [Test]
    public void 빈_항목은_실리지_않는다()
    {
        Assert.IsFalse(MenuListing.ShouldList((ResearchEntrySO)null, _ledger));
    }

    [Test]
    public void 다른_것을_가져_봤다고_옆_항목까지_열리지는_않는다()
    {
        var 핵 = 항목("res_submersible", 소재("relic_core", "낫의 핵", 1));
        var 막 = 항목("res_surface_walker", 소재("relic_membrane", "낫의 막", 1));

        HeldRecord.Record(_ledger, "relic_core");

        Assert.IsTrue(MenuListing.ShouldList(핵, _ledger));
        Assert.IsFalse(MenuListing.ShouldList(막, _ledger),
            "하나를 겪었다고 옆것까지 새면 감춘 보람이 없다");
    }

    // ── ② 하나라도 가지면 나타난다 ───────────────────────────

    [Test]
    public void 하나라도_가지면_그_자리에서_목록에_나타난다()
    {
        var e = 항목("res_submersible", 소재("relic_core", "낫의 핵", 1));
        Assert.IsFalse(MenuListing.ShouldList(e, _ledger));

        HeldRecord.Record(_ledger, "relic_core");

        Assert.IsTrue(MenuListing.ShouldList(e, _ledger),
            "재료를 손에 넣었는데 창을 다시 열어야 보이면 실패다");
    }

    [Test]
    public void 소재_둘_중_하나만_겪어도_실린다()
    {
        var e = 항목("res_two", 소재("part_ball", "공의 껍질", 3), 소재("part_eye", "눈의 렌즈", 3));
        HeldRecord.Record(_ledger, "part_eye");

        Assert.IsTrue(MenuListing.ShouldList(e, _ledger),
            "하나를 쥔 순간 그 항목의 존재는 이미 알려졌다");
    }

    [Test]
    public void 목록은_겪은_것을_따라_자란다()
    {
        var 책 = new[]
        {
            항목("res_codex_ball",  소재("part_ball",  "공의 껍질", 3)),
            항목("res_codex_eye",   소재("part_eye",   "눈의 렌즈", 3)),
            항목("res_submersible", 소재("relic_core", "낫의 핵",   1)),
        };

        Assert.AreEqual(0, 실린수(책), "처음에는 아무것도 없다");

        HeldRecord.Record(_ledger, "part_eye");
        Assert.AreEqual(1, 실린수(책));

        HeldRecord.Record(_ledger, "relic_core");
        Assert.AreEqual(2, 실린수(책));
    }

    // ── ③ 다 써 버려도 남아 있다 ─────────────────────────────

    [Test]
    public void 다_써_버려도_기록은_남는다()
    {
        var inv = new Inventory(8);
        var 핵 = 아이템("relic_core", "낫의 핵");
        var e = 항목("res_submersible", 소재(핵, 1));

        inv.TryAdd(핵, 1);
        HeldRecord.RecordAll(_ledger, inv);
        Assert.IsTrue(MenuListing.ShouldList(e, _ledger));

        inv.TryRemove("relic_core", 1);
        Assert.AreEqual(0, inv.CountOf("relic_core"), "손에서는 비었다");

        Assert.IsTrue(MenuListing.ShouldList(e, _ledger),
            "다 써 버렸다고 항목이 사라지면 무엇을 하려던 것인지조차 잊는다");
    }

    [Test]
    public void 이미_밝혀낸_항목은_소재를_겪은_기록이_없어도_보인다()
    {
        var bp = 청사진("bp_submersible");
        var e = 항목("res_submersible", 소재("relic_core", "낫의 핵", 1));
        e.unlocks = new[] { bp };
        e.unlockKeys = new string[0];   // 이 항목이 여는 것은 청사진 하나뿐이다

        Assert.IsFalse(MenuListing.ShouldList(e, _ledger));

        _ledger.Unlock("bp_submersible");

        Assert.IsTrue(MenuListing.ShouldList(e, _ledger),
            "아는 것을 감출 이유는 없다 — 도감의 답과 연구대의 답이 어긋나면 안 된다");
        Assert.AreEqual(ResearchReadiness.AlreadyKnown,
                        ResearchService.Evaluate(e, new Inventory(4), _ledger, null, 아이템("scrap", "스크랩")));
    }

    // ── ④ 전부 못 가져 봤어도 무너지지 않는다 ────────────────

    [Test]
    public void 원장이_서기_전에는_감추지_않는다()
    {
        var e = 항목("res_submersible", 소재("relic_core", "낫의 핵", 1));
        Assert.IsTrue(MenuListing.ShouldList(e, null),
            "원장이 없을 때 감추면 판이 통째로 빈 목록이 된다 — 실패는 개방 쪽으로");
    }

    [Test]
    public void 요구_소재를_적지_않은_항목은_감출_것이_없어_실린다()
    {
        var e = 항목("res_free");
        Assert.IsTrue(MenuListing.ShouldList(e, _ledger),
            "감출 이름이 없는데 감추면 영영 뜨지 않는 유령 항목이 된다");
    }

    [Test]
    public void 빈_연구_목록_안내문은_몇_개가_남았는지_세어_주지_않는다()
    {
        var 문구 = MenuListing.NothingKnownToResearch;

        Assert.IsFalse(string.IsNullOrWhiteSpace(문구), "빈 목록에도 할 말은 있어야 한다");
        Assert.IsFalse(문구.Any(char.IsDigit),
            $"수를 세어 주면 앞으로 몇 종을 더 만나는지가 새어 나간다 — \"{문구}\"");
        StringAssert.DoesNotContain("잠", 문구);
        StringAssert.DoesNotContain("청사진", 문구);
        Assert.IsFalse(문구.Contains('—'), "본문 글꼴에 줄표가 없어 네모로 찍힌다");
    }

    // ── ⑤ 저장 왕복 ──────────────────────────────────────────

    [Test]
    public void 저장을_왕복해도_가져_본_기록이_남는다()
    {
        HeldRecord.Record(_ledger, "relic_core");

        var 다시 = new UnlockLedger();
        다시.Restore(_ledger.Capture());

        Assert.IsTrue(HeldRecord.Has(다시, "relic_core"), "세이브에 실려 나가고 돌아온다");
        Assert.IsTrue(MenuListing.ShouldList(항목("res_submersible", 소재("relic_core", "낫의 핵", 1)), 다시));
    }

    [Test]
    public void 소급_기록은_지금_들고_있는_것을_전부_찍는다()
    {
        var inv = new Inventory(8);
        inv.TryAdd(아이템("relic_core", "낫의 핵"), 1);
        inv.TryAdd(아이템("part_eye", "눈의 렌즈"), 2);

        // 이 기록이 생기기 전에 만든 저장본에는 열쇠가 하나도 없다. 불러온 직후
        // 손에 있는 것을 찍지 않으면 이미 핵을 들고 있는 사람의 항목이 사라진다.
        Assert.AreEqual(2, HeldRecord.RecordAll(_ledger, inv));
        Assert.AreEqual(0, HeldRecord.RecordAll(_ledger, inv), "두 번 찍어도 늘지 않는다");

        Assert.IsTrue(HeldRecord.Has(_ledger, "relic_core"));
        Assert.IsTrue(HeldRecord.Has(_ledger, "part_eye"));
    }

    [Test]
    public void 습득_기록_열쇠는_청사진_도감_열쇠와_겹치지_않는다()
    {
        // 원장은 하나뿐이다. 이름이 겹치면 물건을 주웠다고 청사진이 조용히 열린다.
        Assert.AreEqual("held/relic_core", HeldRecord.KeyFor("relic_core"));
        StringAssert.StartsWith(HeldRecord.Prefix, HeldRecord.KeyFor("bp_submersible"));

        HeldRecord.Record(_ledger, "bp_submersible");
        Assert.IsFalse(_ledger.IsUnlocked("bp_submersible"),
            "아이템 id가 청사진 id와 같아도 청사진이 열리지는 않는다");
        Assert.IsFalse(_ledger.IsUnlocked(CodexCatalog.CreatureKeyPrefix + "scythe"));
    }

    [Test]
    public void 비어_있는_것은_기록하지도_묻지도_않는다()
    {
        Assert.IsNull(HeldRecord.KeyFor((string)null));
        Assert.IsNull(HeldRecord.KeyFor("   "));
        Assert.IsFalse(HeldRecord.Record(_ledger, ""));
        Assert.IsFalse(HeldRecord.Record(null, "relic_core"));
        Assert.IsFalse(HeldRecord.Has(null, "relic_core"));
        Assert.IsFalse(HeldRecord.Has(_ledger, (ItemDataSO)null));
        Assert.AreEqual(0, HeldRecord.RecordAll(_ledger, null));
        Assert.AreEqual(0, _ledger.Count, "쓰레기 열쇠가 원장에 남지 않는다");
    }

    // ── ⑥ 실제 에셋 ──────────────────────────────────────────

    [Test]
    public void 실제_에셋에서_초기_연구_목록에는_아무것도_실리지_않는다()
    {
        var 실린것 = 연구목록().entries
            .Where(e => e != null && MenuListing.ShouldList(e, _ledger))
            .Select(e => e.id)
            .ToList();

        Assert.IsEmpty(실린것,
            "처음 연구대 앞에 선 사람은 어떤 부위도 가져 본 적이 없다 — " +
            "실린 것: " + string.Join(", ", 실린것));
    }

    [Test]
    public void 실제_에셋의_초기_연구_화면에_항목_이름도_재료_이름도_없다()
    {
        var 화면 = 연구_화면_문자열(_ledger, new Inventory(15));
        Assert.IsNotEmpty(화면, "화면에 아무것도 없으면 검사가 공회전한다");

        새어나가지_않는다(화면, 감출이름들());
    }

    [Test]
    public void 감출_이름이_실제로_에셋에_들어_있다()
    {
        // 이 검사가 지키는 것은 위 검사의 <b>의미</b>다. 이름이 전부 비어 있으면
        // "새지 않는다"는 단언이 아무것도 증명하지 않는 채로 초록불이 된다.
        var 이름 = 감출이름들();
        Assert.Greater(이름.Count, 5,
            "감출 이름이 없으면 위 검사가 공회전한다 — 실제 항목 7종과 재료 7종이 있어야 한다");
        Assert.IsTrue(이름.Contains("낫의 핵") || 이름.Any(n => n.Contains("낫")),
            "낫의 부위 이름이 실제로 에셋에 있다");
    }

    [Test]
    public void 실제_에셋에서_부위_하나를_쥐면_그_항목_하나만_늘어난다()
    {
        var book = 연구목록();
        var 핵항목 = book.entries.First(e => e != null && e.id == "res_submersible");
        var 핵 = 핵항목.materials[0].item;

        HeldRecord.Record(_ledger, 핵.id);

        var 실린것 = book.entries.Where(e => e != null && MenuListing.ShouldList(e, _ledger))
                                 .Select(e => e.id).ToList();
        CollectionAssert.AreEquivalent(new[] { "res_submersible" }, 실린것);

        // 다른 항목들의 이름은 여전히 화면 어디에도 없다.
        var 남은이름 = 감출이름들(제외: 핵항목);
        새어나가지_않는다(연구_화면_문자열(_ledger, new Inventory(15)), 남은이름);
    }

    [Test]
    public void 실제_에셋으로_그린_연구_줄에_줄표가_없다()
    {
        // 본문 글꼴(ChosunGu)에 U+2014가 없어 화면에는 두부(□)로 찍힌다.
        foreach (var e in 연구목록().entries)
        {
            if (e == null) continue;
            HeldRecord.Record(_ledger, e.materials?.FirstOrDefault()?.item?.id);
        }

        foreach (var s in 연구_화면_문자열(_ledger, new Inventory(15)))
            Assert.IsFalse(s.Contains('—'), $"줄표가 섞였다 — \"{s}\"");
    }

    // ── 도우미 ───────────────────────────────────────────────

    /// <summary>지금 원장으로 연구대 화면이 낼 수 있는 문자열 전부.</summary>
    static List<string> 연구_화면_문자열(UnlockLedger ledger, Inventory inv)
    {
        var book = 연구목록();
        var queue = new ResearchQueue();
        var text = new List<string> { MenuListing.ResearchHeaderLine("연구대", queue) };
        int listed = 0;

        foreach (var e in book.entries)
        {
            if (!MenuListing.ShouldList(e, ledger)) continue;
            listed++;

            text.Add(MenuListing.NameOf(e));
            foreach (ResearchReadiness state in System.Enum.GetValues(typeof(ResearchReadiness)))
                text.Add(MenuListing.ResearchLine(e, inv, book.energyItem, state));
        }

        if (listed == 0) text.Add(MenuListing.NothingKnownToResearch);
        return text;
    }

    /// <summary>어떤 문자열에도 감춰야 할 이름이 섞이지 않았다.</summary>
    static void 새어나가지_않는다(List<string> 화면, List<string> 이름들)
    {
        foreach (var name in 이름들)
        foreach (var s in 화면)
            StringAssert.DoesNotContain(name, s, $"\"{name}\"이(가) 화면 문자열에 섞였다 — \"{s}\"");
    }

    /// <summary>실제 에셋이 들고 있는 항목 이름과 재료 이름 전부.</summary>
    static List<string> 감출이름들(ResearchEntrySO 제외 = null)
    {
        var names = new HashSet<string>();

        foreach (var e in 연구목록().entries)
        {
            if (e == null || e == 제외) continue;

            if (!string.IsNullOrWhiteSpace(e.displayName)) names.Add(e.displayName.Trim());
            if (e.materials == null) continue;

            foreach (var need in e.materials)
            {
                var n = need?.item != null ? need.item.displayName : null;
                if (!string.IsNullOrWhiteSpace(n)) names.Add(n.Trim());
            }
        }
        return names.ToList();
    }

    static ResearchBookSO 연구목록()
    {
        var book = AssetDatabase.LoadAssetAtPath<ResearchBookSO>(BookPath);
        Assert.IsNotNull(book, $"{BookPath}를 못 읽었다");
        Assert.IsNotNull(book.entries, "연구 목록이 비어 있다");
        return book;
    }

    int 실린수(IEnumerable<ResearchEntrySO> 책) => 책.Count(e => MenuListing.ShouldList(e, _ledger));

    static ItemDataSO 아이템(string id, string name)
    {
        var item = ScriptableObject.CreateInstance<ItemDataSO>();
        item.id = id;
        item.displayName = name;
        item.maxStack = 99;
        return item;
    }

    static ItemStack 소재(string id, string name, int count) => 소재(아이템(id, name), count);

    static ItemStack 소재(ItemDataSO item, int count) => new ItemStack { item = item, count = count };

    static BlueprintSO 청사진(string id)
    {
        var bp = ScriptableObject.CreateInstance<BlueprintSO>();
        bp.id = id;
        bp.displayName = id + " 이름";
        bp.hint = id + " 힌트";
        return bp;
    }

    static ResearchEntrySO 항목(string id, params ItemStack[] materials)
    {
        var e = ScriptableObject.CreateInstance<ResearchEntrySO>();
        e.id = id;
        e.displayName = id + " 분석";
        e.materials = materials ?? new ItemStack[0];
        e.energyCost = 10;
        e.researchSeconds = 20f;
        e.unlockKeys = new[] { "codex_" + id };
        return e;
    }
}
