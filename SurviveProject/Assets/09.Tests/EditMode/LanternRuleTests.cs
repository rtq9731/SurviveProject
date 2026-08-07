using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Survive.Items;
using Survive.Localization;
using Survive.World;

/// <summary>
/// 챕터 1 재건 스펙 §12 - 랜턴의 반경은 작고, 넓은 티어는 더 먹는다.
///
/// 지켜야 하는 규칙은 넷이다.
/// 1. <b>끌 수 있다.</b> F 배선이 살아 있어야 한다(검토회신 2026-08-07 ②).
///    스위치 자체의 규칙은 <c>LanternSwitchTests</c>가 따로 본다.
/// 2. 불이 들어오는 재료는 둘이다 - 랜턴을 가졌는가, 배터리가 남았는가.
/// 3. 반경은 티어에서 나오고, 넓은 티어는 초당 더 먹는다.
/// 4. <b>수치를 여기 베껴 적지 않는다.</b> 랜턴 반경·초당 소모는 사람이 정할
///    튜닝 5값 중 셋이고(기획서 §9, 실행 스펙 §16), 상수를 돌릴 때마다 검사가
///    거짓으로 깨지면 손잡이가 손잡이가 아니게 된다. 그래서 아래 단언은
///    전부 <see cref="LanternRule"/>의 상수를 <b>참조</b>해서 관계만 본다.
/// </summary>
public class LanternRuleTests
{
    // ── ① 반경 - 티어에서 나온다 ─────────────────────────────

    [TestCase(1, 0)]
    [TestCase(2, 1)]
    [TestCase(3, 2)]
    public void 티어가_오를수록_반경이_한_칸씩_넓어진다(int 티어, int 칸수)
    {
        Assert.AreEqual(LanternRule.Tier1Radius + LanternRule.RadiusPerTier * 칸수,
                        LanternRule.RadiusForTier(티어), 0.0001f);
    }

    [Test]
    public void 랜턴이_없으면_반경도_소모도_없다()
    {
        Assert.AreEqual(0f, LanternRule.RadiusForTier(0), "티어 0은 자리가 아니라 부재다");
        Assert.AreEqual(0f, LanternRule.DrainForTier(0), "만들기도 전에 배터리가 닳으면 버그다");
        Assert.AreEqual(0f, LanternRule.RadiusForTier(-3), "음수도 부재로 본다");
    }

    [Test]
    public void 최고_티어를_넘겨_물어도_최고_티어로_답한다()
    {
        Assert.AreEqual(LanternRule.RadiusForTier(LanternRule.MaxTier),
                        LanternRule.RadiusForTier(LanternRule.MaxTier + 5), 0.0001f);
        Assert.AreEqual(LanternRule.DrainForTier(LanternRule.MaxTier),
                        LanternRule.DrainForTier(LanternRule.MaxTier + 5), 0.0001f);
    }

    /// <summary>
    /// 반경이 관대하면 어둠은 배터리가 다했을 때만 나타나는 처벌이 된다(스펙 §12).
    /// 화톳불보다 넓은 랜턴을 들고 다니면 거점을 세울 이유가 없어지므로,
    /// 티어 1은 <b>화톳불보다 좁아야</b> 한다.
    /// </summary>
    [Test]
    public void 티어1_반경은_화톳불_불빛보다_좁다()
    {
        const float 화톳불반경 = 10f;   // Campfire.fullRange (프리팹 기본값)
        Assert.Less(LanternRule.Tier1Radius, 화톳불반경,
                    "랜턴이 거점보다 넓으면 거점을 세울 이유가 사라진다");
    }

    // ── ② 소모 - 넓게 보면 더 낸다 ───────────────────────────

    [TestCase(1, 0)]
    [TestCase(2, 1)]
    [TestCase(3, 2)]
    public void 티어가_오를수록_초당_소모가_한_칸씩_는다(int 티어, int 칸수)
    {
        Assert.AreEqual(LanternRule.Tier1DrainPerSecond + LanternRule.DrainPerTier * 칸수,
                        LanternRule.DrainForTier(티어), 0.0001f);
    }

    /// <summary>
    /// 티어가 순수 상향이면 고를 것이 없고, 고를 것이 없으면 스위치에서 뺏은
    /// 선택을 돌려준 것이 아니다. 넓게 볼 것인가 오래 버틸 것인가여야 한다.
    /// </summary>
    [Test]
    public void 넓은_랜턴은_오래_못_간다()
    {
        for (int t = 1; t < LanternRule.MaxTier; t++)
        {
            Assert.Greater(LanternRule.RadiusForTier(t + 1), LanternRule.RadiusForTier(t),
                           $"티어 {t + 1}이 더 넓어야 업그레이드다");
            Assert.Less(LanternRule.SecondsOfLight(LanternRule.MaxBattery, t + 1),
                        LanternRule.SecondsOfLight(LanternRule.MaxBattery, t),
                        $"티어 {t + 1}이 더 오래 가면 판단할 것이 없다");
        }
    }

    // ── ③ 배터리 - 경계값 ────────────────────────────────────

    [Test]
    public void 랜턴이_없으면_배터리가_닳지_않는다()
    {
        Assert.AreEqual(LanternRule.MaxBattery,
                        LanternRule.AfterDrain(LanternRule.MaxBattery, 0, 10f), 0.0001f);
    }

    [Test]
    public void 다_쓴_배터리는_음수로_내려가지_않는다()
    {
        Assert.AreEqual(0f, LanternRule.AfterDrain(0f, 1, 999f), 0.0001f);
        Assert.AreEqual(0f, LanternRule.AfterDrain(0.1f, LanternRule.MaxTier, 999f), 0.0001f);
    }

    [Test]
    public void 가득_찬_배터리는_더_채워도_넘치지_않는다()
    {
        Assert.AreEqual(LanternRule.MaxBattery,
                        LanternRule.AfterRecharge(LanternRule.MaxBattery, 9999f), 0.0001f);
        Assert.AreEqual(LanternRule.MaxBattery,
                        LanternRule.AfterRecharge(0f, LanternRule.BatteryPerCell), 0.0001f,
                        "셀 하나가 가득을 채운다 - 화톳불 추출 레시피와 눈금이 같은 말을 해야 한다");
    }

    [Test]
    public void 흐른_시간이_0이면_배터리가_그대로다()
    {
        Assert.AreEqual(42f, LanternRule.AfterDrain(42f, 1, 0f), 0.0001f);
        Assert.AreEqual(42f, LanternRule.AfterRecharge(42f, 0f), 0.0001f);
    }

    [Test]
    public void 한_칸이라도_남으면_켜져_있고_다_쓰면_꺼진다()
    {
        Assert.IsTrue(LanternRule.IsLit(1, 0.001f, true), "남아 있으면 켜져 있다");
        Assert.IsTrue(LanternRule.IsLit(1, LanternRule.MaxBattery, true), "가득이면 당연히 켜져 있다");
        Assert.IsFalse(LanternRule.IsLit(1, 0f, true), "다 쓰면 꺼진다 - 스위치를 켜 두어도 그렇다");
    }

    [Test]
    public void 랜턴이_없으면_배터리가_가득이어도_어둡다()
    {
        Assert.IsFalse(LanternRule.IsLit(0, LanternRule.MaxBattery, true),
                       "제작 전에는 어둠을 그대로 견딘다");
    }

    [Test]
    public void 경고는_임계_이하에서만_울리고_꺼진_랜턴은_울리지_않는다()
    {
        float 임계 = LanternRule.MaxBattery * LanternRule.FlickerThreshold;
        Assert.IsTrue(LanternRule.IsWarning(1, 임계, true), "임계값에 닿는 순간부터 깜빡인다");
        Assert.IsTrue(LanternRule.IsWarning(1, 임계 * 0.5f, true));
        Assert.IsFalse(LanternRule.IsWarning(1, 임계 + 1f, true), "임계 위에서는 아직 조용하다");
        Assert.IsFalse(LanternRule.IsWarning(1, 0f, true), "이미 꺼진 것을 경고할 이유가 없다");
        Assert.IsFalse(LanternRule.IsWarning(0, 임계, true), "없는 랜턴은 경고하지 않는다");
        Assert.IsFalse(LanternRule.IsWarning(1, 임계, false), "스위치를 내렸으면 깜빡일 빛이 없다");
    }

    [Test]
    public void 가득_찬_배터리로_버티는_시간은_최대치를_소모로_나눈_값이다()
    {
        Assert.AreEqual(LanternRule.MaxBattery / LanternRule.Tier1DrainPerSecond,
                        LanternRule.SecondsOfLight(LanternRule.MaxBattery, 1), 0.001f);
        Assert.AreEqual(LanternRule.FullBatterySecondsAtTier1,
                        LanternRule.SecondsOfLight(LanternRule.MaxBattery, 1), 0.001f);
        Assert.AreEqual(0f, LanternRule.SecondsOfLight(LanternRule.MaxBattery, 0),
                        "랜턴이 없으면 버틸 것도 없다");
    }

    // ── ④ 무엇이 랜턴인가 ───────────────────────────────────

    static ToolItemSO 랜턴(int tier = 1, string id = "lantern")
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

    static ItemDataSO 곡괭이()
    {
        var it = ScriptableObject.CreateInstance<ToolItemSO>();
        it.id = "pickaxe";
        it.category = ItemCategory.Tool;
        it.equipSlot = EquipmentSlotKind.None;
        it.tier = 3;
        return it;
    }

    [Test]
    public void 조명_자리에_걸리는_것만_랜턴이다()
    {
        Assert.AreEqual(2, LanternRule.TierOf(랜턴(2)), "티어는 도구 티어를 그대로 읽는다");
        Assert.AreEqual(0, LanternRule.TierOf(곡괭이()), "티어 3짜리 곡괭이도 랜턴은 아니다");
        Assert.AreEqual(0, LanternRule.TierOf(null));
        Assert.AreEqual(LanternRule.MaxTier, LanternRule.TierOf(랜턴(99)), "상한을 넘기지 않는다");
    }

    [Test]
    public void 장비_자리에_걸린_것이_가방_속_상위_티어를_이긴다()
    {
        var inv = new Inventory(15, new EquipmentSlots());
        inv.TryAdd(랜턴(1), 1);                       // 장비 자리로 간다
        inv.TryAdd(랜턴(LanternRule.MaxTier, "lantern_t3"), 1);   // 자리가 찼으니 칸으로 간다

        Assert.AreEqual(1, LanternRule.EquippedTier(inv),
                        "가진 것 중 제일 좋은 것이 저절로 켜지면 티어 교체가 판단이 아니게 된다");
    }

    [Test]
    public void 장비_자리가_비면_칸에_있는_랜턴이_켜진다()
    {
        // 저장 복원은 칸에 직접 앉히는 길을 하나 남겨 둔다(Inventory.RehomeEquipment).
        // 그 사이에 불이 꺼지면 불러오기 한 번에 어둠이 된다.
        var inv = new Inventory(15, new EquipmentSlots());
        inv.TryAdd(랜턴(1), 1);                            // 장비 자리로 간다
        inv.TryAdd(랜턴(2, "lantern_t2"), 1);              // 자리가 찼으니 칸으로 간다
        inv.Equipment.Clear();                             // 자리에 걸린 것만 사라진다

        Assert.AreEqual(0, inv.Equipment.CountOf("lantern"), "자리는 비었다");
        Assert.AreEqual(1, inv.CountInSlots("lantern_t2"), "칸에는 남아 있다");
        Assert.AreEqual(2, LanternRule.EquippedTier(inv));
    }

    [Test]
    public void 랜턴이_하나도_없으면_티어는_0이다()
    {
        var inv = new Inventory(15, new EquipmentSlots());
        inv.TryAdd(곡괭이(), 1);

        Assert.AreEqual(0, LanternRule.EquippedTier(inv));
        Assert.AreEqual(0, LanternRule.EquippedTier(null));
    }

    // ── ⑤ 끄는 길이 코드에 살아 있다 ────────────────────────

    static string 스크립트뿌리 => Path.Combine(Application.dataPath, "02.Scripts");

    static IEnumerable<string> 소스전부(string 확장자) =>
        Directory.EnumerateFiles(스크립트뿌리, 확장자, SearchOption.AllDirectories);

    static string 짧은이름(string full) =>
        "Assets" + full.Substring(Application.dataPath.Length).Replace('\\', '/');

    /// <summary>
    /// <b>이 검사가 이 항목의 핵심이다.</b> 규칙을 아무리 옳게 써도 F를 받는
    /// 배선이 한 가닥이라도 끊어져 있으면 랜턴은 그대로 켜져만 있다. 배선은
    /// 넷을 지나므로(에셋 → 생성 코드 → 리더 → 몸) 넷을 다 센다.
    /// </summary>
    [Test]
    public void 랜턴을_끄는_입력_배선이_네_군데_모두_살아_있다()
    {
        var 지나야하는곳 = new Dictionary<string, string>
        {
            { Path.Combine(스크립트뿌리, "Input", "PlayerInputActions.inputactions"), "ToggleLantern" },
            { Path.Combine(스크립트뿌리, "Input", "PlayerInputActions.cs"), "OnToggleLantern" },
            { Path.Combine(스크립트뿌리, "Input", "InputReaderSO.cs"), "ToggleLanternEvent" },
            { Path.Combine(스크립트뿌리, "Player", "PlayerToolUser.cs"), "ToggleLanternEvent" },
        };

        foreach (var 쌍 in 지나야하는곳)
        {
            Assert.IsTrue(File.Exists(쌍.Key), "찾지 못했다: " + 쌍.Key);
            StringAssert.Contains(쌍.Value, File.ReadAllText(쌍.Key),
                                  $"{Path.GetFileName(쌍.Key)}에서 배선이 끊겼다");
        }
    }

    [Test]
    public void F키가_실제로_토글에_묶여_있다()
    {
        string path = Path.Combine(스크립트뿌리, "Input", "PlayerInputActions.inputactions");
        string text = File.ReadAllText(path);

        int 바인딩 = text.IndexOf("<Keyboard>/f", System.StringComparison.Ordinal);
        Assert.Greater(바인딩, 0, "F 바인딩이 없다");

        // 바인딩 블록 안에서 액션 이름이 이어져야 한다. 액션만 있고 키가
        // 다른 데로 가 있으면 누를 키가 없는 기능이 된다.
        int 액션 = text.IndexOf("\"action\": \"ToggleLantern\"", 바인딩, System.StringComparison.Ordinal);
        Assert.Greater(액션, 바인딩, "F가 ToggleLantern에 묶여 있지 않다");
        Assert.Less(액션 - 바인딩, 300, "같은 바인딩 블록 안이어야 한다");
    }

    [Test]
    public void 랜턴_컨트롤러에_켜고_끄는_공개_창구가_있다()
    {
        string path = Path.Combine(스크립트뿌리, "World", "LanternController.cs");
        Assert.IsTrue(File.Exists(path), "LanternController를 찾지 못했다: " + path);

        string text = File.ReadAllText(path);
        Assert.IsTrue(text.Contains("public void Toggle"), "F가 부를 창구가 없다");
        Assert.IsTrue(text.Contains("public void SetSwitch"), "상태를 못 박을 창구가 없다");
        Assert.IsTrue(text.Contains("public bool IsOn"), "켜짐 여부는 여전히 읽을 수 있어야 한다");
        Assert.IsTrue(text.Contains("public bool HasLantern"),
                      "배터리 눈금은 소지 여부로 뜬다 - 꺼져도 눈금은 남아야 한다");
    }

    [Test]
    public void 랜턴_반경과_소모는_LanternRule에만_적혀_있다()
    {
        // 프리팹에 사본이 있으면 상수를 돌려도 게임이 안 바뀐다.
        // 화톳불에서 실제로 겪은 일이라(CampfireFuelRule) 같은 실수를 막아 둔다.
        string path = Path.Combine(스크립트뿌리, "World", "LanternController.cs");
        string text = File.ReadAllText(path);

        foreach (var 금지 in new[] { "float maxBattery", "float drainPerSecond",
                                     "float fullRange", "float fullIntensity",
                                     "float batteryPerCell", "float flickerThreshold" })
            StringAssert.DoesNotContain(금지, text,
                                        "직렬화 필드로 두면 프리팹의 사본이 상수를 덮는다");
    }

    // ── ⑥ 있는 조작을 안내한다 ──────────────────────────────

    [Test]
    public void 조작_안내가_F로_켜고_끄라고_말한다()
    {
        Loc.Load(LocalizationTestBootstrap.LoadCatalogFromDisk());
        Loc.SetLocale(StringCatalog.DefaultLocale);

        string 안내 = Loc.T("UI", "hint_lantern");
        Assert.IsNotEmpty(안내, "안내 문구는 여전히 있어야 한다");
        StringAssert.Contains("[F]", 안내, "누를 키를 말하지 않으면 배울 길이 없다");
        StringAssert.Contains("끄기", 안내, "끌 수 있다는 것이 이 게임의 선택이다");
    }
}
