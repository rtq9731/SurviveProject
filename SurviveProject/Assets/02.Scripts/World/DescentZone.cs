using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Survive.Core;
using Survive.Items;
using Survive.Player;
using Survive.Progression;
using Survive.UI;
using Survive.Vitals;

namespace Survive.World
{
    /// <summary>
    /// 짙은 매크로늄 층 하나. 잠항구를 걸치고 여기를 <b>끝까지 내려가면 챕터가 끝난다</b>
    /// (기획서 §6.2 "종막 — 뚫고 내려간다").
    ///
    /// <b>무엇이 새로운가.</b> 예전 종막은 남이 놔둔 외계 장치를 켜고 떠나는 이야기였고,
    /// 4번 섬에서 캔 매크로늄은 쓸 데가 없었다. 지금은 그 매크로늄으로
    /// 만든 껍데기가 열쇠이고, 문은 놓여 있는 것이 아니라 여태 닿으면 죽던 액면 그 자체다.
    ///
    /// <b>판정은 여기 없다.</b> "다 내려갔는가"는 <see cref="MacroniumDescent.Breached"/>가
    /// Unity 없이 답한다. 이 컴포넌트가 하는 일은 발밑 높이를 재서 규칙에 묻고,
    /// 참이라는 답이 오면 한 번만 종막을 트는 것뿐이다.
    ///
    /// <b>연출도 새로 만들지 않는다.</b> <see cref="ScreenFader"/>로 어두워지고
    /// <see cref="SceneFlowService"/>가 다음 씬을 올린다 —
    /// <see cref="SceneTransitionTrigger"/>가 프롤로그에서 쓰는 그 길이다.
    /// 다음 구역이 아직 없으면 <see cref="destination"/>을 비워 두고, 그것이 곧
    /// "구간의 끝"을 뜻한다 — 예전 종막 장치가 잡아 둔 규약을 그대로 물려받았다.
    ///
    /// <b>씬에는 아직 놓여 있지 않다.</b> 액면섬과 그 아래 층의 배치는 기획서 §8-4의 일이고,
    /// 씬은 병합할 수 없는 단일 파일이라 여기서 건드리지 않는다. 그때까지 이 컴포넌트는
    /// 검증이 런타임에 세워서 쓴다(<see cref="Setup"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public class DescentZone : MonoBehaviour, IHazardZoneSource
    {
        static readonly List<DescentZone> _all = new List<DescentZone>();

        [Tooltip("층이 깔린 반경(m). 이 안에서 내려가야 뚫은 것으로 친다")]
        [Min(0f)] [SerializeField] float radius = 18f;

        [Tooltip("뚫고 내려가야 하는 층의 두께(m). 잠항구의 용량이 이 값 이상이어야 지난다")]
        [Min(0f)] [SerializeField] float layerThickness = 12f;

        [Tooltip("중심을 오브젝트 원점에서 옮긴다. 중심의 높이가 곧 층의 윗면(액면)이다")]
        [SerializeField] Vector3 centerOffset = Vector3.zero;

        [Tooltip("다 내려갔을 때 세울 플래그. 챕터의 마지막 목표가 이것을 읽는다")]
        [SerializeField] string flagKey = "ch1_descended";

        [Tooltip("다음 구역. 비워 두면 여기가 구간의 끝이라는 뜻이다")]
        [SerializeField] SceneReferenceSO destination;

        [Tooltip("암전에 걸리는 시간(초)")]
        [Min(0f)] [SerializeField] float fadeSeconds = 1.5f;

        /// <summary>
        /// 목적지 없이 하강이 끝났을 때. 구간의 마지막 층이라는 뜻이다.
        /// 예전 종막 장치가 내던 신호와 같은 자리다.
        /// </summary>
        public static event Action<DescentZone> ChapterEnded;

        /// <summary>층을 뚫은 횟수. 한 번 내려가 한 번만 끝나는지 보려는 것이다.</summary>
        public static int Breaches { get; private set; }

        bool _breached;

        /// <summary>이 층은 이미 뚫렸는가.</summary>
        public bool HasBreached => _breached;

        public Vector3 HazardZoneCenter => transform.TransformPoint(centerOffset);
        public float HazardZoneRadius => radius;

        // 이 구역이 막는 것은 하나로 고정한다 — MacroniumSurfaceZone과 같은 이유다.
        public EnvironmentHazard Hazard => EnvironmentHazard.MacroniumLayer;

        public float Magnitude => layerThickness;

        /// <summary>판정에 넘길 구간 하나.</summary>
        public HazardZone Zone => new HazardZone(Hazard, layerThickness);

        /// <summary>층의 윗면 = 액면. 구역 중심의 높이가 그 자리다.</summary>
        public float TopY => HazardZoneCenter.y;

        /// <summary>층의 아랫면. 발이 여기를 넘어서면 뚫은 것이다.</summary>
        public float BottomY => MacroniumDescent.BottomY(TopY, layerThickness);

        /// <summary>이 지점이 층 위(수평으로)인가. 높이는 보지 않는다.</summary>
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
        /// 이 지점의 층. 없으면 false. 겹치면 가장 높은 것을 쓴다 —
        /// <see cref="MacroniumSurfaceZone.TryGetSurfaceAt"/>와 같은 규칙이다.
        /// </summary>
        public static bool TryGetLayerAt(Vector3 p, out DescentZone zone)
        {
            zone = null;
            float best = float.MinValue;

            for (int i = 0; i < _all.Count; i++)
            {
                var z = _all[i];
                if (z == null || !z.ContainsHorizontally(p)) continue;
                if (zone == null || z.TopY > best) { best = z.TopY; zone = z; }
            }
            return zone != null;
        }

        PlayerVitals _vitals;
        Transform _body;
        CharacterController _cc;
        PlayerTraversalGear _gear;
        PlayerInventory _inventory;

        readonly List<GearCapability> _loadout = new List<GearCapability>();

        void Update()
        {
            if (_breached) return;
            if (!Acquire()) return;
            if (!ContainsHorizontally(_body.position)) return;

            if (!MacroniumDescent.Breached(FeetY, TopY, Zone, CurrentLoadout())) return;

            Breach();
        }

        /// <summary>발바닥 높이. 층의 아랫면과 견주는 값이 이것이다.</summary>
        float FeetY => _cc != null
            ? _body.position.y - _cc.height * 0.5f + _cc.center.y
            : (_body != null ? _body.position.y : 0f);

        // MacroniumContactService와 같은 방식 — 씬이 다시 올라오면 새 플레이어를 다시 잡는다.
        bool Acquire()
        {
            if (_vitals != null && _body != null) return true;

            if (!GameServices.TryGet<PlayerVitals>(out var found) || found == null) return false;

            _vitals = found;
            _body = found.GetComponentInParent<PlayerContext>()?.transform ?? found.transform;
            _cc = _body.GetComponentInChildren<CharacterController>();
            _gear = _body.GetComponentInChildren<PlayerTraversalGear>(true);
            _inventory = _body.GetComponentInChildren<PlayerInventory>(true);
            return true;
        }

        IReadOnlyList<GearCapability> CurrentLoadout()
        {
            if (_gear != null) return _gear.Loadout;

            _loadout.Clear();
            TraversalLoadout.Collect(_inventory?.Inventory, _loadout);
            return _loadout;
        }

        /// <summary>
        /// 층을 뚫었다. 진행도에 알리고 종막 연출을 튼다.
        ///
        /// 플래그를 먼저 세우는 이유: 암전이 시작되면 목표가 넘어간 것을 확인할 길이
        /// 없어진다. 진행도는 즉시, 연출은 그다음이다.
        /// </summary>
        public void Breach()
        {
            if (_breached) return;
            _breached = true;
            Breaches++;

            if (!string.IsNullOrEmpty(flagKey) &&
                GameServices.TryGet<ChapterDirector>(out var director) && director != null)
                director.SetFlag(flagKey, 1);

            Debug.Log($"[DescentZone] 짙은 매크로늄 층을 뚫고 내려갔다 — " +
                      $"윗면 {TopY:F2}, 두께 {layerThickness:F1}m ({name})");

            StartCoroutine(Finale());
        }

        IEnumerator Finale()
        {
            // 목적지를 비워 두는 것은 오류가 아니다. 다음 구역이 아직 없는 구간의
            // 마지막 층이 그렇다 — 하강은 성립하고 챕터는 여기서 끝난다.
            // 예전 종막 장치가 잡아 둔 규약을 그대로 따른다.
            // 화면은 GameServices에 등록된 것을 쓴다. ScreenFader가 OnEnable에서 스스로
            // 등록하므로 씬을 뒤질 이유가 없다.
            GameServices.TryGet<ScreenFader>(out var fader);

            if (destination == null || string.IsNullOrEmpty(destination.sceneName))
            {
                if (fader != null) yield return fader.FadeOut(fadeSeconds);

                Debug.Log("[DescentZone] 다음 구역이 아직 없어 챕터를 여기서 마칩니다.", this);
                ChapterEnded?.Invoke(this);
                yield break;
            }

            var flow = fader != null ? fader.BuildFlow()
                     : (GameServices.TryGet<SceneFlowService>(out var known) ? known : new SceneFlowService());

            yield return flow.LoadScene(destination, fadeSeconds);
        }

        /// <summary>
        /// 코드로 세울 때 값을 넣는다. 검증이 런타임에 층을 스폰하기 위한 것이다 —
        /// <see cref="MacroniumSurfaceZone.Setup"/>이 같은 이유로 열려 있다.
        /// </summary>
        public void Setup(float zoneRadius, float thickness, Vector3 offset = default,
                          string flag = null)
        {
            radius = Mathf.Max(0f, zoneRadius);
            layerThickness = Mathf.Max(0f, thickness);
            centerOffset = offset;
            if (flag != null) flagKey = flag;
        }

        /// <summary>검증이 실행 사이에 상태를 비운다.</summary>
        public static void ResetCounters() => Breaches = 0;

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            // 층은 액면 아래라 씬 뷰에서 아예 보이지 않는다. 윗면과 아랫면을 같이 그린다.
            var top = HazardZoneCenter;
            var bottom = new Vector3(top.x, BottomY, top.z);

            Gizmos.color = new Color(0.63f, 0.18f, 0.88f, 0.35f);
            Gizmos.DrawWireSphere(top, radius);

            Gizmos.color = new Color(0.35f, 0.10f, 0.55f, 0.35f);
            Gizmos.DrawWireSphere(bottom, radius);
            Gizmos.DrawLine(top, bottom);
        }
#endif
    }
}
