using System;
using UnityEngine;
using Survive.Building;
using Survive.Core;
using Survive.Items;
using Survive.Player;
using Survive.Progression;
using Survive.World;

namespace Survive.Vehicles
{
    /// <summary>
    /// 손에 든 돌파정을 <b>놓는</b> 쪽 (스펙 §6). 놓은 뒤의 일은 <see cref="BreachPod"/>가 든다.
    ///
    /// <b>왜 서비스가 스스로 서는가.</b> 이 물건은 씬에도 플레이어 프리팹에도 자리가 없다 —
    /// 지하의 배치가 사람의 몫이라 붙일 곳이 아직 없기 때문이다.
    /// <c>MacroniumContactService</c>·<c>RespawnService</c>가 같은 이유로 같은 모양이다.
    ///
    /// <b>판정은 여기 없다.</b> 여기서 하는 일은 <b>세계를 읽어 자리 하나를 만드는 것</b>뿐이고
    /// (<see cref="Survey"/>), 가부는 <see cref="BreachPodPlacement.Evaluate"/>가 답한다.
    /// 그래야 경계값을 Unity 없이 시험할 수 있다.
    ///
    /// <b>왜 조준한 곳에 지면을 요구하지 않는가.</b> 짙은 구간은 액체라 콜라이더가 없다 —
    /// 층이 드러난 자리에서 광선은 아무것도 맞히지 못한다. 그래서 <b>드러난 액면 자체를
    /// 놓을 면으로 친다</b>. 광선이 무언가를 맞혔다면 그것은 층 위에 얹힌 것이고,
    /// 그 높이가 층의 윗면보다 높으면 층은 덮여 있다는 뜻이다 —
    /// 덮인 자리와 드러난 자리를 가르는 것이 그 높이 하나다.
    /// </summary>
    [DisallowMultipleComponent]
    public class BreachPodService : MonoBehaviour
    {
        public const string PodItemId = "breach_pod";

        /// <summary>돌파 설계. 열려 있어야 놓을 줄 안다 — 건축의 청사진 관문과 같은 자리다.</summary>
        public const string PodBlueprintId = "bp_breach_pod";

        /// <summary>조준이 닿는 거리(m). 건설 모드와 같은 감각으로 잡았다.</summary>
        public const float Reach = 6f;

        /// <summary>이만큼 안에 이미 돌파정이 있으면 겹친 것으로 친다(m).</summary>
        public const float ClearanceRadius = 3f;

        public static BreachPodService Instance { get; private set; }

        /// <summary>마지막 판정. 화면이 사유를 띄운다.</summary>
        public PlacementResult LastResult { get; private set; } = PlacementResult.NoSurface;

        /// <summary>판정이 바뀔 때마다. HUD가 여기에 붙는다.</summary>
        public static event Action<PlacementResult> ResultChanged;

        /// <summary>놓인 순간.</summary>
        public static event Action<BreachPod> Deployed;

        /// <summary>몇 대를 놓았는가. 검증이 보는 값이다.</summary>
        public static int Deploys { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Instance != null) return;

            var go = new GameObject("BreachPodService");
            DontDestroyOnLoad(go);
            go.AddComponent<BreachPodService>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        // ── 세계를 읽는다 ────────────────────────────────────────

        /// <summary>
        /// 지금 조준한 자리. 규칙이 받는 값 전부를 여기서 채운다.
        /// </summary>
        /// <param name="where">실제로 놓일 자리. 층이 있으면 그 윗면 높이에 선다.</param>
        public BreachPodSite Survey(out Vector3 where)
        {
            where = Vector3.zero;

            var eye = Eye();
            if (eye == null) return BreachPodSite.Nowhere;

            bool hitSomething = Physics.Raycast(eye.position, eye.forward, out var hit,
                                                Reach, ~0, QueryTriggerInteraction.Ignore);
            Vector3 aim = hitSomething ? hit.point : eye.position + eye.forward * Reach;

            bool hasLayer = DescentZone.TryGetLayerAt(aim, out var layer) && layer != null;

            // 층이 없으면 광선이 맞힌 것이 곧 면이다. 층이 있으면 드러난 액면도 면이다.
            bool hasSurface = hitSomething || hasLayer;
            float surfaceY = hitSomething ? hit.point.y : (hasLayer ? layer.TopY : 0f);

            where = hasLayer ? new Vector3(aim.x, layer.TopY, aim.z) : aim;

            return new BreachPodSite(
                hasSurface,
                hasLayer,
                hasLayer ? layer.Hazard : EnvironmentHazard.None,
                hasLayer ? layer.TopY : 0f,
                surfaceY,
                Occupied(where));
        }

        /// <summary>그 자리에 이미 돌파정이 서 있는가.</summary>
        public static bool Occupied(Vector3 at)
        {
            var all = BreachPod.All;
            for (int i = 0; i < all.Count; i++)
            {
                var pod = all[i];
                if (pod == null) continue;
                if ((pod.transform.position - at).sqrMagnitude <= ClearanceRadius * ClearanceRadius)
                    return true;
            }
            return false;
        }

        // ── 판정과 배치 ──────────────────────────────────────────

        /// <summary>지금 놓을 수 있는가. 판정만 하고 아무것도 바꾸지 않는다.</summary>
        public PlacementResult Evaluate() => Evaluate(out _);

        public PlacementResult Evaluate(out Vector3 where)
        {
            var site = Survey(out where);
            var result = BreachPodPlacement.Evaluate(site, Unlocked(), CountInHand() > 0);
            Publish(result);
            return result;
        }

        /// <summary>
        /// 놓는다. 판정이 <see cref="PlacementResult.Ok"/>일 때만 손에서 하나가 빠진다.
        /// 못 놓으면 null이고, 사유는 <see cref="LastResult"/>에 남는다.
        /// </summary>
        public BreachPod TryDeploy()
        {
            var site = Survey(out var where);
            var result = BreachPodPlacement.Evaluate(site, Unlocked(), CountInHand() > 0);
            Publish(result);
            if (result != PlacementResult.Ok) return null;

            if (!DescentZone.TryGetLayerAt(where, out var layer) || layer == null)
            {
                // 판정이 Ok인데 층이 없을 수는 없다. 그래도 여기서 터지면
                // 재료만 사라지므로 조용히 물린다.
                Publish(PlacementResult.NotDenseLayer);
                return null;
            }

            float capacity = PodCapacity();
            var inv = Inventory();
            if (inv == null || !inv.TryRemove(PodItemId, 1))
            {
                Publish(PlacementResult.NotEnoughResources);
                return null;
            }

            var pod = Build(where);
            pod.Setup(layer, capacity);

            Deploys++;
            Debug.Log($"[BreachPodService] 돌파정을 놓았다 — {where.ToString("F2")} " +
                      $"(층 윗면 {layer.TopY:F2}, 두께 {layer.Zone.Magnitude:F1}m)", pod);

            Deployed?.Invoke(pod);
            return pod;
        }

        void Publish(PlacementResult result)
        {
            if (result == LastResult) return;
            LastResult = result;
            ResultChanged?.Invoke(result);
        }

        // ── 놓인 물건을 짓는다 ───────────────────────────────────

        /// <summary>
        /// 놓인 돌파정의 몸.
        ///
        /// <b>모델은 아직 없다.</b> 프리팹은 이 라운드가 손댈 자리가 아니라
        /// (씬·프리팹은 병합할 수 없는 단일 파일이다) 형태만 세워 둔다 —
        /// 1인용 드랍포드이므로 좌석 하나짜리 캡슐 하나다. 진짜 모델이 오면
        /// 이 함수 하나만 바뀐다.
        /// </summary>
        static BreachPod Build(Vector3 where)
        {
            var go = new GameObject("BreachPod");
            go.transform.position = where;

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(1.1f, 0.9f, 1.1f);

            var skirt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            skirt.name = "Skirt";
            skirt.transform.SetParent(go.transform, false);
            skirt.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            skirt.transform.localScale = new Vector3(1.6f, 0.12f, 1.6f);

            // 몸통 콜라이더는 상호작용 광선이 맞혀야 하므로 남긴다.
            // 치마는 놓을 자리 판정에 자기가 걸리지 않게 떼어 낸다.
            var skirtCollider = skirt.GetComponent<Collider>();
            if (skirtCollider != null) Destroy(skirtCollider);

            return go.AddComponent<BreachPod>();
        }

        // ── 손에 든 것 ───────────────────────────────────────────

        static Transform Eye()
        {
            var cam = Camera.main;
            if (cam != null) return cam.transform;
            return GameServices.TryGet<PlayerContext>(out var p) && p != null ? p.transform : null;
        }

        static Inventory Inventory() =>
            GameServices.TryGet<PlayerInventory>(out var inv) && inv != null ? inv.Inventory : null;

        public static int CountInHand()
        {
            var inv = Inventory();
            return inv != null ? inv.CountOf(PodItemId) : 0;
        }

        /// <summary>손에 든 돌파정이 감당하는 층 두께(m). 값은 아이템 정의가 든다.</summary>
        static float PodCapacity()
        {
            if (!GameServices.TryGet<PlayerInventory>(out var inv) || inv?.Database == null) return 0f;
            return inv.Database.GetById(PodItemId) is TraversalGearItemSO gear ? gear.capacity : 0f;
        }

        static bool Unlocked() =>
            BlueprintGate.Active == null || BlueprintGate.Active.IsUnlocked(PodBlueprintId);

        /// <summary>검증이 실행 사이에 상태를 비운다.</summary>
        public static void ResetCounters() => Deploys = 0;
    }
}
