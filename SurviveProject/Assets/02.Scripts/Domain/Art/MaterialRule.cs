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
