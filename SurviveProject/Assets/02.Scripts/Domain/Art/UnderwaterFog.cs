using UnityEngine;
using Survive.World;

namespace Survive.Domain.Art
{
    /// <summary>
    /// 물속 안개. <b>이 파일은 안개와 랜턴의 관계 하나만 정한다</b>
    /// (검토회신 2026-08-07 ②④).
    ///
    /// <b>고치기 전의 상태.</b> 물속에서는 랜턴을 켜도 화면에 아무 일도 안
    /// 일어났다. 실측해 보니 원인은 "안개가 랜턴을 삼킨다"가 아니었다 —
    /// 밀도 0.028은 <see cref="DepthFog.FullDistance"/>로 <b>77m</b>이라
    /// 랜턴 반경(8m)에서는 빛을 1%도 못 깎는다. 진짜 문제는 둘이었다.
    ///
    /// <list type="number">
    /// <item><b>세계가 안 닫혔다.</b> 77m까지 흐릿하게 다 보이니 반경 밖이
    ///       "새까만 것"이 아니라 <b>평평한 자홍 벽지</b>였다. 그러면 반경
    ///       안과 밖을 가르는 것이 없어 랜턴을 켜도 달라지는 것이 없고,
    ///       가장자리 연출도 그 벽지 위에서는 눈에 안 잡힌다.</item>
    /// <item><b>물속이 뭍보다 밝았다.</b> 물속 안개색이
    ///       <see cref="ArtPalette.FogCliffs"/>(휘도 0.105)인데 수면 위 밴드는
    ///       <see cref="ArtPalette.FogIslands"/>(0.058)다. 깊이 들어갈수록
    ///       밝아지는 것은 어둠 축과 정확히 반대다.</item>
    /// </list>
    ///
    /// <b>그래서 정하는 것도 둘이다.</b>
    /// <list type="number">
    /// <item><b>세계가 닫히는 거리를 랜턴 반경에 매단다.</b> 랜턴 정면 도달
    ///       거리의 <see cref="CloseAtReachMultiple"/>배에서 안개가 화면을 다
    ///       덮는다. 반경 안은 랜턴이 이기고 반경 밖은 새까맣다 — 지상과 같은
    ///       규칙이고, 물속에서만 <b>유효 반경이 더 짧다</b>
    ///       (<see cref="LanternRangeUnderwater"/>).</item>
    /// <item><b>먼 곳의 색을 수면 위보다 어둡게 못 박는다.</b> 새 hex를 적지
    ///       않는다 — 팔레트의 자홍을 그대로 쓰되 휘도만 내린다. 색을 새로
    ///       적으면 아트 규칙 검사기가 보는 팔레트와 화면이 갈라진다
    ///       (<see cref="DepthFog.MidDescent"/>가 같은 이유로 Lerp를 쓴다).</item>
    /// </list>
    ///
    /// <b>연출 상수는 여기 없다.</b> 화면 가장자리가 조여드는 정도
    /// (<c>DiveRule.EdgeDarkness</c>)는 손대지 않는다. 렌더링을 먼저 정리하고
    /// 연출 값은 그 위에서 다시 보기로 했기 때문이다 — 지금 올려 두면 안개를
    /// 고친 뒤에 두 번 맞춰야 한다.
    /// </summary>
    public static class UnderwaterFog
    {
        // ══ 튜닝 손잡이 셋 ══════════════════════════════════════

        /// <summary>
        /// 세계가 닫히는 거리 = 랜턴 정면 도달 거리 × 이 배수.
        ///
        /// <b>1에 가까우면 반경 끝이 곧 벽</b>이라 앞이 안 보여 이동이
        /// 성립하지 않고, <b>크면 반경 밖이 계속 보여</b> 켜고 끄는 것이
        /// 아무 차이도 만들지 않는다. 2는 "빛이 닿는 데까지가 또렷하고,
        /// 그 두 배쯤에서 완전히 닫힌다"이다.
        /// </summary>
        public const float CloseAtReachMultiple = 2f;

        /// <summary>
        /// 물속 먼 곳의 휘도가 수면 위 밴드의 몇 배인가. <b>1보다 작아야 한다.</b>
        /// 물속이 지상보다 밝으면 깊이 들어갈수록 편해지고, 그 순간
        /// "어둠은 비용"이 뒤집힌다.
        /// </summary>
        public const float DarkerThanSurface = 0.6f;

        /// <summary>
        /// 빛이 <b>아직 읽히는</b> 최소 투과율. 이 아래로 떨어지면 안개색과
        /// 구별되지 않는다 — <see cref="DepthFog.GuideLightFloor"/>가 유도등에
        /// 대해 잡은 것과 같은 종류의 바닥이되, 이쪽은 <b>면을 비추는 빛</b>이라
        /// 훨씬 높다. 점 하나로 보이는 것과 벽면이 읽히는 것은 다른 일이다.
        /// </summary>
        public const float VisibleTransmittanceFloor = 0.35f;

        // ══ 파생값 ══════════════════════════════════════════════

        /// <summary>기준이 되는 랜턴. 티어 1이 "가장 좁을 때"이므로 여기에 맞춘다.</summary>
        public const int BaselineTier = 1;

        /// <summary>안개가 화면을 다 덮는 거리(m). 이 너머는 그려도 안개색이다.</summary>
        public static float CloseDistance =>
            LanternRule.ForwardReachForTier(BaselineTier) * CloseAtReachMultiple;

        /// <summary>
        /// 물속 <see cref="FogMode.ExponentialSquared"/> 밀도.
        /// <see cref="DepthFog.FullDistance"/>의 역함수다 — 거리에서 밀도를 낸다.
        /// </summary>
        public static float Density => DensityForFullDistance(CloseDistance);

        /// <summary>이 거리에서 화면을 다 덮으려면 필요한 밀도.</summary>
        public static float DensityForFullDistance(float distance) =>
            distance <= 0f ? 0f : Mathf.Sqrt(Mathf.Log(100f)) / distance;

        /// <summary>수면 위 밴드의 휘도. 물속이 넘으면 안 되는 천장이다.</summary>
        public static float SurfaceLuminance => Luminance(ArtPalette.FogIslands);

        /// <summary>
        /// 물속 안개색. 팔레트의 자홍을 그대로 쓰되 휘도만 내린다 —
        /// 색조는 세계의 것이고 밝기는 이 규칙의 것이다.
        /// </summary>
        public static Color Color =>
            WithLuminance(ArtPalette.FogCliffs, SurfaceLuminance * DarkerThanSurface);

        // ══ 계산 ════════════════════════════════════════════════

        /// <summary>이 거리의 빛이 얼마나 남아 도착하는가. 물속 밀도로 잰다.</summary>
        public static float Transmittance(float distance) =>
            DepthFog.Transmittance(Density, distance);

        /// <summary>
        /// <b>물속에서 랜턴이 실제로 보이게 하는 거리(m).</b>
        ///
        /// 둘 중 짧은 쪽이다 — 빛이 닿는 거리(<see cref="LanternRule.ForwardReachForTier"/>)와
        /// 안개를 뚫고 살아남는 거리. 물속에서는 뒤엣것이 이기므로 유효 반경이
        /// 지상보다 짧아진다. <b>그것이 자연스러운 쪽이다.</b>
        /// </summary>
        public static float LanternRangeUnderwater(int tier)
        {
            float reach = LanternRule.ForwardReachForTier(tier);
            if (reach <= 0f) return 0f;

            float throughFog = Mathf.Sqrt(-Mathf.Log(VisibleTransmittanceFloor)) / Density;
            return Mathf.Min(reach, throughFog);
        }

        /// <summary>지상에서의 같은 값. 견줄 짝이다.</summary>
        public static float LanternRangeOnLand(int tier) => LanternRule.ForwardReachForTier(tier);

        // ══ 색 다루기 ═══════════════════════════════════════════

        /// <summary>Rec.709 휘도. 픽셀 실측이 쓰는 것과 같은 식이다.</summary>
        public static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        /// <summary>색조는 그대로 두고 휘도만 이 값으로. 원색이 검정이면 검정이다.</summary>
        public static Color WithLuminance(Color c, float target)
        {
            float now = Luminance(c);
            if (now <= 0.0001f) return new Color(0f, 0f, 0f, c.a);

            float k = Mathf.Max(0f, target) / now;
            return new Color(c.r * k, c.g * k, c.b * k, c.a);
        }
    }
}
