using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Survive.Crafting;
using Survive.Items;
using Survive.World;

/// <summary>
/// <b>대기열과 그릇이 저장을 건넌다.</b>
///
/// 규칙 §5.4가 「여러 개를 대기열에 걸어두고 자리를 뜰 수 있고, 취소하면 재료가
/// 돌아온다」고 한다. 그런데 <b>저장으로 자리를 뜨면 잃었다</b> — 그러면 그 설계가
/// 반만 참이다. 재료는 걸 때 이미 빠지므로 잃는 것은 시간이 아니라 물건이었다.
///
/// 여기서 못 박는 것이 셋이다.
/// <list type="number">
/// <item><b>이어 간다. 돌려주지 않는다.</b> 취소는 사람이 「그만두겠다」고 말한
///   것이고 불러오기는 「이어서 하겠다」다. 두 규칙이 같은 답을 내면 스무 개를
///   걸어 놓고 나간 사람이 돌아왔을 때 재료 더미를 보게 된다.</item>
/// <item><b>되돌리기가 재료를 다시 받지 않는다.</b> 받으면 저장할 때마다 재료가
///   사라진다.</item>
/// <item><b>붓기 전에 비운다.</b> 안 비우면 두 번 되돌릴 때 내용물이 불어난다 —
///   저장이 물건을 먹는 것보다 나쁜 것은 물건을 찍어 내는 것이다.</item>
/// </list>
/// </summary>
public class StationSaveRuleTests
{
    static ItemDataSO 아이템(string id, int maxStack = 99)
    {
        var it = ScriptableObject.CreateInstance<ItemDataSO>();
        it.id = id;
        it.displayName = id;
        it.maxStack = maxStack;
        return it;
    }

    static RecipeSO 레시피(string id, float seconds = 4f, params ItemStack[] 재료)
    {
        var r = ScriptableObject.CreateInstance<RecipeSO>();
        r.id = id;
        r.displayName = id;
        r.craftSeconds = seconds;
        r.ingredients = 재료;
        r.result = new ItemStack(아이템(id), 1);
        return r;
    }

    /// <summary>id에서 정의를 찾는 손. 실제로는 표가 하는 일이다.</summary>
    static System.Func<string, ItemDataSO> 아이템표(params ItemDataSO[] 것들)
    {
        var map = new Dictionary<string, ItemDataSO>();
        foreach (var it in 것들) map[it.id] = it;
        return id => map.TryGetValue(id, out var v) ? v : null;
    }

    static System.Func<string, RecipeSO> 레시피표(params RecipeSO[] 것들)
    {
        var map = new Dictionary<string, RecipeSO>();
        foreach (var r in 것들) map[r.id] = r;
        return id => map.TryGetValue(id, out var v) ? v : null;
    }

    // ══ 그릇 ═══════════════════════════════════════════════════

    /// <summary>
    /// <b>빈 칸은 안 적는다.</b> 18칸짜리 보관함에 하나만 들어 있으면 저장본에도
    /// 한 줄이다. 칸 번호도 안 적는다 — 앞에서부터 채우면 사람 눈에는 같은
    /// 것이고, 칸 번호를 적으면 칸 수를 바꾸는 날 저장본이 범위 밖을 가리킨다.
    /// </summary>
    [Test]
    public void 빈_칸은_저장본에_안_적힌다()
    {
        var 잔해 = 아이템("scrap");
        var 그릇 = new Inventory(18);
        그릇.TryAdd(잔해, 7);

        var 적힌것 = StationSaveRule.Capture(그릇);

        Assert.AreEqual(1, 적힌것.Count, "빈 칸 열일곱이 저장본을 채우지 않는다");
        Assert.AreEqual("scrap", 적힌것[0].id);
        Assert.AreEqual(7, 적힌것[0].count);
    }

    [Test]
    public void 적은_것이_그대로_돌아온다()
    {
        var 잔해 = 아이템("scrap");
        var 목재 = 아이템("mushroom_wood");

        var 넣은것 = new Inventory(18);
        넣은것.TryAdd(잔해, 12);
        넣은것.TryAdd(목재, 5);

        var 되돌린것 = new Inventory(18);
        StationSaveRule.AdoptInto(되돌린것, StationSaveRule.Capture(넣은것),
                                  아이템표(잔해, 목재));

        Assert.AreEqual(12, 되돌린것.CountOf("scrap"));
        Assert.AreEqual(5, 되돌린것.CountOf("mushroom_wood"));
    }

    /// <summary>
    /// <b>두 번 되돌려도 안 불어난다.</b> 불러오기는 한 판에 여러 번 일어날 수
    /// 있고(사람이 이어하기를 다시 누른다), 그때마다 내용물이 배로 늘면
    /// 저장이 물건을 찍어 내는 기계가 된다.
    /// </summary>
    [Test]
    public void 두_번_되돌려도_내용물이_불어나지_않는다()
    {
        var 잔해 = 아이템("scrap");
        var 적힌것 = new List<SpawnStack> { new SpawnStack { id = "scrap", count = 9 } };

        var 그릇 = new Inventory(18);
        StationSaveRule.AdoptInto(그릇, 적힌것, 아이템표(잔해));
        StationSaveRule.AdoptInto(그릇, 적힌것, 아이템표(잔해));

        Assert.AreEqual(9, 그릇.CountOf("scrap"));
    }

    /// <summary>
    /// 표에서 사라진 아이템은 건너뛴다. 통째로 실패하면 <b>나머지 열일곱 칸</b>까지
    /// 함께 잃는다 — 하나가 빠진 보관함이 텅 빈 보관함보다 낫다.
    /// </summary>
    [Test]
    public void 표에_없는_아이템은_건너뛰고_나머지는_들어온다()
    {
        var 잔해 = 아이템("scrap");
        var 적힌것 = new List<SpawnStack>
        {
            new SpawnStack { id = "없어진것", count = 3 },
            new SpawnStack { id = "scrap", count = 4 },
        };

        // 사유는 경고로 남는다(오류가 아니다) — 저장본이 낡은 것은 사고가 아니라
        // 데이터를 고친 결과일 수 있고, 그때 게임이 멈추면 안 된다.
        var 그릇 = new Inventory(18);
        StationSaveRule.AdoptInto(그릇, 적힌것, 아이템표(잔해));

        Assert.AreEqual(4, 그릇.CountOf("scrap"));
    }

    // ══ 대기열 ═════════════════════════════════════════════════

    [Test]
    public void 걸어_둔_제작이_남은_수와_진행도_그대로_돌아온다()
    {
        var 배터리 = 레시피("battery_cell", 6f);
        var 원본 = new CraftQueue();
        var 소지품 = new Inventory(10);

        Assert.IsTrue(CraftQueueService.TryEnqueue(원본, 배터리, 5, 소지품, StationType.None));

        // 진행도는 실제로 시간을 흘려 만든다. 손으로 찍어 넣으면 검사가
        // Tick이 만들지 못하는 상태를 재는 셈이 된다.
        var 회수함 = new Inventory(4);
        CraftQueueService.Tick(원본, 2.5f, 회수함, powered: true);
        Assert.AreEqual(2.5f, 원본.Active.Elapsed, 0.001f);

        var 적힌것 = StationSaveRule.Capture(원본);
        Assert.AreEqual(1, 적힌것.Count);

        var 되돌린것 = new CraftQueue();
        Assert.AreEqual(1, StationSaveRule.AdoptInto(되돌린것, 적힌것, 레시피표(배터리)));

        Assert.AreEqual(5, 되돌린것.Active.Remaining);
        Assert.AreEqual(2.5f, 되돌린것.Active.Elapsed, 0.001f, "진행도를 잃으면 시간을 잃는다");
        Assert.AreEqual(6f, 되돌린것.Active.UnitSeconds, 0.001f);
        Assert.AreSame(배터리, 되돌린것.Active.Recipe);
    }

    /// <summary>
    /// <b>되돌리기는 재료를 다시 받지 않는다.</b> 재료는 걸 때 전부 빠졌고,
    /// 저장본에 적힌 것은 그 결과다. 되돌리면서 <c>TryEnqueue</c>를 지나면
    /// 저장할 때마다 같은 재료를 한 번 더 내게 된다.
    /// </summary>
    [Test]
    public void 되돌리기가_재료를_다시_받지_않는다()
    {
        var 잔해 = 아이템("scrap");
        var 배터리 = 레시피("battery_cell", 4f, new ItemStack(잔해, 2));

        var 소지품 = new Inventory(10);
        소지품.TryAdd(잔해, 6);

        var 원본 = new CraftQueue();
        CraftQueueService.TryEnqueue(원본, 배터리, 3, 소지품, StationType.None);
        Assert.AreEqual(0, 소지품.CountOf("scrap"), "걸 때 여섯이 전부 빠졌다");

        var 적힌것 = StationSaveRule.Capture(원본);

        // 재료가 없는 소지품 앞에서도 되돌아와야 한다.
        var 되돌린것 = new CraftQueue();
        Assert.AreEqual(1, StationSaveRule.AdoptInto(되돌린것, 적힌것, 레시피표(배터리)));
        Assert.AreEqual(3, 되돌린것.Active.Remaining);
        Assert.AreEqual(0, 소지품.CountOf("scrap"), "되돌리기가 소지품을 건드리지 않는다");
    }

    /// <summary>
    /// <b>취소와 불러오기는 다른 답을 낸다.</b> 취소는 남은 것을 전부 환급하고,
    /// 불러오기는 남은 것을 그대로 이어 간다. 같은 답을 내면 스무 개를 걸어 놓고
    /// 나간 사람이 돌아왔을 때 스무 개가 아니라 재료 더미를 보게 된다 —
    /// 그것은 「걸어두고 자리를 뜰 수 있다」가 아니다.
    /// </summary>
    [Test]
    public void 취소는_재료를_돌려주지만_불러오기는_이어_간다()
    {
        var 잔해 = 아이템("scrap");
        var 배터리 = 레시피("battery_cell", 4f, new ItemStack(잔해, 2));

        // 취소 쪽
        var 소지품A = new Inventory(10);
        소지품A.TryAdd(잔해, 6);
        var 줄A = new CraftQueue();
        CraftQueueService.TryEnqueue(줄A, 배터리, 3, 소지품A, StationType.None);
        Assert.IsTrue(CraftQueueService.TryCancel(줄A, 0, 소지품A));

        Assert.AreEqual(6, 소지품A.CountOf("scrap"), "취소하면 재료가 돌아온다");
        Assert.IsTrue(줄A.IsEmpty);

        // 불러오기 쪽
        var 소지품B = new Inventory(10);
        소지품B.TryAdd(잔해, 6);
        var 줄B = new CraftQueue();
        CraftQueueService.TryEnqueue(줄B, 배터리, 3, 소지품B, StationType.None);

        var 되돌린것 = new CraftQueue();
        StationSaveRule.AdoptInto(되돌린것, StationSaveRule.Capture(줄B), 레시피표(배터리));

        Assert.AreEqual(0, 소지품B.CountOf("scrap"), "불러오기는 재료를 안 돌려준다");
        Assert.AreEqual(3, 되돌린것.Active.Remaining, "대신 만들던 것이 그대로 남는다");
    }

    /// <summary>
    /// 진행도는 한 개분을 넘을 수 없다. 넘은 값을 그대로 앉히면 다음 한 번의
    /// <c>Tick</c>에 여러 개가 쏟아진다 — 고쳐 쓴 저장본으로 제작을 공짜로 만드는 길이다.
    /// </summary>
    [Test]
    public void 진행도가_개당_시간을_넘지_못한다()
    {
        var 배터리 = 레시피("battery_cell", 4f);
        var 줄 = new CraftQueue();

        StationSaveRule.AdoptInto(줄, new List<SpawnJobRow>
        {
            new SpawnJobRow { recipe = "battery_cell", remaining = 9,
                              elapsed = 9999f, unitSeconds = 4f },
        }, 레시피표(배터리));

        Assert.AreEqual(4f, 줄.Active.Elapsed, 0.001f);
    }

    /// <summary>
    /// <c>Stalled</c>는 안 적는다. 「지금 넣을 자리가 없다」는 판정이지 상태가
    /// 아니고, 되살아난 스테이션의 첫 <c>Tick</c>이 그 자리에서 다시 판정한다.
    /// </summary>
    [Test]
    public void 멈춤_표시는_저장본을_건너지_않는다()
    {
        var 배터리 = 레시피("battery_cell", 4f);
        var 줄 = new CraftQueue();
        var 소지품 = new Inventory(4);
        CraftQueueService.TryEnqueue(줄, 배터리, 2, 소지품, StationType.None);

        // 회수함을 남의 것으로 꽉 채워 실제로 멈추게 만든다.
        var 회수함 = new Inventory(1);
        회수함.TryAdd(아이템("남의것", 1), 1);
        CraftQueueService.Tick(줄, 5f, 회수함, powered: true);
        Assert.IsTrue(줄.Active.Stalled, "넣을 자리가 없어 멈춰 섰다");

        var 되돌린것 = new CraftQueue();
        StationSaveRule.AdoptInto(되돌린것, StationSaveRule.Capture(줄), 레시피표(배터리));

        Assert.IsFalse(되돌린것.Active.Stalled);
    }

    /// <summary>
    /// 대기열 칸 수를 넘는 줄은 안 들어간다. 저장본은 사람이 고칠 수 있는 입력이라
    /// 여기서도 게임의 규칙이 이겨야 한다.
    /// </summary>
    [Test]
    public void 대기열_칸_수보다_많은_줄은_안_들어간다()
    {
        var 배터리 = 레시피("battery_cell");
        var 줄들 = new List<SpawnJobRow>();
        for (int i = 0; i < CraftQueue.DefaultCapacity + 3; i++)
            줄들.Add(new SpawnJobRow { recipe = "battery_cell", remaining = 1, unitSeconds = 1f });

        var 줄 = new CraftQueue();
        StationSaveRule.AdoptInto(줄, 줄들, 레시피표(배터리));

        Assert.AreEqual(CraftQueue.DefaultCapacity, 줄.Count);
    }

    /// <summary>
    /// 되돌리기 전에 비운다. 대기열도 그릇과 같은 이유다.
    /// </summary>
    [Test]
    public void 두_번_되돌려도_대기열이_불어나지_않는다()
    {
        var 배터리 = 레시피("battery_cell");
        var 적힌것 = new List<SpawnJobRow>
        {
            new SpawnJobRow { recipe = "battery_cell", remaining = 2, unitSeconds = 1f },
        };

        var 줄 = new CraftQueue();
        StationSaveRule.AdoptInto(줄, 적힌것, 레시피표(배터리));
        StationSaveRule.AdoptInto(줄, 적힌것, 레시피표(배터리));

        Assert.AreEqual(1, 줄.Count);
    }

    /// <summary>
    /// 빈 저장본을 되돌리면 <b>비운다</b>. 「저장할 때 아무것도 안 걸려 있었다」는
    /// 것도 사실이고, 그것을 안 반영하면 불러오기가 앞 판의 대기열을 물려받는다.
    /// </summary>
    [Test]
    public void 비어_있던_저장본은_대기열을_비운다()
    {
        var 배터리 = 레시피("battery_cell");
        var 소지품 = new Inventory(10);
        var 줄 = new CraftQueue();
        CraftQueueService.TryEnqueue(줄, 배터리, 1, 소지품, StationType.None);

        StationSaveRule.AdoptInto(줄, new List<SpawnJobRow>(), 레시피표(배터리));

        Assert.IsTrue(줄.IsEmpty);
    }
}
