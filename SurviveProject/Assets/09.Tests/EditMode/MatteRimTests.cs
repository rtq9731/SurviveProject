using System.Linq;
using NUnit.Framework;
using Survive.Domain.Art;
using Survive.World;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ⑤ 무광버섯의 윤곽이 읽혀야 한다 — 림라이트가 <b>테두리에만</b> 머무는지 지킨다.
///
/// <b>이 게이트가 막는 것 둘.</b>
/// <list type="number">
/// <item><b>면을 밝히는 쪽으로 흘러가는 것.</b> 값 하나를 올리면 금세 "어두운 버섯"이
///       "보라색으로 빛나는 버섯"이 된다. 정면에 얹히는 양이 0이라는 것과 45도에서
///       이미 죽어 있다는 것을 숫자로 붙들어 둔다</item>
/// <item><b>주변을 밝히기 시작하는 것.</b> 무광버섯이 밝은 구역이 되면 플레이어가
///       버섯 옆에 서서 밤을 넘긴다. Light 컴포넌트가 하나도 없다는 것이 그 보증이고,
///       그 사실 자체를 단언한다</item>
/// </list>
/// </summary>
public class MatteRimTests
{
    const string 머티리얼경로 = "Assets/05.Prefabs/Harvesting/Node_MatteMushroom_Mat.mat";
    const string 프리팹경로 = "Assets/05.Prefabs/Harvesting/Node_MatteMushroom.prefab";
    const string 셰이더이름 = "Survive/MatteRim";

    static Material 머티리얼() => AssetDatabase.LoadAssetAtPath<Material>(머티리얼경로);
    static GameObject 프리팹() => AssetDatabase.LoadAssetAtPath<GameObject>(프리팹경로);

    // ── 규칙: 테두리에만 머무는가 ─────────────────────────────

    [Test]
    public void 정면에는_아무것도_얹히지_않는다()
    {
        // 이것이 "면을 밝히는 것이 아니다"의 정의다.
        Assert.AreEqual(0f, MatteRimRule.FaceAmount(), 1e-6f);
    }

    [Test]
    public void 실루엣_끝에서는_빛이_스친다()
    {
        Assert.Greater(MatteRimRule.EdgeAmount(), 0f, "테두리가 아예 없으면 고친 것이 없다");
        Assert.AreEqual(MatteRimRule.RimStrength, MatteRimRule.EdgeAmount(), 1e-6f);
    }

    [Test]
    public void 사십오도에서_이미_최대치의_백분의_일_아래다()
    {
        float 사십오도 = MatteRimRule.RimAmount(Mathf.Cos(45f * Mathf.Deg2Rad));
        Assert.LessOrEqual(사십오도, MatteRimRule.EdgeAmount() * MatteRimRule.FalloffRatioAt45,
                           "45도에서 아직 살아 있으면 화면에서는 면이 물든 것으로 보인다");
        Assert.IsTrue(MatteRimRule.IsSilhouetteOnly());
    }

    [Test]
    public void 각도가_커질수록_단조롭게_커진다()
    {
        // 중간에 꺾이면 테두리가 두 줄로 보인다.
        float 앞 = -1f;
        for (int i = 0; i <= 20; i++)
        {
            float ndotv = 1f - i / 20f;
            float v = MatteRimRule.RimAmount(ndotv);
            Assert.GreaterOrEqual(v, 앞, $"ndotv={ndotv}에서 되돌아갔다");
            앞 = v;
        }
    }

    // ── 색: 새 색을 만들지 않았는가 ─────────────────────────────

    [Test]
    public void 림_색은_광원_네_색_안에_있다()
    {
        Assert.IsTrue(EmissionPaletteMatch.IsAllowed(MatteRimRule.RimColor,
                                                     MaterialRule.EmissionChannelTolerance),
                      "팔레트 밖 색이다 — 아트 규칙이 무너진다");
        Assert.AreEqual(ArtPalette.Macronium, MatteRimRule.RimColor);
    }

    [Test]
    public void 셰이더가_허용_목록_안에_있다()
    {
        Assert.IsTrue(MaterialRule.IsAllowedShader(셰이더이름));
    }

    // ── 머티리얼: 규칙과 어긋나지 않는가 ─────────────────────────────

    [Test]
    public void 머티리얼이_규칙과_같은_값을_들고_있다()
    {
        var m = 머티리얼();
        Assert.IsNotNull(m, 머티리얼경로 + " 를 찾지 못했다");
        Assert.AreEqual(셰이더이름, m.shader.name);

        Assert.AreEqual(MatteRimRule.RimPower, m.GetFloat("_RimPower"), 1e-4f);
        Assert.AreEqual(MatteRimRule.RimStrength, m.GetFloat("_RimStrength"), 1e-4f);

        var c = m.GetColor("_RimColor");
        var want = MatteRimRule.RimColor;
        Assert.AreEqual(want.r, c.r, 0.01f, "림 색 R이 규칙과 다르다");
        Assert.AreEqual(want.g, c.g, 0.01f, "림 색 G가 규칙과 다르다");
        Assert.AreEqual(want.b, c.b, 0.01f, "림 색 B가 규칙과 다르다");
    }

    [Test]
    public void 머티리얼은_여전히_무광_밴드다()
    {
        var m = 머티리얼();
        Assert.IsTrue(MaterialRule.IsBandedSmoothness(m.GetFloat("_Smoothness")));
        Assert.AreEqual(MaterialRule.SmoothnessMatte, m.GetFloat("_Smoothness"),
                        MaterialRule.SmoothnessTolerance);
    }

    [Test]
    public void 머티리얼은_에미션을_켜지_않는다()
    {
        // 켜면 "받은 빛을 튕긴다"가 아니라 "스스로 낸다"가 되어 어둠 규칙이 뒤집힌다.
        var m = 머티리얼();
        Assert.IsFalse(m.IsKeywordEnabled("_EMISSION"));
        Assert.AreEqual(MaterialGlobalIlluminationFlags.EmissiveIsBlack, m.globalIlluminationFlags);
    }

    [Test]
    public void 면의_바탕색은_그대로_어둡다()
    {
        // 테두리를 얻자고 면을 밝히면 고치려던 것이 반대로 무너진다.
        var c = 머티리얼().GetColor("_BaseColor");
        float 밝기 = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
        Assert.Less(밝기, 0.15f, $"바탕이 너무 밝다: {밝기:0.###}");
    }

    // ── 어둠을 지키는가 ─────────────────────────────

    [Test]
    public void 무광버섯에는_광원이_하나도_없다()
    {
        // FixedLightZoneService는 Light를 훑어 밝은 구역을 세운다.
        // Light가 없다는 것이 곧 "그 그물에 걸리지 않는다"의 근거다.
        var p = 프리팹();
        Assert.IsNotNull(p, 프리팹경로 + " 를 찾지 못했다");
        Assert.IsEmpty(p.GetComponentsInChildren<Light>(true),
                       "무광버섯에 광원이 붙었다 — 밝은 구역 판정에 걸린다");
    }

    [Test]
    public void 무광버섯은_밝은_구역을_스스로_내지_않는다()
    {
        var p = 프리팹();
        Assert.IsFalse(p.GetComponentsInChildren<Component>(true).Any(c => c is ILitZoneSource),
                       "밝은 구역 소스가 붙었다");

        LitZoneRegistry.Clear();
        var 인스턴스 = Object.Instantiate(p);
        try
        {
            var 자리 = 인스턴스.transform.position;
            Assert.IsFalse(LitZoneRegistry.IsLit(자리),
                           "무광버섯 자리가 밝다고 답했다 — 버섯 옆이 안전지대가 된다");
            Assert.IsFalse(LitZoneRegistry.IsLit(자리 + Vector3.right * 0.5f));
        }
        finally
        {
            Object.DestroyImmediate(인스턴스);
            LitZoneRegistry.Clear();
        }
    }

    [Test]
    public void 광원이_아니므로_고정_광원_규칙의_대상이_아니다()
    {
        // 만약 누군가 나중에 Light를 붙이더라도, 그 세기로는 구역이 될 수 없어야 한다는
        // 선을 여기 남겨 둔다. 8m가 구역의 하한이다(FixedLightRule.MinLitRadius).
        Assert.IsFalse(FixedLightRule.IsZoneWorthy(intensity: 0.6f, range: 2.2f),
                       "장식 밝기가 구역으로 승격되면 이 게이트의 전제가 무너진다");
    }
}
