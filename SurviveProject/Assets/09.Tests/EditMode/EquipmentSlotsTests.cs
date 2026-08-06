using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Survive.Items;

/// <summary>
/// 챕터 1 재건 스펙 §11 - 필수 장비를 소지품 칸에서 떼어낸다.
///
/// 지켜야 하는 규칙은 넷이다.
/// 1. 장비 슬롯은 일반 칸 수를 줄이지 않는다.
/// 2. 소지품이 가득 차도 걸어 둔 장비는 밀려나지 않는다.
/// 3. 장비가 아닌 것은 장비 슬롯에 들어가지 않는다.
/// 4. 세이브를 왕복해도 그대로 있다 (왕복 자체는 PlayerInventory가 하고,
///    여기서는 그 왕복이 기대는 순수 규칙을 지킨다).
/// </summary>
public class EquipmentSlotsTests
{
    static ItemDataSO 물건(string id, int maxStack = 99)
    {
        var it = ScriptableObject.CreateInstance<ItemDataSO>();
        it.id = id;
        it.displayName = id;
        it.maxStack = maxStack;
        it.category = ItemCategory.Resource;
        return it;
    }

    static ItemDataSO 랜턴(string id = "lantern")
    {
        var it = ScriptableObject.CreateInstance<ToolItemSO>();
        it.id = id;
        it.displayName = id;
        it.maxStack = 1;
        it.category = ItemCategory.Tool;
        it.equipSlot = EquipmentSlotKind.Light;
        return it;
    }

    static Inventory 장비칸이_있는_소지품(int slots) => new Inventory(slots, new EquipmentSlots());

    // ── 자리 그 자체 ─────────────────────────────────────────

    [Test]
    public void 장비_슬롯은_일반_칸_수를_줄이지_않는다()
    {
        var inv = 장비칸이_있는_소지품(15);
        inv.TryAdd(랜턴(), 1);

        Assert.AreEqual(15, inv.SlotCount, "칸 수는 그대로다");
        Assert.AreEqual(15, inv.Slots.Count(s => s.IsEmpty), "랜턴을 걸어도 빈 칸은 15개다");
        Assert.IsNotNull(inv.Equipment.Get(EquipmentSlotKind.Light), "랜턴은 장비 자리에 걸린다");
    }

    [Test]
    public void 장비_슬롯이_없는_인벤토리는_예전_그대로다()
    {
        var inv = new Inventory(3);
        Assert.IsNull(inv.Equipment, "보관함과 사망 가방에는 장비 자리가 없다");

        Assert.AreEqual(0, inv.TryAdd(랜턴(), 1));
        Assert.AreEqual(1, inv.Slots[0].count, "장비 자리가 없으면 그냥 칸에 들어간다");
    }

    [Test]
    public void 빈_장비_슬롯은_아무것도_들고_있지_않다()
    {
        var equip = new EquipmentSlots();
        Assert.AreEqual(1, equip.SlotCount, "지금 자리는 조명 하나다");
        Assert.IsNull(equip.Get(EquipmentSlotKind.Light));
        Assert.IsTrue(equip.IsEmpty(EquipmentSlotKind.Light));
        Assert.AreEqual(0, equip.CountOf("lantern"));
        Assert.IsFalse(equip.Has("lantern"));
    }

    // ── 밀려나지 않는다 ──────────────────────────────────────

    [Test]
    public void 소지품이_가득_차도_장비는_밀려나지_않는다()
    {
        var inv = 장비칸이_있는_소지품(15);
        var 등 = 랜턴();
        inv.TryAdd(등, 1);

        // 15칸을 서로 다른 물건으로 꽉 채운다
        for (int i = 0; i < 15; i++) inv.TryAdd(물건("junk" + i, 1), 1);

        Assert.AreEqual(0, inv.Slots.Count(s => s.IsEmpty), "칸은 전부 찼다");
        Assert.AreSame(등, inv.Equipment.Get(EquipmentSlotKind.Light), "랜턴은 그대로 걸려 있다");
        Assert.AreEqual(1, inv.CountOf("lantern"));
    }

    [Test]
    public void 소지품이_가득_찼어도_들어갈_자리가_없으면_남는다()
    {
        var inv = 장비칸이_있는_소지품(2);
        inv.TryAdd(물건("a", 1), 1);
        inv.TryAdd(물건("b", 1), 1);

        int 남은수 = inv.TryAdd(물건("c", 1), 1);
        Assert.AreEqual(1, 남은수, "장비 자리가 있다고 일반 물건이 거기로 새면 안 된다");
    }

    [Test]
    public void 두_번째_랜턴은_걸린_것을_밀어내지_않고_칸으로_간다()
    {
        var inv = 장비칸이_있는_소지품(4);
        var 먼저 = 랜턴();
        var 나중 = 랜턴();

        inv.TryAdd(먼저, 1);
        inv.TryAdd(나중, 1);

        Assert.AreSame(먼저, inv.Equipment.Get(EquipmentSlotKind.Light), "먼저 걸린 것이 이긴다");
        Assert.AreSame(나중, inv.Slots[0].item, "나중 것은 그냥 짐이다");
    }

    // ── 넣고 빼기 ────────────────────────────────────────────

    [Test]
    public void 걸었다가_벗으면_자리가_빈다()
    {
        var equip = new EquipmentSlots();
        var 등 = 랜턴();

        Assert.IsTrue(equip.TryEquip(등));
        Assert.AreSame(등, equip.Get(EquipmentSlotKind.Light));

        Assert.AreSame(등, equip.Unequip(EquipmentSlotKind.Light));
        Assert.IsTrue(equip.IsEmpty(EquipmentSlotKind.Light));
        Assert.IsNull(equip.Unequip(EquipmentSlotKind.Light), "빈 자리를 또 벗기면 null이다");
    }

    [Test]
    public void 찬_자리에_걸면_있던_것을_돌려준다()
    {
        var equip = new EquipmentSlots();
        var 헌것 = 랜턴("lantern");
        var 새것 = 랜턴("lantern_t2");

        equip.TryEquip(헌것);
        Assert.IsTrue(equip.TryEquip(새것, out var 밀려난것));

        Assert.AreSame(헌것, 밀려난것, "밀려난 것은 버리지 않고 돌려준다");
        Assert.AreSame(새것, equip.Get(EquipmentSlotKind.Light));
    }

    [Test]
    public void 장비가_아닌_것은_거부한다()
    {
        var equip = new EquipmentSlots();
        var 돌 = 물건("scrap");

        Assert.IsFalse(equip.CanEquip(돌));
        Assert.IsFalse(equip.TryEquip(돌));
        Assert.IsFalse(equip.TryEquipIntoEmpty(돌));
        Assert.IsTrue(equip.IsEmpty(EquipmentSlotKind.Light), "거부했으면 자리는 그대로 비어 있다");
    }

    [Test]
    public void null은_거부하고_터지지_않는다()
    {
        var equip = new EquipmentSlots();

        Assert.IsFalse(EquipmentSlots.IsEquipment(null));
        Assert.IsFalse(equip.CanEquip(null));
        Assert.IsFalse(equip.TryEquip(null));
        Assert.IsFalse(equip.TryEquipIntoEmpty(null));
        Assert.AreEqual(0, equip.CountOf(null));
        Assert.AreEqual(0, equip.CountOf(""));
        Assert.AreEqual(0, equip.RemoveById(null, 1));
        Assert.AreEqual(0, equip.RemoveById("lantern", 0));

        var inv = 장비칸이_있는_소지품(3);
        Assert.AreEqual(5, inv.TryAdd(null, 5), "null은 하나도 안 들어가고 그대로 남는다");
        Assert.IsTrue(inv.Slots.All(s => s.IsEmpty), "칸도 장비 자리도 건드리지 않는다");
        Assert.IsTrue(inv.Equipment.IsEmpty(EquipmentSlotKind.Light));
    }

    // ── 세는 법 ──────────────────────────────────────────────

    [Test]
    public void 걸어_둔_것도_가진_것으로_센다()
    {
        var inv = 장비칸이_있는_소지품(15);
        inv.TryAdd(랜턴(), 1);

        Assert.AreEqual(1, inv.CountOf("lantern"), "칸에 없어도 가진 것은 가진 것이다");
        Assert.IsTrue(inv.Has("lantern", 1), "랜턴 점등 판정이 이것에 기댄다");
        Assert.AreEqual(0, inv.CountInSlots("lantern"), "칸에는 없다");
    }

    [Test]
    public void 칸에_있는_것부터_빼고_모자랄_때만_벗긴다()
    {
        var inv = 장비칸이_있는_소지품(4);
        var 등 = 랜턴();
        inv.TryAdd(등, 1);   // 자리로
        inv.TryAdd(등, 1);   // 칸으로

        Assert.AreEqual(2, inv.CountOf("lantern"));

        Assert.IsTrue(inv.TryRemove("lantern", 1));
        Assert.AreSame(등, inv.Equipment.Get(EquipmentSlotKind.Light), "칸에 있는 것이 먼저 나간다");
        Assert.AreEqual(0, inv.CountInSlots("lantern"));

        Assert.IsTrue(inv.TryRemove("lantern", 1));
        Assert.IsTrue(inv.Equipment.IsEmpty(EquipmentSlotKind.Light), "그래도 모자라면 벗긴다");
        Assert.AreEqual(0, inv.CountOf("lantern"));
    }

    [Test]
    public void 모자라면_아무것도_건드리지_않는다()
    {
        var inv = 장비칸이_있는_소지품(4);
        var 등 = 랜턴();
        inv.TryAdd(등, 1);

        Assert.IsFalse(inv.TryRemove("lantern", 2), "둘은 없다");
        Assert.AreSame(등, inv.Equipment.Get(EquipmentSlotKind.Light), "실패했으면 걸린 것은 그대로다");
    }

    // ── 세이브 왕복 ──────────────────────────────────────────

    [Test]
    public void 세이브_왕복을_해도_장비는_자리에_있다()
    {
        var 원본 = 장비칸이_있는_소지품(15);
        var 등 = 랜턴();
        원본.TryAdd(등, 1);
        원본.TryAdd(물건("scrap"), 7);

        // PlayerInventory.CaptureState/RestoreState가 하는 일을 그대로 흉내낸다.
        var 걸린id = 원본.Equipment.GetAt(0)?.id;
        var 칸id = 원본.Slots.Select(s => s.IsEmpty ? "" : s.item.id).ToArray();
        var 칸수 = 원본.Slots.Select(s => s.count).ToArray();

        var 복원 = 장비칸이_있는_소지품(15);
        for (int i = 0; i < 칸id.Length; i++)
        {
            if (string.IsNullOrEmpty(칸id[i])) continue;
            복원.Slots[i].item = 칸id[i] == "scrap" ? 물건("scrap") : 랜턴();
            복원.Slots[i].count = 칸수[i];
        }
        if (!string.IsNullOrEmpty(걸린id)) 복원.Equipment.TryEquipIntoEmpty(랜턴(걸린id));

        Assert.AreEqual("lantern", 걸린id, "저장에 실린다");
        Assert.IsFalse(복원.Equipment.IsEmpty(EquipmentSlotKind.Light), "불러오면 그대로 걸려 있다");
        Assert.AreEqual(7, 복원.CountOf("scrap"));
        Assert.AreEqual(1, 복원.CountOf("lantern"));
    }

    [Test]
    public void 장비_칸이_없던_세이브는_불러올_때_자리로_옮긴다()
    {
        var inv = 장비칸이_있는_소지품(15);

        // 예전 세이브 복원은 TryAdd를 지나지 않고 슬롯에 직접 앉힌다
        inv.Slots[3].item = 랜턴();
        inv.Slots[3].count = 1;

        Assert.AreEqual(1, inv.RehomeEquipment());
        Assert.IsTrue(inv.Slots[3].IsEmpty, "칸을 돌려준다");
        Assert.IsFalse(inv.Equipment.IsEmpty(EquipmentSlotKind.Light));
        Assert.AreEqual(0, inv.RehomeEquipment(), "두 번 불러도 더 옮길 것이 없다");
    }

    // ── 사망 드롭 ────────────────────────────────────────────

    [Test]
    public void 죽어도_걸어_둔_장비는_떨구지_않는다()
    {
        var inv = 장비칸이_있는_소지품(15);
        var 등 = 랜턴();
        inv.TryAdd(등, 1);
        inv.TryAdd(물건("scrap"), 12);

        var 떨군것 = DeathDrop.Extract(inv);

        Assert.AreEqual(1, 떨군것.Count, "벌어온 것만 나간다");
        Assert.AreEqual("scrap", 떨군것[0].item.id);
        Assert.AreSame(등, inv.Equipment.Get(EquipmentSlotKind.Light), "랜턴은 몸에 남는다");
        Assert.AreEqual(1, inv.CountOf("lantern"));
    }

    [Test]
    public void 걸어_둔_장비도_가져_본_것으로_적힌다()
    {
        // 불러온 직후의 소급 기록은 칸만 훑는다. 장비 자리를 안 보면
        // 랜턴을 걸어 둔 사람의 도감·연구 항목이 불러온 뒤에 사라진다.
        var inv = 장비칸이_있는_소지품(4);
        inv.TryAdd(랜턴(), 1);

        var 원장 = new Survive.Progression.UnlockLedger();
        Assert.AreEqual(1, Survive.Progression.HeldRecord.RecordAll(원장, inv));
        Assert.IsTrue(Survive.Progression.HeldRecord.Has(원장, "lantern"));
    }

    // ── 신호 ────────────────────────────────────────────────

    [Test]
    public void 장비가_바뀌면_소지품_화면도_다시_그린다()
    {
        var inv = 장비칸이_있는_소지품(4);
        int 울린수 = 0;
        inv.Changed += () => 울린수++;

        inv.TryAdd(랜턴(), 1);
        Assert.AreEqual(1, 울린수, "습득 한 번에 한 번만 울린다");

        inv.Equipment.Unequip(EquipmentSlotKind.Light);
        Assert.AreEqual(2, 울린수, "장비 자리를 직접 고쳐도 화면이 안다");
    }

    [Test]
    public void 습득_신호는_장비로_들어가도_울린다()
    {
        var inv = 장비칸이_있는_소지품(4);
        ItemDataSO 들어온것 = null;
        int 들어온수 = 0;
        inv.ItemAdded += (item, n) => { 들어온것 = item; 들어온수 = n; };

        var 등 = 랜턴();
        Assert.AreEqual(0, inv.TryAdd(등, 1), "전부 들어갔다");
        Assert.AreSame(등, 들어온것, "첫 습득으로 청사진이 열리는 길이 끊기면 안 된다");
        Assert.AreEqual(1, 들어온수);
    }

    // ── 데이터 ──────────────────────────────────────────────

    [Test]
    public void 랜턴_에셋이_조명_자리로_지정돼_있다()
    {
        var 에셋 = AssetDatabase.LoadAssetAtPath<ItemDataSO>("Assets/08.Data/Items/Lantern.asset");
        Assert.IsNotNull(에셋, "랜턴 아이템 정의가 있다");
        Assert.AreEqual("lantern", 에셋.id);
        Assert.AreEqual(EquipmentSlotKind.Light, 에셋.equipSlot,
                        "랜턴은 장비 자리로 간다 (08.Data/Items/Lantern.asset)");
    }

    [Test]
    public void 통행_장비는_장비_자리로_가지_않는다()
    {
        // 판단 근거는 EquipmentSlots 주석에 있다. 무엇을 챙길지 고르는 판단이
        // 스펙 §11의 알맹이고, 통행 장비가 그 판단이다.
        var 통행 = AssetDatabase.FindAssets("t:TraversalGearItemSO")
                                .Select(AssetDatabase.GUIDToAssetPath)
                                .Select(AssetDatabase.LoadAssetAtPath<TraversalGearItemSO>)
                                .Where(i => i != null && !string.IsNullOrEmpty(i.id))
                                .ToArray();

        Assert.IsNotEmpty(통행, "통행 장비 정의가 있다");
        foreach (var it in 통행)
            Assert.AreEqual(EquipmentSlotKind.None, it.equipSlot,
                            $"{it.id}는 소지품 칸에 남는다");
    }

    [Test]
    public void 기본값은_장비가_아니다()
    {
        var 보통 = ScriptableObject.CreateInstance<ItemDataSO>();
        Assert.AreEqual(EquipmentSlotKind.None, 보통.equipSlot,
                        "표시하지 않은 물건이 조용히 장비가 되면 안 된다");
        Assert.IsFalse(EquipmentSlots.IsEquipment(보통));
    }

    [Test]
    public void 자리_목록에_None은_없다()
    {
        CollectionAssert.DoesNotContain(EquipmentSlots.AllKinds, EquipmentSlotKind.None);
        Assert.AreEqual(EquipmentSlots.AllKinds.Length,
                        EquipmentSlots.AllKinds.Distinct().Count(), "자리는 종류마다 하나씩이다");
    }

    [Test]
    public void 자리를_비우면_전부_벗겨진다()
    {
        var equip = new EquipmentSlots();
        equip.TryEquip(랜턴());

        int 울린수 = 0;
        equip.Changed += () => 울린수++;

        equip.Clear();
        Assert.IsTrue(equip.IsEmpty(EquipmentSlotKind.Light));
        Assert.AreEqual(1, 울린수);

        equip.Clear();
        Assert.AreEqual(1, 울린수, "비어 있는데 또 비우면 아무 일도 없다");
    }
}
