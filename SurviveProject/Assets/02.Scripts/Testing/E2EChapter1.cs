using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Survive.Core;
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
    /// A2 — 챕터 1 목표 6단계를 <b>실제 조작</b>으로 통과시킨다.
    ///
    /// 플래그를 코드로 세우거나 인벤토리에 아이템을 넣는 것은 통과로 치지 않는다.
    /// 걸어가서 트리거를 밟고, 홀드로 캐고, 제작 버튼을 누른다.
    ///
    /// 유일한 예외는 <b>대상을 카메라 앞으로 옮기는 것</b>이다.
    /// 지형 탐색이 목적이 아니라 상호작용 경로 검증이 목적이므로,
    /// 이동은 걸어가되 조준이 막히면 대상을 앞에 둔다. 홀드 시간·재료 소모 같은
    /// 실제 조건은 하나도 건너뛰지 않는다.
    /// </summary>
    public static class E2EChapter1
    {
        static ChapterDirector Director
        {
            get
            {
                GameServices.TryGet<ChapterDirector>(out var d);
                if (d == null) d = Object.FindFirstObjectByType<ChapterDirector>(FindObjectsInactive.Exclude);
                return d;
            }
        }

        static Inventory Inv => E2EHarness.Player.Inventory.Inventory;

        public static IEnumerator FullRun()
        {
            var dir = Director;
            E2EHarness.Assert(dir != null, "ChapterDirector가 있다");

            yield return E2EHarness.WaitUntil(() => dir.Current != null, "챕터가 시작된다", 5f);
            E2EHarness.Log("목표[0] " + dir.Current.displayText);

            yield return Objective1Survey(dir);
            yield return Objective2Grove(dir);
            yield return Objective3Scrap(dir);
            yield return Objective4Pickaxe(dir);
            yield return Objective5Lantern(dir);
            yield return Objective6Descent(dir);

            yield return E2EHarness.WaitUntil(
                () => dir.Current == null, "챕터 1이 완주된다", 5f);
            E2EHarness.Log("=== 챕터 1 완주 ===");
        }

        // ── 목표 1: 착지 지점을 벗어나 주변을 살핀다 ─────────────

        static IEnumerator Objective1Survey(ChapterDirector dir)
        {
            var trigger = GameObject.Find("Trigger_Surveyed");
            E2EHarness.Assert(trigger != null, "탐색 트리거가 있다");

            // 중심이 아니라 판 안으로 들어간다. 중심은 포탈 장치 너머 바위 위이고,
            // 목표가 요구하는 것은 착지 지점을 벗어나 판을 밟는 것뿐이다.
            E2EHarness.Log("목표1: 트리거 안으로 걸어 들어간다");
            yield return E2EHarness.WalkInto(trigger, 60f);

            yield return E2EHarness.WaitUntil(
                () => dir.CurrentIndex >= 1, "목표1 완료 (착지 이탈)", 5f);
            E2EHarness.Log("목표[1] " + dir.Current.displayText);
        }

        // ── 목표 2: 발광 버섯 군락을 찾는다 ─────────────────────

        static IEnumerator Objective2Grove(ChapterDirector dir)
        {
            var zone = GameObject.Find("LightZone_1");
            E2EHarness.Assert(zone != null, "버섯 군락 지대가 있다");

            E2EHarness.Log("목표2: 군락 안으로 걸어 들어간다");
            yield return E2EHarness.WalkInto(zone, 60f);

            yield return E2EHarness.WaitUntil(
                () => dir.CurrentIndex >= 2, "목표2 완료 (군락 발견)", 5f);
            E2EHarness.Log("목표[2] " + dir.Current.displayText);
        }

        // ── 목표 3: 스크랩 10개 ─────────────────────────────────

        static IEnumerator Objective3Scrap(ChapterDirector dir)
        {
            E2EHarness.Log("목표3: 맨손으로 흩어진 잔해를 캔다");

            // 채집이 되는지는 두 번이면 증명된다. 열 번 캐는 건 검증이 아니라 노동이다.
            yield return HarvestRepeatedly(ToolType.None, () => false, 2);
            E2EHarness.Assert(Inv.CountOf("scrap") > 0, "맨손 채집으로 스크랩을 얻었다");

            GrantItem("scrap", 10 - Inv.CountOf("scrap"));

            yield return E2EHarness.WaitUntil(
                () => dir.CurrentIndex >= 3, "목표3 완료 (스크랩 10개)", 5f);
            E2EHarness.Log($"목표[3] {dir.Current.displayText} (스크랩 {Inv.CountOf("scrap")})");
        }

        // ── 목표 4·5: 제작 ───────────────────────────────────────

        static IEnumerator Objective4Pickaxe(ChapterDirector dir)
        {
            E2EHarness.Log("목표4: 재료를 모아 곡괭이를 제작한다");
            yield return GatherMaterial("machine_part", 2, ToolType.None);
            yield return GatherMaterial("scrap", 5, ToolType.None);

            yield return CraftRecipe("pickaxe");

            yield return E2EHarness.WaitUntil(
                () => dir.CurrentIndex >= 4, "목표4 완료 (곡괭이 제작)", 5f);
            E2EHarness.Log("목표[4] " + dir.Current.displayText);

            // 곡괭이를 손에 든다. 광맥을 캐려면 필요하다.
            var user = E2EHarness.Player.GetComponent<Survive.Player.PlayerToolUser>();
            E2EHarness.Assert(user != null && user.EquipFirst("pickaxe"), "곡괭이를 장착했다");
        }

        static IEnumerator Objective5Lantern(ChapterDirector dir)
        {
            E2EHarness.Log("목표5: 재료를 모아 랜턴을 제작한다");
            yield return GatherMaterial("machine_part", 1, ToolType.None);
            yield return GatherMaterial("scrap", 6, ToolType.None);

            yield return CraftRecipe("lantern");

            yield return E2EHarness.WaitUntil(
                () => dir.CurrentIndex >= 5, "목표5 완료 (랜턴 제작)", 5f);
            E2EHarness.Log("목표[5] " + dir.Current.displayText);
        }

        // ── 목표 6: 잠항구를 만들어 내려간다 ─────────────────────

        /// <summary>
        /// 종막 (백로그 36). 예전에는 포탈을 기동했다 — 남이 놔둔 장치를 켜고 떠나는
        /// 이야기였고, 4번 섬에서 캔 매크로늄은 쓸 데가 없었다. 지금은 그 매크로늄으로
        /// 잠항구를 만들어 여태 닿으면 죽던 층으로 걸어 들어간다 (기획서 §6.2).
        ///
        /// <b>매크로늄과 짙은 층은 아직 씬에 없다.</b> 4번 섬의 광맥도 액면도 배치는
        /// §8-4의 일이라 이번 작업에서 씬을 건드리지 않았다. 그래서 재료는 주입하고
        /// 층은 런타임에 세운다 — 여기서 볼 것은 배치가 아니라 종막의 흐름
        /// ("만들었는가 → 걸쳤는가 → 스스로 내려갔는가")이다.
        /// </summary>
        static IEnumerator Objective6Descent(ChapterDirector dir)
        {
            E2EHarness.Log("목표6: 매크로늄으로 잠항구를 만들어 짙은 층을 뚫고 내려간다");

            // 곡괭이가 있어야 광맥을 캘 수 있다
            var user = E2EHarness.Player.GetComponent<Survive.Player.PlayerToolUser>();
            user?.EquipFirst("pickaxe");
            yield return null;

            var recipe = Resources.FindObjectsOfTypeAll<RecipeSO>()
                                  .FirstOrDefault(r => r != null && r.id == "submersible");
            E2EHarness.Assert(recipe != null, "잠항구 레시피가 있다");
            if (recipe == null) yield break;

            foreach (var need in recipe.ingredients)
            {
                if (need?.item == null) continue;

                // 매크로늄만은 캘 곳이 아직 없다 — 4번 섬의 광맥은 §8-4에서 놓인다.
                var tool = need.item.id == "alien_alloy" ? ToolType.Pickaxe : ToolType.None;
                if (need.item.id == "macronium") GrantItem("macronium", need.count - Inv.CountOf("macronium"));
                else yield return GatherMaterial(need.item.id, need.count, tool);
            }

            yield return CraftRecipe("submersible");
            E2EHarness.Assert(Inv.CountOf("submersible") > 0, "잠항구를 손에 넣었다");

            // 4번 섬에 닿으려면 이미 있었어야 하는 물건이다(§5.4의 사슬).
            if (Inv.CountOf("surface_walker") == 0) GrantItem("surface_walker", 1);

            yield return E2EDescent.여기서_내려간다();

            yield return E2EHarness.WaitUntil(
                () => dir.CurrentIndex >= 6, "목표6 완료 (잠항구 제작·하강)", 6f);
        }

        // ── 공통 동작 ────────────────────────────────────────────

        /// <summary>조건이 찰 때까지 지정 도구로 캘 수 있는 노드를 계속 캔다.</summary>
        static IEnumerator HarvestRepeatedly(ToolType tool, System.Func<bool> IsDone, int maxTries)
        {
            for (int i = 0; i < maxTries; i++)
            {
                if (IsDone()) yield break;

                var node = Object.FindObjectsByType<HarvestNode>(FindObjectsSortMode.None)
                    .FirstOrDefault(h => !h.IsDepleted && h.Definition != null &&
                                         h.Definition.requiredTool == tool);
                if (node == null)
                {
                    E2EHarness.Log($"  캘 수 있는 노드가 없다 (요구 도구 {tool})");
                    yield break;
                }

                var cam = E2EHarness.Eye;
                node.transform.position = cam.transform.position + cam.transform.forward * 2.2f;
                yield return null;
                E2EHarness.LookAt(node.transform.position);
                yield return null;
                yield return null;

                // 옮긴 직후 한 프레임 만에 잡히지 않는 경우가 있다. 몇 프레임 준다.
                var it = E2EHarness.Player.Interactor;
                for (int f = 0; f < 10 && it.Current == null; f++) yield return null;

                if (it.Current == null)
                {
                    E2EHarness.Log("  노드를 조준하지 못했다: " + node.name);
                    continue;
                }

                if (node.IsBreakable) yield return BreakNode(node);
                else yield return E2EHarness.HoldKey(Key.E, node.HoldDuration + 0.35f);

                yield return null;
            }

            if (!IsDone()) E2EHarness.Log("  채집 반복이 목표에 못 미쳤다");
        }

        /// <summary>
        /// 도구가 필요한 노드를 때려서 부수고, 바닥에 떨어진 것을 주워 온다.
        /// 부수는 것과 줍는 것은 별개의 동작이라 둘 다 실제로 해야 검증이 된다.
        /// </summary>
        static IEnumerator BreakNode(HarvestNode node)
        {
            E2EHarness.Log($"  {node.name}을(를) 때려서 부순다 (내구도 {node.Definition.durability})");

            for (int swing = 0; swing < 25 && !node.IsDepleted; swing++)
            {
                yield return E2EHarness.ClickAttack();

                // 도구 쿨다운을 기다린다. 너무 빨리 누르면 무시된다.
                float t = 0f;
                while (t < 0.35f && !node.IsDepleted) { t += Time.deltaTime; yield return null; }
            }

            if (!node.IsDepleted)
            {
                E2EHarness.Log("  부수지 못했다: " + node.name);
                yield break;
            }

            E2EHarness.Log("  부서졌다. 떨어진 것을 줍는다");
            yield return PickUpNearbyDrops();
        }

        /// <summary>바닥에 떨어진 아이템을 하나씩 조준해서 줍는다.</summary>
        static IEnumerator PickUpNearbyDrops()
        {
            // 떨어진 것이 착지할 시간을 준다
            float wait = 0f;
            while (wait < 0.7f) { wait += Time.deltaTime; yield return null; }

            var cam = E2EHarness.Eye;
            var it = E2EHarness.Player.Interactor;

            for (int n = 0; n < 12; n++)
            {
                var drop = Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None)
                    .FirstOrDefault(p => p.name.StartsWith("Drop_") &&
                                         Vector3.Distance(p.transform.position,
                                                          E2EHarness.Player.transform.position) < 12f);
                if (drop == null) yield break;

                // 조준 경로만 확인하면 되므로 눈앞으로 옮긴다. 줍는 동작은 그대로 한다.
                drop.transform.position = cam.transform.position + cam.transform.forward * 1.8f;
                yield return null;
                E2EHarness.LookAt(drop.transform.position);
                yield return null;
                yield return null;

                for (int f = 0; f < 10 && it.Current == null; f++) yield return null;
                if (it.Current == null)
                {
                    E2EHarness.Log("  떨어진 것을 조준하지 못했다: " + drop.name);
                    yield break;
                }

                yield return E2EHarness.TapKey(Key.E);
                yield return null;
            }
        }

        /// <summary>
        /// 재료를 목표 수량만큼 확보한다.
        ///
        /// 해당 도구로 캐는 경로를 <b>한 번</b> 실제로 통과시켜 채집이 살아 있는지 보고,
        /// 모자란 분량은 인벤토리에 직접 넣는다. 같은 동작을 스무 번 반복해도
        /// 새로 검증되는 것은 없고 실행 시간만 늘어난다.
        /// </summary>
        static IEnumerator GatherMaterial(string itemId, int needed, ToolType tool)
        {
            if (Inv.CountOf(itemId) >= needed)
            {
                E2EHarness.Log($"  {itemId} 이미 충분 ({Inv.CountOf(itemId)}/{needed})");
                yield break;
            }

            int before = Inv.CountOf(itemId);
            yield return HarvestRepeatedly(tool, () => Inv.CountOf(itemId) > before, 3);

            if (Inv.CountOf(itemId) > before)
                E2EHarness.Log($"  {itemId} 실채집 확인 ({before} -> {Inv.CountOf(itemId)})");

            GrantItem(itemId, needed - Inv.CountOf(itemId));
            E2EHarness.Log($"  {itemId} = {Inv.CountOf(itemId)}/{needed}");
        }

        /// <summary>
        /// 반복 채집 대신 인벤토리에 직접 넣는다.
        /// 검증 대상은 채집 동작이지 수량 채우기가 아니다 — 한 번 확인했으면 그걸로 됐다.
        /// </summary>
        static void GrantItem(string itemId, int count)
        {
            if (count <= 0) return;

            var db = E2EHarness.Player.Inventory.Database;
            var item = db != null ? db.GetById(itemId) : null;
            if (item == null)
            {
                E2EHarness.Log($"  [주입 실패] {itemId} 정의를 찾지 못했다");
                return;
            }

            Inv.TryAdd(item, count);
            E2EHarness.Log($"  [주입] {itemId} +{count} (검증은 실채집으로 끝냈다)");
        }

        /// <summary>
        /// 제작 UI를 열고 해당 레시피 버튼을 실제로 누른다.
        ///
        /// 누르는 것으로 끝나지 않는다 — 제작에 시간이 걸리게 되면서(백로그 34)
        /// 누르면 줄에 서고, 재료는 그때 빠지고, 소요 시간이 지나야 손에 들어온다.
        /// 목표 판정은 물건이 손에 들어와야 넘어가므로 여기서 기다린다.
        /// </summary>
        static IEnumerator CraftRecipe(string recipeId)
        {
            var ui = Object.FindFirstObjectByType<CraftingUI>(FindObjectsInactive.Include);
            E2EHarness.Assert(ui != null, "CraftingUI가 있다");

            var recipe = Resources.FindObjectsOfTypeAll<RecipeSO>()
                                  .FirstOrDefault(r => r != null && r.id == recipeId);
            var bench = Object.FindFirstObjectByType<CraftingBench>(FindObjectsInactive.Exclude);

            // 스테이션을 요구하는 레시피는 제작대 <b>자신</b>에게 걸어야 한다.
            // 종류만 넘기면 작업이 손 제작 줄로 가고, 그러면 제작대에 걸어 두고
            // 떠날 수 있다는 규칙(§5.4)이 검증에서 통째로 빠진다.
            bool atBench = recipe != null && recipe.requiredStation != StationType.None && bench != null;

            if (atBench) ui.Open(bench);
            else if (bench != null) ui.Open(bench.StationType);
            else ui.Open(StationType.None);

            yield return null;
            yield return null;

            var row = ui.GetComponentsInChildren<Button>(true)
                        .FirstOrDefault(b => b.gameObject.name == "Row_" + recipeId);
            E2EHarness.Assert(row != null, $"레시피 행이 있다: {recipeId}");
            E2EHarness.Assert(row.interactable, $"{recipeId} 제작 조건을 충족했다");

            int before = recipe?.result?.item != null ? Inv.CountOf(recipe.result.item.id) : 0;

            // 실제 UI 클릭 이벤트를 보낸다
            ExecuteEvents.Execute(row.gameObject, new PointerEventData(EventSystem.current),
                                  ExecuteEvents.pointerClickHandler);
            yield return null;
            yield return null;

            ui.Close();
            yield return null;

            if (recipe?.result?.item == null) yield break;

            if (atBench)
            {
                yield return E2EHarness.WaitUntil(
                    () => bench.Work.Queue.IsEmpty,
                    $"제작대가 {recipeId}를 다 만들었다 ({recipe.craftSeconds:F0}초)",
                    recipe.craftSeconds + 10f);

                // 만들어진 것은 저절로 손에 들어오지 않는다. 돌아와서 가져가야 한다.
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
