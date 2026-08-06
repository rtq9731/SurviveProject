using Survive.Localization;

namespace Survive.Building
{
    /// <summary>배치가 가능한지, 아니면 왜 안 되는지.</summary>
    public enum PlacementResult
    {
        Ok,

        /// <summary>바라보는 곳에 놓을 면이 없다.</summary>
        NoSurface,

        /// <summary>너무 가파르다.</summary>
        TooSteep,

        /// <summary>다른 건축물과 겹친다.</summary>
        Blocked,

        /// <summary>재료가 모자라다.</summary>
        NotEnoughResources,

        /// <summary>이 배치 모드가 요구하는 바닥이 아니다.</summary>
        WrongSurface,

        /// <summary>모듈 조각인데 붙일 자리가 근처에 없다.</summary>
        NoAnchor,

        /// <summary>그 자리에는 같은 조각이 이미 있다.</summary>
        SlotTaken,

        /// <summary>지을 줄 모른다 — 청사진이 아직 안 열렸다.</summary>
        NotResearched,
    }

    public static class PlacementCheckText
    {
        /// <summary>
        /// 화면에 그대로 띄울 수 있는 한 줄.
        ///
        /// 까닭을 덧붙일 때는 쌍점으로 잇는다. 줄표(em dash)는 본문 글꼴(ChosunGu)에
        /// 없어 화면에 두부(□)로 떴다 — 화면에 나가는 문자는 글꼴이 아는 것만 쓴다.
        /// </summary>
        public static string Describe(PlacementResult r) => r switch
        {
            PlacementResult.Ok => "",
            PlacementResult.NoSurface => Loc.T("Build", "reject_no_surface"),
            PlacementResult.TooSteep => Loc.T("Build", "reject_too_steep"),
            PlacementResult.Blocked => Loc.T("Build", "reject_blocked"),
            PlacementResult.NotEnoughResources => Loc.T("Build", "reject_not_enough"),
            PlacementResult.WrongSurface => Loc.T("Build", "reject_wrong_surface"),
            PlacementResult.NoAnchor => Loc.T("Build", "reject_no_anchor"),
            PlacementResult.SlotTaken => Loc.T("Build", "reject_slot_taken"),
            PlacementResult.NotResearched => Loc.T("Build", "reject_not_researched"),
            _ => "",
        };
    }
}
