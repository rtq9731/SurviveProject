using System;
using UnityEngine;

namespace Survive.Narrative
{
    /// <summary>
    /// 자막 시퀀스. 대사를 프리팹에 하드코딩하지 않고 에셋으로 뺀다.
    /// </summary>
    [CreateAssetMenu(menuName = "Survive/Narrative/Sequence")]
    public class SequenceSO : ScriptableObject
    {
        [Serializable]
        public class Line
        {
            [Tooltip("화자. 비우면 표시하지 않는다")]
            public string speaker;

            [TextArea(2, 5)] public string text;

            [Tooltip("이 줄을 유지할 시간(초)")]
            [Min(0.1f)] public float holdSeconds = 3f;
        }

        public string id;
        public Line[] lines = new Line[0];

        [Tooltip("재생 중 플레이어 조작을 잠글지")]
        public bool lockInput = true;

        [Min(0f)] public float fadeSeconds = 0.3f;
    }
}
