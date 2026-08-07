namespace Survive.World
{
    /// <summary>
    /// <b>B섬에 도착했는가.</b> 한 번 참이 되면 되돌아가도 취소되지 않는다.
    ///
    /// <b>「도착」을 무엇으로 정했는가 — 판정 구역에 들어가는 순간이다.</b>
    /// 지면을 밟는 순간이 아니다. 근거 셋:
    ///
    /// <list type="number">
    /// <item><b>발이 땅에 닿지 않는 도착이 있다.</b> B섬으로 건너가는 수단은 액면 보행
    ///       장비이고, 그것을 신은 사람은 액면 위를 <i>걸어서</i> 들어온다. 둥지로 가는
    ///       길도 육지를 파고든 수로다(기획서 §2.1) — 마지막 몇 걸음이 지면이 아니다.
    ///       "지면을 밟는 순간"으로 정하면 그 사람은 영영 도착하지 않는다</item>
    /// <item><b>지형은 아직 임시다(§16).</b> 지면 접촉은 메시가 정하고, 메시는 바뀐다.
    ///       구역은 사람이 손으로 긋는 것이라 바뀌어도 뜻이 남는다</item>
    /// <item><b>회신 ⑮가 그렇게 말한다.</b> "증원은 플레이어 행동이 아니라 장소가 정한다.
    ///       낫은 무언가에 반응해서 나오는 존재가 아니라 그냥 항상 감시하고 있는 존재다."
    ///       조건을 행동에 붙이면 <i>"안 건드리면 안 오는구나"</i>가 되어 회피 가능한
    ///       위협이 된다. 장소에 붙이면 피할 방법이 없다</item>
    /// </list>
    ///
    /// <b>왜 되돌릴 수 없는가.</b> 각성은 낫이 플레이어를 <i>알아본 것</i>이지 플레이어가
    /// 어디에 서 있는가가 아니다. 돌아가면 풀린다면 A섬으로 한 발 물러서는 것이
    /// 위협을 끄는 스위치가 되고, 그것은 위협이 아니라 토글이다.
    ///
    /// <b>여기서 하지 않는 것 — 증원 자체.</b> 이 판정은 사실 하나만 들고 있고,
    /// 그 위에 무엇을 얹을지는 §5가 정한다.
    /// </summary>
    public struct IslandArrival
    {
        bool _arrived;

        /// <summary>B섬에 닿은 적이 있는가.</summary>
        public bool Arrived => _arrived;

        /// <summary>
        /// 지금 있는 구역을 알려 준다.
        /// <b>처음으로 참이 되는 순간에만</b> true를 돌려준다 — 그 한 번이 방아쇠다.
        /// </summary>
        public bool Observe(IslandZone zone)
        {
            if (_arrived) return false;
            if (zone != IslandZone.IslandB) return false;

            _arrived = true;
            return true;
        }

        /// <summary>세이브에서 되살릴 때. 진행도는 되돌아가지 않으므로 켜는 쪽만 있다.</summary>
        public void Restore(bool arrived)
        {
            if (arrived) _arrived = true;
        }
    }
}
