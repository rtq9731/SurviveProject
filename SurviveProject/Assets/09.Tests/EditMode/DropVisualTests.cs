using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Survive.Items;

/// <summary>
/// 바닥에 떨어진 것이 무엇인지 보이는가.
///
/// 예전에는 프리팹이 없는 스무 종이 전부 같은 베이지색 큐브로 떨어졌다.
/// 지금은 인벤토리 아이콘을 세우므로 종류가 구별된다 — 단, <b>아이콘이 있을 때만</b>이다.
/// 아이템 하나에 아이콘을 빠뜨리면 그 아이템만 조용히 알 수 없는 덩어리로 돌아간다.
/// 그래서 규칙 자체와 실제 데이터베이스를 둘 다 여기서 본다.
/// </summary>
public class DropVisualTests
{
    const string DatabasePath = "Assets/08.Data/Items/ItemDatabase.asset";

    static ItemDatabaseSO Database =>
        AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(DatabasePath);

    static ItemDataSO 아이템만들기(string id, Sprite icon = null, GameObject worldPrefab = null)
    {
        var it = ScriptableObject.CreateInstance<ItemDataSO>();
        it.id = id;
        it.displayName = id;
        it.icon = icon;
        it.worldPrefab = worldPrefab;
        return it;
    }

    static Sprite 아이콘만들기()
    {
        var tex = new Texture2D(4, 4);
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
    }

    // ── 규칙 ─────────────────────────────────────────────────

    [Test]
    public void worldPrefab이_있으면_그것이_이긴다()
    {
        var item = 아이템만들기("x", 아이콘만들기(), new GameObject("모델"));
        Assert.AreEqual(DropVisualKind.WorldPrefab, DropVisualRule.Choose(item, true));
    }

    [Test]
    public void worldPrefab이_없으면_떨구는_쪽의_프리팹을_쓴다()
    {
        var item = 아이템만들기("x", 아이콘만들기());
        Assert.AreEqual(DropVisualKind.SpawnerPrefab, DropVisualRule.Choose(item, true));
    }

    [Test]
    public void 프리팹이_하나도_없으면_아이콘을_세운다()
    {
        var item = 아이템만들기("x", 아이콘만들기());
        Assert.AreEqual(DropVisualKind.IconBillboard, DropVisualRule.Choose(item, false));
    }

    [Test]
    public void 아이콘도_없으면_마지막_수단으로_떨어진다()
    {
        var item = 아이템만들기("x");
        Assert.AreEqual(DropVisualKind.Fallback, DropVisualRule.Choose(item, false));
        Assert.IsFalse(DropVisualRule.IsRecognizable(DropVisualKind.Fallback));
    }

    [Test]
    public void 아이템이_없으면_마지막_수단이다()
    {
        Assert.AreEqual(DropVisualKind.Fallback, DropVisualRule.Choose(null, true));
    }

    [Test]
    public void 마지막_수단만_알아볼_수_없다()
    {
        Assert.IsTrue(DropVisualRule.IsRecognizable(DropVisualKind.WorldPrefab));
        Assert.IsTrue(DropVisualRule.IsRecognizable(DropVisualKind.SpawnerPrefab));
        Assert.IsTrue(DropVisualRule.IsRecognizable(DropVisualKind.IconBillboard));
    }

    // ── 어둠 ─────────────────────────────────────────────────

    [Test]
    public void 낙하물이_조명이_되지_않는다()
    {
        // 이 게임의 대원칙은 어둠을 지키는 것이다. 발광이 1을 넘으면
        // 낙하물이 스스로 주변을 밝히기 시작하고, 아이콘은 흰 덩어리로 뭉개진다.
        Assert.Less(DropVisualRule.IconEmission, 1f,
                    "발광이 1 이상이면 낙하물이 광원이 된다");
        Assert.Greater(DropVisualRule.IconEmission, 0f,
                       "발광이 0이면 어둠 속에서 아무것도 보이지 않는다");
    }

    [Test]
    public void 아이콘_판이_사람_키만_해지지_않는다()
    {
        Assert.Greater(DropVisualRule.IconSize, 0.1f, "너무 작으면 멀리서 못 읽는다");
        Assert.Less(DropVisualRule.IconSize, 0.8f, "무릎보다 커지면 주울 물건으로 안 보인다");
    }

    // ── 실제 데이터 ──────────────────────────────────────────

    [Test]
    public void 어떤_아이템도_알_수_없는_덩어리로_떨어지지_않는다()
    {
        var db = Database;
        Assert.IsNotNull(db, DatabasePath + "를 찾지 못했다");
        Assert.IsNotEmpty(db.items, "아이템이 하나도 없다");

        var 알수없는것 = db.items
            .Where(it => it != null)
            .Where(it => !DropVisualRule.IsRecognizable(DropVisualRule.Choose(it, false)))
            .Select(it => it.id)
            .ToList();

        Assert.IsEmpty(알수없는것,
            "겉모습이 없어 같은 덩어리로 떨어지는 아이템: " + string.Join(", ", 알수없는것));
    }

    [Test]
    public void 아이콘이_빠진_아이템이_없다()
    {
        // 인벤토리는 아이콘이 없으면 첫 글자로 때운다. 바닥에서는 때울 것조차 없다.
        var 없는것 = Database.items
            .Where(it => it != null && it.icon == null)
            .Select(it => it.id)
            .ToList();

        Assert.IsEmpty(없는것, "아이콘이 없는 아이템: " + string.Join(", ", 없는것));
    }

    [Test]
    public void 프리팹이_있는_아이템은_계속_프리팹으로_떨어진다()
    {
        // 손으로 만든 3D 모델이 아이콘 판으로 대체되면 오히려 후퇴다.
        var 프리팹있음 = Database.items
            .Where(it => it != null && it.worldPrefab != null)
            .ToList();

        Assert.IsNotEmpty(프리팹있음, "프리팹을 가진 아이템이 하나도 없다");
        foreach (var it in 프리팹있음)
            Assert.AreEqual(DropVisualKind.WorldPrefab, DropVisualRule.Choose(it, false),
                            it.id + "이(가) 프리팹을 두고 다른 겉모습으로 떨어진다");
    }
}
