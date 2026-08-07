using System;
using NUnit.Framework;
using Survive.Core;

/// <summary>
/// <b>검사가 사람의 저장본을 밟지 못한다.</b>
///
/// 저장 경로는 이 기계의 에디터 전부가 공유하고, E2E는 챕터 목표를 넘길 때마다
/// 자동 저장을 건드린다. 그래서 「슬롯을 손으로 넘기자」는 규율이 아니라
/// <b>길목의 규칙</b>이 되어야 했다(<see cref="SaveSlots"/>).
///
/// 아래에는 <b>음성 확인</b>이 함께 있다 — 격리를 걸지 않으면 기본 슬롯으로 간다는
/// 것까지 단언한다. 그것이 참이 되지 않으면 이 규칙은 아무것도 막고 있지 않은
/// 것이고, 통과한 검사들은 그저 늘 참인 문장을 읽고 있었던 것이 된다.
/// </summary>
public class SaveSlotTests
{
    // 정적 하나이므로 검사끼리 새어 나간다. 앞뒤로 모두 푼다.
    [SetUp]
    public void 시작할_때_격리를_푼다() => SaveSlots.Release();

    [TearDown]
    public void 끝날_때_격리를_푼다() => SaveSlots.Release();

    // ── 음성 확인: 슬롯을 안 갈면 사람의 저장본으로 간다 ──────

    [Test]
    public void 격리하지_않으면_이름_없는_저장이_기본_슬롯으로_간다()
    {
        Assert.IsFalse(SaveSlots.IsIsolated);
        Assert.AreEqual(SaveSlots.Default, SaveSlots.Resolve(null));
        Assert.AreEqual(SaveSlots.Default, SaveSlots.Resolve(""));
        Assert.IsTrue(SaveSlots.TouchesDefault(null),
                      "격리를 안 걸었는데 기본 슬롯을 안 건드린다면 이 검사는 아무것도 못 잡는다");
    }

    [Test]
    public void 격리하지_않으면_기본_슬롯을_콕_집은_요청도_그대로_간다()
    {
        Assert.AreEqual(SaveSlots.Default, SaveSlots.Resolve(SaveSlots.Default));
        Assert.IsTrue(SaveSlots.TouchesDefault(SaveSlots.Default));
    }

    // ── 양성 확인: 격리하면 닿지 못한다 ──────────────────────

    [Test]
    public void 격리하면_이름_없는_저장이_전용_슬롯으로_간다()
    {
        SaveSlots.Isolate("e2e_1234");

        Assert.IsTrue(SaveSlots.IsIsolated);
        Assert.AreEqual("e2e_1234", SaveSlots.Resolve(null));
        Assert.IsFalse(SaveSlots.TouchesDefault(null));
    }

    [Test]
    public void 격리하면_기본_슬롯을_콕_집어도_전용_슬롯으로_간다()
    {
        SaveSlots.Isolate("e2e_1234");

        // 이 한 줄이 이 규칙의 전부다. 시나리오가 Save("auto")·Delete()를 부르든
        // 자동 저장이 걸리든, 사람의 파일에는 닿지 못한다.
        Assert.AreEqual("e2e_1234", SaveSlots.Resolve(SaveSlots.Default));
        Assert.IsFalse(SaveSlots.TouchesDefault(SaveSlots.Default));
    }

    [Test]
    public void 자기_슬롯을_쓰던_시나리오는_격리_중에도_그_슬롯을_쓴다()
    {
        SaveSlots.Isolate("e2e_1234");

        // 이미 자기 슬롯을 쓰고 있던 시나리오(E2ELander·E2EBlueprintUnlock)의 슬롯
        // 둘을 하나로 합쳐 버리면 그쪽이 검증하던 「슬롯이 서로 다르다」가 사라진다.
        Assert.AreEqual("lander_e2e", SaveSlots.Resolve("lander_e2e"));
        Assert.AreEqual("blueprint_e2e", SaveSlots.Resolve("blueprint_e2e"));
        Assert.IsFalse(SaveSlots.TouchesDefault("lander_e2e"));
    }

    [Test]
    public void 격리를_풀면_사람의_저장으로_돌아온다()
    {
        SaveSlots.Isolate("e2e_1234");
        SaveSlots.Release();

        Assert.IsFalse(SaveSlots.IsIsolated);
        Assert.AreEqual(SaveSlots.Default, SaveSlots.Resolve(null));
    }

    // ── 격리 슬롯의 이름 ─────────────────────────────────────

    [Test]
    public void 기본_슬롯을_격리_슬롯으로_삼을_수_없다()
    {
        Assert.Throws<ArgumentException>(() => SaveSlots.Isolate(SaveSlots.Default));
        Assert.IsFalse(SaveSlots.IsIsolated, "던지고 나서도 격리가 걸려 있으면 안 된다");
    }

    [Test]
    public void 머리말_없는_이름은_격리_슬롯이_아니다()
    {
        Assert.Throws<ArgumentException>(() => SaveSlots.Isolate("temp"));
        Assert.Throws<ArgumentException>(() => SaveSlots.Isolate(""));
        Assert.Throws<ArgumentException>(() => SaveSlots.Isolate(null));
    }

    [Test]
    public void 세션마다_슬롯_이름이_다르다()
    {
        // 클론 셋이 동시에 돌면 이름이 같아지는 순간 서로의 검사를 밟는다.
        Assert.AreNotEqual(SaveSlots.IsolatedNameFor(1000), SaveSlots.IsolatedNameFor(2000));
        Assert.IsTrue(SaveSlots.IsIsolationSlot(SaveSlots.IsolatedNameFor(1000)));

        // 그리고 그 이름은 언제나 격리 슬롯으로 받아들여져야 한다.
        Assert.DoesNotThrow(() => SaveSlots.Isolate(SaveSlots.IsolatedNameFor(31337)));
    }

    [Test]
    public void 파일_이름은_슬롯_이름을_그대로_쓴다()
    {
        Assert.AreEqual("save_auto.json", SaveSlots.FileNameOf(SaveSlots.Default));
        Assert.AreEqual("save_e2e_1234.json", SaveSlots.FileNameOf("e2e_1234"));

        // FileNameOf는 풀이하지 않는다. 풀이는 부르는 쪽이 Resolve로 먼저 한다 —
        // 두 곳에서 풀이하면 「이미 풀린 이름을 또 푸는」 자리가 생긴다.
        SaveSlots.Isolate("e2e_1234");
        Assert.AreEqual("save_auto.json", SaveSlots.FileNameOf(SaveSlots.Default));
        Assert.AreEqual("save_e2e_1234.json",
                        SaveSlots.FileNameOf(SaveSlots.Resolve(SaveSlots.Default)));
    }
}
