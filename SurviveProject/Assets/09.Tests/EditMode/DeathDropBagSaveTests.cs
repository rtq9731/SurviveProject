using NUnit.Framework;
using UnityEngine;
using Survive.Items;

/// <summary>
/// 가방이 저장본을 넘어가는가. 죽은 자리에 남긴 것은 저장하고 다시 켜도
/// 같은 자리에 같은 내용물로 있어야 한다 — 그러지 않으면 저장이
/// "죽기 전으로 돌아가는 버튼"이 되어 왕복 압박이 사라진다.
///
/// 씬을 띄우지 않고 보는 것은 <b>형식의 왕복</b>뿐이다. 실제로 가방이
/// 다시 세워지는지는 PlayMode의 E2ESave가 본다.
/// </summary>
public class DeathDropBagSaveTests
{
    static ItemDataSO 아이템(string id, ItemCategory category = ItemCategory.Resource, int maxStack = 99)
    {
        var it = ScriptableObject.CreateInstance<ItemDataSO>();
        it.id = id;
        it.displayName = id;
        it.maxStack = maxStack;
        it.category = category;
        return it;
    }

    static ItemDatabaseSO 데이터베이스(params ItemDataSO[] items)
    {
        var db = ScriptableObject.CreateInstance<ItemDatabaseSO>();
        db.items = items;
        return db;
    }

    /// <summary>SaveService가 하는 것과 같은 경로 — 상태 객체를 JSON으로 굽고 되읽는다.</summary>
    static DeathDropBagsState 왕복(DeathDropBagsState state)
    {
        var text = JsonUtility.ToJson(state);
        var back = JsonUtility.FromJson<DeathDropBagsState>(text);
        Assert.IsNotNull(back, "저장본을 되읽지 못했다: " + text);
        return back;
    }

    static DeathDropBagsState 담기(params DeathDropBagRecord[] records)
    {
        var s = new DeathDropBagsState();
        foreach (var r in records) if (r != null) s.bags.Add(r);
        return s;
    }

    // ── 적기 ────────────────────────────────────────────────

    [Test]
    public void 빈_가방은_적지_않는다()
    {
        // 회수가 끝난 가방은 세계에서도 사라진다. 되살릴 것이 없다.
        Assert.IsNull(DeathDropBagSave.Capture(Vector3.zero, new Inventory(6)));
    }

    [Test]
    public void 인벤토리가_없으면_적지_않는다()
    {
        Assert.IsNull(DeathDropBagSave.Capture(Vector3.zero, null));
    }

    [Test]
    public void 적을_때_칸_수와_채워진_칸만_남는다()
    {
        var 가방 = new Inventory(8);
        가방.TryAdd(아이템("scrap"), 12);

        var record = DeathDropBagSave.Capture(new Vector3(1f, 2f, 3f), 가방);

        Assert.IsNotNull(record);
        Assert.AreEqual(8, record.slotCount);
        Assert.AreEqual(1, record.slots.Count, "빈 칸까지 적으면 저장본이 부풀기만 한다");
        Assert.AreEqual("scrap", record.slots[0].itemId);
        Assert.AreEqual(12, record.slots[0].count);
    }

    // ── 왕복 ────────────────────────────────────────────────

    [Test]
    public void 왕복하면_가방_위치가_보존된다()
    {
        var 가방 = new Inventory(6);
        가방.TryAdd(아이템("scrap"), 5);

        var 자리 = new Vector3(12.5f, 51.25f, -3.75f);
        var back = 왕복(담기(DeathDropBagSave.Capture(자리, 가방)));

        Assert.AreEqual(1, back.bags.Count);
        Assert.AreEqual(자리.x, back.bags[0].position.x, 0.001f);
        Assert.AreEqual(자리.y, back.bags[0].position.y, 0.001f);
        Assert.AreEqual(자리.z, back.bags[0].position.z, 0.001f);
    }

    [Test]
    public void 왕복하면_내용물_수량이_그대로다()
    {
        var scrap = 아이템("scrap");
        var alloy = 아이템("alien_alloy", ItemCategory.Resource, 20);
        var filter = 아이템("oxygen_filter", ItemCategory.Consumable, 5);

        var 가방 = new Inventory(10);
        가방.TryAdd(scrap, 47);
        가방.TryAdd(alloy, 13);
        가방.TryAdd(filter, 2);

        var back = 왕복(담기(DeathDropBagSave.Capture(Vector3.zero, 가방)));
        var 되살린것 = DeathDropBagSave.Restore(back.bags[0], 데이터베이스(scrap, alloy, filter));

        Assert.IsNotNull(되살린것);
        Assert.AreEqual(47, 되살린것.CountOf("scrap"));
        Assert.AreEqual(13, 되살린것.CountOf("alien_alloy"));
        Assert.AreEqual(2, 되살린것.CountOf("oxygen_filter"));
    }

    [Test]
    public void 왕복하면_슬롯_배치가_그대로다()
    {
        // 여러 슬롯에 걸친 스택은 죽을 때의 모양대로 돌아와야 한다.
        // 다시 담으면 앞 스택부터 채워져 배치가 달라진다.
        var scrap = 아이템("scrap", ItemCategory.Resource, 10);
        var 가방 = new Inventory(5);
        가방.TryAdd(scrap, 25);            // 10 / 10 / 5

        var back = 왕복(담기(DeathDropBagSave.Capture(Vector3.zero, 가방)));
        var 되살린것 = DeathDropBagSave.Restore(back.bags[0], 데이터베이스(scrap));

        Assert.IsNotNull(되살린것);
        Assert.AreEqual(5, 되살린것.SlotCount);
        Assert.AreEqual(10, 되살린것.Slots[0].count);
        Assert.AreEqual(10, 되살린것.Slots[1].count);
        Assert.AreEqual(5, 되살린것.Slots[2].count);
        Assert.IsTrue(되살린것.Slots[3].IsEmpty);
    }

    [Test]
    public void 가방이_여럿이면_각각_제자리로_돌아온다()
    {
        var scrap = 아이템("scrap");
        var alloy = 아이템("alien_alloy");

        var 첫가방 = new Inventory(4);
        첫가방.TryAdd(scrap, 3);
        var 둘째가방 = new Inventory(4);
        둘째가방.TryAdd(alloy, 7);

        var back = 왕복(담기(
            DeathDropBagSave.Capture(new Vector3(1f, 0f, 0f), 첫가방),
            DeathDropBagSave.Capture(new Vector3(0f, 0f, 9f), 둘째가방)));

        Assert.AreEqual(2, back.bags.Count, "두 번 죽으면 가방도 둘이다");

        var db = 데이터베이스(scrap, alloy);
        Assert.AreEqual(3, DeathDropBagSave.Restore(back.bags[0], db).CountOf("scrap"));
        Assert.AreEqual(7, DeathDropBagSave.Restore(back.bags[1], db).CountOf("alien_alloy"));
        Assert.AreEqual(1f, back.bags[0].position.x, 0.001f);
        Assert.AreEqual(9f, back.bags[1].position.z, 0.001f);
    }

    [Test]
    public void 가방이_하나도_없는_저장본도_왕복한다()
    {
        var back = 왕복(new DeathDropBagsState());
        Assert.IsNotNull(back.bags, "목록이 null이면 불러오기가 그 자리에서 죽는다");
        Assert.AreEqual(0, back.bags.Count);
    }

    // ── 망가진 저장본 ───────────────────────────────────────

    [Test]
    public void 정의를_찾지_못한_아이템만_버리고_나머지는_살린다()
    {
        var scrap = 아이템("scrap");
        var 가방 = new Inventory(4);
        가방.TryAdd(scrap, 5);
        가방.TryAdd(아이템("사라진_아이템"), 2);

        var back = 왕복(담기(DeathDropBagSave.Capture(Vector3.zero, 가방)));
        var 되살린것 = DeathDropBagSave.Restore(back.bags[0], 데이터베이스(scrap));

        Assert.IsNotNull(되살린것, "하나 빠졌다고 나머지 회수분까지 잃으면 안 된다");
        Assert.AreEqual(5, 되살린것.CountOf("scrap"));
        Assert.AreEqual(0, 되살린것.CountOf("사라진_아이템"));
    }

    [Test]
    public void 아는_아이템이_하나도_없으면_가방을_세우지_않는다()
    {
        var 가방 = new Inventory(4);
        가방.TryAdd(아이템("사라진_아이템"), 2);

        var record = DeathDropBagSave.Capture(Vector3.zero, 가방);
        Assert.IsNull(DeathDropBagSave.Restore(record, 데이터베이스()));
    }

    [Test]
    public void 슬롯_번호가_범위를_벗어나면_그_칸만_버린다()
    {
        var scrap = 아이템("scrap");
        var record = new DeathDropBagRecord { position = Vector3.zero, slotCount = 2 };
        record.slots.Add(new DeathDropSlotRecord { slot = 0, itemId = "scrap", count = 4 });
        record.slots.Add(new DeathDropSlotRecord { slot = 9, itemId = "scrap", count = 4 });

        var 되살린것 = DeathDropBagSave.Restore(record, 데이터베이스(scrap));

        Assert.IsNotNull(되살린것);
        Assert.AreEqual(4, 되살린것.CountOf("scrap"), "범위 밖의 칸이 터지거나 섞이면 안 된다");
    }

    [Test]
    public void 수량이_0이하인_칸은_버린다()
    {
        var scrap = 아이템("scrap");
        var record = new DeathDropBagRecord { position = Vector3.zero, slotCount = 3 };
        record.slots.Add(new DeathDropSlotRecord { slot = 0, itemId = "scrap", count = 0 });
        record.slots.Add(new DeathDropSlotRecord { slot = 1, itemId = "scrap", count = -5 });

        Assert.IsNull(DeathDropBagSave.Restore(record, 데이터베이스(scrap)));
    }

    [Test]
    public void 칸_수가_0인_저장본도_되살릴_수_있다()
    {
        // Inventory는 0칸을 허용하지 않는다. 손상된 값에 터지지 않아야 한다.
        var scrap = 아이템("scrap");
        var record = new DeathDropBagRecord { position = Vector3.zero, slotCount = 0 };
        record.slots.Add(new DeathDropSlotRecord { slot = 0, itemId = "scrap", count = 3 });

        Inventory 되살린것 = null;
        Assert.DoesNotThrow(() => 되살린것 = DeathDropBagSave.Restore(record, 데이터베이스(scrap)));
        Assert.IsNotNull(되살린것);
        Assert.AreEqual(3, 되살린것.CountOf("scrap"));
    }

    [Test]
    public void null_레코드나_데이터베이스_없이는_되살리지_않는다()
    {
        var 가방 = new Inventory(3);
        가방.TryAdd(아이템("scrap"), 1);
        var record = DeathDropBagSave.Capture(Vector3.zero, 가방);

        Assert.IsNull(DeathDropBagSave.Restore(null, 데이터베이스()));
        Assert.IsNull(DeathDropBagSave.Restore(record, null));
        Assert.IsNull(DeathDropBagSave.Restore(null, null));
    }

    [Test]
    public void 칸_목록이_null인_저장본도_터지지_않는다()
    {
        var record = new DeathDropBagRecord { position = Vector3.zero, slotCount = 3, slots = null };
        Assert.DoesNotThrow(() => Assert.IsNull(DeathDropBagSave.Restore(record, 데이터베이스())));
    }
}
