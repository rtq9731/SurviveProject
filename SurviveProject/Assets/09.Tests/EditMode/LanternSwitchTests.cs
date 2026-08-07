using System.IO;
using NUnit.Framework;
using UnityEngine;
using Survive.Items;
using Survive.World;

/// <summary>
/// 검토회신 2026-08-07 ② — <b>랜턴은 끌 수 있다.</b>
///
/// "상시 점등"은 기계적 잠금이 아니라 <b>끌 이유가 없게 설계한다</b>는 의도였다.
/// 끌 수 있어야 "어둠은 비용"이 실제 <b>선택</b>이 된다 — 잠그면 플레이어가
/// 비용을 감수할 방법이 없어 어둠이 상수로 굳는다.
///
/// 그래서 여기서 보는 것은 넷이다.
/// 1. <b>F로 켜고 끈다</b> — 스위치 규칙의 경계값.
/// 2. <b>꺼지면 밝은 구역이 사라진다</b> — 낫이 읽는 창구까지 실제로 꺼진다.
/// 3. <b>꺼진 동안 배터리가 줄지 않는다</b> — 아끼는 대신 못 보는 것이
///    거래의 다른 쪽이다. 꺼도 계속 닳으면 끄는 것이 순수한 손해라 아무도
///    끄지 않고, 그러면 잠그지 않았을 뿐 잠근 것과 같다.
/// 4. <b>어제 라운드의 성과는 그대로다</b> — 장비 칸·오프셋·반경·눈금 기준.
///    되돌린 것은 스위치 하나뿐이라는 것을 회귀로 못 박는다.
/// </summary>
public class LanternSwitchTests
{
    // ── ① 스위치 자체 ───────────────────────────────────────

    [Test]
    public void 스위치는_켜진_채로_시작한다()
    {
        Assert.IsTrue(LanternRule.DefaultSwitchedOn,
                      "손에 넣는 순간 이유 없이 어두우면 물건이 고장 난 것으로 읽힌다");
    }

    [Test]
    public void F를_누르면_꺼지고_다시_누르면_켜진다()
    {
        bool 스위치 = LanternRule.DefaultSwitchedOn;

        스위치 = LanternRule.NextSwitchState(스위치, 1);
        Assert.IsFalse(스위치, "한 번 누르면 꺼진다");

        스위치 = LanternRule.NextSwitchState(스위치, 1);
        Assert.IsTrue(스위치, "다시 누르면 켜진다 - 왕복이 되어야 선택이다");
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void 어느_티어든_같은_키로_켜고_끈다(int 티어)
    {
        Assert.IsFalse(LanternRule.NextSwitchState(true, 티어));
        Assert.IsTrue(LanternRule.NextSwitchState(false, 티어));
    }

    [Test]
    public void 랜턴이_없으면_눌러도_스위치가_안_움직인다()
    {
        // 제작 전에 꺼 두면 랜턴을 손에 넣는 순간 이유 없이 어둡고,
        // 플레이어는 무엇을 눌러야 하는지 배운 적이 없다.
        Assert.AreEqual(LanternRule.DefaultSwitchedOn, LanternRule.NextSwitchState(true, 0));
        Assert.AreEqual(LanternRule.DefaultSwitchedOn, LanternRule.NextSwitchState(false, 0));
        Assert.AreEqual(LanternRule.DefaultSwitchedOn, LanternRule.NextSwitchState(false, -2));
    }

    // ── ② 꺼진 상태의 규칙 ──────────────────────────────────

    [Test]
    public void 껐으면_배터리가_가득이어도_어둡다()
    {
        Assert.IsFalse(LanternRule.IsLit(1, LanternRule.MaxBattery, false));
        Assert.IsFalse(LanternRule.IsLit(LanternRule.MaxTier, LanternRule.MaxBattery, false));
    }

    [Test]
    public void 껐어도_켤_재료는_그대로_남는다()
    {
        // 불이 없는 상태에 이르는 길이 둘이라는 것이 예전과 갈리는 자리다 —
        // 껐거나(선택), 다 태웠거나(비용을 다 낸 결과). 이 둘을 구별하지 못하면
        // 배터리 눈금이 무엇을 말하는지도 정할 수 없다.
        Assert.IsTrue(LanternRule.CanLight(1, LanternRule.MaxBattery),
                      "껐다고 배터리가 사라지는 것은 아니다");
        Assert.IsFalse(LanternRule.CanLight(1, 0f), "다 태우면 재료가 없다");
        Assert.IsFalse(LanternRule.CanLight(0, LanternRule.MaxBattery), "랜턴이 없으면 재료가 없다");
    }

    [Test]
    public void 꺼진_동안_배터리가_줄지_않는다()
    {
        float 남은것 = LanternRule.AfterDrain(LanternRule.MaxBattery, 1, 600f, switchedOn: false);
        Assert.AreEqual(LanternRule.MaxBattery, 남은것, 0.0001f,
                        "끄면 아끼는 것이 거래의 한쪽이다");
    }

    [Test]
    public void 켜_두면_지난_시간만큼_실제로_준다()
    {
        float 켠뒤 = LanternRule.AfterDrain(LanternRule.MaxBattery, 1, 10f, switchedOn: true);
        Assert.Less(켠뒤, LanternRule.MaxBattery, "켜 두면 시계가 돈다");
        Assert.AreEqual(LanternRule.AfterDrain(LanternRule.MaxBattery, 1, 10f), 켠뒤, 0.0001f,
                        "켠 채로 부르는 것과 스위치를 켜고 부르는 것은 같은 값이어야 한다");
    }

    [Test]
    public void 껐다_켜도_이미_태운_배터리는_돌아오지_않는다()
    {
        float 태운뒤 = LanternRule.AfterDrain(LanternRule.MaxBattery, 1, 20f, switchedOn: true);
        float 꺼둔뒤 = LanternRule.AfterDrain(태운뒤, 1, 300f, switchedOn: false);

        Assert.AreEqual(태운뒤, 꺼둔뒤, 0.0001f, "스위치는 배터리를 만들어 내지 않는다");
        Assert.Less(꺼둔뒤, LanternRule.MaxBattery);
    }

    [Test]
    public void 다_태운_랜턴은_켜도_어둡다()
    {
        Assert.IsFalse(LanternRule.IsLit(1, 0f, true),
                       "스위치는 재료를 대신하지 않는다");
    }

    // ── ③ 꺼지면 밝은 구역이 사라진다 ───────────────────────

    /// <summary>
    /// 랜턴이 세계에 내놓는 얼굴. <see cref="LanternController"/>가 하는 것과
    /// 같은 계산을 규칙에서 그대로 끌어온다 — 씬 없이 등록부까지 확인하려는 것이다.
    /// </summary>
    class 스위치달린랜턴 : IOffsetLitSource
    {
        public int Tier = 1;
        public float Battery = LanternRule.MaxBattery;
        public bool Switch = LanternRule.DefaultSwitchedOn;

        public Vector3 Anchor = Vector3.zero;
        public Vector3 Look = Vector3.forward;

        public bool IsLit => LanternRule.IsLit(Tier, Battery, Switch);
        public float LitZoneRadius => LanternRule.RadiusForTier(Tier);
        public Vector3 LitAnchor => Anchor;
        public Vector3 LitForward => LanternRule.Facing(Look);
        public Vector3 LitZoneCenter =>
            LanternRule.LitCenter(Anchor, Look, LanternRule.OffsetForTier(Tier));
    }

    [SetUp]
    public void 초기화() => LitZoneRegistry.Clear();

    [TearDown]
    public void 정리() => LitZoneRegistry.Clear();

    [Test]
    public void 끄면_밝은_구역이_사라진다()
    {
        var 랜턴 = new 스위치달린랜턴();
        LitZoneRegistry.Register(랜턴);

        var 앞 = Vector3.forward * (LanternRule.Tier1ForwardOffset);
        Assert.IsTrue(LitZoneRegistry.IsLit(앞), "켜져 있으면 앞이 밝다");

        랜턴.Switch = false;
        Assert.IsFalse(LitZoneRegistry.IsLit(앞), "끄면 같은 자리가 어두워진다");
        Assert.IsFalse(LitZoneRegistry.IsLit(Vector3.zero), "선 자리조차 어둡다");
    }

    [Test]
    public void 껐다_켜면_밝은_구역이_그대로_돌아온다()
    {
        var 랜턴 = new 스위치달린랜턴();
        LitZoneRegistry.Register(랜턴);

        var 앞 = Vector3.forward * LanternRule.Tier1ForwardOffset;
        랜턴.Switch = false;
        Assert.IsFalse(LitZoneRegistry.IsLit(앞));

        랜턴.Switch = true;
        Assert.IsTrue(LitZoneRegistry.IsLit(앞), "왕복이 되어야 선택이다");
    }

    /// <summary>
    /// 낫이 읽는 창구까지 실제로 꺼지는가. 여기가 안 꺼지면 화면은 어두운데
    /// 규칙은 밝다고 답하고, 그러면 끈 대가가 아무 데도 청구되지 않는다.
    /// </summary>
    [Test]
    public void 끄면_등_뒤_사각을_따질_것도_없이_전부_어둡다()
    {
        var 랜턴 = new 스위치달린랜턴();
        LitZoneRegistry.Register(랜턴);

        var 등뒤 = Vector3.back * (LanternRule.BackReach + 1f);
        Assert.IsTrue(LitZoneRegistry.IsBlindSide(등뒤), "켜져 있을 때는 등 뒤가 사각이다");

        랜턴.Switch = false;
        Assert.IsFalse(LitZoneRegistry.IsBlindSide(등뒤),
                       "꺼진 랜턴에는 사각이 없다 - 전부 어두우므로 가릴 것이 없다");
        Assert.IsFalse(LitZoneRegistry.IsLit(등뒤));
    }

    // ── ④ 어제 성과 회귀 — 되돌린 것은 스위치 하나뿐이다 ────

    [Test]
    public void 스위치가_돌아와도_반경은_티어1_8m_그대로다()
    {
        Assert.AreEqual(8f, LanternRule.Tier1Radius, 0.0001f,
                        "반경을 되돌리라고 하지 않았다");
        Assert.AreEqual(LanternRule.Tier1Radius, LanternRule.RadiusForTier(1), 0.0001f);
    }

    [Test]
    public void 스위치가_돌아와도_오프셋과_등_뒤_사각의_관계는_그대로다()
    {
        // 값 자체는 2026-08-07에 3m → 6.5m로 올랐다(사각이 원이 되면서 함께 움직였다).
        // 여기서 지키는 것은 <b>수가 아니라 관계</b>다 — 스위치를 되돌린 것이
        // 오프셋 체계를 건드리지 않았다는 것.
        Assert.AreEqual(6.5f, LanternRule.Tier1ForwardOffset, 0.0001f);
        Assert.AreEqual(LanternRule.Tier1ForwardOffset, LanternRule.OffsetForTier(1), 0.0001f,
                        "티어 1의 오프셋은 손잡이 값 그대로다");
        Assert.AreEqual(LanternRule.Tier1ForwardOffset, LanternRule.BlindSpotDepthForTier(1), 0.0001f,
                        "사각의 크기 = 오프셋이라는 등호는 그대로다");
        Assert.AreEqual(LanternRule.Tier1Radius - LanternRule.Tier1ForwardOffset,
                        LanternRule.BackReach, 0.0001f);
    }

    static ToolItemSO 랜턴에셋(int tier = 1, string id = "lantern")
    {
        var it = ScriptableObject.CreateInstance<ToolItemSO>();
        it.id = id;
        it.displayName = id;
        it.maxStack = 1;
        it.category = ItemCategory.Tool;
        it.equipSlot = EquipmentSlotKind.Light;
        it.tier = tier;
        return it;
    }

    [Test]
    public void 스위치가_돌아와도_랜턴은_장비_칸에_들어간다()
    {
        var inv = new Inventory(15, new EquipmentSlots());
        inv.TryAdd(랜턴에셋(), 1);

        Assert.AreEqual(1, inv.Equipment.CountOf("lantern"), "조명 자리에 걸린다(§11)");
        Assert.AreEqual(1, LanternRule.EquippedTier(inv));
    }

    /// <summary>
    /// ⑨ 유지 — 퀵슬롯에는 넣지 않는다. 착용물이지 손에 드는 도구가 아니다.
    /// 퀵슬롯은 <c>Inventory.Slots</c>만 훑으므로(QuickSlotBar.Refresh),
    /// 랜턴이 장비 자리로 가면 그것만으로 퀵슬롯에서 빠진다.
    /// </summary>
    [Test]
    public void 스위치가_돌아와도_랜턴은_퀵슬롯이_훑는_칸에_없다()
    {
        var inv = new Inventory(15, new EquipmentSlots());
        inv.TryAdd(랜턴에셋(), 1);

        Assert.AreEqual(0, inv.CountInSlots("lantern"),
                        "칸에 있으면 퀵슬롯에 뜬다 - F 토글이 있으므로 둘 이유가 없다");
    }

    /// <summary>
    /// 배터리 눈금은 <b>소지</b> 기준으로 뜬다. 껐거나 다 태운 순간이 눈금이
    /// 가장 필요한 순간인데, 켜짐을 기준으로 숨기면 그때 함께 사라져
    /// 플레이어가 어두워진 이유를 화면 어디서도 확인할 수 없다.
    /// </summary>
    [Test]
    public void 배터리_눈금은_켜짐이_아니라_소지로_뜬다()
    {
        string path = Path.Combine(Application.dataPath, "02.Scripts", "UI", "BatteryBarView.cs");
        Assert.IsTrue(File.Exists(path), "BatteryBarView를 찾지 못했다: " + path);

        string text = File.ReadAllText(path);
        StringAssert.Contains("HasLantern", text);
        StringAssert.DoesNotContain("_lantern.IsOn", text,
                                    "켜짐 기준으로 숨기면 꺼진 순간 눈금까지 사라진다");
    }
}
