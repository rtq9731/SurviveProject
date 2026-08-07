using UnityEngine;

namespace Survive.World
{
    /// <summary>
    /// 씬에 박혀 있는 광원 하나를 <see cref="LitZoneRegistry"/>의 밝은 구역으로 내놓는다.
    ///
    /// <b>주인 없는 빛에 주인을 붙이는 컴포넌트다.</b> 화톳불·랜턴·발광 군락은 저마다
    /// 자기를 등록하는 컴포넌트를 이미 달고 있다. 그런 주인이 없는 Light —
    /// 시작 지점의 빛기둥이 그것이다 — 는 화면만 밝히고 규칙에는 없었다.
    ///
    /// <b>씬을 고치지 않는다.</b> MainScene은 병합할 수 없는 단일 파일이라
    /// <see cref="FixedLightZoneService"/>가 실행 시점에 스스로 붙인다 —
    /// <see cref="GlowGroveService"/>·<see cref="MacroniumContactService"/>와 같은 이유다.
    ///
    /// 판정과 넓이는 여기 없다. <see cref="FixedLightRule"/>이 Unity 없이 답하고,
    /// 이 컴포넌트는 <b>재는 일</b>만 한다 — 빛이 어디 바닥에 닿는가를 광선으로 짚는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class FixedLightZone : MonoBehaviour, ILitZoneSource
    {
        Light _light;
        Vector3 _center;
        float _radius;

        /// <summary>구역을 실제로 잡았는가. 못 잡았으면 등록되지 않는다.</summary>
        public bool HasZone => _radius > 0f;

        /// <summary>이 구역을 내는 광원. 검증이 어느 빛인지 되짚을 때 쓴다.</summary>
        public Light Source => _light;

        public Vector3 LitZoneCenter => _center;

        public float LitZoneRadius => _radius;

        /// <summary>
        /// 지금 실제로 빛나고 있는가. 연료가 떨어진 화톳불이 false를 내는 것과 같은 물음이다 —
        /// 누군가 이 광원을 끄면 그 자리는 곧바로 어두워져야 한다.
        /// </summary>
        public bool IsLit => _light != null && _light.enabled &&
                             _light.gameObject.activeInHierarchy && _light.intensity > 0f &&
                             _radius > 0f;

        void Awake() => _light = GetComponent<Light>();

        void OnEnable()
        {
            if (_light == null) _light = GetComponent<Light>();
            Measure();
            if (HasZone) LitZoneRegistry.Register(this);
        }

        void OnDisable() => LitZoneRegistry.Unregister(this);

        /// <summary>
        /// 구역의 자리와 넓이를 다시 잰다. 광원을 옮기거나 세기를 바꿨으면 부른다.
        /// </summary>
        public void Measure()
        {
            _center = transform.position;
            _radius = 0f;
            if (_light == null) return;

            if (_light.type == LightType.Spot)
            {
                MeasureSpot();
                return;
            }

            _radius = FixedLightRule.PointZoneRadius(_light.intensity, _light.range);
        }

        /// <summary>
        /// 스폿은 <b>빛이 닿는 바닥</b>이 구역이다. 광원 자리가 아니다 —
        /// 빛기둥의 광원은 천장 구멍(y=92)에 있고 사람이 서는 곳은 40m 아래다.
        /// </summary>
        void MeasureSpot()
        {
            float reach = Mathf.Min(_light.range, FixedLightRule.Reach(_light.intensity));
            if (reach <= 0f) return;

            float distance = reach;
            if (TryFindFloor(reach, out var hit)) { _center = hit.point; distance = hit.distance; }
            else _center = transform.position + transform.forward * reach;

            _radius = FixedLightRule.SpotZoneRadius(_light.intensity, distance, _light.spotAngle);
        }

        /// <summary>
        /// 빔이 처음 만나는 <b>움직이지 않는</b> 것을 찾는다.
        ///
        /// <b>가장 가까운 것을 그냥 집으면 안 된다.</b> 빛기둥 바로 아래가 플레이어의
        /// 시작 지점이라 첫 광선이 사람의 CharacterController를 맞는다(실측 37.4m,
        /// 바닥은 40.0m). 그러면 사람이 비켜서기만 해도 구역이 2.6m 움직인다.
        /// 밝은 구역은 지형이 정하는 것이지 그 안에 서 있는 것이 정하지 않는다.
        /// </summary>
        bool TryFindFloor(float maxDistance, out RaycastHit floor)
        {
            floor = default;
            var hits = Physics.RaycastAll(transform.position, transform.forward, maxDistance,
                                          ~0, QueryTriggerInteraction.Ignore);
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                var c = hits[i].collider;
                if (c == null) continue;
                if (c.attachedRigidbody != null) continue;   // 굴러다니는 것
                if (c is CharacterController) continue;      // 사람·생물
                if (c.transform.IsChildOf(transform.root)) continue; // 제 몸(빔 메시)

                if (found && hits[i].distance >= floor.distance) continue;
                floor = hits[i];
                found = true;
            }
            return found;
        }
    }
}
