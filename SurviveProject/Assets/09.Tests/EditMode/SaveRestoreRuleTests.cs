using System.Collections.Generic;
using NUnit.Framework;
using Survive.Core;

/// <summary>
/// 저장본의 절을 <b>순서에 기대지 않고</b> 되돌리는 규칙.
///
/// 왜 규칙이 따로 있는가: 절 하나를 되돌리는 일이 <b>다음 절의 주인을 태어나게</b>
/// 한다. 「세계」 절은 생성 목록대로 몸을 다시 세우고 그 몸들이 그 자리에서
/// 저장 대상으로 등록한다. 그래서 위에서 아래로 한 번만 훑으면, 아직 없는 주인을
/// 가리키는 절이 조용히 버려진다 — 사람 눈에는 「저장이 맡긴 물건을 먹었다」로
/// 보이고, 로그에는 아무것도 안 남는다.
///
/// 여기 있는 검사들은 그 사고의 모양을 그대로 재현한다. <c>SaveService</c>는
/// 예정된 어셈블리(Assembly-CSharp)에 있어 EditMode에서 못 부르므로,
/// <b>순서를 정하는 판단만</b> Domain으로 빼서 여기서 친다.
/// </summary>
public class SaveRestoreRuleTests
{
    static SaveEntry 절(string key) => new SaveEntry { key = key, type = "T", json = "{}" };

    /// <summary>
    /// 「세계」 절을 앉히면 그 안에서 몸이 태어나 등록한다 — 그 모양을 흉내 낸
    /// 가짜 세계. <see cref="Apply"/>가 <c>SaveService.TryRestore</c>의 자리다.
    /// </summary>
    class 가짜세계
    {
        public readonly HashSet<string> 등록된주인 = new HashSet<string>();
        public readonly List<string> 앉힌차례 = new List<string>();

        /// <summary>이 절을 앉히면 등록되는 주인들.</summary>
        public readonly Dictionary<string, string[]> 태어나게하는것 =
            new Dictionary<string, string[]>();

        public bool Apply(SaveEntry entry)
        {
            if (!등록된주인.Contains(entry.key)) return false;

            앉힌차례.Add(entry.key);
            if (태어나게하는것.TryGetValue(entry.key, out var 자식들))
                foreach (var c in 자식들) 등록된주인.Add(c);

            return true;
        }
    }

    static 가짜세계 세계(params string[] 처음부터있는것)
    {
        var w = new 가짜세계();
        foreach (var k in 처음부터있는것) w.등록된주인.Add(k);
        return w;
    }

    // ── 순서 ─────────────────────────────────────────────────

    [Test]
    public void 주인이_이미_있으면_한_바퀴에_전부_앉는다()
    {
        var w = 세계("a", "b", "c");
        var 절들 = new List<SaveEntry> { 절("a"), 절("b"), 절("c") };

        int 앉힌것 = SaveRestoreRule.Apply(절들, w.Apply, out int 못찾은것);

        Assert.AreEqual(3, 앉힌것);
        Assert.AreEqual(0, 못찾은것);
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, w.앉힌차례);
    }

    [Test]
    public void 세계_절이_뒤에_있어도_먼저_생긴_몸이_제_절을_받는다()
    {
        var w = 세계("world");
        w.태어나게하는것["world"] = new[] { "storage_1_2_3" };

        // 실제 게임이 만드는 순서 — 몸을 만드는 절이 앞이다.
        var 절들 = new List<SaveEntry> { 절("world"), 절("storage_1_2_3") };

        int 앉힌것 = SaveRestoreRule.Apply(절들, w.Apply, out int 못찾은것);

        Assert.AreEqual(2, 앉힌것);
        Assert.AreEqual(0, 못찾은것);
        CollectionAssert.AreEqual(new[] { "world", "storage_1_2_3" }, w.앉힌차례);
    }

    [Test]
    public void 몸을_가리키는_절이_앞에_있어도_잃지_않는다()
    {
        var w = 세계("world");
        w.태어나게하는것["world"] = new[] { "storage_1_2_3" };

        // 옛 저장본의 모양 — 딸림이 제 절을 갖고, 그 절이 「세계」 절보다 앞이다.
        var 절들 = new List<SaveEntry> { 절("storage_1_2_3"), 절("world") };

        int 앉힌것 = SaveRestoreRule.Apply(절들, w.Apply, out int 못찾은것);

        Assert.AreEqual(2, 앉힌것, "한 번만 훑었다면 보관함 절이 통째로 버려졌다");
        Assert.AreEqual(0, 못찾은것);
        CollectionAssert.AreEqual(new[] { "world", "storage_1_2_3" }, w.앉힌차례,
                                  "몸이 선 뒤에 내용물이 앉는다");
    }

    [Test]
    public void 태어나는_것이_또_태어나게_해도_끝까지_앉는다()
    {
        var w = 세계("world");
        w.태어나게하는것["world"] = new[] { "storage" };
        w.태어나게하는것["storage"] = new[] { "inner" };

        var 절들 = new List<SaveEntry> { 절("inner"), 절("storage"), 절("world") };

        int 앉힌것 = SaveRestoreRule.Apply(절들, w.Apply, out int 못찾은것);

        Assert.AreEqual(3, 앉힌것);
        Assert.AreEqual(0, 못찾은것);
        CollectionAssert.AreEqual(new[] { "world", "storage", "inner" }, w.앉힌차례);
    }

    // ── 멈추는 자리 ──────────────────────────────────────────

    [Test]
    public void 주인이_끝내_없는_절은_못찾은것으로_센다()
    {
        var w = 세계("a");
        var 절들 = new List<SaveEntry> { 절("a"), 절("사라진기능"), 절("다른씬의절") };

        int 앉힌것 = SaveRestoreRule.Apply(절들, w.Apply, out int 못찾은것);

        Assert.AreEqual(1, 앉힌것);
        Assert.AreEqual(2, 못찾은것, "옛 저장본과 지운 기능의 절은 그냥 건너뛴다");
    }

    [Test]
    public void 아무도_못_앉는_저장본에서도_영영_돌지_않는다()
    {
        var w = 세계();
        int 부른횟수 = 0;

        var 절들 = new List<SaveEntry> { 절("x"), 절("y") };
        int 앉힌것 = SaveRestoreRule.Apply(절들, e => { 부른횟수++; return w.Apply(e); },
                                          out int 못찾은것);

        Assert.AreEqual(0, 앉힌것);
        Assert.AreEqual(2, 못찾은것);
        Assert.AreEqual(2, 부른횟수, "한 바퀴에 아무것도 못 앉히면 거기서 멈춘다");
    }

    [Test]
    public void 빈_칸과_빈_목록을_그냥_넘긴다()
    {
        var w = 세계("a");
        var 절들 = new List<SaveEntry> { null, 절("a"), null };

        int 앉힌것 = SaveRestoreRule.Apply(절들, w.Apply, out int 못찾은것);

        Assert.AreEqual(1, 앉힌것);
        Assert.AreEqual(0, 못찾은것);

        Assert.AreEqual(0, SaveRestoreRule.Apply(null, w.Apply, out int 없음));
        Assert.AreEqual(0, 없음);
        Assert.AreEqual(0, SaveRestoreRule.Apply(절들, null, out _));
    }

    // ── 되풀이 비용 ──────────────────────────────────────────

    [Test]
    public void 이미_앉힌_절을_다시_앉히지_않는다()
    {
        var w = 세계("world");
        w.태어나게하는것["world"] = new[] { "a", "b" };

        var 절들 = new List<SaveEntry> { 절("a"), 절("b"), 절("world") };
        SaveRestoreRule.Apply(절들, w.Apply, out _);

        CollectionAssert.AreEquivalent(new[] { "world", "a", "b" }, w.앉힌차례);
        Assert.AreEqual(3, w.앉힌차례.Count, "한 절은 한 번만 앉는다");
    }
}
