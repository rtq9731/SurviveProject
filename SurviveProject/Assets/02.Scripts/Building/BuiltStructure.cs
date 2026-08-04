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

        public void Setup(BuildableSO d)
        {
            definition = d;

            // 세우는 그 순간 경로에도 등록한다. 다음 프레임까지 미루면 그 사이에
            // 생물이 방금 세운 벽을 지나는 경로를 그대로 들고 달린다.
            StructureNavObstacle.Attach(gameObject);
        }

        /// <summary>
        /// 씬에 미리 놓인 건축물을 챙긴다. 이쪽은 <see cref="Setup"/>을 거치지 않는다.
        ///
        /// definition이 비어 있으면 손대지 않는다. 건설 미리보기(고스트)가 바로
        /// 그 상태다 — 프리팹의 definition은 비어 있고 BuildPlacer가 세울 때만 채운다.
        /// 유령이 NavMesh를 도려내면 아직 짓지도 않은 벽이 길을 막는다.
        /// </summary>
        void Start()
        {
            if (definition != null) StructureNavObstacle.Attach(gameObject);
        }
    }
}
