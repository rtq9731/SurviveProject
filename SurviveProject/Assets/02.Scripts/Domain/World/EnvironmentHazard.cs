namespace Survive.World
{
    /// <summary>
    /// 무엇이 길을 막는가. P2 스펙 §4의 위협 넷이 그대로 값이 된다.
    ///
    /// 위협은 <b>막는 것</b>이지 깎는 것이 아니다 — 체력을 조금씩 닳게 하는 종류는
    /// 여기 들어오지 않는다. 그건 잡무이지 관문이 아니다(스펙 §4).
    ///
    /// 섬 사이 바다가 살을 깎는 것(<see cref="MacroniumSea"/>)은 이 목록에 없다.
    /// 그것은 지날 수 있는 곳에 값을 매기는 규칙이지 길을 막는 위협이 아니라서,
    /// "지날 수 있는가"를 묻는 이 열거형과는 물음 자체가 다르다.
    ///
    /// <b>"폭"도 없다.</b> 건너야 하는 거리는 세워 둔 것으로 메우는 것이고,
    /// 그것은 장비가 아니라 노동이다(기획서 §6.4). 위협이 되려면 그것을 뚫는 수단이
    /// <see cref="TraversalGear"/>에 자리를 가져야 하는데, 그 목록은 장비만 받는다.
    /// </summary>
    public enum EnvironmentHazard
    {
        /// <summary>막는 것이 없다. 착지섬 안쪽처럼 그냥 걸어 다니는 곳.</summary>
        None,

        /// <summary>어둠 — 광원 밖은 지형도 낙하도 길도 감춰진다.</summary>
        Darkness,

        /// <summary>수심 — 산소가 다하면 익사한다.</summary>
        Depth,

        /// <summary>
        /// 잠수 — 물 <b>속으로 들어가</b> 통로를 지나야 한다.
        ///
        /// <see cref="Depth"/>와 물질이 같고 묻는 것이 다르다. 수심은
        /// "머리가 잠긴 동안 숨이 버티는가"를 묻고 맨몸으로도 답이 나온다 —
        /// 강을 건너며 숨이 시계라는 것을 배우는 자리라 관문이 아니다.
        /// 잠수는 "바닥으로 내려가 저쪽으로 나올 수 있는가"를 묻고,
        /// 방호복이 없으면 애초에 시작되지 않는다.
        ///
        /// <b>이 위협은 죽이지 않는다.</b> 위협 계층 원칙 — 환경은 죽이지 않고
        /// 생물만 죽인다(기획서 갱신점 _3 §2). 장비 없이 들어가려 하면
        /// 물이 몸을 밀어내지, 익사시키지 않는다(<see cref="DiveRule"/>).
        ///
        /// 크기의 단위는 <b>초</b> — 통로를 정상 속도로 지나는 데 걸리는 시간이다.
        /// </summary>
        Submersion,

        /// <summary>매크로늄 액면 — 표면장력이 강해 뚫고 갈 수 없다.</summary>
        MacroniumSurface,

        /// <summary>
        /// 짙은 매크로늄 <b>층</b> — 액면 아래로 이만큼의 두께가 막고 있다.
        ///
        /// <see cref="MacroniumSurface"/>와 같은 물질이고 묻는 방향만 다르다.
        /// 액면은 "가로로 얼마나 건너야 하는가"를 묻고, 층은 "세로로 얼마나 뚫어야 하는가"를 묻는다.
        /// 기획서 §6.4 "구역 경계의 물리적 정체는 매크로늄 층이고, 통과 수단은 그 층을 뚫는 장비다".
        ///
        /// 챕터 1의 종막이 이것이다 — 뚫고 내려가면 부유섬이 끝난다 (§6.2).
        /// </summary>
        MacroniumLayer,
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
    /// <item><see cref="EnvironmentHazard.Submersion"/> — 잠수 통로를 지나는 데 걸리는 시간(초)</item>
    /// <item><see cref="EnvironmentHazard.MacroniumSurface"/> — 액면 위로 지나야 하는 폭(m)</item>
    /// <item><see cref="EnvironmentHazard.MacroniumLayer"/> — 뚫고 내려가야 하는 층의 두께(m)</item>
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
