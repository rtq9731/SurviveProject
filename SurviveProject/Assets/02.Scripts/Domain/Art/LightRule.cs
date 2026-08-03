using System.Collections.Generic;
using UnityEngine;

namespace Survive.Domain.Art
{
    /// <summary>
    /// 광원 4색 규칙(ArtPalette)을 실제 Light 컴포넌트에 적용한다.
    ///
    /// MaterialRule.IsAllowedEmission이 머티리얼의 Emission을 판정하는 것과
    /// 같은 자격으로, 이 클래스는 Light.color를 판정한다. 둘 다
    /// EmissionPaletteMatch 하나만 호출해 정규화 로직이 갈라지지 않게 한다.
    ///
    /// Directional 라이트(태양·전역 조명)는 색을 판정하지 않는다. §4의 광원
    /// 4색 규칙은 "지하에서 빛나는 것" — 랜드마크가 되는 개별 발광체(빛기둥·
    /// 버섯·랜턴·매크로늄)를 가리키는 규칙이지, 장면 전체를 비추는 방향광까지
    /// 포함하지 않는다. §5도 이를 뒷받침한다: 프롤로그(StartScene)는 태양이
    /// 정당하고, 지하(MainScene)의 전역광은 "출처가 있어야 한다"는 원칙의
    /// 대상이지 4색 규칙의 대상이 아니다. 그렇다고 씬 이름으로 면제하지는
    /// 않는다 — 씬 기준 면제는 그 씬의 다른 라이트(랜턴 등)까지 뚫어버리는
    /// 구멍이 된다. 타입 하나로만 좁게 면제한다. 지하 씬에 Directional
    /// 라이트가 있다는 것 자체는 이 규칙이 판정하지 않는 별개의 의심 신호이며,
    /// ArtRuleChecker가 사람이 볼 목록으로 따로 보고한다.
    ///
    /// 강도(HDR 세기)는 판정하지 않는다 — 색만 본다. 밝기 같은 튜닝값은
    /// 스크린샷을 보지 않고는 정할 수 없다는 것이 설계 문서 §3의 방침이다.
    /// </summary>
    public static class LightRule
    {
        /// <summary>색 규칙의 대상인 타입인가. Directional만 뺀다.</summary>
        public static bool IsColorCandidate(LightType type) => type != LightType.Directional;

        public static bool IsAllowedColor(Color c)
            => EmissionPaletteMatch.IsAllowed(c, MaterialRule.EmissionChannelTolerance);

        /// <summary>위반을 전부 모아 돌려준다. Directional은 애초에 대상이 아니라 항상 빈 목록이다.</summary>
        public static IReadOnlyList<string> Violations(in LightFacts f)
        {
            var list = new List<string>();
            if (!IsColorCandidate(f.Type)) return list;

            if (!IsAllowedColor(f.Color))
                list.Add($"광원 4색 밖 라이트: '{f.GameObjectName}' " +
                         $"{ColorUtility.ToHtmlStringRGB(EmissionPaletteMatch.Normalize(f.Color))} " +
                         $"(intensity {f.Intensity:0.##})");

            return list;
        }
    }
}
