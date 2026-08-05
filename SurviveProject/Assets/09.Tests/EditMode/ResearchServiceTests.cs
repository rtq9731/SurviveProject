using NUnit.Framework;
using UnityEngine;
using Survive.Items;
using Survive.Progression;

/// <summary>
/// 연구의 생애 규칙 — 걸고, 들여다보고, 물린다.
///
/// 여기서 지키는 것: 모자란 것이 무엇인지 <b>구별해서</b> 말하는 것(소재가 없는 것과
/// 스크랩이 없는 것은 다른 사정이다), 거는 순간 대가가 빠지는 것, 실패하면 아무것도
/// 건드리지 않는 것, 다 보면 원장에 적히는 것, 그리고 <b>끝나지 않은 것을 물리면
/// 전부 돌아오는 것</b> — 제작대의 취소와 규칙이 같아야 한다.
/// </summary>
public class ResearchServiceTests
{
    ItemDataSO _scrap;
    ItemDataSO _relic;
    BlueprintSO _blueprint;
    ResearchEntrySO _entry;
    ResearchQueue _queue;
    UnlockLedger _ledger;
    Inventory _inv;

    static ItemDataSO 아이템(string id, int stack = 50)
    {
        var it = ScriptableObject.CreateInstance<ItemDataSO>();
        it.id = id;
        it.displayName = id;
        it.maxStack = stack;
        return it;
    }

    static BlueprintSO 청사진(string id)
    {
        var bp = ScriptableObject.CreateInstance<BlueprintSO>();
        bp.id = id;
        bp.displayName = id;
        return bp;
    }

    [SetUp]
    public void SetUp()
    {
        _scrap = 아이템("scrap");
        _relic = 아이템("relic_membrane", 10);
        _blueprint = 청사진("bp_surface_walker");

        _entry = ScriptableObject.CreateInstance<ResearchEntrySO>();
        _entry.id = "res_surface_walker";
        _entry.displayName = "젖지 않는 막 분석";
        _entry.materials = new[] { new ItemStack(_relic, 1) };
        _entry.energyCost = 15;
        _entry.researchSeconds = 45f;
        _entry.unlocks = new[] { _blueprint };

        _queue = new ResearchQueue();
        _ledger = new UnlockLedger();
        _inv = new Inventory(12);
    }

    void 채운다(int relics = 1, int scrap = 15)
    {
        if (relics > 0) _inv.TryAdd(_relic, relics);
        if (scrap > 0) _inv.TryAdd(_scrap, scrap);
    }

    ResearchReadiness 판정() =>
        ResearchService.Evaluate(_entry, _inv, _ledger, _queue, _scrap);

    bool 건다() => ResearchService.TryEnqueue(_queue, _entry, _inv, _ledger, _scrap);

    // ── 판정 ────────────────────────────────────────────────

    [Test]
    public void 소재와_스크랩이_다_있으면_걸_수_있다()
    {
        채운다();
        Assert.AreEqual(ResearchReadiness.Ready, 판정());
    }

    [Test]
    public void 소재가_없으면_소재_부족이라고_말한다()
    {
        채운다(relics: 0);
        Assert.AreEqual(ResearchReadiness.MissingMaterial, 판정());
    }

    [Test]
    public void 스크랩이_모자라면_에너지_부족이라고_말한다()
    {
        채운다(scrap: 14);
        Assert.AreEqual(ResearchReadiness.MissingEnergy, 판정(),
            "소재는 있는데 태울 것이 없다 — 소재 부족과 다른 사정이다");
    }

    [Test]
    public void 소재가_먼저_모자라면_스크랩보다_소재를_먼저_말한다()
    {
        // 둘 다 없을 때 무엇을 먼저 말하는가를 못 박아 둔다. 안내문이 흔들리면
        // 사용자는 무엇을 구하러 가야 할지 모른다.
        Assert.AreEqual(ResearchReadiness.MissingMaterial, 판정());
    }

    [Test]
    public void 이미_다_알고_있으면_다시_걸_수_없다()
    {
        채운다();
        _ledger.Unlock(_blueprint.id);
        Assert.AreEqual(ResearchReadiness.AlreadyKnown, 판정());
        Assert.IsFalse(건다(), "두 번 알아낼 것은 없다");
    }

    [Test]
    public void 여는_것이_하나도_없는_항목은_헛_항목이다()
    {
        채운다();
        _entry.unlocks = new BlueprintSO[0];
        Assert.AreEqual(ResearchReadiness.Invalid, 판정());
    }

    [Test]
    public void 태울_것을_지정하지_않으면_걸_수_없다()
    {
        채운다();
        Assert.AreEqual(ResearchReadiness.Invalid,
            ResearchService.Evaluate(_entry, _inv, _ledger, _queue, null),
            "환급할 수 없는 것을 태우게 두면 취소가 도둑질이 된다");
    }

    [Test]
    public void 같은_항목을_두_번_걸_수_없다()
    {
        채운다(relics: 2, scrap: 40);
        Assert.IsTrue(건다());
        Assert.AreEqual(ResearchReadiness.Queued, 판정());
        Assert.IsFalse(건다());
    }

    [Test]
    public void 줄이_가득_차면_더_걸_수_없다()
    {
        var 항목들 = new ResearchEntrySO[ResearchQueue.DefaultCapacity];
        for (int i = 0; i < 항목들.Length; i++)
        {
            항목들[i] = ScriptableObject.CreateInstance<ResearchEntrySO>();
            항목들[i].id = "res_" + i;
            항목들[i].materials = new ItemStack[0];
            항목들[i].energyCost = 0;
            항목들[i].researchSeconds = 10f;
            항목들[i].unlocks = new[] { 청사진("bp_" + i) };
            Assert.IsTrue(ResearchService.TryEnqueue(_queue, 항목들[i], _inv, _ledger, _scrap));
        }

        채운다();
        Assert.IsTrue(_queue.IsFull);
        Assert.AreEqual(ResearchReadiness.QueueFull, 판정());
    }

    [Test]
    public void 거절_사유마다_다른_문구가_나온다()
    {
        foreach (ResearchReadiness r in System.Enum.GetValues(typeof(ResearchReadiness)))
            Assert.IsNotEmpty(ResearchService.Describe(r), $"{r}에 안내문이 없다");
    }

    // ── 걸기 ────────────────────────────────────────────────

    [Test]
    public void 걸면_소재와_스크랩이_그_자리에서_빠진다()
    {
        채운다(relics: 3, scrap: 40);
        Assert.IsTrue(건다());

        Assert.AreEqual(2, _inv.CountOf("relic_membrane"), "소재 하나가 들어갔다");
        Assert.AreEqual(25, _inv.CountOf("scrap"), "스크랩 15가 탔다");
        Assert.AreEqual(1, _queue.Count);
    }

    [Test]
    public void 실패한_걸기는_아무것도_건드리지_않는다()
    {
        채운다(relics: 1, scrap: 14);
        Assert.IsFalse(건다());

        Assert.AreEqual(1, _inv.CountOf("relic_membrane"), "소재가 그대로다");
        Assert.AreEqual(14, _inv.CountOf("scrap"), "스크랩도 그대로다");
        Assert.IsTrue(_queue.IsEmpty);
    }

    [Test]
    public void 건다고_바로_알게_되지는_않는다()
    {
        채운다();
        건다();
        Assert.IsFalse(_ledger.IsUnlocked(_blueprint.id), "시간을 들이지 않고 열리면 안 된다");
    }

    // ── 진행 ────────────────────────────────────────────────

    [Test]
    public void 시간이_흐르면_진행도가_찬다()
    {
        채운다();
        건다();

        Assert.IsNull(ResearchService.Tick(_queue, 22.5f, _ledger));
        Assert.AreEqual(0.5f, _queue.Active.Progress, 0.01f);
        Assert.AreEqual(22.5f, _queue.Active.SecondsLeft, 0.01f);
    }

    [Test]
    public void 다_보면_원장에_적히고_줄에서_빠진다()
    {
        채운다();
        건다();

        var done = ResearchService.Tick(_queue, 45f, _ledger);

        Assert.AreSame(_entry, done, "끝난 항목을 돌려준다 — 화면과 AI가 이것을 읽는다");
        Assert.IsTrue(_ledger.IsUnlocked(_blueprint.id), "알아낸 것이 원장에 남는다");
        Assert.IsTrue(_queue.IsEmpty);
    }

    [Test]
    public void 진행할_수_없으면_시간이_흘러도_그대로다()
    {
        채운다();
        건다();

        Assert.IsNull(ResearchService.Tick(_queue, 45f, _ledger, powered: false));
        Assert.AreEqual(0f, _queue.Active.Elapsed);
        Assert.IsFalse(_ledger.IsUnlocked(_blueprint.id));
    }

    [Test]
    public void 맨_앞_하나만_진행한다()
    {
        채운다(relics: 1, scrap: 40);
        건다();

        var 둘째 = ScriptableObject.CreateInstance<ResearchEntrySO>();
        둘째.id = "res_second";
        둘째.materials = new ItemStack[0];
        둘째.energyCost = 5;
        둘째.researchSeconds = 10f;
        둘째.unlocks = new[] { 청사진("bp_second") };
        Assert.IsTrue(ResearchService.TryEnqueue(_queue, 둘째, _inv, _ledger, _scrap));

        ResearchService.Tick(_queue, 10f, _ledger);

        Assert.AreEqual(2, _queue.Count, "뒤에 선 것은 아직 시작도 안 했다");
        Assert.AreEqual(0f, _queue.At(1).Elapsed);
        Assert.IsFalse(_ledger.IsUnlocked("bp_second"));
    }

    [Test]
    public void 남은_시간은_줄_전체를_합한_것이다()
    {
        채운다(relics: 1, scrap: 40);
        건다();

        var 둘째 = ScriptableObject.CreateInstance<ResearchEntrySO>();
        둘째.id = "res_second";
        둘째.materials = new ItemStack[0];
        둘째.energyCost = 0;
        둘째.researchSeconds = 20f;
        둘째.unlocks = new[] { 청사진("bp_second") };
        ResearchService.TryEnqueue(_queue, 둘째, _inv, _ledger, _scrap);

        Assert.AreEqual(65f, ResearchService.TotalSecondsLeft(_queue), 0.01f);
    }

    // ── 취소 ────────────────────────────────────────────────

    [Test]
    public void 물리면_소재와_스크랩이_전부_돌아온다()
    {
        채운다(relics: 2, scrap: 40);
        건다();
        ResearchService.Tick(_queue, 20f, _ledger);   // 절반쯤 봤다

        Assert.IsTrue(ResearchService.TryCancel(_queue, 0, _inv, _scrap));

        Assert.AreEqual(2, _inv.CountOf("relic_membrane"), "반쯤 본 것도 전부 돌려준다");
        Assert.AreEqual(40, _inv.CountOf("scrap"));
        Assert.IsTrue(_queue.IsEmpty);
        Assert.IsFalse(_ledger.IsUnlocked(_blueprint.id), "물렸으니 알아낸 것도 없다");
    }

    [Test]
    public void 끝난_것은_물릴_수_없다()
    {
        채운다();
        건다();
        ResearchService.Tick(_queue, 45f, _ledger);

        Assert.IsFalse(ResearchService.TryCancel(_queue, 0, _inv, _scrap),
            "이미 끝나 줄에서 빠진 것을 물려 환급받을 수는 없다");
        Assert.AreEqual(0, _inv.CountOf("relic_membrane"));
    }

    [Test]
    public void 줄_전체를_물릴_수_있다()
    {
        채운다(relics: 1, scrap: 40);
        건다();

        var 둘째 = ScriptableObject.CreateInstance<ResearchEntrySO>();
        둘째.id = "res_second";
        둘째.materials = new ItemStack[0];
        둘째.energyCost = 5;
        둘째.researchSeconds = 10f;
        둘째.unlocks = new[] { 청사진("bp_second") };
        ResearchService.TryEnqueue(_queue, 둘째, _inv, _ledger, _scrap);

        Assert.AreEqual(2, ResearchService.CancelAll(_queue, _inv, _scrap));
        Assert.IsTrue(_queue.IsEmpty);
        Assert.AreEqual(1, _inv.CountOf("relic_membrane"));
        Assert.AreEqual(40, _inv.CountOf("scrap"));
    }

    // ── 줄 다루기 ───────────────────────────────────────────

    [Test]
    public void 줄이_바뀔_때마다_알린다()
    {
        int 들은횟수 = 0;
        _queue.Changed += () => 들은횟수++;

        채운다();
        건다();
        Assert.AreEqual(1, 들은횟수, "걸었다");

        ResearchService.Tick(_queue, 45f, _ledger);
        Assert.AreEqual(2, 들은횟수, "끝났다");
    }

    [Test]
    public void 몇_번째에_서_있는지_알_수_있다()
    {
        채운다(relics: 1, scrap: 40);

        var 둘째 = ScriptableObject.CreateInstance<ResearchEntrySO>();
        둘째.id = "res_second";
        둘째.materials = new ItemStack[0];
        둘째.energyCost = 0;
        둘째.researchSeconds = 10f;
        둘째.unlocks = new[] { 청사진("bp_second") };

        Assert.AreEqual(-1, _queue.IndexOf(_entry), "걸지 않은 것은 줄에 없다");

        건다();
        ResearchService.TryEnqueue(_queue, 둘째, _inv, _ledger, _scrap);

        Assert.AreEqual(0, _queue.IndexOf(_entry));
        Assert.AreEqual(1, _queue.IndexOf(둘째));
    }

    [Test]
    public void 태울_것을_지정하지_않으면_스크랩을_찾는다()
    {
        Assert.AreEqual("scrap", ResearchService.EnergyIdOf(null));
        Assert.AreEqual("scrap", ResearchService.EnergyIdOf(_scrap));
    }

    [Test]
    public void 시간이_0인_항목은_한_번의_흐름으로_끝난다()
    {
        _entry.researchSeconds = 0f;
        채운다();
        건다();

        Assert.AreSame(_entry, ResearchService.Tick(_queue, 0.016f, _ledger));
        Assert.IsTrue(_ledger.IsUnlocked(_blueprint.id));
    }
}
