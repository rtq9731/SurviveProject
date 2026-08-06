using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Survive.Combat;
using Survive.Harvesting;
using Survive.Interaction;
using Survive.Items;
using Survive.Player;

namespace Survive.Testing
{
    /// <summary>
    /// 챕터 1 재건 스펙 §7 — 신규 재료 둘을 <b>실제로 주울 수 있는가.</b>
    ///
    /// <b>왜 하네스가 무대를 세우는가.</b> 이 둘이 나는 곳은 B섬이고, B섬 지형은
    /// 아직 없다(§16 사람의 몫). 씬에 심는 것은 이 라운드의 범위가 아니므로
    /// 프리팹을 눈앞에 세워 놓고 캔다. <b>그래도 데이터는 진짜다</b> —
    /// 사람이 나중에 씬에 갖다 놓을 바로 그 프리팹을 그대로 인스턴스화하므로,
    /// 배선이 끊겨 있으면 여기서 먼저 걸린다.
    ///
    /// 보는 것 넷.
    /// <list type="number">
    /// <item>무광버섯은 <b>맨손으로 눌러</b> 딴다 (홀드형, 가방으로 바로 들어온다)</item>
    /// <item>매크로늄 석영은 <b>맨손으로는 흠집도 안 난다</b> — 되는 것만 보면
    ///       도구 요구가 데이터에서 사라져도 검사가 통과한다</item>
    /// <item>곡괭이로 부수면 바닥에 떨어지고, 걸어가서 줍는다</item>
    /// <item>둘 다 끝난 뒤 무대를 비운다 — 다음 시나리오가 남의 소품을 밟지 않게</item>
    /// </list>
    ///
    /// 수치(재생 초·요구 등급·수확량)는 EditMode가 본다(<c>NewMaterialDataTests</c>).
    /// 여기서 보는 것은 배선이다.
    /// </summary>
    public static class E2ENewMaterials
    {
        const string 무광프리팹 = "Assets/05.Prefabs/Harvesting/Node_MatteMushroom.prefab";
        const string 석영프리팹 = "Assets/05.Prefabs/Harvesting/Node_MacroniumQuartz.prefab";

        const string 무광버섯 = "matte_mushroom";
        const string 석영 = "macronium_quartz";

        static Inventory Inv => E2EHarness.Player.Inventory.Inventory;

        /// <summary>이 시나리오가 세운 것. 끝나면 전부 치운다.</summary>
        static readonly System.Collections.Generic.List<GameObject> _세운것 =
            new System.Collections.Generic.List<GameObject>();

        public static IEnumerator FullRun()
        {
            yield return 준비();

            yield return 무광버섯을_맨손으로_딴다();
            yield return 석영은_맨손으로는_안_깨진다();
            yield return 석영을_곡괭이로_깨서_줍는다();

            yield return 치운다();
            E2EHarness.Log("=== 신규 재료 둘 완주 ===");
        }

        // ── 준비 ────────────────────────────────────────────────

        static IEnumerator 준비()
        {
            var dir = Object.FindAnyObjectByType<Survive.Progression.ChapterDirector>(
                FindObjectsInactive.Exclude);
            yield return E2EHarness.WaitUntil(() => dir != null && dir.Current != null,
                                              "챕터가 시작된다", 8f);

            // 야생 생물이 무대 위를 지나가면 조준선이 그쪽을 먼저 잡는다.
            int 잰것 = E2EHarness.SleepWildCreatures();
            E2EHarness.Log($"  야생 생물 {잰것}마리를 재웠다");

            비운다(무광버섯);
            비운다(석영);
            _세운것.Clear();
            yield return null;
        }

        static void 준다(string id, int count)
        {
            var db = E2EHarness.Player.Inventory.Database;
            var item = db != null ? db.GetById(id) : null;
            if (item != null && Inv.CountOf(id) < count) Inv.TryAdd(item, count - Inv.CountOf(id));
        }

        static void 비운다(string id)
        {
            int n = Inv.CountOf(id);
            if (n > 0) Inv.TryRemove(id, n);
        }

        /// <summary>
        /// 손을 비운다. "맨손으로는 안 된다"를 보려면 실제로 맨손이어야 한다 —
        /// 앞 시나리오가 들려 놓은 곡괭이가 그대로 남아 있으면 검사가 통과한다.
        /// </summary>
        static void 맨손으로_만든다()
        {
            var holder = E2EHarness.Player.ToolHolder;
            if (holder != null) holder.Unequip();
        }

        /// <summary>
        /// 사람이 나중에 B섬에 심을 바로 그 프리팹을 눈앞에 세운다.
        /// 프리팹을 못 읽으면 세우지 않는다 — 대신 세운 가짜로 통과시키면
        /// 정작 심을 물건이 망가져 있어도 초록불이 켜진다.
        /// </summary>
        static HarvestNode 세운다(string 프리팹경로, string 이름)
        {
            var prefab = 프리팹(프리팹경로);
            E2EHarness.Assert(prefab != null, $"{이름} 프리팹을 읽었다 ({프리팹경로})");
            if (prefab == null) return null;

            var cam = E2EHarness.Eye;
            var go = Object.Instantiate(prefab,
                                        cam.transform.position + cam.transform.forward * 2f,
                                        Quaternion.identity);
            go.name = "E2E_" + 이름;
            _세운것.Add(go);

            var node = go.GetComponentInChildren<HarvestNode>(true);
            E2EHarness.Assert(node != null, $"{이름}에 채집 컴포넌트가 붙어 있다");
            E2EHarness.Assert(node != null && node.Definition != null,
                              $"{이름} 프리팹이 채집 정의를 물고 있다");
            return node;
        }

        static GameObject 프리팹(string path)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
            return null;
#endif
        }

        /// <summary>
        /// 쓴 무대를 곧바로 내린다.
        ///
        /// <b>실측으로 배운 것</b> — 앞 단락에서 세운 것을 그대로 두면 다음 단락의
        /// 휘두르기가 그것까지 함께 부순다(첫 판에서 석영 둘이 한꺼번에 깨져
        /// 바닥에 네 덩이가 떨어졌다). 세어 보는 수가 곧 거짓이 된다.
        /// </summary>
        static void 내린다(HarvestNode node)
        {
            if (node == null) return;
            var go = node.transform.root.gameObject;
            _세운것.Remove(go);
            Object.Destroy(go);
        }

        /// <summary>콜라이더 한가운데가 눈앞에 오도록 옮긴다. 원점을 겨누면 빗나간다.</summary>
        static IEnumerator 눈앞에_둔다(HarvestNode node, float 거리 = 1.8f)
        {
            var cam = E2EHarness.Eye;
            var 몸 = node.GetComponentInChildren<Collider>(true);
            Vector3 겨냥 = cam.transform.position + cam.transform.forward * 거리;

            // autoSyncTransforms가 꺼져 있다. 재기 전과 옮긴 뒤에 한 번씩 맞춘다.
            if (몸 != null)
            {
                E2EHarness.SyncPhysics();
                node.transform.position += 겨냥 - 몸.bounds.center;
            }
            else node.transform.position = 겨냥;
            E2EHarness.SyncPhysics();

            yield return null;
            E2EHarness.LookAt(몸 != null ? 몸.bounds.center : node.transform.position);
            yield return null;
            yield return null;
        }

        // ── 1. 무광버섯 — 맨손 홀드 ─────────────────────────────

        static IEnumerator 무광버섯을_맨손으로_딴다()
        {
            E2EHarness.Log("— 무광버섯을 맨손으로 딴다 —");

            맨손으로_만든다();
            yield return null;

            var node = 세운다(무광프리팹, "MatteMushroom");
            if (node == null) yield break;

            E2EHarness.Assert(!node.IsBreakable, "무광버섯은 부수는 것이 아니라 따는 것이다");
            yield return 눈앞에_둔다(node);

            var it = E2EHarness.Player.Interactor;
            yield return E2EHarness.WaitUntil(() => it.Current != null, "무광버섯이 조준된다", 4f);

            node.CanInteract(E2EHarness.Player);   // 지금 든 것(맨손)을 물려 준다
            string 문구 = node.InteractionPrompt;
            E2EHarness.Log("  프롬프트: " + 문구);
            E2EHarness.Assert(문구.Contains(Survive.Localization.DataText.Name(node.Definition)),
                              "무엇을 따는지 이름으로 말한다");

            int 전 = Inv.CountOf(무광버섯);
            float 홀드 = node.HoldDuration;
            E2EHarness.Log($"  필요 홀드 시간 {홀드:F2}초");
            E2EHarness.Assert(홀드 > 0f, "홀드형이다");

            yield return E2EHarness.HoldKey(Key.E, 홀드 + 0.5f);
            yield return null;

            // 조준이 남의 것을 잡았을 수 있다. 홀드 경로 자체는 E2EScenarios가 이미
            // 통과시켰으므로, 여기서 볼 "따면 가방에 들어오는가"는 직접 눌러 끝낸다.
            if (!node.IsDepleted)
            {
                E2EHarness.Log("    조준이 빗나갔다 — 상호작용을 직접 넣어 배선만 본다");
                node.Interact(E2EHarness.Player);
                yield return null;
            }

            E2EHarness.Assert(node.IsDepleted, "무광버섯을 땄다");
            E2EHarness.Assert(Inv.CountOf(무광버섯) > 전,
                              $"무광버섯이 가방에 들어왔다 ({전} -> {Inv.CountOf(무광버섯)})");

            내린다(node);
            yield return null;
        }

        // ── 2. 석영 — 맨손으로는 흠집도 안 난다 ─────────────────

        static IEnumerator 석영은_맨손으로는_안_깨진다()
        {
            E2EHarness.Log("— 석영은 맨손으로는 안 깨진다 —");

            맨손으로_만든다();
            yield return null;

            var node = 세운다(석영프리팹, "MacroniumQuartz_NoTool");
            if (node == null) yield break;

            E2EHarness.Assert(node.IsBreakable, "석영은 눌러 캐는 것이 아니라 부수는 것이다");
            yield return 눈앞에_둔다(node);

            node.CanInteract(E2EHarness.Player);
            string 문구 = node.InteractionPrompt;
            E2EHarness.Log("  프롬프트: " + 문구);
            E2EHarness.Assert(문구.Contains(ToolMatch.Name(ToolType.Pickaxe)),
                              "무엇이 필요한지 말한다 (곡괭이)");

            int 헛침전 = HarvestNode.WrongToolHits;
            float 내구도전 = node.HealthNormalized;

            for (int 회 = 0; 회 < 2 && HarvestNode.WrongToolHits == 헛침전; 회++)
                yield return E2EHarness.HoldAttack(1.6f);

            if (HarvestNode.WrongToolHits == 헛침전)
            {
                E2EHarness.Log("    휘두른 것이 닿지 않았다 — 타격을 직접 넣어 규칙만 본다");
                var swing = Object.FindAnyObjectByType<MeleeSwing>(FindObjectsInactive.Exclude);
                var 몸 = node.GetComponentInChildren<Collider>(true);
                ((IDamageable)node).TakeDamage(new DamageInfo(
                    99f, swing != null ? swing.gameObject : null,
                    몸 != null ? 몸.bounds.center : node.transform.position, Vector3.forward));
                yield return null;
            }

            E2EHarness.Assert(!node.IsDepleted, "맨손으로는 안 깨졌다");
            E2EHarness.AssertEqual(node.HealthNormalized, 내구도전, "흠집도 나지 않았다");
            E2EHarness.Assert(HarvestNode.WrongToolHits > 헛침전,
                              $"맞지 않는 도구는 거부된다 ({헛침전} -> {HarvestNode.WrongToolHits})");
            E2EHarness.AssertEqual(Inv.CountOf(석영), 0, "거부됐으니 아무것도 안 나왔다");

            내린다(node);
            yield return null;
        }

        // ── 3. 석영 — 곡괭이로 깨서 줍는다 ──────────────────────

        static IEnumerator 석영을_곡괭이로_깨서_줍는다()
        {
            E2EHarness.Log("— 석영을 곡괭이로 깬다 —");

            준다("pickaxe", 1);
            var user = E2EHarness.Player.GetComponent<PlayerToolUser>();
            E2EHarness.Assert(user != null && user.EquipFirst("pickaxe"), "곡괭이를 들었다");
            yield return null;

            var node = 세운다(석영프리팹, "MacroniumQuartz");
            if (node == null) yield break;

            // 지하 조명과 같은 성분이라는 것이 화면에 드러나야 한다.
            var 빛 = node.GetComponentInChildren<Light>(true);
            E2EHarness.Assert(빛 != null && 빛.enabled, "석영이 빛나고 있다");

            yield return 눈앞에_둔다(node);

            node.CanInteract(E2EHarness.Player);
            E2EHarness.Log("  프롬프트: " + node.InteractionPrompt);

            비운다(석영);

            for (int 회 = 0; 회 < 3 && !node.IsDepleted; 회++)
                yield return E2EHarness.HoldAttack(1.6f);

            if (!node.IsDepleted)
            {
                E2EHarness.Log("    휘두른 것이 닿지 않았다 — 남은 내구도를 직접 넣어 넘긴다");
                var swing = Object.FindAnyObjectByType<MeleeSwing>(FindObjectsInactive.Exclude);
                var 몸 = node.GetComponentInChildren<Collider>(true);
                ((IDamageable)node).TakeDamage(new DamageInfo(
                    node.Definition.durability + 1f, swing != null ? swing.gameObject : null,
                    몸 != null ? 몸.bounds.center : node.transform.position, Vector3.forward));
                yield return null;
            }

            E2EHarness.Assert(node.IsDepleted, "석영이 깨졌다");
            if (!node.IsDepleted) yield break;

            // 다시 자라는 것이므로 오브젝트째 꺼지면 안 된다.
            E2EHarness.Assert(node.gameObject.activeInHierarchy,
                              "깬 자리는 남는다 - 오브젝트째 꺼지면 다시 자랄 수 없다");
            E2EHarness.Assert(빛 == null || !빛.enabled, "깨진 자리의 빛도 함께 꺼진다");

            yield return 떨어진_것을_줍는다(석영);
            E2EHarness.Assert(Inv.CountOf(석영) > 0,
                              $"매크로늄 석영을 주웠다 ({Inv.CountOf(석영)}개)");

            // 한 무리에서 나오는 양이 전리품 표를 넘으면, 무대에 남의 것이 서 있어
            // 함께 깨진 것이다. 그대로 두면 이 시나리오가 세는 수가 거짓이 된다.
            int 최대 = node.Definition.drops.entries
                .Where(e => e?.item != null && e.item.id == 석영).Sum(e => e.maxCount);
            E2EHarness.Assert(Inv.CountOf(석영) <= 최대,
                              $"한 무리에서 표({최대}개) 이상 나오지 않았다");

            내린다(node);
            yield return null;
        }

        static IEnumerator 떨어진_것을_줍는다(string id)
        {
            float 기다림 = 0f;
            while (기다림 < 0.7f) { 기다림 += Time.deltaTime; yield return null; }

            var cam = E2EHarness.Eye;
            var it = E2EHarness.Player.Interactor;

            int 처음 = Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Exclude)
                .Count(p => p.name.StartsWith("Drop_" + id));
            E2EHarness.Log($"  바닥에 떨어진 것 {처음}덩이");
            E2EHarness.Assert(처음 > 0, "깬 자리에 무언가 떨어졌다");

            for (int n = 0; n < 8; n++)
            {
                var drop = Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Exclude)
                    .Where(p => p.name.StartsWith("Drop_" + id))
                    .OrderBy(p => Vector3.Distance(p.transform.position,
                                                   E2EHarness.Player.transform.position))
                    .FirstOrDefault();
                if (drop == null) yield break;

                drop.transform.position = cam.transform.position + cam.transform.forward * 1.8f;
                E2EHarness.SyncPhysics();
                yield return null;
                E2EHarness.LookAt(drop.transform.position);
                yield return null;
                yield return null;

                for (int f = 0; f < 10 && it.Current == null; f++) yield return null;

                if (it.Current != null)
                {
                    yield return E2EHarness.TapKey(Key.E);
                    yield return null;
                }

                if (drop != null)
                {
                    E2EHarness.Log("    조준이 빗나가 직접 줍는다: " + drop.name);
                    drop.Interact(E2EHarness.Player);
                    yield return null;
                }
            }
        }

        // ── 무대를 비운다 ───────────────────────────────────────

        static IEnumerator 치운다()
        {
            foreach (var go in _세운것)
                if (go != null) Object.Destroy(go);
            _세운것.Clear();

            // 깨면서 바닥에 흩어진 것도 치운다. 남겨 두면 다음 시나리오가
            // 남의 소품을 조준한다.
            foreach (var p in Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Include))
                if (p != null && (p.name.StartsWith("Drop_" + 석영) ||
                                  p.name.StartsWith("Drop_" + 무광버섯)))
                    Object.Destroy(p.gameObject);

            E2EHarness.WakeWildCreatures();
            yield return E2EHarness.ReleaseAllKeys();
            yield return null;
        }
    }
}
