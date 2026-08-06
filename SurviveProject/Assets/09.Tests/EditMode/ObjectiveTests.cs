using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Survive.Items;
using Survive.Progression;

public class ObjectiveTests
{
    class 가짜상태 : IObjectiveContext
    {
        public Inventory inv = new Inventory(10);
        public Dictionary<string, int> flags = new Dictionary<string, int>();

        public Inventory PlayerInventory => inv;
        public int GetFlag(string key) => flags.TryGetValue(key, out var v) ? v : 0;
    }

    static ItemDataSO 아이템(string id)
    {
        var it = ScriptableObject.CreateInstance<ItemDataSO>();
        it.id = id;
        it.maxStack = 999;
        return it;
    }

    [Test]
    public void 수집목표는_보유량에_비례해_진행된다()
    {
        var o = ScriptableObject.CreateInstance<CollectItemObjective>();
        o.itemId = "scrap";
        o.amount = 12;

        var ctx = new 가짜상태();
        Assert.AreEqual(0f, o.Evaluate(ctx), 0.001f);

        ctx.inv.TryAdd(아이템("scrap"), 6);
        Assert.AreEqual(0.5f, o.Evaluate(ctx), 0.001f);
        Assert.IsFalse(o.IsComplete(ctx));

        ctx.inv.TryAdd(아이템("scrap"), 6);
        Assert.AreEqual(1f, o.Evaluate(ctx), 0.001f);
        Assert.IsTrue(o.IsComplete(ctx));
    }

    [Test]
    public void 수집목표는_초과해도_1을_넘지_않는다()
    {
        var o = ScriptableObject.CreateInstance<CollectItemObjective>();
        o.itemId = "scrap";
        o.amount = 5;

        var ctx = new 가짜상태();
        ctx.inv.TryAdd(아이템("scrap"), 50);
        Assert.AreEqual(1f, o.Evaluate(ctx), 0.001f);
    }

    [Test]
    public void 플래그목표는_플래그가_서면_완료된다()
    {
        var o = ScriptableObject.CreateInstance<FlagObjective>();
        o.flagKey = "reached_mushroom_grove";

        var ctx = new 가짜상태();
        Assert.IsFalse(o.IsComplete(ctx));

        ctx.flags["reached_mushroom_grove"] = 1;
        Assert.IsTrue(o.IsComplete(ctx));
    }

    [Test]
    public void 플래그목표는_필요_횟수를_반영한다()
    {
        var o = ScriptableObject.CreateInstance<FlagObjective>();
        o.flagKey = "salvaged_parts";
        o.requiredCount = 4;

        var ctx = new 가짜상태();
        ctx.flags["salvaged_parts"] = 2;
        Assert.AreEqual(0.5f, o.Evaluate(ctx), 0.001f);
    }

    [Test]
    public void 처치목표는_kill_접두사_플래그를_읽는다()
    {
        var o = ScriptableObject.CreateInstance<KillCreatureObjective>();
        o.creatureId = "eye";
        o.amount = 3;

        var ctx = new 가짜상태();
        ctx.flags["kill:eye"] = 3;
        Assert.IsTrue(o.IsComplete(ctx));
    }

    [Test]
    public void 처치목표는_다른_생물의_처치를_세지_않는다()
    {
        var o = ScriptableObject.CreateInstance<KillCreatureObjective>();
        o.creatureId = "eye";
        o.amount = 1;

        var ctx = new 가짜상태();
        ctx.flags["kill:ball"] = 5;
        Assert.IsFalse(o.IsComplete(ctx));
    }

    [Test]
    public void null_상태는_0이다()
    {
        var o = ScriptableObject.CreateInstance<CollectItemObjective>();
        o.itemId = "scrap";
        Assert.AreEqual(0f, o.Evaluate(null), 0.001f);

        var f = ScriptableObject.CreateInstance<FlagObjective>();
        f.flagKey = "x";
        Assert.AreEqual(0f, f.Evaluate(null), 0.001f);
    }

    [Test]
    public void id가_비면_0이다()
    {
        var o = ScriptableObject.CreateInstance<CollectItemObjective>();
        o.itemId = "";
        Assert.AreEqual(0f, o.Evaluate(new 가짜상태()), 0.001f);
    }
}
