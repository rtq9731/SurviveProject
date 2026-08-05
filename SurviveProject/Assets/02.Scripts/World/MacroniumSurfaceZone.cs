using System.Collections.Generic;
using UnityEngine;

namespace Survive.World
{
    /// <summary>
    /// 매크로늄 액면이 깔린 구역. P2 스펙 §3의 네 번째 관문 — 액면섬을 막는 것이 이것이다.
    ///
    /// 표면장력이 강해 뚫고 갈 수 없고(로드맵 §4.3), 액면 보행 장비를 갖춰야 지난다.
    /// 여기서는 <b>막는 사실만</b> 내놓는다. 실제로 지날 수 있는지 답하는 것은
    /// <see cref="HazardZoneRegistry"/>이고, 그 판정은 <see cref="EnvironmentThreat"/>가 한다 —
    /// 이 컴포넌트가 이동 쪽을 직접 참조하지 않으므로 씬에 몇 개를 놓든 배선이 늘지 않는다.
    ///
    /// <see cref="Survive.Building.Campfire"/>가 <see cref="ILitZoneSource"/>로 자신을 내놓는 것과
    /// 같은 모양이다 — 등록·해제를 OnEnable/OnDisable에 두면 생명주기를 따로 신경 쓸 필요가 없다.
    ///
    /// <b>액면의 높이도 여기서 답한다.</b> 관문 판정에는 필요 없지만 "닿았는가"에는 필요하다
    /// (<see cref="MacroniumContact"/>). 물이 <see cref="WaterBody.TryGetSurfaceAt"/>로
    /// 수면을 내놓는 것과 같은 모양으로 맞췄다 — 잠김 판정이 두 가지 방식으로 갈리면
    /// 물에서 되는 것이 액면에서 안 되는 일이 생긴다.
    /// </summary>
    [DisallowMultipleComponent]
    public class MacroniumSurfaceZone : MonoBehaviour, IHazardZoneSource
    {
        static readonly List<MacroniumSurfaceZone> _all = new List<MacroniumSurfaceZone>();

        [Tooltip("액면이 깔린 반경(m). 이 안에 들어오면 판정이 걸린다")]
        [Min(0f)] [SerializeField] float radius = 18f;

        [Tooltip("건너야 하는 폭(m). 액면 보행 장비의 용량이 이 값 이상이어야 지난다")]
        [Min(0f)] [SerializeField] float crossingWidth = 30f;

        [Tooltip("중심을 오브젝트 원점에서 옮긴다. 액면은 발밑이라 보통 아래로 내린다")]
        [SerializeField] Vector3 centerOffset = Vector3.zero;

        public Vector3 HazardZoneCenter => transform.TransformPoint(centerOffset);
        public float HazardZoneRadius => radius;

        // 이 구역이 막는 것은 하나로 고정한다. 아무 위협이나 담을 수 있게 하면
        // 씬에서 잘못 고른 것을 컴파일도 테스트도 잡아 주지 못한다.
        public EnvironmentHazard Hazard => EnvironmentHazard.MacroniumSurface;

        public float Magnitude => crossingWidth;

        /// <summary>판정에 넘길 구간 하나. 위협과 크기를 짝지어 내놓는 것이 전부다.</summary>
        public HazardZone Zone => new HazardZone(Hazard, crossingWidth);

        /// <summary>
        /// 액면의 높이. 구역의 중심이 곧 액면이다 —
        /// <see cref="centerOffset"/>의 툴팁이 말하는 "발밑으로 내린 중심"이 그 자리다.
        /// </summary>
        public float SurfaceY => HazardZoneCenter.y;

        /// <summary>이 지점이 액면 위(수평으로)인가. 높이는 보지 않는다.</summary>
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
        /// 이 지점의 액면 높이. 액면이 없으면 false.
        /// 겹치면 가장 높은 액면을 쓴다 — <see cref="WaterBody.TryGetSurfaceAt"/>와 같은 규칙이다.
        /// </summary>
        public static bool TryGetSurfaceAt(Vector3 p, out float surfaceY, out MacroniumSurfaceZone zone)
        {
            surfaceY = float.MinValue;
            zone = null;

            for (int i = 0; i < _all.Count; i++)
            {
                var z = _all[i];
                if (z == null || !z.ContainsHorizontally(p)) continue;

                float s = z.SurfaceY;
                if (zone == null || s > surfaceY) { surfaceY = s; zone = z; }
            }
            return zone != null;
        }

        /// <summary>
        /// 코드로 세울 때 값을 넣는다. 검증이 런타임에 구역을 스폰하기 위한 것이다 —
        /// <see cref="Survive.Interaction.ItemPickup.Setup"/>이 같은 이유로 열려 있다.
        /// </summary>
        public void Setup(float zoneRadius, float width, Vector3 offset = default)
        {
            radius = Mathf.Max(0f, zoneRadius);
            crossingWidth = Mathf.Max(0f, width);
            centerOffset = offset;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            // 액면은 어두운 데다 포그가 짙어 씬 뷰에서 경계가 안 보인다.
            // 매크로늄 색(ArtPalette.Macronium #A12EE0)을 그대로 쓴다.
            Gizmos.color = new Color(0.63f, 0.18f, 0.88f, 0.35f);
            Gizmos.DrawWireSphere(HazardZoneCenter, radius);
        }
#endif
    }
}
