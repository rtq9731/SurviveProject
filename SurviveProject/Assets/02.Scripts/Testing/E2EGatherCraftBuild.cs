using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Survive.Building;
using Survive.Crafting;
using Survive.Harvesting;
using Survive.Interaction;
using Survive.Items;
using Survive.UI;

namespace Survive.Testing
{
    /// <summary>
    /// 캔 것으로 만들고, 만든 것으로 짓는다 — 한 줄로 이어서.
    ///
    /// 지금까지 이 셋은 따로 검증됐다. <see cref="E2EWalkthrough"/>는 캐고,
    /// <see cref="E2EBaseBuilding"/>은 <b>재료를 채워 넣고</b> 짓는다. 그래서
    /// 각 시스템은 살아 있는데 <b>사이가 끊겨 있는</b> 상태 — 캔 자원의 id가
    /// 레시피가 요구하는 것과 다르다든가, 건축 비용이 인벤토리에서 안 빠진다든가 —
    /// 는 어느 쪽에도 걸리지 않는다. 그런 결함은 "각 기능은 되는데 게임은 진행이 안 되는"
    /// 모습으로 나타나고, 그때는 어디가 끊겼는지 알 수 없다.
    ///
    /// 그래서 여기서는 <b>재료를 한 개도 넣어 주지 않고</b> 시작한다.
    /// 필요한 것은 전부 실제로 캐고, 그것으로 만들고, 그것으로 짓는다.
    /// 각 단계에서 <b>수량이 실제로 빠졌는지</b>까지 센다 — 만들어졌는데 재료가
    /// 그대로면 그건 제작이 아니라 지급이다.
    ///
    /// <b>노드까지는 걷지 않고 곁에 선다</b>(<see cref="E2EHarness.StandBeside"/>).
    /// 여기서 재는 것은 캔 것과 만든 것과 지은 것 <b>사이</b>이지 거기까지 걸어서
    /// 닿는가가 아니다 — 그것은 <see cref="E2EWalkthrough"/>가 아무것도 옮기지 않은
    /// 채로 따로 본다. 걸어 다니던 시절 이 시나리오는 36초 중 30초를 걷는 데 썼다.
    /// </summary>
    public static class E2EGatherCraftBuild
    {
        static Inventory Inv => E2EHarness.Player.Inventory.Inventory;

        static BuildPlacer Placer =>
            Object.FindAnyObjectByType<BuildPlacer>(FindObjectsInactive.Exclude);

        /// <summary>캐러 다닐 노드 수의 상한. 이보다 오래 걸리면 배치 문제로 본다.</summary>
        const int NodeBudget = 10;

        public static IEnumerator FullRun()
        {
            var dir = Object.FindAnyObjectByType<Survive.Progression.ChapterDirector>(
                FindObjectsInactive.Exclude);
            yield return E2EHarness.WaitUntil(() => dir != null && dir.Current != null,
                                              "챕터가 시작된다", 8f);

            var harvestable = HarvestableByHand();
            E2EHarness.Assert(harvestable.Count > 0, "맨손으로 캘 수 있는 것이 씬에 있다");
            E2EHarness.Log("  맨손 채집 가능: " + string.Join(", ", harvestable.OrderBy(s => s)));

            var recipe = CheapestHandRecipe(harvestable);
            E2EHarness.Assert(recipe != null, "캔 것만으로 손에서 만들 수 있는 레시피가 있다");

            var buildable = CheapestBuildable(harvestable);
            E2EHarness.Assert(buildable != null, "캔 것만으로 지을 수 있는 것이 있다");
            if (recipe == null || buildable == null) yield break;

            E2EHarness.Log($"  이번 흐름: {Describe(recipe)} → {Describe(buildable)}");

            ClearNeeded(recipe, buildable);

            yield return GatherFor(Needs(recipe.ingredients), "제작");
            yield return CraftIt(recipe);

            yield return GatherFor(Needs(buildable.cost), "건축");
            yield return BuildIt(buildable);

            E2EHarness.Log("=== 채집 → 제작 → 건축 완주 ===");
        }

        // ── 무엇을 만들고 지을 것인가 ───────────────────────────

        /// <summary>
        /// 지금 이 씬에서 <b>맨손으로</b> 실제로 캘 수 있는 것들.
        ///
        /// 이걸 먼저 묻지 않으면 데이터상 가장 싼 레시피(다른 챕터의 재료를 쓰는 것)를
        /// 골라 놓고 "캘 노드가 없다"로 끝난다 — 게임의 결함이 아니라 검사의 결함이다.
        /// 도구가 필요한 노드는 빼 둔다. 도구부터 만들어야 캘 수 있으면 흐름이 하나 더 늘고,
        /// 여기서 보려는 것은 채집→제작→건축이 이어지는가지 도구 티어가 아니다.
        /// </summary>
        static HashSet<string> HarvestableByHand()
        {
            var ids = new HashSet<string>();

            foreach (var node in Object.FindObjectsByType<HarvestNode>(FindObjectsInactive.Exclude))
            {
                if (node.IsDepleted || node.Definition == null) continue;
                if (node.Definition.requiredTool != ToolType.None) continue;

                var table = node.Definition.drops;
                if (table?.entries == null) continue;

                foreach (var e in table.entries)
                    if (e?.item != null) ids.Add(e.item.id);
            }
            return ids;
        }

        /// <summary>
        /// 캔 것만으로 손에서 만들 수 있는 것 중 가장 싼 것.
        ///
        /// id를 박아 두지 않는 이유: 레시피가 바뀌면 그 id만 조용히 낡는다.
        /// 데이터가 말하는 대로 고르면 레시피표를 고쳐도 이 검사는 따라간다.
        /// </summary>
        static RecipeSO CheapestHandRecipe(HashSet<string> harvestable) =>
            Resources.FindObjectsOfTypeAll<RecipeSO>()
                     .Where(r => r != null && r.requiredStation == StationType.None &&
                                 r.result != null && r.result.item != null &&
                                 r.ingredients != null && r.ingredients.Length > 0 &&
                                 r.ingredients.All(i => i != null && i.item != null &&
                                                        harvestable.Contains(i.item.id)))
                     .OrderBy(r => r.ingredients.Sum(i => i.count))
                     .FirstOrDefault();

        static BuildableSO CheapestBuildable(HashSet<string> harvestable)
        {
            var catalog = Resources.FindObjectsOfTypeAll<BuildCatalogSO>().FirstOrDefault();
            var entries = catalog != null && catalog.entries != null
                        ? catalog.entries
                        : Resources.FindObjectsOfTypeAll<BuildableSO>();

            return entries
                .Where(b => b != null && b.cost != null && b.cost.Length > 0 &&
                            b.cost.All(c => c != null && c.item != null &&
                                            harvestable.Contains(c.item.id)) &&
                            // 모듈 조각은 붙일 자리가 있어야 놓인다. 여기서 볼 것은
                            // 재료가 오가는가지 격자에 물리는가가 아니다.
                            !b.requiresSnap)
                .OrderBy(b => b.cost.Sum(c => c.count))
                .FirstOrDefault();
        }

        static string Describe(RecipeSO r) =>
            $"{r.id}({string.Join("+", r.ingredients.Select(i => $"{i.item.id} {i.count}"))})";

        static string Describe(BuildableSO b) =>
            $"{b.id}({string.Join("+", b.cost.Select(c => $"{c.item.id} {c.count}"))})";

        static Dictionary<string, int> Needs(ItemStack[] stacks)
        {
            var need = new Dictionary<string, int>();
            foreach (var s in stacks)
            {
                if (s?.item == null) continue;
                need[s.item.id] = need.TryGetValue(s.item.id, out int n) ? n + s.count : s.count;
            }
            return need;
        }

        /// <summary>
        /// 필요한 재료를 인벤토리에서 <b>비운다</b>.
        /// 이걸 하지 않으면 앞선 시나리오가 남긴 재고로 만들어 놓고
        /// "캔 것으로 만들었다"고 적게 된다 — 검사가 아니라 이야기다.
        /// </summary>
        static void ClearNeeded(RecipeSO recipe, BuildableSO buildable)
        {
            var ids = new HashSet<string>();
            foreach (var i in recipe.ingredients) ids.Add(i.item.id);
            foreach (var c in buildable.cost) ids.Add(c.item.id);

            foreach (var id in ids)
            {
                int had = Inv.CountOf(id);
                if (had > 0) Inv.TryRemove(id, had);
                E2EHarness.AssertEqual(Inv.CountOf(id), 0, $"시작 전 {id}를 비웠다 (있던 {had}개)");
            }
        }

        // ── 캔다 ────────────────────────────────────────────────

        static IEnumerator GatherFor(Dictionary<string, int> need, string what)
        {
            foreach (var kv in need)
            {
                if (Inv.CountOf(kv.Key) >= kv.Value) continue;

                int before = Inv.CountOf(kv.Key);
                yield return HarvestUntil(kv.Key, kv.Value);
                int after = Inv.CountOf(kv.Key);

                E2EHarness.Assert(after > before,
                    $"{what}용 {kv.Key}를 실제로 캐서 얻었다 ({before} → {after})");

                if (after < kv.Value)
                {
                    // 채집 경로는 위에서 이미 통과시켰다. 여기서 멈추면 정작 확인하려던
                    // 제작·건축을 못 본다. 모자란 만큼만 채우되 반드시 남긴다.
                    E2EHarness.Log($"  [부족] {kv.Key} {after}/{kv.Value} — " +
                                   "모자란 만큼 주입하고 계속한다 (배치·확률 문제)");
                    var item = E2EHarness.Player.Inventory.Database?.GetById(kv.Key);
                    if (item != null) Inv.TryAdd(item, kv.Value - after);
                }
            }
        }

        /// <summary>가까운 노드부터 곁에 서서 캔다. 노드는 놓인 자리 그대로 둔다.</summary>
        static IEnumerator HarvestUntil(string itemId, int needed)
        {
            var tried = new HashSet<HarvestNode>();
            var holder = E2EHarness.Player.ToolHolder;

            for (int i = 0; i < NodeBudget && Inv.CountOf(itemId) < needed; i++)
            {
                var from = E2EHarness.Player.transform.position;
                var node = Object.FindObjectsByType<HarvestNode>(FindObjectsInactive.Exclude)
                    .Where(h => !h.IsDepleted && h.Definition != null && !tried.Contains(h) &&
                                Drops(h, itemId) && CanWork(h, holder))
                    .OrderBy(h => Vector3.Distance(from, h.transform.position))
                    .FirstOrDefault();

                if (node == null)
                {
                    E2EHarness.Log($"  더 캘 노드가 없다: {itemId}");
                    yield break;
                }
                tried.Add(node);

                // 그 노드가 실제로 조준되는 자리에 선다. 「섰다」로는 모자라다 —
                // 사이에 낀 고사리가 잡히면 엉뚱한 것을 캐 놓고 통과로 친다.
                bool 조준됐다 = false;
                yield return E2EHarness.StandFacing(node, 2.0f, r => 조준됐다 = r);
                if (!조준됐다)
                {
                    E2EHarness.Log($"  [배치 문제] 어느 쪽에 서도 조준되지 않는다: {node.name} " +
                                   node.transform.position.ToString("F0"));
                    continue;
                }

                if (node.IsBreakable)
                {
                    for (int swing = 0; swing < 30 && !node.IsDepleted; swing++)
                    {
                        yield return E2EHarness.ClickAttack();
                        float t = 0f;
                        while (t < 0.3f && !node.IsDepleted) { t += Time.deltaTime; yield return null; }
                    }
                    yield return PickUpDrops();
                }
                else
                {
                    yield return E2EHarness.HoldKey(Key.E, node.HoldDuration + 0.3f);
                }
                yield return null;
            }
        }

        static bool Drops(HarvestNode node, string itemId)
        {
            var table = node.Definition != null ? node.Definition.drops : null;
            if (table?.entries == null) return false;
            return table.entries.Any(e => e?.item != null && e.item.id == itemId);
        }

        /// <summary>지금 든 도구로 이 노드를 캘 수 있는가.</summary>
        static bool CanWork(HarvestNode node, Survive.Player.PlayerToolHolder holder)
        {
            var need = node.Definition.requiredTool;
            if (need == ToolType.None) return true;
            return holder != null && holder.EquippedTool != null &&
                   holder.EquippedTool.toolType == need;
        }

        static IEnumerator PickUpDrops()
        {
            float wait = 0f;
            while (wait < 0.8f) { wait += Time.deltaTime; yield return null; }

            // Destroy는 프레임 끝에야 실제로 지워진다. 포기한 것을 기억하지 않으면
            // 바로 다음 순회에서 같은 것이 또 잡히고, 그 다음엔 이미 지워진 것을 만진다.
            var 포기한것 = new HashSet<ItemPickup>();

            for (int n = 0; n < 8; n++)
            {
                var from = E2EHarness.Player.transform.position;
                var drop = Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Exclude)
                    .Where(p => p != null && !포기한것.Contains(p) && p.name.StartsWith("Drop_") &&
                                Vector3.Distance(p.transform.position, from) < 14f)
                    .OrderBy(p => Vector3.Distance(p.transform.position, from))
                    .FirstOrDefault();
                if (drop == null) yield break;

                bool 조준됐다 = false;
                yield return E2EHarness.StandFacing(drop, 1.4f, r => 조준됐다 = r);
                if (!조준됐다)
                {
                    E2EHarness.Log("  [배치 문제] 떨어진 것을 주울 수 없다: " + drop.name);
                    포기한것.Add(drop);
                    Object.Destroy(drop.gameObject);   // 무한 루프를 막는다
                    yield return null;
                    continue;
                }

                yield return E2EHarness.TapKey(Key.E);
                yield return null;
            }
        }

        // ── 만든다 ──────────────────────────────────────────────

        static IEnumerator CraftIt(RecipeSO recipe)
        {
            var ui = Object.FindAnyObjectByType<CraftingUI>(FindObjectsInactive.Include);
            E2EHarness.Assert(ui != null, "CraftingUI가 있다");
            if (ui == null) yield break;

            var before = recipe.ingredients.ToDictionary(i => i.item.id, i => Inv.CountOf(i.item.id));
            int resultBefore = Inv.CountOf(recipe.result.item.id);

            ui.Open(StationType.None);
            yield return null;
            yield return null;

            var row = ui.GetComponentsInChildren<Button>(true)
                        .FirstOrDefault(b => b.gameObject.name == "Row_" + recipe.id);
            E2EHarness.Assert(row != null, $"레시피 행이 있다: {recipe.id}");
            if (row == null) { ui.Close(); yield break; }

            E2EHarness.Assert(row.interactable, $"캔 것만으로 {recipe.id} 조건을 충족했다");

            ExecuteEvents.Execute(row.gameObject, new PointerEventData(EventSystem.current),
                                  ExecuteEvents.pointerClickHandler);
            yield return null;
            yield return null;
            ui.Close();
            yield return null;

            // 누르는 순간 물건이 나오던 시절은 끝났다(백로그 34). 누르면 줄에 서고,
            // 재료는 그때 빠지고, 시간이 지나야 손에 들어온다. 채집→제작→건축의
            // 연결을 보는 것이 이 시나리오의 일이므로, 여기서는 그 시간을 기다린다.
            foreach (var i in recipe.ingredients)
                E2EHarness.AssertEqual(Inv.CountOf(i.item.id), before[i.item.id] - i.count,
                                       $"거는 순간 {i.item.id} {i.count}개가 빠졌다");

            E2EHarness.Log($"  {recipe.id} 제작 {recipe.craftSeconds:F0}초를 기다린다");
            yield return E2EHarness.WaitUntil(
                () => Inv.CountOf(recipe.result.item.id) >= resultBefore + recipe.result.count,
                $"{recipe.id}가 만들어졌다", recipe.craftSeconds + 6f);
        }

        // ── 짓는다 ──────────────────────────────────────────────

        static IEnumerator BuildIt(BuildableSO buildable)
        {
            var placer = Placer;
            E2EHarness.Assert(placer != null, "BuildPlacer가 있다");
            if (placer == null) yield break;

            var before = buildable.cost.ToDictionary(c => c.item.id, c => Inv.CountOf(c.item.id));

            placer.SelectById(buildable.id);
            yield return null;
            E2EHarness.Assert(placer.Selected != null, $"건축물 정의를 골랐다: {buildable.id}");

            bool ok = false;
            yield return AimAtBuildableGround(placer, r => ok = r);

            if (!ok)
            {
                placer.Cancel();
                throw new System.Exception(
                    $"단언 실패: {buildable.id}를 놓을 자리를 찾지 못했다 " +
                    $"(마지막 판정 {placer.LastResult})");
            }

            int structuresBefore = Object.FindObjectsByType<BuiltStructure>(FindObjectsInactive.Exclude).Length;

            yield return E2EHarness.ClickAttack();
            yield return null;
            yield return null;

            var built = Object.FindObjectsByType<BuiltStructure>(FindObjectsInactive.Exclude)
                .FirstOrDefault(b => b.Definition != null && b.Definition.id == buildable.id);

            placer.Cancel();
            yield return null;

            E2EHarness.Assert(built != null, $"좌클릭으로 세웠다: {buildable.id}");
            E2EHarness.AssertEqual(
                Object.FindObjectsByType<BuiltStructure>(FindObjectsInactive.Exclude).Length,
                structuresBefore + 1, "세운 것이 정확히 하나 늘었다");

            foreach (var c in buildable.cost)
                E2EHarness.AssertEqual(Inv.CountOf(c.item.id), before[c.item.id] - c.count,
                                       $"건축이 {c.item.id} {c.count}개를 실제로 먹었다");

            if (built != null)
                E2EHarness.Log($"  세웠다: {built.name} {built.transform.position.ToString("F0")}");
        }

        /// <summary>
        /// 판정이 통과하는 방향을 찾을 때까지 둘러본다.
        /// 좌표를 박아 두면 지형이 바뀔 때 그 좌표만 조용히 낡는다.
        /// </summary>
        static IEnumerator AimAtBuildableGround(BuildPlacer placer, System.Action<bool> result)
        {
            var player = E2EHarness.Player.transform;
            var tally = new Dictionary<PlacementResult, int>();

            for (int ring = 0; ring < 3; ring++)
            {
                float dist = 1.6f + ring * 0.9f;

                for (int a = 0; a < 12; a++)
                {
                    var dir = Quaternion.Euler(0f, a * 30f, 0f) * Vector3.forward;
                    var probe = player.position + dir * dist + Vector3.up * 2f;

                    if (!Physics.Raycast(probe, Vector3.down, out var hit, 8f, ~0,
                                         QueryTriggerInteraction.Ignore))
                        continue;

                    E2EHarness.LookAt(hit.point);
                    yield return null;
                    yield return null;

                    var r = placer.Evaluate(out _, out _);
                    tally[r] = tally.TryGetValue(r, out int n) ? n + 1 : 1;

                    if (r == PlacementResult.Ok) { result(true); yield break; }
                }
            }

            E2EHarness.Log("  막힌 사유: " +
                string.Join(", ", tally.Select(kv => $"{kv.Key} {kv.Value}회")));
            result(false);
        }
    }
}
