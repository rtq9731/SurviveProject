namespace Survive.World
{
    /// <summary>
    /// 무엇이 길을 막는가. P2 스펙 §4의 위협 넷이 그대로 값이 된다.
    ///
    /// 위협은 <b>막는 것</b>이지 깎는 것이 아니다 — 체력을 조금씩 닳게 하는 종류는
    /// 여기 들어오지 않는다. 그건 잡무이지 관문이 아니다(스펙 §4).
    /// </summary>
    public enum EnvironmentHazard
    {
        /// <summary>막는 것이 없다. 착지섬 안쪽처럼 그냥 걸어 다니는 곳.</summary>
        None,

        /// <summary>어둠 — 광원 밖은 지형도 낙하도 길도 감춰진다.</summary>
        Darkness,

        /// <summary>수심 — 산소가 다하면 익사한다.</summary>
        Depth,

        /// <summary>폭 — 물리적으로 건널 수 없다.</summary>
        Gap,

        /// <summary>매크로늄 액면 — 표면장력이 강해 뚫고 갈 수 없다.</summary>
        MacroniumSurface,
    }

    /// <summary>
    /// 위협 하나가 걸린 구간. "여기를 지나려면 무엇이 얼마나 필요한가"를 데이터로 적은 것이다.
    ///
    /// <see cref="Magnitude"/>의 단위는 위협마다 다르다 — 하나의 수로 통일한 이유는
    /// 판정을 위협마다 특수 처리하지 않기 위해서다. 장비도 같은 단위의 용량
    /// (<see cref="GearCapability.Capacity"/>)을 내놓고, 판정은 둘을 비교하는 것으로 끝난다.
    ///
    /// <list type="bullet">
    /// <item><see cref="EnvironmentHazard.Darkness"/> — 광원 없이 지나야 하는 거리(m)</item>
    /// <item><see cref="EnvironmentHazard.Depth"/> — 물속에 머물러야 하는 시간(초)</item>
    /// <item><see cref="EnvironmentHazard.Gap"/> — 건너야 하는 폭(m)</item>
    /// <item><see cref="EnvironmentHazard.MacroniumSurface"/> — 액면 위로 지나야 하는 폭(m)</item>
    /// <item><see cref="EnvironmentHazard.None"/> — 쓰이지 않는다</item>
    /// </list>
    /// </summary>
    public readonly struct HazardZone
    {
        public readonly EnvironmentHazard Hazard;

        /// <summary>위협의 크기. 단위는 위협마다 다르다 — 위 목록 참고.</summary>
        public readonly float Magnitude;

        public HazardZone(EnvironmentHazard hazard, float magnitude)
        {
            Hazard = hazard;
            Magnitude = magnitude;
        }
    }
}
