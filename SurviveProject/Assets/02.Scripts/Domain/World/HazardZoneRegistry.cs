using System.Collections.Generic;
using UnityEngine;

namespace Survive.World
{
    /// <summary>
    /// "이 자리는 지날 수 있는가"를 답하는 창구. 스펙 §4가 Domain에 두라고 한 물음의
    /// 위치 버전이고, <see cref="LitZoneRegistry"/>와 같은 순수 C# 정적 등록부다.
    ///
    /// 이동 쪽(수영·건축·액면 보행)은 특정 구역 인스턴스를 참조할 필요가 없다 —
    /// <see cref="IHazardZoneSource"/>만 구현하면 무엇이든 똑같이 조회된다.
    /// </summary>
    public static class HazardZoneRegistry
    {
        static readonly List<IHazardZoneSource> _sources = new List<IHazardZoneSource>();

        public static void Register(IHazardZoneSource source)
        {
            if (source == null) return;
            if (!_sources.Contains(source)) _sources.Add(source);
        }

        public static void Unregister(IHazardZoneSource source) => _sources.Remove(source);

        /// <summary>
        /// 이 위치를 지금 갖춘 것으로 지날 수 있는가.
        ///
        /// 구역이 겹치면 <b>하나라도 막으면 막힌다</b>. 돌려주는 것은 그중 처음 걸린
        /// 것이다 — 등록 순서를 따르므로 결과가 흔들리지 않는다. 어느 쪽을 먼저
        /// 보여줄지 우열을 매기지 않는 이유는, 스펙 §3에서 구간마다 막는 것이 하나씩이라
        /// 겹침 자체가 예외 상황이기 때문이다.
        /// </summary>
        public static PassageCheck Evaluate(Vector3 position, IReadOnlyList<GearCapability> loadout)
        {
            for (int i = 0; i < _sources.Count; i++)
            {
                var source = _sources[i];
                if (source == null) continue;

                float r = source.HazardZoneRadius;
                if ((position - source.HazardZoneCenter).sqrMagnitude > r * r) continue;

                var check = EnvironmentThreat.Evaluate(new HazardZone(source.Hazard, source.Magnitude), loadout);
                if (!check.CanPass) return check;
            }
            return PassageCheck.Passable();
        }

        /// <summary>가부만 필요할 때.</summary>
        public static bool CanEnter(Vector3 position, IReadOnlyList<GearCapability> loadout) =>
            Evaluate(position, loadout).CanPass;

        /// <summary>테스트·씬 전환 사이에 상태를 비운다.</summary>
        public static void Clear() => _sources.Clear();
    }
}
