using UnityEngine;
using UnityEngine.SceneManagement;
using Survive.Harvesting;
using Survive.Items;

namespace Survive.World
{
    /// <summary>
    /// 씬에 이미 서 있는 거대 버섯을 벌목 노드로 만든다.
    ///
    /// <b>왜 씬을 고치지 않는가.</b> MainScene은 병합할 수 없는 단일 파일이라
    /// 여러 갈래로 나뉘어 일하는 동안 손대지 않는다 — <c>GlowGroveService</c>·
    /// <c>MacroniumContactService</c>와 같은 이유다. 실행할 때마다 씬을 훑어
    /// 스스로 붙으므로, 사람이 §8-4에서 버섯을 더 놓거나 옮겨도 따라간다.
    ///
    /// <b>무엇을 베는가.</b> 이름이 <c>Giant</c>인 버섯만이다
    /// (<see cref="MushroomLumberRule.IsGiant"/>). 발광 버섯 <b>군락의 갓</b>은
    /// 건드리지 않는다 — 그쪽은 R9-B의 <c>GlowCapCluster</c>가 맡은,
    /// 손으로 따고 군락의 밝기를 좌우하는 다른 자원이다. 지금 씬에서 둘은
    /// 겹치지 않지만(군락 갓은 Giant가 아니다), 사람이 나중에 겹쳐 놓을 수 있으므로
    /// 갓이 이미 붙은 것은 건너뛴다.
    ///
    /// <b>정의는 왜 코드가 만드는가.</b> 노드가 씬에 없으니 인스펙터로 물려 줄
    /// 자리도 없다. 수치는 <see cref="MushroomLumberRule"/>에 있고 여기서는
    /// 그것을 <see cref="HarvestNodeSO"/> 한 벌로 조립해 모든 그루가 나눠 쓴다.
    /// </summary>
    public static class MushroomLumberService
    {
        /// <summary>마지막으로 세운 벌목 노드 수. 검증에서 집는다.</summary>
        public static int InstalledTrees { get; private set; }

        static HarvestNodeSO _definition;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            // 두 번 걸리지 않게 먼저 뗀다. 도메인 리로드를 끄고 재생하면
            // static 구독이 살아남아 같은 씬에 두 번 붙을 수 있다.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Build();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Build();

        /// <summary>
        /// 지금 열려 있는 씬을 훑어 벌목 노드를 세운다.
        /// 이미 세워진 것은 건드리지 않는다.
        /// </summary>
        /// <returns>세운(또는 이미 서 있던) 그루 수.</returns>
        public static int Build()
        {
            InstalledTrees = 0;

            var definition = Definition();
            if (definition == null) return 0;

            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            foreach (var t in all)
            {
                if (!MushroomLumberRule.IsGiant(t.name)) continue;
                if (t.GetComponent<GlowCapCluster>() != null) continue;   // 군락의 갓은 남의 것이다

                var node = t.GetComponent<HarvestNode>();
                if (node == null)
                {
                    // 때려서 부수려면 맞을 몸이 있어야 한다. 콜라이더가 없는
                    // 장식 메시는 벨 수 없으므로 조용히 지나간다.
                    if (t.GetComponentInChildren<Collider>(true) == null) continue;
                    node = t.gameObject.AddComponent<HarvestNode>();
                }

                node.Bind(definition);
                InstalledTrees++;
            }

            if (InstalledTrees > 0)
                Debug.Log($"[MushroomLumberService] 거대 버섯 {InstalledTrees}그루를 벌목 대상으로 세웠습니다.");
            return InstalledTrees;
        }

        /// <summary>
        /// 모든 그루가 나눠 쓰는 정의 한 벌. 처음 필요할 때 만든다.
        /// 목재 아이템을 못 찾으면 만들지 않는다 — 아무것도 떨구지 않는 나무를
        /// 세워 두면 "벌목이 고장났다"로 읽힌다.
        /// </summary>
        static HarvestNodeSO Definition()
        {
            if (_definition != null) return _definition;

            var wood = FindWoodItem();
            if (wood == null)
            {
                Debug.LogWarning($"[MushroomLumberService] '{MushroomLumberRule.WoodItemId}' " +
                                 "아이템 정의를 찾지 못해 벌목 노드를 세우지 않았습니다.");
                return null;
            }

            var loot = ScriptableObject.CreateInstance<LootTableSO>();
            loot.name = "MushroomTreeLoot(runtime)";
            loot.entries = new[]
            {
                new LootTableSO.Entry
                {
                    item = wood,
                    minCount = MushroomLumberRule.MinYield,
                    maxCount = MushroomLumberRule.MaxYield,
                    chance = 1f
                }
            };

            var def = ScriptableObject.CreateInstance<HarvestNodeSO>();
            def.name = "MushroomTree(runtime)";
            def.displayName = MushroomLumberRule.DisplayName;
            def.requiredTool = ToolType.Pickaxe;      // 광맥과 같은 관례 — 때려서 부순다
            def.requiredTier = 1;
            def.baseDuration = 1.4f;                  // 부수는 노드는 홀드하지 않지만 값은 채워 둔다
            def.durability = MushroomLumberRule.Durability;
            def.drops = loot;
            def.respawnSeconds = MushroomLumberRule.RegrowSeconds;

            _definition = def;
            return _definition;
        }

        /// <summary>
        /// 목재 아이템 정의를 씬의 아이템 데이터베이스에서 꺼낸다.
        /// 플레이어가 들고 있는 것이 게임이 실제로 읽는 목록이다.
        /// </summary>
        static ItemDataSO FindWoodItem()
        {
            var inv = Object.FindAnyObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            var db = inv != null ? inv.Database : null;
            return db != null ? db.GetById(MushroomLumberRule.WoodItemId) : null;
        }
    }
}
