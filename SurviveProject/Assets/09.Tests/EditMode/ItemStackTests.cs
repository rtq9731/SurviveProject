using NUnit.Framework;
using UnityEngine;
using Survive.Items;

/// <summary>
/// ItemStack의 세 파생 값(IsEmpty, RemainingSpace, Clear)의 경계를 못박는다.
/// 인벤토리·제작·건설이 전부 이 세 가지 위에 서 있어서, 여기가 조용히
/// 바뀌면 위쪽 테스트는 통과하는데 게임만 이상해진다.
/// </summary>
public class ItemStackTests
{
    static ItemDataSO 아이템(string id, int maxStack = 99)
    {
        var it = ScriptableObject.CreateInstance<ItemDataSO>();
        it.id = id;
        it.displayName = id;
        it.maxStack = maxStack;
        return it;
    }

    // ── IsEmpty ─────────────────────────────────────────────

    [Test]
    public void 기본_생성자로_만든_스택은_비어있다()
    {
        var s = new ItemStack();
        Assert.IsNull(s.item);
        Assert.AreEqual(0, s.count);
        Assert.IsTrue(s.IsEmpty);
    }

    [Test]
    public void 아이템이_없으면_수량이_있어도_비어있다()
    {
        var s = new ItemStack(null, 5);
        Assert.IsTrue(s.IsEmpty, "아이템 없는 수량은 아무 의미가 없다");
    }

    [Test]
    public void 수량이_영이면_비어있다()
    {
        Assert.IsTrue(new ItemStack(아이템("scrap"), 0).IsEmpty);
    }

    [Test]
    public void 수량이_음수면_비어있다()
    {
        Assert.IsTrue(new ItemStack(아이템("scrap"), -1).IsEmpty);
    }

    [Test]
    public void 수량이_하나면_비어있지_않다()
    {
        Assert.IsFalse(new ItemStack(아이템("scrap"), 1).IsEmpty,
            "1개가 비어있음의 경계다");
    }

    // ── RemainingSpace ──────────────────────────────────────

    [Test]
    public void 아이템이_없으면_남는_자리도_없다()
    {
        Assert.AreEqual(0, new ItemStack().RemainingSpace);
        Assert.AreEqual(0, new ItemStack(null, 5).RemainingSpace,
            "아이템이 없으면 maxStack을 알 수 없으니 0이다");
    }

    [Test]
    public void 빈_스택의_남는_자리는_최대치_전부다()
    {
        Assert.AreEqual(99, new ItemStack(아이템("scrap", 99), 0).RemainingSpace);
    }

    [Test]
    public void 절반_찬_스택은_나머지만큼_남는다()
    {
        Assert.AreEqual(4, new ItemStack(아이템("scrap", 10), 6).RemainingSpace);
    }

    [Test]
    public void 가득_찬_스택은_남는_자리가_없다()
    {
        Assert.AreEqual(0, new ItemStack(아이템("scrap", 10), 10).RemainingSpace);
    }

    [Test]
    public void 최대치가_하나면_한_개만_넣어도_가득_찬다()
    {
        var 곡괭이 = 아이템("pickaxe", 1);
        Assert.AreEqual(1, new ItemStack(곡괭이, 0).RemainingSpace);
        Assert.AreEqual(0, new ItemStack(곡괭이, 1).RemainingSpace);
    }

    [Test]
    public void 최대치를_넘긴_스택은_남는_자리가_음수다()
    {
        // 현재 동작을 못박아 둔다 — 클램프하지 않는다.
        // 넘긴 상태는 애초에 만들어지면 안 되는 것이므로, 0으로 감춰서
        // "더 넣어도 된다"고 답하는 것보다 음수로 드러나는 편이 낫다.
        Assert.AreEqual(-3, new ItemStack(아이템("scrap", 10), 13).RemainingSpace);
    }

    [Test]
    public void 최대치가_영인_아이템은_남는_자리가_없다()
    {
        Assert.AreEqual(0, new ItemStack(아이템("잘못된아이템", 0), 0).RemainingSpace);
    }

    // ── Clear ───────────────────────────────────────────────

    [Test]
    public void 비우면_아이템과_수량이_함께_사라진다()
    {
        var s = new ItemStack(아이템("scrap", 99), 42);
        s.Clear();

        Assert.IsNull(s.item, "아이템 참조를 남기면 슬롯이 비어보이는데 물건은 붙어 있다");
        Assert.AreEqual(0, s.count);
        Assert.IsTrue(s.IsEmpty);
        Assert.AreEqual(0, s.RemainingSpace);
    }

    [Test]
    public void 이미_빈_스택을_비워도_아무_일도_없다()
    {
        var s = new ItemStack();
        Assert.DoesNotThrow(() => s.Clear());
        Assert.IsTrue(s.IsEmpty);
    }

    [Test]
    public void 여러_번_비워도_같은_상태다()
    {
        var s = new ItemStack(아이템("scrap"), 3);
        s.Clear();
        s.Clear();

        Assert.IsNull(s.item);
        Assert.AreEqual(0, s.count);
    }

    [Test]
    public void 비운_뒤_다시_채울_수_있다()
    {
        var s = new ItemStack(아이템("scrap", 99), 3);
        s.Clear();

        s.item = 아이템("mushroom", 20);
        s.count = 7;

        Assert.IsFalse(s.IsEmpty);
        Assert.AreEqual(13, s.RemainingSpace);
    }
}
