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
    /// </summary>
    [DisallowMultipleComponent]
    public class MacroniumSurfaceZone : MonoBehaviour, IHazardZoneSource
    {
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

        void OnEnable() => HazardZoneRegistry.Register(this);
        void OnDisable() => HazardZoneRegistry.Unregister(this);

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
