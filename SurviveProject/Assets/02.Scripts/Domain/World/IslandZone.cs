namespace Survive.World
{
    /// <summary>
    /// A섬이 갈린 구역 다섯과, 그 바깥의 바다·B섬.
    ///
    /// <b>갈라 놓은 것은 지면이 아니라 액체다</b>(기획서 §2.1). A-1·A-2·A-3은 같은 섬의
    /// 같은 땅이고, 그 사이를 가르는 것은 강과 얕은 바다다. 그래서 이 열거형에는
    /// 「섬 셋」이 아니라 「구역 다섯」이 들어간다 — 값의 순서가 곧 걸어 나가는 순서다.
    ///
    /// <b>이름은 여기 적지 않는다.</b> 화면에 나갈 글자는 번역 표가 가진다
    /// (<see cref="IslandZones.NameKey"/>).
    /// </summary>
    public enum IslandZone
    {
        /// <summary>시작 지점.</summary>
        A1,

        /// <summary>A섬 내부를 가로지르는 강. 수영 — 잠기면 살이 깎인다.</summary>
        River,

        /// <summary>강 건너.</summary>
        A2,

        /// <summary>발목 깊이. 그냥 걸어서 건넌다 — 무해하다.</summary>
        ShallowSea,

        /// <summary>레이더가 있는 자리.</summary>
        A3,

        /// <summary>A섬과 B섬을 가르는 묽은 층. 넓다 — 헤엄쳐서는 못 건넌다.</summary>
        Sea,

        /// <summary>B섬. 여기 <b>닿는 것</b>이 §5의 각성 방아쇠다.</summary>
        IslandB,
    }
}
