using UnityEngine;
using Survive.Core;
using Survive.Items;
using Survive.Player;

namespace Survive.Vitals
{
    /// <summary>
    /// 죽으면 벌어온 것을 그 자리에 떨군다. P2 스펙 §6-3의 실행부다.
    ///
    /// <b>무엇을 떨구는지는 여기서 정하지 않는다.</b> 그 규칙은
    /// <see cref="Survive.Items.DeathDrop"/>에 있고 Unity 없이 테스트된다.
    /// 이 컴포넌트가 하는 일은 사망 시점을 잡아 그 규칙을 부르고,
    /// 나온 것을 담을 가방을 세계에 세우는 것뿐이다.
    ///
    /// <b>왜 씬에 놓지 않고 스스로 붙는가.</b> 플레이어 프리팹과 MainScene은
    /// 병합할 수 없는 단일 파일이라 여러 갈래로 나뉘어 일하는 동안 손대지 않기로 했다.
    /// 대신 실행 시점에 스스로 서고, 씬이 다시 올라오면 새 플레이어를 다시 잡는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class DeathDropService : MonoBehaviour
    {
        static DeathDropService _instance;

        public static DeathDropService Instance => _instance;

        /// <summary>마지막으로 만든 가방. 검증에서 결과를 집기 위한 것이다.</summary>
        public static DeathDropBag LastBag { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_instance != null) return;

            var go = new GameObject("DeathDropService");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<DeathDropService>();
        }

        PlayerVitals _vitals;

        void OnDisable() => Unhook();

        // 씬을 다시 올리면 지금 잡고 있던 플레이어가 사라진다. 매 프레임 확인하는 것이
        // sceneLoaded를 듣는 것보다 짧다 — 사라졌으면 다시 잡는다, 그게 전부다.
        void Update()
        {
            if (_vitals != null) return;

            Unhook();
            if (!GameServices.TryGet<PlayerVitals>(out var found) || found == null) return;

            _vitals = found;
            _vitals.Died += OnDied;
        }

        void Unhook()
        {
            if (_vitals != null) _vitals.Died -= OnDied;
            _vitals = null;
        }

        void OnDied()
        {
            if (!GameServices.TryGet<PlayerInventory>(out var inventory) ||
                inventory == null || inventory.Inventory == null)
            {
                Debug.LogWarning("[DeathDropService] 인벤토리를 찾지 못해 떨구지 못했습니다.");
                return;
            }

            var dropped = DeathDrop.Extract(inventory.Inventory);
            if (dropped.Count == 0)
            {
                Debug.Log("[DeathDropService] 떨굴 것이 없어 가방을 남기지 않는다");
                return;
            }

            var where = DeathPlace();
            LastBag = SpawnBag(where, dropped, inventory.Inventory.SlotCount);

            Debug.Log($"[DeathDropService] 사망 — {dropped.Count}칸을 {where.ToString("F1")}에 남겼다");
        }

        /// <summary>죽은 자리. 발밑을 찾아 가방이 지형에 박히거나 뜨지 않게 한다.</summary>
        Vector3 DeathPlace()
        {
            var body = _vitals != null
                ? (_vitals.GetComponentInParent<PlayerContext>()?.transform ?? _vitals.transform)
                : transform;

            var from = body.position;
            if (Physics.Raycast(from + Vector3.up * 1.2f, Vector3.down, out var hit, 8f,
                                ~0, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.25f;

            return from;
        }

        /// <summary>
        /// 가방을 세운다. 프리팹을 새로 만들지 않는 이유는
        /// <see cref="Survive.Interaction.ItemDropper"/>가 임시 큐브를 쓰는 이유와 같다 —
        /// 겉모습이 정해지기 전에 프리팹을 박아 두면 나중에 그 자리를 다시 뜯어야 한다.
        /// 어두운 데서 눈에 띄는 것이 지금 필요한 전부다.
        /// </summary>
        static DeathDropBag SpawnBag(Vector3 where, System.Collections.Generic.IReadOnlyList<ItemStack> stacks,
                                     int slotCount)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "DeathDropBag";
            go.transform.position = where;
            go.transform.localScale = new Vector3(0.55f, 0.4f, 0.45f);

            // 조준용 트리거. 물리로 밀려나면 죽은 자리와 어긋난다.
            var col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            Tint(go);

            var bag = go.AddComponent<DeathDropBag>();
            bag.Fill(stacks, slotCount);
            return bag;
        }

        static void Tint(GameObject go)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return;

            // 자원 드롭과 같은 계열이되 더 어둡고 붉다. 주울 것과 되찾을 것은 다른 물건이다.
            var tint = new Color(0.78f, 0.34f, 0.30f);
            var mat = new Material(shader) { color = tint };
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", tint * 0.6f);
            rend.sharedMaterial = mat;
        }
    }
}
