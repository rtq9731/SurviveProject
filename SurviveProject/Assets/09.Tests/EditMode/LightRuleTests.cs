using NUnit.Framework;
using UnityEngine;
using Survive.Domain.Art;

namespace Survive.Tests.EditMode
{
    /// <summary>
    /// 광원 4색 규칙은 원래 머티리얼의 Emission만 봤다. 그런데 지하를 실제로
    /// 밝히는 것은 Light 컴포넌트다 — MainScene의 발광 버섯 15개, 그로브 라이트
    /// 3개가 전부 팔레트 밖 청록으로 켜져 있어도 머티리얼 검사기는 0건을 냈다.
    /// 이 테스트는 그 구멍을 막는 LightRule을 고정한다.
    /// </summary>
    public class LightRuleTests
    {
        static LightFacts Facts(
            string assetPath = "Assets/dummy.unity",
            string name = "TestLight",
            Color? color = null,
            LightType type = LightType.Point,
            float intensity = 1f)
            => new LightFacts(assetPath, name, color ?? ArtPalette.Glowshroom, type, intensity);

        [Test]
        public void Palette_matching_point_light_has_no_violations()
        {
            Assert.IsEmpty(LightRule.Violations(Facts(color: ArtPalette.Flame)));
        }

        [Test]
        public void Off_palette_point_light_is_a_violation()
        {
            // MainScene의 실제 값: 청록 팔레트(#2FE6C8)를 의도했지만 실제로는
            // #8CF2BF로 켜져 있다 — 정규화한 r 채널이 허용 오차(0.04)의 9배 어긋난다.
            var offPalette = new Color(0.55f, 0.95f, 0.75f);
            var v = LightRule.Violations(Facts(color: offPalette, name: "Glow"));
            Assert.AreEqual(1, v.Count);
            StringAssert.Contains("Glow", v[0]);
        }

        [Test]
        public void Hdr_intensity_beyond_one_still_matches_by_hue()
        {
            // 라이트는 강도가 1을 훌쩍 넘는 게 정상(MainScene Glow는 2.2)이다.
            // 색으로만 판정해야 강도가 규칙을 어기지 않는다.
            var bright = ArtPalette.Macronium * 4f;
            Assert.IsTrue(LightRule.IsAllowedColor(bright));
        }

        [Test]
        public void Directional_light_is_exempt_from_the_color_check()
        {
            // Directional은 태양·전역 조명을 나타낸다. 광원 4색 규칙(§4)은
            // "지하에서 빛나는 것" — 랜드마크가 되는 개별 발광체를 가리키는
            // 규칙이지, 장면 전체를 비추는 방향광까지 포함하지 않는다.
            var offPalette = Color.grey;
            Assert.IsEmpty(LightRule.Violations(
                Facts(color: offPalette, type: LightType.Directional)));
        }

        [Test]
        public void Directional_exemption_does_not_cover_point_or_spot_lights()
        {
            // 면제는 타입 하나에만 좁게 걸려야 한다. 프롤로그(StartScene)의
            // 랜턴·화톳불은 여전히 Flame 색이어야 한다 — 씬 이름으로 통째로
            // 면제하지 않는다.
            var offPalette = Color.grey;
            Assert.IsNotEmpty(LightRule.Violations(
                Facts(assetPath: "Assets/01.Scenes/StartScene.unity",
                      color: offPalette, type: LightType.Point)));
            Assert.IsNotEmpty(LightRule.Violations(
                Facts(assetPath: "Assets/01.Scenes/StartScene.unity",
                      color: offPalette, type: LightType.Spot)));
        }

        [Test]
        public void Violations_report_includes_intensity_for_context()
        {
            var v = LightRule.Violations(Facts(color: Color.grey, intensity: 4.2f));
            Assert.AreEqual(1, v.Count);
            StringAssert.Contains("4.2", v[0]);
        }
    }
}
