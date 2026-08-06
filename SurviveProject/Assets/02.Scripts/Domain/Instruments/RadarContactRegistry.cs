using System.Collections.Generic;
using UnityEngine;

namespace Survive.Instruments
{
    /// <summary>
    /// 레이더 앞에 설 수 있는 것. 섬이든 공동이든 낫이든 <b>같은 창구로</b> 들어온다.
    ///
    /// 종류마다 다른 통로를 내면 "낫은 애초에 등록을 안 한다"는 손쉬운 길이 생기고,
    /// 그 순간 레이더가 낫을 못 잡는 이유가 물성이 아니라 배선이 된다.
    /// 낫도 똑같이 등록하고 똑같이 판정에서 떨어져야 규칙이 규칙이다.
    /// </summary>
    public interface IRadarContactSource
    {
        /// <summary>번역 표에서 이름을 찾는 열쇠.</summary>
        string ContactId { get; }

        RadarContactKind ContactKind { get; }

        /// <summary>세계에서의 자리.</summary>
        Vector3 ContactPosition { get; }

        /// <summary>가장 긴 쪽의 길이(m).</summary>
        float ContactSizeMeters { get; }

        /// <summary>지금 움직이는 빠르기(m/s).</summary>
        float ContactSpeedMps { get; }

        /// <summary>지표 아래로 들어간 깊이(m). 물 위나 지표면이면 0.</summary>
        float ContactDepthMeters { get; }
    }

    /// <summary>
    /// "여기서 훑으면 무엇이 걸리는가"를 답하는 창구.
    /// <see cref="Survive.World.LitZoneRegistry"/>와 같은 자리다 — 순수 정적 등록부이고,
    /// 등록·해제는 소스의 OnEnable/OnDisable이 한다.
    ///
    /// <b>여기서는 거르지 않는다.</b> 후보를 물리량으로 옮겨 담기만 하고,
    /// 잡히느냐 마느냐는 전부 <see cref="RadarDetection"/>이 정한다. 거르는 자리가
    /// 둘이면 언젠가 둘이 어긋난다.
    /// </summary>
    public static class RadarContactRegistry
    {
        static readonly List<IRadarContactSource> _sources = new List<IRadarContactSource>();

        public static void Register(IRadarContactSource source)
        {
            if (source == null) return;
            if (!_sources.Contains(source)) _sources.Add(source);
        }

        public static void Unregister(IRadarContactSource source) => _sources.Remove(source);

        public static int Count => _sources.Count;

        /// <summary>테스트·씬 전환 사이에 상태를 비운다.</summary>
        public static void Clear() => _sources.Clear();

        /// <summary>이 자리에서 본 후보 전부. 거르지 않은 날것이다.</summary>
        public static List<RadarContact> Candidates(Vector3 from)
        {
            var list = new List<RadarContact>();

            for (int i = 0; i < _sources.Count; i++)
            {
                var s = _sources[i];
                if (s == null) continue;
                list.Add(Describe(s, from));
            }
            return list;
        }

        /// <summary>소스 하나를 물리량으로 옮겨 담는다.</summary>
        public static RadarContact Describe(IRadarContactSource source, Vector3 from)
        {
            var to = source.ContactPosition;
            var flat = new Vector3(to.x - from.x, 0f, to.z - from.z);

            return new RadarContact
            {
                id = source.ContactId,
                kind = source.ContactKind,
                sizeMeters = source.ContactSizeMeters,
                speedMps = source.ContactSpeedMps,
                depthMeters = source.ContactDepthMeters,
                distanceMeters = flat.magnitude,
                bearingDegrees = Bearing(flat),
            };
        }

        /// <summary>북쪽(+Z)이 0도, 시계 방향. 나침반과 같은 셈법이라야 사람이 읽는다.</summary>
        public static float Bearing(Vector3 flat)
        {
            if (flat.sqrMagnitude <= 0.0001f) return 0f;

            float deg = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            return deg < 0f ? deg + 360f : deg;
        }
    }
}
