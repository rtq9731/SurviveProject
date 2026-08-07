using System.Collections.Generic;
using UnityEngine;

namespace Survive.Creatures
{
    /// <summary>
    /// 코어가 지금 어디에 있는가 (기획서 §2.1 "둥지와 코어", §4.5 "경계 상태").
    ///
    /// <b>자리가 넷인 것은 각각 되돌아가는 길이 다르기 때문이다.</b> 손에 있는 것과
    /// 바닥에 있는 것은 둘 다 "둥지 밖"이지만, 앞엣것은 사람이 걸어가면 끝나고
    /// 뒤엣것은 낫이 주우러 온다. 하나로 접으면 그 차이가 코드 여기저기에 흩어진다.
    /// </summary>
    public enum CoreWhere
    {
        /// <summary>둥지에 있다. <b>유일한 해제 조건</b>이다.</summary>
        Nest,

        /// <summary>사람이 들고 있다.</summary>
        Held,

        /// <summary>바닥에 떨어져 있다. 낫이 주우러 온다.</summary>
        Dropped,

        /// <summary>낫이 꼬리로 물고 둥지로 가는 중이다.</summary>
        Carried,
    }

    /// <summary>
    /// 코어에 무슨 일이 일어났는가. <b>세계가 알려 주는 사건이지 개체의 판단이 아니다.</b>
    /// </summary>
    public enum CoreEvent
    {
        /// <summary>사람이 집었다.</summary>
        Taken,

        /// <summary>사람이 내려놓았다(떨궜다).</summary>
        Dropped,

        /// <summary>낫 하나가 물었다.</summary>
        PickedUpByScythe,

        /// <summary>둥지에 놓였다. 사람이 들고 갔든 낫이 옮겼든 같다.</summary>
        Delivered,
    }

    /// <summary>
    /// <b>둥지와 코어의 규칙</b> (기획서 §2.1 · §4.5).
    ///
    /// <b>해제 조건은 하나다 — 코어가 둥지에 있느냐.</b> 사람이 어디에 떨궜는지도,
    /// 지금 들고 있는지도, 몇 번 훔쳤는지도 보지 않는다. 규칙이 하나라 배우는 데
    /// 한 번이면 되고, 그것이 이 항목이 노리는 전부다.
    ///
    /// <b>소프트락이 없다.</b> 어느 자리에서도 둥지로 돌아가는 길이 있다 — 사람이
    /// 들고 있으면 걸어가면 되고, 바닥에 있으면 낫이 주우러 온다. 실패해도 낫이
    /// 되돌려 놓으므로 다시 훔치면 된다. <b>대가는 시간뿐이다.</b>
    /// </summary>
    public static class NestRule
    {
        /// <summary>
        /// 둥지 반경(m). 이 안에 놓이면 "둥지에 있다"로 친다.
        ///
        /// <b>점이 아니라 원인 이유.</b> 코어를 정확히 한 점에 놓게 하면 되돌려 놓는
        /// 일이 조작 시험이 된다. 되돌리는 것은 <b>벌이 아니라 취소</b>여야 하므로
        /// 너그러워야 한다. 낫이 물어다 놓는 자리도 프레임마다 조금씩 다르다.
        /// </summary>
        public const float HomeRadius = 3f;

        /// <summary>고를 것이 없을 때의 답.</summary>
        public const int NoOne = -1;

        /// <summary>
        /// 이 자리가 둥지 안인가. <b>수평으로만 잰다</b> — 코어가 바닥에 놓이든
        /// 낫의 꼬리에 매달려 지나가든 같은 자리로 쳐야, 물어다 놓는 높이가
        /// 판정을 가르지 않는다.
        /// </summary>
        public static bool AtHome(Vector3 core, Vector3 nest, float radius = HomeRadius)
        {
            float dx = core.x - nest.x;
            float dz = core.z - nest.z;
            return dx * dx + dz * dz <= radius * radius;
        }

        /// <summary>
        /// <b>해제는 코어의 자리만 본다.</b> 사람이 어디 있는지도, 들고 있는지도
        /// 인자에 없다 — 없어야 그 사실이 규칙으로 참이 된다.
        /// </summary>
        public static ScytheAlert AlertFor(CoreWhere where) =>
            where == CoreWhere.Nest ? ScytheAlert.Calm : ScytheAlert.Alarmed;

        /// <summary>
        /// 사건 하나가 코어를 어디로 옮기는가.
        ///
        /// <b>어울리지 않는 사건은 자리를 바꾸지 않는다.</b> 예외를 던지지 않는 것은
        /// 세계가 사건을 두 번 보내는 일이 흔하기 때문이다 — 두 번째가 아무것도
        /// 하지 않으면 그것으로 족하다.
        /// </summary>
        public static CoreWhere Next(CoreWhere where, CoreEvent what)
        {
            switch (what)
            {
                // 둥지에서든 바닥에서든 사람이 집으면 손에 있다. 낫이 물고 가는
                // 것도 사람이 가로챌 수 있다 — 따라붙어 뺏는 것이 놓친 판을 되돌리는 길이다.
                case CoreEvent.Taken:
                    return CoreWhere.Held;

                // 손에 있던 것만 떨어진다.
                case CoreEvent.Dropped:
                    return where == CoreWhere.Held ? CoreWhere.Dropped : where;

                // 바닥에 있는 것만 낫이 문다. 사람 손에 있는 것을 뺏지 않는다 —
                // 뺏기 시작하면 "붙어 있으면 안 뺏긴다"는 약속이 필요해진다.
                case CoreEvent.PickedUpByScythe:
                    return where == CoreWhere.Dropped ? CoreWhere.Carried : where;

                // 둥지에 놓였다. 누가 옮겼든 같다.
                case CoreEvent.Delivered:
                    return CoreWhere.Nest;

                default:
                    return where;
            }
        }

        /// <summary>
        /// 이 자리에서 둥지로 돌아가는 <b>다음 한 걸음</b>. 없으면 null이 아니라
        /// 자기 자신을 돌려준다(이미 둥지다).
        ///
        /// <b>소프트락이 없다는 것을 코드가 들고 있는 자리다.</b> 검증이 이 함수를
        /// 따라 걸어 언제나 둥지에 닿는지 확인한다 — 글로 "없다"고 적는 것보다
        /// 기계가 걸어 보는 편이 낫다.
        /// </summary>
        public static CoreEvent? StepHome(CoreWhere where)
        {
            switch (where)
            {
                case CoreWhere.Nest: return null;              // 이미 집이다
                case CoreWhere.Held: return CoreEvent.Delivered;    // 사람이 걸어가 놓는다
                case CoreWhere.Dropped: return CoreEvent.PickedUpByScythe;  // 낫이 주우러 온다
                case CoreWhere.Carried: return CoreEvent.Delivered; // 물고 가던 것이 닿는다
                default: return null;
            }
        }

        /// <summary>
        /// <b>누가 회수하러 가는가 — 코어에 가장 가까운 하나.</b>
        ///
        /// 하나뿐인 것이 요점이다. 기획서 §4.5: "다섯이 함께 둥지로 가지 않는다.
        /// <b>한 개체만 코어를 들고 둥지로 향하고, 나머지는 주변으로 흩어져
        /// 디스폰한다.</b>" 무리 지어 퇴각하는 것은 생물의 그림이고, 각자 복귀하는
        /// 것이 정비 유닛의 그림이다.
        ///
        /// <b>왜 코어에 가까운 것인가.</b> 사람에게 가까운 것을 고르면 회수가
        /// 사람을 쫓는 일이 된다. 회수는 사람과 무관한 일이므로 기준도 코어여야 한다.
        ///
        /// 같은 거리면 앞선 자리가 이긴다 — 타겟 선정·재등장과 같은 문법이다.
        /// </summary>
        public static int PickRetriever(IReadOnlyList<float> distancesToCore)
        {
            if (distancesToCore == null) return NoOne;

            int best = NoOne;
            float bestDistance = 0f;

            for (int i = 0; i < distancesToCore.Count; i++)
            {
                if (best != NoOne && distancesToCore[i] >= bestDistance) continue;

                best = i;
                bestDistance = distancesToCore[i];
            }

            return best;
        }
    }
}
