using UnityEngine;

namespace Survive.World
{
    /// <summary>
    /// 밝은 구역 하나를 내놓는 존재. 화톳불이든, 나중에 붙을 발광 버섯 군락 같은
    /// 고정 광원이든, "어디에 얼마나 넓게, 지금 실제로 켜져 있는가"만 답하면
    /// <see cref="LitZoneRegistry"/>에 등록해서 누구든 위치로 조회할 수 있게 된다.
    ///
    /// Domain 소속 인터페이스지만 구현체는 대개 MonoBehaviour다 —
    /// Assembly-CSharp이 Survive.Domain을 참조하므로 구현에는 문제가 없다.
    /// </summary>
    public interface ILitZoneSource
    {
        /// <summary>구역의 중심.</summary>
        Vector3 LitZoneCenter { get; }

        /// <summary>구역의 반경.</summary>
        float LitZoneRadius { get; }

        /// <summary>
        /// 지금 실제로 빛나고 있는가. 세워져 있다는 것과는 다르다 —
        /// 연료가 떨어진 화톳불은 여기서 false를 돌려줘야 한다.
        /// </summary>
        bool IsLit { get; }
    }
}
