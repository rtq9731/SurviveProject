using UnityEngine;

namespace Survive.Building
{
    /// <summary>
    /// 씬이 공유하는 붙일 자리 그래프 하나.
    ///
    /// 탐색과 목록 관리는 전부 <see cref="SnapPointGraph"/>(Domain)로 옮겼다.
    /// 여기 남은 것은 "씬 전체가 하나를 같이 쓴다"는 사실뿐이다 —
    /// <c>BuildSnapPoint</c>는 <c>OnEnable</c>에서 자기를 등록해야 하는데
    /// 그 시점에 인스턴스를 건네줄 곳이 없기 때문이다.
    /// </summary>
    public static class SnapGraph
    {
        static readonly SnapPointGraph _graph = new SnapPointGraph();

        public static int Count => _graph.Count;

        public static void Register(BuildSnapPoint p) => _graph.Register(p);

        public static void Unregister(BuildSnapPoint p) => _graph.Unregister(p);

        /// <summary>
        /// 조준점에서 가장 가까운, <paramref name="kind"/>를 받아 주는 자리.
        /// 없으면 false.
        /// </summary>
        public static bool TryFindNearest(Vector3 aim, BuildPieceKind kind, float radius,
                                          out Vector3 position, out Quaternion rotation)
            => _graph.TryFindNearest(aim, kind, radius, out position, out rotation);

        /// <summary>씬을 갈아 끼울 때 남은 참조를 털어 낸다.</summary>
        public static void Clear() => _graph.Clear();
    }
}
