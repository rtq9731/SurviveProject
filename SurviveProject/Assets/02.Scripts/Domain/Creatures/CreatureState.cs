namespace Survive.Creatures
{
    /// <summary>
    /// 생물이 지금 무엇을 하고 있는가. CreatureBrain의 상태머신이 쓰는 값이다.
    ///
    /// Domain에 두는 이유는 상태 전이 판단(<see cref="CreatureDecision"/>)이
    /// MonoBehaviour 없이 확인 가능해야 하기 때문이다.
    /// </summary>
    public enum CreatureState
    {
        /// <summary>아무것도 하지 않는다. 시작 상태.</summary>
        Idle,

        /// <summary>거처 주변을 어슬렁거린다.</summary>
        Wander,

        /// <summary>위협에서 멀어진다.</summary>
        Flee,

        /// <summary>위협을 쫓는다.</summary>
        Chase,

        /// <summary>사거리 안이라 멈춰서 때린다.</summary>
        Attack,

        /// <summary>식물을 먹으러 간다 (생산자).</summary>
        Feed,

        /// <summary>떨어진 전리품을 주우러 간다 (분해자).</summary>
        Scavenge,

        /// <summary>죽었다. 더 이상 갱신되지 않는다.</summary>
        Dead,
    }

    /// <summary>
    /// 한 프레임의 전이 판단 결과.
    ///
    /// <see cref="Ecology"/>만 상태가 아니다 — 먹이나 전리품이 실제로 있는지는
    /// 물리 질의를 해봐야 알 수 있어서, 그 결정은 <see cref="CreatureDecision.PickEcology"/>로
    /// 한 단계 더 내려간다.
    /// </summary>
    public enum CreatureIntent
    {
        /// <summary>전이하지 않는다. 하던 것을 계속한다.</summary>
        Hold,

        /// <summary>위협이 없으니 생태 행동을 고른다 (먹이 → 수집 → 배회).</summary>
        Ecology,

        Wander,
        Flee,
        Chase,
        Attack,
    }

    /// <summary>상태가 정해졌을 때 실제로 몸이 하는 일.</summary>
    public enum CreatureAction
    {
        /// <summary>멈춰 선다.</summary>
        Idle,

        /// <summary>미리 잡아 둔 목적지로 간다.</summary>
        MoveToDestination,

        /// <summary>위협의 현재 위치로 간다.</summary>
        PursueThreat,

        /// <summary>멈춰 서서 위협을 때린다.</summary>
        AttackThreat,
    }

    /// <summary>맞았을 때의 반응. <see cref="BehaviorProfile"/>이 결정한다.</summary>
    public enum DamageReaction
    {
        /// <summary>신경 쓰지 않는다 (Passive).</summary>
        Ignore,

        /// <summary>도망친다 (Skittish).</summary>
        Flee,

        /// <summary>어그로를 채우고 반격한다 (Defensive·Aggressive).</summary>
        Retaliate,
    }
}
