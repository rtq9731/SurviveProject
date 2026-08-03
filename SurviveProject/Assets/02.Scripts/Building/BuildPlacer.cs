using System;
using System.Collections.Generic;
using UnityEngine;
using Survive.Core;
using Survive.Items;
using Survive.Player;

namespace Survive.Building
{
    /// <summary>
    /// 건설 모드. 미리보기를 띄우고, 놓을 수 있는지 판정하고, 세운다.
    ///
    /// 격자 스냅 대신 자유 배치를 기본으로 잡았다. 이 맵은 동굴 지형이라
    /// 평평한 곳이 거의 없다 — 격자를 강제하면 바닥부터 깔아야 뭘 지을 수 있고,
    /// 첫 건설이 무거워진다. 지면에 바로 놓되 각도와 겹침만 막는다.
    /// 정렬이 필요한 물건은 BuildableSO에서 gridSnap을 켜면 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class BuildPlacer : MonoBehaviour
    {
        [SerializeField] BuildCatalogSO catalog;
        [SerializeField] Transform rayOrigin;          // 보통 카메라
        [SerializeField] PlayerInventory inventory;

        [Tooltip("건설물을 담을 부모. 비우면 씬 루트에 놓는다")]
        [SerializeField] Transform builtParent;

        [Header("조준")]
        [SerializeField] float maxDistance = 6f;
        [SerializeField] LayerMask surfaceMask = ~0;

        [Header("미리보기 색")]
        [SerializeField] Color okColor = new Color(0.45f, 0.95f, 0.55f, 0.45f);
        [SerializeField] Color blockedColor = new Color(0.95f, 0.35f, 0.30f, 0.45f);

        BuildableSO _selected;
        GameObject _ghost;
        Renderer[] _ghostRenderers;
        MaterialPropertyBlock _mpb;
        float _yaw;

        /// <summary>건설 모드가 켜져 있는가.</summary>
        public bool IsActive => _selected != null;

        public BuildableSO Selected => _selected;

        /// <summary>지금 놓을 수 있는지. UI가 사유를 띄운다.</summary>
        public PlacementResult LastResult { get; private set; } = PlacementResult.NoSurface;

        public event Action<BuildableSO> SelectionChanged;
        public event Action<PlacementResult> ResultChanged;
        public event Action<GameObject> Built;

        static readonly Collider[] _overlapBuffer = new Collider[16];

        void Awake()
        {
            if (rayOrigin == null && Camera.main != null) rayOrigin = Camera.main.transform;
            if (inventory == null) inventory = GetComponentInParent<PlayerInventory>();
            _mpb = new MaterialPropertyBlock();
        }

        void OnEnable() => GameServices.Register(this);
        void OnDisable()
        {
            GameServices.Unregister<BuildPlacer>();
            Cancel();
        }

        // ── 모드 전환 ────────────────────────────────────────────

        public void Select(BuildableSO buildable)
        {
            if (_selected == buildable) return;

            Cancel();
            _selected = buildable;
            if (_selected != null) SpawnGhost();
            SelectionChanged?.Invoke(_selected);
        }

        public void SelectById(string id) => Select(catalog != null ? catalog.GetById(id) : null);

        public void Cancel()
        {
            if (_ghost != null) Destroy(_ghost);
            _ghost = null;
            _ghostRenderers = null;

            if (_selected == null) return;
            _selected = null;
            SelectionChanged?.Invoke(null);
        }

        /// <summary>배치 회전. 마우스 휠이나 키에 물린다.</summary>
        public void Rotate(float degrees)
        {
            if (_selected == null) return;

            float step = _selected.rotationStep;
            _yaw += degrees;
            if (step > 0f) _yaw = Mathf.Round(_yaw / step) * step;
        }

        // ── 매 프레임 ────────────────────────────────────────────

        void Update()
        {
            if (_selected == null || _ghost == null) return;

            var result = Evaluate(out var pos, out var rot);

            _ghost.transform.SetPositionAndRotation(pos, rot);
            Tint(result == PlacementResult.Ok ? okColor : blockedColor);

            if (result != LastResult)
            {
                LastResult = result;
                ResultChanged?.Invoke(result);
            }
        }

        /// <summary>
        /// 지금 조준한 곳에 놓을 수 있는지 본다.
        /// 판정과 배치를 한 함수에 섞으면 미리보기와 실제 결과가 어긋나기 쉬워서 나눴다.
        /// </summary>
        public PlacementResult Evaluate(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            if (_selected == null || rayOrigin == null) return PlacementResult.NoSurface;

            if (!Physics.Raycast(rayOrigin.position, rayOrigin.forward, out var hit,
                                 maxDistance, surfaceMask, QueryTriggerInteraction.Ignore))
                return PlacementResult.NoSurface;

            position = hit.point;

            if (_selected.gridSnap > 0f)
            {
                float g = _selected.gridSnap;
                position = new Vector3(Mathf.Round(position.x / g) * g,
                                       position.y,
                                       Mathf.Round(position.z / g) * g);
            }

            rotation = Quaternion.Euler(0f, _yaw, 0f);

            // 비탈 판정
            float slope = Vector3.Angle(hit.normal, Vector3.up);
            if (slope > _selected.maxSlopeDegrees) return PlacementResult.TooSteep;

            // 배치 모드
            bool onStructure = hit.collider.GetComponentInParent<BuiltStructure>() != null;
            switch (_selected.placement)
            {
                case PlacementMode.Ground when onStructure:
                    return PlacementResult.WrongSurface;
                case PlacementMode.OnStructure when !onStructure:
                    return PlacementResult.WrongSurface;
            }

            // 겹침. 미리보기 자신은 세지 않는다.
            if (_selected.clearanceRadius > 0f)
            {
                int n = Physics.OverlapSphereNonAlloc(
                    position + Vector3.up * 0.4f, _selected.clearanceRadius,
                    _overlapBuffer, ~0, QueryTriggerInteraction.Ignore);

                for (int i = 0; i < n; i++)
                {
                    var c = _overlapBuffer[i];
                    if (c == null) continue;
                    if (_ghost != null && c.transform.IsChildOf(_ghost.transform)) continue;
                    if (c.GetComponentInParent<BuiltStructure>() != null)
                        return PlacementResult.Blocked;
                }
            }

            if (!HasResources()) return PlacementResult.NotEnoughResources;

            return PlacementResult.Ok;
        }

        /// <summary>세운다. 성공하면 만들어진 오브젝트를 돌려준다.</summary>
        public GameObject TryBuild()
        {
            if (_selected == null) return null;

            var result = Evaluate(out var pos, out var rot);
            if (result != PlacementResult.Ok) return null;

            if (!SpendResources()) return null;

            var prefab = _selected.prefab;
            if (prefab == null) return null;

            var go = Instantiate(prefab, pos, rot, builtParent);
            go.name = _selected.id;

            // 세워진 것임을 표시한다. 겹침 판정과 나중의 철거가 이걸로 대상을 고른다.
            var marker = go.GetComponent<BuiltStructure>();
            if (marker == null) marker = go.AddComponent<BuiltStructure>();
            marker.Setup(_selected);

            Built?.Invoke(go);
            return go;
        }

        // ── 재료 ─────────────────────────────────────────────────

        bool HasResources()
        {
            var inv = inventory?.Inventory;
            if (inv == null || _selected.cost == null) return false;

            foreach (var c in _selected.cost)
            {
                if (c?.item == null) continue;
                if (!inv.Has(c.item.id, c.count)) return false;
            }
            return true;
        }

        bool SpendResources()
        {
            var inv = inventory?.Inventory;
            if (inv == null) return false;

            // 하나라도 모자라면 아무것도 빼지 않는다.
            // 반쯤 빼고 실패하면 재료가 증발한 것처럼 보인다.
            if (!HasResources()) return false;

            foreach (var c in _selected.cost)
            {
                if (c?.item == null) continue;
                inv.TryRemove(c.item.id, c.count);
            }
            return true;
        }

        // ── 미리보기 ─────────────────────────────────────────────

        void SpawnGhost()
        {
            var src = _selected.ghostPrefab != null ? _selected.ghostPrefab : _selected.prefab;
            if (src == null) return;

            _ghost = Instantiate(src);
            _ghost.name = "Ghost_" + _selected.id;

            // 미리보기는 물리에 관여하면 안 된다. 자기 자신에 걸려 배치가 막힌다.
            foreach (var c in _ghost.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var b in _ghost.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
            foreach (var rb in _ghost.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;

            _ghostRenderers = _ghost.GetComponentsInChildren<Renderer>(true);
        }

        void Tint(Color color)
        {
            if (_ghostRenderers == null) return;

            foreach (var r in _ghostRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_BaseColor", color);
                _mpb.SetColor("_Color", color);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
