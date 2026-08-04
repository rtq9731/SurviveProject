using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Survive.Building;
using Survive.Items;

/// <summary>
/// BuildCatalogSO의 조회 로직과, 실제로 게임이 읽는 BuildCatalog 에셋의 무결성.
///
/// 카탈로그는 id 문자열로만 연결된다 — 컴파일러가 오타를 잡아주지 않는다.
/// 중복 id는 더 나쁘다. 컴파일도 통과하고 조회도 성공하는데, 둘 중 어느 쪽이
/// 나올지는 배열 순서가 정한다.
/// </summary>
public class BuildCatalogTests
{
    const string CatalogPath = "Assets/08.Data/Buildables/BuildCatalog.asset";

    static BuildableSO 건축물(string id)
    {
        var b = ScriptableObject.CreateInstance<BuildableSO>();
        b.id = id;
        b.displayName = id;
        return b;
    }

    static BuildCatalogSO 카탈로그(params BuildableSO[] entries)
    {
        var c = ScriptableObject.CreateInstance<BuildCatalogSO>();
        c.entries = entries;
        return c;
    }

    // ── 조회 로직 ────────────────────────────────────────────

    [Test]
    public void 새_카탈로그는_빈_목록으로_시작한다()
    {
        var c = ScriptableObject.CreateInstance<BuildCatalogSO>();
        Assert.IsNotNull(c.entries, "null이면 foreach마다 방어 코드가 필요해진다");
        Assert.AreEqual(0, c.entries.Length);
    }

    [Test]
    public void id로_건축물을_찾는다()
    {
        var 화톳불 = 건축물("campfire");
        var c = 카탈로그(건축물("bench"), 화톳불, 건축물("storage"));

        Assert.AreSame(화톳불, c.GetById("campfire"));
    }

    [Test]
    public void 없는_id는_null을_돌려준다()
    {
        Assert.IsNull(카탈로그(건축물("bench")).GetById("없는건축물"));
    }

    [Test]
    public void null이나_빈_id는_null을_돌려준다()
    {
        var c = 카탈로그(건축물("bench"));
        Assert.IsNull(c.GetById(null));
        Assert.IsNull(c.GetById(""));
    }

    [Test]
    public void 목록이_null이면_예외_없이_null을_돌려준다()
    {
        var c = ScriptableObject.CreateInstance<BuildCatalogSO>();
        c.entries = null;

        BuildableSO 찾은것 = null;
        Assert.DoesNotThrow(() => 찾은것 = c.GetById("campfire"));
        Assert.IsNull(찾은것);
    }

    [Test]
    public void 목록_중간의_null_항목을_건너뛴다()
    {
        var 보관함 = 건축물("storage");
        var c = 카탈로그(null, 보관함, null);

        Assert.AreSame(보관함, c.GetById("storage"),
            "인스펙터에서 항목을 지우면 빈 칸이 남는다 — 그 뒤를 못 찾으면 안 된다");
    }

    [Test]
    public void id가_빈_항목은_빈_id로_조회해도_걸리지_않는다()
    {
        var c = 카탈로그(건축물(""), 건축물("bench"));
        Assert.IsNull(c.GetById(""), "빈 id는 조회 자체가 거부된다");
    }

    [Test]
    public void 중복_id는_앞에_있는_것을_돌려준다()
    {
        // 현재 동작을 못박아 둔다. 어느 쪽이 나오든 데이터가 잘못된 것이므로,
        // 실제 방어는 아래 "실제 에셋" 테스트가 한다.
        var 첫번째 = 건축물("campfire");
        var 두번째 = 건축물("campfire");
        var c = 카탈로그(첫번째, 두번째);

        Assert.AreSame(첫번째, c.GetById("campfire"));
    }

    // ── 실제 에셋 무결성 ─────────────────────────────────────

    static BuildCatalogSO 실제_카탈로그()
    {
        var c = AssetDatabase.LoadAssetAtPath<BuildCatalogSO>(CatalogPath);
        Assert.IsNotNull(c, $"{CatalogPath}를 못 읽었다 — 경로가 바뀌었는가");
        return c;
    }

    [Test]
    public void 실제_카탈로그에_빈_항목이_없다()
    {
        var c = 실제_카탈로그();
        Assert.Greater(c.entries.Length, 0, "카탈로그가 비어 있으면 아무것도 못 짓는다");

        for (int i = 0; i < c.entries.Length; i++)
            Assert.IsNotNull(c.entries[i], $"entries[{i}]가 비어 있다");
    }

    [Test]
    public void 실제_카탈로그의_id가_전부_채워져_있다()
    {
        foreach (var e in 실제_카탈로그().entries)
            Assert.IsFalse(string.IsNullOrWhiteSpace(e.id),
                $"{e.name}의 id가 비어 있다 — 조회로 절대 찾을 수 없다");
    }

    [Test]
    public void 실제_카탈로그에_중복_id가_없다()
    {
        var 본것 = new HashSet<string>();
        var 중복 = new List<string>();

        foreach (var e in 실제_카탈로그().entries)
            if (!본것.Add(e.id)) 중복.Add(e.id);

        Assert.IsEmpty(중복, "중복 id: " + string.Join(", ", 중복));
    }

    [Test]
    public void 실제_카탈로그의_모든_항목을_id로_찾을_수_있다()
    {
        var c = 실제_카탈로그();
        foreach (var e in c.entries)
            Assert.AreSame(e, c.GetById(e.id), $"{e.id}를 자기 카탈로그에서 못 찾는다");
    }

    [Test]
    public void 실제_카탈로그의_건축_비용이_유효하다()
    {
        foreach (var e in 실제_카탈로그().entries)
        {
            Assert.IsNotNull(e.cost, $"{e.id}의 cost가 null이다");

            for (int i = 0; i < e.cost.Length; i++)
            {
                ItemStack 비용 = e.cost[i];
                Assert.IsNotNull(비용, $"{e.id}의 cost[{i}]가 비어 있다");
                Assert.IsNotNull(비용.item, $"{e.id}의 cost[{i}]에 아이템이 없다");
                Assert.Greater(비용.count, 0, $"{e.id}의 cost[{i}] 수량이 0 이하다");
            }
        }
    }

    [Test]
    public void 실제_카탈로그의_모든_항목에_세울_프리팹이_있다()
    {
        foreach (var e in 실제_카탈로그().entries)
            Assert.IsNotNull(e.prefab, $"{e.id}에 prefab이 없다 — 지어도 아무것도 안 선다");
    }
}
