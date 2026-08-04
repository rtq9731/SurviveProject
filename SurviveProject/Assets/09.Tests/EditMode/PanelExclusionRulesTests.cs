using System.Collections.Generic;
using NUnit.Framework;
using Survive.UI;

/// <summary>
/// 백로그 17 — 같이 떠 있어도 되는 패널 조합을 적은 표.
/// 규칙 엔진 자체의 경계값과, 이 게임이 실제로 쓰는 기본 규칙 둘 다 확인한다.
/// 기본 규칙은 패널 코드에 흩어져 있던 동작을 옮겨 온 것이라, 여기 적힌 답이
/// 곧 화면에서 보이던 동작이어야 한다.
/// </summary>
public class PanelExclusionRulesTests
{
    // ── 규칙 엔진 ───────────────────────────────────────────────────────

    [Test]
    public void 규칙이_없으면_무엇이든_같이_뜬다()
    {
        var r = new PanelExclusionRules();
        Assert.IsTrue(r.CanCoexist(UIPanelKind.Storage, UIPanelKind.HandCrafting));
        Assert.IsTrue(r.CanCoexist(UIPanelKind.Inventory, UIPanelKind.BuildMenu));
        Assert.IsFalse(r.ClosesTogether(UIPanelKind.Inventory, UIPanelKind.HandCrafting));
    }

    [Test]
    public void 밀어내기는_적은_방향으로만_간다()
    {
        var r = new PanelExclusionRules().Suppresses(UIPanelKind.Storage, UIPanelKind.HandCrafting);

        Assert.IsFalse(r.CanCoexist(UIPanelKind.Storage, UIPanelKind.HandCrafting),
            "보관함이 열리면 손 제작은 닫힌다");
        Assert.IsTrue(r.CanCoexist(UIPanelKind.HandCrafting, UIPanelKind.Storage),
            "반대로는 아니다 — 보관함이 제 손으로 부른 목록에 밀려 닫히면 안 된다");
    }

    [Test]
    public void 상호_배타는_양쪽_모두를_적는다()
    {
        var r = new PanelExclusionRules().Mutual(UIPanelKind.Inventory, UIPanelKind.BuildMenu);

        Assert.IsFalse(r.CanCoexist(UIPanelKind.Inventory, UIPanelKind.BuildMenu));
        Assert.IsFalse(r.CanCoexist(UIPanelKind.BuildMenu, UIPanelKind.Inventory));
    }

    [Test]
    public void 자기_자신을_밀어내는_규칙은_받아들이지_않는다()
    {
        // 받아들이면 열자마자 스스로 닫힌다.
        var r = new PanelExclusionRules().Suppresses(UIPanelKind.Inventory, UIPanelKind.Inventory);
        Assert.IsTrue(r.CanCoexist(UIPanelKind.Inventory, UIPanelKind.Inventory));
    }

    [Test]
    public void 이름_없는_패널은_규칙에_얽히지_않는다()
    {
        var r = new PanelExclusionRules()
            .Suppresses(UIPanelKind.None, UIPanelKind.Inventory)
            .Suppresses(UIPanelKind.Inventory, UIPanelKind.None)
            .ClosesWith(UIPanelKind.None, UIPanelKind.Inventory);

        Assert.IsTrue(r.CanCoexist(UIPanelKind.None, UIPanelKind.Inventory));
        Assert.IsTrue(r.CanCoexist(UIPanelKind.Inventory, UIPanelKind.None));
        Assert.IsFalse(r.ClosesTogether(UIPanelKind.None, UIPanelKind.Inventory));
    }

    [Test]
    public void 딸려_닫기도_방향이_있다()
    {
        var r = new PanelExclusionRules().ClosesWith(UIPanelKind.Inventory, UIPanelKind.HandCrafting);

        Assert.IsTrue(r.ClosesTogether(UIPanelKind.Inventory, UIPanelKind.HandCrafting));
        Assert.IsFalse(r.ClosesTogether(UIPanelKind.HandCrafting, UIPanelKind.Inventory));
        Assert.IsTrue(r.CanCoexist(UIPanelKind.Inventory, UIPanelKind.HandCrafting),
            "딸려 닫힌다는 것과 같이 못 뜬다는 것은 다른 말이다");
    }

    // ── 열릴 때 닫을 것 고르기 ──────────────────────────────────────────

    [Test]
    public void 열린_것_중_걸리는_것만_고른다()
    {
        var r = PanelExclusionRules.CreateDefault();
        var 열린것 = new List<UIPanelKind>
        {
            UIPanelKind.Inventory, UIPanelKind.HandCrafting, UIPanelKind.BuildMenu
        };
        var 닫을것 = new List<UIPanelKind>();

        r.CollectSuppressed(UIPanelKind.Storage, 열린것, 닫을것);

        CollectionAssert.AreEqual(new[] { UIPanelKind.HandCrafting }, 닫을것);
    }

    [Test]
    public void 같은_것이_두_번_열려_있어도_한_번만_고른다()
    {
        var r = PanelExclusionRules.CreateDefault();
        var 닫을것 = new List<UIPanelKind>();

        r.CollectSuppressed(UIPanelKind.Storage,
            new[] { UIPanelKind.HandCrafting, UIPanelKind.HandCrafting }, 닫을것);

        Assert.AreEqual(1, 닫을것.Count);
    }

    [Test]
    public void 고른_목록은_비우지_않고_덧붙인다()
    {
        var r = PanelExclusionRules.CreateDefault();
        var 닫을것 = new List<UIPanelKind> { UIPanelKind.BuildMenu };

        r.CollectSuppressed(UIPanelKind.Storage, new[] { UIPanelKind.HandCrafting }, 닫을것);

        CollectionAssert.AreEqual(new[] { UIPanelKind.BuildMenu, UIPanelKind.HandCrafting }, 닫을것);
    }

    [Test]
    public void 열린_것이_없으면_고를_것도_없다()
    {
        var r = PanelExclusionRules.CreateDefault();
        var 닫을것 = new List<UIPanelKind>();

        r.CollectSuppressed(UIPanelKind.Storage, new UIPanelKind[0], 닫을것);
        r.CollectSuppressed(UIPanelKind.Storage, null, 닫을것);
        r.CollectSuppressed(UIPanelKind.Storage, new[] { UIPanelKind.HandCrafting }, null);

        Assert.AreEqual(0, 닫을것.Count);
    }

    // ── 이 게임의 기본 규칙 (옮겨 오기 전 동작 그대로) ──────────────────

    [Test]
    public void 보관함은_손_제작을_밀어낸다()
    {
        var r = PanelExclusionRules.CreateDefault();
        Assert.IsFalse(r.CanCoexist(UIPanelKind.Storage, UIPanelKind.HandCrafting));
    }

    [Test]
    public void 보관함은_제작대_목록은_밀어내지_않는다()
    {
        // 제작대 목록은 제작대 앞에서만 뜬다. 상자 때문에 닫힐 이유가 없다.
        var r = PanelExclusionRules.CreateDefault();
        Assert.IsTrue(r.CanCoexist(UIPanelKind.Storage, UIPanelKind.StationCrafting));
    }

    [Test]
    public void 손_제작이_열려도_보관함은_닫히지_않는다()
    {
        // 보관함은 열리면서 소지품을 같이 열고, 소지품이 손 제작을 딸고 온다.
        // 양방향이면 그 순간 보관함이 사라진다.
        var r = PanelExclusionRules.CreateDefault();
        Assert.IsTrue(r.CanCoexist(UIPanelKind.HandCrafting, UIPanelKind.Storage));
    }

    [Test]
    public void 보관함과_소지품은_같이_뜬다()
    {
        // 옮길 대상이 양쪽에 보여야 옮길 수 있다.
        var r = PanelExclusionRules.CreateDefault();
        Assert.IsTrue(r.CanCoexist(UIPanelKind.Storage, UIPanelKind.Inventory));
        Assert.IsTrue(r.CanCoexist(UIPanelKind.Inventory, UIPanelKind.Storage));
    }

    [Test]
    public void 소지품을_닫으면_손_제작도_닫힌다()
    {
        var r = PanelExclusionRules.CreateDefault();
        Assert.IsTrue(r.ClosesTogether(UIPanelKind.Inventory, UIPanelKind.HandCrafting));
    }

    [Test]
    public void 소지품을_닫아도_제작대_목록은_남는다()
    {
        var r = PanelExclusionRules.CreateDefault();
        Assert.IsFalse(r.ClosesTogether(UIPanelKind.Inventory, UIPanelKind.StationCrafting));
    }

    [Test]
    public void 건설_목록은_아무것과도_부딪히지_않는다()
    {
        var r = PanelExclusionRules.CreateDefault();
        foreach (UIPanelKind 남 in System.Enum.GetValues(typeof(UIPanelKind)))
        {
            Assert.IsTrue(r.CanCoexist(UIPanelKind.BuildMenu, 남), $"{남}을 밀어내면 안 된다");
            Assert.IsTrue(r.CanCoexist(남, UIPanelKind.BuildMenu), $"{남}에 밀려 닫히면 안 된다");
        }
    }

    [Test]
    public void 기본_규칙은_부를_때마다_새로_만든다()
    {
        // 하나를 돌려쓰면 어딘가에서 덧붙인 규칙이 다른 곳까지 따라간다.
        var a = PanelExclusionRules.CreateDefault();
        var b = PanelExclusionRules.CreateDefault();

        a.Mutual(UIPanelKind.Inventory, UIPanelKind.BuildMenu);

        Assert.IsTrue(b.CanCoexist(UIPanelKind.Inventory, UIPanelKind.BuildMenu));
    }
}
