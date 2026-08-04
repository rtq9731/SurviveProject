using System.Collections.Generic;
using UnityEngine;

namespace Survive.Building
{
    /// <summary>
    /// 그래프가 자리 하나에게 묻는 것 전부.
    ///
    /// <c>BuildSnapPoint</c>는 MonoBehaviour라 Domain에서 참조할 수 없다.
    /// 탐색이 실제로 필요로 하는 것은 "살아 있는가 / 이 종류를 받는가 /
    /// 어디에 어떤 자세로 놓이는가" 셋뿐이므로 그만큼만 인터페이스로 끊었다.
    ///
    /// <see cref="IsAlive"/>를 따로 둔 이유: 인터페이스 참조에는 UnityEngine.Object의
    /// <c>==</c> 오버로드가 걸리지 않는다. 파괴된 오브젝트를 <c>p == null</c>로
    /// 거르던 원래 동작을 유지하려면 구현체가 직접 답해야 한다.
    /// </summary>
    public interface ISnapPoint
    {
        /// <summary>조각이 부서져 사라졌으면 false.</summary>
        bool IsAlive { get; }

        /// <summary>이 자리가 <paramref name="kind"/>를 받아 주는가.</summary>
        bool Takes(BuildPieceKind kind);

        /// <summary>새 조각의 루트가 놓일 위치.</summary>
        Vector3 SnapPosition { get; }

        /// <summary>새 조각의 루트가 놓일 회전.</summary>
        Quaternion SnapRotation { get; }
    }

    /// <summary>
    /// 세워진 조각들이 내놓은 붙일 자리 모음. 순수 자료구조 부분.
    ///
    /// 매번 <c>FindObjectsByType</c>로 긁으면 조각 수에 비례해 프레임마다
    /// 씬 전체를 훑는다. 미리보기는 매 프레임 돌아가므로 그건 곧 체감된다.
    /// 등록/해제로 목록을 들고 있다가 조준점 주변만 본다.
    ///
    /// 공간 분할까지는 하지 않는다. 조각 수백 개까지는 선형 탐색이 더 싸고,
    /// 그 이상이 되면 그때 격자를 넣으면 된다 — 지금 넣으면 검증할 수 없는
    /// 복잡도만 는다.
    ///
    /// 정적 클래스가 아니라 인스턴스로 둔 이유: 테스트마다 새 그래프를 만들면
    /// 정적 상태 오염을 걱정할 필요가 없다. 씬에서 쓰는 하나짜리는
    /// <c>Survive.Building.SnapGraph</c>가 들고 있다.
    /// </summary>
    public sealed class SnapPointGraph
    {
        readonly List<ISnapPoint> _points = new List<ISnapPoint>();

        public int Count => _points.Count;

        public void Register(ISnapPoint p)
        {
            if (p == null || !p.IsAlive || _points.Contains(p)) return;
            _points.Add(p);
        }

        public void Unregister(ISnapPoint p)
        {
            if (p == null) return;
            _points.Remove(p);
        }

        /// <summary>
        /// 조준점에서 가장 가까운, <paramref name="kind"/>를 받아 주는 자리.
        /// 없으면 false.
        ///
        /// 반경은 <b>열린 구간</b>이다 — 거리가 정확히 <paramref name="radius"/>인
        /// 자리는 잡히지 않는다. 반경 0이면 아무것도 잡히지 않는다.
        /// </summary>
        public bool TryFindNearest(Vector3 aim, BuildPieceKind kind, float radius,
                                   out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            float bestSqr = radius * radius;
            ISnapPoint best = null;

            for (int i = _points.Count - 1; i >= 0; i--)
            {
                var p = _points[i];

                // 조각이 부서지면 자리도 함께 사라진다. OnDisable을 못 타는
                // 경우가 있어 여기서도 걸러 낸다.
                if (p == null || !p.IsAlive) { _points.RemoveAt(i); continue; }
                if (!p.Takes(kind)) continue;

                float sqr = (p.SnapPosition - aim).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = p;
            }

            if (best == null) return false;

            position = best.SnapPosition;
            rotation = best.SnapRotation;
            return true;
        }

        /// <summary>씬을 갈아 끼울 때 남은 참조를 털어 낸다.</summary>
        public void Clear() => _points.Clear();
    }
}

