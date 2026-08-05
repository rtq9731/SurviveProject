using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Survive.Crafting;
using Survive.Harvesting;
using Survive.Interaction;
using Survive.Items;
using Survive.Progression;
using Survive.UI;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// 놓여 있는 그대로 걸어가서 만질 수 있는지 본다.
    ///
    /// A2는 대상을 카메라 앞으로 옮겨서 확인한다. 상호작용 경로를 보기에는
    /// 그걸로 충분하지만, <b>배치가 실제로 플레이 가능한지</b>는 전혀 검증하지 않는다.
    /// 배치를 통째로 바꾼 뒤(B1) 노드가 바위에 박혀 있어도, 강을 건널 수 없어도,
    /// 제작대에 닿지 못해도 A2는 그대로 통과한다.
    ///
    /// 그래서 여기서는 아무것도 옮기지 않는다. 걸어가서, 조준하고, 캔다.
    /// 사람이 직접 플레이하는 게이트(D2)를 대신할 수는 없지만,
    /// "길이 막혀 있어서 못 깬다"는 종류의 문제는 여기서 걸러진다.
    ///
    /// 덤으로 목표별 소요 시간을 잰다. 밸런스 판단의 근거가 된다.
    /// </summary>
    public static class E2EWalkthrough
    {
        static readonly List<string> _timings = new List<string>();
        static float _mark;

        static void Mark() => _mark = Time.time;

        static void Lap(string what)
        {
            float sec = Time.time - _mark;
            _timings.Add($"{what,-24} {sec,6:F1}초");
            E2EHarness.Log($"  [{sec:F1}초] {what}");
            _mark = Time.time;
        }

        static Inventory Inv => E2EHarness.Player.Inventory.Inventory;

        public static IEnumerator FullRun()
        {
            _timings.Clear();

            var dir = Object.FindFirstObjectByType<ChapterDirector>(FindObjectsInactive.Exclude);
            E2EHarness.Assert(dir != null, "ChapterDirector가 있다");
            yield return E2EHarness.WaitUntil(() => dir.Current != null, "챕터가 시작된다", 5f);

            Mark();

            // ── 목표 1·2: 걸어서 지대에 들어간다 ────────────────
            // 트리거는 44m 폭의 판이다. 중심에 설 이유는 없고 — 그 자리는 포탈 장치
            // 너머 바위 위다 — 게임이 요구하는 것은 판을 밟는 것뿐이다.
            var trigger = GameObject.Find("Trigger_Surveyed");
            E2EHarness.Assert(trigger != null, "탐색 트리거가 있다");
            yield return E2EHarness.WalkInto(trigger, 60f);
            yield return E2EHarness.WaitUntil(() => dir.CurrentIndex >= 1, "목표1 완료", 6f);
            Lap("목표1 착지 이탈");

            var grove = GameObject.Find("LightZone_1");
            E2EHarness.Assert(grove != null, "버섯 군락 지대가 있다");
            yield return E2EHarness.WalkInto(grove, 60f);
            yield return E2EHarness.WaitUntil(() => dir.CurrentIndex >= 2, "목표2 완료", 6f);
            Lap("목표2 군락 발견");

            // ── 목표 3: 놓인 자리에서 맨손 채집 ─────────────────
            yield return HarvestInPlace(ToolType.None, () => Inv.CountOf("scrap") >= 10, 14);
            yield return E2EHarness.WaitUntil(() => dir.CurrentIndex >= 3, "목표3 완료", 6f);
            Lap("목표3 스크랩 10개");

            // ── 목표 4: 걸어가서 제작 ───────────────────────────
            yield return GatherInPlace("machine_part", 2, ToolType.None, 10);
            yield return GatherInPlace("scrap", 5, ToolType.None, 8);
            yield return CraftAtBench("pickaxe");
            yield return E2EHarness.WaitUntil(() => dir.CurrentIndex >= 4, "목표4 완료", 6f);
            Lap("목표4 곡괭이 제작");

            var user = E2EHarness.Player.GetComponent<Survive.Player.PlayerToolUser>();
            E2EHarness.Assert(user != null && user.EquipFirst("pickaxe"), "곡괭이를 장착했다");

            // ── 목표 5 ──────────────────────────────────────────
            yield return GatherInPlace("machine_part", 1, ToolType.None, 8);
            yield return GatherInPlace("scrap", 6, ToolType.None, 8);
            yield return CraftAtBench("lantern");
            yield return E2EHarness.WaitUntil(() => dir.CurrentIndex >= 5, "목표5 완료", 6f);
            Lap("목표5 랜턴 제작");

            // ── 목표 6: 강 건너 광맥, 그리고 잠항구를 만들어 내려간다 ──
            //
            // 예전에는 포탈까지 걸어가 기동했다(백로그 36 이전). 지금 종막은 하강이고,
            // 그 무대인 짙은 액면층과 4번 섬의 매크로늄 광맥은 아직 씬에 없다 —
            // 배치는 §8-4의 일이라 이번 작업에서 씬을 건드리지 않았다.
            //
            // 그래서 이 구간만은 "놓인 그대로 걸어간다"는 이 시나리오의 원칙에서 벗어난다.
            // 다른 재료의 채집과 제작대까지의 보행은 그대로 걸어서 하고,
            // 매크로늄과 층만 주입·스폰한다. §8-4에서 배치가 끝나면 되돌린다.
            user?.EquipFirst("pickaxe");
            yield return null;

            var 잠항구레시피 = Resources.FindObjectsOfTypeAll<RecipeSO>()
                                       .FirstOrDefault(r => r != null && r.id == "submersible");
            E2EHarness.Assert(잠항구레시피 != null, "잠항구 레시피가 있다");
            if (잠항구레시피 == null) yield break;

            // 잠항 설계는 이제 연구대의 산출물이다(백로그 38). 소재인 유물은 낫이
            // 떨구는데 그 배선은 작업 39라 지금 세계에는 유물이 없다 — 매크로늄과
            // 같은 사정이므로 같은 방식으로 넘어간다. 연구 절차 자체는
            // E2EResearchStation이 실제 조작으로 본다.
            E2EHarness.Log("  [미배치] 잠항·보행 설계를 원장에 적는다 — 유물 드롭은 작업 39");
            E2EResearchStation.원장에_적는다("bp_submersible", "bp_surface_walker");

            foreach (var need in 잠항구레시피.ingredients)
            {
                if (need?.item == null) continue;

                if (need.item.id == "macronium")
                {
                    E2EHarness.Log($"  [미배치] macronium {need.count}개를 주입한다 — 4번 섬 광맥은 §8-4");
                    var ore = E2EHarness.Player.Inventory.Database?.GetById("macronium");
                    if (ore != null && Inv.CountOf("macronium") < need.count)
                        Inv.TryAdd(ore, need.count - Inv.CountOf("macronium"));
                    continue;
                }

                var tool = need.item.id == "alien_alloy" ? ToolType.Pickaxe : ToolType.None;
                yield return GatherInPlace(need.item.id, need.count, tool, 14);
            }
            Lap("목표6 재료 수집");

            yield return CraftAtBench("submersible");
            E2EHarness.Assert(Inv.CountOf("submersible") > 0, "잠항구를 손에 넣었다");
            Lap("목표6 잠항구 제작");

            // 4번 섬에 닿으려면 이미 있었어야 하는 물건이다(§5.4의 사슬).
            if (Inv.CountOf("surface_walker") == 0)
            {
                var walker = E2EHarness.Player.Inventory.Database?.GetById("surface_walker");
                if (walker != null) Inv.TryAdd(walker, 1);
            }

            yield return E2EDescent.여기서_내려간다();
            yield return E2EHarness.WaitUntil(() => dir.CurrentIndex >= 6, "목표6 완료", 8f);
            Lap("목표6 잠항구 하강");

            E2EHarness.Log("=== 걸어서 완주 ===");
            foreach (var t in _timings) E2EHarness.Log("  " + t);
        }

        // ── 놓인 자리에서 캔다 ──────────────────────────────────

        /// <summary>이 노드가 해당 아이템을 떨구는가.</summary>
        static bool Drops(HarvestNode node, string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return true;
            var table = node.Definition != null ? node.Definition.drops : null;
            if (table?.entries == null) return false;
            return table.entries.Any(e => e?.item != null && e.item.id == itemId);
        }

        /// <summary>
        /// 가장 가까운 노드로 걸어가 그 자리에서 캔다. 아무것도 옮기지 않는다.
        ///
        /// <paramref name="wantItem"/>을 지정하면 그것을 떨구는 노드만 고른다.
        /// 도구만 보고 고르면 곡괭이가 필요한 잔해와 광맥이 섞여, 합금을 캐러
        /// 갔다가 잔해만 부수고 돌아온다.
        /// </summary>
        static IEnumerator HarvestInPlace(ToolType tool, System.Func<bool> IsDone, int maxNodes,
                                          string wantItem = null)
        {
            var tried = new HashSet<HarvestNode>();

            for (int i = 0; i < maxNodes; i++)
            {
                if (IsDone()) yield break;

                var from = E2EHarness.Player.transform.position;
                var node = Object.FindObjectsByType<HarvestNode>(FindObjectsSortMode.None)
                    .Where(h => !h.IsDepleted && h.Definition != null &&
                                h.Definition.requiredTool == tool && !tried.Contains(h) &&
                                Drops(h, wantItem))
                    .OrderBy(h => Vector3.Distance(from, h.transform.position))
                    .FirstOrDefault();

                if (node == null)
                {
                    E2EHarness.Log($"  더 캘 노드가 없다 (요구 도구 {tool}" +
                                   (wantItem != null ? $", {wantItem} 산출" : "") + ")");
                    yield break;
                }
                tried.Add(node);

                yield return E2EHarness.TryWalkTo(node.transform.position, 2.0f, 45f);
                if (!E2EHarness.LastWalkArrived)
                {
                    // 지형에 갇혔거나 길이 없다. 배치 문제로 남기고 다음 것을 본다.
                    E2EHarness.Log($"  [배치 문제] 걸어서 닿지 못한다: {node.name} " +
                                   $"{node.transform.position.ToString("F0")}");
                    continue;
                }
                E2EHarness.LookAt(node.transform.position);
                yield return null;
                yield return null;

                var it = E2EHarness.Player.Interactor;
                for (int f = 0; f < 20 && it.Current == null; f++) yield return null;

                if (it.Current == null)
                {
                    // 걸어갔는데 조준이 안 잡힌다 = 배치가 잘못된 것이다.
                    // 지형에 박혔거나, 발판이 없어 올라설 수 없는 자리다.
                    E2EHarness.Log($"  [배치 문제] 걸어갔으나 조준되지 않는다: {node.name} " +
                                   $"{node.transform.position.ToString("F0")}");
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
                    yield return PickUpDropsInPlace();
                }
                else
                {
                    yield return E2EHarness.HoldKey(Key.E, node.HoldDuration + 0.3f);
                }
                yield return null;
            }
        }

        static IEnumerator GatherInPlace(string itemId, int needed, ToolType tool, int maxNodes)
        {
            if (Inv.CountOf(itemId) >= needed) yield break;
            yield return HarvestInPlace(tool, () => Inv.CountOf(itemId) >= needed, maxNodes, itemId);

            // 맨손 잔해가 동나면 플레이어는 곡괭이로 기계 잔해를 캔다.
            // 챕터 1의 스크랩 총량은 처음부터 그 둘을 합쳐 맞춰져 있다.
            if (tool == ToolType.None && Inv.CountOf(itemId) < needed)
            {
                var holder = E2EHarness.Player.ToolHolder;
                if (holder != null && holder.EquippedTool != null &&
                    holder.EquippedTool.toolType == ToolType.Pickaxe)
                {
                    E2EHarness.Log($"  맨손 노드가 동났다. 곡괭이로 {itemId}를 마저 캔다");
                    yield return HarvestInPlace(ToolType.Pickaxe,
                        () => Inv.CountOf(itemId) >= needed, maxNodes, itemId);
                }
            }

            int have = Inv.CountOf(itemId);
            if (have < needed)
            {
                // 걸어서 모으는 데 실패했으면 그건 배치나 확률의 문제다.
                // 여기서 멈추면 뒤를 못 보므로 채워 넣되, 반드시 남긴다.
                E2EHarness.Log($"  [부족] {itemId} {have}/{needed} — 모자란 만큼 주입하고 계속한다");
                var item = E2EHarness.Player.Inventory.Database?.GetById(itemId);
                if (item != null) Inv.TryAdd(item, needed - have);
            }
        }

        /// <summary>떨어진 것을 걸어가서 줍는다.</summary>
        static IEnumerator PickUpDropsInPlace()
        {
            float wait = 0f;
            while (wait < 0.8f) { wait += Time.deltaTime; yield return null; }

            // 포기한 것을 기억한다. Destroy는 프레임 끝에야 실제로 지워지므로
            // 바로 다음 순회에서 같은 것이 또 잡히고, 그 다음엔 이미 지워진 것을
            // 만지다 터진다 — 실제로 그렇게 시나리오가 죽었다.
            var 포기한것 = new HashSet<ItemPickup>();

            for (int n = 0; n < 8; n++)
            {
                var from = E2EHarness.Player.transform.position;
                var drop = Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None)
                    .Where(p => p != null && !포기한것.Contains(p) && p.name.StartsWith("Drop_") &&
                                Vector3.Distance(p.transform.position, from) < 14f)
                    .OrderBy(p => Vector3.Distance(p.transform.position, from))
                    .FirstOrDefault();
                if (drop == null) yield break;

                yield return E2EHarness.TryWalkTo(drop.transform.position, 1.4f, 20f);
                if (!E2EHarness.LastWalkArrived)
                {
                    E2EHarness.Log("  [배치 문제] 떨어진 것에 닿지 못한다: " + drop.name);
                    포기한것.Add(drop);
                    Object.Destroy(drop.gameObject);
                    yield return null;
                    continue;
                }
                E2EHarness.LookAt(drop.transform.position);
                yield return null;
                yield return null;

                var it = E2EHarness.Player.Interactor;
                for (int f = 0; f < 20 && it.Current == null; f++) yield return null;
                if (it.Current == null)
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

        /// <summary>제작대까지 걸어가서 연다. 스테이션 요건을 실제로 통과해야 한다.</summary>
        static IEnumerator CraftAtBench(string recipeId)
        {
            var bench = Object.FindFirstObjectByType<CraftingBench>(FindObjectsInactive.Exclude);
            E2EHarness.Assert(bench != null, "제작대가 씬에 있다");

            yield return E2EHarness.WalkTo(bench.transform.position, 2.5f, 60f);

            var ui = Object.FindFirstObjectByType<CraftingUI>(FindObjectsInactive.Include);
            E2EHarness.Assert(ui != null, "CraftingUI가 있다");

            var recipe = Resources.FindObjectsOfTypeAll<RecipeSO>()
                                  .FirstOrDefault(r => r != null && r.id == recipeId);

            // 스테이션을 요구하는 레시피는 제작대 <b>자신</b>에게 건다. 종류만 넘기면
            // 작업이 손 제작 줄로 가고, 그러면 "제작대에 걸어 두고 떠난다"(§5.4)가
            // 검증에서 통째로 빠진다.
            bool atBench = recipe != null && recipe.requiredStation != StationType.None;
            if (atBench) ui.Open(bench);
            else ui.Open(bench.StationType);
            yield return null;
            yield return null;

            var row = ui.GetComponentsInChildren<Button>(true)
                        .FirstOrDefault(b => b.gameObject.name == "Row_" + recipeId);
            E2EHarness.Assert(row != null, $"레시피 행이 있다: {recipeId}");
            E2EHarness.Assert(row.interactable, $"{recipeId} 제작 조건을 충족했다");

            int before = recipe?.result?.item != null ? Inv.CountOf(recipe.result.item.id) : 0;

            ExecuteEvents.Execute(row.gameObject, new PointerEventData(EventSystem.current),
                                  ExecuteEvents.pointerClickHandler);
            yield return null;
            yield return null;
            ui.Close();
            yield return null;

            if (recipe?.result?.item == null) yield break;

            // 제작에는 시간이 걸린다(백로그 34). 누르는 것과 손에 들어오는 것 사이가
            // 벌어졌으므로, 목표가 넘어가기 전에 그 사이를 기다려 준다.
            if (atBench)
            {
                yield return E2EHarness.WaitUntil(
                    () => bench.Work.Queue.IsEmpty,
                    $"제작대가 {recipeId}를 다 만들었다 ({recipe.craftSeconds:F0}초)",
                    recipe.craftSeconds + 10f);

                // 제작대는 만든 것을 들고 기다린다. 돌아와 가져가야 손에 들어온다.
                if (bench.Work.HasOutput) bench.Interact(E2EHarness.Player);
                yield return null;
            }

            yield return E2EHarness.WaitUntil(
                () => Inv.CountOf(recipe.result.item.id) > before,
                $"{recipeId} 제작이 끝났다 ({recipe.craftSeconds:F0}초)",
                recipe.craftSeconds + 6f);
        }
    }
}
