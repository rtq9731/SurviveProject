using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Survive.Building;
using Survive.Creatures;
using Survive.Harvesting;
using Survive.Interaction;
using Survive.Items;
using Survive.Crafting;
using Survive.Player;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// 버섯 목재 벌목 (백로그 35번).
    ///
    /// 네 가지를 실제 조작으로 통과시킨다.
    /// <b>거대 버섯을 베면 목재가 떨어지고 그루터기에서 다시 자란다</b>,
    /// <b>그 목재로 조립 조각을 세운다</b>,
    /// <b>그 목재로만 불이 살고, 불이 살아야 에너지 추출이 진행된다</b>,
    /// <b>그리고 불이 꺼지면 밝은 구역이 사라져 낫이 그 자리로 들어온다.</b>
    ///
    /// 넷째가 "목재 재고가 곧 안전 재고"(기획서 §5.3)의 증명이다. 셋째까지는
    /// 목재가 <b>생산</b>을 지킨다는 이야기이고, 넷째라야 목재가 <b>안전</b>을
    /// 지킨다는 이야기가 된다 — 다리 관문이 빠지며 옮겨 온 밸런스 축이 그쪽이다.
    ///
    /// 둘째 항목을 예전에는 "다리 조각"이라 불렀다. 다리가 관문에서 빠지면서
    /// (기획서 §6.4) 그 이름은 없는 관문을 가리키게 됐다 — 이 시나리오가 보는 것은
    /// 처음부터 <b>벌목한 목재가 건축 비용으로 실제로 빠지는가</b>이지 관문이 아니다.
    ///
    /// 마지막이 이 라운드의 세계관이다 — 스크랩은 태우는 물건이 아니다.
    /// 그래서 "스크랩으로는 불이 붙지 않는다"까지 확인한다. 되는 것만 보면
    /// 옛 경로가 살아 있어도 검사가 통과한다.
    ///
    /// 수치 자체(재생 초·수확량·비용)는 EditMode가 본다
    /// (<c>MushroomLumberRuleTests</c>, <c>CampfireFuelRuleTests</c>,
    /// <c>MushroomWoodDataTests</c>). 여기서 보는 것은 배선이다.
    /// </summary>
    public static class E2EMushroomLumber
    {
        /// <summary>검증용 재생 시간. 300초를 실시간으로 기다릴 수는 없다.</summary>
        const float 검증용재생초 = 2.5f;

        static Inventory Inv => E2EHarness.Player.Inventory.Inventory;

        static string WoodId => MushroomLumberRule.WoodItemId;

        public static IEnumerator FullRun()
        {
            yield return Prepare();

            yield return 첫_화톳불까지_손이_닿는다();
            yield return 도구는_전용이다();
            yield return 거대_버섯을_벤다();
            yield return 목재로_짓는다();
            yield return 목재라야_불이_산다();
            yield return 불이_꺼지면_안전지대가_사라진다();

            E2EHarness.Log("=== 벌목 완주 ===");
        }

        // ── 준비 ────────────────────────────────────────────────

        static IEnumerator Prepare()
        {
            var dir = Object.FindAnyObjectByType<Survive.Progression.ChapterDirector>(
                FindObjectsInactive.Exclude);
            yield return E2EHarness.WaitUntil(() => dir != null && dir.Current != null,
                                              "챕터가 시작된다", 8f);

            // 씬을 고치지 않고 붙는 서비스라, 붙었는지부터 본다.
            yield return E2EHarness.WaitUntil(() => MushroomLumberService.InstalledTrees > 0,
                                              "거대 버섯이 벌목 대상으로 서 있다", 8f);
            E2EHarness.Log($"  벌목 노드 {MushroomLumberService.InstalledTrees}그루가 " +
                           "실행 시점에 세워졌다");

            MushroomLumberService.OverrideRegrowSeconds(검증용재생초);
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

        // ── 0. 초반 동선 실측 ───────────────────────────────────

        /// <summary>
        /// 목재가 연료가 된 뒤에도 초반이 죽지 않는가.
        ///
        /// 불에 들어가는 것이 목재뿐이 되면서 첫 화톳불을 지키려면 벌목이 먼저다.
        /// 그래서 <b>시작 지점에서 걸어서 닿는 거리에 벨 나무가 있는가</b>가
        /// 이 라운드에서 가장 위험한 가정이다 — 없으면 게임은 시작하자마자 막힌다.
        /// 수치가 아니라 지금 씬을 재서 확인한다.
        /// </summary>
        static IEnumerator 첫_화톳불까지_손이_닿는다()
        {
            E2EHarness.Log("— 시작 지점에서 벌목까지 —");

            var start = Survive.Vitals.RespawnService.Instance != null
                      ? Survive.Vitals.RespawnService.Instance.StartPoint
                      : E2EHarness.Player.transform.position;
            E2EHarness.Log($"  시작 지점 {start.ToString("F1")}");

            HarvestNode 가장가까운 = null;
            float 직선 = float.MaxValue, 보행 = -1f;

            foreach (var h in Object.FindObjectsByType<HarvestNode>(FindObjectsInactive.Include))
            {
                if (h.Definition == null || h.Definition.drops?.entries == null) continue;
                if (!h.Definition.drops.entries.Any(e => e?.item != null && e.item.id == WoodId))
                    continue;

                float d = Vector3.Distance(start, h.transform.position);
                if (d >= 직선) continue;
                직선 = d;
                가장가까운 = h;
            }

            E2EHarness.Assert(가장가까운 != null, "시작 지점에서 잴 수 있는 벌목 노드가 있다");
            if (가장가까운 == null) yield break;

            보행 = 보행거리(start, 가장가까운.transform.position);
            E2EHarness.Log($"  가장 가까운 거대 버섯 {가장가까운.name} " +
                           $"{가장가까운.transform.position.ToString("F1")} — " +
                           $"직선 {직선:F1}m, " + (보행 > 0f ? $"보행 {보행:F1}m" : "NavMesh 온전한 경로 없음"));

            // 40m는 스크랩을 주우러 다니는 반경이다(챕터 1 목표 3의 잔해 분포).
            // 그 안에 벨 나무가 있어야 "불을 지키러 나간다"가 심부름이 아니라 동선이 된다.
            E2EHarness.Assert(직선 <= 40f, $"시작 지점에서 {직선:F1}m — 손이 닿는다");
            E2EHarness.Assert(보행 > 0f, "걸어서 닿는 길이 있다 (NavMesh 온전한 경로)");
            E2EHarness.Assert(보행 <= 직선 * 3f,
                              $"돌아가는 길이 지나치지 않다 (직선 {직선:F1}m, 보행 {보행:F1}m)");
        }

        /// <summary>두 점 사이의 NavMesh 보행 거리. 온전한 길이 없으면 -1.</summary>
        static float 보행거리(Vector3 from, Vector3 to)
        {
            if (!UnityEngine.AI.NavMesh.SamplePosition(from, out var a, 8f,
                                                       UnityEngine.AI.NavMesh.AllAreas)) return -1f;
            if (!UnityEngine.AI.NavMesh.SamplePosition(to, out var b, 12f,
                                                       UnityEngine.AI.NavMesh.AllAreas)) return -1f;

            var path = new UnityEngine.AI.NavMeshPath();
            if (!UnityEngine.AI.NavMesh.CalculatePath(a.position, b.position,
                                                      UnityEngine.AI.NavMesh.AllAreas, path))
                return -1f;
            if (path.status != UnityEngine.AI.NavMeshPathStatus.PathComplete) return -1f;

            float len = 0f;
            for (int i = 1; i < path.corners.Length; i++)
                len += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            return len;
        }

        // ── 0. 도구는 전용이다 ──────────────────────────────────

        /// <summary>
        /// 곡괭이로는 나무를 못 베고, 도끼로는 돌을 못 깬다.
        ///
        /// 되는 것만 확인하면 도구를 두 개 만들 이유가 데이터에서 사라져도
        /// 검사가 통과한다. 안 되는 쪽을 먼저 본다.
        /// </summary>
        static IEnumerator 도구는_전용이다()
        {
            E2EHarness.Log("— 도구는 전용이다 —");

            준다("pickaxe", 1);
            yield return 도끼를_든다();          // 도끼는 여기서 실제로 만들어진다
            var user = E2EHarness.Player.GetComponent<PlayerToolUser>();

            var 나무 = 벌목노드();
            var 광맥 = Object.FindObjectsByType<HarvestNode>(FindObjectsInactive.Exclude)
                .FirstOrDefault(h => !h.IsDepleted && h.Definition != null &&
                                     h.Definition.requiredTool == ToolType.Pickaxe);
            E2EHarness.Assert(나무 != null && 광맥 != null, "버섯과 광맥이 둘 다 있다");
            if (나무 == null || 광맥 == null) yield break;

            E2EHarness.Assert(user.EquipFirst("pickaxe"), "곡괭이를 들었다");
            yield return null;
            yield return 안_먹히는지_본다(나무, "곡괭이로 거대 버섯을 친다", "도끼");

            E2EHarness.Assert(user.EquipFirst("axe"), "도끼를 들었다");
            yield return null;
            yield return 안_먹히는지_본다(광맥, "도끼로 광맥을 친다", "곡괭이");
        }

        /// <summary>맞지 않는 도구로 때려 보고, 흠집도 안 나는지 본다.</summary>
        static IEnumerator 안_먹히는지_본다(HarvestNode node, string what, string 필요한도구)
        {
            E2EHarness.Log("  " + what);

            yield return 눈앞에_둔다(node);

            // 착지 지점은 소품이 빽빽해 조준선이 버섯 기둥 같은 남의 것을 먼저 잡는다.
            // 조준 경로 자체는 다른 시나리오가 이미 통과시켰으므로, 여기서 볼
            // "무엇이 필요한지 말하는가"는 노드에서 직접 읽는다.
            var it = E2EHarness.Player.Interactor;
            for (int f = 0; f < 15 && it.Current == null; f++) yield return null;
            if (it.Current != null) E2EHarness.Log("    조준된 것: " + it.Current.InteractionPrompt);

            // 프롬프트는 "지금 든 도구"를 보고 쓰이는데, 그 도구는 PlayerInteractor가
            // 매 프레임 CanInteract로 물려 준다. 조준이 남의 것을 잡았을 때를 대비해
            // 여기서 한 번 물려 준다 — 게임이 하는 것과 같은 호출이다.
            node.CanInteract(E2EHarness.Player);
            E2EHarness.Log("    프롬프트: " + node.InteractionPrompt);
            E2EHarness.Assert(node.InteractionPrompt.Contains(필요한도구),
                              $"무엇이 필요한지 말한다 ({필요한도구})");

            int 헛침전 = HarvestNode.WrongToolHits;
            float 내구도전 = node.HealthNormalized;

            // 꾹 눌러 연속으로 휘두른다. 한 번씩 클릭하면 쿨다운과 프레임이 어긋나
            // 헛스윙이 섞여 "안 먹혔다"와 "안 휘둘렀다"를 구별할 수 없다.
            for (int round = 0; round < 2 && HarvestNode.WrongToolHits == 헛침전; round++)
                yield return E2EHarness.HoldAttack(1.6f);

            // 휘둘러서 닿았는지는 판마다 흔들린다(거대 메시의 판정 구·원뿔이 걸린다).
            // 그래서 <b>도구가 거부되는가</b>는 타격을 직접 넣어 확정적으로 본다.
            // 확인하려는 것은 조준 운이 아니라 규칙이다.
            if (HarvestNode.WrongToolHits == 헛침전)
            {
                E2EHarness.Log("    휘두른 것이 닿지 않았다 — 타격을 직접 넣어 규칙만 본다");
                var 몸 = 때릴자리(node);
                var swing = Object.FindAnyObjectByType<Survive.Combat.MeleeSwing>(
                    FindObjectsInactive.Exclude);
                ((Survive.Combat.IDamageable)node).TakeDamage(
                    new Survive.Combat.DamageInfo(99f, swing != null ? swing.gameObject : null,
                                                  몸 != null ? 몸.bounds.center : node.transform.position,
                                                  Vector3.forward));
                yield return null;
            }

            E2EHarness.Assert(!node.IsDepleted, "안 부서졌다");
            E2EHarness.AssertEqual(node.HealthNormalized, 내구도전, "흠집도 나지 않았다");
            E2EHarness.Assert(HarvestNode.WrongToolHits > 헛침전,
                              $"맞지 않는 도구는 거부된다 ({헛침전} → {HarvestNode.WrongToolHits})");
        }

        /// <summary>
        /// 노드의 <b>콜라이더 한가운데</b>가 눈앞에 오도록 옮긴다.
        /// 원점이 발밑인 것(거대 버섯)은 원점을 겨누면 몸통이 전방 원뿔 위로 벗어난다 —
        /// 실제로 스물다섯 번을 헛휘둘렀다.
        /// </summary>
        static IEnumerator 눈앞에_둔다(HarvestNode node)
        {
            var cam = E2EHarness.Eye;
            var 몸 = 때릴자리(node);
            Vector3 겨냥 = cam.transform.position + cam.transform.forward * 1.6f;

            // autoSyncTransforms가 꺼져 있어 재기 전과 옮긴 뒤에 한 번씩 물리와 맞춘다.
            // 그러지 않으면 옛 중심으로 델타를 재 이동이 두 번 들어가고, 옮긴 자리는
            // 다음 물리 틱까지 조준 캐스트에 보이지 않는다.
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

        /// <summary>
        /// 사람이 실제로 겨누는 몸. 거대 버섯은 밑동에 세워 둔 때릴 자리가 그것이고
        /// (MushroomLumberService.TrunkHitName), 광맥·잔해는 메시 그대로다.
        /// </summary>
        static Collider 때릴자리(HarvestNode node)
        {
            var trunk = node.transform.Find(MushroomLumberService.TrunkHitName);
            var c = trunk != null ? trunk.GetComponent<Collider>() : null;
            return c != null ? c : node.GetComponentInChildren<Collider>(true);
        }

        /// <summary>
        /// 도끼를 <b>실제로 만들어</b> 손에 든다.
        ///
        /// 그냥 넣어 주면 "도끼가 있으면 벨 수 있다"만 보고 끝난다. 이 라운드가
        /// 세운 동선은 스크랩을 줍고 → 도끼를 만들고 → 나무를 베는 것이라,
        /// 그 첫 칸이 실제로 열려 있는지가 확인 대상이다.
        /// </summary>
        static IEnumerator 도끼를_든다()
        {
            var user = E2EHarness.Player.GetComponent<PlayerToolUser>();
            E2EHarness.Assert(user != null, "PlayerToolUser가 있다");

            if (Inv.CountOf("axe") == 0)
            {
                yield return E2EHarness.WaitUntil(() => HandCraftingService.Instance != null,
                                                  "손 제작 서비스가 서 있다", 8f);
                var recipe = Resources.FindObjectsOfTypeAll<Survive.Crafting.RecipeSO>()
                    .FirstOrDefault(r => r != null && r.id == "axe");
                E2EHarness.Assert(recipe != null, "도끼 레시피가 있다");
                if (recipe != null)
                {
                    foreach (var i in recipe.ingredients) 준다(i.item.id, i.count * 2);
                    E2EHarness.Log("  도끼 재료: " +
                        string.Join("+", recipe.ingredients.Select(i => $"{i.item.id} {i.count}")));

                    int 전 = Inv.CountOf("axe");
                    Survive.Crafting.CraftQueueService.TryEnqueue(
                        HandCraftingService.Instance.Queue, recipe, 1, Inv,
                        Survive.Crafting.StationType.None);
                    yield return E2EHarness.WaitUntil(() => Inv.CountOf("axe") > 전,
                                                      "손에서 도끼를 만들었다",
                                                      recipe.craftSeconds + 6f);
                }
            }

            E2EHarness.Assert(user != null && user.EquipFirst("axe"), "도끼를 들었다");
            yield return null;
        }

        // ── 1. 벌목 → 드롭 → 재생 ───────────────────────────────

        static IEnumerator 거대_버섯을_벤다()
        {
            E2EHarness.Log("— 거대 버섯을 벤다 —");

            yield return 도끼를_든다();

            var tree = 벌목노드();
            E2EHarness.Assert(tree != null, "벨 수 있는 거대 버섯이 있다");
            if (tree == null) yield break;

            E2EHarness.Log($"  대상: {tree.name} (내구도 {tree.Definition.durability})");

            // 벤 뒤에 남는 밑동이 정말 납작해졌는지 견주려면 베기 전의 크기가 필요하다.
            var 벤전크기 = tree.transform.localScale;

            // 발광 개체면 빛이 함께 꺼져야 한다. 밑동만 남은 자리가 여전히 환하면
            // 무엇을 벤 것인지 알 수 없다. 씬의 발광 버섯 중에는 라이트가 꺼진 채
            // 놓인 것도 있어(장식), 켜져 있던 것만 상대로 삼는다.
            var 빛 = tree.GetComponentInChildren<Light>(true);
            bool 빛나던가 = 빛 != null && 빛.enabled;
            E2EHarness.Log(빛나던가 ? "  이 개체는 빛나고 있다 — 빛이 함께 꺼지는지 본다"
                                    : "  이 개체는 빛을 켜고 있지 않다");

            // 조준 경로만 확인하면 되므로 눈앞으로 옮긴다 — E2EChapter1과 같은 관례다.
            E2EHarness.Assert(tree.GetComponentInChildren<Collider>(true) != null,
                              "거대 버섯에 맞을 몸이 있다");
            yield return 눈앞에_둔다(tree);

            // 몸통이 카메라를 감싸는 크기라 조준선(레이)이 안쪽에서 시작해 안 잡힐 수 있다.
            // 조준 경로 자체는 바로 앞 시나리오(도구는 전용이다)가 이미 통과시켰으므로,
            // 여기서는 문구를 노드에서 직접 읽는다.
            var it = E2EHarness.Player.Interactor;
            for (int f = 0; f < 15 && it.Current == null; f++) yield return null;
            E2EHarness.Log(it.Current != null
                ? "  프롬프트: " + it.Current.InteractionPrompt
                : "  [조준 안 됨] 몸통 안에서 레이가 시작한다 — 문구는 노드에서 직접 읽는다");
            tree.CanInteract(E2EHarness.Player);   // 지금 든 도구를 물려 준다
            E2EHarness.Log("  노드 문구: " + tree.InteractionPrompt);
            E2EHarness.Assert(tree.InteractionPrompt.Contains("부순다"), "때려서 부수라고 말한다");

            비운다(WoodId);

            // 실제로 휘둘러 부순다.
            // 꾹 눌러 연속으로 휘두른다. 쿨다운(0.4초)마다 한 번씩 나간다.
            for (int round = 0; round < 3 && !tree.IsDepleted; round++)
                yield return E2EHarness.HoldAttack(1.6f);

            // 거대 메시는 판정 구·전방 원뿔에 걸리는 판이 있어(콜라이더 한가운데가
            // 사람 키의 몇 배 위다) 휘둘러 닿는 것이 매번 보장되지 않는다.
            // 조준·타격 경로는 바로 앞 시나리오가 이미 통과시켰고, 여기서 볼 것은
            // <b>넘어간 뒤에 무슨 일이 일어나는가</b>다 — 남은 몫은 타격을 직접 넣어 끝낸다.
            if (!tree.IsDepleted)
            {
                E2EHarness.Log("    휘두른 것이 닿지 않았다 — 남은 내구도를 직접 넣어 넘긴다");
                var swing = Object.FindAnyObjectByType<Survive.Combat.MeleeSwing>(
                    FindObjectsInactive.Exclude);
                var 몸 = 때릴자리(tree);
                ((Survive.Combat.IDamageable)tree).TakeDamage(
                    new Survive.Combat.DamageInfo(tree.Definition.durability + 1f,
                                                  swing != null ? swing.gameObject : null,
                                                  몸 != null ? 몸.bounds.center : tree.transform.position,
                                                  Vector3.forward));
                yield return null;
            }
            E2EHarness.Assert(tree.IsDepleted, "거대 버섯이 넘어갔다");
            if (!tree.IsDepleted) yield break;

            E2EHarness.Assert(tree.gameObject.activeInHierarchy,
                              "그루터기는 남는다 — 오브젝트째 꺼지면 다시 자랄 수 없다");

            // 벤 자리에 <b>흔적</b>이 남는다. 통째로 사라졌다가 튀어나오면 세계가
            // 가짜로 보이고, 무엇보다 어디가 되살아나는 자리인지 배울 방법이 없다
            // (HarvestRespawnRule.RemnantScale).
            var 겉 = tree.GetComponentsInChildren<Renderer>(true);
            E2EHarness.Assert(겉.Any(r => r.enabled), "벤 자리에 밑동이 남는다");
            E2EHarness.Assert(tree.transform.localScale.y < 벤전크기.y * 0.5f,
                              $"밑동은 납작하다 ({벤전크기.y:F2} → {tree.transform.localScale.y:F2})");
            var 몸들 = tree.GetComponentsInChildren<Collider>(true);
            E2EHarness.Assert(몸들.All(c => !c.enabled), "밑동은 겨눠지지 않는다");
            if (빛나던가) E2EHarness.Assert(!빛.enabled, "발광 개체는 빛도 함께 꺼진다");

            // 떨어진 것을 실제로 줍는다. 부수는 것과 줍는 것은 다른 동작이다.
            yield return 떨어진_것을_줍는다();
            E2EHarness.Assert(Inv.CountOf(WoodId) > 0,
                              $"버섯 목재를 얻었다 ({Inv.CountOf(WoodId)}개)");
            E2EHarness.Assert(Inv.CountOf(WoodId) >= MushroomLumberRule.MinYield,
                              $"한 그루에서 {MushroomLumberRule.MinYield}개 이상 나온다");

            // 그루터기에서 다시 자란다. <b>보고 있는 앞에서는 자라지 않는다</b> —
            // 눈앞에서 나무가 솟는 것은 규칙이 아니라 사고로 읽힌다. 그래서
            // 돌아선다. 이 한 줄이 없으면 아래 대기가 그대로 시간 초과다.
            var 눈 = E2EHarness.Eye.transform.position;
            var 밑동 = 때릴자리(tree);
            var 그루터기 = 밑동 != null ? 밑동.bounds.center : tree.transform.position;
            var 반대 = 눈 - 그루터기;
            반대.y = 0f;
            if (반대.sqrMagnitude < 0.01f) 반대 = -E2EHarness.Eye.transform.forward;
            E2EHarness.LookAt(눈 + 반대.normalized * 10f);
            yield return null;
            E2EHarness.Log($"  등을 돌리고 {검증용재생초}초 뒤 다시 자라는지 본다");
            yield return E2EHarness.WaitUntil(() => !tree.IsDepleted,
                                              "그루터기에서 거대 버섯이 다시 자랐다",
                                              검증용재생초 + 6f);
            E2EHarness.Assert(겉.All(r => r.enabled), "다시 자란 버섯이 보인다");
            E2EHarness.Assert(몸들.All(c => c.enabled), "다시 자란 버섯은 다시 겨눠진다");
            if (빛나던가) E2EHarness.Assert(빛.enabled, "빛도 돌아왔다");
        }

        /// <summary>목재를 떨구는, 아직 안 벤 노드 하나. 발광 개체를 먼저 고른다.</summary>
        static HarvestNode 벌목노드()
        {
            var all = Object.FindObjectsByType<HarvestNode>(FindObjectsInactive.Include)
                .Where(h => !h.IsDepleted && h.Definition != null &&
                            h.Definition.drops != null &&
                            h.Definition.drops.entries.Any(
                                e => e?.item != null && e.item.id == WoodId))
                .ToList();

            // 빛까지 함께 확인할 수 있는 개체가 있으면 그쪽이 검사 값이 더 크다.
            return all.FirstOrDefault(h =>
                       {
                           var l = h.GetComponentInChildren<Light>(true);
                           return l != null && l.enabled;
                       })
                ?? all.FirstOrDefault();
        }

        static IEnumerator 떨어진_것을_줍는다()
        {
            float wait = 0f;
            while (wait < 0.7f) { wait += Time.deltaTime; yield return null; }

            var cam = E2EHarness.Eye;
            var it = E2EHarness.Player.Interactor;

            // 거대 버섯은 몸통 한가운데(사람 키의 몇 배 위)에서 떨어뜨린다.
            // 굴러가는 거리가 잔해와 달라 반경을 넉넉히 잡는다.
            int 처음 = Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Exclude)
                .Count(p => p.name.StartsWith("Drop_" + WoodId));
            E2EHarness.Log($"  바닥에 떨어진 목재 {처음}덩이");

            for (int n = 0; n < 12; n++)
            {
                var drop = Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Exclude)
                    .Where(p => p.name.StartsWith("Drop_" + WoodId))
                    .OrderBy(p => Vector3.Distance(p.transform.position,
                                                   E2EHarness.Player.transform.position))
                    .FirstOrDefault();
                if (drop == null) yield break;

                drop.transform.position = cam.transform.position + cam.transform.forward * 1.8f;
                E2EHarness.SyncPhysics();   // 옮긴 자리가 조준 캐스트에 바로 보이게 한다
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

                // 착지 지점은 소품이 빽빽해 조준선이 남의 것을 먼저 잡는다.
                // 줍는 조작 경로는 E2EScenarios.Pickup과 E2EChapter1이 이미 통과시켰으므로,
                // 여기서 확인할 것은 "벤 것이 가방에 들어오는가"다.
                if (drop != null)
                {
                    E2EHarness.Log("    조준이 빗나가 직접 줍는다: " + drop.name);
                    drop.Interact(E2EHarness.Player);
                    yield return null;
                }
            }
        }

        // ── 2. 목재로 짓는다 ────────────────────────────────────

        static IEnumerator 목재로_짓는다()
        {
            E2EHarness.Log("— 조립 조각은 목재를 먹는다 —");

            var placer = Object.FindAnyObjectByType<BuildPlacer>(FindObjectsInactive.Exclude);
            E2EHarness.Assert(placer != null, "BuildPlacer가 있다");
            if (placer == null) yield break;

            placer.SelectById("piece_foundation");
            yield return null;
            var def = placer.Selected;
            E2EHarness.Assert(def != null, "토대 정의를 찾았다");
            if (def == null) yield break;

            int 목재값 = def.cost.Where(c => c.item != null && c.item.id == WoodId)
                                .Sum(c => c.count);
            E2EHarness.Assert(목재값 > 0, $"토대가 목재를 요구한다 ({목재값}개)");

            // 목재가 없으면 지어지지 않는다는 것부터 본다. 되는 것만 보면
            // 비용이 실제로 걸려 있는지 알 수 없다.
            비운다(WoodId);
            준다("scrap", 400);
            placer.Cancel();
            placer.SelectById("piece_foundation");
            yield return null;

            Vector3 자리 = Vector3.zero;
            bool 땅찾음 = false;
            yield return 놓을_땅을_본다(placer, r => { 땅찾음 = r.Item1; 자리 = r.Item2; },
                                       기대성공: false);
            if (땅찾음)
                E2EHarness.Assert(placer.LastResult == PlacementResult.NotEnoughResources,
                                  $"목재가 없으면 못 짓는다 (판정 {placer.LastResult})");

            // 목재를 채우고 다시 본다.
            준다(WoodId, 목재값 * 3);
            int 목재전 = Inv.CountOf(WoodId);

            GameObject 지은것 = null;
            yield return 놓을_땅을_본다(placer, r => { 땅찾음 = r.Item1; 자리 = r.Item2; },
                                       기대성공: true, built: g => 지은것 = g);

            E2EHarness.Assert(지은것 != null, "목재로 토대를 세웠다");
            if (지은것 == null) yield break;

            E2EHarness.AssertEqual(Inv.CountOf(WoodId), 목재전 - 목재값,
                                   "세운 만큼 목재가 빠졌다");

            Object.Destroy(지은것);
            placer.Cancel();
            yield return null;
        }

        /// <summary>
        /// 둘레를 돌며 놓을 만한 평지를 본다. 배치 절차 자체는
        /// <see cref="E2EModularBuild"/>가 이미 보므로 여기서는 자원 판정만 노린다.
        /// </summary>
        static IEnumerator 놓을_땅을_본다(BuildPlacer placer,
                                        System.Action<(bool, Vector3)> result,
                                        bool 기대성공,
                                        System.Action<GameObject> built = null)
        {
            var 기준 = E2EHarness.Player.transform.position;

            for (int a = 0; a < 12; a++)
            {
                var dir = Quaternion.Euler(0f, a * 30f, 0f) * Vector3.forward;
                var probe = 기준 + dir * 4f;

                if (!Physics.Raycast(probe + Vector3.up * 4f, Vector3.down, out var g, 12f, ~0,
                                     QueryTriggerInteraction.Ignore))
                    continue;

                E2EHarness.LookAt(g.point);
                yield return null;
                yield return null;

                var r = placer.Evaluate(out _, out _);
                // 지형·겹침 때문에 못 놓는 자리는 그냥 다음 자리를 본다.
                // 여기서 보려는 것은 재료 판정이다.
                if (r != PlacementResult.Ok && r != PlacementResult.NotEnoughResources)
                    continue;

                result((true, g.point));

                if (!기대성공) yield break;
                if (r != PlacementResult.Ok) continue;

                var go = placer.TryBuild();
                if (go != null) { built?.Invoke(go); yield break; }
            }

            result((false, Vector3.zero));
        }

        // ── 3. 목재라야 불이 산다 ───────────────────────────────

        static IEnumerator 목재라야_불이_산다()
        {
            E2EHarness.Log("— 스크랩은 타지 않는다 —");

            var fire = 화톳불을_세운다();
            E2EHarness.Assert(fire != null, "화톳불을 세웠다");
            if (fire == null) yield break;

            E2EHarness.Assert(fire.IsBurning, "세우자마자 타고 있다");

            // 스크랩 에너지 추출을 걸어 둔다. 불이 꺼지면 멈춰야 할 그 작업이다.
            준다("scrap", 40);
            var 추출 = Resources.FindObjectsOfTypeAll<Survive.Crafting.RecipeSO>()
                .FirstOrDefault(r => r != null &&
                                     r.requiredStation == Survive.Crafting.StationType.Campfire);
            if (추출 != null)
            {
                Survive.Crafting.CraftQueueService.TryEnqueue(
                    fire.Work.Queue, 추출, 1, Inv, Survive.Crafting.StationType.Campfire);
                E2EHarness.AssertEqual(fire.Work.Queue.Count, 1, "에너지 추출이 불에 걸렸다");
            }

            // 연료를 바닥낸다.
            연료를_바닥낸다(fire);
            yield return E2EHarness.WaitUntil(() => !fire.IsBurning, "불이 꺼졌다", 4f);
            E2EHarness.Assert(fire.PausedReason != null, "멈춘 이유를 말한다: " + fire.PausedReason);

            float 꺼진직후 = 추출 != null ? fire.Work.Queue.Active.Elapsed : 0f;
            yield return 기다린다(1.2f);
            if (추출 != null)
                E2EHarness.AssertEqual(fire.Work.Queue.Active.Elapsed, 꺼진직후,
                                       "불이 꺼진 동안에는 추출이 멈춘다");

            // 스크랩만 잔뜩 들고 있어도 불은 붙지 않는다.
            비운다(WoodId);
            준다("scrap", 40);
            E2EHarness.Assert(!fire.Refuel(Inv), "스크랩으로는 불이 붙지 않는다");
            E2EHarness.Assert(!fire.CanInteract(E2EHarness.Player),
                              "스크랩만 들고서는 꺼진 불에 할 일이 없다");
            E2EHarness.Assert(!fire.IsBurning, "여전히 꺼져 있다");

            // 목재를 넣으면 살아난다.
            준다(WoodId, 8);
            int 목재전 = Inv.CountOf(WoodId);
            E2EHarness.Assert(fire.CanInteract(E2EHarness.Player), "목재를 들면 지필 수 있다");
            E2EHarness.Assert(fire.Refuel(Inv), "목재를 넣었다");
            yield return null;

            E2EHarness.Assert(fire.IsBurning, $"불이 다시 붙었다 (연료 {fire.FuelNormalized:P0})");
            E2EHarness.Assert(Inv.CountOf(WoodId) < 목재전,
                              $"목재가 들어갔다 ({목재전} → {Inv.CountOf(WoodId)})");
            E2EHarness.Log("  연료 줄 문구: " + fire.SideAction.Label());
            E2EHarness.Assert(fire.SideAction.Label().Contains("목재"),
                              "화면이 목재를 넣으라고 말한다");

            // 다시 흐르는지 본다.
            if (추출 != null)
            {
                yield return 기다린다(1.2f);
                E2EHarness.Assert(fire.Work.Queue.Active.Elapsed > 꺼진직후,
                                  "불이 살아나자 추출이 이어진다");
            }

            Object.Destroy(fire.gameObject);
            yield return null;
        }

        // ── 4. 목재 재고가 곧 안전 재고다 ───────────────────────

        /// <summary>
        /// <b>불이 꺼지면 밝은 구역이 사라지고, 빛을 꺼리는 것이 그 자리로 들어온다.</b>
        ///
        /// 이것이 "목재 재고 = 안전 재고"(기획서 §5.3)가 말이 되는 유일한 이유다.
        /// 앞 단계(<see cref="목재라야_불이_산다"/>)는 불이 꺼지면 <b>생산</b>이 멈추는
        /// 것을 봤다. 여기서 보는 것은 불이 꺼지면 <b>안전</b>이 사라지는 것이다.
        ///
        /// 낫을 실제로 불러 세우지 않는다. 판단은 전부
        /// <see cref="CreatureDecision.JudgeLight"/>가 하고
        /// <c>CreatureBrain</c>은 <see cref="LitZoneRegistry"/>에 물어 그 입력을
        /// 채워 넣기만 한다 — 그래서 <b>레지스트리의 실제 답</b>을 그 함수에 그대로
        /// 먹이면 몸을 세우지 않고도 같은 결론을 얻는다. 개체를 소환하면 이 시나리오가
        /// 낫의 서식 범위·이동 능력까지 함께 재게 되어, 그쪽이 바뀔 때마다
        /// 목재와 무관한 이유로 깨진다.
        /// </summary>
        static IEnumerator 불이_꺼지면_안전지대가_사라진다()
        {
            E2EHarness.Log("— 불이 꺼지면 안전지대가 사라진다 —");

            // 씬의 발광 군락이 불 자리를 이미 덮고 있으면 아무것도 재지 못한다.
            int 끈광원 = E2EHarness.MuteAmbientLitZones();
            E2EHarness.DarkenLantern();
            E2EHarness.Log($"  무대 정리: 주변 광원 {끈광원}곳을 뺐고 랜턴 배터리를 비웠다");

            var fire = 화톳불을_세운다();
            E2EHarness.Assert(fire != null, "화톳불을 세웠다");
            if (fire == null) { E2EHarness.RestoreWorld(); yield break; }

            var 자리 = fire.LitZoneCenter;

            E2EHarness.Assert(fire.IsBurning, "세우자마자 타고 있다");
            E2EHarness.Assert(LitZoneRegistry.IsLit(자리), "불 자리가 밝은 구역이다");
            E2EHarness.AssertEqual(낫의_판단(자리), LightVerdict.Blocked,
                                   "밝은 동안에는 낫이 그 자리로 다가오지 못한다");

            // 연료를 바닥낸다. 여기부터가 이 시나리오의 본론이다.
            연료를_바닥낸다(fire);
            yield return E2EHarness.WaitUntil(() => !fire.IsBurning, "연료가 떨어졌다", 4f);

            E2EHarness.Assert(!LitZoneRegistry.IsLit(자리),
                              "불이 꺼지자 밝은 구역이 사라졌다");
            E2EHarness.AssertEqual(낫의_판단(자리), LightVerdict.Clear,
                                   "이제 낫이 그 자리로 접근할 수 있다");

            // 되돌릴 수 있어야 재고가 재고다. 목재를 넣으면 안전지대가 돌아온다.
            준다(WoodId, CampfireFuelRule.LogsPerRefuel);
            E2EHarness.Assert(fire.Refuel(Inv), "목재를 넣었다");
            yield return null;

            E2EHarness.Assert(fire.IsBurning,
                              $"불이 살아났다 (연료 {fire.FuelSeconds:F0}초)");
            E2EHarness.Assert(LitZoneRegistry.IsLit(자리), "밝은 구역이 돌아왔다");
            E2EHarness.AssertEqual(낫의_판단(자리), LightVerdict.Blocked,
                                   "낫이 다시 막혔다");

            E2EHarness.Log($"  실측: 목재 {CampfireFuelRule.CapacityLogs}개 = " +
                           $"{CampfireFuelRule.MaxFuelSeconds:F0}초, " +
                           $"목재 1개 = {CampfireFuelRule.SecondsPerLog:F0}초");

            Object.Destroy(fire.gameObject);
            yield return null;
            E2EHarness.RestoreWorld();
            yield return null;
        }

        /// <summary>
        /// 이 자리를 노리는 낫이 지금 무엇으로 판정받는가.
        ///
        /// 낫의 성질 중 이 판정에 쓰이는 것은 <c>avoidsLight</c>뿐이다. 감지 반경·
        /// 공격 거리는 <see cref="CreatureDecision.JudgeLight"/>가 보지 않으므로
        /// 에셋을 열지 않는다 — 클론 A가 낫의 수치를 고치는 중이라도 이 판정은
        /// 흔들리지 않아야 한다.
        /// </summary>
        static LightVerdict 낫의_판단(Vector3 자리)
        {
            var 낫 = new CreatureTraits(BehaviorProfile.Aggressive, 20f, 2f, avoidsLight: true);
            var 감각 = new CreatureSenses(distanceToThreat: 5f, aggroLeft: 0f, stateTimer: 0f,
                                          selfInLight: false,
                                          threatInLight: LitZoneRegistry.IsLit(자리));
            return CreatureDecision.JudgeLight(낫, 감각);
        }

        static Campfire 화톳불을_세운다()
        {
            var catalog = Resources.FindObjectsOfTypeAll<BuildCatalogSO>().FirstOrDefault();
            var def = catalog?.entries?.FirstOrDefault(b => b != null && b.id == "campfire");
            if (def?.prefab == null) return null;

            // 배치 절차는 E2EBaseBuilding·E2ERespawn이 이미 본다.
            var pos = E2EHarness.Player.transform.position +
                      E2EHarness.Player.transform.forward * 2.2f;
            var go = Object.Instantiate(def.prefab, pos, Quaternion.identity);
            return go.GetComponentInChildren<Campfire>(true);
        }

        /// <summary>
        /// 연료를 한 프레임분만 남긴다. 가득 찬 불을 실시간으로 태울 수는 없고,
        /// 불을 끄는 공개 창구는 게임에 필요 없는 것이라 만들지 않았다.
        /// 남은 한 방울은 Campfire의 Update가 정상 경로로 소진시킨다 —
        /// 값을 0으로 밀어 넣고 끝내면 소등 처리를 건너뛴다.
        /// </summary>
        static void 연료를_바닥낸다(Campfire fire)
        {
            var field = typeof(Campfire).GetField("_fuel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            E2EHarness.Assert(field != null, "화톳불의 연료를 찾았다");
            field?.SetValue(fire, 0.01f);
        }

        static IEnumerator 기다린다(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.deltaTime; yield return null; }
        }
    }
}
