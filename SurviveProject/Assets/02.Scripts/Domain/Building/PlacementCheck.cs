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
    }

    public static class PlacementCheckText
    {
        /// <summary>화면에 그대로 띄울 수 있는 한 줄.</summary>
        public static string Describe(PlacementResult r) => r switch
        {
            PlacementResult.Ok => "",
            PlacementResult.NoSurface => "놓을 자리가 없다",
            PlacementResult.TooSteep => "너무 가파르다",
            PlacementResult.Blocked => "다른 것과 겹친다",
            PlacementResult.NotEnoughResources => "재료가 모자라다",
            PlacementResult.WrongSurface => "여기엔 놓을 수 없다",
            PlacementResult.NoAnchor => "붙일 곳이 없다 — 토대부터 놓아라",
            PlacementResult.SlotTaken => "그 자리엔 이미 있다",
            _ => "",
        };
    }
}
