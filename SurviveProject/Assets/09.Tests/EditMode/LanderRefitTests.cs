using System;
using System.Linq;
using NUnit.Framework;
using Survive.Building;

/// <summary>
/// 착륙선 개조 축 — <b>자리만 열려 있고 채워지지 않았다.</b>
///
/// 최종 목표가 이 배이므로(세계관 §7) EA 범위 안에 다 들어오지 않는다.
/// 그런 항목에서 가장 나쁜 결과는 비어 있는 것이 아니라 <b>그럴듯하게 채워지는 것</b>이다 —
/// 아무 데도 닿지 않는 진행률 막대와 아무것도 만들지 않는 재료표가 붙으면
/// 「최종 목표」가 잡무가 되고, 그것을 나중에 걷어내는 값이 처음부터 안 만든 값보다 크다.
///
/// 그래서 이 파일은 기능을 재지 않는다. <b>가짜 단계가 없다는 것</b>을 잰다.
/// </summary>
public class LanderRefitTests
{
    [Test]
    public void 못_떠나는_이유는_넷이고_세계관이_적어_둔_그_넷이다()
    {
        Assert.AreEqual(4, LanderRefit.Systems.Length);
        CollectionAssert.AreEquivalent(
            Enum.GetValues(typeof(LanderSystem)).Cast<LanderSystem>().ToArray(),
            LanderRefit.Systems,
            "열거값과 목록이 어긋나면 어느 쪽이 진짜인지 알 수 없다");

        // 다섯째를 더하려면 세계관부터 고쳐야 한다는 것을 여기서 못 박는다.
        CollectionAssert.AreEquivalent(
            new[] { LanderSystem.Destination, LanderSystem.Fuel,
                    LanderSystem.Navigation, LanderSystem.Hull },
            LanderRefit.Systems);
    }

    [Test]
    public void 이_릴리스에서_열린_계통은_하나도_없다()
    {
        CollectionAssert.IsEmpty(LanderRefit.Repairable,
            "계통을 하나 열었으면 그 계통이 무엇으로 열리는지도 함께 서 있어야 한다. " +
            "여기만 늘리면 「고칠 수 있다」고 말하고 아무 일도 일어나지 않는다");

        foreach (var s in LanderRefit.Systems)
            Assert.IsFalse(LanderRefit.CanRepair(s), $"{s}가 열려 있다");
    }

    [Test]
    public void 어떤_계통도_고칠_수_없고_사유는_하나다()
    {
        var 장부 = new LanderRefitLedger();

        foreach (var s in LanderRefit.Systems)
        {
            Assert.AreEqual(RefitVerdict.NotInThisRelease, 장부.TryComplete(s),
                $"{s}를 고칠 수 있게 됐다. 가짜 단계가 붙었는지 보라");
            Assert.IsFalse(장부.IsDone(s));
        }

        Assert.AreEqual(0, 장부.DoneCount);
    }

    [Test]
    public void 몇_번을_두드려도_배는_떠나지_못한다()
    {
        // "여러 번 부르면 언젠가 열린다" 같은 우연한 통로가 없는지 본다.
        var 장부 = new LanderRefitLedger();

        for (int i = 0; i < 50; i++)
            foreach (var s in LanderRefit.Systems)
                장부.TryComplete(s);

        Assert.IsFalse(장부.IsSpacefaring,
            "성간 이동이 가능해졌다. 이 값이 참이 되는 날은 게임이 끝나는 날이다");
        Assert.AreEqual(0, 장부.DoneCount);
    }

    [Test]
    public void 장부는_비어_있는_채로_저장을_넘긴다()
    {
        var 장부 = new LanderRefitLedger();
        CollectionAssert.IsEmpty(장부.Capture());

        var 다시 = new LanderRefitLedger();
        다시.Restore(장부.Capture());
        Assert.AreEqual(0, 다시.DoneCount);
    }

    [Test]
    public void 모르는_이름은_건너뛴다()
    {
        // 저장본은 열쇠-값 목록이라 덧붙임이 자유롭고, 그 자유는 읽는 쪽이
        // 모르는 것을 조용히 넘길 때만 성립한다. 다음 릴리스가 적어 둔 것을
        // 이 릴리스가 열어도 터지지 않아야 한다.
        var 장부 = new LanderRefitLedger();

        Assert.DoesNotThrow(() => 장부.Restore(new[] { "warp_core", null, "", "선실" }));
        Assert.AreEqual(0, 장부.DoneCount, "모르는 이름이 칸을 채웠다");

        Assert.DoesNotThrow(() => 장부.Restore(null));
        Assert.AreEqual(0, 장부.DoneCount);
    }

    [Test]
    public void 아는_이름은_그대로_되읽는다()
    {
        // <b>장부는 관문이 아니라 적바림이다.</b> 언젠가 계통 하나가 열려 저장되면
        // 그것을 그대로 되읽어야 한다 — 여기서 걸러 버리면 다음 라운드가
        // "고쳤는데 껐다 켜니 사라진다"를 만나고, 원인을 이 파일에서 찾지 못한다.
        //
        // 오늘 게임 안에서 이 상태에 닿는 길은 없다. 그것을 막는 것은 장부가
        // 아니라 LanderRefit.Repairable이 비어 있다는 사실이다.
        var 장부 = new LanderRefitLedger();
        장부.Restore(new[] { "fuel", "hull" });

        Assert.IsTrue(장부.IsDone(LanderSystem.Fuel));
        Assert.IsTrue(장부.IsDone(LanderSystem.Hull));
        Assert.IsFalse(장부.IsDone(LanderSystem.Destination));
        Assert.AreEqual(2, 장부.DoneCount);

        CollectionAssert.AreEqual(new[] { "fuel", "hull" }, 장부.Capture(),
            "적는 순서는 세계관이 적어 둔 순서를 따른다 — 저장본이 흔들리면 diff가 시끄럽다");

        // 되읽기는 덮어쓰기다. 앞 판의 상태가 남으면 이어하기가 앞 판을 섞는다.
        장부.Restore(new string[0]);
        Assert.AreEqual(0, 장부.DoneCount);
    }

    [Test]
    public void 계통마다_저장에_적을_이름이_있고_되읽힌다()
    {
        foreach (var s in LanderRefit.Systems)
        {
            string id = LanderRefit.Id(s);
            Assert.IsFalse(string.IsNullOrEmpty(id), $"{s}에 저장 이름이 없다");
            Assert.IsTrue(LanderRefit.TryParse(id, out var 되읽은것));
            Assert.AreEqual(s, 되읽은것);
        }

        Assert.AreEqual(LanderRefit.Systems.Length,
                        LanderRefit.Systems.Select(LanderRefit.Id).Distinct().Count(),
                        "저장 이름이 겹치면 두 계통이 한 칸을 나눠 쓴다");
    }
}
