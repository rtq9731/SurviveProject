using NUnit.Framework;
using UnityEngine;
using Survive.Harvesting;
using Survive.Items;

public class LootTableTests
{
    static ItemDataSO 아이템(string id)
    {
        var it = ScriptableObject.CreateInstance<ItemDataSO>();
        it.id = id;
        it.displayName = id;
        it.maxStack = 99;
        return it;
    }

    static LootTableSO 표(params LootTableSO.Entry[] entries)
    {
        var t = ScriptableObject.CreateInstance<LootTableSO>();
        t.entries = entries;
        return t;
    }

    static LootTableSO.Entry 항목(string id, int min, int max, float chance) =>
        new LootTableSO.Entry { item = 아이템(id), minCount = min, maxCount = max, chance = chance };

    [Test]
    public void 확률_1이면_항상_나온다()
    {
        var t = 표(항목("scrap", 2, 2, 1f));
        for (int seed = 0; seed < 20; seed++)
        {
            var r = t.Roll(new System.Random(seed));
            Assert.AreEqual(1, r.Count, "seed " + seed);
            Assert.AreEqual(2, r[0].count);
        }
    }

    [Test]
    public void 확률_0이면_절대_나오지_않는다()
    {
        var t = 표(항목("scrap", 1, 5, 0f));
        for (int seed = 0; seed < 20; seed++)
            Assert.AreEqual(0, t.Roll(new System.Random(seed)).Count, "seed " + seed);
    }

    [Test]
    public void min과_max가_같으면_그_값이_나온다()
    {
        var t = 표(항목("scrap", 3, 3, 1f));
        Assert.AreEqual(3, t.Roll(new System.Random(1))[0].count);
    }

    [Test]
    public void 개수는_min과_max_사이다()
    {
        var t = 표(항목("scrap", 2, 5, 1f));
        for (int seed = 0; seed < 50; seed++)
        {
            int c = t.Roll(new System.Random(seed))[0].count;
            Assert.GreaterOrEqual(c, 2);
            Assert.LessOrEqual(c, 5);
        }
    }

    [Test]
    public void 같은_시드는_같은_결과를_준다()
    {
        var t = 표(항목("scrap", 1, 9, 0.5f), 항목("part", 1, 3, 0.5f));
        var a = t.Roll(new System.Random(42));
        var b = t.Roll(new System.Random(42));

        Assert.AreEqual(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++)
        {
            Assert.AreEqual(a[i].item.id, b[i].item.id);
            Assert.AreEqual(a[i].count, b[i].count);
        }
    }

    [Test]
    public void 항목마다_독립적으로_굴린다()
    {
        var t = 표(항목("a", 1, 1, 1f), 항목("b", 1, 1, 1f));
        Assert.AreEqual(2, t.Roll(new System.Random(0)).Count);
    }

    [Test]
    public void min과_max가_뒤바뀌어도_처리한다()
    {
        var t = 표(항목("scrap", 5, 2, 1f));
        int c = t.Roll(new System.Random(3))[0].count;
        Assert.GreaterOrEqual(c, 2);
        Assert.LessOrEqual(c, 5);
    }

    [Test]
    public void null_항목과_null_아이템은_건너뛴다()
    {
        var t = ScriptableObject.CreateInstance<LootTableSO>();
        t.entries = new[] { null, new LootTableSO.Entry { item = null, chance = 1f }, 항목("scrap", 1, 1, 1f) };
        Assert.AreEqual(1, t.Roll(new System.Random(0)).Count);
    }

    [Test]
    public void 개수가_0이면_결과에_넣지_않는다()
    {
        var t = 표(항목("scrap", 0, 0, 1f));
        Assert.AreEqual(0, t.Roll(new System.Random(0)).Count);
    }

    [Test]
    public void rng가_null이어도_동작한다()
    {
        var t = 표(항목("scrap", 1, 1, 1f));
        Assert.DoesNotThrow(() => t.Roll(null));
    }
}
