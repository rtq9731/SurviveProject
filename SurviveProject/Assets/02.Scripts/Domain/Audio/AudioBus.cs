namespace Survive.Domain.Audio
{
    /// <summary>
    /// 소리가 흘러 들어가는 갈래. 믹서 그룹 하나에 대응한다.
    ///
    /// <b>왜 코드에 열거형으로 두는가.</b> 믹서 에셋은 병합할 수 없는 이진 파일이고,
    /// 아직 이 저장소에 없다. 갈래를 에셋이 아니라 여기에 두면 믹서가 없어도
    /// 갈래별 볼륨이 성립하고(코드 쪽 배율), 나중에 믹서를 넣으면 이름으로 붙는다.
    /// 소리를 부르는 쪽은 어느 쪽이든 이 값 하나만 말하면 된다.
    /// </summary>
    public enum AudioBus
    {
        /// <summary>전체. 다른 갈래에도 곱해진다.</summary>
        Master = 0,

        /// <summary>효과음 — 발소리·타격·줍기처럼 한 번 나고 마는 것.</summary>
        Sfx = 1,

        /// <summary>환경음 — 불 타는 소리, 다가오는 것의 기척처럼 계속 깔리는 것.</summary>
        Ambient = 2,

        /// <summary>화면 소리. 세계 안에서 나는 것이 아니므로 언제나 2D다.</summary>
        UI = 3,
    }
}
