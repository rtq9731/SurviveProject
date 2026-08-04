using System.Linq;
using UnityEngine;

namespace Survive.Testing
{
    /// <summary>
    /// 씬에서 물을 찾아 준다.
    ///
    /// 수면은 맵 전체를 덮는 한 장이고 콜라이더가 없다. 레이캐스트로는 수면을
    /// 찾을 수 없어서 렌더러 경계에서 읽는다 — <see cref="Survive.World.WaterBody"/>가
    /// 하는 것과 같은 방식이다.
    ///
    /// 수영 시나리오 둘이 같은 것을 필요로 하므로 여기 모아 둔다.
    /// 각자 찾게 두면 한쪽만 고치고 다른 쪽은 낡는다.
    /// </summary>
    public static class E2EWaterProbe
    {
        /// <summary>수면 높이. 물을 못 찾으면 false.</summary>
        public static bool TryFindSurface(out float surfaceY, out GameObject water)
        {
            surfaceY = 0f;
            water = null;

            var body = Object.FindAnyObjectByType<Survive.World.WaterBody>(FindObjectsInactive.Exclude);
            if (body != null)
            {
                water = body.gameObject;
                surfaceY = body.SurfaceY;
                return true;
            }

            var rend = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
                             .FirstOrDefault(r => r.gameObject.name.Contains("Water"));
            if (rend == null) return false;

            water = rend.gameObject;
            surfaceY = rend.bounds.max.y;
            return true;
        }

        /// <summary>이 지점의 물 밑바닥 높이. 못 찾으면 NaN.</summary>
        public static float BedAt(float x, float z, float surfaceY)
        {
            if (!Physics.Raycast(new Vector3(x, surfaceY + 30f, z), Vector3.down, out var hit, 90f,
                                 ~0, QueryTriggerInteraction.Ignore))
                return float.NaN;
            return hit.point.y;
        }

        /// <summary>
        /// 주변에서 가장 깊은 곳. 발을 수면 바로 아래에 두면 머리는 물 위에 남으므로,
        /// 잠기는 것을 보려면 실제로 깊은 자리를 찾아야 한다.
        /// </summary>
        public static bool TryFindDeepest(Vector3 center, float surfaceY, float scan,
                                          out Vector3 bed, out float depth)
        {
            bed = Vector3.zero;
            depth = 0f;

            for (float dx = -scan; dx <= scan; dx += 3f)
            for (float dz = -scan; dz <= scan; dz += 3f)
            {
                float y = BedAt(center.x + dx, center.z + dz, surfaceY);
                if (float.IsNaN(y)) continue;

                float d = surfaceY - y;
                if (d > depth) { depth = d; bed = new Vector3(center.x + dx, y, center.z + dz); }
            }
            return depth > 0.1f;
        }

        /// <summary>
        /// 걸어서 물에 들어갈 수 있는 물가.
        ///
        /// 뭍에 서서 한 방향으로 걸으면 얕은 물을 지나 헤엄칠 깊이에 닿는 자리를 찾는다.
        /// 낭떠러지처럼 뚝 떨어지는 곳을 고르면 "걸어 들어간다"가 "떨어진다"가 된다.
        /// </summary>
        public static bool TryFindShore(Vector3 center, float surfaceY, float scan,
                                        out Vector3 landSpot, out Vector3 towardWater, out float depth)
        {
            landSpot = Vector3.zero;
            towardWater = Vector3.forward;
            depth = 0f;

            float bestCost = float.MaxValue;

            for (float dx = -scan; dx <= scan; dx += 2f)
            for (float dz = -scan; dz <= scan; dz += 2f)
            {
                float x = center.x + dx, z = center.z + dz;
                float land = BedAt(x, z, surfaceY);

                // 뭍이어야 한다. 수면보다 확실히 위여야 "들어간다"가 성립한다.
                if (float.IsNaN(land) || land < surfaceY + 0.4f || land > surfaceY + 3f) continue;

                for (int a = 0; a < 8; a++)
                {
                    var dir = Quaternion.Euler(0f, a * 45f, 0f) * Vector3.forward;

                    float near = BedAt(x + dir.x * 3f, z + dir.z * 3f, surfaceY);
                    float mid = BedAt(x + dir.x * 7f, z + dir.z * 7f, surfaceY);
                    float far = BedAt(x + dir.x * 11f, z + dir.z * 11f, surfaceY);
                    if (float.IsNaN(near) || float.IsNaN(mid) || float.IsNaN(far)) continue;

                    // 얕은 데를 거쳐 깊어져야 한다. 한 번에 떨어지면 물가가 아니라 절벽이다.
                    if (near > surfaceY - 0.2f) continue;
                    if (land - near > 2.5f) continue;
                    if (mid > surfaceY - 2.2f) continue;
                    if (far > surfaceY - 2.2f) continue;

                    // 가까울수록, 그리고 뭍이 낮을수록(기어 나오기 쉬울수록) 좋다
                    float cost = Vector2.Distance(new Vector2(x, z), new Vector2(center.x, center.z))
                               + (land - surfaceY) * 4f;
                    if (cost >= bestCost) continue;

                    bestCost = cost;
                    landSpot = new Vector3(x, land, z);
                    towardWater = dir;
                    depth = surfaceY - Mathf.Min(mid, far);
                }
            }
            return bestCost < float.MaxValue;
        }
    }
}
