using UnityEngine;
using UnityEngine.AI;

namespace Survive.Building
{
    /// <summary>
    /// 세운 건축물을 NavMesh 경로 계산에 등록한다.
    ///
    /// 생물의 몸은 CreatureCollisionGuard가 벽에서 멈춰 세우지만, NavMeshAgent가
    /// 그리는 <b>경로</b>는 여전히 벽을 그대로 관통한다. 그래서 생물이 벽에 코를 박고
    /// 밀거나 모서리로 미끄러진다. 경로 쪽에서 벽을 보이게 하려면 NavMeshObstacle의
    /// carving이 필요하다.
    ///
    /// 콜라이더 하나마다 장애물을 하나씩 붙인다. 오브젝트 전체를 한 상자로 덮으면
    /// 문간(Piece_Doorway)의 빈 가운데까지 막혀 문이 벽이 된다 — 기둥과 옆판에만
    /// 붙여야 사이로 지나갈 수 있다.
    ///
    /// carving은 NavMesh를 실제로 도려내므로 공짜가 아니다. 건축물은 세운 뒤
    /// 움직이지 않으므로 <see cref="NavMeshObstacle.carveOnlyStationary"/>를 켜서
    /// 한 번만 도려내고 그대로 둔다.
    /// </summary>
    public static class StructureNavObstacle
    {
        /// <summary>
        /// 아래 지면에서 이만큼 떠 있는 덩어리는 밑으로 지나갈 수 있다고 본다.
        ///
        /// carving은 높이를 보지 않고 NavMesh에 수직으로 찍어 내린다. 이 걸러내기가
        /// 없으면 문간의 상인방(바닥에서 2.2m)이 문 아래 바닥까지 도려내 문을 막는다.
        /// 2층 바닥도 마찬가지로 1층을 통째로 지운다.
        /// </summary>
        public const float PassUnderHeight = 2f;

        /// <summary>
        /// 이 오브젝트의 단단한 콜라이더마다 장애물을 붙인다. 붙인 개수를 돌려준다.
        /// 여러 번 불러도 안전하다 — 이미 붙은 곳은 건너뛴다.
        /// </summary>
        public static int Attach(GameObject root)
        {
            if (root == null) return 0;

            int added = 0;

            foreach (var col in root.GetComponentsInChildren<Collider>(true))
            {
                if (col == null || !col.enabled || col.isTrigger) continue;

                // 트리거가 아닌 콜라이더가 한 오브젝트에 둘 이상이면 첫 하나만 쓴다.
                // NavMeshObstacle은 오브젝트당 하나뿐이라 나눌 방법이 없다.
                if (col.GetComponent<NavMeshObstacle>() != null) continue;

                if (PassesUnder(col)) continue;

                var obstacle = col.gameObject.AddComponent<NavMeshObstacle>();
                ShapeLike(col, obstacle);

                obstacle.carving = true;
                obstacle.carveOnlyStationary = true;
                added++;
            }

            return added;
        }

        /// <summary>
        /// 이 덩어리 밑으로 생물이 지나갈 수 있는가.
        ///
        /// 묻는 방식이 곧 답이다 — "이 콜라이더의 밑동에서 <see cref="PassUnderHeight"/>
        /// 안에 걸어다닐 수 있는 바닥이 있는가". 있으면 그 바닥을 도려내야 하고,
        /// 없으면 도려낼 것도 없다.
        ///
        /// 훑는 거리를 딱 그 높이로 묶는 것이 핵심이다. 넉넉하게 잡으면
        /// <see cref="NavMesh.SamplePosition"/>이 <b>가장 가까운</b> 지점을 주므로
        /// 동굴 안에서는 10m 위 천장의 NavMesh를 집어 와, 상인방이 자기 아래
        /// 바닥보다 낮다는 답이 나온다. 지면을 아래로 쏜 레이로 찾는 방법도 안 된다 —
        /// 건축물의 밑동은 대개 지형에 조금 파묻혀 있어 레이가 콜라이더 안에서
        /// 출발하고, 그러면 지면을 통째로 놓친다.
        /// </summary>
        static bool PassesUnder(Collider col)
        {
            var bounds = col.bounds;
            var foot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

            return !NavMesh.SamplePosition(foot, out _, PassUnderHeight, NavMesh.AllAreas);
        }

        /// <summary>장애물 상자를 콜라이더 모양에 맞춘다.</summary>
        static void ShapeLike(Collider col, NavMeshObstacle obstacle)
        {
            obstacle.shape = NavMeshObstacleShape.Box;

            // 상자는 그대로 옮겨 담으면 정확하다. 이 프로젝트의 건축물은 전부 상자다.
            if (col is BoxCollider box)
            {
                obstacle.center = box.center;
                obstacle.size = box.size;
                return;
            }

            // 그 밖의 모양은 월드 AABB로 근사한다. 장애물의 size는 로컬 값이라
            // 스케일을 되돌려 놔야 결과가 AABB와 같아진다.
            var bounds = col.bounds;
            var t = col.transform;
            var scale = t.lossyScale;

            obstacle.center = t.InverseTransformPoint(bounds.center);
            obstacle.size = new Vector3(bounds.size.x / SafeScale(scale.x),
                                        bounds.size.y / SafeScale(scale.y),
                                        bounds.size.z / SafeScale(scale.z));
        }

        static float SafeScale(float v) => Mathf.Max(Mathf.Abs(v), 0.0001f);
    }
}
