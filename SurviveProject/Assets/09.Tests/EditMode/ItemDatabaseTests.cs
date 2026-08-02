using NUnit.Framework;
using UnityEngine;
using Survive.Items;

public class ItemDatabaseTests
{
    static ItemDataSO 아이템만들기(string id, int maxStack = 1)
    {
        var it = ScriptableObject.CreateInstance<ItemDataSO>();
        it.id = id;
        it.displayName = id;
        it.maxStack = maxStack;
        return it;
    }

    static ItemDatabaseSO DB만들기(params ItemDataSO[] items)
    {
        var db = ScriptableObject.CreateInstance<ItemDatabaseSO>();
        db.items = items;
        return db;
    }

    [Test]
    public void id로_아이템을_찾는다()
    {
        var scrap = 아이템만들기("scrap");
        var db = DB만들기(scrap, 아이템만들기("pickaxe"));
        Assert.AreSame(scrap, db.GetById("scrap"));
    }

    [Test]
    public void 없는_id는_null을_돌려준다()
    {
        var db = DB만들기(아이템만들기("scrap"));
        Assert.IsNull(db.GetById("없는아이템"));
    }

    [Test]
    public void TryGetById는_없으면_false다()
    {
        var db = DB만들기(아이템만들기("scrap"));
        Assert.IsFalse(db.TryGetById("없는아이템", out var found));
        Assert.IsNull(found);
    }

    [Test]
    public void 중복_id를_검증에서_보고한다()
    {
        var db = DB만들기(아이템만들기("scrap"), 아이템만들기("scrap"));
        var 문제 = db.Validate();
        Assert.AreEqual(1, 문제.Count);
        StringAssert.Contains("scrap", 문제[0]);
    }

    [Test]
    public void 빈_id를_검증에서_보고한다()
    {
        var db = DB만들기(아이템만들기(""));
        Assert.AreEqual(1, db.Validate().Count);
    }

    [Test]
    public void null_항목을_검증에서_보고한다()
    {
        var db = DB만들기(아이템만들기("scrap"), null);
        Assert.AreEqual(1, db.Validate().Count);
    }

    [Test]
    public void 문제가_없으면_검증결과가_비어있다()
    {
        var db = DB만들기(아이템만들기("scrap"), 아이템만들기("pickaxe"));
        Assert.AreEqual(0, db.Validate().Count);
    }
}
