using NUnit.Framework;
using UnityEngine;
using Survive.Domain.Art;

namespace Survive.Tests.EditMode
{
    /// <summary>
    /// "깊이가 곧 자홍의 농도"(상세기획서 §7.3)를 값으로 지킨다.
    ///
    /// 화면을 보고 "자홍이 도는 것 같다"고 말할 수는 있지만, 그것이 팔레트의
    /// 자홍인지 누가 인스펙터에서 밀어 넣은 보라인지는 눈으로 구별되지 않는다.
    /// 여기서 밴드 표가 <see cref="ArtPalette"/>에서만 색을 끌어오는지,
    /// 내려갈수록 짙어지는 단조성이 깨지지 않는지 본다.
    /// </summary>
    public class DepthFogTests
    {
        const float Tolerance = 0.0005f;

        [Test]
        public void 밴드는_위에서_아래로_정렬돼_있다()
        {
            for (int i = 0; i < DepthFog.Bands.Length - 1; i++)
                Assert.Greater(DepthFog.Bands[i].Y, DepthFog.Bands[i + 1].Y,
                    $"밴드 {i}가 아래 밴드보다 낮다 — Sample의 이웃 탐색이 성립하지 않는다");
        }

        [Test]
        public void 내려갈수록_안개가_짙어진다()
        {
            for (int i = 0; i < DepthFog.Bands.Length - 1; i++)
                Assert.Less(DepthFog.Bands[i].Density, DepthFog.Bands[i + 1].Density,
                    "깊은 밴드가 얕은 밴드보다 옅다");
        }

        [Test]
        public void 섬_높이의_안개는_팔레트의_부유섬_색이다()
        {
            DepthFog.Sample(DepthFog.SeaLevelY + 50f, out var color, out _);
            AssertColor(ArtPalette.FogIslands, color);
        }

        [Test]
        public void 가장_깊은_곳의_안개는_팔레트의_절벽_색이다()
        {
            DepthFog.Sample(DepthFog.SeaLevelY - 500f, out var color, out _);
            AssertColor(ArtPalette.FogCliffs, color);
        }

        [Test]
        public void 내려가는_동안_자홍이_단조롭게_짙어진다()
        {
            // 붉은 채널이 아니라 "절벽 색에 얼마나 가까운가"로 본다.
            float previous = -1f;
            for (float y = DepthFog.SeaLevelY + 20f; y >= DepthFog.SeaLevelY - 40f; y -= 2f)
            {
                DepthFog.Sample(y, out var color, out _);
                float magenta = color.b - color.g;   // 자홍은 청이 녹보다 높다
                Assert.GreaterOrEqual(magenta, previous - Tolerance,
                    $"y={y}에서 자홍 기운이 도로 옅어졌다");
                previous = magenta;
            }
        }

        [Test]
        public void 밴드_경계에서_값이_튀지_않는다()
        {
            foreach (var band in DepthFog.Bands)
            {
                DepthFog.Sample(band.Y + 0.001f, out var above, out float da);
                DepthFog.Sample(band.Y - 0.001f, out var below, out float db);

                Assert.AreEqual(da, db, 0.001f, $"y={band.Y} 경계에서 밀도가 튄다");
                AssertColor(above, below, 0.01f);
            }
        }

        [Test]
        public void 표_바깥은_끝_밴드로_고정된다()
        {
            DepthFog.Sample(float.MaxValue, out var top, out float dt);
            Assert.AreEqual(DepthFog.Bands[0].Density, dt, Tolerance);
            AssertColor(DepthFog.Bands[0].Color, top);

            var last = DepthFog.Bands[DepthFog.Bands.Length - 1];
            DepthFog.Sample(float.MinValue, out var bottom, out float db);
            Assert.AreEqual(last.Density, db, Tolerance);
            AssertColor(last.Color, bottom);
        }

        [Test]
        public void 안개가_다_덮는_거리는_밀도에_반비례한다()
        {
            float thin = DepthFog.FullDistance(0.008f);
            float thick = DepthFog.FullDistance(0.045f);

            Assert.Greater(thin, thick);
            // exp(-(0.008·d)²) = 0.01 → d = √(ln100)/0.008 ≈ 268m
            Assert.AreEqual(268f, thin, 2f);
        }

        [Test]
        public void 원거리_평면은_유도등을_자를_만큼_줄지_않는다()
        {
            // 유도등 원칙 — 완전 어둠 구간의 탈출 방향은 멀리 보이는 광원 하나로만
            // 성립한다. 안개가 아무리 짙어도 바닥값 아래로는 내려가지 않아야 한다.
            float far = DepthFog.FarClipFor(0.5f, 3000f, minFar: 250f);
            Assert.AreEqual(250f, far, 0.01f);
        }

        [Test]
        public void 원거리_평면은_씬_설정을_넘지_않는다()
        {
            float far = DepthFog.FarClipFor(0.0001f, 3000f);
            Assert.AreEqual(3000f, far, 0.01f);
        }

        [Test]
        public void 섬_높이에서는_이백미터_너머까지_보인다()
        {
            DepthFog.Sample(DepthFog.SeaLevelY + 20f, out _, out float density);
            Assert.Greater(DepthFog.FullDistance(density), 200f,
                "섬 위에서 건너편 빛기둥이 안개에 먹힌다");
        }

        [Test]
        public void 모든_밴드에서_유도등을_둘_자리가_남는다()
        {
            // 유도등 원칙의 나머지 절반 — 원거리 평면이 자르지 않아도 안개가
            // 삼키면 결과는 같다. 가장 짙은 밴드에서도 사람이 걸어서 닿을 만한
            // 거리 안에는 광원을 둘 수 있어야 한다.
            foreach (var band in DepthFog.Bands)
            {
                float range = DepthFog.GuideLightRange(band.Density);
                Assert.Greater(range, 40f,
                    $"y={band.Y}의 안개가 40m 앞의 유도등도 삼킨다 (밀도 {band.Density})");
            }
        }

        [Test]
        public void 원거리_평면은_안개가_아직_보여_주는_광원을_자르지_않는다()
        {
            // 두 방어선이 어긋나면 안 된다. 안개는 아직 보여 주는데 컬링이
            // 잘라 버리면 유도등은 그냥 사라진다.
            foreach (var band in DepthFog.Bands)
            {
                float range = DepthFog.GuideLightRange(band.Density);
                float far = DepthFog.FarClipFor(band.Density, 3000f);
                Assert.GreaterOrEqual(far, range,
                    $"y={band.Y}에서 원거리 평면({far:F0}m)이 안개 가시거리({range:F0}m) 안쪽이다");
            }
        }

        [Test]
        public void 투과율은_거리가_멀수록_떨어진다()
        {
            float near = DepthFog.Transmittance(0.026f, 20f);
            float far = DepthFog.Transmittance(0.026f, 80f);
            Assert.Greater(near, far);
            Assert.AreEqual(1f, DepthFog.Transmittance(0.026f, 0f), 1e-4f);
        }

        [Test]
        public void 가시거리에서의_투과율이_정확히_바닥값이다()
        {
            float range = DepthFog.GuideLightRange(0.045f);
            Assert.AreEqual(DepthFog.GuideLightFloor,
                            DepthFog.Transmittance(0.045f, range), 1e-4f);
        }

        static void AssertColor(Color expected, Color actual, float tolerance = Tolerance)
        {
            Assert.AreEqual(expected.r, actual.r, tolerance, "R");
            Assert.AreEqual(expected.g, actual.g, tolerance, "G");
            Assert.AreEqual(expected.b, actual.b, tolerance, "B");
        }
    }
}
