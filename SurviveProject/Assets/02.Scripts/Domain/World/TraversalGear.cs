namespace Survive.World
{
    /// <summary>
    /// 무엇이 그 위협을 뚫는가. P2 스펙 §3·§4의 "뚫는 것" 열이 그대로 값이 된다.
    /// 섬 번호 = 티어 번호이므로 이 열거형의 순서가 곧 진행 순서다.
    ///
    /// <see cref="Bridge"/>는 몸에 걸치는 물건이 아니라 세워 둔 건축물이지만,
    /// 판정 쪽에서 보면 "그 구간을 뚫을 수단을 갖췄는가" 하나로 똑같다.
    /// 건축이 진행 경로가 되는 것이 스펙 §3의 의도이므로 같은 자리에 둔다.
    /// </summary>
    public enum TraversalGear
    {
        /// <summary>아무것도 필요 없다 — 걷기.</summary>
        None,

        /// <summary>랜턴. 어둠을 뚫는다.</summary>
        Lantern,

        /// <summary>수영(산소 용량 포함). 수심을 뚫는다.</summary>
        Swimming,

        /// <summary>세워 둔 다리. 폭을 뚫는다.</summary>
        Bridge,

        /// <summary>액면 보행 장비. 매크로늄 액면을 뚫는다.</summary>
        SurfaceWalker,
    }

    /// <summary>
    /// 갖춘 장비 하나와 그것이 감당하는 크기.
    ///
    /// 용량의 단위는 상대하는 위협의 단위와 같다 — 랜턴은 밝히는 거리(m),
    /// 수영은 버틸 수 있는 시간(초), 다리는 잇는 폭(m), 액면 보행 장비는 감당하는 폭(m).
    /// </summary>
    public readonly struct GearCapability
    {
        public readonly TraversalGear Gear;

        /// <summary>이 장비가 감당하는 크기. <see cref="HazardZone.Magnitude"/>와 같은 단위.</summary>
        public readonly float Capacity;

        public GearCapability(TraversalGear gear, float capacity)
        {
            Gear = gear;
            Capacity = capacity;
        }
    }
}
