using System.Collections.Generic;
using UnityEngine;

namespace Survive.World
{
    /// <summary>
    /// 부활 지점 후보 하나. 화톳불 하나를 좌표·불붙인 시각·지금 타고 있는가로 줄인 것이다.
    ///
    /// MonoBehaviour를 받지 않는 이유는 규칙을 Unity 없이 검증하기 위해서다.
    /// 후보를 모으는 일(씬에서 화톳불을 찾는 일)은 실행부의 몫이고,
    /// 그중 어디로 돌려보낼지는 여기서만 정한다.
    /// </summary>
    public readonly struct RespawnAnchor
    {
        /// <summary>돌아갈 자리. 화톳불이 서 있는 발밑 좌표다.</summary>
        public readonly Vector3 Position;

        /// <summary>마지막으로 <b>불붙인</b> 시각. 세운 시각이 아니라 불이 붙은 시각이다.</summary>
        public readonly float KindledAt;

        /// <summary>지금 실제로 타고 있는가. 연료가 떨어졌으면 false다.</summary>
        public readonly bool IsLit;

        public RespawnAnchor(Vector3 position, float kindledAt, bool isLit)
        {
            Position = position;
            KindledAt = kindledAt;
            IsLit = isLit;
        }
    }

    /// <summary>
    /// 죽은 사람을 어디로 돌려보낼지 정한다.
    ///
    /// 규칙은 한 줄이다 — <b>마지막으로 불붙인 화톳불, 없으면 시작 지점.</b>
    /// 다만 "있다"의 뜻을 정해야 한다. 여기서는 <b>지금 타고 있는</b> 화톳불만 센다.
    /// <see cref="LitZoneRegistry"/>가 이미 같은 선을 긋고 있고
    /// (연료가 떨어진 화톳불은 배치돼 있어도 밝은 구역에서 빠진다),
    /// <see cref="Survive.Building.Campfire"/> 자신도 "연료를 계속 넣어야 거점"이라는
    /// 전제 위에 서 있다. 꺼진 잿더미로 돌려보내면 어둠 한복판에 떨구는 것과 같다 —
    /// 거점의 값어치는 불이 살아 있는 동안에만 있다.
    ///
    /// "마지막"은 세운 순서가 아니라 <b>불붙인 순서</b>다. 오래전에 세워 두고 방금 다시
    /// 지핀 불이 사람이 마지막으로 머문 자리다.
    /// </summary>
    public static class RespawnRule
    {
        /// <summary>
        /// 후보 중 돌아갈 화톳불을 고른다.
        /// 같은 시각에 붙은 불이 둘이면 먼저 온 것을 쓴다 — 순서가 흔들리지 않게 하려는 것뿐이다.
        /// </summary>
        /// <param name="index">고른 후보의 첨자. 실행부가 원래 화톳불을 되찾는 데 쓴다.</param>
        public static bool TryChoose(IReadOnlyList<RespawnAnchor> anchors, out int index)
        {
            index = -1;
            if (anchors == null) return false;

            float best = 0f;
            for (int i = 0; i < anchors.Count; i++)
            {
                var a = anchors[i];
                if (!a.IsLit) continue;
                if (index >= 0 && a.KindledAt <= best) continue;

                index = i;
                best = a.KindledAt;
            }

            return index >= 0;
        }

        /// <summary>
        /// 돌아갈 좌표. 탈 만한 화톳불이 하나도 없으면 시작 지점을 돌려준다.
        /// </summary>
        public static Vector3 Choose(IReadOnlyList<RespawnAnchor> anchors, Vector3 startPoint)
            => TryChoose(anchors, out int i) ? anchors[i].Position : startPoint;
    }
}
