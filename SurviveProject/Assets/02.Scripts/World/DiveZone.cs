using System.Collections.Generic;
using UnityEngine;

namespace Survive.World
{
    /// <summary>
    /// 잠수 통로 하나 (실행 스펙 §8-1). 매크로늄 방호복을 걸쳐야 들어간다.
    ///
    /// <b>여기서는 막는 사실만 내놓는다.</b> 지날 수 있는지 답하는 것은
    /// <see cref="HazardZoneRegistry"/>이고 규칙은 <see cref="DiveRule"/>에 있다 —
    /// <see cref="MacroniumSurfaceZone"/>과 같은 모양이라 씬에 몇 개를 놓든 배선이 늘지 않는다.
    ///
    /// <b>입구는 높이 하나로 적는다.</b> 구역 중심의 높이가 <see cref="MouthY"/>,
    /// 즉 "여기부터가 물속"인 선이다. 방호복이 없는 사람은 그 선 아래로 내려가지
    /// 못하고, <b>죽지 않는다</b> — 위협 계층 원칙(환경은 죽이지 않고 생물만 죽인다).
    /// 그 밀어내기는 <see cref="DiveGateService"/>가 한다.
    ///
    /// <b>왜 통로 길이가 아니라 시간인가.</b> 판정은 장비의 용량과 구간의 크기를
    /// 견주는 것 하나뿐이고(<see cref="EnvironmentThreat"/>), 방호복이 내놓는 것은
    /// 물속에서 버티는 <b>시간</b>이다. 길이로 적으면 헤엄 속도가 판정 안에
    /// 숨어 들어가 튜닝 손잡이가 둘로 갈린다. 미터로 재고 싶은 사람은
    /// <see cref="DiveRule.PassageSecondsFor"/>로 옮겨 적는다.
    ///
    /// <b>씬에는 아직 놓여 있지 않다.</b> 통로의 배치는 사람의 몫이라
    /// (실행 스펙 §9) 이 컴포넌트는 검증이 런타임에 세워서 쓴다(<see cref="Setup"/>).
    /// 몇 미터짜리로 파야 하는지는 <see cref="DiveRule.FirstDivePassageMeters"/>가 답한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class DiveZone : MonoBehaviour, IHazardZoneSource
    {
        static readonly List<DiveZone> _all = new List<DiveZone>();

        [Tooltip("통로 입구가 걸린 반경(m). 이 안에 들어오면 판정이 걸린다")]
        [Min(0f)] [SerializeField] float radius = 12f;

        [Tooltip("정상 속도로 통로를 지나는 데 걸리는 시간(초). 방호복의 용량이 이 값 이상이어야 지난다")]
        [Min(0f)] [SerializeField] float passageSeconds = 36f;

        [Tooltip("중심을 오브젝트 원점에서 옮긴다. 중심의 높이가 곧 입구의 수면이다")]
        [SerializeField] Vector3 centerOffset = Vector3.zero;

        public Vector3 HazardZoneCenter => transform.TransformPoint(centerOffset);
        public float HazardZoneRadius => radius;

        // 이 구역이 막는 것은 하나로 고정한다 — MacroniumSurfaceZone과 같은 이유다.
        public EnvironmentHazard Hazard => EnvironmentHazard.Submersion;

        public float Magnitude => passageSeconds;

        /// <summary>판정에 넘길 구간 하나.</summary>
        public HazardZone Zone => new HazardZone(Hazard, passageSeconds);

        /// <summary>입구의 수면. 발이 여기를 넘어 내려가면 들어간 것이다.</summary>
        public float MouthY => HazardZoneCenter.y;

        /// <summary>이 통로의 길이(m). 씬에 놓는 사람이 읽는 값이다.</summary>
        public float PassageMeters => DiveRule.PassageMetersFor(passageSeconds);

        /// <summary>이 통로가 첫 잠수의 규격(도착 잔량 0~20%)을 지키는가.</summary>
        public bool IsFirstDiveTuned => DiveRule.IsFirstDiveTuned(passageSeconds);

        /// <summary>이 지점이 통로 입구 위(수평으로)인가. 높이는 보지 않는다.</summary>
        public bool ContainsHorizontally(Vector3 p)
        {
            var c = HazardZoneCenter;
            float dx = p.x - c.x, dz = p.z - c.z;
            return dx * dx + dz * dz <= radius * radius;
        }

        void OnEnable()
        {
            HazardZoneRegistry.Register(this);
            if (!_all.Contains(this)) _all.Add(this);
        }

        void OnDisable()
        {
            HazardZoneRegistry.Unregister(this);
            _all.Remove(this);
        }

        /// <summary>
        /// 이 지점의 통로. 없으면 false. 겹치면 입구가 가장 높은 것을 쓴다 —
        /// <see cref="MacroniumSurfaceZone.TryGetSurfaceAt"/>와 같은 규칙이다.
        /// </summary>
        public static bool TryGetAt(Vector3 p, out DiveZone zone)
        {
            zone = null;
            float best = float.MinValue;

            for (int i = 0; i < _all.Count; i++)
            {
                var z = _all[i];
                if (z == null || !z.ContainsHorizontally(p)) continue;
                if (zone == null || z.MouthY > best) { best = z.MouthY; zone = z; }
            }
            return zone != null;
        }

        /// <summary>
        /// 코드로 세울 때 값을 넣는다. 검증이 런타임에 통로를 스폰하기 위한 것이다 —
        /// <see cref="MacroniumSurfaceZone.Setup"/>이 같은 이유로 열려 있다.
        /// </summary>
        public void Setup(float zoneRadius, float seconds, Vector3 offset = default)
        {
            radius = Mathf.Max(0f, zoneRadius);
            passageSeconds = Mathf.Max(0f, seconds);
            centerOffset = offset;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            // 입구는 물 아래라 씬 뷰에서 잘 안 보인다. 입구와 그 아래 통로 끝을 같이 그린다.
            var mouth = HazardZoneCenter;
            Gizmos.color = new Color(0.36f, 0.72f, 0.80f, 0.40f);
            Gizmos.DrawWireSphere(mouth, radius);

            var far = mouth + Vector3.down * PassageMeters;
            Gizmos.color = new Color(0.18f, 0.42f, 0.50f, 0.30f);
            Gizmos.DrawLine(mouth, far);
        }
#endif
    }
}
