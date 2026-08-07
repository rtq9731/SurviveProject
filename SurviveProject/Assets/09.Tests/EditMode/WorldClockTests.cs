using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Survive.Harvesting;
using Survive.World;

/// <summary>
/// <b>세계의 시계.</b> 「지금이 언제인가」를 묻는 자리가 하나여야 한다.
///
/// <b>왜 갈아 끼웠는가.</b> 주인은 이미 있었다(<c>DayNightService</c>가 시각을
/// 소유하고 저장까지 한다). 그런데 재생과 굴림은 여전히 <c>Time.time</c>을 봤고,
/// 그것은 <b>저장 안 되고 · 배속을 타고 · 씬 로드로 0이 된다</b>. 셋 다 재생이
/// 원하지 않는 성질이고, 마지막 것은 조용히 치명적이다 — 시계만 0으로 돌아가고
/// 다 캔 시각이 큰 값으로 남으면 그 자리는 <b>영영</b> 안 돌아온다.
/// </summary>
public class WorldClockTests
{
    [SetUp]
    public void 시계를_비운다() => WorldClock.Reset();

    [TearDown]
    public void 뒷정리() => WorldClock.Reset();

    // ══ 배속을 안 탄다 — 시계 교체의 회귀선 ═══════════════════

    /// <summary>
    /// <b>이것이 회귀선이다.</b> 이 시계에는 <b>배율을 받는 인자가 아예 없다</b> —
    /// 받은 초가 곧 흐른 초다. 배속을 8로 올려도 세계가 8배 늙지 않는다는 말을
    /// 코드로 옮기면 이 모양이 된다.
    ///
    /// 배속을 여기서 직접 흔들어 볼 수 없는 것은 <b>흔들 자리가 없기 때문</b>이고,
    /// 그것이 이 테스트가 말하는 전부다. Unity 쪽에서 무엇을 먹이는지는
    /// <see cref="재생_경로에_프레임_시계가_없다"/>가 따로 지킨다.
    /// </summary>
    [Test]
    public void 세계_시계는_배속을_곱하지_않는다()
    {
        for (int i = 0; i < 10; i++) WorldClock.Advance(1.0);
        Assert.AreEqual(10.0, WorldClock.Now, 1e-9,
            "받은 초를 그대로 더한다. 배율이 끼어들 자리가 없다");
    }

    [Test]
    public void 멈춘_동안에는_흐르지_않는다()
    {
        WorldClock.Advance(5.0);
        WorldClock.Paused = true;
        WorldClock.Advance(100.0);

        Assert.AreEqual(5.0, WorldClock.Now, 1e-9,
            "일시정지는 배속이 아니라 「세계가 섰다」는 뜻이므로 존중한다");

        WorldClock.Paused = false;
        WorldClock.Advance(2.0);
        Assert.AreEqual(7.0, WorldClock.Now, 1e-9);
    }

    [Test]
    public void 멈춰_있어도_건너뛰기는_먹는다()
    {
        WorldClock.Paused = true;
        WorldClock.Skip(50.0);
        Assert.AreEqual(50.0, WorldClock.Now, 1e-9,
            "건너뛰기는 흐름이 아니라 손으로 옮기는 것이라 일시정지와 무관하다");
    }

    // ══ 되감지 않는다 ═════════════════════════════════════════

    /// <summary>
    /// 시각을 뒤로 돌리면 다 캔 시각이 <b>미래</b>가 되고, 그 자리는 영영
    /// 돌아오지 않는다. 그래서 흐름 쪽 문은 음수를 무시한다.
    /// </summary>
    [Test]
    public void 음수는_시계를_되감지_못한다()
    {
        WorldClock.Advance(10.0);
        WorldClock.Advance(-5.0);
        WorldClock.Skip(-5.0);
        Assert.AreEqual(10.0, WorldClock.Now, 1e-9);
    }

    /// <summary>
    /// 지나간 시각을 달라고 하면 <b>다음 바퀴</b>의 같은 자리로 간다.
    /// 하루를 앞뒤로 훑는 검증은 「다음 날 같은 시각」으로도 똑같이 성립한다 —
    /// 재는 것은 날짜가 아니라 시각이기 때문이다.
    /// </summary>
    [Test]
    public void 지나간_시각을_달라고_하면_다음_바퀴로_간다()
    {
        WorldClock.Skip(1000.0);
        WorldClock.ForwardTo(200.0, 1200.0);

        Assert.AreEqual(1400.0, WorldClock.Now, 1e-9);
        Assert.Greater(WorldClock.Now, 1000.0, "앞으로만 간다");
    }

    [Test]
    public void 앞에_있는_시각은_그대로_간다()
    {
        WorldClock.Skip(100.0);
        WorldClock.ForwardTo(900.0, 1200.0);
        Assert.AreEqual(900.0, WorldClock.Now, 1e-9);
    }

    [Test]
    public void 세계는_한_번만_세워진다()
    {
        Assert.IsTrue(WorldClock.StartAt(420.0), "처음에는 세워진다");
        WorldClock.Advance(60.0);

        Assert.IsFalse(WorldClock.StartAt(420.0),
            "이미 흐르는 시계를 다시 아침으로 앉히면 재생 시각이 전부 미래가 된다");
        Assert.AreEqual(480.0, WorldClock.Now, 1e-9);
    }

    /// <summary>불러오기만 값을 통째로 앉힌다. 그때는 되감아도 된다.</summary>
    [Test]
    public void 불러오기는_다른_세계의_시각을_앉힌다()
    {
        WorldClock.Skip(9999.0);
        WorldClock.Restore(120.0);
        Assert.AreEqual(120.0, WorldClock.Now, 1e-9);
    }

    // ══ 재생이 저장을 건넌다 ═══════════════════════════════════

    /// <summary>
    /// <b>저장을 건너서도 주기가 돈다.</b> 다 캔 시각을 <b>절대 세계 초</b>로 적어
    /// 두었으므로, 저장했다 불러온 뒤에도 같은 뺄셈이 성립한다.
    ///
    /// 남은 시간을 적었다면 여기서 값이 어긋난다 — 불러온 순간부터 다시 세므로
    /// 저장해 둔 사이에 흐른 세계 시간이 통째로 사라진다.
    /// </summary>
    [Test]
    public void 재생_시각은_저장을_건너서도_같은_답을_낸다()
    {
        const float 주기 = 180f;

        WorldClock.Skip(1000.0);
        float 다캔시각 = WorldClock.Seconds;

        // 저장 → (다른 세션) → 불러오기. 그 사이 세계는 100초 더 흘렀다.
        float 저장된시계 = WorldClock.Seconds;
        WorldClock.Reset();
        WorldClock.Restore(저장된시계 + 100.0);

        Assert.IsFalse(HarvestRespawnRule.HasRespawned(다캔시각, WorldClock.Seconds, 주기),
            "100초는 주기에 못 미친다");

        WorldClock.Skip(80.0);
        Assert.IsTrue(HarvestRespawnRule.HasRespawned(다캔시각, WorldClock.Seconds, 주기),
            "합쳐 180초가 지나면 돌아온다 — 저장을 건너서도 시계가 이어졌다");
    }

    /// <summary>
    /// <b>씬 로드로 0이 되던 성질이 사라졌다.</b> 프레임 시계였다면 여기서
    /// <c>now - depletedAt</c>이 음수가 되어 그 자리는 영영 안 돌아왔다.
    /// </summary>
    [Test]
    public void 씬을_갈아_끼워도_다_캔_시각이_미래가_되지_않는다()
    {
        WorldClock.Skip(5000.0);
        float 다캔시각 = WorldClock.Seconds;

        // 씬이 바뀌어도 시계를 모는 쪽이 다시 「처음」을 앉히지 못한다
        WorldClock.StartAt(420.0);

        Assert.GreaterOrEqual(WorldClock.Seconds, 다캔시각,
            "시계가 되감기면 다 캔 시각이 미래가 되고 그 자리는 영영 안 돌아온다");
    }

    // ══ 프레임 시계가 재생 경로에 남아 있지 않다 ═══════════════

    /// <summary>
    /// <b>사람이 되돌리는 것을 막는 진짜 회귀선.</b> 위의 테스트들은 시계가
    /// 옳게 굴러가는지를 재지만, 어느 날 누군가 <c>HarvestNode</c>에
    /// <c>Time.time</c> 한 줄을 다시 적어 넣으면 그 테스트들은 여전히 초록이다.
    /// 그래서 <b>파일을 직접 훑는다</b> — 이 저장소가 폐기어를 잡는 방식과 같다.
    ///
    /// 쿨다운은 대상이 아니다. 공격·점프·섭식 쿨다운은 저장될 이유가 없고
    /// 씬 로드로 리셋되는 것이 오히려 맞다. 여기서 보는 것은 <b>재생과
    /// 지핀 시각</b>, 곧 세계 상태에 실리는 시각뿐이다.
    /// </summary>
    [Test]
    public void 재생_경로에_프레임_시계가_없다()
    {
        string[] 파일들 =
        {
            "Assets/02.Scripts/Harvesting/HarvestNode.cs",
            "Assets/02.Scripts/Harvesting/GlowCapCluster.cs",
            "Assets/02.Scripts/Building/Campfire.cs",
        };

        // 조각으로 짓는다 — 이 파일 자신이 검사에 걸리면 안 되기 때문이다.
        string 프레임시계 = "Time" + ".time";

        var 걸린것 = new List<string>();
        foreach (var 상대 in 파일들)
        {
            string 절대 = Path.Combine(Directory.GetCurrentDirectory(), 상대);
            Assert.IsTrue(File.Exists(절대), $"검사 대상이 없다: {상대}");

            var 줄들 = File.ReadAllLines(절대);
            for (int i = 0; i < 줄들.Length; i++)
            {
                // 주석에 남은 설명은 봐준다. 코드에 남은 것만 잡는다.
                string 줄 = 줄들[i].TrimStart();
                if (줄.StartsWith("//") || 줄.StartsWith("///") || 줄.StartsWith("*")) continue;
                if (줄.Contains(프레임시계)) 걸린것.Add($"{상대}:{i + 1}");
            }
        }

        Assert.IsEmpty(걸린것,
            "재생과 지핀 시각이 프레임 시계로 돌아갔다. 그것은 저장되지 않고, " +
            "배속을 타고, 씬 로드로 0이 된다 — 셋 다 세계 상태가 원하지 않는 성질이다:\n  " +
            string.Join("\n  ", 걸린것));
    }
}
