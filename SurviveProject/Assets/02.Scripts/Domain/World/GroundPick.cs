using System.Collections.Generic;

namespace Survive.World
{
    /// <summary>아래로 쏜 광선이 맞은 것 하나. 높이와 「내 몸인가」만 남긴다.</summary>
    public readonly struct GroundProbeHit
    {
        /// <summary>광선 시작점에서의 거리. 작을수록 위쪽에서 맞았다.</summary>
        public readonly float Distance;

        /// <summary>맞은 점의 높이.</summary>
        public readonly float Y;

        /// <summary>발밑을 재는 그 사람의 몸인가.</summary>
        public readonly bool Mine;

        public GroundProbeHit(float distance, float y, bool mine)
        {
            Distance = distance;
            Y = y;
            Mine = mine;
        }
    }

    /// <summary>
    /// 발밑 고르기 — <b>자기 몸은 지면이 아니다.</b>
    ///
    /// <b>이 규칙이 없어서 무슨 일이 벌어졌는가.</b> 어떤 자리에 사람을 세우려면
    /// 그 자리의 지면 높이를 알아야 하고, 그것은 위에서 아래로 광선을 쏴서 잰다.
    /// 그런데 <b>이미 거기 서 있는 사람</b>의 발밑을 재면 광선이 지면보다 먼저
    /// 자기 몸통을 맞는다. 그 높이를 지면으로 알고 그 위에 다시 세우면 사람이
    /// 자기 키만큼 올라가고, 같은 호출을 되풀이할수록 계속 올라간다 —
    /// E2E 하네스의 <c>StandInFrontOf</c>를 제자리에서 두 번 불러 실측 5.2m까지
    /// 떠오른 적이 있다. 여러 라운드가 이 증상을 「지형 결함」으로 적고 우회했다.
    ///
    /// 고치는 자리는 광선이 아니라 <b>맞은 것을 고르는 규칙</b>이다.
    /// 그래서 이 판단만 떼어 여기에 둔다 — 물리 없이 검사할 수 있다.
    /// </summary>
    public static class GroundPick
    {
        /// <summary>
        /// 발밑 높이를 고른다. <b>내 몸에 맞은 것은 빼고</b> 가장 위에서 맞은 것을 쓴다.
        /// </summary>
        /// <returns>쓸 만한 것이 하나도 없으면 false.</returns>
        public static bool TryPick(IReadOnlyList<GroundProbeHit> hits, out float groundY)
        {
            groundY = 0f;
            if (hits == null) return false;

            bool found = false;
            float nearest = float.MaxValue;

            for (int i = 0; i < hits.Count; i++)
            {
                var hit = hits[i];
                if (hit.Mine) continue;               // 자기 몸은 지면이 아니다
                if (hit.Distance >= nearest) continue;

                nearest = hit.Distance;
                groundY = hit.Y;
                found = true;
            }
            return found;
        }

        /// <summary>
        /// 발밑에 세울 때의 몸 중심 높이. 같은 자리에서 다시 불러도 <b>같은 값</b>이어야 한다.
        /// 그 성질이 곧 「제자리에서 두 번 불러도 안 뜬다」이다.
        /// </summary>
        public static bool TryStandY(IReadOnlyList<GroundProbeHit> hits, float footLift,
                                     out float standY)
        {
            standY = 0f;
            if (!TryPick(hits, out float groundY)) return false;

            standY = groundY + footLift;
            return true;
        }
    }
}
