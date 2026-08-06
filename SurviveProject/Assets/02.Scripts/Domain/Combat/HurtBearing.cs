using UnityEngine;

namespace Survive.Combat
{
    /// <summary>
    /// 맞았을 때 <b>어디서 왔는가</b>를 화면이 쓸 수 있는 값으로 바꾼다 (기획서 §9).
    ///
    /// <b>왜 이것이 필요한가.</b> 랜턴이 앞으로 밀려 등 뒤에 사각이 생긴 순간부터,
    /// 이 게임에서 사람이 맞는 자리는 대개 <b>보이지 않는 곳</b>이다. 어둠 속에서
    /// 뒤통수를 맞았는데 화면이 아무 방향도 말해 주지 않으면 그것은 난이도가 아니라
    /// <b>억울함</b>이 된다. 기획서가 오프셋의 파생물로 "방향 신호가 반드시 필요하다"를
    /// 함께 적어 둔 이유다.
    ///
    /// <b>소리가 아니라 화면으로 한다.</b> 소리 표는 아직 클립이 비어 있어(골격만 있다)
    /// 지금 방향을 소리에 걸면 검증할 수 있는 것이 없다. 그리고 화면 쪽이라도
    /// <b>어둠을 깨서는 안 된다</b> — 그래서 신호는 밝히는 것이 아니라 <b>가리는 것</b>,
    /// 비네트가 한쪽으로 쏠리는 것으로 낸다. 어두운 게임에서 유일하게 공짜인 표현이다.
    ///
    /// <b>순수 함수다.</b> 각도 하나로 줄여 두면 화면 없이 전수로 확인할 수 있고,
    /// 나중에 소리·햅틱이 붙어도 같은 값을 읽으면 된다.
    /// </summary>
    public static class HurtBearing
    {
        /// <summary>알 수 없는 방향(추락·질식·독). 정면으로 취급해 좌우로 쏠리지 않는다.</summary>
        public const float Unknown = 0f;

        /// <summary>
        /// 사람이 보는 쪽을 0도로 놓았을 때, 때린 것이 있는 방향(도).
        /// 오른쪽이 양수, 왼쪽이 음수, 등 뒤가 ±180이다.
        ///
        /// 수평으로만 잰다 — 위아래는 몸을 돌려 대응할 수 있는 축이 아니다.
        /// </summary>
        public static float Degrees(Vector3 facing, Vector3 selfPosition, Vector3 sourcePosition)
        {
            var f = facing;
            f.y = 0f;
            var d = sourcePosition - selfPosition;
            d.y = 0f;

            if (f.sqrMagnitude < 0.0001f || d.sqrMagnitude < 0.0001f) return Unknown;

            return Vector3.SignedAngle(f.normalized, d.normalized, Vector3.up);
        }

        /// <summary>
        /// 얼마나 <b>등 뒤</b>였는가 (0 = 정면, 1 = 정확히 뒤).
        ///
        /// 이 값이 신호의 세기를 정한다. 앞에서 맞은 것은 이미 눈으로 봤으니
        /// 크게 말할 필요가 없고, <b>못 본 것일수록 크게 말해야 한다.</b>
        /// </summary>
        public static float Behindness(float degrees) =>
            (1f - Mathf.Cos(degrees * Mathf.Deg2Rad)) * 0.5f;

        /// <summary>
        /// 화면에서 <b>어느 쪽 가장자리를 어둡게 할 것인가</b> (-1 왼쪽 ~ +1 오른쪽).
        ///
        /// <b>90도에서 이미 끝까지 간다.</b> 정확한 각도를 화면에 그리려는 것이
        /// 아니라 <b>어느 쪽으로 돌아야 하는가</b> 하나만 전하려는 것이기 때문이다.
        /// 뒤에서 맞으면 왼쪽이든 오른쪽이든 몸을 반 바퀴 돌려야 하고, 그때
        /// 짧은 쪽을 알려 주는 것이 이 부호다.
        /// </summary>
        public static float ScreenSide(float degrees) => Mathf.Clamp(degrees / 90f, -1f, 1f);
    }
}
