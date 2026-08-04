using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Survive.World;

/// <summary>
/// 백로그 26번 — "죽으면 마지막 화톳불에서, 없으면 시작 지점에서" 의 규칙부.
/// 순수 함수라 MonoBehaviour나 씬 없이 전부 검증한다.
/// </summary>
public class RespawnRuleTests
{
    static RespawnAnchor 불(float x, float 붙인시각, bool 타는중 = true)
        => new RespawnAnchor(new Vector3(x, 0f, 0f), 붙인시각, 타는중);

    [Test]
    public void 화톳불이_없으면_시작_지점으로_돌아간다()
    {
        var 시작 = new Vector3(1f, 2f, 3f);
        Assert.AreEqual(시작, RespawnRule.Choose(new List<RespawnAnchor>(), 시작));
    }

    [Test]
    public void 후보_목록이_비어_있지_않아도_다_꺼져_있으면_시작_지점이다()
    {
        // 잿더미로 돌려보내는 것은 어둠 한복판에 떨구는 것과 같다.
        var 시작 = new Vector3(1f, 2f, 3f);
        var 후보 = new List<RespawnAnchor> { 불(10f, 5f, 타는중: false), 불(20f, 9f, 타는중: false) };

        Assert.AreEqual(시작, RespawnRule.Choose(후보, 시작));
    }

    [Test]
    public void 화톳불이_하나면_그리로_간다()
    {
        var 후보 = new List<RespawnAnchor> { 불(10f, 5f) };
        Assert.AreEqual(new Vector3(10f, 0f, 0f), RespawnRule.Choose(후보, Vector3.zero));
    }

    [Test]
    public void 마지막으로_불붙인_화톳불로_간다()
    {
        // 목록 순서가 아니라 붙인 시각이 기준이다 — 늦게 붙은 것이 이긴다.
        var 후보 = new List<RespawnAnchor> { 불(10f, 30f), 불(20f, 5f), 불(30f, 12f) };
        Assert.AreEqual(new Vector3(10f, 0f, 0f), RespawnRule.Choose(후보, Vector3.zero));
    }

    [Test]
    public void 나중에_다시_지핀_불이_먼저_세운_불을_이긴다()
    {
        // 오래전에 세워 두고 방금 다시 지핀 불이 사람이 마지막으로 머문 자리다.
        var 오래된불_다시지핌 = 불(10f, 100f);
        var 최근에세운불 = 불(20f, 40f);

        var 후보 = new List<RespawnAnchor> { 오래된불_다시지핌, 최근에세운불 };
        Assert.AreEqual(new Vector3(10f, 0f, 0f), RespawnRule.Choose(후보, Vector3.zero));
    }

    [Test]
    public void 꺼진_불은_더_최근에_붙었어도_지나친다()
    {
        var 후보 = new List<RespawnAnchor> { 불(10f, 5f), 불(20f, 99f, 타는중: false) };
        Assert.AreEqual(new Vector3(10f, 0f, 0f), RespawnRule.Choose(후보, Vector3.zero));
    }

    [Test]
    public void 같은_시각에_붙은_불은_먼저_온_것을_쓴다()
    {
        // 값 자체보다 흔들리지 않는 것이 중요하다. 두 번 물어도 같은 답이어야 한다.
        var 후보 = new List<RespawnAnchor> { 불(10f, 7f), 불(20f, 7f) };

        Assert.AreEqual(new Vector3(10f, 0f, 0f), RespawnRule.Choose(후보, Vector3.zero));
        Assert.AreEqual(new Vector3(10f, 0f, 0f), RespawnRule.Choose(후보, Vector3.zero));
    }

    [Test]
    public void 고른_첨자로_원래_화톳불을_되찾을_수_있다()
    {
        // 실행부는 좌표만으로는 부족하다 — 어느 화톳불이었는지 알아야 그 곁에 세운다.
        var 후보 = new List<RespawnAnchor> { 불(10f, 5f), 불(20f, 50f), 불(30f, 12f) };

        Assert.IsTrue(RespawnRule.TryChoose(후보, out int 첨자));
        Assert.AreEqual(1, 첨자);
    }

    [Test]
    public void 고를_것이_없으면_첨자는_음수다()
    {
        Assert.IsFalse(RespawnRule.TryChoose(new List<RespawnAnchor>(), out int 첨자));
        Assert.Less(첨자, 0);
    }

    [Test]
    public void 후보가_null이어도_시작_지점을_돌려준다()
    {
        var 시작 = new Vector3(4f, 5f, 6f);
        Assert.AreEqual(시작, RespawnRule.Choose(null, 시작));
    }

    [Test]
    public void 붙인_시각이_0이어도_타고_있으면_후보다()
    {
        // 씬이 시작하자마자 붙은 불은 Time.time이 0에 가깝다.
        // 0을 "아직 안 붙었다"로 잘못 읽으면 이 불이 통째로 사라진다.
        var 후보 = new List<RespawnAnchor> { 불(10f, 0f) };
        Assert.AreEqual(new Vector3(10f, 0f, 0f), RespawnRule.Choose(후보, new Vector3(9f, 9f, 9f)));
    }
}
