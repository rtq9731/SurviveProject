using UnityEngine;

namespace Survive.Player
{
    /// <summary>
    /// 떨어져 굳은 땅에 부딪힌 대가.
    ///
    /// <b>왜 높이가 아니라 속도인가.</b> 떨어진 높이를 재려면 "발이 떨어진 자리"를
    /// 기억해야 하는데, 그 자리는 경사·계단·순간이동으로 쉽게 오염된다.
    /// 땅에 닿는 순간의 하강 속도는 그런 기억 없이 그 프레임에 그대로 있고,
    /// 아픈 정도와도 직결된다. 자유낙하라면 둘은 v = √(2gh)로 서로 환산된다.
    ///
    /// <b>임계는 일상 이동에서 잰 값 위에 둔다.</b> (중력 9.81, 점프력 2 기준)
    ///   평지에서 점프해 내려앉기        2.0 m/s
    ///   1m 턱에서 내려서기              4.4 m/s
    ///   2m 언덕을 뛰어내리기            6.3 m/s
    ///   3m                              7.7 m/s
    /// 무해 구간을 <see cref="SafeImpactSpeed"/>=9 m/s(자유낙하 4.1m)로 두면
    /// 점프의 4.5배, 흔한 지형 낙차의 두 배 위다 — 걷고 뛰는 동안은 절대 닿지 않는다.
    ///
    /// <b>치명은 <see cref="LethalImpactSpeed"/>=24 m/s(자유낙하 29.4m).</b>
    /// 어둠이 감추는 균열은 그만큼 깊다는 뜻이고, 그 아래로는 온전한 체력이라도
    /// 살아남지 못한다. 그 사이는 속도에 비례한다:
    ///   12 m/s ( 7.3m) → 20    15 m/s (11.5m) → 40
    ///   18 m/s (16.5m) → 60    21 m/s (22.5m) → 80
    ///
    /// 물은 여기 오지 않는다. 입수는 어떤 높이에서도 무해하므로 부르는 쪽에서 거른다.
    /// </summary>
    public static class FallImpact
    {
        /// <summary>여기까지는 아프지 않다. 자유낙하 4.1m에 해당한다.</summary>
        public const float SafeImpactSpeed = 9f;

        /// <summary>여기서부터는 온전한 체력도 남지 않는다. 자유낙하 29.4m에 해당한다.</summary>
        public const float LethalImpactSpeed = 24f;

        /// <summary>치명 속도에서 주는 피해. 기본 최대 체력과 같게 둬 확실히 죽는다.</summary>
        public const float LethalDamage = 100f;

        /// <summary>
        /// 착지 속도(m/s, 양수가 하강)에 대한 피해.
        /// 무해 구간 이하는 0, 치명 속도 이상은 <see cref="LethalDamage"/>로 잘린다 —
        /// 더 깎아 봐야 이미 죽은 사람에게 더 깎는 것이라 뜻이 없다.
        /// </summary>
        public static float DamageFor(float impactSpeed)
        {
            if (float.IsNaN(impactSpeed) || impactSpeed <= SafeImpactSpeed) return 0f;
            if (impactSpeed >= LethalImpactSpeed) return LethalDamage;

            float t = (impactSpeed - SafeImpactSpeed) / (LethalImpactSpeed - SafeImpactSpeed);
            return t * LethalDamage;
        }

        /// <summary>자유낙하 높이 → 착지 속도. 임계를 미터로 읽기 위한 환산이다.</summary>
        public static float ImpactSpeedFromHeight(float meters, float gravity = 9.81f)
        {
            if (meters <= 0f || gravity <= 0f) return 0f;
            return Mathf.Sqrt(2f * gravity * meters);
        }
    }
}
