using UnityEngine;

namespace Survive.World
{
    /// <summary>
    /// 위협이 걸린 구역 하나를 내놓는 존재. 섬 사이의 물목이든, 매크로늄 액면이든,
    /// "어디에 얼마나 넓게, 무엇이 얼마나 세게 막는가"만 답하면
    /// <see cref="HazardZoneRegistry"/>에 등록해서 누구든 위치로 조회할 수 있게 된다.
    ///
    /// <see cref="ILitZoneSource"/>가 P1에서 잡은 패턴을 그대로 따른다 —
    /// Domain 소속 인터페이스지만 구현체는 대개 MonoBehaviour이고,
    /// 등록·해제는 OnEnable/OnDisable에서 하면 생명주기를 따로 신경 쓸 필요가 없다.
    /// </summary>
    public interface IHazardZoneSource
    {
        /// <summary>구역의 중심.</summary>
        Vector3 HazardZoneCenter { get; }

        /// <summary>구역의 반경.</summary>
        float HazardZoneRadius { get; }

        /// <summary>무엇이 막는가.</summary>
        EnvironmentHazard Hazard { get; }

        /// <summary>얼마나 막는가. 단위는 <see cref="HazardZone.Magnitude"/>와 같다.</summary>
        float Magnitude { get; }
    }
}
