using System.Collections.Generic;

namespace Survive.Vitals
{
    /// <summary>
    /// 산소 변화율 계산. 도메인 계층의 순수 정적 클래스라 EditMode로 검증한다.
    /// </summary>
    public static class OxygenRate
    {
        /// <summary>
        /// 보정이 하나도 없으면 기본 감소율, 있으면 그 중 가장 유리한(가장 큰) 값.
        /// 합산하지 않는 이유: 버섯 군락 안에 있으면 안전하다는 규칙이 읽히기 쉽다.
        /// </summary>
        public static float Calculate(float baseRate, IReadOnlyList<IOxygenModifier> modifiers)
        {
            if (modifiers == null || modifiers.Count == 0) return baseRate;

            bool 하나라도있음 = false;
            float 최댓값 = float.NegativeInfinity;

            for (int i = 0; i < modifiers.Count; i++)
            {
                var m = modifiers[i];
                if (m == null) continue;
                하나라도있음 = true;
                if (m.OxygenDeltaPerSecond > 최댓값) 최댓값 = m.OxygenDeltaPerSecond;
            }

            return 하나라도있음 ? 최댓값 : baseRate;
        }
    }
}
