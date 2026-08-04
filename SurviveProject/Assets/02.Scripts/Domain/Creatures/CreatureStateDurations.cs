namespace Survive.Creatures
{
    /// <summary>
    /// 각 상태를 얼마나 붙들고 있는가. 이 시간이 다 되기 전에는
    /// 같은 상태로 다시 들어가지 못한다(<see cref="CreatureDecision.ShouldTransition"/>).
    ///
    /// 배회만 난수다. 여러 마리가 같은 박자로 방향을 트는 것이
    /// 기계 무리로 보이지 않고 격자로 보이기 때문이다. 난수를 뽑는 일은
    /// 호출자에게 남겨 두었다 — Domain은 난수를 쓰지 않는다.
    /// </summary>
    public static class CreatureStateDurations
    {
        /// <summary>배회 지속시간의 하한.</summary>
        public const float WanderMinSeconds = 2f;

        /// <summary>배회 지속시간의 상한.</summary>
        public const float WanderMaxSeconds = 4f;

        /// <summary>먹기·줍기. 목표에 닿기까지의 여유.</summary>
        public const float EcologySeconds = 1.5f;

        /// <summary>도주. 이 시간 동안은 같은 방향으로 내뺀다.</summary>
        public const float FleeSeconds = 1.5f;

        /// <summary>그 밖의 상태(대기·추격·공격). 짧게 잡아 매 프레임 가깝게 다시 판단한다.</summary>
        public const float DefaultSeconds = 0.5f;

        /// <summary>
        /// <paramref name="state"/>를 붙들 시간.
        /// <see cref="CreatureState.Wander"/>일 때만 <paramref name="wanderDuration"/>을 그대로 돌려준다 —
        /// 호출자가 <see cref="WanderMinSeconds"/>~<see cref="WanderMaxSeconds"/>에서 뽑아 넘긴 값이다.
        /// </summary>
        public static float For(CreatureState state, float wanderDuration)
        {
            switch (state)
            {
                case CreatureState.Wander:
                    return wanderDuration;

                case CreatureState.Feed:
                case CreatureState.Scavenge:
                    return EcologySeconds;

                case CreatureState.Flee:
                    return FleeSeconds;

                default:
                    return DefaultSeconds;
            }
        }
    }
}
