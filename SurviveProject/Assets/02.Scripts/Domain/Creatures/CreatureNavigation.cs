using UnityEngine;

namespace Survive.Creatures
{
    /// <summary>
    /// 목적지를 어디로 잡는가. 벡터 산수뿐이라 씬 없이 확인할 수 있다.
    ///
    /// 두 목적지 모두 <b>높이를 거처의 높이로 되돌린다</b>. 지상 생물은
    /// NavMesh가 알아서 붙여 주지만, 비행 생물은 그렇지 않아서
    /// 이 보정이 없으면 도주할 때마다 조금씩 하늘로 올라간다.
    /// </summary>
    public static class CreatureNavigation
    {
        /// <summary>
        /// 거처 주변의 배회 지점.
        /// <paramref name="unitOffset"/>은 호출자가 뽑은 단위 구 안의 임의의 점이다.
        /// </summary>
        public static Vector3 WanderDestination(Vector3 home, Vector3 unitOffset, float radius)
        {
            Vector3 destination = home + unitOffset * radius;
            destination.y = home.y;
            return destination;
        }

        /// <summary>
        /// 위협의 반대쪽으로 <paramref name="fleeDistance"/>만큼 떨어진 지점.
        ///
        /// 위협과 정확히 같은 자리에 겹쳐 있으면 방향이 없어 제자리를 돌려준다.
        /// 다음 프레임이면 조금이라도 어긋나 방향이 생긴다.
        /// </summary>
        public static Vector3 FleeDestination(Vector3 self, Vector3 threat, float fleeDistance, float groundY)
        {
            Vector3 away = (self - threat).normalized;
            Vector3 destination = self + away * fleeDistance;
            destination.y = groundY;
            return destination;
        }
    }
}
