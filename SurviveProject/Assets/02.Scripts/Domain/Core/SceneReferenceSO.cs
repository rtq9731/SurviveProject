using UnityEngine;

namespace Survive.Core
{
    /// <summary>
    /// 씬 이름을 문자열로 흩뿌리지 않기 위한 에셋.
    /// </summary>
    [CreateAssetMenu(menuName = "Survive/Core/Scene Reference")]
    public class SceneReferenceSO : ScriptableObject
    {
        [Tooltip("Build Settings에 등록된 씬 이름")]
        public string sceneName;

        public string displayName;
    }
}
