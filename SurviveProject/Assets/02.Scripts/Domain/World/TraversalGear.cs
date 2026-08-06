namespace Survive.World
{
    /// <summary>
    /// 무엇이 그 위협을 뚫는가. 기획서 §6.4의 "뚫는 것" 열이 그대로 값이 된다.
    /// 이 열거형의 순서가 곧 진행 순서다.
    ///
    /// <b>여기 들어오는 것은 장비뿐이다.</b> 기획서 §6.4의 관통 원칙 —
    /// 게이트는 장비여야 하고 노동이면 안 된다. 장비는 "전에 못 하던 것을 하게 됨"이고
    /// 노동은 "시간을 냄"이다. 세워 두는 건축물은 통과해도 새로 할 수 있는 일을
    /// 주지 않으므로 관문의 수단이 될 수 없다. 다리가 이 목록에서 빠진 이유가 그것이다
    /// (건축물로는 남는다 — 강제가 아니라 편의시설이다).
    ///
    /// <b>값을 지울 때 주의.</b> <see cref="Survive.Items.TraversalGearItemSO.gear"/>가
    /// 이것을 정수로 직렬화한다. 중간 값을 빼면 뒤의 에셋이 조용히 다른 장비를 가리키므로,
    /// 지운 다음에는 에셋의 <c>gear:</c> 값을 함께 밀어야 한다.
    /// 그 대응은 <c>BridgeRetirementTests</c>의 장비 에셋 정합 검사가 지킨다.
    /// </summary>
    public enum TraversalGear
    {
        /// <summary>아무것도 필요 없다 — 걷기.</summary>
        None,

        /// <summary>랜턴. 어둠을 뚫는다.</summary>
        Lantern,

        /// <summary>수영(산소 용량 포함). 수심을 뚫는다.</summary>
        Swimming,

        /// <summary>액면 보행 장비. 매크로늄 액면을 뚫는다.</summary>
        SurfaceWalker,

        /// <summary>
        /// 돌파정. 짙은 매크로늄 층을 <b>뚫고 내려간다</b> (기획서 §5.4·§6.2).
        ///
        /// 액면 보행 장비와 상대하는 물질이 같고 방향만 다르다 — 하나는 위를 걷고
        /// 하나는 아래로 지난다. 같은 자리에서 둘 중 무엇을 할지는 플레이어가 고른다
        /// (<see cref="MacroniumContact"/> 참고).
        ///
        /// 티어의 끝이자 챕터 1의 출구다 — 4번 섬에서만 나오는 매크로늄으로 만든다.
        /// </summary>
        BreachCraft,
    }

    /// <summary>
    /// 갖춘 장비 하나와 그것이 감당하는 크기.
    ///
    /// 용량의 단위는 상대하는 위협의 단위와 같다 — 랜턴은 밝히는 거리(m),
    /// 수영은 버틸 수 있는 시간(초), 액면 보행 장비는 감당하는 폭(m),
    /// 돌파정은 뚫고 내려갈 수 있는 층의 두께(m).
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
