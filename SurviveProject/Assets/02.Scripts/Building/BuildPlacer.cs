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

            bool hitSomething = Physics.Raycast(rayOrigin.position, rayOrigin.forward, out var hit,
                                                maxDistance, surfaceMask, QueryTriggerInteraction.Ignore);

            // 모듈 조각은 지형이 아니라 이미 세운 것에 물린다.
            // 지면 각도나 겹침을 따지기 전에 붙일 자리부터 본다 —
            // 벽은 원래 바닥에 딱 붙어야 하므로 겹침 판정에 걸리는 게 정상이다.
            //
            // 허공을 봐도 된다. 2층 바닥이나 벽 위 조각은 뒤에 아무것도 없는 쪽을
            // 보게 되는데, 거기서 "놓을 면이 없다"고 막으면 위로는 지을 수가 없다.
            if (_selected.IsModular)
            {
                var aim = hitSomething ? hit.point
                                       : rayOrigin.position + rayOrigin.forward * maxDistance;
                return EvaluateModular(aim, hitSomething, ref position, ref rotation);
            }

            if (!hitSomething) return PlacementResult.NoSurface;

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

        /// <summary>
        /// 모듈 조각의 자리를 정한다.
        ///
        /// 붙일 자리를 찾으면 그 자세를 그대로 쓴다. 플레이어의 휠 회전은 무시한다 —
        /// 격자에 물린 조각을 손으로 돌릴 수 있으면 그건 격자가 아니다.
        /// 토대만 예외로, 붙일 곳이 없으면 지면에 자유롭게 놓는다.
        /// </summary>
        PlacementResult EvaluateModular(Vector3 aim, bool onSurface,
                                        ref Vector3 position, ref Quaternion rotation)
        {
            var kind = _selected.pieceKind;

            if (SnapGraph.TryFindNearest(aim, kind, _selected.snapRadius, out var snapPos, out var snapRot))
            {
                position = snapPos;
                rotation = snapRot;

                if (SlotOccupied(position, kind)) return PlacementResult.SlotTaken;
                if (!HasResources()) return PlacementResult.NotEnoughResources;
                return PlacementResult.Ok;
            }

            // 붙을 곳이 없다. 토대가 아니면 여기서 끝이다.
            if (_selected.requiresSnap) return PlacementResult.NoAnchor;

            // 토대는 지면이 있어야 한다. 허공에 첫 조각을 띄울 수는 없다.
            if (!onSurface) return PlacementResult.NoSurface;

            // 첫 토대는 지면에 놓는다. 지면 판정은 이때만 의미가 있다.
            if (!Physics.Raycast(aim + Vector3.up * 2f, Vector3.down, out var ground,
                                 6f, surfaceMask, QueryTriggerInteraction.Ignore))
                return PlacementResult.NoSurface;

            if (Vector3.Angle(ground.normal, Vector3.up) > _selected.maxSlopeDegrees)
                return PlacementResult.TooSteep;

            position = ground.point;
            rotation = Quaternion.Euler(0f, _yaw, 0f);

            if (SlotOccupied(position, kind)) return PlacementResult.SlotTaken;
            if (!HasResources()) return PlacementResult.NotEnoughResources;
            return PlacementResult.Ok;
        }

        /// <summary>
        /// 한 자리를 두고 다투는 조각들을 한 묶음으로 본다.
        ///
        /// 종류로 비교하면 벽이 선 자리에 문간이 또 들어간다 — 둘은 다른 종류지만
        /// 같은 모서리를 쓴다. 경사로도 그 모서리에서 시작하므로 같은 묶음이다.
        /// 반대로 바닥 모서리에 벽을 세우는 건 다른 묶음이라 막지 않는다.
        /// </summary>
        static BuildPieceKind SlotGroup(BuildPieceKind kind)
        {
            if ((kind & BuildPieceKind.Platform) != 0) return BuildPieceKind.Platform;
            return BuildPieceKind.Upright | BuildPieceKind.Ramp;
        }

        /// <summary>그 자리를 이미 같은 묶음의 조각이 차지했는가.</summary>
        bool SlotOccupied(Vector3 at, BuildPieceKind kind)
        {
            var group = SlotGroup(kind);

            int n = Physics.OverlapSphereNonAlloc(at, 1.0f, _overlapBuffer, ~0,
                                                  QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = _overlapBuffer[i];
                if (c == null) continue;
                if (_ghost != null && c.transform.IsChildOf(_ghost.transform)) continue;

                var piece = c.GetComponentInParent<ModularPiece>();
                if (piece == null) continue;
                if ((piece.Kind & group) == 0) continue;
                if (Vector3.Distance(piece.transform.position, at) > 0.6f) continue;

                return true;
            }
            return false;
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

            // 지은 것은 전부 부술 수 있어야 한다. 잘못 지은 순간 재료가 날아가면
            // 아무도 실험하지 않는다.
            if (go.GetComponent<StructureDemolisher>() == null)
                go.AddComponent<StructureDemolisher>();

            if (_selected.IsModular)
            {
                var piece = go.GetComponent<ModularPiece>();
                if (piece == null) piece = go.AddComponent<ModularPiece>();
                piece.Setup(_selected.pieceKind);
            }

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

            // 끄는 것만으로는 모자라다. 컴포넌트를 꺼도 FindObjectsByType는 찾아내므로
            // 미리보기가 "세워진 조각"으로 세어진다 — 격자 검사가 유령을 보고
            // 어긋났다고 판정했다. 조각으로 오해될 것은 아예 떼어 낸다.
            foreach (var p in _ghost.GetComponentsInChildren<ModularPiece>(true)) Destroy(p);
            foreach (var s in _ghost.GetComponentsInChildren<BuildSnapPoint>(true)) Destroy(s);
            foreach (var m in _ghost.GetComponentsInChildren<BuiltStructure>(true)) Destroy(m);

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
