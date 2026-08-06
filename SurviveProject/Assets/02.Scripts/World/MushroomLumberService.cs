using UnityEngine;
using UnityEngine.SceneManagement;
using Survive.Harvesting;
using Survive.Items;
using Survive.Localization;

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
        static Retrier _retrier;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            // 두 번 걸리지 않게 먼저 뗀다. 도메인 리로드를 끄고 재생하면
            // static 구독이 살아남아 같은 씬에 두 번 붙을 수 있다.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            // 이름은 정의를 만들 때 한 번 찍히므로, 그 뒤에 로케일이 바뀌면
            // 벌목 노드만 옛 언어로 남는다. 다시 찍어 준다.
            Loc.LocaleChanged -= RenameToCurrentLocale;
            Loc.LocaleChanged += RenameToCurrentLocale;

            BuildOrRetry();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => BuildOrRetry();

        /// <summary>
        /// 이미 세워 둔 정의의 이름만 지금 로케일로 바꾼다.
        /// 정의가 아직 없으면 할 일이 없다 — 만들 때 어차피 지금 로케일로 찍는다.
        /// </summary>
        static void RenameToCurrentLocale()
        {
            if (_definition != null) _definition.displayName = MushroomLumberRule.DisplayName;
        }

        /// <summary>
        /// 세우고, 한 번에 안 됐으면 잠깐 더 본다.
        ///
        /// 씬이 다 올라온 뒤에도 아이템 목록을 들고 있는 플레이어가 아직
        /// 서 있지 않은 순간이 있다(재생을 걸자마자 컴파일이 끼어들 때 실제로 그랬다).
        /// 그때 한 번 실패했다고 포기하면 그 판 내내 벌 나무가 하나도 없다.
        /// </summary>
        static void BuildOrRetry()
        {
            if (Build() > 0) return;

            if (_retrier == null)
            {
                var go = new GameObject("MushroomLumberService");
                Object.DontDestroyOnLoad(go);
                _retrier = go.AddComponent<Retrier>();
            }
            _retrier.Begin();
        }

        /// <summary>붙을 때까지 몇 초만 더 두드려 보는 일꾼.</summary>
        class Retrier : MonoBehaviour
        {
            const float GiveUpAfter = 6f;
            float _deadline;

            public void Begin()
            {
                _deadline = Time.unscaledTime + GiveUpAfter;
                enabled = true;
            }

            void Update()
            {
                if (Build() > 0 || Time.unscaledTime >= _deadline) enabled = false;
            }
        }

        /// <summary>
        /// 지금 열려 있는 씬을 훑어 벌목 노드를 세운다.
        /// 이미 세워진 것은 건드리지 않는다.
        /// </summary>
        /// <returns>세운(또는 이미 서 있던) 그루 수.</returns>
        public static int Build()
        {
            var definition = Definition();
            if (definition == null) { InstalledTrees = 0; return 0; }

            int count = 0;
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
                EnsureTrunkHit(t);
                count++;
            }

            if (count > 0 && count != InstalledTrees)
                Debug.Log($"[MushroomLumberService] 거대 버섯 {count}그루를 벌목 대상으로 세웠습니다.");

            InstalledTrees = count;
            return count;
        }

        /// <summary>때릴 자리. 밑동을 감싸는 이 이름의 트리거를 하나 붙인다.</summary>
        public const string TrunkHitName = "TrunkHit";

        /// <summary>
        /// 밑동에 <b>때릴 자리</b>를 하나 세운다.
        ///
        /// <b>왜 필요한가.</b> <see cref="Survive.Combat.MeleeSwing"/>은 콜라이더의
        /// <c>bounds.center</c>가 전방 원뿔(90도) 안에 있어야 때린 것으로 본다.
        /// 거대 버섯의 통짜 메시는 높이가 10m를 넘어 그 한가운데가 <b>사람 머리 위</b>다 —
        /// 밑동 앞에 서서 도끼를 휘두르면 원뿔을 벗어나 한 대도 들어가지 않는다.
        /// 게다가 그 한가운데는 대에서 갓 사이의 <b>빈 공간</b>이라 판정 구와 겹치지도 않는다.
        ///
        /// 나무를 벨 때 사람이 겨누는 곳은 밑동이다. 그 자리에 실제로 몸을 하나 세워 준다.
        /// 트리거로 두는 이유: 걸어 다니는 데 새 벽을 만들지 않으면서
        /// 조준(레이)과 타격(구 겹침)에는 잡히게 하려는 것이다.
        /// </summary>
        static void EnsureTrunkHit(Transform t)
        {
            for (int i = 0; i < t.childCount; i++)
                if (t.GetChild(i).name == TrunkHitName) return;

            var renderer = t.GetComponentInChildren<Renderer>(true);
            if (renderer == null) return;

            var b = renderer.bounds;
            float 높이 = Mathf.Min(2.4f, Mathf.Max(1f, b.size.y * 0.35f));
            float 반지름 = Mathf.Clamp(Mathf.Min(b.size.x, b.size.z) * 0.22f, 0.35f, 1.4f);

            var go = new GameObject(TrunkHitName);
            go.transform.SetParent(t, false);
            go.transform.position = new Vector3(b.center.x, b.min.y + 높이 * 0.5f, b.center.z);
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;   // 부모 스케일을 물려받지 않게 세계 기준으로 잰다

            var cap = go.AddComponent<CapsuleCollider>();
            cap.isTrigger = true;
            cap.direction = 1;                       // Y축
            cap.height = 높이 / Mathf.Max(0.01f, go.transform.lossyScale.y);
            cap.radius = 반지름 / Mathf.Max(0.01f, go.transform.lossyScale.x);
        }

        /// <summary>
        /// 그루터기 재생 시간을 바꾼다. 검증에서만 쓴다 —
        /// 300초를 실시간으로 기다리는 검사는 아무도 돌리지 않게 된다.
        /// 규칙(<see cref="MushroomLumberRule.RegrowSeconds"/>)은 건드리지 않고
        /// 지금 세워 둔 정의만 바꾼다.
        /// </summary>
        public static void OverrideRegrowSeconds(float seconds)
        {
            var def = Definition();
            if (def != null) def.respawnSeconds = Mathf.Max(0f, seconds);
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
                // 매 프레임 짖지 않는다. Retrier가 몇 초 동안 다시 물어보므로
                // 여기서 소리를 내면 콘솔이 같은 줄로 덮인다.
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning($"[MushroomLumberService] '{MushroomLumberRule.WoodItemId}' " +
                                     "아이템 정의를 아직 찾지 못했습니다. 잠시 뒤 다시 시도합니다.");
                }
                return null;
            }
            _warned = false;

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
            def.requiredTool = MushroomLumberRule.RequiredTool;   // 나무는 도끼로 벤다
            def.requiredTier = MushroomLumberRule.RequiredTier;
            def.baseDuration = 1.4f;                  // 부수는 노드는 홀드하지 않지만 값은 채워 둔다
            def.durability = MushroomLumberRule.Durability;
            def.drops = loot;
            def.respawnSeconds = MushroomLumberRule.RegrowSeconds;

            _definition = def;
            return _definition;
        }

        static bool _warned;

        /// <summary>
        /// 목재 아이템 정의를 아이템 데이터베이스에서 꺼낸다.
        ///
        /// 플레이어가 들고 있는 것이 게임이 실제로 읽는 목록이라 그쪽을 먼저 본다.
        /// 플레이어가 아직 씬에 서지 않았을 수 있어, 이미 메모리에 올라와 있는
        /// 데이터베이스 에셋으로 한 번 더 물어본다.
        /// </summary>
        static ItemDataSO FindWoodItem()
        {
            var inv = Object.FindAnyObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            var db = inv != null ? inv.Database : null;
            var wood = db != null ? db.GetById(MushroomLumberRule.WoodItemId) : null;
            if (wood != null) return wood;

            foreach (var loaded in Resources.FindObjectsOfTypeAll<ItemDatabaseSO>())
            {
                wood = loaded != null ? loaded.GetById(MushroomLumberRule.WoodItemId) : null;
                if (wood != null) return wood;
            }
            return null;
        }
    }
}
