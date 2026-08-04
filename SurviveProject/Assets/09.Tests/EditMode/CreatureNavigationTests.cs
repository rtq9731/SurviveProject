using NUnit.Framework;
using UnityEngine;
using Survive.Creatures;

/// <summary>
/// 백로그 15 — CreatureBrain에서 뽑아낸 목적지 산수와 상태 지속시간.
///
/// 비행 생물은 NavMesh가 높이를 붙잡아 주지 않는다. 목적지의 y를 거처 높이로
/// 되돌리는 한 줄이 빠지면 도주할 때마다 조금씩 하늘로 올라가서
/// 결국 손이 닿지 않는 곳에 떠 있게 된다. 그 한 줄을 여기서 지킨다.
/// </summary>
public class CreatureNavigationTests
{
    const float 허용오차 = 1e-4f;

    // ── 배회 목적지 ─────────────────────────────────────────────────────

    [Test]
    public void 배회는_거처에서_반경만큼_떨어진_곳을_잡는다()
    {
        var 거처 = new Vector3(10f, 3f, -5f);
        var 결과 = CreatureNavigation.WanderDestination(거처, Vector3.right, 6f);

        Assert.AreEqual(16f, 결과.x, 허용오차);
        Assert.AreEqual(-5f, 결과.z, 허용오차);
    }

    [Test]
    public void 배회_목적지의_높이는_거처의_높이다()
    {
        var 거처 = new Vector3(0f, 12.5f, 0f);
        // 위로 치솟는 오프셋을 줘도 높이는 거처를 따른다.
        var 결과 = CreatureNavigation.WanderDestination(거처, Vector3.up, 6f);

        Assert.AreEqual(12.5f, 결과.y, 허용오차);
    }

    [Test]
    public void 반경이_0이면_거처를_그대로_잡는다()
    {
        var 거처 = new Vector3(1f, 2f, 3f);
        var 결과 = CreatureNavigation.WanderDestination(거처, Vector3.one, 0f);

        Assert.AreEqual(거처, 결과);
    }

    // ── 도주 목적지 ─────────────────────────────────────────────────────

    [Test]
    public void 도주는_위협의_반대쪽으로_간다()
    {
        // 위협이 원점, 생물이 +x 쪽 3m — 더 멀리 +x로 달아나야 한다.
        var 결과 = CreatureNavigation.FleeDestination(
            self: new Vector3(3f, 0f, 0f), threat: Vector3.zero, fleeDistance: 12f, groundY: 0f);

        Assert.AreEqual(15f, 결과.x, 허용오차);
        Assert.AreEqual(0f, 결과.z, 허용오차);
    }

    [Test]
    public void 도주_거리는_방향에_상관없이_같다()
    {
        var 자신 = new Vector3(4f, 0f, 3f);       // 원점에서 5m
        var 결과 = CreatureNavigation.FleeDestination(자신, Vector3.zero, 12f, 0f);

        Assert.AreEqual(12f, Vector3.Distance(자신, 결과), 허용오차);
    }

    [Test]
    public void 도주_목적지의_높이는_거처의_높이다()
    {
        var 결과 = CreatureNavigation.FleeDestination(
            self: new Vector3(0f, 40f, 5f), threat: new Vector3(0f, 40f, 0f),
            fleeDistance: 12f, groundY: 2.5f);

        Assert.AreEqual(2.5f, 결과.y, 허용오차, "도주할 때마다 고도가 딸려 올라가면 안 된다");
    }

    [Test]
    public void 위협과_정확히_겹치면_제자리를_잡는다()
    {
        // 방향이 없으니 갈 곳도 없다. 다음 프레임이면 조금이라도 어긋난다.
        var 자리 = new Vector3(7f, 1f, 7f);
        var 결과 = CreatureNavigation.FleeDestination(자리, 자리, 12f, 1f);

        Assert.AreEqual(7f, 결과.x, 허용오차);
        Assert.AreEqual(1f, 결과.y, 허용오차);
        Assert.AreEqual(7f, 결과.z, 허용오차);
    }

    // ── 상태 지속시간 ───────────────────────────────────────────────────

    [Test]
    public void 배회_지속시간은_호출자가_뽑은_값을_그대로_쓴다()
    {
        Assert.AreEqual(3.14f, CreatureStateDurations.For(CreatureState.Wander, 3.14f), 허용오차);
    }

    [Test]
    public void 먹기와_줍기는_같은_시간을_붙든다()
    {
        Assert.AreEqual(CreatureStateDurations.EcologySeconds,
                        CreatureStateDurations.For(CreatureState.Feed, 0f), 허용오차);
        Assert.AreEqual(CreatureStateDurations.EcologySeconds,
                        CreatureStateDurations.For(CreatureState.Scavenge, 0f), 허용오차);
    }

    [Test]
    public void 도주는_한동안_같은_방향으로_내뺀다()
    {
        Assert.AreEqual(CreatureStateDurations.FleeSeconds,
                        CreatureStateDurations.For(CreatureState.Flee, 0f), 허용오차);
        Assert.Greater(CreatureStateDurations.FleeSeconds, CreatureStateDurations.DefaultSeconds,
                       "도주가 기본 상태보다 짧으면 방향이 매 프레임 흔들린다");
    }

    [Test]
    public void 추격과_공격과_대기는_짧게_붙들고_다시_판단한다()
    {
        Assert.AreEqual(CreatureStateDurations.DefaultSeconds,
                        CreatureStateDurations.For(CreatureState.Chase, 99f), 허용오차);
        Assert.AreEqual(CreatureStateDurations.DefaultSeconds,
                        CreatureStateDurations.For(CreatureState.Attack, 99f), 허용오차);
        Assert.AreEqual(CreatureStateDurations.DefaultSeconds,
                        CreatureStateDurations.For(CreatureState.Idle, 99f), 허용오차);
    }

    [Test]
    public void 어떤_상태도_0초_이하로_붙들지_않는다()
    {
        // 0초면 다음 프레임에 바로 다시 전이해 목적지가 매 프레임 새로 뽑힌다.
        foreach (CreatureState 상태 in System.Enum.GetValues(typeof(CreatureState)))
            Assert.Greater(CreatureStateDurations.For(상태, CreatureStateDurations.WanderMinSeconds), 0f,
                           $"{상태}를 붙드는 시간이 없다");
    }

    [Test]
    public void 배회_지속시간의_범위는_뒤집혀_있지_않다()
    {
        Assert.Greater(CreatureStateDurations.WanderMaxSeconds, CreatureStateDurations.WanderMinSeconds);
        Assert.Greater(CreatureStateDurations.WanderMinSeconds, 0f);
    }
}
