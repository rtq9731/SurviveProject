using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Survive.Items;
using Survive.Localization;
using Survive.UI;

/// <summary>
/// 획득 알림 목록의 규칙. <b>Unity를 켜지 않고</b> 전부 판정한다 —
/// 시각을 인자로 받으므로 5초 뒤에 사라지는지를 5초 기다리지 않고 물을 수 있다.
/// </summary>
public class PickupFeedTests
{
    static ItemDataSO 아이템(string id)
    {
        var it = ScriptableObject.CreateInstance<ItemDataSO>();
        it.id = id;
        it.displayName = id;
        it.maxStack = 99;
        return it;
    }

    static PickupFeed 목록(int maxRows = 5, float lifetime = 5f, float window = 3f) =>
        new PickupFeed(maxRows, lifetime, window);

    // ── 기본 ─────────────────────────────────────────────────

    [Test]
    public void 아무것도_안_주웠으면_목록이_비어_있다()
    {
        var feed = 목록();
        Assert.AreEqual(0, feed.Count);
        Assert.IsEmpty(feed.Rows);
    }

    [Test]
    public void 하나_주우면_한_줄이_생긴다()
    {
        var feed = 목록();
        var scrap = 아이템("scrap");

        Assert.IsTrue(feed.Add(scrap, 1, 0f));
        Assert.AreEqual(1, feed.Count);
        Assert.AreSame(scrap, feed.Rows[0].Item);
        Assert.AreEqual(1, feed.Rows[0].Count);
    }

    [Test]
    public void 다른_것을_주우면_새_줄이_된다()
    {
        var feed = 목록();
        feed.Add(아이템("scrap"), 1, 0f);
        feed.Add(아이템("wood"), 1, 0.2f);

        Assert.AreEqual(2, feed.Count);
    }

    [Test]
    public void 같은_프레임에_여러_종류를_주우면_종류마다_한_줄이다()
    {
        var feed = 목록();
        feed.Add(아이템("scrap"), 2, 1f);
        feed.Add(아이템("wood"), 3, 1f);
        feed.Add(아이템("fiber"), 1, 1f);

        Assert.AreEqual(3, feed.Count);
        CollectionAssert.AreEqual(new[] { 2, 3, 1 }, feed.Rows.Select(r => r.Count).ToArray());
    }

    // ── 합치기 ───────────────────────────────────────────────

    [Test]
    public void 같은_것을_잇달아_주우면_한_줄로_합쳐진다()
    {
        var feed = 목록();
        var scrap = 아이템("scrap");

        for (int i = 0; i < 10; i++) feed.Add(scrap, 1, i * 0.2f);

        Assert.AreEqual(1, feed.Count, "열 번 주웠다고 열 줄이 뜨면 화면이 가려진다");
        Assert.AreEqual(10, feed.Rows[0].Count);
    }

    [Test]
    public void 합칠_때_사라질_시각을_다시_센다()
    {
        var feed = 목록(lifetime: 5f, window: 3f);
        var scrap = 아이템("scrap");

        feed.Add(scrap, 1, 0f);
        Assert.AreEqual(5f, feed.Rows[0].ExpiresAt, 1e-4f);

        feed.Add(scrap, 1, 2f);
        Assert.AreEqual(7f, feed.Rows[0].ExpiresAt, 1e-4f,
            "합쳤으면 방금 얻은 것이다. 사라질 시각을 다시 세지 않으면 갱신된 줄이 곧바로 사라진다");
    }

    [Test]
    public void 시간_창_밖이면_새_줄이_된다()
    {
        var feed = 목록(lifetime: 5f, window: 3f);
        var scrap = 아이템("scrap");

        feed.Add(scrap, 1, 0f);
        feed.Add(scrap, 1, 3.5f);   // 창(3초)은 지났고 수명(5초)은 남았다

        Assert.AreEqual(2, feed.Count, "이미 읽고 눈을 뗀 줄의 수를 몰래 늘리지 않는다");
        Assert.AreEqual(1, feed.Rows[0].Count);
        Assert.AreEqual(1, feed.Rows[1].Count);
    }

    [Test]
    public void 이미_사라진_줄은_되살아나지_않는다()
    {
        var feed = 목록(lifetime: 5f, window: 3f);
        var scrap = 아이템("scrap");

        feed.Add(scrap, 3, 0f);
        feed.Add(scrap, 1, 20f);

        Assert.AreEqual(1, feed.Count);
        Assert.AreEqual(1, feed.Rows[0].Count, "한참 전에 사라진 줄에 합쳐지면 수가 3에서 이어진다");
    }

    [Test]
    public void 합쳐진_줄은_가장_최근_자리로_간다()
    {
        var feed = 목록();
        var scrap = 아이템("scrap");
        var wood = 아이템("wood");

        feed.Add(scrap, 1, 0f);
        feed.Add(wood, 1, 0.5f);
        feed.Add(scrap, 1, 1f);

        Assert.AreEqual(2, feed.Count);
        Assert.AreSame(wood, feed.Rows[0].Item);
        Assert.AreSame(scrap, feed.Rows[1].Item, "방금 바뀐 줄이 눈이 머무는 자리로 와야 한다");
        Assert.AreEqual(2, feed.Rows[1].Count);
    }

    [Test]
    public void 목록은_언제나_최근_순이다()
    {
        var feed = 목록();
        var scrap = 아이템("scrap");
        var wood = 아이템("wood");
        var fiber = 아이템("fiber");

        feed.Add(scrap, 1, 0f);
        feed.Add(wood, 1, 0.5f);
        feed.Add(fiber, 1, 1f);
        feed.Add(wood, 1, 1.5f);
        feed.Add(scrap, 1, 2f);

        var times = feed.Rows.Select(r => r.AddedAt).ToArray();
        CollectionAssert.AreEqual(times.OrderBy(t => t).ToArray(), times,
            "최근 순이 무너지면 만료가 앞에서부터 일어나지 않아 가운데 줄이 빠진다");
    }

    // ── 넘칠 때 ──────────────────────────────────────────────

    [Test]
    public void 최대_줄_수를_넘으면_가장_오래된_것부터_밀려난다()
    {
        var feed = 목록(maxRows: 3);
        for (int i = 0; i < 6; i++) feed.Add(아이템("item" + i), 1, i * 0.1f);

        Assert.AreEqual(3, feed.Count);
        CollectionAssert.AreEqual(new[] { "item3", "item4", "item5" },
                                  feed.Rows.Select(r => r.Item.id).ToArray());
    }

    [Test]
    public void 최대_줄_수는_적어도_하나다()
    {
        var feed = 목록(maxRows: 0);
        Assert.AreEqual(1, feed.MaxRows);

        feed.Add(아이템("scrap"), 1, 0f);
        feed.Add(아이템("wood"), 1, 0.1f);
        Assert.AreEqual(1, feed.Count);
    }

    // ── 만료 ─────────────────────────────────────────────────

    [Test]
    public void 시간이_지나면_사라진다()
    {
        var feed = 목록(lifetime: 5f);
        feed.Add(아이템("scrap"), 1, 0f);

        Assert.IsFalse(feed.Tick(4.9f), "아직 살아 있다");
        Assert.AreEqual(1, feed.Count);

        Assert.IsTrue(feed.Tick(5f));
        Assert.AreEqual(0, feed.Count);
    }

    [Test]
    public void 앞줄만_만료되고_뒷줄은_남는다()
    {
        var feed = 목록(lifetime: 5f);
        feed.Add(아이템("scrap"), 1, 0f);
        feed.Add(아이템("wood"), 1, 3f);

        feed.Tick(5.5f);

        Assert.AreEqual(1, feed.Count);
        Assert.AreEqual("wood", feed.Rows[0].Item.id);
    }

    [Test]
    public void 바뀐_것이_없으면_Tick은_false다()
    {
        var feed = 목록();
        Assert.IsFalse(feed.Tick(0f));
        Assert.IsFalse(feed.Tick(100f), "빈 목록을 아무리 흔들어도 바뀔 것이 없다");
    }

    // ── 이상한 입력 ──────────────────────────────────────────

    [Test]
    public void 수가_0이면_줄을_만들지_않는다()
    {
        var feed = 목록();
        Assert.IsFalse(feed.Add(아이템("scrap"), 0, 0f));
        Assert.AreEqual(0, feed.Count);
    }

    [Test]
    public void 수가_음수면_줄을_만들지_않는다()
    {
        var feed = 목록();
        Assert.IsFalse(feed.Add(아이템("scrap"), -3, 0f));
        Assert.AreEqual(0, feed.Count);
    }

    [Test]
    public void 아이템이_없으면_줄을_만들지_않는다()
    {
        var feed = 목록();
        Assert.IsFalse(feed.Add(null, 5, 0f));
        Assert.AreEqual(0, feed.Count);
    }

    [Test]
    public void 수가_0이어도_다_산_줄은_걷어_낸다()
    {
        var feed = 목록(lifetime: 5f);
        feed.Add(아이템("scrap"), 1, 0f);

        // 넣지는 못해도 시각은 알려 준 셈이다. 죽은 줄이 남아 있으면 안 된다.
        feed.Add(아이템("wood"), 0, 10f);

        Assert.AreEqual(0, feed.Count);
    }

    // ── 판(Version) ──────────────────────────────────────────

    [Test]
    public void 목록이_바뀔_때만_판이_오른다()
    {
        var feed = 목록(lifetime: 5f);
        int 처음 = feed.Version;

        feed.Add(아이템("scrap"), 1, 0f);
        int 넣은뒤 = feed.Version;
        Assert.Greater(넣은뒤, 처음);

        feed.Tick(1f);
        Assert.AreEqual(넣은뒤, feed.Version, "아무것도 안 바뀌었는데 다시 그리게 하면 안 된다");

        feed.Tick(6f);
        Assert.Greater(feed.Version, 넣은뒤);
    }

    [Test]
    public void Clear는_전부_지우고_판을_올린다()
    {
        var feed = 목록();
        feed.Add(아이템("scrap"), 1, 0f);
        int 넣은뒤 = feed.Version;

        feed.Clear();
        Assert.AreEqual(0, feed.Count);
        Assert.Greater(feed.Version, 넣은뒤);
    }

    [Test]
    public void 빈_목록을_또_지우면_판이_오르지_않는다()
    {
        var feed = 목록();
        int 처음 = feed.Version;
        feed.Clear();
        Assert.AreEqual(처음, feed.Version);
    }

    // ── 줄에 적히는 글자 ─────────────────────────────────────

    [Test]
    public void 줄_문장은_표에서_나온다()
    {
        Assert.IsTrue(Loc.IsLoaded, "표가 안 실렸으면 아래 검사는 공회전한다");
        Assert.IsTrue(Loc.Catalog.Contains(new LocKey("Feed", "pickup_one")));
        Assert.IsTrue(Loc.Catalog.Contains(new LocKey("Feed", "pickup_many")));
    }

    [Test]
    public void 수가_하나일_때와_여럿일_때가_다른_문장이다()
    {
        var scrap = 아이템("scrap");

        string 하나 = PickupFeedText.Line(scrap, 1);
        string 여럿 = PickupFeedText.Line(scrap, 7);

        Assert.AreEqual(Loc.F("Feed", "pickup_one", DataText.Name(scrap)), 하나);
        Assert.AreEqual(Loc.F("Feed", "pickup_many", DataText.Name(scrap), 7), 여럿);
        Assert.AreNotEqual(하나, 여럿);
        StringAssert.Contains("7", 여럿);
    }

    [Test]
    public void 로케일을_바꾸면_줄_문장도_바뀐다()
    {
        var scrap = 아이템("scrap");
        string 처음 = Loc.CurrentLocale;
        try
        {
            Loc.SetLocale("ko");
            string 한국어 = PickupFeedText.Line(scrap, 2);

            Loc.SetLocale("en");
            string 영어 = PickupFeedText.Line(scrap, 2);

            Assert.AreNotEqual(한국어, 영어, "껍데기가 코드에 박혀 있으면 이 둘이 같다");
        }
        finally { Loc.SetLocale(처음); }
    }

    [Test]
    public void 아이콘이_없으면_이름_첫_글자로_자리를_지킨다()
    {
        var scrap = 아이템("scrap");
        Assert.AreEqual(DataText.Name(scrap).Substring(0, 1), PickupFeedText.IconLetter(scrap));
        Assert.AreEqual("", PickupFeedText.IconLetter(null));
    }

    [Test]
    public void 줄에서_바로_문장을_뽑을_수_있다()
    {
        var feed = 목록();
        var scrap = 아이템("scrap");
        feed.Add(scrap, 4, 0f);

        Assert.AreEqual(PickupFeedText.Line(scrap, 4), PickupFeedText.Line(feed.Rows[0]));
        Assert.AreEqual("", PickupFeedText.Line(null));
    }
}
