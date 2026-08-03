using NUnit.Framework;
using UnityEngine;
using Survive.Domain.Art;

namespace Survive.Tests.EditMode
{
    /// <summary>
    /// 사람이 눈으로 지키는 규칙은 지켜지지 않는다. 그래서 판정을 코드로 둔다.
    /// </summary>
    public class MaterialRuleTests
    {
        static MaterialFacts Facts(
            string shader = "Universal Render Pipeline/Lit",
            float smoothness = MaterialRule.SmoothnessMatte,
            float metallic = 0f,
            bool hasEmission = false,
            Color? emission = null)
            => new MaterialFacts("Assets/dummy.mat", shader, smoothness, metallic,
                                 hasEmission, emission ?? Color.black);

        [Test]
        public void Clean_material_has_no_violations()
        {
            Assert.IsEmpty(MaterialRule.Violations(Facts()));
        }

        [Test]
        public void Unlisted_shader_is_a_violation()
        {
            var v = MaterialRule.Violations(Facts(shader: "Custom/SomePackToon"));
            Assert.AreEqual(1, v.Count);
            StringAssert.Contains("셰이더", v[0]);
        }

        [Test]
        public void Water_shader_is_allowed()
        {
            // "Stylized Water For URP" 패키지가 실제로 물고 있는 셰이더 이름은
            // 폴더명과 다르게 그냥 "Stylized Water"다. 프로젝트에 실제로 존재하는
            // 이름을 검증해야 이 테스트가 실체 없는 이름을 통과시키지 않는다.
            Assert.IsTrue(MaterialRule.IsAllowedShader("Stylized Water"));
        }

        [TestCase(0.1f)]
        [TestCase(0.35f)]
        [TestCase(0.6f)]
        public void Banded_smoothness_is_allowed(float v)
        {
            Assert.IsTrue(MaterialRule.IsBandedSmoothness(v));
        }

        [TestCase(0.0f)]
        [TestCase(0.25f)]
        [TestCase(0.5f)]
        [TestCase(1.0f)]
        public void Off_band_smoothness_is_a_violation(float v)
        {
            Assert.IsFalse(MaterialRule.IsBandedSmoothness(v));
            var list = MaterialRule.Violations(Facts(smoothness: v));
            Assert.AreEqual(1, list.Count);
            StringAssert.Contains("스무스니스", list[0]);
        }

        [Test]
        public void Smoothness_within_tolerance_is_allowed()
        {
            Assert.IsTrue(MaterialRule.IsBandedSmoothness(
                MaterialRule.SmoothnessSemi + MaterialRule.SmoothnessTolerance * 0.5f));
        }

        [Test]
        public void Emission_in_palette_is_allowed()
        {
            Assert.IsEmpty(MaterialRule.Violations(
                Facts(hasEmission: true, emission: ArtPalette.Glowshroom)));
        }

        [Test]
        public void Emission_outside_palette_is_a_violation()
        {
            var v = MaterialRule.Violations(
                Facts(hasEmission: true, emission: ArtPalette.FromHex(0x00FF00)));
            Assert.AreEqual(1, v.Count);
            StringAssert.Contains("발광", v[0]);
        }

        [Test]
        public void Emission_off_ignores_the_color_field()
        {
            Assert.IsEmpty(MaterialRule.Violations(
                Facts(hasEmission: false, emission: ArtPalette.FromHex(0x00FF00))));
        }

        [Test]
        public void Hdr_emission_beyond_one_still_matches_by_hue()
        {
            var bright = ArtPalette.Macronium * 3f;
            Assert.IsTrue(MaterialRule.IsAllowedEmission(bright),
                "HDR 발광은 강도가 1을 넘는다. 강도가 아니라 색으로 판정해야 한다.");
        }

        [Test]
        public void Violations_are_reported_together()
        {
            var v = MaterialRule.Violations(Facts(
                shader: "Custom/SomePackToon", smoothness: 0.42f,
                hasEmission: true, emission: ArtPalette.FromHex(0x00FF00)));
            Assert.AreEqual(3, v.Count, "위반은 하나만 보고하고 멈추면 안 된다");
        }
    }
}
