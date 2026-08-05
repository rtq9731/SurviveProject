namespace Survive.UI
{
    /// <summary>
    /// 패널의 종류. 배타 규칙은 패널 인스턴스가 아니라 이 종류로 적는다 —
    /// 규칙을 MonoBehaviour 참조 없이 표로 두려면 이름이 필요하다.
    ///
    /// 제작 목록이 둘로 나뉘어 있는 것은 실수가 아니다. 같은 컴포넌트지만
    /// 손으로 만드는 목록과 제작대에서 여는 목록은 화면에서 다른 물건이고,
    /// 보관함이 밀어내는 것은 그중 손 제작 쪽뿐이다.
    /// </summary>
    public enum UIPanelKind
    {
        /// <summary>규칙에 걸리지 않는 패널. 종류를 밝히지 않은 것들의 기본값이다.</summary>
        None = 0,
        Inventory = 1,
        HandCrafting = 2,
        StationCrafting = 3,
        Storage = 4,
        BuildMenu = 5,

        /// <summary>
        /// 분석 기록(도감). 아무것도 밀어내지 않는다 — 읽기만 하는 화면이라
        /// 소지품 위에 겹쳐 떠도 잃는 것이 없고, ESC 한 번에 이것만 걷히고
        /// 아래 있던 화면이 그대로 남는 편이 보던 흐름을 덜 끊는다.
        /// </summary>
        Codex = 6,
    }
}
