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
            "Stylized Water For URP/Water",
            "TextMeshPro/Distance Field",
            "TextMeshPro/Mobile/Distance Field",
            "Skybox/Procedural",
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
        /// 가장 밝은 채널을 1로 맞춘 뒤 비교한다.
        /// </summary>
        public static bool IsAllowedEmission(Color c)
        {
            var a = Normalize(c);
            foreach (var allowed in ArtPalette.AllowedEmission)
            {
                var b = Normalize(allowed);
                if (Mathf.Abs(a.r - b.r) <= EmissionChannelTolerance
                 && Mathf.Abs(a.g - b.g) <= EmissionChannelTolerance
                 && Mathf.Abs(a.b - b.b) <= EmissionChannelTolerance)
                    return true;
            }
            return false;
        }

        static Color Normalize(Color c)
        {
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (max <= 0.0001f) return Color.black;
            return new Color(c.r / max, c.g / max, c.b / max, 1f);
        }

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
                list.Add($"광원 4색 밖 발광: {ColorUtility.ToHtmlStringRGB(Normalize(m.EmissionColor))}");

            return list;
        }
    }
}
