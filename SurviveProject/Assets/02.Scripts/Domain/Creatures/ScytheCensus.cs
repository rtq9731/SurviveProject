using System.Collections.Generic;

namespace Survive.Creatures
{
    /// <summary>
    /// <b>지금 세계에 낫이 몇 마리 있어야 하는가</b> (기획서 §4.5 "경계 상태").
    ///
    /// <b>원장을 새로 두지 않는다.</b> 개체수는 저장할 값이 아니라 <b>등급의 함수</b>다.
    /// 등급은 이미 월드가 하나로 들고 있으므로(<see cref="ScytheWatch"/>) 저장할 것도
    /// 그것 하나이고, 여기서는 셈만 한다. 값을 두 군데 두면 "등급은 발령인데 수는
    /// 하나"인 상태가 만들어질 수 있고, 그것을 막는 코드를 또 써야 한다.
    ///
    /// <b>수가 곧 난이도 곡선이다.</b> 등 뒤 사각이 있으므로 어느 쪽을 보든 반대쪽이
    /// 비고(§19), 그래서 마릿수가 늘면 "빈 쪽"이 늘어난다 — 개체 하나하나를 세게
    /// 만들지 않고도 압력이 오른다. 그것이 이 표가 1·2·5인 이유다.
    /// </summary>
    public static class ScytheCensus
    {
        /// <summary>아무도 고르지 않는다 / 보호할 개체가 없다.</summary>
        public const int NoOne = -1;

        /// <summary>평시. <b>한 마리다</b> — 낫은 처음부터 거기 있다.</summary>
        public const int CalmCount = 1;

        /// <summary>각성. 특정 구역에 닿으면 하나 붙는다.</summary>
        public const int AwakeCount = 2;

        /// <summary>
        /// 발령. 코어를 훔친 순간이다.
        ///
        /// <b>둘에서 다섯으로 뛰는 것이 의도다.</b> 셋·넷을 거치면 "조금씩 나빠진다"가
        /// 되고, 종막의 사건이 곡선의 한 점이 된다. 여기서만 계단이 가팔라야
        /// 「무언가 돌이킬 수 없는 일을 했다」가 수로 읽힌다.
        /// </summary>
        public const int AlarmedCount = 5;

        /// <summary>이 등급에서 있어야 할 마릿수.</summary>
        public static int CountFor(ScytheAlert alert)
        {
            switch (alert)
            {
                case ScytheAlert.Awake: return AwakeCount;
                case ScytheAlert.Alarmed: return AlarmedCount;
                default: return CalmCount;
            }
        }

        /// <summary>
        /// 등급이 내려갈 때 <b>몇 마리를 물려야 하는가</b>. 음수가 되지 않는다.
        /// </summary>
        public static int SurplusOver(int alive, ScytheAlert alert)
        {
            int want = CountFor(alert);
            return alive > want ? alive - want : 0;
        }

        /// <summary>
        /// 등급이 올라갈 때 <b>몇 마리를 더 세워야 하는가</b>.
        /// </summary>
        public static int ShortfallUnder(int alive, ScytheAlert alert)
        {
            int want = CountFor(alert);
            return alive < want ? want - alive : 0;
        }

        /// <summary>
        /// 수가 줄 때 <b>누가 사라지는가</b> — 사람에게서 <b>먼 것부터</b>다.
        ///
        /// <b>왜 먼 것부터인가.</b> 기획서 §4.5의 회수 연출이 그 그림을 정해 두었다 —
        /// "한 개체만 코어를 들고 둥지로 향하고, <b>나머지는 주변으로 흩어져
        /// 디스폰한다</b>." 흩어진다는 것은 멀어진다는 뜻이고, 눈앞에서 사라지는 것은
        /// 흩어지는 것이 아니라 <b>지워지는 것</b>이다. 가까운 것을 남기면 시선이
        /// 남은 하나에 모이고, 그것이 그 연출이 노리는 바다.
        ///
        /// <b>결정적이어야 한다.</b> 같은 거리면 목록에서 뒤에 있는 것부터 물린다 —
        /// 앞선 자리가 살아남는다는 것은 타겟 선정·재등장과 같은 문법이고
        /// (<see cref="ThreatSelection"/>), 매번 답이 뒤집히면 어느 개체가 남았는지
        /// 검증이 말할 수 없다.
        ///
        /// <b>회수하는 개체는 빠진다 — 2026-08-07에 맞춰졌다.</b> 이 규칙과 §4.5의
        /// 회수 연출이 서로 다른 말을 하고 있었다. 여기서는 "사람에게 가까운 하나가
        /// 남는다"인데 기획서는 "<b>코어를 든 하나가 남고</b> 나머지가 흩어진다"였다.
        ///
        /// <b>둘은 다른 순간을 말한다.</b> 이 규칙이 도는 것은 등급이 내려갈 때이고,
        /// 회수 연출이 도는 것은 코어가 떨어져 하나가 물고 갈 때다. 그런데 그 둘이
        /// 한 프레임에 겹칠 수 있다 — 코어가 둥지에 닿는 순간 등급이 내려가는데,
        /// 그때 물고 온 개체는 둥지에 있고 <b>사람에게서는 멀다.</b> 거리만 보면
        /// 주인공이 먼저 지워진다.
        ///
        /// 그래서 <b>보호할 자리를 하나 받는다.</b> 그 개체는 아무리 멀어도 남고,
        /// 나머지 중에서 먼 것부터 흩어진다. 보호할 것이 없으면
        /// <see cref="NoOne"/>을 주면 되고, 그때는 예전과 글자 그대로 같다.
        /// </summary>
        /// <param name="distances">개체마다 사람까지의 거리. 자리(index)가 곧 개체다.</param>
        /// <param name="howMany">물릴 마릿수.</param>
        /// <param name="keep">
        /// 반드시 남길 자리. 코어를 물고 온 개체다. 없으면 <see cref="NoOne"/>.
        /// </param>
        /// <returns>사라질 개체의 자리들. 먼 것부터 담긴다.</returns>
        public static List<int> PickDespawn(IReadOnlyList<float> distances, int howMany,
                                            int keep = NoOne)
        {
            var picked = new List<int>();
            if (distances == null || howMany <= 0) return picked;

            var order = new List<int>(distances.Count);
            for (int i = 0; i < distances.Count; i++)
                if (i != keep) order.Add(i);

            // 먼 것이 앞으로. 같은 거리면 뒤 자리가 앞으로 와서 먼저 물린다.
            order.Sort((a, b) =>
            {
                int byDistance = distances[b].CompareTo(distances[a]);
                return byDistance != 0 ? byDistance : b.CompareTo(a);
            });

            int take = howMany < order.Count ? howMany : order.Count;
            for (int i = 0; i < take; i++) picked.Add(order[i]);

            return picked;
        }
    }
}
