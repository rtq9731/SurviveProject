using UnityEngine;

namespace Survive.Creatures
{
    /// <summary>
    /// 판단에 쓰이는 생물의 고정 성질. <see cref="CreatureDefinitionSO"/>에서 뽑아낸 스칼라뿐이라
    /// 테스트가 에셋을 만들지 않고도 값을 넣어 볼 수 있다.
    /// </summary>
    public readonly struct CreatureTraits
    {
        public readonly BehaviorProfile Behavior;
        public readonly float DetectRadius;
        public readonly float AttackRange;

        public CreatureTraits(BehaviorProfile behavior, float detectRadius, float attackRange)
        {
            Behavior = behavior;
            DetectRadius = detectRadius;
            AttackRange = attackRange;
        }

        public static CreatureTraits From(CreatureDefinitionSO definition) =>
            new CreatureTraits(definition.behavior, definition.detectRadius, definition.attackRange);
    }

    /// <summary>
    /// 이번 프레임에 생물이 아는 것. 위협이 없으면
    /// <see cref="DistanceToThreat"/>는 <see cref="float.MaxValue"/>다 —
    /// 그래야 어떤 감지 반경으로도 감지되지 않는다.
    /// </summary>
    public readonly struct CreatureSenses
    {
        public readonly float DistanceToThreat;

        /// <summary>남은 어그로 시간. 0 이하면 식었다.</summary>
        public readonly float AggroLeft;

        /// <summary>현재 상태가 유지되기로 한 남은 시간. 0 이하면 다시 고를 때다.</summary>
        public readonly float StateTimer;

        public CreatureSenses(float distanceToThreat, float aggroLeft, float stateTimer)
        {
            DistanceToThreat = distanceToThreat;
            AggroLeft = aggroLeft;
            StateTimer = stateTimer;
        }

        /// <summary>위협이 하나도 없는 상황.</summary>
        public static CreatureSenses NoThreat(float aggroLeft, float stateTimer) =>
            new CreatureSenses(float.MaxValue, aggroLeft, stateTimer);
    }

    /// <summary>
    /// 목적지를 하나 내놓을 수 있으면 true. 물리 질의를 감춘다 —
    /// <see cref="CreatureDecision.PickEcology"/>가 우선순위만 알고
    /// 어떻게 찾는지는 모르게 하기 위한 것이다.
    /// </summary>
    public delegate bool TryGetDestination(out Vector3 destination);

    /// <summary>생태 행동 선택의 결과. 목적지가 없으면 <see cref="HasDestination"/>이 false다.</summary>
    public readonly struct EcologyChoice
    {
        public readonly CreatureState State;
        public readonly bool HasDestination;
        public readonly Vector3 Destination;

        EcologyChoice(CreatureState state, bool hasDestination, Vector3 destination)
        {
            State = state;
            HasDestination = hasDestination;
            Destination = destination;
        }

        public static EcologyChoice At(CreatureState state, Vector3 destination) =>
            new EcologyChoice(state, true, destination);

        public static EcologyChoice None(CreatureState state) =>
            new EcologyChoice(state, false, Vector3.zero);
    }

    /// <summary>
    /// 생물의 판단. 전부 순수 함수다 — 같은 입력이면 같은 답을 내고,
    /// 씬도 시간도 난수도 건드리지 않는다.
    ///
    /// CreatureBrain은 값을 모아서 여기에 묻고, 답대로 몸을 움직이기만 한다.
    /// 경계 비교(&lt;= 인지 &lt; 인지)가 곧 게임의 감촉이라
    /// 그 부분이 테스트 가능한 곳에 있어야 한다.
    /// </summary>
    public static class CreatureDecision
    {
        /// <summary>감지 반경 <b>안쪽 또는 경계 위</b>면 감지된 것으로 본다.</summary>
        public static bool IsDetected(float distance, float detectRadius) => distance <= detectRadius;

        /// <summary>사거리 <b>안쪽 또는 경계 위</b>면 닿는 것으로 본다. 먹이·수집 범위도 같은 규칙이다.</summary>
        public static bool IsWithinRange(float distance, float range) => distance <= range;

        /// <summary>쿨다운이 끝났는가. 정확히 그 시각이면 끝난 것으로 본다.</summary>
        public static bool IsReady(float now, float nextActionTime) => now >= nextActionTime;

        /// <summary>
        /// 이번 프레임에 무엇으로 전이해야 하는가.
        ///
        /// 성향마다 판단의 뼈대가 다르다.
        /// <list type="bullet">
        /// <item><b>Passive</b> — 위협을 아예 보지 않는다. 시간이 되면 생태 행동만 다시 고른다.</item>
        /// <item><b>Skittish</b> — 감지되면 무조건 도망. 그 외에는 생태 행동.</item>
        /// <item><b>Defensive</b> — 먼저 덤비지 않는다. 맞아서 어그로가 남아 있을 때만 추격·공격.</item>
        /// <item><b>Aggressive</b> — 감지되기만 해도 추격·공격. 한가하면 배회(생태 행동이 아니다).</item>
        /// </list>
        /// </summary>
        public static CreatureIntent NextIntent(in CreatureTraits traits, in CreatureSenses senses)
        {
            bool detected = IsDetected(senses.DistanceToThreat, traits.DetectRadius);

            switch (traits.Behavior)
            {
                case BehaviorProfile.Passive:
                    return senses.StateTimer <= 0f ? CreatureIntent.Ecology : CreatureIntent.Hold;

                case BehaviorProfile.Skittish:
                    if (detected) return CreatureIntent.Flee;
                    return senses.StateTimer <= 0f ? CreatureIntent.Ecology : CreatureIntent.Hold;

                case BehaviorProfile.Defensive:
                    if (senses.AggroLeft > 0f) return Engage(traits, senses);
                    return senses.StateTimer <= 0f ? CreatureIntent.Ecology : CreatureIntent.Hold;

                case BehaviorProfile.Aggressive:
                    if (detected || senses.AggroLeft > 0f) return Engage(traits, senses);
                    return senses.StateTimer <= 0f ? CreatureIntent.Wander : CreatureIntent.Hold;

                default:
                    return CreatureIntent.Hold;
            }
        }

        /// <summary>덤비기로 했을 때 — 닿으면 때리고, 아니면 쫓는다.</summary>
        static CreatureIntent Engage(in CreatureTraits traits, in CreatureSenses senses) =>
            IsWithinRange(senses.DistanceToThreat, traits.AttackRange)
                ? CreatureIntent.Attack
                : CreatureIntent.Chase;

        /// <summary>
        /// 위협이 없을 때 무엇을 할지. 생산자는 먹으러, 분해자는 주우러 간다.
        /// 할 일이 없으면 배회한다.
        ///
        /// 먹이가 수집보다 앞선다. 먹이가 잡히면 <paramref name="scavenge"/>는
        /// 아예 부르지 않는다 — 물리 질의를 한 번 더 하는 값이 싸지 않고,
        /// 원래 코드도 그렇게 짧게 끊었다.
        /// </summary>
        public static EcologyChoice PickEcology(TryGetDestination feed, TryGetDestination scavenge)
        {
            if (feed != null && feed(out var food)) return EcologyChoice.At(CreatureState.Feed, food);
            if (scavenge != null && scavenge(out var junk)) return EcologyChoice.At(CreatureState.Scavenge, junk);
            return EcologyChoice.None(CreatureState.Wander);
        }

        /// <summary>
        /// 정말 상태를 갈아치울 것인가.
        ///
        /// 같은 상태로 다시 들어가는 것은 남은 시간이 없을 때만 허용한다.
        /// 아니면 배회 목적지가 매 프레임 새로 뽑혀 제자리에서 떠는 꼴이 된다.
        /// </summary>
        public static bool ShouldTransition(CreatureState current, CreatureState next, float stateTimer) =>
            current != next || stateTimer <= 0f;

        /// <summary>이 상태에서 몸이 할 일.</summary>
        public static CreatureAction ActionFor(CreatureState state)
        {
            switch (state)
            {
                case CreatureState.Wander:
                case CreatureState.Flee:
                case CreatureState.Feed:
                case CreatureState.Scavenge:
                    return CreatureAction.MoveToDestination;

                case CreatureState.Chase:
                    return CreatureAction.PursueThreat;

                case CreatureState.Attack:
                    return CreatureAction.AttackThreat;

                default:
                    return CreatureAction.Idle;
            }
        }

        /// <summary>맞았을 때 어떻게 반응하는가.</summary>
        public static DamageReaction ReactToDamage(BehaviorProfile behavior)
        {
            switch (behavior)
            {
                case BehaviorProfile.Skittish:
                    return DamageReaction.Flee;

                case BehaviorProfile.Defensive:
                case BehaviorProfile.Aggressive:
                    return DamageReaction.Retaliate;

                default:
                    return DamageReaction.Ignore;
            }
        }
    }
}
