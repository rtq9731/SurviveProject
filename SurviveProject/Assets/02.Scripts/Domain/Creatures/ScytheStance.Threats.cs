namespace Survive.Creatures
{
    /// <summary>
    /// 꼬리 자세 판정의 입구를 위협 목록으로 넓힌 부분 (스펙 §22).
    /// 규칙은 <c>ScytheStance.cs</c> 그대로이고, 여기는 목록을 접어 넘기기만 한다.
    /// </summary>
    public static partial class ScytheStance
    {
        /// <summary>
        /// 이번 프레임의 꼬리 자세 — 위협 목록판.
        ///
        /// <b>자세는 「규칙이 고른 위협」을 기준으로 한다.</b> 꼬리는 상태 표시등이고
        /// 그 표시등이 답해야 하는 물음은 "지금 이 개체가 누구를 보고 있는가"이므로,
        /// 두뇌가 고른 것과 <b>같은 것</b>을 봐야 한다. 둘이 갈리면 두뇌는 쫓기 시작했는데
        /// 꼬리는 늘어져 있는 프레임이 생긴다 — 이 파일이 두뇌와 같은
        /// <see cref="ThreatSelection"/>을 쓰는 이유가 그것이다.
        ///
        /// <b>상태 유지 시간(stateTimer)은 받지 않는다.</b>
        /// <see cref="PostureFor(in CreatureTraits, in CreatureSenses, CreatureState, HabitatZone, ScytheAlert)"/>가
        /// 그 값을 보지 않으므로, 부르는 쪽이 뜻 없는 0을 넘기게 두지 않는다.
        /// </summary>
        public static ScythePosture PostureFor(in CreatureTraits traits, in ThreatRoster threats,
                                               float aggroLeft, CreatureState state,
                                               HabitatZone zone, ScytheAlert alert) =>
            PostureFor(traits, threats.Senses(traits, aggroLeft, 0f), state, zone, alert);
    }
}
