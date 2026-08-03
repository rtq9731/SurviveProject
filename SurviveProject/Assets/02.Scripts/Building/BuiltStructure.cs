using UnityEngine;

namespace Survive.Building
{
    /// <summary>
    /// 플레이어가 세운 것임을 표시한다.
    ///
    /// 태그나 레이어 대신 컴포넌트를 쓴 이유: 무엇으로 지었는지(정의)를 같이
    /// 들고 있어야 나중에 철거 환급과 저장 복원을 할 수 있다.
    /// </summary>
    [DisallowMultipleComponent]
    public class BuiltStructure : MonoBehaviour
    {
        [SerializeField] BuildableSO definition;

        public BuildableSO Definition => definition;

        public void Setup(BuildableSO d) => definition = d;
    }
}
