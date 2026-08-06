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

        /// <summary>
        /// 밝은 곳을 꺼리는가. 소비자(포식자)만 참이다.
        ///
        /// 이 게임에서 빛은 배터리를 태우는 대가로 얻는 것이고, 그 대가에는
        /// 값이 있어야 한다 — 랜턴을 켜면 포식자가 다가오지 못한다는 것이 그 값이다.
        /// </summary>
        public readonly bool AvoidsLight;

        public CreatureTraits(BehaviorProfile behavior, float detectRadius, float attackRange)
            : this(behavior, detectRadius, attackRange, false)
        {
        }

        public CreatureTraits(BehaviorProfile behavior, float detectRadius, float attackRange,
                              bool avoidsLight)
        {
            Behavior = behavior;
            DetectRadius = detectRadius;
            AttackRange = attackRange;
            AvoidsLight = avoidsLight;
        }

        public static CreatureTraits From(CreatureDefinitionSO definition) =>
            new CreatureTraits(definition.behavior, definition.detectRadius, definition.attackRange,
                               definition.avoidsLight);
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

        /// <summary>내가 지금 밝은 구역 안에 서 있는가.</summary>
        public readonly bool SelfInLight;

        /// <summary>위협이 지금 밝은 구역 안에 서 있는가. 위협이 없으면 false다.</summary>
        public readonly bool ThreatInLight;

        public CreatureSenses(float distanceToThreat, float aggroLeft, float stateTimer)
            : this(distanceToThreat, aggroLeft, stateTimer, false, false)
        {
        }

        public CreatureSenses(float distanceToThreat, float aggroLeft, float stateTimer,
                              bool selfInLight, bool threatInLight)
        {
            DistanceToThreat = distanceToThreat;
            AggroLeft = aggroLeft;
            StateTimer = stateTimer;
            SelfInLight = selfInLight;
            ThreatInLight = threatInLight;
        }

        /// <summary>위협이 하나도 없는 상황.</summary>
        public static CreatureSenses NoThreat(float aggroLeft, float stateTimer) =>
            new CreatureSenses(float.MaxValue, aggroLeft, stateTimer);

        /// <summary>위협도 없고 빛도 없는 것이 아니라, 위협만 없는 상황. 빛은 따로 준다.</summary>
        public static CreatureSenses NoThreat(float aggroLeft, float stateTimer, bool selfInLight) =>
            new CreatureSenses(float.MaxValue, aggroLeft, stateTimer, selfInLight, false);
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
    /// <remarks>
    /// <b>partial인 이유.</b> 위협 「목록」을 받는 입구는
    /// <c>CreatureDecision.Threats.cs</c>에 따로 있다. 코옵 사전 확인(스펙 §22)이
    /// 여는 것은 입구뿐이고 판단의 내용은 하나도 건드리지 않으므로, 그 둘을
    /// 다른 파일에 두면 여기서 무엇이 바뀌었는지 <b>보이지 않는다는 것이 곧 증명</b>이 된다.
    /// </remarks>
    public static partial class CreatureDecision
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
        /// <item><b>Aggressive</b> — 감지되기만 해도 추격·공격. 한가하면 배회(생태 행동이 아니다).
        ///   <see cref="CreatureTraits.AvoidsLight"/>가 켜져 있으면 빛이 그 앞을 막는다 —
        ///   <see cref="LightVerdict"/> 참조.</item>
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
                    switch (JudgeLight(traits, senses))
                    {
                        case LightVerdict.Retreat: return CreatureIntent.Flee;
                        case LightVerdict.Blocked: return CreatureIntent.Wander;
                    }
                    if (detected || senses.AggroLeft > 0f) return Engage(traits, senses);
                    return senses.StateTimer <= 0f ? CreatureIntent.Wander : CreatureIntent.Hold;

                default:
                    return CreatureIntent.Hold;
            }
        }

        /// <summary>
        /// 빛이 이번 판단을 어떻게 가로막는가.
        ///
        /// 빛을 꺼리지 않는 생물에게는 언제나 <see cref="LightVerdict.Clear"/>다 —
        /// 기존 4종(눈·공·날개·열매게)은 <see cref="CreatureDefinitionSO.avoidsLight"/>가
        /// 꺼져 있으므로 이 함수를 통과하고 나면 예전과 완전히 같은 판단을 받는다.
        ///
        /// 도주·방어 성향에는 아예 적용하지 않는다. 도주는 빛과 무관하게 이미 달아나고,
        /// 방어는 먼저 덤비지 않아 빛이 더 막을 것이 없다. 규칙을 넓게 걸어 두면
        /// 나중에 그 두 성향의 감촉이 조용히 바뀐다.
        /// </summary>
        public static LightVerdict JudgeLight(in CreatureTraits traits, in CreatureSenses senses)
        {
            if (!traits.AvoidsLight) return LightVerdict.Clear;

            // 내가 빛 안이면 무엇을 하던 중이든 물러난다. 화톳불 옆을 지나던 중일 수도,
            // 코앞에서 플레이어가 랜턴을 켠 것일 수도 있다 — 둘 다 결과는 같다.
            if (senses.SelfInLight) return LightVerdict.Retreat;

            // 대상이 빛 안이면 경계에서 끊는다. 어그로가 타고 있어도 마찬가지다 —
            // 빛이 어그로보다 세지 않으면 "랜턴을 켜면 다가오지 못한다"가 거짓이 된다.
            if (senses.ThreatInLight) return LightVerdict.Blocked;

            return LightVerdict.Clear;
        }

        /// <summary>
        /// 선공 성향이 위협을 계속 보고 있어 어그로 시계를 다시 채워야 하는가.
        ///
        /// 이것이 없으면 선공 추격은 감지 반경을 한 발짝 벗어난 순간 끝난다 —
        /// 어그로는 피격으로만 차오르기 때문이다. 시야에 담고 있는 동안 계속
        /// 다시 채워 두면, 놓친 뒤 <see cref="CreatureDefinitionSO.aggroSeconds"/>만큼
        /// 더 쫓다가 배회(=거처 주변)로 돌아간다. 그것이 "도망이 성립한다"는 말의 내용이다.
        ///
        /// 빛에 막힌 프레임에는 채우지 않는다. 채워 버리면 플레이어가 랜턴을 켜고
        /// 서 있는 내내 어그로가 만료되지 않아, 빛을 끄는 순간 추격이 이어진다.
        /// </summary>
        public static bool ShouldRenewAggro(in CreatureTraits traits, in CreatureSenses senses)
            => traits.Behavior == BehaviorProfile.Aggressive
            && JudgeLight(traits, senses) == LightVerdict.Clear
            && IsDetected(senses.DistanceToThreat, traits.DetectRadius);

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
