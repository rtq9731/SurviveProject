using System.Collections.Generic;

namespace Survive.Creatures
{
    /// <summary>
    /// 이번 프레임에 잰 위협 하나. <b>플레이어 한 명이 아니라 「위협 하나」다</b> —
    /// 지금은 언제나 플레이어지만, 판단이 그것을 알 이유가 없다.
    ///
    /// 좌표를 담지 않는 것은 의도다. 판단이 쓰는 것은 거리와 빛뿐이고, 재는 일은
    /// 몸이 한다(<see cref="CreatureDecision"/>의 관례). 고른 위협을 다시 몸으로
    /// 돌려주는 것은 <b>목록에서의 자리(index)</b>이므로 식별자도 필요 없다.
    /// </summary>
    public readonly struct ThreatSighting
    {
        /// <summary>이 위협까지의 거리.</summary>
        public readonly float Distance;

        /// <summary>
        /// 이 위협이 지금 밝은 구역 안에 서 있는가.
        ///
        /// <b>위협마다 따로 답해야 하는 값이다.</b> 둘이 등을 맞대면 한 명은 랜턴
        /// 안이고 다른 한 명은 그 사각에 있을 수 있다(기획서 §14 "코옵 확장 여지").
        /// 이것을 "누군가 빛 안에 있다"는 하나의 깃발로 접으면 그 구도가 사라진다.
        /// </summary>
        public readonly bool InLight;

        /// <summary>
        /// 이 위협이 내준 쪽(등 뒤 사각)에 서 있는가.
        ///
        /// <b>이것도 위협마다 따로 답해야 한다.</b> 빛은 앞쪽만 지키므로, 같은 개체가
        /// 한 사람에게는 정면이고 다른 사람에게는 사각일 수 있다. 「누군가의 사각인가」로
        /// 접으면 등을 맞대는 구도가 성립하지 않는다 (스펙 §19 · §22).
        /// </summary>
        public readonly bool BlindSide;

        public ThreatSighting(float distance) : this(distance, false, false)
        {
        }

        public ThreatSighting(float distance, bool inLight) : this(distance, inLight, false)
        {
        }

        public ThreatSighting(float distance, bool inLight, bool blindSide)
        {
            Distance = distance;
            InLight = inLight;
            BlindSide = blindSide;
        }
    }

    /// <summary>
    /// 여럿 중 누구를 상대할 것인가.
    ///
    /// <b>지금까지 이 규칙은 코드에 없었다.</b> 위협이 하나뿐이면 고를 것이 없으므로
    /// 함수도 없었고, 그래서 "1인 전제"가 어디에 박혀 있는지도 보이지 않았다.
    /// 규칙을 함수 하나로 세워 두면 나중에 규칙을 바꿀 때 코드를 찾아다니지 않아도 된다.
    /// </summary>
    public static class ThreatSelection
    {
        /// <summary>고를 것이 없을 때의 답.</summary>
        public const int None = -1;

        /// <summary>
        /// 누구를 상대하는가 — <b>가장 가까운 것</b>.
        ///
        /// <b>왜 가장 가까운 것인가.</b> 낫은 원한을 품는 포식자가 아니라 MARSO의
        /// 유지보수 유닛이고(<see cref="ScytheHabitat"/>), 그것이 반응하는 것은
        /// "누가 더 미운가"가 아니라 "무엇이 제일 가까이 왔는가"다. 그리고 실무적으로
        /// 이 규칙만이 <b>위협이 하나일 때 지금과 글자 그대로 같은 답</b>을 낸다 —
        /// 새 상태를 하나도 만들지 않기 때문이다.
        ///
        /// <b>어그로가 가장 높은 것을 고르지 않는 이유.</b> 어그로는 지금 개체마다
        /// 스칼라 하나(<c>_aggroLeft</c>)로 있고 위협별로 나뉘어 있지 않다. 위협별
        /// 어그로를 세우는 것은 그 자체로 새 구현이고, 스펙 §22가 명시적으로
        /// 범위 밖에 둔 것이다. 규칙을 바꾸려면 이 함수 하나만 고치면 된다.
        ///
        /// <b>같은 거리면 목록에서 앞선 쪽이 이긴다.</b> 뒤에 오는 것으로 갈아타면
        /// 두 위협이 똑같이 떨어져 있는 동안 매 프레임 대상이 뒤집히고, 그러면
        /// 꼬리 자세가 깜빡인다(<see cref="ScytheStance"/> — 자세가 곧 경고다).
        /// 임의의 규칙이라도 <b>흔들리지 않는</b> 것이 흔들리는 것보다 낫다.
        ///
        /// <b>감지 반경으로 미리 거르지 않는다.</b> 가장 가까운 것이 감지되지 않으면
        /// 나머지도 전부 감지되지 않으므로, 고른 뒤에 재는 것과 결과가 같다.
        /// 거르는 단계를 하나 두면 그 단계가 또 하나의 규칙이 된다.
        /// </summary>
        public static int Select(IReadOnlyList<ThreatSighting> threats)
        {
            if (threats == null) return None;

            int best = None;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < threats.Count; i++)
            {
                float d = threats[i].Distance;
                if (best != None && d >= bestDistance) continue;

                best = i;
                bestDistance = d;
            }

            return best;
        }
    }

    /// <summary>
    /// 이번 프레임에 이 개체가 아는 위협들. <b>「하나」가 아니라 「목록」이 판단의 입구다</b>
    /// (스펙 §22 코옵 사전 확인).
    ///
    /// <b>코옵을 구현하는 것이 아니다.</b> 지금 몸 쪽은 여전히 플레이어 하나를 찾아
    /// 길이 1짜리 목록을 넘기고, 길이 1일 때 이 그릇은 예전과 완전히 같은
    /// <see cref="CreatureSenses"/>를 만든다. 여는 이유는 하나다 — 스펙 §19·§20이
    /// 랜턴 사각과 낫 FSM을 이 위에 얹을 참인데, 그때 1인 전제를 한 겹 더 쌓으면
    /// 나중에 그것을 걷어내야 한다.
    ///
    /// <b>왜 구조체이고 왜 목록을 복사하지 않는가.</b> 매 프레임 도는 자리라 할당이
    /// 생기면 안 된다. 부르는 쪽이 배열 하나를 재사용해 채우고 이 그릇은 그것을
    /// 들여다보기만 한다. 그래서 이 값은 <b>그 프레임 안에서만</b> 유효하다.
    /// </summary>
    public readonly struct ThreatRoster
    {
        readonly IReadOnlyList<ThreatSighting> _threats;

        ThreatRoster(IReadOnlyList<ThreatSighting> threats)
        {
            _threats = threats;
        }

        /// <summary>아무도 없다. <c>default</c>와 같다.</summary>
        public static ThreatRoster Empty => default;

        /// <summary>이미 채워 둔 목록을 들여다본다. 복사하지 않는다.</summary>
        public static ThreatRoster From(IReadOnlyList<ThreatSighting> threats) =>
            new ThreatRoster(threats);

        /// <summary>
        /// 하나짜리 목록. 테스트가 "길이 1이면 예전과 같다"를 적을 때 쓰는 창구다.
        ///
        /// <b>매 프레임 도는 자리에서는 쓰지 않는다</b> — 배열을 하나 새로 만들기
        /// 때문이다. 몸 쪽은 배열 하나를 들고 있다가 값만 갈아 끼우고
        /// <see cref="From"/>으로 감싼다.
        /// </summary>
        public static ThreatRoster One(float distance, bool inLight = false) =>
            new ThreatRoster(new[] { new ThreatSighting(distance, inLight) });

        /// <summary>몇 명인가.</summary>
        public int Count => _threats?.Count ?? 0;

        /// <summary>하나라도 있는가.</summary>
        public bool HasThreat => Count > 0;

        public ThreatSighting this[int index] => _threats[index];

        /// <summary>
        /// 규칙이 고른 자리. 아무도 없으면 <see cref="ThreatSelection.None"/>.
        /// 매번 다시 고르는 것은 목록이 프레임마다 바뀌기 때문이고, 손에 꼽을
        /// 개수라 세는 값이 싸다.
        /// </summary>
        public int SelectedIndex => ThreatSelection.Select(_threats);

        /// <summary>
        /// 가장 가까운 위협까지의 거리. 아무도 없으면 <see cref="float.MaxValue"/>다 —
        /// 그래야 어떤 반경으로도 감지되지 않는다(<see cref="CreatureSenses"/>와 같은 약속).
        /// </summary>
        public float NearestDistance
        {
            get
            {
                int i = SelectedIndex;
                return i == ThreatSelection.None ? float.MaxValue : _threats[i].Distance;
            }
        }

        /// <summary>
        /// 이 반경 안에 <b>하나라도</b> 있는가. 목격·기척처럼 "누구든 곁에 있으면
        /// 성립하는" 판정이 쓴다. 고르는 규칙과 무관하다 — 가장 가까운 것이 밖이면
        /// 나머지도 밖이므로 결과는 같지만, 묻는 물음이 다르므로 이름을 따로 둔다.
        /// </summary>
        public bool AnyWithin(float radius) => NearestDistance <= radius;

        /// <summary>
        /// <b>그 위협 하나만</b> 보고 있는 감각. 이것이 사각 판정을 위협마다 답하게
        /// 하는 자리다 — 같은 개체·같은 프레임에 위협 A는 빛 안, 위협 B는 사각이라는
        /// 답이 나올 수 있어야 한다. 나온 값을 <see cref="CreatureDecision.JudgeLight"/>에
        /// 그대로 넘기면 그 위협에 대한 판정을 얻는다.
        /// </summary>
        public CreatureSenses SensesFor(int index, float aggroLeft, float stateTimer,
                                        bool selfInLight = false)
        {
            if (_threats == null || index < 0 || index >= _threats.Count)
                return CreatureSenses.NoThreat(aggroLeft, stateTimer, selfInLight);

            var t = _threats[index];
            return new CreatureSenses(t.Distance, aggroLeft, stateTimer, selfInLight, t.InLight, t.BlindSide);
        }

        /// <summary>
        /// 규칙이 고른 위협을 보고 있는 감각. <b>목록을 하나로 접는 유일한 자리다.</b>
        ///
        /// 위협이 하나뿐일 때 이 함수가 내놓는 값은 지금 <c>CreatureBrain</c>이
        /// 손으로 만들던 것과 필드 하나까지 같다. 아무도 없을 때도 마찬가지로
        /// <see cref="CreatureSenses.NoThreat"/>와 같다. 그것이 "동작을 바꾸지 않았다"의
        /// 내용이다.
        /// </summary>
        public CreatureSenses Senses(float aggroLeft, float stateTimer, bool selfInLight = false) =>
            SensesFor(SelectedIndex, aggroLeft, stateTimer, selfInLight);
    }
}
