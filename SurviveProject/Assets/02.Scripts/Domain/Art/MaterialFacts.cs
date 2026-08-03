using UnityEngine;

namespace Survive.Domain.Art
{
    /// <summary>
    /// 머티리얼 하나에서 뽑아낸 검사 대상 값들.
    /// 에디터 API에 의존하지 않으므로 테스트에서 손으로 만들 수 있다.
    /// </summary>
    public readonly struct MaterialFacts
    {
        public string AssetPath { get; }
        public string ShaderName { get; }
        public float Smoothness { get; }
        public float Metallic { get; }
        public bool HasEmission { get; }
        public Color EmissionColor { get; }

        public MaterialFacts(string assetPath, string shaderName, float smoothness,
                             float metallic, bool hasEmission, Color emissionColor)
        {
            AssetPath = assetPath;
            ShaderName = shaderName;
            Smoothness = smoothness;
            Metallic = metallic;
            HasEmission = hasEmission;
            EmissionColor = emissionColor;
        }
    }
}
