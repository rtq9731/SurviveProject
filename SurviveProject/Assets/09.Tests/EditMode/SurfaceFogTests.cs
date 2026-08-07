using NUnit.Framework;
using UnityEngine;
using Survive.Domain.Art;
using Survive.World;

namespace Survive.Tests.EditMode
{
    /// <summary>
    /// <b>지상 안개는 연출이 아니라 물성이다</b> (세계관 §2 · 상세기획서 §7.4).
    ///
    /// 여기서 지키는 것은 셋이다.
    /// <list type="number">
    /// <item><b>축이 둘이다.</b> 지상은 거리, 지하는 깊이. 두 축이 각각
    ///       제 규칙대로 도는가</item>
    /// <item><b>탁한 것이 아니라 두꺼운 것이다.</b> 수평으로는 대기를 길게 지나
    ///       뿌옇고 천정으로는 짧게 지나 맑은가 — 이 구분이 깨지면
    ///       「밤은 별이 잘 보인다」가 함께 깨진다</item>
    /// <item><b>회귀선.</b> 지하와 물속 안개는 <b>한 값도</b> 움직이지 않았는가.
    ///       지상 규칙을 들이면서 저 둘을 건드리는 것이 가장 흔한 사고다</item>
    /// </list>
    ///
    /// 밀도·거리 값 자체는 여기서 판정하지 않는다. <b>사람이 정할 튜닝값</b>이고
    /// 코드가 지킬 것은 곡선의 모양이다.
    /// </summary>
    public class SurfaceFogTests
    {
        const float Tolerance = 0.0005f;

        // ── ① 수평은 거리에 비례한다 ──────────────────────────────

        [Test]
        public void 수평으로는_지나는_대기가_거리_그대로다()
        {
            foreach (float d in new[] { 1f, 10f, 100f, 268f, 1000f })
                Assert.AreEqual(d, DepthFog.AtmospherePath(0f, d), d * 0.001f,
                    $"수평 {d}m에서 지나는 대기가 거리와 다르다 — 지상 포그는 거리 축이다");
        }

        [Test]
        public void 내려다보는_쪽은_수평과_같다()
        {
            // 발밑으로 대기가 더 짙어야 맞지만 카메라가 지표에 붙어 있어
            // 아래로 남은 대기가 거의 없다. 수평값을 그대로 쓰는 쪽이 안전하다.
            foreach (float elevation in new[] { -1f, -30f, -89f })
                Assert.AreEqual(100f, DepthFog.AtmospherePath(elevation, 100f), 0.01f,
                    $"고도 {elevation}도에서 값이 수평과 다르다");
        }

        // ── ② 위를 볼수록 맑다 ────────────────────────────────────

        [Test]
        public void 시선을_올릴수록_지나는_대기가_얇아진다()
        {
            float previous = float.MaxValue;
            for (float elevation = 0f; elevation <= 90f; elevation += 5f)
            {
                float path = DepthFog.AtmospherePath(elevation, DepthFog.SurfaceHorizonReach);
                Assert.LessOrEqual(path, previous + Tolerance,
                    $"고도 {elevation}도에서 대기가 도로 두꺼워졌다");
                previous = path;
            }
        }

        [Test]
        public void 천정에서는_아무리_멀리_봐도_대기가_척도고도만큼이다()
        {
            // 이것이 「탁한 것이 아니라 두꺼운 것」의 알맹이다. 먼지가 낀 것이라면
            // 멀리 볼수록 계속 두꺼워져 위도 흐려진다.
            float near = DepthFog.AtmospherePath(90f, 1000f);
            float far = DepthFog.AtmospherePath(90f, 100000f);

            Assert.AreEqual(DepthFog.SurfaceScaleHeight, far, 0.01f);
            Assert.AreEqual(near, far, 0.5f, "천정 쪽 값이 거리를 따라 계속 자란다");
        }

        [Test]
        public void 밀도는_고도를_따라_단조롭게_옅어진다()
        {
            float previous = float.MaxValue;
            for (float elevation = 0f; elevation <= 90f; elevation += 5f)
            {
                float density = DepthFog.SurfaceDensity(elevation);
                Assert.LessOrEqual(density, previous + 1e-6f,
                    $"고도 {elevation}도에서 안개가 도로 짙어졌다");
                Assert.Greater(density, 0f, $"고도 {elevation}도에서 안개가 사라졌다");
                previous = density;
            }
        }

        [Test]
        public void 수평_밀도는_기준값_그대로다()
        {
            Assert.AreEqual(DepthFog.SurfaceBaseDensity, DepthFog.SurfaceDensity(0f), 1e-6f);
            Assert.AreEqual(DepthFog.SurfaceBaseDensity, DepthFog.SurfaceDensity(-45f), 1e-6f);
        }

        [Test]
        public void 천정이_지평선보다_훨씬_멀리까지_열린다()
        {
            // <b>이 라운드의 핵심 숫자.</b> 값 자체는 튜닝값(척도고도)이 정하므로
            // 여기서는 "눈에 띄게 열린다"만 지킨다 — 2배도 안 되면 같은 화면 안에서
            // 지평선과 천정이 구별되지 않는다.
            Assert.Greater(DepthFog.ZenithClearing, 2f,
                $"천정이 수평의 {DepthFog.ZenithClearing:F2}배밖에 안 열린다");

            // 대략 수평 가시거리 / 척도고도다. 식을 갈아 끼우면 여기서 잡힌다.
            float expected = DepthFog.SurfaceHorizonReach / DepthFog.SurfaceScaleHeight;
            Assert.AreEqual(expected, DepthFog.ZenithClearing, expected * 0.05f);
        }

        // ── ③ 하늘 — 지평선은 뿌옇고 머리 위는 맑다 ────────────────

        [Test]
        public void 지평선_쪽_하늘은_대기가_통째로_덮는다()
        {
            Assert.Greater(DepthFog.SkyCoverage(0f), 0.9f,
                "지평선 너머가 비쳐 보인다 — 「끝이 보이면 끝이 있는 것」이 된다");
        }

        [Test]
        public void 천정_쪽_하늘은_대부분_비어_있다()
        {
            Assert.Less(DepthFog.SkyCoverage(90f), 0.3f,
                "머리 위가 뿌옇다 — 「밤은 별이 잘 보인다」가 깨진다");
        }

        [Test]
        public void 하늘을_덮는_정도는_고도를_따라_단조롭게_준다()
        {
            float previous = float.MaxValue;
            for (float elevation = 0f; elevation <= 90f; elevation += 5f)
            {
                float cover = DepthFog.SkyCoverage(elevation);
                Assert.LessOrEqual(cover, previous + Tolerance,
                    $"고도 {elevation}도에서 하늘이 도로 뿌예졌다");
                previous = cover;
            }
        }

        [Test]
        public void 천정의_하늘이_지평선보다_어둡다()
        {
            foreach (float daylight in new[] { 0f, 0.5f, 1f })
            {
                float horizon = ArtPalette.Luminance(DepthFog.SkyColor(0f, daylight));
                float zenith = ArtPalette.Luminance(DepthFog.SkyColor(90f, daylight));

                Assert.Less(zenith, horizon * 0.5f,
                    $"햇빛 {daylight}에서 머리 위(휘도 {zenith:F4})가 지평선({horizon:F4})만큼 밝다");
            }
        }

        // ── ④ 밤에는 자홍이 짙어진다 ──────────────────────────────

        /// <summary>자홍의 정도. 청이 녹보다 얼마나 높은가 — 밴드 검사와 같은 자다.</summary>
        static float 자홍기운(Color c) => c.b - c.g;

        [Test]
        public void 밤의_지평선은_팔레트의_자홍_그대로다()
        {
            var night = DepthFog.HorizonColor(0f);
            var 자홍 = ArtPalette.Macronium;

            // 밝기는 다르되 색조는 같아야 한다. 채널 비율로 본다.
            float k = ArtPalette.Luminance(night) / ArtPalette.Luminance(자홍);
            Assert.AreEqual(자홍.r * k, night.r, 0.002f, "R");
            Assert.AreEqual(자홍.g * k, night.g, 0.002f, "G");
            Assert.AreEqual(자홍.b * k, night.b, 0.002f, "B");
        }

        [Test]
        public void 밤이_낮보다_자홍이_짙다()
        {
            float 밤 = 자홍기운(DepthFog.HorizonColor(0f));
            float 낮 = 자홍기운(DepthFog.HorizonColor(1f));

            Assert.Greater(밤, 낮,
                $"밤의 자홍({밤:F4})이 낮({낮:F4})보다 옅다 — " +
                "밤은 어두워지는 것이 아니라 보라색이 짙어지는 것이다");
            Assert.Greater(밤, 0.05f, "밤인데 자홍이 거의 없다");
        }

        [Test]
        public void 밤이_낮보다_어둡다()
        {
            Assert.Less(ArtPalette.Luminance(DepthFog.HorizonColor(0f)),
                        ArtPalette.Luminance(DepthFog.HorizonColor(1f)),
                        "밤의 지평선이 낮보다 밝다");
        }

        [Test]
        public void 밤의_휘도_바닥은_지금_씬이_정한_그대로다()
        {
            // 앞 라운드가 잰 밤의 화면 휘도(0.0376)는 이 값이 정한다.
            // 색조만 옮기고 밝기는 옮기지 않았다는 것을 못 박는다.
            Assert.AreEqual(ArtPalette.Luminance(ArtPalette.FogIslands),
                            ArtPalette.Luminance(DepthFog.HorizonColor(0f)), 1e-4f);
        }

        [Test]
        public void 밤낮_곡선을_따라_자홍이_단조롭게_짙어진다()
        {
            // 밤낮 시계와 실제로 물려 있는지 본다. 곡선을 갈아 끼우면 여기서 잡힌다.
            float previous = float.MinValue;
            for (float t = DayNightCycle.DuskStart; t <= DayNightCycle.DuskEnd + 0.001f; t += 0.005f)
            {
                float 자홍 = 자홍기운(DepthFog.HorizonColor(DayNightCycle.Daylight(t)));
                Assert.GreaterOrEqual(자홍, previous - Tolerance,
                    $"해질녘 {t:F3}에서 자홍이 도로 옅어졌다");
                previous = 자홍;
            }

            Assert.Greater(자홍기운(DepthFog.HorizonColor(DayNightCycle.Daylight(0f))),
                           자홍기운(DepthFog.HorizonColor(DayNightCycle.Daylight(0.5f))),
                           "자정이 정오보다 자홍이 옅다");
        }

        [Test]
        public void 지평선_색은_새_hex가_아니라_팔레트에서_나온다()
        {
            // 낮의 지평선은 자홍과 햇빛의 혼합이다. 채널 비율이 그 혼합과 같아야 한다.
            var mix = Color.Lerp(ArtPalette.Macronium, ArtPalette.LightShaft, DepthFog.DaylightWash);
            var day = DepthFog.HorizonColor(1f);

            float k = ArtPalette.Luminance(day) / ArtPalette.Luminance(mix);
            Assert.AreEqual(mix.r * k, day.r, 0.002f, "R");
            Assert.AreEqual(mix.g * k, day.g, 0.002f, "G");
            Assert.AreEqual(mix.b * k, day.b, 0.002f, "B");
        }

        // ── ⑤ 두 축의 경계 ───────────────────────────────────────

        [Test]
        public void 공기의_아랫끝은_액면이다()
        {
            Assert.AreEqual(DepthFog.SeaLevelY, DepthFog.AirLineY, 0.001f,
                "두 매질의 경계와 두 규칙의 경계가 어긋나면 " +
                "「왜 여기서 규칙이 바뀌는가」에 답이 없다");
        }

        [Test]
        public void 경계에서_밀도가_두_배_넘게_튀지_않는다()
        {
            // 완전히 이어 붙일 수는 없다 — 위는 공기고 아래는 매크로늄이라
            // 애초에 다른 매질이다. 다만 걸어 들어가는 사람에게 "툭" 하고
            // 보이지 않을 만큼은 좁혀 둔다.
            DepthFog.Sample(DepthFog.AirLineY, out _, out float below);
            float above = DepthFog.SurfaceDensity(0f);

            float ratio = Mathf.Max(above, below) / Mathf.Min(above, below);
            Assert.Less(ratio, 2f, $"액면을 넘는 순간 안개가 {ratio:F2}배로 튄다");
        }

        // ── ⑥ 회귀선 — 지하와 물속은 한 값도 안 움직였다 ───────────

        [Test]
        public void 지하_밴드는_한_값도_움직이지_않았다()
        {
            // 밴드 표를 그대로 받아 적는다. 지상 규칙을 들이면서 이 표를
            // 건드리면 여기서 빨개진다.
            var 기대 = new[]
            {
                (DepthFog.SeaLevelY + 20f, ArtPalette.FogIslands, 0.008f),
                (DepthFog.SeaLevelY,       ArtPalette.FogIslands, 0.014f),
                (DepthFog.SeaLevelY - 20f, DepthFog.MidDescent,   0.026f),
                (DepthFog.SeaLevelY - 40f, ArtPalette.FogCliffs,  0.045f),
            };

            Assert.AreEqual(기대.Length, DepthFog.Bands.Length, "밴드 개수가 바뀌었다");

            for (int i = 0; i < 기대.Length; i++)
            {
                var (y, color, density) = 기대[i];
                DepthFog.Sample(y, out var 실제색, out float 실제밀도);

                Assert.AreEqual(density, 실제밀도, 1e-5f, $"y={y}의 밀도가 바뀌었다");
                AssertColor(color, 실제색, $"y={y}의 색이 바뀌었다");
            }
        }

        [Test]
        public void 지하_밴드_사이_보간도_그대로다()
        {
            DepthFog.Sample(DepthFog.SeaLevelY - 10f, out var color, out float density);

            Assert.AreEqual(0.020f, density, 1e-5f);
            AssertColor(Color.Lerp(ArtPalette.FogIslands, ArtPalette.FogCliffs, 0.25f), color,
                        "밴드 사이 보간이 바뀌었다");
        }

        [Test]
        public void 물속_안개는_한_값도_움직이지_않았다()
        {
            // 랜턴 반경에서 역산한 값들이다. 22m에서 세계가 닫힌다.
            Assert.AreEqual(22f, UnderwaterFog.CloseDistance, 1e-4f, "닫히는 거리");
            Assert.AreEqual(0.0975439f, UnderwaterFog.Density, 1e-6f, "밀도");

            var c = UnderwaterFog.Color;
            Assert.AreEqual(0.057048f, c.r, 0.0005f, "R");
            Assert.AreEqual(0.023337f, c.g, 0.0005f, "G");
            Assert.AreEqual(0.082983f, c.b, 0.0005f, "B");
        }

        [Test]
        public void 물속이_지상보다_어둡다는_규칙이_살아_있다()
        {
            Assert.Less(UnderwaterFog.Luminance(UnderwaterFog.Color),
                        UnderwaterFog.SurfaceLuminance,
                        "물속이 수면 위보다 밝다 — 어둠 축이 뒤집혔다");
        }

        [Test]
        public void 휘도를_재는_자가_두_곳에서_갈리지_않았다()
        {
            // 팔레트와 물속 규칙이 각각 휘도 식을 들고 있다. 하나를 고치고
            // 다른 하나를 잊으면 같은 색이 두 밝기가 된다.
            foreach (var c in new[]
                     {
                         ArtPalette.Macronium, ArtPalette.FogIslands,
                         ArtPalette.FogCliffs, ArtPalette.LightShaft,
                     })
                Assert.AreEqual(UnderwaterFog.Luminance(c), ArtPalette.Luminance(c), 1e-6f);
        }

        // ── 도구 ────────────────────────────────────────────────

        static void AssertColor(Color expected, Color actual, string message)
        {
            Assert.AreEqual(expected.r, actual.r, Tolerance, message + " (R)");
            Assert.AreEqual(expected.g, actual.g, Tolerance, message + " (G)");
            Assert.AreEqual(expected.b, actual.b, Tolerance, message + " (B)");
        }
    }
}
