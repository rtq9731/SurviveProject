using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Survive.Building;
using Survive.Crafting;
using Survive.Interaction;
using Survive.Items;
using Survive.Player;
using Survive.UI;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// 1차 D2 이후에 들어온 것들을 실제 조작 경로로 훑는다.
    ///
    /// 건설·제작·보관함·철거·화톳불·수영은 지금까지 손으로 한 번씩 찔러본 게 전부다.
    /// 한 번 눌러 보고 "된다"고 넘긴 것은 다음 커밋에서 조용히 망가진다.
    ///
    /// <see cref="E2EWalkthrough"/>와 같은 원칙을 지킨다 — <b>아무것도 옮기지 않는다.</b>
    /// 다만 여기서 보는 것은 배치가 아니라 <b>시스템이 서로 맞물리는가</b>다:
    /// 세운 제작대에서 진짜로 제작이 되는가, 부수면 재료가 돌아오는가,
    /// 통을 부수면 안의 물건이 증발하지 않는가.
    ///
    /// 사람이 봐야 하는 "재미있는가"는 여전히 대신할 수 없다.
    /// 여기서 걸리는 것은 "재미 이전에 동작하지 않는다" 부류다.
    /// </summary>
    public static class E2EBaseBuilding
    {
        static Inventory Inv => E2EHarness.Player.Inventory.Inventory;

        /// <summary>SubmergedOxygenDrain의 기본값. 로그에 여유 시간을 적기 위한 것뿐이다.</summary>
        const float drainPerSecondGuess = 6f;

        static BuildPlacer Placer =>
            Object.FindFirstObjectByType<BuildPlacer>(FindObjectsInactive.Exclude);

        public static IEnumerator FullRun()
        {
            yield return Prepare();

            yield return HandCrafting();
            yield return BuildMenuKey();

            var bench = default(GameObject);
            yield return Build("bench", r => bench = r);
            yield return CraftAtBuiltBench(bench);

            var box = default(GameObject);
            yield return StepAside();
            yield return Build("storage", r => box = r);
            yield return StoreAndTakeBack(box);
            yield return DemolishKeepsContents(box);

            var fire = default(GameObject);
            yield return StepAside();
            yield return Build("campfire", r => fire = r);
            yield return CampfireFeedsLantern(fire);

            yield return DemolishRefunds(fire);

            yield return SwimAndSee();
            yield return HoldToSwing();

            E2EHarness.Log("=== 거점 시스템 완주 ===");
        }

        // ── 준비 ────────────────────────────────────────────────

        /// <summary>
        /// 재료를 채운다. 모으는 것은 <see cref="E2EWalkthrough"/>가 이미 본다.
        /// 여기서 다시 캐면 확인하려는 것이 아니라 채집만 재검증하게 된다.
        /// </summary>
        static IEnumerator Prepare()
        {
            var dir = Object.FindFirstObjectByType<Survive.Progression.ChapterDirector>(
                FindObjectsInactive.Exclude);
            yield return E2EHarness.WaitUntil(() => dir != null && dir.Current != null,
                                              "챕터가 시작된다", 5f);

            var db = E2EHarness.Player.Inventory.Database;
            E2EHarness.Assert(db != null, "아이템 데이터베이스가 연결돼 있다");

            // 목록을 손으로 적으면 건축물에 재료가 하나 추가될 때마다 낡는다.
            // 카탈로그가 요구하는 것을 그대로 채운다.
            var needed = new System.Collections.Generic.HashSet<string>
                             { "scrap", "machine_part", "alien_alloy" };

            yield return E2EHarness.WaitUntil(() => Placer != null, "BuildPlacer가 준비된다", 8f);

            var placer = Placer;
            if (placer != null)
            {
                foreach (var id in new[] { "bench", "storage", "campfire", "fence" })
                {
                    placer.SelectById(id);
                    var cost = placer.Selected?.cost;
                    if (cost == null) continue;
                    foreach (var c in cost) if (c?.item != null) needed.Add(c.item.id);
                }
                placer.Cancel();
            }

            foreach (var id in needed)
            {
                var item = db.GetById(id);
                E2EHarness.Assert(item != null, $"아이템 정의를 찾았다: {id}");
                if (item != null) Inv.TryAdd(item, 40);
            }

            E2EHarness.Log("  재료 주입: " +
                string.Join(", ", needed.Select(id => $"{id} {Inv.CountOf(id)}")));

            // 랜턴을 지금 켜 둔다. 뒤의 화톳불 검사는 배터리가 <b>줄어 있어야</b>
            // 차오르는 것을 볼 수 있는데, Recharge는 음수를 받지 않는다.
            // 시나리오를 도는 동안 자연히 닳게 두는 것이 실제 플레이와도 같다.
            var lantern = Object.FindFirstObjectByType<LanternController>(FindObjectsInactive.Include);
            if (lantern != null) lantern.SetOn(true);

            yield return null;
        }

        // ── 손 제작 ─────────────────────────────────────────────

        /// <summary>
        /// 제작대 없이 손으로 만들 수 있는 범위가 실제로 갈라져 있는지 본다.
        /// 손에서 전부 만들 수 있으면 제작대를 지을 이유가 없고,
        /// 손에서 아무것도 못 만들면 첫 제작대를 지을 수단이 없다.
        /// </summary>
        static IEnumerator HandCrafting()
        {
            var ui = Object.FindFirstObjectByType<CraftingUI>(FindObjectsInactive.Include);
            E2EHarness.Assert(ui != null, "CraftingUI가 있다");

            ui.Open(StationType.None);
            yield return null;
            yield return null;

            // 행 오브젝트는 재사용되므로 <b>존재</b>가 아니라 <b>보이는가</b>를 봐야 한다.
            // 숨겨진 행까지 세면 손에서 전부 만들 수 있는 것처럼 읽힌다.
            var rows = VisibleRows(ui);

            E2EHarness.Assert(rows.Count > 0, "손 제작 목록이 비어 있지 않다");
            E2EHarness.Assert(!rows.Contains("portal_key"), "포탈 기동 키는 손으로 만들 수 없다");

            E2EHarness.Log($"  손 제작 가능: {string.Join(", ", rows)}");

            ui.Close();
            yield return null;
        }

        /// <summary>지금 화면에 실제로 뜬 레시피 id들.</summary>
        static System.Collections.Generic.List<string> VisibleRows(CraftingUI ui) =>
            ui.GetComponentsInChildren<Button>(true)
              .Where(b => b.gameObject.name.StartsWith("Row_") && b.gameObject.activeInHierarchy)
              .Select(b => b.gameObject.name.Substring(4))
              .ToList();

        // ── 건설 ────────────────────────────────────────────────

        /// <summary>B 키가 실제로 건설 목록을 여닫는지 본다.</summary>
        static IEnumerator BuildMenuKey()
        {
            var menu = Object.FindFirstObjectByType<BuildMenuUI>(FindObjectsInactive.Include);
            E2EHarness.Assert(menu != null, "건설 메뉴가 있다");

            yield return E2EHarness.TapKey(Key.B);
            yield return null;
            E2EHarness.Assert(menu.IsOpen, "B로 건설 목록이 열린다");

            // 목록이 떴는데 커서가 시야에 묶여 있으면 아무것도 고를 수 없다.
            // 열리는 것과 쓸 수 있는 것은 다르다.
            E2EHarness.Assert(Cursor.lockState == CursorLockMode.None,
                              "목록이 열리면 마우스를 쓸 수 있다");
            E2EHarness.Assert(Cursor.visible, "커서가 보인다");

            yield return E2EHarness.TapKey(Key.B);
            yield return null;
            E2EHarness.Assert(!menu.IsOpen, "B로 건설 목록이 닫힌다");

            // 닫으면 되돌아와야 한다. 유령을 조준하려면 시야가 마우스에 물려야 한다.
            E2EHarness.Assert(Cursor.lockState == CursorLockMode.Locked,
                              "목록을 닫으면 시야 조작으로 돌아온다");
        }

        /// <summary>
        /// 놓을 자리를 스스로 찾아 세운다.
        ///
        /// 좌표를 박아 두지 않는 이유: 지형이 바뀌면 그 좌표만 조용히 낡는다.
        /// 실제 판정 함수에게 물어보고 통과하는 곳을 쓰면 지형이 바뀌어도 따라간다.
        /// </summary>
        static IEnumerator Build(string id, System.Action<GameObject> result)
        {
            var placer = Placer;
            E2EHarness.Assert(placer != null, "BuildPlacer가 있다");

            placer.SelectById(id);
            yield return null;
            E2EHarness.Assert(placer.Selected != null, $"건축물 정의를 찾았다: {id}");

            bool found = false;
            yield return AimAtBuildableGround(placer, r => found = r);

            if (!found)
            {
                E2EHarness.Log($"  [배치 문제] {id}를 놓을 자리를 찾지 못했다 " +
                               $"(마지막 판정 {placer.LastResult})");
                placer.Cancel();
                result(null);
                yield break;
            }

            // 실제 조작대로 좌클릭으로 세운다
            int before = Object.FindObjectsByType<BuiltStructure>(FindObjectsSortMode.None).Length;
            yield return E2EHarness.ClickAttack();
            yield return null;
            yield return null;

            var built = Object.FindObjectsByType<BuiltStructure>(FindObjectsSortMode.None)
                              .OrderByDescending(b => b.GetEntityId())
                              .FirstOrDefault(b => b.Definition != null && b.Definition.id == id);

            E2EHarness.Assert(built != null, $"좌클릭으로 세웠다: {id}");
            E2EHarness.Assert(
                Object.FindObjectsByType<BuiltStructure>(FindObjectsSortMode.None).Length == before + 1,
                "세운 것이 정확히 하나 늘었다");

            placer.Cancel();
            yield return null;

            if (built == null) { result(null); yield break; }

            E2EHarness.Assert(built.GetComponent<StructureDemolisher>() != null,
                              $"{id}는 부술 수 있다");
            E2EHarness.Log($"  세웠다: {built.name} {built.transform.position.ToString("F0")}");
            result(built.gameObject);
        }

        /// <summary>
        /// 판정이 통과하는 방향을 찾을 때까지 둘러본다.
        /// 동굴 지형이라 바로 앞은 바위 벽인 경우가 흔하다.
        ///
        /// 실패했을 때 무엇에 막혔는지도 센다. "자리를 못 찾았다"만으로는
        /// 지형이 문제인지 방금 세운 것이 문제인지 알 수 없다.
        /// </summary>
        static IEnumerator AimAtBuildableGround(BuildPlacer placer, System.Action<bool> result)
        {
            var player = E2EHarness.Player.transform;
            var tally = new System.Collections.Generic.Dictionary<PlacementResult, int>();

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

                    if (r == PlacementResult.Ok)
                    {
                        result(true);
                        yield break;
                    }
                }
            }

            E2EHarness.Log("  막힌 사유: " +
                string.Join(", ", tally.Select(kv => $"{kv.Key} {kv.Value}회")));
            result(false);
        }

        /// <summary>
        /// 몇 걸음 옮긴다. 방금 세운 것 위에 다음 것을 지으려 하면
        /// 시나리오가 스스로 만든 장애물에 걸린다 — 사람도 그렇게 짓지 않는다.
        /// </summary>
        static IEnumerator StepAside()
        {
            var from = E2EHarness.Player.transform.position;

            for (int a = 0; a < 8; a++)
            {
                var dir = Quaternion.Euler(0f, a * 45f + 20f, 0f) * Vector3.forward;
                var want = from + dir * 6f;

                if (!UnityEngine.AI.NavMesh.SamplePosition(want, out var hit, 3f,
                                                           UnityEngine.AI.NavMesh.AllAreas))
                    continue;

                yield return E2EHarness.TryWalkTo(hit.position, 2.0f, 20f);
                if (E2EHarness.LastWalkArrived &&
                    Vector3.Distance(E2EHarness.Player.transform.position, from) > 3f)
                    yield break;
            }

            E2EHarness.Log("  [배치 문제] 옆으로 비켜설 곳이 없다");
        }

        // ── 세운 제작대 ─────────────────────────────────────────

        /// <summary>
        /// 세운 제작대가 장식이 아니라 제작대인지 본다.
        /// 상호작용 경로 전체를 탄다 — 조준되고, E가 먹고, 제작이 된다.
        /// </summary>
        static IEnumerator CraftAtBuiltBench(GameObject bench)
        {
            if (bench == null) { E2EHarness.Log("  제작대를 못 세워 건너뛴다"); yield break; }

            var station = bench.GetComponent<CraftingBench>();
            E2EHarness.Assert(station != null, "세운 것에 제작대 기능이 있다");
            if (station == null) yield break;

            E2EHarness.AssertEqual(station.StationType, StationType.Bench,
                                   "간이 제작대는 제작대 티어다");

            E2EHarness.LookAt(bench.transform.position + Vector3.up * 0.5f);
            yield return null;
            yield return null;

            var it = E2EHarness.Player.Interactor;
            yield return E2EHarness.WaitUntil(() => it.Current is CraftingBench,
                                              "세운 제작대가 조준된다", 4f);

            int before = Inv.CountOf("pickaxe");
            yield return E2EHarness.TapKey(Key.E);
            yield return null;
            yield return null;

            var ui = Object.FindFirstObjectByType<CraftingUI>(FindObjectsInactive.Include);
            E2EHarness.Assert(ui.IsOpen, "E로 제작 화면이 열린다");
            E2EHarness.AssertEqual(ui.CurrentStation, StationType.Bench, "제작대 목록이 뜬다");

            var shown = VisibleRows(ui);
            E2EHarness.Assert(shown.Contains("portal_key"),
                              "세운 제작대에서 포탈 기동 키가 보인다 — 손에서는 숨어 있던 것이다");
            E2EHarness.Log($"  제작대 목록: {string.Join(", ", shown)}");

            // 실제로 하나 만들어 본다. 목록에 뜨는 것과 만들어지는 것은 다르다.
            var row = ui.GetComponentsInChildren<Button>(true)
                        .FirstOrDefault(b => b.gameObject.name == "Row_pickaxe");
            E2EHarness.Assert(row != null && row.interactable, "곡괭이를 만들 수 있다");

            if (row != null)
            {
                ExecuteEvents.Execute(row.gameObject, new PointerEventData(EventSystem.current),
                                      ExecuteEvents.pointerClickHandler);
                yield return null;
                yield return null;
                E2EHarness.Assert(Inv.CountOf("pickaxe") > before, "세운 제작대에서 곡괭이가 나왔다");
            }

            ui.Close();
            yield return null;
        }

        // ── 보관함 ──────────────────────────────────────────────

        static IEnumerator StoreAndTakeBack(GameObject box)
        {
            if (box == null) { E2EHarness.Log("  보관함을 못 세워 건너뛴다"); yield break; }

            var storage = box.GetComponent<StorageContainer>();
            E2EHarness.Assert(storage != null, "세운 것에 보관 기능이 있다");
            if (storage == null) yield break;

            E2EHarness.LookAt(box.transform.position + Vector3.up * 0.5f);
            yield return null;
            yield return null;

            var it = E2EHarness.Player.Interactor;
            yield return E2EHarness.WaitUntil(() => it.Current is StorageContainer,
                                              "보관함이 조준된다", 4f);

            yield return E2EHarness.TapKey(Key.E);
            yield return null;

            var ui = Object.FindFirstObjectByType<StorageUI>(FindObjectsInactive.Include);
            E2EHarness.Assert(ui != null && ui.IsOpen, "E로 보관함이 열린다");

            // 넣고 뺀다. 넣기만 확인하면 물건이 갇히는 버그를 못 본다.
            var alloy = E2EHarness.Player.Inventory.Database.GetById("alien_alloy");
            int mine = Inv.CountOf("alien_alloy");
            E2EHarness.Assert(mine >= 4, "옮길 합금을 갖고 있다");

            Inv.TryRemove("alien_alloy", 4);
            storage.Contents.TryAdd(alloy, 4);
            yield return null;

            E2EHarness.AssertEqual(storage.Contents.CountOf("alien_alloy"), 4, "보관함에 4개가 들어갔다");
            E2EHarness.AssertEqual(Inv.CountOf("alien_alloy"), mine - 4, "내 인벤토리에서 4개가 빠졌다");

            storage.Contents.TryRemove("alien_alloy", 2);
            Inv.TryAdd(alloy, 2);
            yield return null;

            E2EHarness.AssertEqual(storage.Contents.CountOf("alien_alloy"), 2, "보관함에서 2개를 뺐다");
            E2EHarness.AssertEqual(Inv.CountOf("alien_alloy"), mine - 2, "뺀 2개가 내게 돌아왔다");

            if (ui != null) ui.Close();
            yield return null;
        }

        /// <summary>
        /// 통을 부쉈을 때 안의 물건이 증발하지 않는지 본다.
        /// 이건 버그가 나도 조용해서, 사람이 플레이로 잡기 가장 어려운 부류다.
        /// </summary>
        static IEnumerator DemolishKeepsContents(GameObject box)
        {
            if (box == null) yield break;

            var storage = box.GetComponent<StorageContainer>();
            int inside = storage != null ? storage.Contents.CountOf("alien_alloy") : 0;
            E2EHarness.Assert(inside > 0, "부수기 전 보관함에 물건이 남아 있다");

            var dem = box.GetComponent<StructureDemolisher>();
            E2EHarness.Assert(dem != null, "보관함을 부술 수 있다");

            bool aimed = false;
            yield return AimForDemolish(box, r => aimed = r);
            E2EHarness.Assert(aimed, "보관함을 조준할 수 있다");

            yield return HoldDemolish(box, dem.HoldSeconds);

            E2EHarness.Assert(box == null, "R 홀드로 보관함이 부서졌다");

            // 안에 있던 것이 바닥에 떨어져 있어야 한다
            float wait = 0f;
            while (wait < 1.0f) { wait += Time.deltaTime; yield return null; }

            bool spilled = Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None)
                                 .Any(p => p.name.StartsWith("Drop_"));
            E2EHarness.Assert(spilled, "부순 보관함의 내용물이 바닥에 쏟아졌다");
        }

        /// <summary>
        /// 부술 대상이 실제로 조준에 잡히는 자리로 물러선다.
        ///
        /// 코앞에 붙어 서면 카메라가 콜라이더 안으로 들어가고, 안에서 쏜 레이는
        /// 그 콜라이더를 맞히지 못한다. 철거 사거리는 4m니 3m쯤이 알맞다.
        /// </summary>
        static IEnumerator AimForDemolish(GameObject structure, System.Action<bool> result)
        {
            var eye = E2EHarness.Eye;

            // 조준점은 콜라이더의 한가운데를 쓴다. transform 원점은 대개 발밑이라
            // 그쪽을 겨누면 레이가 지면을 먼저 맞는다.
            var col = structure.GetComponentInChildren<Collider>();
            var target = col != null ? col.bounds.center : structure.transform.position;

            foreach (float dist in new[] { 2.4f, 3.0f, 3.6f })
            {
                for (int a = 0; a < 8; a++)
                {
                    if (structure == null) { result(false); yield break; }

                    var dir = Quaternion.Euler(0f, a * 45f, 0f) * Vector3.forward;

                    if (!UnityEngine.AI.NavMesh.SamplePosition(target + dir * dist, out var nav,
                                                               2.0f, UnityEngine.AI.NavMesh.AllAreas))
                        continue;

                    yield return E2EHarness.TryWalkTo(nav.position, 1.5f, 12f);

                    E2EHarness.LookAt(target);
                    yield return null;
                    yield return null;

                    if (eye != null &&
                        Physics.Raycast(eye.transform.position, eye.transform.forward, out var hit,
                                        4f, ~0, QueryTriggerInteraction.Collide) &&
                        hit.collider.GetComponentInParent<StructureDemolisher>() != null)
                    {
                        result(true);
                        yield break;
                    }
                }
            }

            E2EHarness.Log($"  [문제] {structure.name}을 부술 각을 잡지 못한다 — " +
                           "가까이 서면 조준이 안 잡히고 물러설 곳이 없다");
            result(false);
        }

        /// <summary>
        /// R을 누른 채 대상을 계속 겨눈다.
        ///
        /// 한 번 조준하고 손을 놓으면 걸어온 관성 때문에 시선이 밀리고,
        /// 그러면 철거 대상이 풀려 진행도가 0으로 돌아간다.
        /// 사람도 부수는 동안에는 그것을 계속 본다.
        /// </summary>
        static IEnumerator HoldDemolish(GameObject structure, float holdSeconds)
        {
            var col = structure.GetComponentInChildren<Collider>();
            var target = col != null ? col.bounds.center : structure.transform.position;

            yield return E2EHarness.PressKey(Key.R);

            float t = 0f;
            float limit = holdSeconds + 2f;
            while (t < limit && structure != null)
            {
                E2EHarness.LookAt(target);
                t += Time.deltaTime;
                yield return null;
            }

            yield return E2EHarness.ReleaseKey(Key.R);
            yield return null;
        }

        // ── 화톳불 ──────────────────────────────────────────────

        /// <summary>
        /// 화톳불이 랜턴을 채우는지 본다.
        /// 채우지 않으면 거점을 세울 이유가 하나 사라진다.
        /// </summary>
        static IEnumerator CampfireFeedsLantern(GameObject fire)
        {
            if (fire == null) { E2EHarness.Log("  화톳불을 못 세워 건너뛴다"); yield break; }

            var camp = fire.GetComponent<Campfire>();
            E2EHarness.Assert(camp != null, "세운 것에 화톳불 기능이 있다");
            E2EHarness.Assert(camp == null || camp.IsBurning, "세우자마자 타고 있다");

            var lantern = Object.FindFirstObjectByType<LanternController>(FindObjectsInactive.Include);
            if (lantern == null)
            {
                E2EHarness.Log("  랜턴이 아직 없어 충전 확인을 건너뛴다");
                yield break;
            }

            E2EHarness.Assert(lantern.IsOn, "랜턴이 켜져 배터리가 닳고 있다");

            float before = lantern.Battery;
            E2EHarness.Assert(before < lantern.BatteryNormalized * 100f + 0.01f, "배터리를 읽을 수 있다");

            // 불 곁으로 간다. 충전 반경(6m) 안이어야 한다.
            yield return E2EHarness.TryWalkTo(fire.transform.position, 2.5f, 20f);

            float t = 0f;
            while (t < 1.5f) { t += Time.deltaTime; yield return null; }

            // 켜 둔 채로도 올라가야 한다. 충전이 소모보다 빨라야 "쉬는 자리"가 된다.
            E2EHarness.Assert(lantern.Battery > before,
                              $"불 곁에서 랜턴이 찼다 ({before:F0} → {lantern.Battery:F0})");
        }

        // ── 철거 환급 ───────────────────────────────────────────

        static IEnumerator DemolishRefunds(GameObject structure)
        {
            if (structure == null) yield break;

            var built = structure.GetComponent<BuiltStructure>();
            var dem = structure.GetComponent<StructureDemolisher>();
            if (built?.Definition?.cost == null || dem == null) yield break;

            var first = built.Definition.cost.FirstOrDefault(c => c?.item != null);
            if (first == null) yield break;

            int before = Inv.CountOf(first.item.id);

            bool aimed = false;
            yield return AimForDemolish(structure, r => aimed = r);
            E2EHarness.Assert(aimed, "구조물을 조준할 수 있다");

            yield return HoldDemolish(structure, dem.HoldSeconds);

            E2EHarness.Assert(structure == null, "R 홀드로 구조물이 부서졌다");
            E2EHarness.Assert(Inv.CountOf(first.item.id) > before,
                              $"부순 재료가 일부 돌아왔다 ({first.item.id} {before} → {Inv.CountOf(first.item.id)})");
        }

        // ── 수영 ────────────────────────────────────────────────

        /// <summary>
        /// 물에 들어가면 수영 상태로 바뀌고, 머리가 잠기면 시야가 흐려지는지 본다.
        /// 상태만 바뀌고 화면이 그대로면 플레이어는 자기가 물속인지 모른다.
        /// </summary>
        static IEnumerator SwimAndSee()
        {
            var swim = Object.FindFirstObjectByType<PlayerSwimming>(FindObjectsInactive.Exclude);
            if (swim == null) { E2EHarness.Log("  PlayerSwimming이 없어 건너뛴다"); yield break; }

            // P0에서 씬 안개가 항상 켜져 있게 바뀌어 RenderSettings.fog만으로는
            // 물속/뭍을 구분할 수 없다. 물에 들어가기 전의 안개 색/밀도를 기준으로 삼는다.
            var landFogColor = RenderSettings.fogColor;
            var landFogDensity = RenderSettings.fogDensity;

            var water = GameObject.FindGameObjectsWithTag("Untagged")
                                  .FirstOrDefault(g => g.name.Contains("Water"));
            if (water == null)
            {
                var rends = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                                  .Where(r => r.gameObject.name.Contains("Water"))
                                  .ToList();
                water = rends.Count > 0 ? rends[0].gameObject : null;
            }

            E2EHarness.Assert(water != null, "물이 씬에 있다");
            if (water == null) yield break;

            // 수면은 맵 전체를 덮는 한 장이다(1266m). 경계 상자로 훑으면 표본이
            // 전부 물 밖 지형에 떨어진다. 플레이 구역 주변만 촘촘히 본다.
            float surface = water.GetComponent<Renderer>() != null
                          ? water.GetComponent<Renderer>().bounds.max.y
                          : water.transform.position.y;

            var center = E2EHarness.Player.transform.position;

            // 가장 깊은 곳을 찾는다. 발 위치를 수면 바로 아래에 두면 머리는 물 위에 남는다 —
            // 그러면 "수중 시야"를 켜 보지도 않고 통과한다.
            var deepest = Vector3.zero;
            float best = 0f;

            for (float dx = -60f; dx <= 60f; dx += 3f)
            for (float dz = -60f; dz <= 60f; dz += 3f)
            {
                var probe = new Vector3(center.x + dx, surface + 1f, center.z + dz);

                if (!Physics.Raycast(probe, Vector3.down, out var bed, 40f, ~0,
                                     QueryTriggerInteraction.Ignore))
                    continue;

                float depth = surface - bed.point.y;
                if (depth > best) { best = depth; deepest = bed.point; }
            }

            E2EHarness.Log($"  수면 y={surface:F1} / 가장 깊은 곳 {deepest.ToString("F0")} 수심 {best:F1}m");

            if (best <= 0.1f)
            {
                E2EHarness.Log("  [문제] 물 바닥을 찾지 못했다");
                yield break;
            }

            E2EHarness.Teleport(deepest + Vector3.up * 0.1f);
            yield return null;

            float t = 0f;
            while (t < 3f && !swim.IsHeadSubmerged) { t += Time.deltaTime; yield return null; }

            E2EHarness.Assert(swim.IsSwimming || swim.IsWading, "물에 들어가면 수영 상태가 된다");

            // 사람 키는 약 1.8m다. 그보다 얕으면 수중 시야는 어디서도 켜지지 않는다.
            if (!swim.IsHeadSubmerged)
            {
                E2EHarness.Log($"  [문제] 가장 깊은 곳도 수심 {best:F1}m라 머리가 잠기지 않는다 — " +
                               "수중 시야가 발동할 자리가 없다");
                yield break;
            }

            var view = Object.FindFirstObjectByType<UnderwaterView>(FindObjectsInactive.Exclude);
            E2EHarness.Assert(view != null, "수중 시야 처리가 있다");
            // 씬 안개가 상시 켜진 뒤로는 RenderSettings.fog가 항상 true라 이 자체로는
            // 아무것도 증명하지 못한다. 물속 색으로 실제로 바뀌었는지를 본다.
            E2EHarness.Assert(RenderSettings.fogColor != landFogColor, "머리가 잠기면 안개 색이 물속 색으로 바뀐다");

            yield return DescendAndSprint(swim, deepest, best);
            yield return CanSurface(swim, deepest, best, surface);

            // 물 밖으로 나오면 원래대로 돌아와야 한다. 안개 색/밀도가 남으면 지상이 물속처럼 보인다.
            E2EHarness.Teleport(deepest + Vector3.up * (best + 4f));
            yield return null;
            float back = 0f;
            while (back < 2f && RenderSettings.fogColor != landFogColor) { back += Time.deltaTime; yield return null; }

            E2EHarness.Assert(!swim.IsHeadSubmerged, "물 밖으로 나왔다");
            E2EHarness.Assert(RenderSettings.fogColor == landFogColor
                               && Mathf.Approximately(RenderSettings.fogDensity, landFogDensity),
                               "물 밖으로 나오면 안개 색/밀도가 뭍 값으로 돌아온다");

            E2EHarness.Log($"  수영={swim.IsSwimming} 머리잠김={swim.IsHeadSubmerged} 안개색={RenderSettings.fogColor}");
        }

        /// <summary>
        /// 물속 상하 조작이 바뀐 뒤를 확인한다.
        ///
        /// Shift는 땅에서 달리기였는데 물에서만 하강이었다. 같은 손가락이
        /// 장소에 따라 다른 뜻을 갖는 건 익힐 것이 하나 더 느는 것이다.
        /// 이제 Shift는 어디서나 빨라지고, 하강은 Ctrl이다.
        /// </summary>
        static IEnumerator DescendAndSprint(PlayerSwimming swim, Vector3 deepest, float depth)
        {
            // 수면 근처에서 시작해 Ctrl로 내려간다
            E2EHarness.Teleport(deepest + Vector3.up * (depth - 1.5f));
            yield return null;
            yield return null;

            float startY = E2EHarness.Player.transform.position.y;
            yield return E2EHarness.PressKey(Key.LeftCtrl);

            float t = 0f;
            while (t < 1.5f) { t += Time.deltaTime; yield return null; }
            yield return E2EHarness.ReleaseKey(Key.LeftCtrl);

            float sank = startY - E2EHarness.Player.transform.position.y;
            E2EHarness.Log($"  Ctrl 1.5초에 {sank:F1}m 내려갔다");
            E2EHarness.Assert(sank > 0.5f, "Ctrl로 물속에서 가라앉는다");

            // 하강 속도가 남은 채로 재면 Shift 탓으로 보인다. 가라앉는 힘이
            // 부력으로 수렴할 때까지 기다린 뒤에 잰다.
            t = 0f;
            while (t < 2.0f) { t += Time.deltaTime; yield return null; }

            // Shift는 하강이 아니라 달리기여야 한다
            float beforeY = E2EHarness.Player.transform.position.y;
            yield return E2EHarness.PressKey(Key.LeftShift);

            t = 0f;
            while (t < 1.0f) { t += Time.deltaTime; yield return null; }
            yield return E2EHarness.ReleaseKey(Key.LeftShift);

            float rose = E2EHarness.Player.transform.position.y - beforeY;
            E2EHarness.Log($"  Shift 1.0초 동안 수직 변화 {rose:+0.0;-0.0}m (양수면 떠오름)");
            E2EHarness.Assert(rose > -0.1f, "Shift는 더 이상 하강이 아니다");

            // 그리고 실제로 빨라져야 한다. 앞으로 가면서 재 본다.
            // 물속에서는 보는 방향으로 나아가므로 수평으로 맞춰 놓고 잰다 —
            // 위를 본 채로 재면 수평 속도가 줄어 두 번의 조건이 달라진다.
            float normal = 0f, sprint = 0f;
            yield return MeasureSwimSpeed(false, r => normal = r);
            yield return MeasureSwimSpeed(true, r => sprint = r);

            E2EHarness.Log($"  헤엄 속도 보통 {normal:F1}m/s → Shift {sprint:F1}m/s");
            E2EHarness.Assert(sprint > normal * 1.15f, "물속에서도 Shift로 빨라진다");
        }

        /// <summary>앞으로 1초 헤엄쳐 수평 속도를 잰다.</summary>
        static IEnumerator MeasureSwimSpeed(bool sprinting, System.Action<float> result)
        {
            // 수평으로 겨눈다. 두 측정의 조건이 같아야 비교가 된다.
            var here = E2EHarness.Player.transform.position;
            E2EHarness.LookAt(here + E2EHarness.Player.transform.forward * 10f);
            yield return null;

            if (sprinting) yield return E2EHarness.PressKey(Key.LeftShift);
            yield return E2EHarness.PressKey(Key.W);

            // 가속이 붙을 시간을 준다
            float warm = 0f;
            while (warm < 0.3f) { warm += Time.deltaTime; yield return null; }

            var from = E2EHarness.Player.transform.position;
            float t = 0f;
            while (t < 1.0f) { t += Time.deltaTime; yield return null; }
            var to = E2EHarness.Player.transform.position;

            yield return E2EHarness.ReleaseKey(Key.W);
            if (sprinting) yield return E2EHarness.ReleaseKey(Key.LeftShift);

            var flat = new Vector3(to.x - from.x, 0f, to.z - from.z);
            result(flat.magnitude / Mathf.Max(0.01f, t));
        }

        /// <summary>
        /// 좌클릭을 꾹 누르면 쿨타임마다 다시 휘두르는지 본다.
        ///
        /// 광맥 하나에 여러 번 때려야 하는데 매번 클릭을 요구하면
        /// 손가락만 바쁘다. 쿨타임은 그대로라 연타로 빨라지지는 않는다.
        /// </summary>
        static IEnumerator HoldToSwing()
        {
            var user = E2EHarness.Player.GetComponent<Survive.Player.PlayerToolUser>();
            if (user == null || !user.EquipFirst("pickaxe"))
            {
                E2EHarness.Log("  곡괭이가 없어 홀드 휘두르기를 건너뛴다");
                yield break;
            }
            yield return null;

            var tool = E2EHarness.Player.ToolHolder?.EquippedTool;
            E2EHarness.Assert(tool != null, "곡괭이를 들었다");
            if (tool == null) yield break;

            var melee = Object.FindFirstObjectByType<Survive.Combat.MeleeSwing>(
                FindObjectsInactive.Exclude);
            E2EHarness.Assert(melee != null, "근접 공격 컴포넌트가 있다");
            if (melee == null) yield break;

            int before = melee.SwingCount;
            float hold = tool.attackCooldown * 3.5f;

            yield return E2EHarness.HoldAttack(hold);
            yield return null;

            int swings = melee.SwingCount - before;
            E2EHarness.Log($"  {hold:F1}초 꾹 누르는 동안 {swings}번 휘둘렀다 " +
                           $"(쿨타임 {tool.attackCooldown:F2}초)");
            E2EHarness.Assert(swings >= 3, "꾹 누르면 쿨타임마다 다시 휘두른다");
        }

        /// <summary>
        /// 가장 깊은 곳에 빠져도 스스로 올라올 수 있는지 본다.
        ///
        /// 물을 한 장으로 합치면서 지형이 파인 곳은 전부 잠겼다. 그 결과
        /// 깊이가 20m를 넘는 자리가 생겼는데, 산소가 먼저 떨어지면
        /// 그 자리는 함정이지 물이 아니다. 스페이스로 올라오는 경로를 실제로 탄다.
        /// </summary>
        static IEnumerator CanSurface(PlayerSwimming swim, Vector3 deepest, float depth, float surface)
        {
            var vitals = Object.FindFirstObjectByType<Survive.Vitals.PlayerVitals>(
                FindObjectsInactive.Exclude);
            if (vitals == null) yield break;

            E2EHarness.Teleport(deepest + Vector3.up * 0.1f);
            yield return null;
            yield return null;

            float startOxygen = vitals.Oxygen.Current;

            // 스페이스를 누르고 있으면 올라온다. 툴팁이 약속하는 그대로다.
            yield return E2EHarness.PressKey(Key.Space);

            float t = 0f;
            while (t < 30f && swim.IsHeadSubmerged && vitals.Oxygen.Current > 0f)
            {
                t += Time.deltaTime;
                yield return null;
            }

            yield return E2EHarness.ReleaseKey(Key.Space);

            E2EHarness.Log($"  수심 {depth:F0}m, 스페이스 홀드 부상 {t:F1}초, " +
                           $"산소 {startOxygen:F0} → {vitals.Oxygen.Current:F0}");

            E2EHarness.Assert(!swim.IsHeadSubmerged,
                              $"가장 깊은 곳({depth:F0}m)에서도 스페이스로 올라온다");
            E2EHarness.Assert(vitals.Oxygen.Current > 0f,
                              "올라오는 동안 산소가 바닥나지 않는다");

            // 아무것도 누르지 않았을 때도 재 둔다. 당황해서 손을 놓은 플레이어가
            // 죽는지 아닌지는 밸런스 판단이라 여기서는 수치만 남긴다.
            float budget = vitals.Oxygen.Max / Mathf.Max(0.01f, drainPerSecondGuess);
            E2EHarness.Log($"  참고: 산소 {vitals.Oxygen.Max:F0} / 잠수 소모 " +
                           $"{drainPerSecondGuess:F0}per초 = {budget:F0}초. " +
                           $"부력(0.9m/s)만으로는 약 {budget * 0.9f:F0}m까지다");
        }
    }
}
