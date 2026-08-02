namespace Survive.Vitals
{
    /// <summary>
    /// 환경이 산소에 거는 보정. 양수는 회복, 음수는 추가 소모.
    /// 여러 개가 겹치면 가장 유리한 값 하나만 쓴다 (합산하지 않는다).
    /// </summary>
    public interface IOxygenModifier
    {
        float OxygenDeltaPerSecond { get; }
    }
}
