using System.Collections.Generic;
using UnityEngine;

namespace Survive.World
{
    /// <summary>
    /// 본거지 하나. 착륙선을 <b>일어설 좌표</b>와 <b>지금 거점 노릇을 하는가</b>로 줄인 것이다.
    ///
    /// <see cref="RespawnAnchor"/>와 나란히 두었지만 필드가 다르다 — 본거지에는
    /// <b>불붙인 시각이 없다.</b> 배는 꺼지지 않고, 여러 개도 아니고, 나중에 지핀 것이
    /// 먼저 지핀 것을 이기는 일도 없다. 없는 축을 흉내 내어 채우면 그 축이 언젠가
    /// 규칙에 끼어든다.
    /// </summary>
    public readonly struct HomeBase
    {
        /// <summary>일어설 자리. 배 안이 아니라 배 곁이다.</summary>
        public readonly Vector3 Position;

        /// <summary>지금 거점 노릇을 하는가. 배가 아직 안 섰으면 false다.</summary>
        public readonly bool Standing;

        public HomeBase(Vector3 position, bool standing = true)
        {
            Position = position;
            Standing = standing;
        }

        /// <summary>배가 없는 세계. 기본값이 그대로 이것이다.</summary>
        public static HomeBase None => default;
    }

    /// <summary>어디서 일어섰는가. 좌표만으로는 무슨 일이 일어난 것인지 못 읽는다.</summary>
    public enum RespawnSite
    {
        /// <summary>거점이 하나도 없어 씬이 세워 둔 자리로 돌아갔다.</summary>
        StartPoint,

        /// <summary>타고 있는 화톳불. 플레이어가 목재를 들여 세운 <b>전진 기지</b>다.</summary>
        ForwardCamp,

        /// <summary>착륙선. 값을 치르지 않아도 언제나 거기 있는 <b>본거지</b>다.</summary>
        Home,
    }

    /// <summary>
    /// 거점이 둘이 되었다. 어느 쪽에서 일어설지 정한다.
    ///
    /// <b>판단 — 전진 기지 대 본거지.</b> 착륙선이 들어와도
    /// <see cref="RespawnRule"/>은 한 글자도 바뀌지 않는다. 이 규칙은 그 위에 얹혀
    /// <b>화톳불이 먼저고 착륙선은 바닥</b>이라고 한 줄을 더할 뿐이다.
    ///
    /// <list type="number">
    /// <item><b>타고 있는 화톳불이 하나라도 있으면 언제나 화톳불이다.</b>
    /// 거리로 재지 않는다 — 착륙선이 죽은 자리 바로 옆이어도 화톳불이 이긴다.
    /// 거리로 재면 호수 근방에서 죽는 동안에는 화톳불을 아무리 세워도 소용이 없고,
    /// 그러면 <b>플레이어가 정한 「여기가 내 자리다」를 규칙이 뒤집는다.</b>
    /// 예측할 수 없는 거점은 거점이 아니다</item>
    /// <item><b>탈 만한 불이 하나도 없을 때만 착륙선이다.</b> 그래서 화톳불의
    /// 부활 지점 역할이 죽지 않는다 — 죽는 것은 「불이 꺼져도 시작 지점이 받아 준다」는
    /// 옛 우연이지 화톳불 쪽이 아니다</item>
    /// <item><b>배도 없으면 시작 지점이다.</b> 지상 지형이 아직 서지 않아 배를 놓을
    /// 자리가 없는 동안에도 오늘과 똑같이 굴러야 한다</item>
    /// </list>
    ///
    /// <b>목재 재고가 곧 안전 재고라는 축이 오히려 선명해진다</b>(기획서 §5.3).
    /// 예전에는 불이 꺼지면 「시작 지점」이라는 이름 없는 자리로 돌아갔고, 그 자리는
    /// 마침 사람이 서 있던 곳이라 대가가 얼마인지 아무도 몰랐다. 이제 꺼진 값은
    /// <b>호수 근방까지 걸어 돌아가는 거리</b>로 또렷하게 계산된다
    /// (<see cref="Setback"/>). 벌목량이 곧 세울 수 있는 전진 기지 수이고,
    /// 전진 기지 수가 곧 죽었을 때 밀려나지 않는 거리다.
    /// </summary>
    public static class HomeBaseRule
    {
        /// <summary>
        /// 어디서 일어설지 정한다.
        /// </summary>
        /// <param name="camps">화톳불 후보들. 판정은 <see cref="RespawnRule"/>에 그대로 넘긴다.</param>
        /// <param name="home">착륙선. 없으면 <see cref="HomeBase.None"/>.</param>
        /// <param name="startPoint">둘 다 없을 때 돌아갈 자리.</param>
        /// <param name="where">일어설 좌표.</param>
        /// <param name="campIndex">고른 화톳불의 첨자. 화톳불이 아니면 -1이다.</param>
        public static RespawnSite Choose(IReadOnlyList<RespawnAnchor> camps, HomeBase home,
                                         Vector3 startPoint, out Vector3 where, out int campIndex)
        {
            if (RespawnRule.TryChoose(camps, out campIndex))
            {
                where = camps[campIndex].Position;
                return RespawnSite.ForwardCamp;
            }

            campIndex = -1;

            if (home.Standing)
            {
                where = home.Position;
                return RespawnSite.Home;
            }

            where = startPoint;
            return RespawnSite.StartPoint;
        }

        /// <summary>
        /// 죽은 자리에서 일어선 자리까지 <b>걸어서 되돌아가야 할 거리</b>.
        /// 사망의 실제 대가이고, 목재 재고가 안전 재고라는 말이 참인지 재는 자다.
        ///
        /// 높이는 세지 않는다 — 되돌아가는 것은 걸어서 하는 일이다.
        /// </summary>
        public static float Setback(Vector3 deathPoint, IReadOnlyList<RespawnAnchor> camps,
                                    HomeBase home, Vector3 startPoint)
        {
            Choose(camps, home, startPoint, out var where, out _);
            deathPoint.y = 0f;
            where.y = 0f;
            return Vector3.Distance(deathPoint, where);
        }
    }
}
