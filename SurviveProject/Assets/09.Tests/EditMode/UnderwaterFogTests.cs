using System.IO;
using NUnit.Framework;
using UnityEngine;
using Survive.Domain.Art;
using Survive.World;

/// <summary>
/// 검토회신 2026-08-07 ④ — <b>랜턴이 수중 포그를 이기는 반경을 확보한다.</b>
///
/// 사용자 판정은 "전역 수중 포그는 그대로 두되 랜턴 광원이 포그를 이기는
/// 반경을 확보한다. 반경 밖은 여전히 새까맣고 반경 안만 보이면 어둠 축은
/// 유지된다"였다. 그 문장을 값으로 옮기면 셋이다.
///
/// 1. <b>반경 안은 보인다</b> — 랜턴이 닿는 거리에서 빛이 살아남는다.
/// 2. <b>반경 밖은 새까맣다</b> — 반경의 두 배쯤에서 세계가 닫힌다.
/// 3. <b>물속이 지상보다 밝아지지 않는다</b> — 밝아지면 어둠 축이 뒤집힌다.
///
/// <b>수치는 여기 베껴 적지 않는다.</b> 아래 단언은 전부
/// <see cref="UnderwaterFog"/>·<see cref="LanternRule"/>의 상수를 참조해
/// 관계만 본다 — 손잡이를 돌릴 때마다 검사가 거짓으로 깨지면 손잡이가
/// 손잡이가 아니게 된다.
/// </summary>
public class UnderwaterFogTests
{
    // ── ① 반경 안은 보인다 ──────────────────────────────────

    [Test]
    public void 랜턴이_닿는_거리에서_빛이_살아남는다()
    {
        // 반경 자체(정면 도달 거리가 아니라)까지는 확실히 읽혀야 한다.
        float 반경 = LanternRule.Tier1Radius;
        Assert.Greater(UnderwaterFog.Transmittance(반경), UnderwaterFog.VisibleTransmittanceFloor,
                       "반경 안이 안 보이면 물속에서 이동 자체가 성립하지 않는다");
    }

    [Test]
    public void 코앞은_거의_손해_없이_보인다()
    {
        Assert.Greater(UnderwaterFog.Transmittance(2f), 0.9f,
                       "발밑까지 뿌예지면 안개가 아니라 눈가리개다");
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void 물속_유효_반경은_0보다_크다(int 티어)
    {
        Assert.Greater(UnderwaterFog.LanternRangeUnderwater(티어), 0f);
    }

    [Test]
    public void 랜턴이_없으면_물속_유효_반경도_0이다()
    {
        Assert.AreEqual(0f, UnderwaterFog.LanternRangeUnderwater(0), 0.0001f);
    }

    // ── ② 반경 밖은 새까맣다 ────────────────────────────────

    [Test]
    public void 세계는_랜턴_도달_거리의_두_배쯤에서_닫힌다()
    {
        float 도달 = LanternRule.ForwardReachForTier(UnderwaterFog.BaselineTier);
        Assert.AreEqual(도달 * UnderwaterFog.CloseAtReachMultiple,
                        UnderwaterFog.CloseDistance, 0.0001f);

        // FullDistance는 "1%가 남는 거리"다. 닫히는 거리에서 실제로 그래야
        // 밀도 역산이 맞은 것이다.
        Assert.AreEqual(UnderwaterFog.CloseDistance,
                        DepthFog.FullDistance(UnderwaterFog.Density), 0.01f);
    }

    [Test]
    public void 닫히는_거리_너머는_사실상_아무것도_안_온다()
    {
        Assert.Less(UnderwaterFog.Transmittance(UnderwaterFog.CloseDistance), 0.02f,
                    "여기서 아직 보이면 반경 밖이 새까만 것이 아니다");
        Assert.Less(UnderwaterFog.Transmittance(UnderwaterFog.CloseDistance * 1.5f), 0.001f);
    }

    /// <summary>
    /// 고치기 전에는 밀도 0.028이라 <b>77m까지</b> 보였다. 랜턴 반경이 8m인데
    /// 77m가 보이면 반경 안과 밖을 가르는 것이 없고, 그러면 켜고 끄는 것이
    /// 화면에서 아무 차이도 만들지 않는다.
    /// </summary>
    [Test]
    public void 옛_밀도보다_확실히_짙다()
    {
        const float 옛밀도 = 0.028f;
        Assert.Greater(UnderwaterFog.Density, 옛밀도 * 2f,
                       "세계가 안 닫히면 반경 밖이 평평한 벽지가 된다");
    }

    // ── ③ 물속이 지상보다 밝아지지 않는다 ───────────────────

    [Test]
    public void 물속_먼_곳은_수면_위_밴드보다_어둡다()
    {
        float 물속 = UnderwaterFog.Luminance(UnderwaterFog.Color);
        Assert.Less(물속, UnderwaterFog.SurfaceLuminance,
                    "깊이 들어갈수록 밝아지면 어둠 축이 뒤집힌다");
        Assert.AreEqual(UnderwaterFog.SurfaceLuminance * UnderwaterFog.DarkerThanSurface,
                        물속, 0.0005f);
        Assert.Less(UnderwaterFog.DarkerThanSurface, 1f, "손잡이 자체가 1을 넘으면 안 된다");
    }

    /// <summary>
    /// 팔레트의 자홍을 그대로 쓰되 휘도만 내린다. 색조가 바뀌면 물속이
    /// 다른 세계로 보이고, 새 hex를 적으면 아트 규칙 검사기가 보는 팔레트와
    /// 화면이 갈라진다.
    /// </summary>
    [Test]
    public void 색조는_팔레트의_절벽_자홍_그대로다()
    {
        var 원색 = ArtPalette.FogCliffs;
        var 물속 = UnderwaterFog.Color;

        // 비율이 같으면 색조가 같다. 원색의 최대 성분으로 정규화해 견준다.
        float k = 물속.b / 원색.b;
        Assert.AreEqual(원색.r * k, 물속.r, 0.001f);
        Assert.AreEqual(원색.g * k, 물속.g, 0.001f);
        Assert.Less(k, 1f, "휘도는 내려가야 한다");
    }

    [Test]
    public void 휘도_맞추기는_검정을_검정으로_둔다()
    {
        var 검정 = UnderwaterFog.WithLuminance(Color.black, 0.5f);
        Assert.AreEqual(0f, UnderwaterFog.Luminance(검정), 0.0001f,
                        "없는 색을 밝힐 수는 없다");
    }

    // ── ④ 물속이 지상보다 좁다 ──────────────────────────────

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void 같은_랜턴이_물속에서는_더_짧게_닿는다(int 티어)
    {
        Assert.Less(UnderwaterFog.LanternRangeUnderwater(티어),
                    UnderwaterFog.LanternRangeOnLand(티어),
                    "물속에서 유효 반경이 더 짧은 편이 자연스럽다");
    }

    [Test]
    public void 티어를_올리면_물속에서도_더_멀리_보거나_최소한_줄지는_않는다()
    {
        for (int t = 1; t < LanternRule.MaxTier; t++)
            Assert.GreaterOrEqual(UnderwaterFog.LanternRangeUnderwater(t + 1),
                                  UnderwaterFog.LanternRangeUnderwater(t),
                                  "상위 티어가 물속에서 더 좁으면 살 이유가 없다");
    }

    // ── ⑤ 씬의 사본이 규칙을 덮지 못한다 ────────────────────

    /// <summary>
    /// 씬에 밀도 0.028과 자홍이 박혀 있어서 상수를 돌려도 게임이 안 바뀌던
    /// 것이 이 항목의 원인 절반이었다. 화톳불 연료·랜턴에 이어 세 번째다.
    /// </summary>
    [Test]
    public void 물속_안개_색과_밀도는_직렬화_필드가_아니다()
    {
        string path = Path.Combine(Application.dataPath, "02.Scripts", "Player", "UnderwaterView.cs");
        Assert.IsTrue(File.Exists(path), "UnderwaterView를 찾지 못했다: " + path);

        string text = File.ReadAllText(path);
        StringAssert.DoesNotContain("SerializeField] Color underwaterFog", text);
        StringAssert.DoesNotContain("SerializeField] float underwaterFogDensity", text);
        StringAssert.Contains("UnderwaterFog.Density", text);
        StringAssert.Contains("UnderwaterFog.Color", text);
    }

    // ── ⑥ 연출 상수는 손대지 않았다 ─────────────────────────

    /// <summary>
    /// 순서를 못 박은 항목이다 — <b>연출 값을 손대기 전에 렌더링부터
    /// 정리한다.</b> 지금 가장자리 값을 올리면 포그를 이기려고 과하게 밝혀
    /// 놓게 되고, 나중에 포그를 고쳤을 때 두 번 맞춰야 한다.
    /// </summary>
    [Test]
    public void 가장자리_연출_값은_그대로다()
    {
        Assert.AreEqual(0.55f, DiveRule.EdgeOnsetRatio, 0.0001f);
        Assert.AreEqual(0.72f, DiveRule.EdgeMaxDarkness, 0.0001f);
    }
}
