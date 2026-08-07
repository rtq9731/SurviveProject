using System.Collections.Generic;
using UnityEngine;

namespace Survive.Domain.Art
{
    /// <summary>
    /// 팩 출처가 섞인 머티리얼을 한 세계로 묶기 위한 판정.
    ///
    /// 세 가지만 본다.
    ///   1) 셰이더가 허용 목록 안인가
    ///   2) 스무스니스가 3단 중 하나인가
    ///   3) 발광색이 광원 4색(+하이라이트) 안인가
    ///
    /// Metallic은 판정하지 않는다. "기계와 인공 구조물에만"이라는 규칙은
    /// 자동 분류가 불가능하므로, 도구가 목록만 뽑아 사람이 본다.
    /// </summary>
    public static class MaterialRule
    {
        public const float SmoothnessMatte = 0.1f;
        public const float SmoothnessSemi = 0.35f;
        public const float SmoothnessMetal = 0.6f;
        public const float SmoothnessTolerance = 0.02f;

        /// <summary>발광색 비교 허용 오차(채널당). 정규화한 색끼리 비교한다.</summary>
        public const float EmissionChannelTolerance = 0.04f;

        static readonly string[] AllowedShaders =
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Simple Lit",
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Particles/Unlit",
            "Universal Render Pipeline/Terrain/Lit",
            "TextMeshPro/Distance Field",
            "TextMeshPro/Mobile/Distance Field",
            "Skybox/Procedural",

            // 아래는 URP/Lit으로 대체할 수 없는 정당한 예외다. 치환하면 기능 자체가 사라진다.
            // 실제 프로젝트 머티리얼이 물고 있는 셰이더 이름을 직접 확인하고 적었다
            // (에디터에서 값만 확인한 이름을 추측으로 적지 않는다).

            // Stylized Water For URP 패키지가 실제로 물고 있는 셰이더 이름(폴더명과 다르다).
            // 굴절·깊이 기반 색 변화(얕은 물/깊은 물)를 셰이더가 직접 계산하고,
            // _BaseColor/_BaseMap 같은 Lit 호환 프로퍼티가 아예 없어 옮길 값도 없다.
            "Stylized Water",
            // IgniteCoders Simple Water Shader(Shader Graph). 위와 마찬가지로 굴절·반사를
            // 셰이더 그래프에서 계산하며, 표준 컬러/텍스처 프로퍼티(_BaseColor 등)조차 없다 —
            // Lit으로 옮길 속성이 없다.
            "Shader Graphs/WaterShader",
            // SpeedTree7(폴리퍼펙트 나무·식생). 바람 흔들림 애니메이션과 거리별
            // 빌보드 크로스페이드를 셰이더가 담당한다 — Lit로 바꾸면 두 기능이 모두 사라진다.
            "Universal Render Pipeline/Nature/SpeedTree7",
            // 빛기둥(LightShaft) 전용 손작성 셰이더(Assets/03.Materials/LightShaftBeam.shader).
            // URP/Lit은 거리 안개를 강제로 적용하며 끌 방법이 없다 — 화면을 가득 채우는
            // 반투명 빛기둥이 안개색(#0C0F15, 거의 검정)으로 당겨져 랜드마크로 보여야 할
            // 먼 거리에서 정확히 사라지는 원인이었다. 이 셰이더는 안개 매크로를 아예 쓰지
            // 않으므로, 이 셰이더로만 그 결함을 없앨 수 있다.
            "Survive/LightShaft",
            // 무광버섯(Assets/03.Materials/MatteRim.shader). URP/Lit에는 프레넬 항을
            // 노출하는 프로퍼티가 없다 — 램버트 확산과 GGX 반사뿐이고, 무광 밴드(0.1)에서
            // GGX 로브는 완전히 퍼져 실루엣 테두리에 아무것도 남기지 않는다. 이 물건에
            // 필요한 것은 정확히 그 반대다: 면은 배경과 같은 검정으로 두고 시선에 스치는
            // 테두리에만 받은 빛을 튕기는 것(검토회신 ⑤). 에미션 맵으로 흉내 내면 UV에
            // 고정되어 카메라가 돌 때 따라오지 않는다 — 실루엣은 시점이 정한다.
            "Survive/MatteRim",
            // 하늘(Assets/03.Materials/Sky.shader). Skybox/Procedural은 지구의 레일리
            // 산란을 제 안에 박아 두고 태양 각도에서 하늘색을 스스로 계산한다 —
            // 이 세계의 규칙(「무엇이 얼마나 두껍게 사이에 있는가」, §7.4)을 받을
            // 창구가 없고, 받지 못하면 지평선의 안개색과 지평선의 하늘색이 서로 다른
            // 색이 되어 땅과 하늘이 만나는 선에 이음매가 보인다. 이 셰이더는 규칙을
            // 제 안에 두지 않고 DepthFog가 구워 준 표(_Coverage)와 색(_HorizonColor)을
            // 받아 곱하기만 하므로, 규칙은 여전히 Domain 한 곳에 있다.
            "Survive/Sky",
        };

        public static bool IsAllowedShader(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName)) return false;
            foreach (var s in AllowedShaders)
                if (s == shaderName) return true;
            return false;
        }

        public static bool IsBandedSmoothness(float v)
            => Mathf.Abs(v - SmoothnessMatte) <= SmoothnessTolerance
            || Mathf.Abs(v - SmoothnessSemi) <= SmoothnessTolerance
            || Mathf.Abs(v - SmoothnessMetal) <= SmoothnessTolerance;

        /// <summary>
        /// HDR 발광은 강도가 1을 넘으므로 밝기가 아니라 색으로 본다.
        /// 가장 밝은 채널을 1로 맞춘 뒤 비교한다. 이 정규화·비교 로직은
        /// LightRule도 그대로 쓴다 — EmissionPaletteMatch 참조.
        /// </summary>
        public static bool IsAllowedEmission(Color c)
            => EmissionPaletteMatch.IsAllowed(c, EmissionChannelTolerance);

        /// <summary>위반을 전부 모아 돌려준다. 하나만 보고하고 멈추지 않는다.</summary>
        public static IReadOnlyList<string> Violations(in MaterialFacts m)
        {
            var list = new List<string>();

            if (!IsAllowedShader(m.ShaderName))
                list.Add($"허용 목록 밖 셰이더: '{m.ShaderName}'");

            if (!IsBandedSmoothness(m.Smoothness))
                list.Add($"스무스니스가 3단 밖: {m.Smoothness:0.###} " +
                         $"(허용 {SmoothnessMatte} / {SmoothnessSemi} / {SmoothnessMetal})");

            if (m.HasEmission && !IsAllowedEmission(m.EmissionColor))
                list.Add($"광원 4색 밖 발광: {ColorUtility.ToHtmlStringRGB(EmissionPaletteMatch.Normalize(m.EmissionColor))}");

            return list;
        }
    }
}
