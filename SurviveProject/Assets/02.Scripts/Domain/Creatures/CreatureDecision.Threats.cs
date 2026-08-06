namespace Survive.Creatures
{
    /// <summary>
    /// 판단의 입구를 <b>위협 하나</b>에서 <b>위협 목록</b>으로 넓힌 부분
    /// (스펙 §22 "코옵 사전 확인 — 구현하지 않고 열어만 둔다").
    ///
    /// <b>규칙은 한 줄도 여기 없다.</b> 목록에서 하나를 고르는 일은
    /// <see cref="ThreatSelection"/>이 하고, 고른 것으로 무엇을 할지는
    /// 원래 있던 <c>CreatureDecision</c>이 그대로 한다. 이 파일이 하는 일은
    /// <b>목록을 하나로 접어 넘기는 것뿐</b>이라, 위협이 하나일 때 답이 예전과
    /// 다를 수가 없다.
    ///
    /// <b>왜 미리 여는가.</b> 등 뒤 사각(§19)과 낫 4상태 FSM(§20)이 이 위에 얹힌다.
    /// 그때 「위협은 한 명」을 다시 전제하면 나중에 그 층을 통째로 뜯어야 한다.
    /// </summary>
    public static partial class CreatureDecision
    {
        /// <summary>
        /// 이번 프레임에 무엇으로 전이해야 하는가 — 위협 목록판.
        ///
        /// <paramref name="threats"/>가 비어 있으면 "위협 없음"과 같고, 하나면
        /// 그 하나를 보던 예전과 같다. 둘 이상이면 <see cref="ThreatSelection.Select"/>가
        /// 고른 하나를 본다.
        /// </summary>
        /// <param name="aggroLeft">남은 어그로. 목록에 담기지 않는 것은 이 값이 아직
        /// 위협별로 나뉘어 있지 않기 때문이다 — 나누는 것은 구현이고 범위 밖이다.</param>
        /// <param name="selfInLight">내가 밝은 구역에 서 있는가. 위협과 무관한 값이라
        /// 목록 밖에 남는다.</param>
        public static CreatureIntent NextIntent(in CreatureTraits traits, in ThreatRoster threats,
                                                float aggroLeft, float stateTimer,
                                                bool selfInLight = false) =>
            NextIntent(traits, threats.Senses(aggroLeft, stateTimer, selfInLight));

        /// <summary>어그로 시계를 다시 채워야 하는가 — 위협 목록판.</summary>
        public static bool ShouldRenewAggro(in CreatureTraits traits, in ThreatRoster threats,
                                            float aggroLeft, float stateTimer,
                                            bool selfInLight = false) =>
            ShouldRenewAggro(traits, threats.Senses(aggroLeft, stateTimer, selfInLight));

        /// <summary>
        /// 규칙이 이번 프레임에 고른 위협은 누구인가. 몸이 <b>그 위협을 향해</b>
        /// 움직이려면 목록에서의 자리를 되돌려 받아야 한다.
        /// 아무도 없으면 <see cref="ThreatSelection.None"/>.
        ///
        /// <c>threats.SelectedIndex</c>를 그대로 부르는 것과 같다. 이름을 하나 더 두는
        /// 것은 <b>고르는 일이 판단이라는 것</b>을 부르는 쪽에서 읽히게 하기 위한 것이다.
        /// </summary>
        public static int SelectThreat(in ThreatRoster threats) => threats.SelectedIndex;
    }
}
