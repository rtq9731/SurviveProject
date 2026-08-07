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
    /// <b>규칙은 하나다 — 「무엇이 얼마나 두껍게 사이에 있는가」.</b> 재는 축만 다르다
    /// (상세기획서 §7.4, 2026-08-07 개정).
    ///
    /// <list type="table">
    /// <item><term>지상</term><description><b>거리</b>. 두꺼운 대기가 사이에 있고,
    ///   시선을 올릴수록 대기를 짧게 지난다 — <see cref="SampleSurface"/></description></item>
    /// <item><term>지하</term><description><b>깊이</b>. 매크로늄 농도가 깊이로 정해진다
    ///   — <see cref="Sample"/></description></item>
    /// <item><term>물속</term><description>랜턴 반경에서 역산 — <see cref="UnderwaterFog"/></description></item>
    /// </list>
    ///
    /// 셋이 같은 광학(<see cref="Transmittance"/>·<see cref="FullDistance"/>)을 쓰고
    /// 밀도를 정하는 방법만 갈린다. 두 벌을 만들면 "안개가 다 덮는 거리"의 정의가
    /// 두 곳에 적히고, 한쪽만 고치는 날이 온다.
    ///
    /// ── 깊이 축 ──
    ///
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
        /// 유도등이 안개를 뚫고 읽히는 최소 투과율. 배경이 거의 검정이고 유도등은
        /// HDR 광원(블룸 threshold 1.1 위)이라, 2%만 남아도 화면에서 점으로 잡힌다.
        /// 이보다 낮으면 안개색과 구별되지 않는다.
        /// </summary>
        public const float GuideLightFloor = 0.02f;

        /// <summary>
        /// 이 거리의 광원이 얼마나 남아 도착하는가. ExponentialSquared의 정의 그대로다.
        /// </summary>
        public static float Transmittance(float density, float distance)
            => density <= 0f ? 1f : Mathf.Exp(-(density * distance) * (density * distance));

        /// <summary>
        /// 유도등을 이 밴드에서 얼마나 멀리 둘 수 있는가 (§8-4 배치 예산).
        ///
        /// <b>유도등 원칙의 코드 측 절반이다.</b> 원거리 평면이 광원을 자르지 않는 것은
        /// <see cref="FarClipFor"/>가 보장하지만, 잘리지 않아도 안개가 삼켜 버리면
        /// 결과는 같다. 밀도에서 "아직 읽히는 최대 거리"를 내어, 배치가 그 안에
        /// 들어오는지 판정할 수 있게 한다.
        /// </summary>
        public static float GuideLightRange(float density, float floor = GuideLightFloor)
        {
            if (density <= 0f) return float.MaxValue;
            floor = Mathf.Clamp(floor, 1e-6f, 0.999999f);
            return Mathf.Sqrt(-Mathf.Log(floor)) / density;
        }

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

        // ══════════════════════════════════════════════════════════
        // 지상 — 두꺼운 대기 (세계관 §2, 상세기획서 §7.4)
        // ══════════════════════════════════════════════════════════
        //
        // <b>연출이 아니라 물성이다.</b> 이 행성의 대기는 지구와 같은 비율인데
        // 훨씬 두껍다. 그 한 물성이 넷을 설명한다 — 우주에서 지표를 확인 못 한 것,
        // 멀리가 안 보이는 것, 레이더가 필요한 것, 낮이 시들시들한 것.
        //
        // <b>탁한 것이 아니라 두꺼운 것이다.</b> 이 구분이 전부다. 먼지가 낀 것이면
        // 위도 흐려야 하고, 그러면 「밤은 별이 잘 보인다」가 깨진다. 맑은 공기가
        // 그저 많은 것이므로 <b>수평으로는 대기를 길게 지나 뿌옇고 천정으로는
        // 짧게 지나 맑다.</b> 지평선은 자홍으로 번지는데 머리 위는 별이 쏟아진다.

        /// <summary>
        /// 공기가 끝나고 매크로늄이 시작되는 높이. <b>이 위는 거리 축, 아래는 깊이 축</b>이다.
        ///
        /// 액면과 같은 자리로 잡는다 — 두 매질의 경계가 곧 두 규칙의 경계라야
        /// "왜 여기서 규칙이 바뀌는가"에 답이 있다. 이 아래로 머리가 잠기면
        /// <see cref="UnderwaterFog"/>가 이어받으므로, 경계 바로 아래 구간은
        /// 실제로는 물속 규칙이 덮는다.
        /// </summary>
        public const float AirLineY = SeaLevelY;

        // ── 튜닝값 둘 ────────────────────────────────────────────
        //
        // <b>둘 다 사람이 정할 값이다.</b> 여기 적힌 것은 재기 위한 임시값이고,
        // 실측을 붙여 올린 뒤 기획서 §7.4의 표에서 확정한다. 코드가 정할 수 있는
        // 것은 「곡선의 모양」이지 「몇 미터에서 세계가 닫히는가」가 아니다.

        /// <summary>
        /// 수평으로 볼 때의 대기 밀도. <b>임시값</b> —
        /// 지금까지 섬 위 밴드가 쓰던 값(0.008 → 268m)을 그대로 옮겨 왔다.
        /// 새로 정한 것이 아니라 <b>바뀌지 않은 것</b>이고, 그래서 이 라운드의
        /// 실측이 앞 라운드의 밝기 실측과 견줄 수 있다.
        /// </summary>
        public const float SurfaceBaseDensity = 0.008f;

        /// <summary>
        /// 대기의 척도고도(m) — 위로 이만큼 오를 때마다 밀도가 1/e로 준다.
        ///
        /// <b>실제 대기의 척도고도가 아니다.</b> 지구는 8500m이고 그 값을 쓰면
        /// 게임 안의 시선 범위(수백 미터)에서는 천정과 지평선이 구별되지 않는다.
        /// 여기서 정하는 것은 <b>화면에서 대비가 읽히는 정도</b>이고, 그 대비의
        /// 크기가 곧 <see cref="ZenithClearing"/>다 (대략 수평 가시거리 / 이 값).
        /// <b>임시값</b>.
        /// </summary>
        public const float SurfaceScaleHeight = 40f;

        /// <summary>
        /// 낮의 지평선이 햇빛 쪽으로 얼마나 씻기는가. 0이면 낮에도 순수 자홍,
        /// 1이면 낮에는 자홍이 사라진다. <b>임시값</b>.
        /// </summary>
        public const float DaylightWash = 0.5f;

        /// <summary>
        /// 낮의 지평선이 밤보다 몇 배 밝은가. <b>임시값</b> —
        /// 산란이 늘어난 만큼 지평선이 밝아지는 정도다.
        /// </summary>
        public const float DaylightBrightening = 2.5f;

        /// <summary>
        /// 밤의 지평선 휘도. <b>지금 씬이 정해 둔 바닥을 그대로 쓴다</b>
        /// (<see cref="ArtPalette.FogIslands"/>의 휘도).
        ///
        /// <b>밤은 어두워지는 것이 아니라 보라색이 짙어지는 것이다</b>(기획서 §5.14).
        /// 그래서 밤에 옮기는 것은 휘도가 아니라 <b>색조</b>다 — 밝기를 그대로 두고
        /// 색만 자홍으로 옮기면, 앞 라운드가 잰 밤의 휘도 바닥(0.0376)이 흔들리지
        /// 않으면서 화면은 보라로 간다.
        /// </summary>
        public static float NightHorizonLuminance => ArtPalette.Luminance(ArtPalette.FogIslands);

        // ── 대기를 얼마나 지나는가 ───────────────────────────────

        /// <summary>
        /// 시선을 <paramref name="elevationDegrees"/>도로 올려 <paramref name="distance"/>미터를
        /// 나아갈 때 <b>실제로 지나는 대기의 두께</b>(m).
        ///
        /// 밀도가 높이에 따라 지수로 줄어드는 대기에서 시선을 따라 적분한 값이다:
        /// ∫₀^d exp(-s·sinφ / H) ds = H·(1 - exp(-d·sinφ/H)) / sinφ.
        ///
        /// <b>이 한 식이 두 문장을 동시에 말한다.</b>
        /// <list type="bullet">
        /// <item>φ → 0 (수평): 값이 <paramref name="distance"/>로 수렴한다 —
        ///       <b>거리에 비례</b>한다</item>
        /// <item>φ = 90도, d → ∞ (천정): 값이 H로 <b>수렴한다</b> —
        ///       아무리 멀리 봐도 대기는 그만큼밖에 없다</item>
        /// </list>
        ///
        /// <b>내려다보는 쪽은 줄이지 않는다.</b> 발밑으로는 대기가 더 짙어야 맞지만
        /// 카메라가 이미 지표에 붙어 있어 아래로 남은 대기가 거의 없다. 수평값을
        /// 그대로 쓰는 것이 안전한 쪽이다 — 잘못 줄이면 발밑이 부자연스럽게 맑아진다.
        /// </summary>
        public static float AtmospherePath(float elevationDegrees, float distance)
        {
            float sin = Mathf.Sin(elevationDegrees * Mathf.Deg2Rad);
            if (sin <= 0f || SurfaceScaleHeight <= 0f) return distance;

            if (float.IsPositiveInfinity(distance)) return SurfaceScaleHeight / sin;

            float u = distance * sin / SurfaceScaleHeight;
            // u가 아주 작으면 (1-e^-u)/u 가 0/0이 된다. 그 극한은 1이고,
            // 그때 값은 distance 그대로다.
            if (u < 1e-4f) return distance;

            return SurfaceScaleHeight * (1f - Mathf.Exp(-u)) / sin;
        }

        /// <summary>수평으로 볼 때 세계가 닫히는 거리(m). 대기 두께를 재는 기준자다.</summary>
        public static float SurfaceHorizonReach => FullDistance(SurfaceBaseDensity);

        /// <summary>
        /// 이 시선 고도에서 쓸 안개 밀도.
        ///
        /// Unity의 안개는 방향을 모르고 밀도 하나만 안다. 그래서 <b>기준 거리만큼
        /// 나아가는 동안의 평균 밀도</b>를 내어 넣는다 — 지나는 대기가 얇아진 만큼
        /// 밀도가 옅어지고, 그만큼 멀리까지 보인다.
        /// </summary>
        public static float SurfaceDensity(float elevationDegrees)
        {
            float reach = SurfaceHorizonReach;
            if (reach <= 0f) return SurfaceBaseDensity;

            return SurfaceBaseDensity * (AtmospherePath(elevationDegrees, reach) / reach);
        }

        /// <summary>
        /// <b>천정을 볼 때 세계가 수평의 몇 배까지 열리는가.</b>
        /// 이 라운드의 핵심 숫자이고, 대략 <see cref="SurfaceHorizonReach"/> /
        /// <see cref="SurfaceScaleHeight"/>다.
        /// </summary>
        public static float ZenithClearing =>
            FullDistance(SurfaceDensity(90f)) / FullDistance(SurfaceDensity(0f));

        // ── 색 ──────────────────────────────────────────────────

        /// <summary>
        /// 지평선의 색. <b>자홍이다</b> — 팔레트의 매크로늄을 그대로 쓰고
        /// 휘도만 시각이 정한다(새 hex를 적지 않는 것은 <see cref="MidDescent"/>와
        /// 같은 이유다).
        ///
        /// 낮에는 햇빛 쪽으로 씻겨 옅은 연보라가 되고, 밤에는 <b>같은 밝기 그대로
        /// 순수 자홍</b>이 된다. 밤이 「어두운 낮」이 아니라 「보라가 짙어진 것」으로
        /// 보이는 자리가 여기다.
        /// </summary>
        public static Color HorizonColor(float daylight)
        {
            float d = Mathf.Clamp01(daylight);

            var night = ArtPalette.WithLuminance(ArtPalette.Macronium, NightHorizonLuminance);
            var day = ArtPalette.WithLuminance(
                Color.Lerp(ArtPalette.Macronium, ArtPalette.LightShaft, DaylightWash),
                NightHorizonLuminance * DaylightBrightening);

            var c = Color.Lerp(night, day, d);
            c.a = 1f;
            return c;
        }

        /// <summary>
        /// 대기 너머. <b>우주는 검다</b> — 하늘이 밝은 것은 대기가 빛을 흩기 때문이고,
        /// 대기를 짧게 지나는 방향에서는 흩을 것이 없다.
        /// </summary>
        public static readonly Color BeyondTheAir = new Color(0f, 0f, 0f, 1f);

        /// <summary>
        /// 이 시선 고도에서 <b>대기가 배경을 얼마나 덮는가</b>(0~1).
        ///
        /// 배경은 무한히 먼 곳이므로 지나는 대기는 <see cref="AtmospherePath"/>의
        /// 수렴값이다. 다만 수평 쪽에서는 그 값이 발산하므로 수평 가시거리에서
        /// 자른다 — 어차피 그 너머는 안개색과 구별되지 않는다.
        /// </summary>
        public static float SkyCoverage(float elevationDegrees)
        {
            float path = Mathf.Min(AtmospherePath(elevationDegrees, float.PositiveInfinity),
                                   SurfaceHorizonReach);
            return 1f - Transmittance(SurfaceBaseDensity, path);
        }

        /// <summary>
        /// 이 시선 고도에서 배경(하늘)에 깔릴 색.
        ///
        /// <b>「지평선은 뿌옇고 머리 위는 맑다」가 실제로 화면에 나오는 자리다.</b>
        /// 지평선 쪽은 대기가 배경을 통째로 덮어 자홍이 되고, 천정 쪽은 대기가
        /// 거의 없어 우주가 그대로 비친다.
        ///
        /// <b>지금은 화면 전체가 이 색 하나다.</b> 씬에 스카이박스가 없어
        /// (<c>m_SkyboxMaterial: 0</c>) 배경이 단색으로 지워지기 때문이다. 한 화면
        /// 안에서 지평선과 천정이 <b>동시에</b> 대비를 이루려면 이 함수를 시선
        /// 방향마다 부르는 스카이박스가 있어야 한다 — 그 자리를 여기 열어 둔다.
        /// </summary>
        public static Color SkyColor(float elevationDegrees, float daylight)
        {
            var c = Color.Lerp(BeyondTheAir, HorizonColor(daylight), SkyCoverage(elevationDegrees));
            c.a = 1f;
            return c;
        }

        /// <summary>
        /// 지상의 안개. 깊이가 아니라 <b>시선 고도와 시각</b>이 정한다.
        /// </summary>
        public static void SampleSurface(float elevationDegrees, float daylight,
                                         out Color color, out float density)
        {
            color = HorizonColor(daylight);
            density = SurfaceDensity(elevationDegrees);
        }
    }
}
