using NUnit.Framework;
using UnityEngine;
using Survive.Crafting;
using Survive.Items;

/// <summary>
/// 제작 대기열의 생애 — 걸고, 흐르고, 물린다.
///
/// "모든 제작에 시간이 걸린다"가 규칙이 된 이상, 재료가 언제 빠지고
/// 언제 돌아오는지가 곧 세계의 신뢰다. 여기가 그 계약서다.
/// </summary>
public class CraftQueueTests
{
    static ItemDataSO 아이템(string id, int maxStack = 99)
    {
        var it = ScriptableObject.CreateInstance<ItemDataSO>();
        it.id = id;
        it.displayName = id;
        it.maxStack = maxStack;
        return it;
    }

    static RecipeSO 레시피(ItemStack result, float seconds,
                        StationType station = StationType.None,
                        params ItemStack[] ing)
    {
        var r = ScriptableObject.CreateInstance<RecipeSO>();
        r.id = "test";
        r.result = result;
        r.ingredients = ing;
        r.craftSeconds = seconds;
        r.requiredStation = station;
        return r;
    }

    // ── 걸기 ────────────────────────────────────────────────

    [Test]
    public void 걸면_재료가_즉시_빠지고_결과는_아직_없다()
    {
        var scrap = 아이템("scrap");
        var 필터 = 아이템("filter", 5);
        var inv = new Inventory(6);
        inv.TryAdd(scrap, 10);

        var r = 레시피(new ItemStack(필터, 1), 4f, StationType.None, new ItemStack(scrap, 8));
        var q = new CraftQueue();

        Assert.IsTrue(CraftQueueService.TryEnqueue(q, r, 1, inv, StationType.None));
        Assert.AreEqual(2, inv.CountOf("scrap"), "재료는 걸 때 빠진다");
        Assert.AreEqual(0, inv.CountOf("filter"), "아직 완성되지 않았다");
        Assert.AreEqual(1, q.Count);
    }

    [Test]
    public void 수량_제작은_재료를_수량배로_먹는다()
    {
        var scrap = 아이템("scrap");
        var 필터 = 아이템("filter", 5);
        var inv = new Inventory(6);
        inv.TryAdd(scrap, 30);

        var r = 레시피(new ItemStack(필터, 1), 2f, StationType.None, new ItemStack(scrap, 8));
        var q = new CraftQueue();

        Assert.IsTrue(CraftQueueService.TryEnqueue(q, r, 3, inv, StationType.None));
        Assert.AreEqual(30 - 24, inv.CountOf("scrap"));
        Assert.AreEqual(3, q.Active.Remaining);
    }

    [Test]
    public void 재료보다_많이_걸_수_없다()
    {
        var scrap = 아이템("scrap");
        var 필터 = 아이템("filter", 5);
        var inv = new Inventory(6);
        inv.TryAdd(scrap, 10);

        var r = 레시피(new ItemStack(필터, 1), 2f, StationType.None, new ItemStack(scrap, 8));
        var q = new CraftQueue();

        Assert.AreEqual(1, CraftQueueService.MaxCraftable(r, inv, StationType.None));
        Assert.IsFalse(CraftQueueService.TryEnqueue(q, r, 2, inv, StationType.None));
        Assert.AreEqual(10, inv.CountOf("scrap"), "실패하면 재료를 건드리지 않는다");
        Assert.AreEqual(0, q.Count);
    }

    [Test]
    public void 스테이션_요건을_못_맞추면_걸리지_않는다()
    {
        var scrap = 아이템("scrap");
        var 키 = 아이템("key", 1);
        var inv = new Inventory(6);
        inv.TryAdd(scrap, 40);

        var r = 레시피(new ItemStack(키, 1), 2f, StationType.Bench, new ItemStack(scrap, 20));
        var q = new CraftQueue();

        Assert.IsFalse(CraftQueueService.TryEnqueue(q, r, 1, inv, StationType.None));
        Assert.IsTrue(CraftQueueService.TryEnqueue(q, r, 1, inv, StationType.Bench));
    }

    [Test]
    public void 대기열이_가득_차면_더_걸_수_없다()
    {
        var scrap = 아이템("scrap");
        var 돌 = 아이템("stone");
        var inv = new Inventory(6);
        inv.TryAdd(scrap, 99);

        var r = 레시피(new ItemStack(돌, 1), 2f, StationType.None, new ItemStack(scrap, 1));
        var q = new CraftQueue(2);

        Assert.IsTrue(CraftQueueService.TryEnqueue(q, r, 1, inv, StationType.None));
        Assert.IsTrue(CraftQueueService.TryEnqueue(q, r, 1, inv, StationType.None));
        Assert.IsFalse(CraftQueueService.TryEnqueue(q, r, 1, inv, StationType.None));
        Assert.AreEqual(2, q.Count);
    }

    // ── 진행 ────────────────────────────────────────────────

    [Test]
    public void 시간이_차야_완성된다()
    {
        var scrap = 아이템("scrap");
        var 필터 = 아이템("filter", 5);
        var inv = new Inventory(6);
        inv.TryAdd(scrap, 10);

        var r = 레시피(new ItemStack(필터, 1), 4f, StationType.None, new ItemStack(scrap, 8));
        var q = new CraftQueue();
        CraftQueueService.TryEnqueue(q, r, 1, inv, StationType.None);

        Assert.AreEqual(0, CraftQueueService.Tick(q, 3.9f, inv, true), "아직이다");
        Assert.AreEqual(0, inv.CountOf("filter"));
        Assert.That(q.Active.UnitProgress, Is.EqualTo(0.975f).Within(0.001f));

        Assert.AreEqual(1, CraftQueueService.Tick(q, 0.2f, inv, true), "이제 완성된다");
        Assert.AreEqual(1, inv.CountOf("filter"));
        Assert.AreEqual(0, q.Count, "다 만든 항목은 줄에서 빠진다");
    }

    [Test]
    public void 수량_제작은_개당_하나씩_들어온다()
    {
        var scrap = 아이템("scrap");
        var 돌 = 아이템("stone");
        var inv = new Inventory(6);
        inv.TryAdd(scrap, 10);

        var r = 레시피(new ItemStack(돌, 1), 2f, StationType.None, new ItemStack(scrap, 2));
        var q = new CraftQueue();
        CraftQueueService.TryEnqueue(q, r, 3, inv, StationType.None);

        CraftQueueService.Tick(q, 2f, inv, true);
        Assert.AreEqual(1, inv.CountOf("stone"));
        Assert.AreEqual(2, q.Active.Remaining, "항목은 남아 있다");

        CraftQueueService.Tick(q, 2f, inv, true);
        Assert.AreEqual(2, inv.CountOf("stone"));

        CraftQueueService.Tick(q, 2f, inv, true);
        Assert.AreEqual(3, inv.CountOf("stone"));
        Assert.AreEqual(0, q.Count);
    }

    [Test]
    public void 맨_앞만_진행하고_뒤는_기다린다()
    {
        var scrap = 아이템("scrap");
        var 돌 = 아이템("stone");
        var 판 = 아이템("plate");
        var inv = new Inventory(6);
        inv.TryAdd(scrap, 20);

        var 첫째 = 레시피(new ItemStack(돌, 1), 4f, StationType.None, new ItemStack(scrap, 2));
        var 둘째 = 레시피(new ItemStack(판, 1), 1f, StationType.None, new ItemStack(scrap, 2));
        둘째.id = "second";

        var q = new CraftQueue();
        CraftQueueService.TryEnqueue(q, 첫째, 1, inv, StationType.None);
        CraftQueueService.TryEnqueue(q, 둘째, 1, inv, StationType.None);

        CraftQueueService.Tick(q, 2f, inv, true);
        Assert.AreEqual(0, inv.CountOf("plate"), "뒤 항목은 시간이 남아도 진행되지 않는다");
        Assert.That(q.At(1).Elapsed, Is.EqualTo(0f));

        CraftQueueService.Tick(q, 2f, inv, true);
        Assert.AreEqual(1, inv.CountOf("stone"));
        Assert.AreEqual(1, q.Count, "이제 둘째가 맨 앞이다");

        CraftQueueService.Tick(q, 1f, inv, true);
        Assert.AreEqual(1, inv.CountOf("plate"));
    }

    [Test]
    public void 동력이_없으면_진행하지_않는다()
    {
        var scrap = 아이템("scrap");
        var 셀 = 아이템("cell");
        var inv = new Inventory(6);
        inv.TryAdd(scrap, 10);

        var r = 레시피(new ItemStack(셀, 1), 3f, StationType.Campfire, new ItemStack(scrap, 5));
        var q = new CraftQueue();
        CraftQueueService.TryEnqueue(q, r, 1, inv, StationType.Campfire);

        CraftQueueService.Tick(q, 10f, inv, powered: false);
        Assert.AreEqual(0f, q.Active.Elapsed, "불이 꺼져 있으면 시간이 흐르지 않는다");
        Assert.AreEqual(0, inv.CountOf("cell"));

        CraftQueueService.Tick(q, 3f, inv, powered: true);
        Assert.AreEqual(1, inv.CountOf("cell"), "다시 불이 붙으면 이어서 진행한다");
    }

    [Test]
    public void 소요시간이_0이면_즉시_완성된다()
    {
        var 돌 = 아이템("stone");
        var inv = new Inventory(4);
        var r = 레시피(new ItemStack(돌, 1), 0f);
        var q = new CraftQueue();

        CraftQueueService.TryEnqueue(q, r, 5, inv, StationType.None);
        Assert.AreEqual(5, CraftQueueService.Tick(q, 0.016f, inv, true));
        Assert.AreEqual(5, inv.CountOf("stone"));
        Assert.AreEqual(0, q.Count);
    }

    [Test]
    public void 넣을_자리가_없으면_완성된_채_기다린다()
    {
        // 한 칸짜리 소지품에 스크랩만 가득. 결과가 들어갈 자리가 없다.
        var scrap = 아이템("scrap", 99);
        var 필터 = 아이템("filter", 1);
        var 재료 = new Inventory(4);
        재료.TryAdd(scrap, 20);

        var r = 레시피(new ItemStack(필터, 1), 1f, StationType.None, new ItemStack(scrap, 8));
        var q = new CraftQueue();
        CraftQueueService.TryEnqueue(q, r, 1, 재료, StationType.None);

        var 가득한_출구 = new Inventory(1);
        가득한_출구.TryAdd(scrap, 99);

        Assert.AreEqual(0, CraftQueueService.Tick(q, 2f, 가득한_출구, true));
        Assert.IsTrue(q.Active.Stalled, "산출물을 버리지 않고 멈춘다");
        Assert.AreEqual(1, q.Active.Remaining);

        var 빈_출구 = new Inventory(4);
        Assert.AreEqual(1, CraftQueueService.Tick(q, 0.016f, 빈_출구, true), "자리가 생기면 곧 나온다");
        Assert.AreEqual(1, 빈_출구.CountOf("filter"));
    }

    // ── 취소 ────────────────────────────────────────────────

    [Test]
    public void 취소하면_남은_개수만큼_전액_환급된다()
    {
        var scrap = 아이템("scrap");
        var 돌 = 아이템("stone");
        var inv = new Inventory(6);
        inv.TryAdd(scrap, 20);

        var r = 레시피(new ItemStack(돌, 1), 4f, StationType.None, new ItemStack(scrap, 5));
        var q = new CraftQueue();
        CraftQueueService.TryEnqueue(q, r, 3, inv, StationType.None);
        Assert.AreEqual(5, inv.CountOf("scrap"));

        // 하나는 완성시키고, 둘째를 반쯤 진행한 상태에서 물린다.
        CraftQueueService.Tick(q, 4f, inv, true);
        CraftQueueService.Tick(q, 2f, inv, true);
        Assert.AreEqual(1, inv.CountOf("stone"));
        Assert.AreEqual(2, q.Active.Remaining);

        Assert.IsTrue(CraftQueueService.TryCancel(q, 0, inv));
        Assert.AreEqual(5 + 10, inv.CountOf("scrap"),
            "완성되지 않은 두 개분(5×2)이 전부 돌아온다");
        Assert.AreEqual(0, q.Count);
    }

    [Test]
    public void 뒤에_있는_항목만_골라_취소할_수_있다()
    {
        var scrap = 아이템("scrap");
        var 돌 = 아이템("stone");
        var inv = new Inventory(6);
        inv.TryAdd(scrap, 20);

        var r = 레시피(new ItemStack(돌, 1), 4f, StationType.None, new ItemStack(scrap, 5));
        var q = new CraftQueue();
        CraftQueueService.TryEnqueue(q, r, 1, inv, StationType.None);
        CraftQueueService.TryEnqueue(q, r, 2, inv, StationType.None);
        Assert.AreEqual(5, inv.CountOf("scrap"));

        Assert.IsTrue(CraftQueueService.TryCancel(q, 1, inv));
        Assert.AreEqual(15, inv.CountOf("scrap"), "미착수분만 돌아온다");
        Assert.AreEqual(1, q.Count, "진행 중이던 항목은 그대로다");
    }

    [Test]
    public void 없는_항목을_취소해도_아무_일도_없다()
    {
        var inv = new Inventory(4);
        var q = new CraftQueue();
        Assert.IsFalse(CraftQueueService.TryCancel(q, 0, inv));
        Assert.IsFalse(CraftQueueService.TryCancel(q, -1, inv));
    }

    [Test]
    public void 전체_취소는_줄을_비우고_전부_돌려준다()
    {
        var scrap = 아이템("scrap");
        var 돌 = 아이템("stone");
        var inv = new Inventory(6);
        inv.TryAdd(scrap, 20);

        var r = 레시피(new ItemStack(돌, 1), 4f, StationType.None, new ItemStack(scrap, 5));
        var q = new CraftQueue();
        CraftQueueService.TryEnqueue(q, r, 2, inv, StationType.None);
        CraftQueueService.TryEnqueue(q, r, 2, inv, StationType.None);

        Assert.AreEqual(2, CraftQueueService.CancelAll(q, inv));
        Assert.AreEqual(20, inv.CountOf("scrap"));
        Assert.IsTrue(q.IsEmpty);
    }

    // ── 남은 시간 ────────────────────────────────────────────

    [Test]
    public void 남은_시간은_줄_전체를_더한_값이다()
    {
        var scrap = 아이템("scrap");
        var 돌 = 아이템("stone");
        var inv = new Inventory(6);
        inv.TryAdd(scrap, 20);

        var r = 레시피(new ItemStack(돌, 1), 3f, StationType.None, new ItemStack(scrap, 2));
        var q = new CraftQueue();
        CraftQueueService.TryEnqueue(q, r, 2, inv, StationType.None);   // 6초
        CraftQueueService.TryEnqueue(q, r, 1, inv, StationType.None);   // 3초

        Assert.That(CraftQueueService.TotalSecondsLeft(q), Is.EqualTo(9f).Within(0.001f));

        CraftQueueService.Tick(q, 1f, inv, true);
        Assert.That(CraftQueueService.TotalSecondsLeft(q), Is.EqualTo(8f).Within(0.001f));
    }

    [Test]
    public void 걸릴_때마다_바뀜이_알려진다()
    {
        var scrap = 아이템("scrap");
        var 돌 = 아이템("stone");
        var inv = new Inventory(6);
        inv.TryAdd(scrap, 20);

        var r = 레시피(new ItemStack(돌, 1), 1f, StationType.None, new ItemStack(scrap, 2));
        var q = new CraftQueue();

        int 알림 = 0;
        q.Changed += () => 알림++;

        CraftQueueService.TryEnqueue(q, r, 1, inv, StationType.None);
        Assert.AreEqual(1, 알림);

        CraftQueueService.Tick(q, 1f, inv, true);
        Assert.AreEqual(2, 알림, "완성도 바뀜이다");

        CraftQueueService.TryEnqueue(q, r, 1, inv, StationType.None);
        CraftQueueService.TryCancel(q, 0, inv);
        Assert.AreEqual(4, 알림);
    }

    // ── 스테이션 귀속 ────────────────────────────────────────

    [Test]
    public void 스테이션은_사람이_없어도_돌고_산출물을_들고_있는다()
    {
        var scrap = 아이템("scrap");
        var 셀 = 아이템("cell");
        var 소지품 = new Inventory(6);
        소지품.TryAdd(scrap, 20);

        var r = 레시피(new ItemStack(셀, 1), 2f, StationType.Bench, new ItemStack(scrap, 5));
        var 작업 = new StationCraftQueue();

        Assert.IsTrue(CraftQueueService.TryEnqueue(작업.Queue, r, 2, 소지품, StationType.Bench));
        Assert.AreEqual(10, 소지품.CountOf("scrap"));

        // 사람은 떠났다. 소지품에 넣는 것이 아니라 회수함에 쌓인다.
        작업.Tick(2f, powered: true);
        작업.Tick(2f, powered: true);

        Assert.AreEqual(0, 소지품.CountOf("cell"), "자리에 없었으니 손에 들어올 리 없다");
        Assert.IsTrue(작업.HasOutput);
        Assert.AreEqual(2, 작업.OutputCount);

        Assert.AreEqual(2, 작업.CollectInto(소지품), "돌아와서 회수한다");
        Assert.AreEqual(2, 소지품.CountOf("cell"));
        Assert.IsFalse(작업.HasOutput);
    }

    [Test]
    public void 회수할_자리가_모자라면_남은_것은_스테이션에_남는다()
    {
        var 셀 = 아이템("cell", 1);
        var scrap = 아이템("scrap");
        var 소지품 = new Inventory(3);
        소지품.TryAdd(scrap, 10);

        var r = 레시피(new ItemStack(셀, 1), 1f, StationType.Bench, new ItemStack(scrap, 1));
        var 작업 = new StationCraftQueue();
        CraftQueueService.TryEnqueue(작업.Queue, r, 3, 소지품, StationType.Bench);

        for (int i = 0; i < 3; i++) 작업.Tick(1f, true);
        Assert.AreEqual(3, 작업.OutputCount);

        // 소지품은 3칸 — 스크랩 1칸 + 셀은 1칸에 1개씩이므로 두 개만 들어간다.
        Assert.AreEqual(2, 작업.CollectInto(소지품));
        Assert.AreEqual(2, 소지품.CountOf("cell"));
        Assert.AreEqual(1, 작업.OutputCount, "못 가져간 것은 스테이션에 남는다");
    }
}
