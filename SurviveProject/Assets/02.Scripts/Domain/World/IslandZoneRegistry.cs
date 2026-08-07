using System.Collections.Generic;
using UnityEngine;

namespace Survive.World
{
    /// <summary>
    /// 구역 하나를 차지하고 있는 존재. 사람이 지형을 놓을 때(§16) 구역마다 하나씩
    /// 세우면 <see cref="IslandZoneRegistry"/>가 위치로 조회할 수 있게 된다.
    ///
    /// <see cref="IHazardZoneSource"/>·<see cref="ILitZoneSource"/>와 같은 모양이다 —
    /// Domain 소속 인터페이스지만 구현체는 대개 MonoBehaviour이고, 등록·해제는
    /// OnEnable/OnDisable에서 한다.
    /// </summary>
    public interface IIslandZoneSource
    {
        /// <summary>구역의 중심.</summary>
        Vector3 IslandZoneCenter { get; }

        /// <summary>구역의 반경.</summary>
        float IslandZoneRadius { get; }

        /// <summary>어느 구역인가.</summary>
        IslandZone Zone { get; }
    }

    /// <summary>
    /// "이 자리는 어느 구역인가"를 답하는 창구.
    ///
    /// <b>왜 필요한가.</b> <see cref="IslandArrival"/>은 구역 하나를 받아 걸쇠를 채우는
    /// 순수한 물건이고, 그 구역이 어디서 오는지는 답하지 않는다. §5가 각성을 얹으려면
    /// <b>지금 서 있는 자리가 B섬인가</b>를 물을 수 있어야 하고, 그 물음의 자리가 여기다.
    ///
    /// <b>겹치면 나중에 등록된 것이 이긴다.</b> <see cref="HazardZoneRegistry"/>가
    /// "하나라도 막으면 막힌다"로 겹침을 푸는 것과 다른 이유는, 구역은 막는 것이 아니라
    /// <b>어디에 있는가</b>이기 때문이다 — 위험은 합쳐지지만 자리는 하나뿐이다.
    /// 안쪽에 놓인 좁은 구역이 바깥의 넓은 구역을 덮을 수 있어야 강이 A섬 안을
    /// 가로지를 수 있다.
    /// </summary>
    public static class IslandZoneRegistry
    {
        static readonly List<IIslandZoneSource> _sources = new List<IIslandZoneSource>();

        public static void Register(IIslandZoneSource source)
        {
            if (source == null) return;
            if (!_sources.Contains(source)) _sources.Add(source);
        }

        public static void Unregister(IIslandZoneSource source) => _sources.Remove(source);

        /// <summary>
        /// 이 자리가 어느 구역인가. 아무 구역에도 들지 않으면 false —
        /// <b>모르는 것을 A-1로 치지 않는다.</b> 구역이 아직 안 놓인 세계에서
        /// "여기는 시작 지점"이라고 답하면 §5가 잘못된 자리에서 각성한다.
        /// </summary>
        public static bool TryAt(Vector3 position, out IslandZone zone)
        {
            zone = default;

            bool found = false;
            for (int i = 0; i < _sources.Count; i++)
            {
                var source = _sources[i];
                if (source == null) continue;

                float r = source.IslandZoneRadius;
                if ((position - source.IslandZoneCenter).sqrMagnitude > r * r) continue;

                zone = source.Zone;
                found = true;
            }
            return found;
        }

        /// <summary>테스트·씬 전환 사이에 상태를 비운다.</summary>
        public static void Clear() => _sources.Clear();
    }
}
