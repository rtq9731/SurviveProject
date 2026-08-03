using UnityEngine;

namespace Survive.Domain.Art
{
    /// <summary>
    /// 색이 광원 4색 팔레트(ArtPalette.AllowedEmission) 안에 드는지 판정하는 공용 로직.
    ///
    /// 머티리얼의 Emission과 Light 컴포넌트의 색은 "무언가 빛난다"는 같은 현상을
    /// 서로 다른 컴포넌트로 표현한 것뿐이다. MaterialRule과 LightRule이 각자
    /// 정규화·비교 로직을 따로 두면 시간이 지나며 조용히 어긋난다 — 그래서 둘 다
    /// 이 클래스 하나만 호출한다.
    ///
    /// HDR 발광/라이트는 강도가 1을 넘으므로 밝기가 아니라 색으로 본다.
    /// 가장 밝은 채널을 1로 맞춘 뒤 비교한다.
    /// </summary>
    public static class EmissionPaletteMatch
    {
        public static Color Normalize(Color c)
        {
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (max <= 0.0001f) return Color.black;
            return new Color(c.r / max, c.g / max, c.b / max, 1f);
        }

        /// <summary>정규화한 색끼리 채널당 tolerance 이내면 팔레트 안으로 본다.</summary>
        public static bool IsAllowed(Color c, float tolerance)
        {
            var a = Normalize(c);
            foreach (var allowed in ArtPalette.AllowedEmission)
            {
                var b = Normalize(allowed);
                if (Mathf.Abs(a.r - b.r) <= tolerance
                 && Mathf.Abs(a.g - b.g) <= tolerance
                 && Mathf.Abs(a.b - b.b) <= tolerance)
                    return true;
            }
            return false;
        }
    }
}
