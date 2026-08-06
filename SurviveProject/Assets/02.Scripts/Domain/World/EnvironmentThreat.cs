using System.Collections.Generic;

namespace Survive.World
{
    /// <summary>
    /// 환경 위협 판정. "이 구간을 지날 수 있는가, 어떤 장비가 필요한가"를 답한다.
    ///
    /// P2 스펙 §4 "판정은 Domain에" — Unity 없이 도는 순수 정적 클래스다.
    /// 위협마다 특수 처리를 두지 않는다: 위협은 종류 하나와 크기 하나로 적히고,
    /// 장비도 종류 하나와 용량 하나를 내놓는다. 판정은 <b>종류가 맞는가</b>와
    /// <b>용량이 크기 이상인가</b> 둘뿐이다. 위협이 늘어도 규칙은 늘지 않는다.
    /// </summary>
    public static class EnvironmentThreat
    {
        /// <summary>
        /// 이 위협을 뚫는 장비. 스펙 §3·§4의 "뚫는 것" 열 그대로다 —
        /// 위협 하나에 장비 하나가 1:1로 대응하므로 플레이어가 "지금 뭘 해야 하는지"
        /// 헷갈릴 여지가 없다(스펙 §3).
        /// </summary>
        public static TraversalGear RequiredGear(EnvironmentHazard hazard) => hazard switch
        {
            EnvironmentHazard.Darkness => TraversalGear.Lantern,
            EnvironmentHazard.Depth => TraversalGear.Swimming,
            EnvironmentHazard.MacroniumSurface => TraversalGear.SurfaceWalker,
            EnvironmentHazard.MacroniumLayer => TraversalGear.BreachPod,
            _ => TraversalGear.None,
        };

        /// <summary>
        /// 이 구간을 지금 갖춘 것으로 지날 수 있는가.
        ///
        /// 같은 장비가 목록에 여럿 들어 있으면 가장 유리한(용량이 가장 큰) 것을 쓴다 —
        /// <see cref="Survive.Vitals.OxygenRate"/>가 잡은 규칙과 같다. 합산하지 않는 이유도 같다:
        /// "더 좋은 랜턴을 들면 더 멀리 간다"가 "랜턴을 두 개 들면 두 배 간다"보다 읽기 쉽다.
        ///
        /// 경계값은 통과로 친다 — 용량이 위협의 크기와 정확히 같으면 지난다.
        /// </summary>
        public static PassageCheck Evaluate(HazardZone zone, IReadOnlyList<GearCapability> loadout)
        {
            var required = RequiredGear(zone.Hazard);
            if (required == TraversalGear.None) return PassageCheck.Passable();

            bool has = false;
            float best = 0f;

            if (loadout != null)
            {
                for (int i = 0; i < loadout.Count; i++)
                {
                    var entry = loadout[i];
                    if (entry.Gear != required) continue;
                    if (!has || entry.Capacity > best) best = entry.Capacity;
                    has = true;
                }
            }

            // 장비 자체가 없으면 크기와 무관하게 막힌다. 위협이 걸린 구간은
            // 그 위협을 뚫는 수단을 갖추는 것이 관문이지, 크기가 작다고 열리는 것이 아니다.
            if (!has) return PassageCheck.Missing(zone.Hazard, required, zone.Magnitude);

            if (best >= zone.Magnitude) return PassageCheck.Passable();
            return PassageCheck.Short(zone.Hazard, required, zone.Magnitude - best);
        }

        /// <summary>가부만 필요할 때.</summary>
        public static bool CanPass(HazardZone zone, IReadOnlyList<GearCapability> loadout) =>
            Evaluate(zone, loadout).CanPass;
    }
}
