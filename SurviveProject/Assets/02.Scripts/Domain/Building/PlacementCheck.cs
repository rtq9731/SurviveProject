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
            _ => "",
        };
    }
}
