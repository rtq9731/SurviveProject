using UnityEngine;

namespace Survive.Creatures
{
    /// <summary>
    /// 이동을 벽에 맞춰 깎는 산수. 벡터뿐이라 씬 없이 확인할 수 있다.
    ///
    /// 생물의 <b>판단</b>은 <see cref="CreatureDecision"/>이 하고, 판단이 정한 곳으로
    /// 가려던 이동량을 벽이 가로막았을 때 얼마나 갈 수 있는지만 여기서 정한다.
    /// 어디로 갈지는 바뀌지 않는다 — 얼마나 갈 수 있는지만 바뀐다.
    /// </summary>
    public static class CreatureCollision
    {
        /// <summary>벽에서 띄워 둘 여유. 다음 프레임 훑기가 벽 안에서 시작하지 않게 한다.</summary>
        public const float SkinWidth = 0.05f;

        /// <summary>이 높이까지의 턱은 넘어간다. 자갈 하나에 걸려 배회가 멈추지 않도록.</summary>
        public const float StepOffset = 0.3f;

        /// <summary>법선의 y가 이보다 크면 밟을 수 있는 바닥으로 본다(약 60도).</summary>
        public const float WalkableNormalY = 0.5f;

        /// <summary>한 프레임에 이보다 멀리 갔으면 걸어간 것이 아니라 옮겨진 것으로 본다.</summary>
        public const float TeleportDistance = 5f;

        /// <summary>
        /// 한 프레임에 이만큼 움직였다면 걸어간 것이 아니라 옮겨진 것이다.
        /// (E2E 하네스의 워프, NavMesh 재배치 등) 이럴 때 벽 판정을 하면
        /// 출발점과 도착점 사이의 지형을 전부 벽으로 오해한다.
        /// </summary>
        public static bool IsTeleport(float distance, float threshold) => distance > threshold;

        /// <summary>
        /// 이 면은 밟고 올라갈 수 있는 바닥인가. 경사면·둔덕은 막지 않는다 —
        /// 막으면 NavMesh가 걸어도 된다고 한 언덕에서 생물이 붙박인다.
        /// 벽으로 보는 것은 <paramref name="walkableNormalY"/>보다 가파른 면뿐이다.
        /// </summary>
        public static bool IsWalkable(float normalY, float walkableNormalY) => normalY >= walkableNormalY;

        /// <summary>
        /// 벽에 닿기 <paramref name="skin"/>만큼 앞에서 멈춘 자리.
        /// 살갗만큼 띄우지 않으면 다음 프레임에 스윕이 벽 안에서 시작해 판정이 무너진다.
        /// 이미 벽에 붙어 있었다면(닿은 거리가 살갗보다 짧다면) 제자리다.
        /// </summary>
        public static Vector3 StopShort(Vector3 from, Vector3 direction, float hitDistance, float skin) =>
            from + direction * Mathf.Max(0f, hitDistance - skin);

        /// <summary>
        /// 벽에 막히고 남은 이동량을 벽면을 따라 미끄러뜨린다.
        /// 그래서 생물이 벽 앞에서 떨지 않고 벽을 끼고 돌아간다.
        ///
        /// 법선의 수평 성분만 쓴다. 천장·바닥의 법선(수직)으로 미끄러뜨리면
        /// 수평 이동이 그대로 통과해 버린다 — 그때는 갈 곳이 없는 것으로 본다.
        /// </summary>
        public static Vector3 SlideAlong(Vector3 motion, Vector3 wallNormal)
        {
            var planarNormal = new Vector3(wallNormal.x, 0f, wallNormal.z);
            if (planarNormal.sqrMagnitude < 1e-8f) return Vector3.zero;

            planarNormal.Normalize();
            var slid = Vector3.ProjectOnPlane(new Vector3(motion.x, 0f, motion.z), planarNormal);
            slid.y = 0f;
            return slid;
        }
    }
}
