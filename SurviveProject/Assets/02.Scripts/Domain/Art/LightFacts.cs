using UnityEngine;

namespace Survive.Domain.Art
{
    /// <summary>
    /// 라이트 하나에서 뽑아낸 검사 대상 값들.
    /// 에디터 API에 의존하지 않으므로 테스트에서 손으로 만들 수 있다.
    /// </summary>
    public readonly struct LightFacts
    {
        /// <summary>씬 경로 또는 프리팹 경로. 보고서 한 줄에 쓴다.</summary>
        public string AssetPath { get; }

        /// <summary>GameObject 이름. 씬 안에서 어느 라이트인지 사람이 알아볼 유일한 단서다.</summary>
        public string GameObjectName { get; }

        public Color Color { get; }
        public LightType Type { get; }
        public float Intensity { get; }

        public LightFacts(string assetPath, string gameObjectName, Color color,
                          LightType type, float intensity)
        {
            AssetPath = assetPath;
            GameObjectName = gameObjectName;
            Color = color;
            Type = type;
            Intensity = intensity;
        }
    }
}
