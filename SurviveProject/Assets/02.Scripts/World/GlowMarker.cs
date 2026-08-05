using System.Collections.Generic;
using UnityEngine;
using Survive.Domain.Art;
using Survive.Harvesting;
using Survive.Interaction;
using Survive.Items;
using Survive.Player;

namespace Survive.World
{
    /// <summary>
    /// 발밑에 놓는 발광 버섯 갓. 지나온 길을 표시한다.
    ///
    /// <b>빛이지만 안전은 아니다.</b> <see cref="LitZoneRegistry"/>에 등록하지 않는다 —
    /// 등록하면 갓 몇 개를 뿌려 포식자를 막을 수 있게 되고, 그 순간
    /// "배터리가 곧 생존줄"이라는 챕터 1의 압박(스펙 D2-L)이 사라진다.
    /// 여기서 갓이 하는 일은 <b>어디까지 와 봤는지 알려 주는 것</b>뿐이다.
    /// 실제로 세기와 반경도 군락이나 랜턴에 견주면 미미하다.
    ///
    /// <b>돌려받을 수 있다.</b> E로 회수하면 인벤토리로 돌아온다. 표식이 소모품이면
    /// 길을 남길 때마다 자원을 태우게 되고, 그러면 길을 남기지 않는 쪽이 최적이 된다.
    ///
    /// <b>프리팹을 만들지 않는다.</b> <see cref="Survive.Items.DeathDropBag"/>과 같은
    /// 방식으로 실행 시점에 만든다 — 프리팹은 병합할 수 없는 파일이라
    /// 여러 갈래로 나뉘어 일하는 동안 늘리지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class GlowMarker : MonoBehaviour, IInteractable
    {
        /// <summary>표식으로 쓰는 아이템. 군락에서 딴 그 갓이다.</summary>
        public const string ItemId = GlowCapCluster.CapItemId;

        /// <summary>표식의 지름(m). 발에 걸리지 않고 눈에는 띄는 크기.</summary>
        const float Size = 0.28f;

        /// <summary>
        /// 표식의 빛. 군락(세기 5.5·반경 11)이나 랜턴(5.5·26)에 견주면 한 뼘이다 —
        /// 길을 밝히는 것이 아니라 <b>자기가 여기 있다는 것만</b> 알린다.
        /// </summary>
        const float LightIntensity = 1.1f;
        const float LightRange = 3.5f;

        /// <summary>조준용 트리거 반경. 작은 물체는 이게 없으면 E가 잡히지 않는다.</summary>
        const float TriggerRadius = 0.45f;

        /// <summary>발밑을 찾을 때 위로 올라가 아래로 쏘는 거리.</summary>
        const float GroundProbeUp = 0.6f;
        const float GroundProbeDown = 4f;

        /// <summary>지금 세계에 놓여 있는 표식들.</summary>
        public static IReadOnlyList<GlowMarker> Active => _active;
        static readonly List<GlowMarker> _active = new List<GlowMarker>();

        ItemDataSO _item;

        /// <summary>표식이 내는 빛. 검증에서 세기를 집는다.</summary>
        public Light Light { get; private set; }

        void OnEnable() { if (!_active.Contains(this)) _active.Add(this); }
        void OnDisable() => _active.Remove(this);

        /// <summary>
        /// 인벤토리에서 갓 하나를 꺼내 플레이어 발밑에 놓는다.
        /// </summary>
        /// <returns>놓은 표식. 갓이 없거나 놓을 자리가 없으면 null.</returns>
        public static GlowMarker PlaceFrom(PlayerContext player)
        {
            var inventory = player?.Inventory?.Inventory;
            if (inventory == null) return null;

            var item = player.Inventory.Database != null
                ? player.Inventory.Database.GetById(ItemId)
                : null;
            if (item == null) return null;
            if (!inventory.TryRemove(ItemId, 1)) return null;

            var marker = Place(item, FootPoint(player.transform));
            if (marker == null) inventory.TryAdd(item, 1);   // 못 놓았으면 돌려준다
            return marker;
        }

        /// <summary>
        /// 발밑 지면. 못 찾으면 발 높이를 그대로 쓴다.
        ///
        /// 자기 몸은 건너뛴다 — 광선이 캡슐 안에서 출발하므로 보통은 잡히지 않지만,
        /// 그 "보통"에 기대면 어느 날 표식이 사람 배꼽 높이에 뜬다.
        /// </summary>
        static Vector3 FootPoint(Transform body)
        {
            Vector3 from = body.position + Vector3.up * GroundProbeUp;
            var hits = Physics.RaycastAll(from, Vector3.down, GroundProbeDown + GroundProbeUp,
                                          ~0, QueryTriggerInteraction.Ignore);

            float nearest = float.MaxValue;
            Vector3 ground = body.position;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                var t = hits[i].collider.transform;
                if (t == body || t.IsChildOf(body)) continue;
                if (hits[i].distance >= nearest) continue;

                nearest = hits[i].distance;
                ground = hits[i].point;
                found = true;
            }

            if (!found) ground = body.position;
            return ground + Vector3.up * (Size * 0.5f);
        }

        /// <summary>표식 하나를 그 자리에 세운다.</summary>
        public static GlowMarker Place(ItemDataSO item, Vector3 at)
        {
            if (item == null) return null;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "GlowMarker";
            go.transform.position = at;
            go.transform.localScale = Vector3.one * Size;

            // 원래 붙어 있는 SphereCollider는 조준용 트리거로 돌린다.
            // 솔리드로 두면 발에 걸려 표식이 장애물이 된다.
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
                if (col is SphereCollider sphere) sphere.radius = TriggerRadius / Size;
            }

            Paint(go);

            var lightGo = new GameObject("MarkerLight");
            lightGo.transform.SetParent(go.transform, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = ArtPalette.Glowshroom;
            light.intensity = LightIntensity;
            light.range = LightRange;
            light.shadows = LightShadows.None;

            var marker = go.AddComponent<GlowMarker>();
            marker._item = item;
            marker.Light = light;
            return marker;
        }

        /// <summary>
        /// 발광 버섯 색으로 칠한다. 광원 4색 규칙(ArtPalette)의 Glowshroom이다 —
        /// 실행 시점에 만드는 물건이라 정적 검사기가 보지 못하는 만큼
        /// 여기서 팔레트를 직접 참조해 어긋날 여지를 없앤다.
        /// </summary>
        static void Paint(GameObject go)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return;

            var mat = new Material(shader) { color = ArtPalette.Glowshroom };
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", ArtPalette.Glowshroom);
            rend.sharedMaterial = mat;
        }

        // ── 회수 ─────────────────────────────────────────────────

        public string InteractionPrompt => $"[E] {GlowCapCluster.DisplayName} 회수";

        public bool CanInteract(PlayerContext player)
            => player != null && player.Inventory?.Inventory != null;

        public void Interact(PlayerContext player)
        {
            if (!CanInteract(player)) return;

            int leftover = player.Inventory.Inventory.TryAdd(_item, 1);
            if (leftover > 0) return;   // 자리가 없으면 표식은 그 자리에 남는다

            Destroy(gameObject);
        }
    }
}
