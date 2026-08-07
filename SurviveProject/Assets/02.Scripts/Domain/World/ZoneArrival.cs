using UnityEngine;

namespace Survive.World
{
    /// <summary>
    /// <b>정해 둔 구역에 닿았는가.</b> 한 번 참이 되면 되돌아가도 취소되지 않는다.
    ///
    /// <b>무엇에 쓰는가.</b> 낫 증원(기획서 §4.5 경계 등급)의 방아쇠가 「특정 구역 도달」로
    /// 돌아갔다. 그 판정의 뼈대가 이것이다.
    ///
    /// <b>어느 구역인지는 여기서 정하지 않는다 — 아직 미결이다(사람의 몫).</b>
    /// 지상 지형이 통째로 새로 만들어지므로(스펙 §13) 방아쇠가 될 자리는 지형이 선 뒤에
    /// 눈으로 정해진다. 그래서 이 구조체는 <b>방아쇠 구역을 비운 채로 태어나고,
    /// 비어 있는 동안에는 어느 구역에 들어도 참이 되지 않는다.</b> 아무 구역이나 임시로
    /// 박아 두면 지형이 서는 날 「왜 여기서 낫이 늘지」를 아무도 설명하지 못한다.
    ///
    /// <b>「도착」을 무엇으로 정했는가 — 판정 구역에 들어가는 순간이다.</b>
    /// 지면을 밟는 순간이 아니다. 근거 셋:
    ///
    /// <list type="number">
    /// <item><b>발이 땅에 닿지 않는 도착이 있다.</b> 넓은 액면을 건너는 수단은 액면 보행
    ///       장비이고, 그것을 신은 사람은 액면 위를 <i>걸어서</i> 들어온다. 둥지로 가는
    ///       길도 육지를 파고든 수로다(기획서 §2.1) — 마지막 몇 걸음이 지면이 아니다.
    ///       "지면을 밟는 순간"으로 정하면 그 사람은 영영 도착하지 않는다</item>
    /// <item><b>지형은 아직 없다(스펙 §13).</b> 지면 접촉은 메시가 정하고, 메시는 바뀐다.
    ///       구역은 사람이 손으로 긋는 것이라 바뀌어도 뜻이 남는다</item>
    /// <item><b>회신 ⑮가 그렇게 말한다.</b> "증원은 플레이어 행동이 아니라 장소가 정한다.
    ///       낫은 무언가에 반응해서 나오는 존재가 아니라 그냥 항상 감시하고 있는 존재다."
    ///       조건을 행동에 붙이면 <i>"안 건드리면 안 오는구나"</i>가 되어 회피 가능한
    ///       위협이 된다. 장소에 붙이면 피할 방법이 없다</item>
    /// </list>
    ///
    /// <b>왜 되돌릴 수 없는가.</b> 각성은 낫이 플레이어를 <i>알아본 것</i>이지 플레이어가
    /// 어디에 서 있는가가 아니다. 돌아가면 풀린다면 한 발 물러서는 것이 위협을 끄는
    /// 스위치가 되고, 그것은 위협이 아니라 토글이다.
    ///
    /// <b>여기서 하지 않는 것 — 증원 자체.</b> 이 판정은 사실 하나만 들고 있고,
    /// 그 위에 무엇을 얹을지는 낫 쪽이 정한다.
    /// </summary>
    public struct ZoneArrival
    {
        SurfaceZone _target;
        bool _hasTarget;
        bool _arrived;

        /// <summary>
        /// 이 구역을 방아쇠로 삼는 판정을 만든다. <b>사람이 자리를 정하는 날 부를 창구가
        /// 이것 하나다</b> — 코드 여기저기에 「어느 구역이면 각성」이 흩어지지 않게.
        /// </summary>
        public static ZoneArrival Watching(SurfaceZone zone) =>
            new ZoneArrival { _target = zone, _hasTarget = true };

        /// <summary>방아쇠가 될 구역이 정해져 있는가. 기본값은 <b>정해져 있지 않다</b>.</summary>
        public bool HasTarget => _hasTarget;

        /// <summary>방아쇠 구역에 닿은 적이 있는가.</summary>
        public bool Arrived => _arrived;

        /// <summary>
        /// 지금 있는 구역을 알려 준다.
        /// <b>처음으로 참이 되는 순간에만</b> true를 돌려준다 — 그 한 번이 방아쇠다.
        /// 방아쇠 구역이 아직 안 정해졌으면 언제나 false다.
        /// </summary>
        public bool Observe(SurfaceZone zone)
        {
            if (_arrived) return false;
            if (!_hasTarget) return false;
            if (zone != _target) return false;

            _arrived = true;
            return true;
        }

        /// <summary>
        /// 이 자리에 서 있다고 알려 준다. <b>아무 구역에도 들지 않으면 거짓이다</b> —
        /// 판정 밖을 기본 구역으로 삼으면 볼륨이 하나도 안 놓인 지금 상태에서
        /// 온 세계가 방아쇠가 된다.
        /// </summary>
        public bool ObserveAt(Vector3 position) =>
            SurfaceZoneRegistry.TryAt(position, out var zone) && Observe(zone);

        /// <summary>세이브에서 되살릴 때. 진행도는 되돌아가지 않으므로 켜는 쪽만 있다.</summary>
        public void Restore(bool arrived)
        {
            if (arrived) _arrived = true;
        }
    }
}
