using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Survive.Building;
using Survive.Crafting;
using Survive.Items;
using Survive.UI;

namespace Survive.Testing
{
    /// <summary>
    /// 제작에 시간이 걸린다 (백로그 34번).
    ///
    /// 즉시 제작이 사라지면 게임의 리듬이 통째로 바뀐다. 여기서 보는 것은
    /// 그 규칙이 실제로 세계에 붙었는가다 —
    /// <b>손으로 건 것은 걸어 다니는 동안에도 흐르고</b>,
    /// <b>제작대에 건 것은 사람이 떠나도 돌아가며</b>,
    /// <b>화톳불에 건 것은 불이 꺼지면 멈춘다.</b>
    /// 그리고 무엇을 물리든 완성되지 않은 재료는 전부 돌아온다.
    ///
    /// 규칙 자체는 <c>CraftQueueTests</c>가 Unity 없이 본다.
    /// 여기서 보는 것은 배선 — UI 클릭이 정말 줄로 가고, 스테이션이 정말 자기 줄을
    /// 돌리고, 회수가 정말 손에 들어오는가.
    /// </summary>
    public static class E2ETimedCrafting
    {
        static Inventory Inv => E2EHarness.Player.Inventory.Inventory;

        static CraftingUI UI =>
            Object.FindAnyObjectByType<CraftingUI>(FindObjectsInactive.Include);

        public static IEnumerator FullRun()
        {
            yield return Prepare();

            yield return 손_제작은_시간이_걸린다();
            yield return 수량을_걸면_하나씩_나온다();
            yield return 취소하면_재료가_돌아온다();
            yield return 소지품을_닫아도_진행한다();
            yield return 제작대는_사람이_없어도_돈다();
            yield return 화톳불은_불이_꺼지면_멈춘다();

            E2EHarness.Log("=== 시간 제작 완주 ===");
        }

        // ── 준비 ────────────────────────────────────────────────

        static IEnumerator Prepare()
        {
            var dir = Object.FindAnyObjectByType<Survive.Progression.ChapterDirector>(
                FindObjectsInactive.Exclude);
            yield return E2EHarness.WaitUntil(() => dir != null && dir.Current != null,
                                              "챕터가 시작된다", 8f);

            yield return E2EHarness.WaitUntil(() => UI != null, "제작 UI가 있다", 8f);
            yield return E2EHarness.WaitUntil(() => HandCraftingService.Instance != null,
                                              "손 제작 서비스가 스스로 서 있다", 8f);

            // 앞선 검사가 남긴 줄이 있으면 지운다. 줄은 몸에 달려 있어 시나리오를
            // 건너뛰지 않는다 — 여기서 비워야 개수 비교가 성립한다.
            while (!HandCraftingService.Instance.Queue.IsEmpty)
                HandCraftingService.Instance.Cancel(0);

            채운다();
            yield return null;
        }

        static void 채운다()
        {
            var db = E2EHarness.Player.Inventory.Database;
            if (db == null) return;

            // 목재가 빠지면 6번 시나리오에서 불을 되살리지 못한다 —
            // 화톳불에 들어가는 것은 이제 목재뿐이다(백로그 35).
            foreach (var id in new[] { "scrap", "alien_alloy", "fern_fiber", "machine_part",
                                       Survive.Harvesting.MushroomLumberRule.WoodItemId })
            {
                var item = db.GetById(id);
                if (item != null && Inv.CountOf(id) < 40) Inv.TryAdd(item, 40);
            }
        }

        /// <summary>손에서 만들 수 있는 것 중 재료가 가장 싼 것. id를 박아 두지 않는다.</summary>
        static RecipeSO 손레시피()
        {
            var book = Resources.FindObjectsOfTypeAll<RecipeBookSO>().FirstOrDefault();
            var pool = book != null && book.recipes != null
                     ? book.recipes
                     : Resources.FindObjectsOfTypeAll<RecipeSO>();

            return pool.Where(r => r != null &&
                                   r.requiredStation == StationType.None &&
                                   r.result?.item != null &&
                                   r.craftSeconds > 0.5f &&
                                   r.ingredients != null && r.ingredients.Length > 0 &&
                                   r.ingredients.All(i => i?.item != null &&
                                                          Inv.CountOf(i.item.id) >= i.count * 2))
                       .OrderBy(r => r.ingredients.Sum(i => i.count))
                       .FirstOrDefault();
        }

        // ── 1. 손 제작 ──────────────────────────────────────────

        static IEnumerator 손_제작은_시간이_걸린다()
        {
            E2EHarness.Log("— 손 제작은 시간이 걸린다 —");

            var r = 손레시피();
            E2EHarness.Assert(r != null, "손으로 만들 수 있는 레시피가 있다");
            if (r == null) yield break;
            비운다(r);

            var queue = HandCraftingService.Instance.Queue;
            int 결과전 = Inv.CountOf(r.result.item.id);
            int 재료전 = 재료수(r);

            yield return 행을_누른다(StationType.None, r, 1);

            E2EHarness.AssertEqual(queue.Count, 1, $"{r.id}가 줄에 섰다");
            E2EHarness.AssertEqual(Inv.CountOf(r.result.item.id), 결과전,
                                   "누르자마자 물건이 나오지는 않는다");
            E2EHarness.Assert(재료수(r) < 재료전, "재료는 걸 때 이미 빠졌다");
            E2EHarness.Log($"  {r.id} 소요 {r.craftSeconds:F0}초");

            // 절반쯤에서 게이지가 실제로 차 있는지 본다.
            yield return 기다린다(r.craftSeconds * 0.5f);
            float 진행 = queue.Active != null ? queue.Active.UnitProgress : -1f;
            E2EHarness.Assert(진행 > 0.2f && 진행 < 0.9f,
                              $"절반쯤 진행돼 있다 ({진행:P0})");

            yield return E2EHarness.WaitUntil(
                () => Inv.CountOf(r.result.item.id) > 결과전,
                $"기다리니 {r.id}가 나왔다", r.craftSeconds + 4f);

            E2EHarness.Assert(queue.IsEmpty, "다 만든 항목은 줄에서 빠진다");
        }

        // ── 2. 수량 제작 ────────────────────────────────────────

        static IEnumerator 수량을_걸면_하나씩_나온다()
        {
            E2EHarness.Log("— 세 개를 걸면 하나씩 나온다 —");
            채운다();

            var r = 손레시피();
            if (r == null) yield break;
            비운다(r);

            var queue = HandCraftingService.Instance.Queue;
            int 결과전 = Inv.CountOf(r.result.item.id);

            yield return 행을_누른다(StationType.None, r, 3);
            E2EHarness.AssertEqual(queue.Count, 1, "세 개는 항목 하나로 묶인다");
            E2EHarness.AssertEqual(queue.Active.Remaining, 3, "항목이 세 개를 들고 있다");

            yield return E2EHarness.WaitUntil(
                () => Inv.CountOf(r.result.item.id) >= 결과전 + 1,
                "첫 개가 나온다", r.craftSeconds + 4f);
            E2EHarness.Assert(Inv.CountOf(r.result.item.id) < 결과전 + 3,
                              "한꺼번에 셋이 쏟아지지는 않는다");

            yield return E2EHarness.WaitUntil(
                () => Inv.CountOf(r.result.item.id) >= 결과전 + 3,
                "셋이 차례로 다 나온다", r.craftSeconds * 3f + 6f);
            E2EHarness.Assert(queue.IsEmpty, "다 만들면 줄이 빈다");
        }

        // ── 3. 취소 환급 ────────────────────────────────────────

        static IEnumerator 취소하면_재료가_돌아온다()
        {
            E2EHarness.Log("— 물리면 재료가 돌아온다 —");
            채운다();

            var r = 손레시피();
            if (r == null) yield break;
            비운다(r);

            int 재료전 = 재료수(r);
            yield return 행을_누른다(StationType.None, r, 2);

            var queue = HandCraftingService.Instance.Queue;
            E2EHarness.AssertEqual(queue.Count, 1, "두 개짜리가 걸렸다");
            E2EHarness.Assert(재료수(r) < 재료전, "재료가 빠졌다");

            // 조금 진행시킨 뒤 물린다. 손도 안 댄 것을 물리는 것과 다르다.
            yield return 기다린다(r.craftSeconds * 0.4f);

            var strip = Object.FindAnyObjectByType<CraftQueueView>(FindObjectsInactive.Include);
            E2EHarness.Assert(strip != null, "대기열 띠가 화면에 있다");

            var 칸 = strip != null
                ? strip.GetComponentsInChildren<Button>(true)
                       .FirstOrDefault(b => b.gameObject.name == "QueueSlot_0")
                : null;
            E2EHarness.Assert(칸 != null, "대기열 첫 칸을 누를 수 있다");

            if (칸 != null)
            {
                ExecuteEvents.Execute(칸.gameObject, new PointerEventData(EventSystem.current),
                                      ExecuteEvents.pointerClickHandler);
                yield return null;
                yield return null;
            }

            E2EHarness.Assert(queue.IsEmpty, "칸을 눌러 항목을 물렸다");
            E2EHarness.AssertEqual(재료수(r), 재료전,
                                   "완성되지 않은 두 개분이 전부 돌아왔다");
        }

        // ── 4. 화면을 닫아도 흐른다 ─────────────────────────────

        static IEnumerator 소지품을_닫아도_진행한다()
        {
            E2EHarness.Log("— 창을 닫아도 줄은 흐른다 —");
            채운다();

            var r = 손레시피();
            if (r == null) yield break;
            비운다(r);

            int 결과전 = Inv.CountOf(r.result.item.id);
            yield return 행을_누른다(StationType.None, r, 1);

            UI.Close();
            yield return null;
            E2EHarness.Assert(!UI.IsOpen, "제작 화면을 닫았다");

            // 닫은 채로 걸어 다닌다. 제작은 화면의 것이 아니라 세계의 것이다.
            yield return E2EHarness.HoldKey(Key.A, Mathf.Min(1.2f, r.craftSeconds * 0.5f));

            yield return E2EHarness.WaitUntil(
                () => Inv.CountOf(r.result.item.id) > 결과전,
                "화면을 닫고 돌아다니는 동안 완성됐다", r.craftSeconds + 5f);
        }

        // ── 5. 제작대는 사람이 없어도 돈다 ──────────────────────

        static IEnumerator 제작대는_사람이_없어도_돈다()
        {
            E2EHarness.Log("— 제작대에 걸어 두고 떠난다 —");
            채운다();

            var bench = 세운다<CraftingBench>("bench");
            E2EHarness.Assert(bench != null, "제작대를 세웠다");
            if (bench == null) yield break;

            var r = 손레시피();
            if (r == null) { Object.Destroy(bench.gameObject); yield break; }
            비운다(r);

            int 결과전 = Inv.CountOf(r.result.item.id);
            yield return 행을_누른다(bench, r, 2);

            E2EHarness.AssertEqual(bench.Work.Queue.Count, 1, "작업이 제작대에 걸렸다");
            E2EHarness.Log("  손 제작 줄: " + 줄설명(HandCraftingService.Instance.Queue));
            E2EHarness.Assert(HandCraftingService.Instance.Queue.IsEmpty,
                              "손 제작 줄에는 아무것도 없다 — 작업은 제작대의 것이다");

            UI.Close();
            yield return null;

            // 떠난다. 자리에 없는 동안에도 돌아야 한다.
            var 멀리 = bench.transform.position + Vector3.right * 14f + Vector3.up * 1f;
            E2EHarness.Teleport(멀리);
            yield return null;

            yield return E2EHarness.WaitUntil(
                () => bench.Work.Queue.IsEmpty,
                "떠나 있는 동안 제작대가 다 만들었다", r.craftSeconds * 2f + 6f);

            E2EHarness.AssertEqual(Inv.CountOf(r.result.item.id), 결과전,
                                   "만들어진 것이 저절로 손에 들어오지는 않는다");
            E2EHarness.Assert(bench.Work.HasOutput, "제작대가 들고 기다린다");
            E2EHarness.AssertEqual(bench.Work.OutputCount, 2, "두 개가 기다린다");
            E2EHarness.Log("  프롬프트: " + bench.InteractionPrompt);
            E2EHarness.Assert(bench.InteractionPrompt.Contains("회수"),
                              "돌아오라고 말한다");

            // 돌아와 가져간다. 조준 절차는 E2EBaseBuilding이 이미 보므로
            // 여기서는 자리로 돌아왔다는 사실만 세우고 상호작용을 건다.
            E2EHarness.Teleport(bench.transform.position - Vector3.right * 1.6f + Vector3.up * 1f);
            yield return null;
            bench.Interact(E2EHarness.Player);
            yield return null;

            E2EHarness.AssertEqual(Inv.CountOf(r.result.item.id), 결과전 + 2,
                                   "돌아와서 회수했다");
            E2EHarness.Assert(!bench.Work.HasOutput, "회수함이 비었다");

            Object.Destroy(bench.gameObject);
            yield return null;
        }

        // ── 6. 화톳불은 불이 꺼지면 멈춘다 ──────────────────────

        static IEnumerator 화톳불은_불이_꺼지면_멈춘다()
        {
            E2EHarness.Log("— 불이 꺼지면 에너지 추출도 멈춘다 —");
            채운다();

            var fire = 세운다<Campfire>("campfire");
            E2EHarness.Assert(fire != null, "화톳불을 세웠다");
            if (fire == null) yield break;

            E2EHarness.Assert(fire.IsBurning, "세우자마자 타고 있다");

            var recipe = Resources.FindObjectsOfTypeAll<RecipeSO>()
                                  .FirstOrDefault(x => x != null &&
                                                       x.requiredStation == StationType.Campfire);
            E2EHarness.Assert(recipe != null, "화톳불 가공 레시피가 있다");
            if (recipe == null) { Object.Destroy(fire.gameObject); yield break; }

            E2EHarness.Log($"  {recipe.id}: {재료설명(recipe)} → {recipe.result.item.id} " +
                           $"({recipe.craftSeconds:F0}초)");

            int 셀전 = Inv.CountOf(recipe.result.item.id);
            int 스크랩전 = Inv.CountOf("scrap");

            yield return 행을_누른다(fire, recipe, 1);
            E2EHarness.AssertEqual(fire.Work.Queue.Count, 1, "에너지 추출이 불에 걸렸다");
            E2EHarness.Assert(Inv.CountOf("scrap") < 스크랩전, "스크랩이 들어갔다");

            UI.Close();
            yield return null;

            // 조금 뽑아내다가 불을 끈다.
            yield return 기다린다(1.5f);
            float 멈추기전 = fire.Work.Queue.Active.Elapsed;
            E2EHarness.Assert(멈추기전 > 0.5f, $"불이 타는 동안 진행했다 ({멈추기전:F1}초)");

            연료를_바닥낸다(fire);
            yield return E2EHarness.WaitUntil(() => !fire.IsBurning, "불이 꺼졌다", 3f);
            E2EHarness.Assert(fire.PausedReason != null, "멈춘 이유를 말한다: " + fire.PausedReason);

            float 꺼진직후 = fire.Work.Queue.Active.Elapsed;
            yield return 기다린다(1.5f);
            E2EHarness.AssertEqual(fire.Work.Queue.Active.Elapsed, 꺼진직후,
                                   "불이 꺼진 동안에는 진행이 멈춘다");

            // 다시 지피면 이어서 뽑아낸다. 처음부터가 아니다.
            // 넣는 것은 목재다 — 스크랩은 이 불에서 타는 쪽이 아니라 짜이는 쪽이다.
            E2EHarness.Assert(fire.Refuel(Inv), "목재를 넣어 불을 되살렸다");
            yield return null;
            E2EHarness.Assert(fire.IsBurning, "다시 지폈다");

            yield return E2EHarness.WaitUntil(
                () => fire.Work.Queue.Active == null || fire.Work.Queue.Active.Elapsed > 꺼진직후 + 0.4f,
                "불을 살리자 진행이 이어진다", 4f);

            // 시간을 접어 완성까지 본다. 30초를 실시간으로 기다릴 이유가 없다.
            yield return 시간을_접고_기다린다(
                () => fire.Work.HasOutput,
                "불 곁에서 스크랩의 에너지가 셀로 옮겨졌다",
                recipe.craftSeconds + 6f);

            E2EHarness.AssertEqual(Inv.CountOf(recipe.result.item.id), 셀전,
                                   "뽑아낸 것은 아직 불 곁에 있다");

            fire.Interact(E2EHarness.Player);
            yield return null;
            E2EHarness.AssertEqual(Inv.CountOf(recipe.result.item.id), 셀전 + 1,
                                   "회수하니 손에 들어왔다");

            Object.Destroy(fire.gameObject);
            yield return null;
        }

        /// <summary>
        /// 연료를 한 프레임분만 남긴다. 90초를 실시간으로 태울 수는 없고,
        /// 불을 끄는 공개 창구는 게임에 필요 없는 것이라 만들지 않았다.
        /// 남은 한 방울은 Campfire의 Update가 정상 경로로 소진시킨다 —
        /// 값을 0으로 밀어 넣고 끝내면 소등 처리(ApplyLight)를 건너뛴다.
        /// </summary>
        static void 연료를_바닥낸다(Campfire fire)
        {
            var field = typeof(Campfire).GetField("_fuel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            E2EHarness.Assert(field != null, "화톳불의 연료를 찾았다");
            field?.SetValue(fire, 0.01f);
        }

        // ── 공통 동작 ───────────────────────────────────────────

        /// <summary>앞선 검사가 만들어 둔 결과물을 치운다. 도구는 한 칸에 하나라
        /// 그냥 두면 소지품이 차서 대기열이 "완성 대기"로 멈춰 버린다.</summary>
        static void 비운다(RecipeSO r)
        {
            var id = r?.result?.item?.id;
            if (string.IsNullOrEmpty(id)) return;
            int n = Inv.CountOf(id);
            if (n > 0) Inv.TryRemove(id, n);
        }

        static string 줄설명(CraftQueue q) =>
            q.Count == 0
                ? "(비어 있다)"
                : string.Join(", ", q.Jobs.Select(
                    j => $"{j.Recipe.id}x{j.Remaining}@{j.Elapsed:F1}{(j.Stalled ? "(막힘)" : "")}"));

        static int 재료수(RecipeSO r) =>
            r.ingredients.Sum(i => i?.item != null ? Inv.CountOf(i.item.id) : 0);

        static string 재료설명(RecipeSO r) =>
            string.Join("+", r.ingredients.Select(i => $"{i.item.id} {i.count}"));

        static IEnumerator 기다린다(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.deltaTime; yield return null; }
        }

        /// <summary>
        /// 시간을 8배로 접어 기다린다. 에너지 추출은 분 단위로 설계된 것이라
        /// 실시간으로 기다리면 검사 하나가 시나리오 전체보다 길어진다.
        /// </summary>
        static IEnumerator 시간을_접고_기다린다(System.Func<bool> 조건, string what, float 실시간상한)
        {
            float 이전 = Time.timeScale;
            Time.timeScale = 8f;
            yield return E2EHarness.WaitUntil(조건, what, 실시간상한);
            Time.timeScale = 이전;
        }

        static T 세운다<T>(string buildableId) where T : Component
        {
            var catalog = Resources.FindObjectsOfTypeAll<BuildCatalogSO>().FirstOrDefault();
            var def = catalog?.entries?.FirstOrDefault(b => b != null && b.id == buildableId);
            if (def?.prefab == null) return null;

            // 배치 절차는 E2EBaseBuilding이 이미 본다. 여기서 보려는 것은 제작이므로
            // 지형 조준에 시나리오를 걸지 않고 곁에 바로 세운다.
            var pos = E2EHarness.Player.transform.position +
                      E2EHarness.Player.transform.forward * 2.2f;
            var go = Object.Instantiate(def.prefab, pos, Quaternion.identity);
            return go.GetComponentInChildren<T>(true);
        }

        /// <summary>제작 화면을 열고 수량을 맞춘 뒤 행을 누른다 — 실제 클릭 경로로.</summary>
        static IEnumerator 행을_누른다(StationType station, RecipeSO r, int count)
        {
            UI.Open(station);
            yield return 행_클릭(r, count);
        }

        static IEnumerator 행을_누른다(ICraftStation station, RecipeSO r, int count)
        {
            UI.Open(station);
            yield return 행_클릭(r, count);
        }

        static IEnumerator 행_클릭(RecipeSO r, int count)
        {
            yield return null;
            yield return null;

            var row = UI.GetComponentsInChildren<Button>(true)
                        .FirstOrDefault(b => b.gameObject.name == "Row_" + r.id);
            E2EHarness.Assert(row != null, $"레시피 행이 있다: {r.id}");
            if (row == null) yield break;

            // 수량은 버튼으로 맞춘다. 값을 코드로 밀어 넣으면 버튼이 죽어 있어도
            // 검사가 지나간다. 화면은 레시피마다 마지막 수량을 기억하므로
            // 먼저 −로 1까지 내린 다음 +로 올린다.
            var minus = 자식버튼(row, "Minus");
            var plus = 자식버튼(row, "Plus");
            E2EHarness.Assert(minus != null && plus != null, "수량 버튼이 있다");

            for (int i = 0; i < CraftQueueService.MaxBatch && minus != null && minus.interactable; i++)
            {
                누른다(minus);
                yield return null;
            }
            E2EHarness.Assert(minus == null || !minus.interactable, "수량이 1까지 내려왔다");

            for (int i = 1; i < count; i++)
            {
                E2EHarness.Assert(plus != null && plus.interactable,
                                  $"수량을 {i + 1}로 올릴 수 있다");
                if (plus == null) break;
                누른다(plus);
                yield return null;
            }

            E2EHarness.Assert(row.interactable, $"{r.id}를 걸 수 있다");
            누른다(row);
            yield return null;
            yield return null;
        }

        static Button 자식버튼(Button row, string name) =>
            row.GetComponentsInChildren<Button>(true)
               .FirstOrDefault(b => b.gameObject.name == name);

        static void 누른다(Button b) =>
            ExecuteEvents.Execute(b.gameObject, new PointerEventData(EventSystem.current),
                                  ExecuteEvents.pointerClickHandler);
    }
}
