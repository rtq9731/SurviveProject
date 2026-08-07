using NUnit.Framework;
using Survive.Vitals;

/// <summary>
/// <b>게이지는 저장된 값 그대로 돌아온다.</b> 넷을 한 규칙으로 본다.
///
/// 이 파일이 지키는 문장은 하나다 — <b>불러오기는 회복 수단이 아니다.</b>
/// 수분 5%로 저장하고 열었더니 100%가 되면 그것이 최적해가 되고, 물가로 돌아갈
/// 이유(<see cref="Sustenance"/>)가 통째로 사라진다.
///
/// 형식의 왕복만 본다. 실제 파일에 써지고 읽히는지는 PlayMode의
/// <c>E2EVitalsSave</c>가 본다.
/// </summary>
public class VitalsSaveTests
{
    static readonly string[] 게이지넷 = { "health", "oxygen", "hydration", "food" };

    static Vital[] 몸(params float[] 지금값)
    {
        var vitals = new Vital[지금값.Length];
        for (int i = 0; i < 지금값.Length; i++)
            vitals[i] = new Vital(100f, 지금값[i]);
        return vitals;
    }

    // ── 왕복 ─────────────────────────────────────────────────

    [Test]
    public void 게이지_넷이_저장을_넘긴다()
    {
        var 담은몸 = 몸(63f, 41f, 27f, 88f);
        var state = VitalsSave.Capture(게이지넷, 담은몸);

        var 새몸 = 몸(100f, 100f, 100f, 100f);
        for (int i = 0; i < 게이지넷.Length; i++)
            Assert.IsTrue(VitalsSave.RestoreInto(state, 게이지넷[i], 새몸[i]));

        Assert.AreEqual(63f, 새몸[0].Current, 0.001f);
        Assert.AreEqual(41f, 새몸[1].Current, 0.001f);
        Assert.AreEqual(27f, 새몸[2].Current, 0.001f);
        Assert.AreEqual(88f, 새몸[3].Current, 0.001f);
    }

    [Test]
    public void 불러오기는_회복_수단이_아니다()
    {
        // 수분 5%로 저장한다. 열었을 때 가득 차 있으면 「저장하고 다시 켜기」가
        // 물을 마시는 것보다 빠른 회복이 된다.
        var state = VitalsSave.Capture(new[] { "hydration" }, 몸(5f));

        var 새몸 = new Vital(100f, 100f);
        VitalsSave.RestoreInto(state, "hydration", 새몸);

        Assert.AreEqual(5f, 새몸.Current, 0.001f);
        Assert.Less(새몸.Current, 새몸.Max, "불러왔더니 가득이면 그것이 최적해가 된다");
    }

    [Test]
    public void 죽기_직전에_저장하면_죽기_직전으로_열린다()
    {
        // 저장은 죽음의 규칙을 다시 쓰지 않는다. 체력을 채우는 것은 Revive이고,
        // 그 일이 벌어진 뒤의 몸이 저장되면 가득 찬 채로 열린다 — 아래는 그
        // 반대편이다. 죽지 않고 저장했으면 그 몸 그대로다.
        var state = VitalsSave.Capture(new[] { "health" }, 몸(3f));

        var 새몸 = new Vital(100f, 100f);
        VitalsSave.RestoreInto(state, "health", 새몸);

        Assert.AreEqual(3f, 새몸.Current, 0.001f);
    }

    [Test]
    public void 산소도_예외가_아니다()
    {
        // 물속에서 산소 8%로 저장하고 나간다. 채워 주면 「잠수 직전 저장」이
        // 무한 산소통이 된다. 저장본에 위치가 없으므로 갇힐 일도 없다.
        var state = VitalsSave.Capture(new[] { "oxygen" }, 몸(8f));

        var 새몸 = new Vital(100f, 100f);
        VitalsSave.RestoreInto(state, "oxygen", 새몸);

        Assert.AreEqual(8f, 새몸.Current, 0.001f);
    }

    // ── 옛 저장본 ────────────────────────────────────────────

    [Test]
    public void 게이지_열쇠가_없는_저장본은_몸을_건드리지_않는다()
    {
        var 새몸 = new Vital(100f, 72f);

        Assert.IsFalse(VitalsSave.RestoreInto(null, "health", 새몸));
        Assert.AreEqual(72f, 새몸.Current, 0.001f, "적혀 있지 않은 것은 손대지 않는다");
    }

    [Test]
    public void 적혀_있지_않은_게이지는_기본값으로_남는다()
    {
        // 게이지가 넷이 되기 전에 쓰인 저장본. 수분·식량 칸이 없다.
        var state = VitalsSave.Capture(new[] { "health", "oxygen" }, 몸(50f, 50f));

        var 수분 = new Vital(100f, 100f);
        Assert.IsFalse(VitalsSave.RestoreInto(state, "hydration", 수분));
        Assert.AreEqual(100f, 수분.Current, 0.001f,
                        "0으로 채우면 옛 저장본이 사람을 바닥에서 깨운다");
    }

    [Test]
    public void 모르는_게이지가_적혀_있어도_읽힌다()
    {
        // 나중에 지워진 게이지가 남아 있는 저장본. 그 칸만 무시하고 나머지는 읽는다.
        var state = VitalsSave.Capture(new[] { "stamina", "health" }, 몸(10f, 44f));

        var 체력 = new Vital(100f, 100f);
        Assert.IsTrue(VitalsSave.RestoreInto(state, "health", 체력));
        Assert.AreEqual(44f, 체력.Current, 0.001f);
    }

    // ── 경계값 ───────────────────────────────────────────────

    [Test]
    public void 최대치를_넘는_저장값은_최대치까지만()
    {
        // 정의 에셋의 maxValue는 저장한 뒤에 바뀔 수 있다.
        var state = VitalsSave.Capture(new[] { "health" }, 몸(100f));

        var 좁아진몸 = new Vital(80f, 80f);
        VitalsSave.RestoreInto(state, "health", 좁아진몸);

        Assert.AreEqual(80f, 좁아진몸.Current, 0.001f);
    }

    [Test]
    public void 음수는_바닥까지만()
    {
        Assert.AreEqual(0f, VitalsSave.Clamped(-13f, 100f), 0.001f);
        Assert.AreEqual(0f, VitalsSave.Clamped(0f, 100f), 0.001f);
        Assert.AreEqual(100f, VitalsSave.Clamped(100f, 100f), 0.001f);
        Assert.AreEqual(0f, VitalsSave.Clamped(50f, -1f), 0.001f, "최대치가 음수여도 눈금 밖으로 못 나간다");
    }

    [Test]
    public void 숫자가_아닌_값은_적혀_있지_않은_것과_같다()
    {
        // 파일은 사람도 고칠 수 있다. NaN이 게이지에 들어가면 그 뒤로 모든 비교가
        // 거짓이 되어 몸이 굳는다 — 죽지도 채워지지도 않는다.
        var state = new VitalsSaveState
        {
            ids = new[] { "health", "oxygen" },
            values = new[] { float.NaN, float.PositiveInfinity },
        };

        var 체력 = new Vital(100f, 55f);
        var 산소 = new Vital(100f, 55f);

        Assert.IsFalse(VitalsSave.RestoreInto(state, "health", 체력));
        Assert.IsFalse(VitalsSave.RestoreInto(state, "oxygen", 산소));
        Assert.AreEqual(55f, 체력.Current, 0.001f);
        Assert.AreEqual(55f, 산소.Current, 0.001f);
    }

    [Test]
    public void 짝이_안_맞는_저장본은_겹치는_데까지만_읽는다()
    {
        var state = new VitalsSaveState
        {
            ids = new[] { "health", "oxygen", "hydration" },
            values = new[] { 30f },
        };

        var 체력 = new Vital(100f, 100f);
        var 산소 = new Vital(100f, 100f);

        Assert.IsTrue(VitalsSave.RestoreInto(state, "health", 체력));
        Assert.AreEqual(30f, 체력.Current, 0.001f);

        Assert.IsFalse(VitalsSave.RestoreInto(state, "oxygen", 산소));
        Assert.AreEqual(100f, 산소.Current, 0.001f);
    }

    [Test]
    public void 담을_때도_짝이_안_맞으면_짧은_쪽까지만()
    {
        var state = VitalsSave.Capture(게이지넷, 몸(10f, 20f));

        Assert.AreEqual(2, state.ids.Length);
        Assert.AreEqual(2, state.values.Length);
        Assert.AreEqual("health", state.ids[0]);
        Assert.AreEqual(20f, state.values[1], 0.001f);
    }

    [Test]
    public void 아무것도_없는_몸도_담긴다()
    {
        var state = VitalsSave.Capture(null, null);

        Assert.IsNotNull(state, "빈 저장본이 null 저장본보다 낫다");
        Assert.AreEqual(0, state.ids.Length);
    }
}
