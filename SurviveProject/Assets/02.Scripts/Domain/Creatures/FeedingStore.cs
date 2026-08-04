using UnityEngine;

namespace Survive.Creatures
{
    /// <summary>
    /// 생산자가 먹어서 쌓아 둔 양을 어떻게 읽는가.
    ///
    /// 배부른 정도는 겉으로 보여야 한다 — 몸이 부풀고 접합부가 밝아지는
    /// 그 정도가 이 값이다. 플레이어가 살진 개체를 골라 잡는 것이
    /// 생태계를 게임으로 만드는 지점이라, 경계가 흔들리면 안 된다.
    /// </summary>
    public static class FeedingStore
    {
        /// <summary>
        /// 0~1로 정규화한 포만도. <paramref name="capacity"/>가 0 이하면
        /// 나눌 수 없으니 0으로 본다 (텅 빈 것으로 그린다).
        /// </summary>
        public static float Fullness(float stored, float capacity) =>
            capacity <= 0f ? 0f : Mathf.Clamp01(stored / capacity);

        /// <summary>정확히 가득 찬 것도 배부른 것이다. 배부르면 더 먹으러 가지 않는다.</summary>
        public static bool IsFull(float stored, float capacity) => stored >= capacity;
    }
}
