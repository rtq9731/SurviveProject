using System.Collections.Generic;
using UnityEngine;

namespace Survive.Creatures
{
    /// <summary>
    /// 재등장 후보 자리 하나. <b>재는 일은 전부 몸이 끝내 놓고 여기로 넘긴다</b> —
    /// 밝은가는 <see cref="Survive.World.LitZoneRegistry"/>가, 보이는가는 물리 질의가 답한다.
    /// </summary>
    public readonly struct ReappearSpot
    {
        /// <summary>그 자리.</summary>
        public readonly Vector3 Position;

        /// <summary>
        /// 그 자리가 밝은가. <b>고정 조명이든 랜턴이든 가리지 않는다</b> —
        /// 여기서 묻는 것은 "나타나도 되는 자리인가"이고, 어떤 빛이든 밝으면
        /// 나타날 수 없다는 점은 같다. 조명의 종류가 갈리는 것은 따라붙기를
        /// 푸느냐 마느냐이고 그쪽은 <see cref="ScytheFsm"/>의 몫이다.
        /// </summary>
        public readonly bool Lit;

        /// <summary>
        /// 지금 자리에서 그리로 가는 <b>구간</b>이 사람 눈에 보이는가.
        ///
        /// <b>도착점이 아니라 구간이다.</b> 옆으로 도는 것이 보이면 그것은 그냥 원을
        /// 그리는 것이고, 그러면 "한 마리가 여럿처럼 느껴진다"는 효과가 통째로 사라진다.
        /// 멀어지며 사라지고 다른 데서 나타나야 한다.
        /// </summary>
        public readonly bool PathVisible;

        public ReappearSpot(Vector3 position, bool lit = false, bool pathVisible = false)
        {
            Position = position;
            Lit = lit;
            PathVisible = pathVisible;
        }
    }

    /// <summary>
    /// 따라붙는 중에 <b>어디로 다시 나타날 것인가</b> (기획서 §4.5 "재등장 위치 선정",
    /// 스펙 §20).
    ///
    /// <b>상태가 아니라 <see cref="ScytheState.Beware"/> 내부의 규칙이다.</b> 상태로
    /// 올리면 "재등장 중"이라는 다섯 번째 칸이 생기고, 그 칸에서 사람이 몸을 돌렸을 때
    /// 무엇을 하는지를 또 적어야 한다. 따라붙는 중에 자리를 고르는 일은 따라붙기의
    /// 일부다.
    ///
    /// <b>왜 위치를 바꾸는가.</b> 같은 방향에 계속 있으면 사람이 등지고 무시한다.
    /// 방향이 바뀌면 매번 확인해야 하고, 그 확인이 곧 §19의 "플레이어 조작 자체가
    /// 방어가 된다"이다. 덤으로 한 마리가 여럿처럼 느껴져 개체수 예산이 절약되고,
    /// "한두 번 얼굴을 비춘다"는 목격 연출이 스크립트 없이 반복된다.
    ///
    /// <b>AI가 똑똑해질 필요는 없다.</b> 정비 유닛이라 영리한 경로 탐색이 어울리지도
    /// 않는다. 조건 셋을 통과하는 자리 중 <b>가장 크게 도는 것</b>을 고르면 그만이다.
    /// </summary>
    public static class ScytheReappearance
    {
        /// <summary>고를 자리가 없다. <b>지금 자리에 머무른다</b> — 아래 <see cref="Pick"/> 참조.</summary>
        public const int Stay = -1;

        /// <summary>
        /// 이만큼은 돌아야 <b>자리를 바꾼 것</b>이다(도).
        ///
        /// <b>왜 직각인가.</b> 그보다 작으면 사람이 몸을 돌리지 않고 곁눈으로 따라갈 수
        /// 있고, 그러면 "매번 확인해야 한다"는 효과가 나오지 않는다. 직각은 목이 아니라
        /// <b>몸을 돌려야 하는</b> 최소 각이다.
        ///
        /// <b>이 값은 튜닝 대상이다</b> — 기획서 §5.2의 튜닝 표에 사각 크기와 함께
        /// 놓일 값이지 규칙의 상수가 아니다. 그래서 <see cref="Pick"/>이 인자로도 받는다.
        /// </summary>
        public const float MinTurnDegrees = 90f;

        /// <summary>
        /// 자리를 옮길 때가 되었는가.
        ///
        /// <b>새 시간 상수를 만들지 않는다.</b> 간격은 부르는 쪽이 준다. 몸은 이 개체의
        /// <c>aggroSeconds</c>(낫은 6초)를 그대로 넘긴다 — 그것이 이미 이 개체의
        /// <b>기억의 길이</b>이기 때문이다. "한 번 잊힐 만한 시간마다 자리를 바꾼다"는
        /// 임의로 고른 초가 아니라 개체가 이미 갖고 있는 시간 단위이고, 튜닝이 그 값을
        /// 바꾸면 재등장 리듬도 함께 따라 움직인다.
        /// </summary>
        public static bool DueToMove(float sinceLastMove, float interval) =>
            interval > 0f && sinceLastMove >= interval;

        /// <summary>
        /// 사람을 꼭짓점으로 두고, 지금 자리에서 그 자리까지 <b>몇 도를 도는가</b>.
        ///
        /// <b>높이를 버린다.</b> 사람이 고개를 돌리는 각은 수평각이고, 낫은 액면에 붙어
        /// 다니므로 높이 차는 자리를 바꿨다는 느낌에 아무것도 더하지 않는다. 높이를
        /// 넣으면 같은 방위에 떠 있기만 해도 각도 차가 커져 조건이 헐거워진다.
        ///
        /// 둘 중 하나가 사람과 겹쳐 방향이 서지 않으면 0을 돌려준다 — 그러면 어떤
        /// 하한도 통과하지 못하므로 <b>애매한 자리는 고르지 않는다</b>가 된다.
        /// </summary>
        public static float TurnDegrees(Vector3 anchor, Vector3 current, Vector3 candidate)
        {
            var a = Flat(current - anchor);
            var b = Flat(candidate - anchor);
            if (a == Vector2.zero || b == Vector2.zero) return 0f;

            return Vector2.Angle(a, b);
        }

        /// <summary>
        /// 조건 셋을 전부 만족하는가 — <b>각도 차 · 빛 밖 · 이동 구간 비가시</b>.
        ///
        /// 셋이 <b>모두</b> 필요하다. 하나라도 빼면 그 하나가 무너뜨리는 것이 있다:
        /// 각도가 없으면 제자리 맴돌이가 되고, 빛을 안 보면 랜턴 웅덩이 한가운데
        /// 나타나 그 프레임에 도로 밀려나며, 구간을 안 보면 옆으로 도는 것이 보여
        /// 「한 마리가 여럿」이 성립하지 않는다.
        /// </summary>
        public static bool IsAcceptable(Vector3 anchor, Vector3 current, in ReappearSpot spot,
                                        float minTurnDegrees = MinTurnDegrees)
        {
            if (spot.Lit) return false;
            if (spot.PathVisible) return false;

            return TurnDegrees(anchor, current, spot.Position) >= minTurnDegrees;
        }

        /// <summary>
        /// 어디로 나타날 것인가. 통과한 후보 중 <b>가장 크게 도는 자리</b>를 고른다.
        ///
        /// <b>자리가 하나도 없으면 <see cref="Stay"/>다 — 머무른다.</b>
        /// 셋을 다 만족하는 자리가 없다는 것은 사람 주변이 빛이나 시야로 덮였다는
        /// 뜻이고, 그때 억지로 옮기면 <b>규칙이 스스로 어긴 자리</b>(빛 안이거나
        /// 보이는 경로)로 가게 된다. 그리고 그 정체가 마침 옳은 그림이다 — 화톳불
        /// 앞에서는 후보가 줄어들고, 줄다 못해 없어지면 낫은 <b>제자리에 묶인다</b>.
        /// 고정 조명이 따라붙기를 푸는 것(<see cref="ScytheFsm"/>)과 같은 압력이
        /// 위치 쪽에서도 한 번 더 걸리는 셈이다.
        ///
        /// <b>같은 각이면 목록에서 앞선 쪽이 이긴다.</b> 후보 목록은 몸이 고정 순서로
        /// 만들므로 흔들리지 않는다 — 타겟 선정(<see cref="ThreatSelection"/>)과 같은
        /// 문법이고, 매 프레임 답이 뒤집히면 낫이 두 자리 사이에서 떨게 된다.
        /// </summary>
        public static int Pick(Vector3 anchor, Vector3 current, IReadOnlyList<ReappearSpot> spots,
                               float minTurnDegrees = MinTurnDegrees)
        {
            if (spots == null) return Stay;

            int best = Stay;
            float bestTurn = 0f;

            for (int i = 0; i < spots.Count; i++)
            {
                var spot = spots[i];
                if (spot.Lit || spot.PathVisible) continue;

                float turn = TurnDegrees(anchor, current, spot.Position);
                if (turn < minTurnDegrees) continue;
                if (best != Stay && turn <= bestTurn) continue;

                best = i;
                bestTurn = turn;
            }

            return best;
        }

        /// <summary>
        /// 통과한 후보가 몇 개인가. <b>실측이 "고정 조명 앞에서 자리가 줄어든다"를
        /// 숫자로 확인하는 창구</b>다 — 고른 하나만 돌려주면 몇 개가 남았는지 알 수 없다.
        /// </summary>
        public static int CountAcceptable(Vector3 anchor, Vector3 current,
                                          IReadOnlyList<ReappearSpot> spots,
                                          float minTurnDegrees = MinTurnDegrees)
        {
            if (spots == null) return 0;

            int n = 0;
            for (int i = 0; i < spots.Count; i++)
                if (IsAcceptable(anchor, current, spots[i], minTurnDegrees)) n++;

            return n;
        }

        static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);
    }
}
