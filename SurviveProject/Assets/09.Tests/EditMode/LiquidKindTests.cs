using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using Survive.World;

/// <summary>
/// 액체에 <b>종류</b> 축이 섰다 (스펙 §3, 사용자 판단 2026-08-07).
///
/// 씬의 유일한 액체가 호수와 매크로늄 바다를 겸하고 있었고, 그래서
/// <b>마실 수 있는 물에 들어가면 살이 깎였다.</b> 새 설계는 둘을 정반대의 것으로
/// 갈랐다 - 진짜 물만 내려 고인 호수는 무해하고 마실 수 있으며(세계관 §2),
/// 70%가 물인 매크로늄 바다는 마실 수 없고 담그면 부식된다(세계관 §3).
///
/// <b>이 파일이 못 박는 것은 「갈래가 아니라 값」이다.</b> 특례를 둘 만들었다면
/// 판정 안에 "호수라면"이 적혀 있을 것이다. 실제로 그렇지 않은지를,
/// 같은 함수에 종류만 갈아 넣어 답이 갈리는 것으로 보인다.
/// </summary>
public class LiquidKindTests
{
    static List<GearCapability> 맨몸 => new List<GearCapability>();

    /// <summary>깊고 넓은 한 자락. 종류만 갈아 끼우려고 수를 고정해 둔다.</summary>
    static LiquidBody 깊고넓은(LiquidKind kind) => new LiquidBody(kind, 6f, 60f);

    // ── (1) 같은 판정이 두 답을 낸다 ────────────────────────────

    /// <summary>
    /// <b>같은 자리에서 종류만 갈면 답이 갈린다.</b> 깊이도 폭도 그대로다.
    /// 이것이 「특례 둘이 아니라 한 규칙의 두 값」의 실제 모습이다.
    /// </summary>
    [Test]
    public void 같은_판정_함수가_호수에는_무해를_바다에는_부식을_낸다()
    {
        var 바다 = 깊고넓은(LiquidKind.Macronium);
        var 호수 = 바다.OfKind(LiquidKind.Water);

        Assert.AreEqual(CrossingVerdict.Costly, LiquidCrossing.Judge(바다, 맨몸, 100f),
            "매크로늄에 잠긴 채 건너면 값을 치러야 한다");
        Assert.AreEqual(CrossingVerdict.Harmless, LiquidCrossing.Judge(호수, 맨몸, 100f),
            "진짜 물에는 값이 없다");

        Assert.Greater(LiquidCrossing.Toll(바다), 0f);
        Assert.AreEqual(0f, LiquidCrossing.Toll(호수), 0.0001f);
    }

    /// <summary>
    /// <b>호수는 판정에서 빠지는 것이 아니라 다른 답을 내는 것이다.</b>
    /// 빠졌다면 폭을 백 배로 늘려도 답이 「모른다」였을 것이다. 여기서는
    /// 백 배로 늘려도 <b>규칙이 0을 계산해서</b> 돌려준다.
    /// </summary>
    [Test]
    public void 호수는_아무리_넓고_깊어도_규칙이_0을_계산해_돌려준다()
    {
        var 호수 = new LiquidBody(LiquidKind.Water, 40f, 6000f);

        Assert.AreEqual(SeaImmersion.Swimming, LiquidCrossing.ImmersionAt(호수.Depth),
            "호수에서도 몸은 뜬다 - 무해한 이유가 얕아서가 아니다");
        Assert.IsFalse(LiquidCrossing.HasFooting(호수.Depth),
            "발도 안 닿는다 - 무해한 이유가 딛고 서서가 아니다");
        Assert.IsTrue(LiquidCrossing.IsExposed(SeaImmersion.Swimming, false),
            "몸도 노출되어 있다 - 무해한 이유가 안 잠겨서가 아니다");

        Assert.AreEqual(0f, LiquidCrossing.Toll(호수), 0.0001f,
            "그런데도 0이다. 남은 이유는 종류 하나뿐이다");
        Assert.AreEqual(float.PositiveInfinity,
            LiquidCrossing.LethalWidth(LiquidKind.Water, 100f),
            "물에는 헤엄쳐 건너다 죽는 폭이 없다");
    }

    /// <summary>
    /// <b>판정 본문에 오브젝트 이름도 구역 이름도 없다.</b>
    ///
    /// 씬의 액체 이름으로 갈랐다면 규칙 안에 그 이름이 적혀 있을 것이고,
    /// 그러면 지형이 바뀔 때마다 규칙이 따라 움직여야 한다.
    /// </summary>
    [Test]
    public void 판정_본문에_씬_오브젝트_이름이_나오지_않는다()
    {
        string 본문 = 주석을_지운다(File.ReadAllText(Path.Combine(
            Application.dataPath, "02.Scripts/Domain/World/LiquidCrossing.cs")));

        foreach (var 이름 in new[] { "Water_Lake", "Macronium_Sea", "Lake", "GameObject", "Find" })
            StringAssert.DoesNotContain(이름, 본문,
                $"판정이 「{이름}」을 알고 있다 - 그 순간 규칙이 지형에 매인다");
    }

    // ── (2) 종류가 둘뿐이고 기본값이 없다 ───────────────────────

    [Test]
    public void 종류는_물과_매크로늄_둘뿐이다()
    {
        var 이름들 = Enum.GetNames(typeof(LiquidKind));
        CollectionAssert.AreEquivalent(new[] { "Water", "Macronium" }, 이름들,
            "종류가 늘거나 줄었다. 늘리는 것은 세계관을 늘리는 일이므로 여기서 한 번 걸린다");
        CollectionAssert.AreEquivalent(이름들, Liquid.All.Select(k => k.ToString()).ToArray(),
            "Liquid.All이 열거형과 어긋난다 - 하나를 더하고 다른 하나를 잊었다");
    }

    /// <summary>
    /// <b>0에 이름이 없다.</b> 있으면 그것이 조용한 기본값이 되고,
    /// 종류를 안 적은 액체가 판정까지 무사히 흘러간다.
    /// </summary>
    [Test]
    public void 기본값_자리인_0에_이름이_없다()
    {
        Assert.IsFalse(Enum.IsDefined(typeof(LiquidKind), 0),
            "LiquidKind에 0값이 정의되어 있다. 그것이 곧 「적지 않아도 되는」 종류가 된다");
        Assert.IsFalse(Liquid.IsKnown(default(LiquidKind)));
    }

    /// <summary>
    /// <b>모르면 실패한다.</b> 물도 매크로늄도 아닌 것을 물으면 답을 지어내지 않는다.
    /// </summary>
    [Test]
    public void 모르는_액체를_물으면_조용히_답하지_않고_던진다()
    {
        var 모르는것 = (LiquidKind)0;
        var 엉뚱한것 = (LiquidKind)99;

        Assert.Throws<ArgumentOutOfRangeException>(() => Liquid.CorrosionPerSecond(모르는것));
        Assert.Throws<ArgumentOutOfRangeException>(() => Liquid.CorrosionPerSecond(엉뚱한것));
        Assert.Throws<ArgumentOutOfRangeException>(() => Liquid.IsPotable(모르는것));
        Assert.Throws<ArgumentOutOfRangeException>(() => Liquid.Require(모르는것));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LiquidBody(모르는것, 2f, 20f));
        Assert.Throws<ArgumentOutOfRangeException>(() => LiquidCrossing.LethalWidth(모르는것, 100f));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => LiquidCrossing.DamagePerSecond(모르는것, SeaImmersion.Swimming, false));
    }

    /// <summary>
    /// <b>모른다고 무해로 떨어지지도 않는다.</b> 「던진다」의 반대말은 「0을 준다」이고,
    /// 그쪽이 훨씬 위험하다 - 씬이 틀렸는데 게임은 멀쩡해 보인다.
    /// </summary>
    [Test]
    public void 모르는_액체가_무해로_떨어지지_않는다()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => LiquidCrossing.DamageOver((LiquidKind)0, SeaImmersion.Swimming, false, 10f));
    }

    /// <summary>마실 수 있는 것은 하나뿐이다. 매크로늄은 70%가 물이어도 아니다.</summary>
    [Test]
    public void 마실_수_있는_액체는_물_하나뿐이다()
    {
        Assert.IsTrue(Liquid.IsPotable(LiquidKind.Water));
        Assert.IsFalse(Liquid.IsPotable(LiquidKind.Macronium));
    }

    // ── (3) 헤엄은 둘 다 된다 ───────────────────────────────────

    /// <summary>
    /// <b>갈리는 것은 부식뿐이다.</b> 같은 깊이·같은 폭이면 잠기는 정도도,
    /// 발이 닿는지도, 건너는 데 걸리는 시간도 종류와 무관하게 같다.
    /// </summary>
    [Test]
    public void 헤엄은_둘_다_되고_갈리는_것은_부식뿐이다()
    {
        foreach (float 깊이 in new[] { 0f, 0.2f, 0.35f, 0.9f, 1.15f, 3f, 20f })
        {
            var 물 = new LiquidBody(LiquidKind.Water, 깊이, 40f);
            var 매크로늄 = 물.OfKind(LiquidKind.Macronium);

            Assert.AreEqual(LiquidCrossing.ImmersionAt(매크로늄.Depth),
                            LiquidCrossing.ImmersionAt(물.Depth),
                            $"깊이 {깊이}m에서 잠기는 정도가 종류에 따라 갈렸다");
            Assert.AreEqual(LiquidCrossing.HasFooting(매크로늄.Depth),
                            LiquidCrossing.HasFooting(물.Depth),
                            $"깊이 {깊이}m에서 발 닿음이 종류에 따라 갈렸다");
            Assert.AreEqual(LiquidCrossing.CrossingSeconds(매크로늄),
                            LiquidCrossing.CrossingSeconds(물), 0.0001f,
                            $"깊이 {깊이}m에서 횡단 시간이 종류에 따라 갈렸다");
        }
    }

    /// <summary>
    /// 무해한 자리는 <b>종류와 상관없이</b> 무해하다. 발을 딛고 선 얕은 물이 그렇다.
    /// 종류가 답을 바꾸는 것은 「노출된 다음」이지 노출 여부가 아니다.
    /// </summary>
    [Test]
    public void 발을_딛고_선_얕은_자리는_종류와_상관없이_무해하다()
    {
        foreach (var kind in Liquid.All)
            Assert.AreEqual(0f,
                LiquidCrossing.DamagePerSecond(kind, SeaImmersion.Wading, footing: true), 0.0001f,
                $"{kind}: 물가에서 발목을 담근 채 캐는 일이 잡무가 되면 안 된다");
    }

    /// <summary>
    /// <b>산소는 종류를 보지 않는다.</b> 호수에서 잠수해도 숨은 똑같이 막힌다 -
    /// 숨은 액체의 성분이 아니라 머리가 잠겼는가의 문제다(기획서 §5.1).
    ///
    /// 잠수 규칙이 종류를 알기 시작하면 「호수에서는 숨을 쉴 수 있다」가 되고,
    /// 그러면 첫 잠수 통로 길이의 역산도 어느 액체인지에 따라 둘이 된다.
    /// </summary>
    [Test]
    public void 잠수_규칙에_종류_축이_없다()
    {
        string 본문 = 주석을_지운다(File.ReadAllText(Path.Combine(
            Application.dataPath, "02.Scripts/Domain/World/DiveRule.cs")));

        StringAssert.DoesNotContain("LiquidKind", 본문,
            "잠수가 액체의 종류를 보기 시작했다. 호수에서 숨을 쉬게 하려는 것이 아니라면 되돌려라");

        // 그리고 실제로 같은 값을 낸다. 위의 글자 검사가 우회되어도 여기서 걸린다.
        Assert.AreEqual(-DiveRule.BareDrainPerSecond,
                        DiveRule.OxygenDeltaPerSecond(submerged: true, suited: false), 0.0001f);
    }

    // ── (4) 씬의 액체는 전부 종류를 갖는다 ──────────────────────

    /// <summary><c>World/WaterBody.cs</c>의 스크립트 guid. 씬에서 그 컴포넌트를 집는 자다.</summary>
    const string WaterBodyGuid = "86ffcebaa6872f64487b18c4631f0175";

    /// <summary>
    /// <b>어느 씬에도 종류 없는 액체가 없다.</b> 비워 두면 그 덩어리는 등록되지 않아
    /// 헤엄조차 쳐지지 않는데, 그 사고를 재생 전에 잡는 자리가 여기다.
    ///
    /// <b>씬을 열지 않고 직렬화된 글을 읽는 이유.</b> 검사 어셈블리는 Domain만 보므로
    /// <c>WaterBody</c> 타입에 손이 닿지 않는다. 그리고 글을 읽는 편이 프리팹까지
    /// 한 번에 훑는다 - 씬을 여는 검사는 프리팹 안의 액체를 못 본다.
    /// </summary>
    [Test]
    public void 씬과_프리팹의_모든_액체가_종류를_갖는다()
    {
        var 뿌리 = Application.dataPath;
        var 파일들 = Directory.GetFiles(뿌리, "*.unity", SearchOption.AllDirectories)
                       .Concat(Directory.GetFiles(뿌리, "*.prefab", SearchOption.AllDirectories))
                       .ToArray();

        int 찾은액체 = 0;
        var 종류없는것 = new List<string>();

        foreach (var 파일 in 파일들)
        {
            var 줄들 = File.ReadAllLines(파일);
            for (int i = 0; i < 줄들.Length; i++)
            {
                if (줄들[i].IndexOf(WaterBodyGuid, StringComparison.Ordinal) < 0) continue;
                if (줄들[i].IndexOf("m_Script:", StringComparison.Ordinal) < 0) continue;

                찾은액체++;

                // 이 컴포넌트 덩어리 안에서 kind를 찾는다. 다음 문서(---)까지가 한 덩어리다.
                LiquidKind? 종류 = null;
                for (int j = i + 1; j < 줄들.Length && !줄들[j].StartsWith("---"); j++)
                {
                    var m = Regex.Match(줄들[j], @"^\s*kind:\s*(-?\d+)\s*$");
                    if (!m.Success) continue;
                    종류 = (LiquidKind)int.Parse(m.Groups[1].Value);
                    break;
                }

                if (종류 == null || !Liquid.IsKnown(종류.Value))
                    종류없는것.Add($"{파일.Substring(뿌리.Length + 1)}:{i + 1}  kind={종류?.ToString() ?? "없음"}");
            }
        }

        Assert.Greater(찾은액체, 0,
            "액체를 하나도 못 찾았다. 검사기가 비어 도는 것이므로 이것도 실패다 " +
            "(WaterBody의 guid가 바뀌었는가)");

        Assert.IsEmpty(종류없는것,
            "종류가 안 적힌 액체가 있다. 물인지 매크로늄인지 정해야 세계에 실린다:\n  " +
            string.Join("\n  ", 종류없는것));
    }

    // ── 도구 ────────────────────────────────────────────────────

    static string 주석을_지운다(string 원문) =>
        Regex.Replace(Regex.Replace(원문, @"/\*.*?\*/", "", RegexOptions.Singleline),
                      @"//.*?$", "", RegexOptions.Multiline);
}
