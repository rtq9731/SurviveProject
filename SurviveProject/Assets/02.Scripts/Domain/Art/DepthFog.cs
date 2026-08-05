using UnityEngine;

namespace Survive.Domain.Art
{
    /// <summary>
    /// 안개 한 밴드. "이 높이에서는 이 색이 이만큼 짙다"가 전부다.
    /// </summary>
    public readonly struct FogBand
    {
        /// <summary>이 밴드의 기준 높이(월드 Y).</summary>
        public readonly float Y;

        public readonly Color Color;

        /// <summary>ExponentialSquared 밀도.</summary>
        public readonly float Density;

        public FogBand(float y, Color color, float density)
        {
            Y = y;
            Color = color;
            Density = density;
        }
    }

    /// <summary>
    /// 깊이가 곧 자홍의 농도다 (상세기획서 §7.3 / P0 아트 방향 §4).
    ///
    /// 그 문장을 구현하는 별도 시스템은 필요 없다 — <b>안개 하나로 끝난다.</b>
    /// 높이에 따라 <see cref="RenderSettings.fogColor"/>와 밀도를 갈아 끼우면
    /// 플레이어는 화면 색만으로 자기가 얼마나 내려왔는지 안다.
    ///
    /// <b>왜 여기(Domain)에 있는가.</b> 밴드 표와 보간은 Unity 없이 시험할 수
    /// 있어야 한다. 씬을 띄우지 않고 "수면 높이에서 자홍이 섞이기 시작하는가",
    /// "밴드 사이가 튀지 않는가"를 값으로 확인하는 것이 이 파일의 존재 이유다.
    ///
    /// <b>색은 반드시 <see cref="ArtPalette"/>에서 끌어온다.</b> 여기에 hex를
    /// 새로 적으면 아트 규칙 검사기가 보는 팔레트와 화면에 실제로 깔리는 색이
    /// 갈라진다. 값을 바꾸려면 ArtPalette(그리고 그 위의 스펙 문서)를 고친다.
    /// </summary>
    public static class DepthFog
    {
        /// <summary>MainScene의 수면 높이. 밴드 표가 이 높이를 기준으로 짜여 있다.</summary>
        public const float SeaLevelY = 50.1f;

        /// <summary>
        /// 높은 곳 → 깊은 곳 순서. Sample이 이 순서를 전제로 이웃 둘을 찾는다.
        ///
        /// 밀도는 "이 밴드에서 몇 미터까지 보이는가"로 정했다(<see cref="FullDistance"/>).
        /// - 섬 위 0.008 → 268m: 건너편 섬의 빛기둥이 읽혀야 한다(유도등 원칙).
        /// - 수면 0.014 → 153m: 섬 하나가 통째로 들어오는 거리.
        /// - 중간 0.026 → 82m: 발밑만 보인다.
        /// - 액면층 0.045 → 47m: 자홍이 화면을 지배한다.
        /// </summary>
        public static readonly FogBand[] Bands =
        {
            new FogBand(SeaLevelY + 20f, ArtPalette.FogIslands, 0.008f),
            new FogBand(SeaLevelY,       ArtPalette.FogIslands, 0.014f),
            new FogBand(SeaLevelY - 20f, MidDescent,            0.026f),
            new FogBand(SeaLevelY - 40f, ArtPalette.FogCliffs,  0.045f),
        };

        /// <summary>
        /// 섬과 절벽 사이. 자홍이 섞이기 시작하는 지점을 팔레트 두 색의 중간으로 잡는다 —
        /// 새 hex를 적지 않으면서 그라데이션의 중간 참을 만드는 유일한 방법이다.
        /// </summary>
        public static Color MidDescent => Color.Lerp(ArtPalette.FogIslands, ArtPalette.FogCliffs, 0.5f);

        /// <summary>
        /// 이 높이의 안개. 밴드 사이는 선형 보간, 표의 바깥은 끝 밴드로 고정한다.
        /// </summary>
        public static void Sample(float y, out Color color, out float density)
        {
            var top = Bands[0];
            if (y >= top.Y) { color = top.Color; density = top.Density; return; }

            var bottom = Bands[Bands.Length - 1];
            if (y <= bottom.Y) { color = bottom.Color; density = bottom.Density; return; }

            for (int i = 0; i < Bands.Length - 1; i++)
            {
                var upper = Bands[i];
                var lower = Bands[i + 1];
                if (y > upper.Y || y < lower.Y) continue;

                float span = upper.Y - lower.Y;
                float t = span <= 0f ? 0f : (upper.Y - y) / span;   // 0=위 밴드, 1=아래 밴드
                color = Color.Lerp(upper.Color, lower.Color, t);
                density = Mathf.Lerp(upper.Density, lower.Density, t);
                return;
            }

            color = bottom.Color;
            density = bottom.Density;
        }

        /// <summary>
        /// 안개가 화면을 다 덮는 거리. ExponentialSquared는
        /// factor = exp(-(density·d)²)이므로 1%가 남는 지점은 d = √(ln 100)/density다.
        /// 이 거리 너머는 그려도 안개색과 구별되지 않는다.
        /// </summary>
        public static float FullDistance(float density)
            => density <= 0f ? float.MaxValue : Mathf.Sqrt(Mathf.Log(100f)) / density;

        /// <summary>
        /// 안개가 짙으면 멀리 그릴 이유가 없다 — 밴드 밀도에서 카메라 원거리 평면을 낸다.
        ///
        /// <b>바닥(<paramref name="minFar"/>)을 두는 이유.</b> 완전 어둠 구간의 탈출
        /// 방향은 "저 멀리 보이는 광원 하나"로만 성립한다. 계산값이 아무리 작아도
        /// 그 아래로는 내리지 않는다 — 성능을 위해 유도등을 잘라내면 게임이 망가진다.
        /// </summary>
        public static float FarClipFor(float density, float sceneFar, float minFar = 250f)
        {
            float full = FullDistance(density) * 1.15f;   // 안개가 다 덮은 뒤로 한 뼘 여유
            return Mathf.Clamp(full, Mathf.Min(minFar, sceneFar), sceneFar);
        }
    }
}
