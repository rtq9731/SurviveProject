using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Survive.Vitals;
using Survive.World;

/// <summary>
/// 수분과 식량의 규칙 (기획서 §5.15 · 스펙 §6).
///
/// <b>이 검사들이 보통과 다르다.</b> 대개는 「작동한다」를 단언하는데 여기서는
/// <b>「눈에 띄지 않는다」</b>를 단언해야 한다. 이전 기획이 이 항목을 명시적으로
/// 기각했고 그 근거(*"시계가 둘이면 하나는 반드시 잡무가 된다"*)가 지금도 옳기
/// 때문이다. 되살아난 이유는 역할이 다르기 때문이고, 감소 속도를 올려 게이지를
/// 쳐다보게 만드는 순간 기각 근거가 그대로 되살아난다.
///
/// 그래서 아래 검사의 절반은 <b>상한</b>을 잰다 — 이만큼보다 더 줄면 안 된다.
/// </summary>
public class SustenanceTests
{
    const string HydrationAssetPath = "Assets/08.Data/Vitals/Resources/Hydration.asset";
    const string FoodAssetPath = "Assets/08.Data/Vitals/Resources/Food.asset";

    // ── ① 종류가 둘뿐이고 기본값이 없다 ──────────────────────

    [Test]
    public void 종류는_둘뿐이고_0에는_이름이_없다()
    {
        Assert.AreEqual(2, Sustenance.All.Length, "수분과 식량 둘뿐이다");
        Assert.IsFalse(Sustenance.IsKnown((SustenanceKind)0),
                       "0은 이름 없는 자리여야 한다. 여기에 이름이 붙으면 그것이 조용한 기본값이 된다");
        Assert.IsFalse(Sustenance.IsKnown((SustenanceKind)99));

        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => Sustenance.DrainPerSecond((SustenanceKind)0),
            "모르는 게이지를 물으면 던진다");
    }

    // ── ② 원정 한 번에 얼마나 주는가 — 이 항목의 게이트 ──────

    /// <summary>
    /// <b>원정 한 번에 경고가 뜨면 안 된다.</b> 「신경 쓰지 않아도 되는 정도」를
    /// 수로 옮긴 것이 이 한 줄이다.
    /// </summary>
    [Test]
    public void 원정_한_번으로는_경고선에_닿지_않는다()
    {
        foreach (var kind in Sustenance.All)
        {
            float lost = Sustenance.PercentPerExpedition(kind) / 100f;
            float left = 1f - lost;

            Assert.IsFalse(Sustenance.IsWarning(left),
                $"{kind}: 가득에서 원정 한 번(62초)을 돌면 {left:P0} 남는데 " +
                "그것이 경고선 아래다. 이 선이 무너지면 이것은 두 번째 시계다");
        }
    }

    [Test]
    public void 원정_한_번의_소모가_배터리_한_셀보다_한참_작다()
    {
        // 배터리는 가득 채워도 62.5초다 — 원정 한 번이 배터리 한 셀이고
        // 그것이 이 게임의 시계다. 수분·식량이 그것과 자릿수가 같아지면
        // 시계가 둘이 되고, 둘이 되면 하나는 잡무가 된다.
        float batterySeconds = LanternRule.MaxBattery / LanternRule.Tier1DrainPerSecond;

        foreach (var kind in Sustenance.All)
        {
            float ratio = Sustenance.SecondsFromFullToEmpty(kind) / batterySeconds;
            Assert.GreaterOrEqual(ratio, 4f,
                $"{kind}: 가득에서 바닥까지가 배터리 한 셀의 {ratio:F1}배뿐이다. " +
                "네 배 아래로 좁히면 배터리와 같은 자릿수의 시계가 하나 더 생긴다");
        }
    }

    [Test]
    public void 수분이_식량보다_빨리_준다()
    {
        // 꾸밈이 아니라 구조다. 수분은 들판에서도 채울 자리가 있고(정제) 식량은
        // 호수에만 있다 - 들판에 답이 없는 쪽을 자주 물으면 그것이 곧
        // 「먹으러 돌아간다」가 된다.
        Assert.Greater(Sustenance.DrainPerSecond(SustenanceKind.Hydration),
                       Sustenance.DrainPerSecond(SustenanceKind.Food));

        Assert.AreEqual(0f, Sustenance.FoodPerSecondFrom(LiquidKind.Macronium), 1e-4f,
                        "식량은 바다에서 한 점도 나오지 않는다 — 이 0이 「돌아갈 이유」다");
    }

    /// <summary>
    /// <b>「두세 번 나갔다 와야 채운다」가 수로 성립하는가.</b>
    ///
    /// 한 사이클은 원정 한 번 + 거점 체류다. 거점 체류는 잴 수 없으므로 두 모형을
    /// 다 잰다 — 쉬지 않고 나가는 쪽(0)과 원정만큼 쉬는 쪽.
    /// </summary>
    [Test]
    public void 두세_번_나갔다_와야_채운다()
    {
        float 쉬지않고 = Sustenance.ExpeditionsUntilWarning(SustenanceKind.Hydration, 0f);
        float 쉬면서 = Sustenance.ExpeditionsUntilWarning(
            SustenanceKind.Hydration, Sustenance.TypicalBaseCampSeconds);

        Debug.Log($"[수분] 경고까지 원정 {쉬면서:F1}번(거점 체류 포함) / " +
                  $"{쉬지않고:F1}번(쉬지 않고 나갈 때)");

        Assert.GreaterOrEqual(쉬면서, 2f,
            $"거점 체류를 넣고도 원정 {쉬면서:F1}번 만에 경고가 뜬다. 너무 빠르다");
        Assert.Less(쉬면서, 4f,
            $"거점 체류를 넣어도 원정 {쉬면서:F1}번을 가야 경고가 뜬다. " +
            "그러면 이 게이지는 아무 일도 하지 않는다");
    }

    /// <summary>
    /// 실측 보고가 읽는 표. <b>검사가 아니라 기록</b>이므로 값을 못 박지 않고 찍는다 —
    /// 튜닝값이 움직일 때마다 이 검사를 고쳐야 한다면 아무도 튜닝하지 않는다.
    /// </summary>
    [Test]
    public void 정찰_원정_하루의_소모를_적어_둔다()
    {
        foreach (var kind in Sustenance.All)
        {
            Debug.Log($"[{kind}] 정찰(11.2초) {Sustenance.PercentPerScout(kind):F2}% · " +
                      $"본 원정(62초) {Sustenance.PercentPerExpedition(kind):F1}% · " +
                      $"하루(1200초) {Sustenance.PercentPerDay(kind):F0}% · " +
                      $"가득→경고 {Sustenance.SecondsFromFullToWarning(kind):F0}초 · " +
                      $"가득→바닥 {Sustenance.SecondsFromFullToEmpty(kind):F0}초");
        }

        Assert.Greater(Sustenance.PercentPerDay(SustenanceKind.Hydration), 100f,
                       "하루에 한 번도 안 채워도 되면 그것은 없는 것과 같다");
    }

    // ── ③ 경계값 ─────────────────────────────────────────────

    [Test]
    public void 경고선의_경계값()
    {
        Assert.IsTrue(Sustenance.IsWarning(Sustenance.WarningThreshold),
                      "경계는 경고 쪽에 붙는다");
        Assert.IsFalse(Sustenance.IsWarning(Sustenance.WarningThreshold + 0.001f));
        Assert.IsTrue(Sustenance.IsWarning(0f));
        Assert.IsFalse(Sustenance.IsWarning(1f));
    }

    [Test]
    public void 경고와_벌_사이에_돌아갈_시간이_있다()
    {
        // 예고 없이 벌어지는 일은 긴장이 아니라 억울함이 된다. 경고가 뜨고 나서
        // 느려지기까지 적어도 원정 한 번만큼은 있어야, 보고 나서 돌아갈 수 있다.
        foreach (var kind in Sustenance.All)
        {
            float 여유 = Sustenance.SecondsBetween(kind, Sustenance.WarningThreshold,
                                                  Sustenance.SlowdownThreshold);
            Assert.GreaterOrEqual(여유, Sustenance.ExpeditionSeconds,
                $"{kind}: 경고에서 벌까지 {여유:F0}초뿐이다. 원정 한 번보다 짧으면 " +
                "밖에서 경고를 본 사람은 이미 늦은 것이다");
        }
    }

    [Test]
    public void 시간_계산은_되감아도_음수를_주지_않는다()
    {
        Assert.AreEqual(0f, Sustenance.SecondsBetween(SustenanceKind.Food, 0.2f, 0.8f), 1e-4f,
                        "올라가는 쪽을 물으면 0이다");
        Assert.AreEqual(0f, Sustenance.LossOver(SustenanceKind.Food, -10f), 1e-4f);
    }

    // ── ④ 바닥을 쳐도 죽지 않는다 ────────────────────────────

    /// <summary>
    /// <b>환경은 죽이지 않고 생물만 죽인다</b>(기획서 §5.9). 굶어 죽게 만들면
    /// 이 게임에서 환경이 사람을 죽이는 두 번째 자리가 생기고, 그것은 낫이
    /// 하는 일을 게이지가 대신하는 것이다.
    /// </summary>
    [Test]
    public void 바닥을_쳐도_죽지_않고_느려지기만_한다()
    {
        Assert.AreEqual(Sustenance.SlowestMoveFactor, Sustenance.MoveFactor(0f), 1e-4f);
        Assert.Greater(Sustenance.MoveFactor(0f), 0f,
                       "0이면 느려지는 것이 아니라 갇히는 것이고, 갇히면 결국 죽는다");

        // 둘이 동시에 바닥이어도 벌은 하나다. 곱하면 0.49가 되어 사실상 갇힌다.
        Assert.AreEqual(Sustenance.SlowestMoveFactor,
                        Sustenance.WorstMoveFactor(0f, 0f), 1e-4f);
        Assert.AreEqual(Sustenance.SlowestMoveFactor,
                        Sustenance.WorstMoveFactor(0f, 1f), 1e-4f);
    }

    [Test]
    public void 경고선_위에서는_아무_벌도_없다()
    {
        Assert.AreEqual(1f, Sustenance.MoveFactor(1f), 1e-4f);
        Assert.AreEqual(1f, Sustenance.MoveFactor(Sustenance.WarningThreshold), 1e-4f,
                        "경고는 알림이지 벌이 아니다");
        Assert.AreEqual(1f, Sustenance.MoveFactor(Sustenance.SlowdownThreshold), 1e-4f);
        Assert.Less(Sustenance.MoveFactor(Sustenance.SlowdownThreshold - 0.01f), 1f);
    }

    [Test]
    public void 벌은_절벽이_아니라_비탈이다()
    {
        float prev = 1f;
        for (float t = Sustenance.SlowdownThreshold; t >= 0f; t -= 0.005f)
        {
            float now = Sustenance.MoveFactor(t);
            Assert.LessOrEqual(now, prev + 1e-4f, $"{t:F3}에서 배율이 도로 올라갔다");
            Assert.LessOrEqual(prev - now, 0.05f, $"{t:F3}에서 배율이 한 번에 뚝 떨어진다");
            prev = now;
        }
    }

    /// <summary>
    /// <b>구조로 못 박는다.</b> 위의 검사들은 규칙이 무엇을 <i>답하는가</i>를 재지만,
    /// 실제로 죽이지 않는다는 것은 <b>체력에 손을 대지 않는다</b>는 사실이다.
    /// 그것은 값이 아니라 코드의 모양이므로 코드를 읽어 확인한다.
    /// </summary>
    [Test]
    public void 수분_식량은_체력에_손을_대지_않는다()
    {
        string path = Path.Combine(Application.dataPath, "02.Scripts/Vitals/SustenanceService.cs");
        Assert.IsTrue(File.Exists(path), "SustenanceService를 찾지 못했다 — 검사가 비어 돈다");

        string src = File.ReadAllText(path);
        Assert.IsFalse(src.Contains(".Health"),
            "SustenanceService가 체력을 만진다. 굶어 죽는 순간 위협 계층(기획서 §5.9)이 " +
            "무너진다 — 환경은 막거나 값을 매기고, 목숨은 움직이는 것이 가져간다");

        string rule = Path.Combine(Application.dataPath, "02.Scripts/Domain/Vitals/Sustenance.cs");
        Assert.IsTrue(File.Exists(rule));
        Assert.IsFalse(File.ReadAllText(rule).Contains("DamagePerSecond"),
                       "규칙에도 피해를 매기는 값이 없어야 한다");
    }

    // ── ⑤ 채우는 곳 ──────────────────────────────────────────

    [Test]
    public void 호수에서는_물도_먹을것도_나온다()
    {
        Assert.Greater(Sustenance.HydrationPerSecondFrom(LiquidKind.Water, false), 0f);
        Assert.Greater(Sustenance.FoodPerSecondFrom(LiquidKind.Water), 0f);

        // 마실 수 있는 액체에서만 물이 나온다는 것이 두 규칙에서 같은 답으로 나와야 한다.
        Assert.IsTrue(Liquid.IsPotable(LiquidKind.Water));
    }

    [Test]
    public void 매크로늄은_정제_없이는_한_방울도_주지_않는다()
    {
        Assert.AreEqual(0f, Sustenance.HydrationPerSecondFrom(LiquidKind.Macronium, false), 1e-4f,
            "70%가 물이지만 음용은 불가능하다(세계관 §3). 여기서 뒤집으면 " +
            "Liquid.IsPotable과 서로 다른 말을 하게 된다");
        Assert.IsFalse(Liquid.IsPotable(LiquidKind.Macronium));
    }

    [Test]
    public void 정제하면_열리지만_호수보다_한참_느리다()
    {
        float 정제 = Sustenance.HydrationPerSecondFrom(LiquidKind.Macronium, true);
        float 호수 = Sustenance.HydrationPerSecondFrom(LiquidKind.Water, false);

        Assert.Greater(정제, 0f, "정제 수단이 있으면 매크로늄 표면에서도 물이 나온다 (§5.15)");
        Assert.Less(정제 * 2f, 호수, "호수의 절반보다도 빨라지면 굳이 돌아갈 이유가 없어진다");

        // 그동안 몸은 부식된다. 가득 채우는 값이 체력으로 얼마인지 적어 둔다 -
        // 이것이 「응급 수단이지 살림이 아니다」의 실체다.
        float 초 = Sustenance.MaxValue / 정제;
        Debug.Log($"[정제] 매크로늄 표면에서 가득 채우려면 {초:F0}초, " +
                  $"그동안 부식 {초 * MacroniumSea.CorrosionPerSecond:F0}");
        Assert.Greater(초 * MacroniumSea.CorrosionPerSecond, 30f,
                       "공짜에 가까우면 바다 위에서도 살 수 있게 된다");
    }

    [Test]
    public void 정제해도_먹을것은_나오지_않는다()
    {
        // 물은 걸러 낼 수 있지만 없는 생물을 걸러 낼 수는 없다 (세계관 §5).
        Assert.AreEqual(0f, Sustenance.FoodPerSecondFrom(LiquidKind.Macronium), 1e-4f);
    }

    [Test]
    public void 모르는_액체를_물으면_던진다()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => Sustenance.HydrationPerSecondFrom((LiquidKind)0, false));
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => Sustenance.FoodPerSecondFrom((LiquidKind)0));
    }

    // ── ⑥ 에셋과 규칙이 두 말을 하지 않는다 ──────────────────

    /// <summary>
    /// <b>감소 속도의 사본을 에셋에 두지 않는다.</b> 화톳불 연료에서 실제로 겪은
    /// 일이다 — 에셋에 사본이 있으면 상수를 돌려도 게임이 안 바뀐다
    /// (<see cref="Survive.Building.CampfireFuelRule"/>). 그래서 에셋은 눈금의
    /// 크기만 들고, 초당 몇인가는 <see cref="Sustenance"/> 하나가 정한다.
    /// </summary>
    [Test]
    public void 수분과_식량의_에셋이_규칙과_같은_말을_한다()
    {
        검사한다(HydrationAssetPath, Sustenance.HydrationId);
        검사한다(FoodAssetPath, Sustenance.FoodId);
    }

    static void 검사한다(string path, string id)
    {
        var def = AssetDatabase.LoadAssetAtPath<VitalDefinitionSO>(path);
        Assert.IsNotNull(def, $"{path}가 없다");
        Assert.AreEqual(id, def.id, $"{path}의 id가 규칙과 다르다");
        Assert.AreEqual(Sustenance.MaxValue, def.maxValue, 1e-4f);
        Assert.AreEqual(Sustenance.MaxValue, def.startValue, 1e-4f,
                        "시작은 가득이다. 호수 옆에 착륙했으므로 초반에는 문제가 되지 않는다");
        Assert.AreEqual(0f, def.passiveRatePerSecond, 1e-4f,
                        "에셋에 감소 속도의 사본을 두지 마라. 규칙은 Sustenance 하나다");
        Assert.IsNotEmpty(def.displayName);

        // Resources 아래에 있어야 프리팹을 열지 않고도 읽힌다.
        Assert.IsTrue(path.Contains("/Resources/"),
                      $"{path}가 Resources 밖에 있다 — PlayerVitals가 못 읽는다");
    }
}
