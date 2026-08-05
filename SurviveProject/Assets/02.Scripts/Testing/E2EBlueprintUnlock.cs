using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Survive.Building;
using Survive.Core;
using Survive.Crafting;
using Survive.Items;
using Survive.Narrative;
using Survive.Progression;
using Survive.UI;

namespace Survive.Testing
{
    /// <summary>
    /// 청사진 해금 — 몰라서 못 만드는 것이 실제로 못 만들어지는가.
    ///
    /// 순수 규칙은 EditMode(UnlockLedgerTests·FieldDiscoveryTests)에서 이미 지킨다.
    /// 여기서 보는 것은 그 규칙이 <b>실제 화면과 실제 소지품</b>에 닿는지다:
    /// 잠긴 줄이 눌리지 않는가, 첫 습득이 진짜로 문을 여는가, AI가 그때 말하는가,
    /// 그리고 저장을 왕복한 뒤에도 알던 것이 남는가.
    /// </summary>
    public static class E2EBlueprintUnlock
    {
        const string PickaxeRecipe = "pickaxe";
        const string MechanismKey = "bp_salvaged_mechanism";
        const string ScrapworksKey = "bp_scrapworks";
        const string Slot = "blueprint_e2e";

        static Inventory Inv => E2EHarness.Player.Inventory.Inventory;
        static UnlockLedger Ledger => UnlockService.Instance.Ledger;

        public static IEnumerator FullRun()
        {
            E2EHarness.ClearLog();
            E2EHarness.Log("=== 청사진 해금 ===");

            E2EHarness.Assert(UnlockService.Instance != null, "해금 서비스가 스스로 서 있다");
            E2EHarness.Assert(UnlockService.Instance.Book != null,
                              "Resources/DiscoveryBook 매핑표를 읽었다");
            E2EHarness.Assert(BlueprintGate.Active != null,
                              "원장이 GameServices에 올라와 있다 — 없으면 모든 것이 열린 것처럼 군다");

            yield return 잠긴_채로_시작한다();
            yield return 재료가_있어도_모르면_못_만든다();
            yield return 첫_습득이_문을_연다();
            yield return 열리면_실제로_만들어진다();
            yield return 건축도_같은_문을_쓴다();
            yield return 저장을_왕복해도_알던_것이_남는다();

            E2EHarness.Log("=== 청사진 해금 통과 ===");
        }

        // ── 1. 아무것도 모르는 상태 ──────────────────────────────

        static IEnumerator 잠긴_채로_시작한다()
        {
            E2EHarness.Log("[1] 아무것도 모르는 채로 시작한다");

            Ledger.Clear();
            비운다("scrap", "machine_part");
            yield return null;

            var ui = Ui();
            ui.Open(StationType.None);
            yield return null;
            yield return null;

            // 모르는 것은 <b>목록에 실리지 않는다</b>. 회색으로 남겨 두던 시절에는
            // 그 줄이 "부품 재조립 청사진이 필요하다: 기계 부품을 손에 넣으면 알게 된다"를
            // 통째로 적어 주었다 — 아직 아무것도 못 본 사람에게 다음 수를 알려 준 셈이다.
            var row = Row(ui, PickaxeRecipe);
            E2EHarness.Assert(row == null || !row.gameObject.activeInHierarchy,
                              "곡괭이 줄이 목록에 없다");

            var shown = VisibleLabels(ui);
            E2EHarness.Assert(shown.Count > 0, "목록이 통째로 사라지지는 않았다");
            E2EHarness.Assert(!shown.Any(s => s.Contains("잠김")),
                              "어느 줄에도 자물쇠 표시가 없다");
            E2EHarness.Assert(!shown.Any(s => s.Contains("청사진")),
                              "어느 줄도 청사진을 입에 올리지 않는다");
            E2EHarness.Assert(!shown.Any(s => s.Contains("기계 부품") && s.Contains("알게 된다")),
                              "무엇을 하면 열리는지도 새어 나가지 않는다");
            E2EHarness.Log("  보이는 줄: " + string.Join(" | ", shown));

            ui.Close();
            yield return null;

            // 요구 청사진이 빈 항목은 원장이 비어 있어도 열려 있다 (하위호환의 근거).
            var campfire = Buildable("campfire");
            E2EHarness.Assert(campfire != null && campfire.requiredBlueprint == null,
                              "화톳불은 요구 청사진이 없다 — 생존의 바닥은 처음부터 열려 있다");
            E2EHarness.Assert(BlueprintGate.IsUnlocked(campfire.requiredBlueprint, Ledger),
                              "요구가 비면 원장이 비어 있어도 열려 있다");
        }

        // ── 2. 재료는 있는데 모르는 상태 ─────────────────────────

        /// <summary>
        /// 잠긴 이유가 <b>재료가 아니라 앎</b>임을 갈라내는 자리.
        /// 재료를 채워 넣고(그 순간 열린다) 원장만 도로 비워, 오직 청사진만
        /// 없는 상태를 만든다.
        /// </summary>
        static IEnumerator 재료가_있어도_모르면_못_만든다()
        {
            E2EHarness.Log("[2] 재료는 손에 있는데 만들 줄 모른다");

            var recipe = Recipe(PickaxeRecipe);
            E2EHarness.Assert(recipe != null, "곡괭이 레시피를 찾았다");
            E2EHarness.Assert(recipe.requiredBlueprint != null &&
                              recipe.requiredBlueprint.id == MechanismKey,
                              "곡괭이는 부품 재조립 청사진을 요구한다");

            채운다(recipe);
            Ledger.Clear();
            yield return null;

            E2EHarness.Assert(CraftingService.CanCraft(recipe, Inv, StationType.None),
                              "원장을 빼고 보면 재료만으로는 조건을 충족한다");
            E2EHarness.Assert(!CraftingService.CanCraft(recipe, Inv, StationType.None, Ledger),
                              "그런데 청사진이 없으니 못 만든다 — 두 잠금은 독립이다");

            var hand = HandCraftingService.Instance;
            int queued = hand.Queue.Count;
            int scrap = Inv.CountOf("scrap");

            E2EHarness.Assert(!hand.TryEnqueue(recipe, 1), "걸어 보려 해도 거절당한다");
            E2EHarness.AssertEqual(hand.Queue.Count, queued, "대기열은 그대로다");
            E2EHarness.AssertEqual(Inv.CountOf("scrap"), scrap, "실패했으니 재료도 그대로다");

            var ui = Ui();
            ui.Open(StationType.None);
            yield return null;
            yield return null;

            var row = Row(ui, PickaxeRecipe);
            E2EHarness.Assert(row == null || !row.gameObject.activeInHierarchy,
                              "재료가 가득해도 모르는 것은 목록에 뜨지 않는다");
            E2EHarness.Assert(!VisibleLabels(ui).Any(s => s.Contains("곡괭이")),
                              "이름조차 새어 나가지 않는다");

            ui.Close();
            yield return null;
        }

        // ── 3. 현장 발견 ─────────────────────────────────────────

        static IEnumerator 첫_습득이_문을_연다()
        {
            E2EHarness.Log("[3] 기계 부품을 처음 손에 넣는다");

            int spokenBefore = UnlockService.Instance.LinesSpoken;
            E2EHarness.Assert(!Ledger.IsUnlocked(MechanismKey), "아직 모른다");

            var db = E2EHarness.Player.Inventory.Database;
            var part = db.GetById("machine_part");
            E2EHarness.Assert(part != null, "기계 부품 정의가 있다");

            // 실제 습득 경로와 같은 길목(Inventory.TryAdd)을 지난다.
            Inv.TryAdd(part, 1);
            E2EHarness.Log("  [주입] machine_part +1 (줍는 동작 자체는 다른 시나리오가 지킨다)");

            E2EHarness.Assert(Ledger.IsUnlocked(MechanismKey), "그 자리에서 청사진이 열렸다");
            E2EHarness.Assert(Ledger.IsUnlocked("disc_machine_part"),
                              "발견 기록도 남아 두 번 울리지 않는다");

            yield return E2EHarness.WaitUntil(
                () => UnlockService.Instance.LinesSpoken > spokenBefore,
                "우주복 AI가 반응했다", 5f);

            E2EHarness.Log($"  AI: {UnlockService.Instance.LastLine}");

            var view = Object.FindObjectsByType<SubtitleView>(FindObjectsInactive.Include).FirstOrDefault();
            E2EHarness.Assert(view != null, "자막판이 화면에 서 있다");

            var text = view.GetComponentInChildren<TMP_Text>(true);
            E2EHarness.Assert(text != null && text.text.Contains("우주복 AI"),
                              $"자막에 화자가 적혀 있다 — \"{(text != null ? text.text : "")}\"");

            // 같은 재료를 또 주워도 다시 떠들지 않는다.
            int spokenNow = UnlockService.Instance.LinesSpoken;
            Inv.TryAdd(part, 1);
            yield return null;
            E2EHarness.AssertEqual(UnlockService.Instance.LinesSpoken, spokenNow,
                                   "두 번째 습득에는 아무 말도 하지 않는다");
        }

        // ── 4. 열린 뒤 ───────────────────────────────────────────

        static IEnumerator 열리면_실제로_만들어진다()
        {
            E2EHarness.Log("[4] 알고 나니 만들어진다");

            var recipe = Recipe(PickaxeRecipe);
            채운다(recipe);
            yield return null;

            var ui = Ui();
            ui.Open(StationType.None);
            yield return null;
            yield return null;

            var row = Row(ui, PickaxeRecipe);
            E2EHarness.Assert(row != null && row.gameObject.activeInHierarchy,
                              "없던 줄이 목록에 새로 생겼다 — 알게 되자 목록이 자랐다");
            E2EHarness.Assert(row.interactable, "그 줄을 누를 수 있다");
            E2EHarness.Assert(!Label(row).Contains("잠김"),
                              $"재료가 적힌다 — \"{Label(row)}\"");

            int before = Inv.CountOf("pickaxe");
            ExecuteEvents.Execute(row.gameObject, new PointerEventData(EventSystem.current),
                                  ExecuteEvents.pointerClickHandler);
            yield return null;
            yield return null;
            ui.Close();

            yield return E2EHarness.WaitUntil(() => Inv.CountOf("pickaxe") > before,
                                              "곡괭이가 손에 들어왔다", recipe.craftSeconds + 8f);
            E2EHarness.Assert(Inv.CountOf("pickaxe") > before,
                              "몰라서 못 만들던 것을 알고 나서 실제로 만들었다");
        }

        // ── 5. 건축 ──────────────────────────────────────────────

        static IEnumerator 건축도_같은_문을_쓴다()
        {
            E2EHarness.Log("[5] 짓는 쪽도 같은 원장을 본다");

            var placer = Object.FindObjectsByType<BuildPlacer>(FindObjectsInactive.Include).FirstOrDefault();
            E2EHarness.Assert(placer != null, "BuildPlacer가 있다");

            var fence = Buildable("fence");
            E2EHarness.Assert(fence != null && fence.requiredBlueprint != null &&
                              fence.requiredBlueprint.id == ScrapworksKey,
                              "울타리는 스크랩 재활용 청사진을 요구한다");

            Ledger.Clear();
            yield return null;

            placer.SelectById("fence");
            yield return null;
            var locked = placer.Evaluate(out _, out _);
            E2EHarness.AssertEqual(locked, PlacementResult.NotResearched,
                                   "모르면 자리를 따지기도 전에 막힌다");
            E2EHarness.Assert(PlacementCheckText.Describe(PlacementResult.NotResearched)
                                                .Contains("청사진"),
                              "안내문이 이유를 말한다");

            var menu = Object.FindObjectsByType<BuildMenuUI>(FindObjectsInactive.Include).FirstOrDefault();
            E2EHarness.Assert(menu != null, "건설 메뉴가 있다");
            menu.Open();
            yield return null;
            yield return null;

            var fenceRow = menu.GetComponentsInChildren<Button>(true)
                               .FirstOrDefault(b => b.gameObject.name == "Row_fence");
            E2EHarness.Assert(fenceRow == null || !fenceRow.gameObject.activeInHierarchy,
                              "건설 목록에서도 모르는 것은 지워진다");

            var campRow = menu.GetComponentsInChildren<Button>(true)
                              .FirstOrDefault(b => b.gameObject.name == "Row_campfire");
            E2EHarness.Assert(campRow != null && campRow.gameObject.activeInHierarchy,
                              "요구가 없는 화톳불은 원장이 비어도 그대로 있다");

            var buildLabels = VisibleLabels(menu);
            E2EHarness.Assert(buildLabels.Count > 0, "건설 목록이 통째로 비지는 않았다");
            E2EHarness.Assert(!buildLabels.Any(s => s.Contains("잠김") || s.Contains("청사진")),
                              "건설 목록도 자물쇠를 보여 주지 않는다");
            E2EHarness.Log("  건설 목록: " + string.Join(" | ", buildLabels));

            menu.Close();
            yield return null;

            // 스크랩을 쥐면 그 자리에서 줄이 <b>생겨야</b> 한다. 창을 다시 열어야
            // 보이면 실패다 — BuildMenuUI가 원장의 KeyUnlocked를 듣는 이유다.
            menu.Open();
            yield return null;
            yield return null;
            int 열기전 = VisibleLabels(menu).Count;

            // 스크랩을 손에 넣으면 그 자리에서 열린다.
            var scrap = E2EHarness.Player.Inventory.Database.GetById("scrap");
            Inv.TryAdd(scrap, 8);
            E2EHarness.Log("  [주입] scrap +8");
            E2EHarness.Assert(Ledger.IsUnlocked(ScrapworksKey), "스크랩을 쥐자 열렸다");

            yield return null;

            // 창을 닫았다 다시 열지 않았는데도 목록이 자랐다.
            var 열린뒤 = VisibleLabels(menu);
            E2EHarness.Assert(열린뒤.Count > 열기전,
                              $"열린 그 자리에서 목록이 자란다 ({열기전} -> {열린뒤.Count}줄)");
            E2EHarness.Assert(열린뒤.Any(s => s.Contains("울타리")),
                              "울타리 줄이 새로 생겼다");
            menu.Close();
            yield return null;

            var after = placer.Evaluate(out _, out _);
            E2EHarness.Assert(after != PlacementResult.NotResearched,
                              $"이제 청사진 때문에 막히지 않는다 (지금 판정: {after})");

            placer.Cancel();
            yield return null;
        }

        // ── 6. 저장 왕복 ─────────────────────────────────────────

        static IEnumerator 저장을_왕복해도_알던_것이_남는다()
        {
            E2EHarness.Log("[6] 저장하고 불러와도 알던 것은 남는다");

            E2EHarness.Assert(GameServices.TryGet<SaveCoordinator>(out var coordinator),
                              "SaveCoordinator가 있다");

            // DontDestroyOnLoad로 뒤늦게 선 서비스가 수집되지 않았을 수 있다.
            coordinator.Collect();

            // [5]에서 원장을 비웠다가 스크랩만 다시 쥐었다. 부품 쪽도 되찾아
            // 두 청사진이 함께 저장되는지를 본다.
            var part = E2EHarness.Player.Inventory.Database.GetById("machine_part");
            if (!Ledger.IsUnlocked(MechanismKey))
            {
                Inv.TryAdd(part, 1);
                E2EHarness.Log("  [주입] machine_part +1");
            }

            E2EHarness.Assert(Ledger.IsUnlocked(MechanismKey) && Ledger.IsUnlocked(ScrapworksKey),
                              "저장 전에 둘 다 열려 있다");
            int knownBefore = Ledger.Count;

            // 하던 말이 남아 있으면 아래 "다시 떠들지 않는다"와 뒤섞인다.
            yield return E2EHarness.WaitUntil(() => !UnlockService.Instance.IsSpeaking,
                                              "AI가 하던 말을 끝냈다", 20f);

            coordinator.Save(Slot);
            yield return null;
            E2EHarness.Assert(coordinator.HasSave(Slot), "저장본이 생겼다");

            Ledger.Clear();
            E2EHarness.Assert(!Ledger.IsUnlocked(MechanismKey), "기억을 지웠다");

            E2EHarness.Assert(coordinator.Load(Slot), "저장본을 불렀다");
            yield return null;

            E2EHarness.Assert(Ledger.IsUnlocked(MechanismKey), "부품 재조립이 돌아왔다");
            E2EHarness.Assert(Ledger.IsUnlocked(ScrapworksKey), "스크랩 재활용도 돌아왔다");
            E2EHarness.AssertEqual(Ledger.Count, knownBefore, "알던 것의 수가 같다");

            // 불러오기는 발견이 아니다 — 다시 떠들지 않아야 한다.
            int spoken = UnlockService.Instance.LinesSpoken;
            yield return null;
            E2EHarness.AssertEqual(UnlockService.Instance.LinesSpoken, spoken,
                                   "불러왔다고 AI가 처음부터 다시 말하지 않는다");

            coordinator.Delete(Slot);
        }

        // ── 도우미 ───────────────────────────────────────────────

        static CraftingUI Ui()
        {
            var ui = Object.FindObjectsByType<CraftingUI>(FindObjectsInactive.Include).FirstOrDefault();
            E2EHarness.Assert(ui != null, "CraftingUI가 있다");
            return ui;
        }

        static Button Row(CraftingUI ui, string id) =>
            ui.GetComponentsInChildren<Button>(true)
              .FirstOrDefault(b => b.gameObject.name == "Row_" + id);

        static string Label(Component row)
        {
            var t = row.GetComponentsInChildren<TMP_Text>(true)
                       .FirstOrDefault(x => x.gameObject.name == "Label");
            return t != null ? t.text : "";
        }

        /// <summary>
        /// 지금 실제로 켜져 있는 줄들의 글자.
        ///
        /// 잠긴 줄을 <b>지우기로</b> 한 뒤로는 "그 줄이 어떻게 생겼는가"가 아니라
        /// "화면에 무엇이 남았는가"를 봐야 한다. 꺼진 오브젝트에 남은 글자는
        /// 아무도 못 읽으므로 세지 않는다.
        /// </summary>
        static List<string> VisibleLabels(Component root) =>
            root.GetComponentsInChildren<TMP_Text>(true)
                .Where(t => t.gameObject.name == "Label" && t.gameObject.activeInHierarchy &&
                            t.transform.parent != null &&
                            t.transform.parent.name.StartsWith("Row_"))
                .Select(t => t.text)
                .ToList();

        static RecipeSO Recipe(string id) =>
            Resources.FindObjectsOfTypeAll<RecipeSO>().FirstOrDefault(r => r != null && r.id == id);

        static BuildableSO Buildable(string id) =>
            Resources.FindObjectsOfTypeAll<BuildableSO>().FirstOrDefault(b => b != null && b.id == id);

        static void 비운다(params string[] itemIds)
        {
            foreach (var id in itemIds)
            {
                int have = Inv.CountOf(id);
                if (have > 0) Inv.TryRemove(id, have);
            }
        }

        /// <summary>레시피 재료를 정확히 한 벌 채운다.</summary>
        static void 채운다(RecipeSO recipe)
        {
            var db = E2EHarness.Player.Inventory.Database;
            foreach (var need in recipe.ingredients)
            {
                if (need?.item == null) continue;
                int short_ = need.count - Inv.CountOf(need.item.id);
                if (short_ <= 0) continue;

                var item = db.GetById(need.item.id);
                if (item != null) Inv.TryAdd(item, short_);
            }
        }
    }
}
