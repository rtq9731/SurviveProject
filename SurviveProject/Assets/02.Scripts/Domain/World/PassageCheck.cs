namespace Survive.World
{
    /// <summary>지날 수 있는지, 아니면 왜 안 되는지.</summary>
    public enum PassageResult
    {
        /// <summary>지날 수 있다.</summary>
        Ok,

        /// <summary>그 위협을 뚫는 장비가 아예 없다.</summary>
        MissingGear,

        /// <summary>장비는 있는데 감당하는 크기가 모자라다.</summary>
        NotEnough,
    }

    /// <summary>
    /// 한 구간에 대한 판정 결과. "지날 수 있는가"와 "무엇이 필요한가"를 함께 답한다 —
    /// 스펙 §4가 Domain에 두라고 한 두 물음이 그것이다.
    /// </summary>
    public readonly struct PassageCheck
    {
        public readonly PassageResult Result;

        /// <summary>무엇이 막고 있는가. 통과한 경우 <see cref="EnvironmentHazard.None"/>.</summary>
        public readonly EnvironmentHazard Hazard;

        /// <summary>그것을 뚫는 장비. 통과한 경우 <see cref="TraversalGear.None"/>.</summary>
        public readonly TraversalGear RequiredGear;

        /// <summary>
        /// 얼마나 모자라는가. 장비가 아예 없으면 위협의 크기 전부, 모자라면 그 차이,
        /// 통과한 경우 0. 단위는 <see cref="HazardZone.Magnitude"/>와 같다.
        /// </summary>
        public readonly float Shortfall;

        PassageCheck(PassageResult result, EnvironmentHazard hazard, TraversalGear requiredGear, float shortfall)
        {
            Result = result;
            Hazard = hazard;
            RequiredGear = requiredGear;
            Shortfall = shortfall;
        }

        public bool CanPass => Result == PassageResult.Ok;

        public static PassageCheck Passable() =>
            new PassageCheck(PassageResult.Ok, EnvironmentHazard.None, TraversalGear.None, 0f);

        public static PassageCheck Missing(EnvironmentHazard hazard, TraversalGear gear, float magnitude) =>
            new PassageCheck(PassageResult.MissingGear, hazard, gear, magnitude);

        public static PassageCheck Short(EnvironmentHazard hazard, TraversalGear gear, float shortfall) =>
            new PassageCheck(PassageResult.NotEnough, hazard, gear, shortfall);
    }
}
