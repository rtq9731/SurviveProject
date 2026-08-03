using System.Collections.Generic;
using UnityEngine;

namespace Survive.Building
{
    /// <summary>
    /// 세워진 조각들이 내놓은 붙일 자리 모음.
    ///
    /// 매번 <c>FindObjectsByType</c>로 긁으면 조각 수에 비례해 프레임마다
    /// 씬 전체를 훑는다. 미리보기는 매 프레임 돌아가므로 그건 곧 체감된다.
    /// 등록/해제로 목록을 들고 있다가 조준점 주변만 본다.
    ///
    /// 공간 분할까지는 하지 않는다. 조각 수백 개까지는 선형 탐색이 더 싸고,
    /// 그 이상이 되면 그때 격자를 넣으면 된다 — 지금 넣으면 검증할 수 없는
    /// 복잡도만 는다.
    /// </summary>
    public static class SnapGraph
    {
        static readonly List<BuildSnapPoint> _points = new List<BuildSnapPoint>();

        public static int Count => _points.Count;

        public static void Register(BuildSnapPoint p)
        {
            if (p == null || _points.Contains(p)) return;
            _points.Add(p);
        }

        public static void Unregister(BuildSnapPoint p) => _points.Remove(p);

        /// <summary>
        /// 조준점에서 가장 가까운, <paramref name="kind"/>를 받아 주는 자리.
        /// 없으면 false.
        /// </summary>
        public static bool TryFindNearest(Vector3 aim, BuildPieceKind kind, float radius,
                                          out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            float bestSqr = radius * radius;
            BuildSnapPoint best = null;

            for (int i = _points.Count - 1; i >= 0; i--)
            {
                var p = _points[i];

                // 조각이 부서지면 자리도 함께 사라진다. OnDisable을 못 타는
                // 경우가 있어 여기서도 걸러 낸다.
                if (p == null) { _points.RemoveAt(i); continue; }
                if (!p.Takes(kind)) continue;

                float sqr = (p.transform.position - aim).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = p;
            }

            if (best == null) return false;

            position = best.transform.position;
            rotation = best.transform.rotation;
            return true;
        }

        /// <summary>씬을 갈아 끼울 때 남은 참조를 털어 낸다.</summary>
        public static void Clear() => _points.Clear();
    }
}
