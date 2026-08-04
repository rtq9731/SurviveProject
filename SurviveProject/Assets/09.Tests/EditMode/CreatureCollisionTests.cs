using NUnit.Framework;
using UnityEngine;
using Survive.Creatures;

/// <summary>
/// 백로그 21 — 생물이 벽을 통과하던 것을 막는 산수.
///
/// NavMeshAgent는 구워 둔 NavMesh만 알고, FlyerMotor는 transform을 직접 더한다.
/// 둘 다 물리를 보지 않아서 플레이어가 세운 벽을 그대로 지나갔다.
/// 그 이동량을 벽에 맞춰 깎는 규칙이 여기 있다.
///
/// 규칙이 <b>과하면</b> 더 큰 손해다 — 비탈까지 벽으로 보면 NavMesh가 걸어도 된다고 한
/// 언덕에서 생물이 붙박인다. 그래서 "무엇을 벽으로 보지 않는가"도 같이 지킨다.
/// </summary>
public class CreatureCollisionTests
{
    const float 허용오차 = 1e-4f;

    // ── 워프 구분 ───────────────────────────────────────────────────────

    [Test]
    public void 걸어간_거리는_워프가_아니다()
    {
        // 3.2m/s 짜리 생물이 한 프레임에 가는 거리는 센티미터 단위다.
        Assert.IsFalse(CreatureCollision.IsTeleport(0.06f, CreatureCollision.TeleportDistance));
    }

    [Test]
    public void 한_프레임에_문턱을_넘게_움직였으면_워프로_본다()
    {
        // E2E 하네스가 생물을 옮겨 놓았을 때 그 사이의 지형을 벽으로 오해하면 안 된다.
        Assert.IsTrue(CreatureCollision.IsTeleport(
            CreatureCollision.TeleportDistance + 0.1f, CreatureCollision.TeleportDistance));
    }

    [Test]
    public void 문턱과_정확히_같으면_워프가_아니다()
    {
        Assert.IsFalse(CreatureCollision.IsTeleport(5f, 5f));
    }

    // ── 무엇이 벽인가 ───────────────────────────────────────────────────

    [Test]
    public void 평평한_바닥은_벽이_아니다()
    {
        Assert.IsTrue(CreatureCollision.IsWalkable(Vector3.up.y, CreatureCollision.WalkableNormalY));
    }

    [Test]
    public void 완만한_비탈은_벽이_아니다()
    {
        // 30도 비탈의 법선 y는 약 0.866. NavMesh가 걸어도 된다고 한 곳이다.
        Assert.IsTrue(CreatureCollision.IsWalkable(Mathf.Cos(30f * Mathf.Deg2Rad),
                                                   CreatureCollision.WalkableNormalY));
    }

    [Test]
    public void 수직면은_벽이다()
    {
        Assert.IsFalse(CreatureCollision.IsWalkable(0f, CreatureCollision.WalkableNormalY));
    }

    [Test]
    public void 뒤집힌_면도_벽이다()
    {
        // 천장이나 처마 아래쪽. 밟고 오를 수 있는 면이 아니다.
        Assert.IsFalse(CreatureCollision.IsWalkable(-1f, CreatureCollision.WalkableNormalY));
    }

    [Test]
    public void 경계면은_밟을_수_있는_쪽으로_본다()
    {
        // 경계에서 막아 버리면 딱 그 각도의 언덕에서 생물이 갇힌다.
        Assert.IsTrue(CreatureCollision.IsWalkable(CreatureCollision.WalkableNormalY,
                                                   CreatureCollision.WalkableNormalY));
    }

    // ── 벽 앞에서 멈추기 ────────────────────────────────────────────────

    [Test]
    public void 벽에_살갗만큼_못_미쳐_멈춘다()
    {
        var 결과 = CreatureCollision.StopShort(Vector3.zero, Vector3.right,
                                               hitDistance: 2f, skin: 0.05f);

        Assert.AreEqual(1.95f, 결과.x, 허용오차);
        Assert.AreEqual(0f, 결과.z, 허용오차);
    }

    [Test]
    public void 이미_벽에_붙어_있으면_제자리다()
    {
        // 닿은 거리가 살갗보다 짧다고 뒤로 밀어내면 벽에서 튕겨 나간다.
        var 자리 = new Vector3(3f, 1f, -2f);
        var 결과 = CreatureCollision.StopShort(자리, Vector3.forward, hitDistance: 0.01f, skin: 0.05f);

        Assert.AreEqual(자리, 결과);
    }

    [Test]
    public void 멈춘_자리는_출발점과_벽_사이에_있다()
    {
        var 출발 = new Vector3(10f, 0f, 10f);
        var 결과 = CreatureCollision.StopShort(출발, Vector3.left, hitDistance: 0.5f, skin: 0.05f);

        Assert.AreEqual(0.45f, Vector3.Distance(출발, 결과), 허용오차);
    }

    // ── 벽을 따라 미끄러지기 ────────────────────────────────────────────

    [Test]
    public void 벽을_비스듬히_밀면_벽을_따라_미끄러진다()
    {
        // +x로 뻗은 벽(법선 -z)에 대각선으로 부딪히면 x 성분만 남는다.
        var 결과 = CreatureCollision.SlideAlong(new Vector3(1f, 0f, 1f), Vector3.back);

        Assert.AreEqual(1f, 결과.x, 허용오차);
        Assert.AreEqual(0f, 결과.z, 허용오차, "벽을 파고드는 성분이 남으면 안 된다");
    }

    [Test]
    public void 벽을_정면으로_밀면_갈_곳이_없다()
    {
        var 결과 = CreatureCollision.SlideAlong(Vector3.forward, Vector3.back);

        Assert.AreEqual(0f, 결과.magnitude, 허용오차);
    }

    [Test]
    public void 미끄러진_이동량은_원래보다_길어지지_않는다()
    {
        var 원래 = new Vector3(0.8f, 0f, 0.6f);
        var 결과 = CreatureCollision.SlideAlong(원래, new Vector3(-0.7071f, 0f, -0.7071f));

        Assert.LessOrEqual(결과.magnitude, 원래.magnitude + 허용오차);
    }

    [Test]
    public void 미끄러진_이동량에는_높이가_남지_않는다()
    {
        // 높이는 NavMesh와 FlyerMotor의 몫이다. 여기서 y를 만들면 생물이 뜨거나 가라앉는다.
        var 결과 = CreatureCollision.SlideAlong(new Vector3(1f, 5f, 1f), Vector3.back);

        Assert.AreEqual(0f, 결과.y, 허용오차);
    }

    [Test]
    public void 수평_성분이_없는_법선으로는_미끄러지지_않는다()
    {
        // 천장·바닥의 법선으로 수평 이동을 투영하면 이동량이 그대로 살아남아 벽을 통과한다.
        Assert.AreEqual(0f, CreatureCollision.SlideAlong(Vector3.right, Vector3.up).magnitude, 허용오차);
        Assert.AreEqual(0f, CreatureCollision.SlideAlong(Vector3.right, Vector3.down).magnitude, 허용오차);
    }

    // ── 문지기가 쓰는 값들 ──────────────────────────────────────────────

    [Test]
    public void 살갗은_있되_턱보다는_얇다()
    {
        Assert.Greater(CreatureCollision.SkinWidth, 0f, "0이면 다음 프레임 훑기가 벽 안에서 시작한다");
        Assert.Less(CreatureCollision.SkinWidth, CreatureCollision.StepOffset);
    }

    [Test]
    public void 넘어갈_수_있는_턱이_있다()
    {
        // 0이면 지면의 작은 요철마다 벽으로 잡혀 배회가 멈춘다.
        Assert.Greater(CreatureCollision.StepOffset, 0f);
    }
}
