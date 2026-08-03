using NUnit.Framework;
using UnityEngine;
using Survive.Domain.Art;

namespace Survive.Tests.EditMode
{
    /// <summary>
    /// 색상값은 P0 스펙 §4·§6에서 확정된 것이다.
    /// 이 테스트가 깨지면 코드가 아니라 스펙을 먼저 고쳤는지 확인한다.
    /// </summary>
    public class ArtPaletteTests
    {
        static void AssertHex(Color actual, int expectedRgb)
        {
            int r = Mathf.RoundToInt(actual.r * 255f);
            int g = Mathf.RoundToInt(actual.g * 255f);
            int b = Mathf.RoundToInt(actual.b * 255f);
            int packed = (r << 16) | (g << 8) | b;
            Assert.AreEqual(expectedRgb, packed, $"기대 #{expectedRgb:X6} / 실제 #{packed:X6}");
        }

        [Test] public void LightShaft_is_E8D5A8() => AssertHex(ArtPalette.LightShaft, 0xE8D5A8);
        [Test] public void Glowshroom_is_2FE6C8() => AssertHex(ArtPalette.Glowshroom, 0x2FE6C8);
        [Test] public void Flame_is_FF9A2E() => AssertHex(ArtPalette.Flame, 0xFF9A2E);
        [Test] public void Macronium_is_A12EE0() => AssertHex(ArtPalette.Macronium, 0xA12EE0);
        [Test] public void MacroniumHighlight_is_E77BFF() => AssertHex(ArtPalette.MacroniumHighlight, 0xE77BFF);

        [Test] public void WaterShallow_is_2E5C7A() => AssertHex(ArtPalette.WaterShallow, 0x2E5C7A);
        [Test] public void WaterDeep_is_0E1F2E() => AssertHex(ArtPalette.WaterDeep, 0x0E1F2E);

        [Test] public void FogSurface_is_C4703A() => AssertHex(ArtPalette.FogSurface, 0xC4703A);
        [Test] public void FogIslands_is_0C0F15() => AssertHex(ArtPalette.FogIslands, 0x0C0F15);
        [Test] public void FogPlains_is_0D1A18() => AssertHex(ArtPalette.FogPlains, 0x0D1A18);
        [Test] public void FogPlainsNight_is_140A1E() => AssertHex(ArtPalette.FogPlainsNight, 0x140A1E);
        [Test] public void FogCliffs_is_2C1240() => AssertHex(ArtPalette.FogCliffs, 0x2C1240);

        [Test]
        public void AllowedEmission_has_exactly_the_five_light_colors()
        {
            Assert.AreEqual(5, ArtPalette.AllowedEmission.Length,
                "광원은 넷이고 매크로늄만 하이라이트를 하나 더 갖는다. 늘리려면 스펙 §4를 먼저 고친다.");
            CollectionAssert.Contains(ArtPalette.AllowedEmission, ArtPalette.LightShaft);
            CollectionAssert.Contains(ArtPalette.AllowedEmission, ArtPalette.Glowshroom);
            CollectionAssert.Contains(ArtPalette.AllowedEmission, ArtPalette.Flame);
            CollectionAssert.Contains(ArtPalette.AllowedEmission, ArtPalette.Macronium);
            CollectionAssert.Contains(ArtPalette.AllowedEmission, ArtPalette.MacroniumHighlight);
        }
    }
}
