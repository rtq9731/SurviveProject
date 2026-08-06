using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Survive.Items;
using Survive.World;

/// <summary>
/// P2 스펙 §8-2 "장착·판정 연결" 중 순수한 부분 — 인벤토리에 있는 것이
/// <see cref="GearCapability"/> 목록이 되어 <see cref="EnvironmentThreat"/>에 닿는 경로.
///
/// 장착 규칙은 <b>보유가 곧 장착</b>이다(랜턴이 잡은 규칙). 그래서 여기 테스트는
/// 전부 "인벤토리에 넣었는가 / 뺐는가"로 장착 상태를 만든다.
/// </summary>
public class TraversalLoadoutTests
{
    static TraversalGearItemSO 장비(string id, TraversalGear gear, float capacity)
    {
        var it = ScriptableObject.CreateInstance<TraversalGearItemSO>();
        it.id = id;
        it.displayName = id;
        it.maxStack = 1;
        it.category = ItemCategory.Tool;
        it.gear = gear;
        it.capacity = capacity;
        return it;
    }

    static ItemDataSO 자원(string id)
    {
        var it = ScriptableObject.CreateInstance<ItemDataSO>();
        it.id = id;
        it.displayName = id;
        it.maxStack = 99;
        return it;
    }

    static Inventory 인벤토리(params ItemDataSO[] 넣을것)
    {
        var inv = new Inventory(10);
        foreach (var it in 넣을것) inv.TryAdd(it, 1);
        return inv;
    }

    // ── 목록 만들기 ──────────────────────────────────────────

    [Test]
    public void 빈_인벤토리는_아무_장비도_내놓지_않는다()
    {
        Assert.IsEmpty(TraversalLoadout.From(인벤토리()));
    }

    [Test]
    public void 인벤토리가_null이어도_터지지_않는다()
    {
        Assert.IsEmpty(TraversalLoadout.From(null));
    }

    [Test]
    public void 이동_장비가_아닌_것은_걸러진다()
    {
        var inv = 인벤토리(자원("scrap"), 자원("alien_alloy"));
        Assert.IsEmpty(TraversalLoadout.From(inv), "자원이 장비로 둔갑하면 관문이 열려 버린다");
    }

    [Test]
    public void 가지고_있는_장비의_종류와_용량을_그대로_내놓는다()
    {
        var inv = 인벤토리(장비("surface_walker", TraversalGear.SurfaceWalker, 36f));
        var loadout = TraversalLoadout.From(inv);

        Assert.AreEqual(1, loadout.Count);
        Assert.AreEqual(TraversalGear.SurfaceWalker, loadout[0].Gear);
        Assert.AreEqual(36f, loadout[0].Capacity, 0.001f);
    }

    [Test]
    public void 종류가_None인_장비는_담지_않는다()
    {
        var inv = 인벤토리(장비("쓸모없는것", TraversalGear.None, 99f));
        Assert.IsEmpty(TraversalLoadout.From(inv), "None은 어떤 위협과도 짝이 없다");
    }

    [Test]
    public void 용량이_0인_장비도_담는다()
    {
        // 담아야 판정이 "장비가 없다"가 아니라 "모자라다"로 나온다 — 플레이어에게 다른 말이다.
        var inv = 인벤토리(장비("고장난보행기", TraversalGear.SurfaceWalker, 0f));
        Assert.AreEqual(1, TraversalLoadout.From(inv).Count);
    }

    [Test]
    public void 여러_장비를_동시에_내놓는다()
    {
        var inv = 인벤토리(
            장비("surface_walker", TraversalGear.SurfaceWalker, 36f),
            장비("lantern_gear", TraversalGear.Lantern, 12f));

        Assert.AreEqual(2, TraversalLoadout.From(inv).Count);
    }

    [Test]
    public void Collect는_기존_목록을_비우지_않는다()
    {
        // 수영과 랜턴은 출처가 인벤토리가 아니다. 다른 시스템이 얹은 것을 지우면 안 된다.
        var into = new List<GearCapability> { new GearCapability(TraversalGear.Swimming, 40f) };
        TraversalLoadout.Collect(인벤토리(장비("sw", TraversalGear.SurfaceWalker, 36f)), into);

        Assert.AreEqual(2, into.Count);
        Assert.AreEqual(TraversalGear.Swimming, into[0].Gear, "먼저 있던 것이 그대로 앞에 남는다");
    }

    // ── 판정까지 이어지는가 ──────────────────────────────────

    static readonly HazardZone 액면 = new HazardZone(EnvironmentHazard.MacroniumSurface, 30f);

    [Test]
    public void 장비가_없으면_액면에서_막힌다()
    {
        var 판정 = TraversalLoadout.Evaluate(액면, 인벤토리(자원("scrap")));

        Assert.IsFalse(판정.CanPass);
        Assert.AreEqual(PassageResult.MissingGear, 판정.Result);
        Assert.AreEqual(TraversalGear.SurfaceWalker, 판정.RequiredGear, "무엇을 만들어야 하는지 답해야 한다");
    }

    [Test]
    public void 장비를_가지면_액면을_지난다()
    {
        var 판정 = TraversalLoadout.Evaluate(액면, 인벤토리(장비("sw", TraversalGear.SurfaceWalker, 36f)));
        Assert.IsTrue(판정.CanPass);
    }

    [Test]
    public void 용량이_폭과_같으면_지난다()
    {
        var 판정 = TraversalLoadout.Evaluate(액면, 인벤토리(장비("sw", TraversalGear.SurfaceWalker, 30f)));
        Assert.IsTrue(판정.CanPass, "경계값은 통과다");
    }

    [Test]
    public void 용량이_모자라면_얼마나_모자란지_답한다()
    {
        var 판정 = TraversalLoadout.Evaluate(액면, 인벤토리(장비("sw", TraversalGear.SurfaceWalker, 22f)));

        Assert.IsFalse(판정.CanPass);
        Assert.AreEqual(PassageResult.NotEnough, 판정.Result);
        Assert.AreEqual(8f, 판정.Shortfall, 0.001f);
    }

    [Test]
    public void 다른_위협을_뚫는_장비로는_액면을_못_지난다()
    {
        var 판정 = TraversalLoadout.Evaluate(액면, 인벤토리(장비("swim", TraversalGear.Swimming, 999f)));

        Assert.IsFalse(판정.CanPass, "수영으로 액면을 건널 수 있으면 티어가 무너진다");
        Assert.AreEqual(PassageResult.MissingGear, 판정.Result);
    }

    [Test]
    public void 장비를_버리면_다시_막힌다()
    {
        var 보행기 = 장비("surface_walker", TraversalGear.SurfaceWalker, 36f);
        var inv = 인벤토리(보행기);
        Assert.IsTrue(TraversalLoadout.Evaluate(액면, inv).CanPass);

        inv.TryRemove("surface_walker", 1);
        Assert.IsFalse(TraversalLoadout.Evaluate(액면, inv).CanPass, "보유가 곧 장착이므로 잃으면 바로 막힌다");
    }

    [Test]
    public void 같은_장비가_둘이면_좋은_쪽을_쓴다()
    {
        var inv = new Inventory(10);
        inv.TryAdd(장비("낡은보행기", TraversalGear.SurfaceWalker, 12f), 1);
        inv.TryAdd(장비("새보행기", TraversalGear.SurfaceWalker, 36f), 1);

        Assert.IsTrue(TraversalLoadout.Evaluate(액면, inv).CanPass, "중복은 합산이 아니라 최댓값이다");
    }
}
