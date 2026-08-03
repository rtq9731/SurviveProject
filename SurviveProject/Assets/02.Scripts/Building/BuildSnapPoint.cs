using UnityEngine;

namespace Survive.Building
{
    /// <summary>
    /// "여기에 이런 조각을 붙일 수 있다"를 표시하는 지점.
    ///
    /// 이 트랜스폼의 위치와 회전이 <b>새 조각의 루트가 놓일 자세 그대로</b>다.
    /// 오프셋을 계산으로 만들지 않고 자세를 직접 들고 있게 한 이유:
    /// 계산으로 만들면 조각을 하나 더할 때마다 그 계산을 고쳐야 하는데,
    /// 자리에 자세를 박아 두면 프리팹만 손보면 된다.
    ///
    /// 눈으로 확인할 수 있어야 해서 기즈모를 그린다 — 모듈 건축은
    /// 어긋난 1도가 나중에 벽 한 줄을 어긋나게 만든다.
    /// </summary>
    [DisallowMultipleComponent]
    public class BuildSnapPoint : MonoBehaviour
    {
        [Tooltip("이 자리가 받아 주는 조각 종류")]
        [SerializeField] BuildPieceKind accepts = BuildPieceKind.None;

        public BuildPieceKind Accepts => accepts;

        public bool Takes(BuildPieceKind kind) => kind != BuildPieceKind.None && (accepts & kind) != 0;

        public void Setup(BuildPieceKind kinds) => accepts = kinds;

        void OnEnable() => SnapGraph.Register(this);
        void OnDisable() => SnapGraph.Unregister(this);

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.25f);
            Gizmos.DrawRay(transform.position, transform.forward * 0.8f);
        }
#endif
    }
}
