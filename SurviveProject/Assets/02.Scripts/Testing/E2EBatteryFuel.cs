using System.Collections;
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
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// 빛의 연료는 스크랩이다 — 2026-08-07 검토 회신 ①을 <b>실제 세계에서</b> 확인한다.
    ///
    /// <b>규칙과 배선은 다르다.</b> <c>BatteryFuelGateTests</c>가 에셋을 읽어
    /// "배터리 레시피의 재료가 스크랩이다"를 못 박지만, 그것이 참이어도 실제 판에서
    /// 사슬이 끊겨 있으면 플레이어는 어둠에서 나올 길이 없다. 그래서 여기서는
    /// <b>한 판을 그대로 걷는다</b> — 맨손으로 스크랩을 캐고, 불 곁에서 셀로 뽑고,
    /// 그 셀이 손에 들어오는 순간 꺼져 있던 랜턴이 스스로 되살아나는지까지.
    ///
    /// <b>매크로늄은 손에 하나도 없이 완주한다.</b> 델타 <c>_3</c>은 배터리 재료를
    /// 매크로늄 쪽으로 옮기려 했고 회신이 그것을 취소했다. 취소가 참이라는 것은
    /// "매크로늄 0개로도 불이 켜진다"로만 보일 수 있다 — 그래서 시작할 때 매크로늄
    /// 계열을 전부 치우고, 끝날 때 여전히 0인지 다시 센다.
    ///
    /// <b>끄는 조작은 쓰지 않는다.</b> 어두운 자리를 만드는 길은 배터리를 다 쓰는
    /// 것 하나뿐이고(<c>E2EHarness.DarkenLantern</c>), 되살리는 길은 셀 하나다.
    /// 랜턴의 점등 규칙 자체는 <see cref="E2ELantern"/>이 따로 본다 — 여기서 재는
    /// 것은 <b>연료가 어디서 오는가</b>이지 랜턴의 동작이 아니다.
    /// </summary>
    public static class E2EBatteryFuel
    {
        /// <summary>이 시나리오가 세계에 없어야 한다고 보는 것들. 접두사로 판정한다.</summary>
        const string 매크로늄접두 = "macronium";

        static PlayerInventory PI => E2EHarness.Player.Inventory;
        static Inventory Inv => PI.Inventory;
        static CraftingUI UI => Object.FindAnyObjectByType<CraftingUI>(FindObjectsInactive.Include);

        static LanternController _lantern;
        static RecipeSO _recipe;
        static Campfire _fire;
        static int _스크랩수;

        public static IEnumerator FullRun()
        {
            yield return 준비();
            yield return 무대를_비운다();
            yield return 스크랩을_실제로_캔다();
            yield return 어둠에서_시작한다();
            yield return 불_곁에서_스크랩이_셀이_된다();
            yield return 어둠에서_셀_하나가_빛이_된다();
            yield return 실측();
            yield return 되돌린다();

            E2EHarness.Log("=== 스크랩이 빛이 되는 사슬 완주 ===");
        }

        // ── 0. 준비 ──────────────────────────────────────────────

        static IEnumerator 준비()
        {
            _lantern = E2EHarness.Lantern;
            E2EHarness.Assert(_lantern != null, "랜턴 장치가 씬에 있다");

            var book = Resources.FindObjectsOfTypeAll<RecipeBookSO>().FirstOrDefault();
            var pool = book?.recipes != null ? book.recipes : Resources.FindObjectsOfTypeAll<RecipeSO>();
            _recipe = pool.FirstOrDefault(r => r != null && r.id == LanternController.BatteryCellId);
            E2EHarness.Assert(_recipe != null, "배터리 레시피가 있다");
            if (_recipe == null) yield break;

            // 재료가 무엇인지를 <b>세계에서</b> 다시 읽는다. 에셋 검사와 같은 말을
            // 두 번 하는 것이 아니다 — 재생 중에 실제로 로드된 그 에셋이 스크랩을
            // 가리키는지 보는 것이고, 아래 사슬 전체의 전제가 이 한 줄이다.
            var 재료 = _recipe.ingredients?.FirstOrDefault(i => i?.item != null);
            E2EHarness.Assert(재료 != null, "배터리 레시피에 재료가 있다");
            E2EHarness.AssertEqual(재료.item.id, "scrap", "배터리가 먹는 재료");
            E2EHarness.AssertEqual(_recipe.ingredients.Length, 1, "배터리가 먹는 재료 가짓수");
            E2EHarness.AssertEqual(_recipe.requiredStation, StationType.Campfire,
                                   "배터리를 뽑는 자리 (열이 든다)");
            _스크랩수 = 재료.count;
            E2EHarness.Log($"  {_recipe.id}: scrap x{_스크랩수} → {_recipe.result.item.id} x{_recipe.result.count} " +
                           $"({_recipe.craftSeconds:F0}초, 화톳불)");

            // 랜턴을 지니고 있어야 티어가 서고, 티어가 서야 배터리가 시계로 돈다.
            if (Inv.CountOf(LanternRule.ItemId) == 0)
            {
                var 정의 = PI.Database != null ? PI.Database.GetById(LanternRule.ItemId) : null;
                E2EHarness.Assert(정의 != null, "랜턴 아이템 정의를 찾았다");
                Inv.TryAdd(정의, 1);
            }
            yield return null;
            yield return E2EHarness.WaitUntil(() => _lantern.Tier > 0, "랜턴을 지니고 있다", 3f);
        }

        // ── 1. 무대를 비운다 ─────────────────────────────────────

        /// <summary>
        /// 재려는 것은 <b>스크랩에서 나온 빛</b>뿐이다. 그래서 셋을 치운다 —
        /// 시작 지점을 덮고 있는 발광 버섯 군락(그대로 두면 "어두워졌다"를 어디서도
        /// 확인할 수 없다), 앞 시나리오가 남긴 배터리 셀, 그리고 <b>매크로늄 계열
        /// 전부</b>(이 사슬이 그것 없이 도는지가 이 라운드의 물음이다).
        /// </summary>
        static IEnumerator 무대를_비운다()
        {
            int 끈광원 = E2EHarness.MuteAmbientLitZones();
            E2EHarness.Log($"  무대 정리: 주변 광원 {끈광원}곳을 밝은 구역에서 뺐다");

            int 셀 = Inv.CountOf(LanternController.BatteryCellId);
            if (셀 > 0) Inv.TryRemove(LanternController.BatteryCellId, 셀);

            int 치운매크로늄 = 매크로늄을_치운다();
            E2EHarness.Log($"  앞 판이 남긴 셀 {셀}개, 매크로늄 계열 {치운매크로늄}개를 치웠다");
            E2EHarness.AssertEqual(매크로늄수(), 0, "손에 남은 매크로늄 계열");
            yield return null;
        }

        static int 매크로늄을_치운다()
        {
            int 치움 = 0;
            foreach (var id in 매크로늄ids())
            {
                int n = Inv.CountOf(id);
                if (n > 0 && Inv.TryRemove(id, n)) 치움 += n;
            }
            return 치움;
        }

        static string[] 매크로늄ids() =>
            PI.Database == null
                ? new string[0]
                : PI.Database.items.Where(i => i != null && i.id != null &&
                                               i.id.StartsWith(매크로늄접두))
                                   .Select(i => i.id).ToArray();

        static int 매크로늄수() => 매크로늄ids().Sum(Inv.CountOf);

        // ── 2. 스크랩을 실제로 캔다 ──────────────────────────────

        /// <summary>
        /// <b>맨손으로</b> 캔다. 이 사슬의 첫 칸이 도구를 요구하면 "떨어진 뒤에
        /// 되살아날 길"이 도구를 잃지 않았을 때만 열리게 된다 — 스크랩이 생태계
        /// 전체의 연료라는 말은 곧 <b>언제든 다시 모을 수 있다</b>는 뜻이다.
        ///
        /// 실채집은 <b>한 번</b>이면 경로가 살아 있음을 증명한다. 모자란 분량은
        /// 주입한다(<c>E2EChapter1</c>과 같은 규율) — 같은 홀드를 스무 번 반복해도
        /// 새로 검증되는 것은 없고 실행 시간만 늘어난다.
        /// </summary>
        static IEnumerator 스크랩을_실제로_캔다()
        {
            E2EHarness.Log("— 맨손으로 스크랩을 캔다 —");

            int 시작 = Inv.CountOf("scrap");
            bool 캤다 = false;

            for (int 회 = 0; 회 < 3 && !캤다; 회++)
            {
                var node = Object.FindObjectsByType<HarvestNode>()
                    .FirstOrDefault(h => !h.IsDepleted && h.Definition != null &&
                                         h.Definition.requiredTool == ToolType.None &&
                                         h.Definition.drops?.entries != null &&
                                         h.Definition.drops.entries.Any(
                                             e => e?.item != null && e.item.id == "scrap"));
                if (node == null)
                {
                    E2EHarness.Log("  맨손으로 캘 수 있는 스크랩 노드를 못 찾았다");
                    break;
                }

                yield return 눈앞에_놓고_겨눈다(node);
                if (!ReferenceEquals(E2EHarness.Player.Interactor.Current, node)) continue;

                yield return E2EHarness.HoldKey(Key.E, node.HoldDuration + 0.35f);
                yield return null;
                캤다 = Inv.CountOf("scrap") > 시작;
            }

            E2EHarness.Assert(캤다, $"맨손 채집으로 스크랩을 얻었다 ({시작} -> {Inv.CountOf("scrap")})");
        }

        /// <summary>
        /// 노드를 눈앞으로 옮기고 조준이 <b>그 노드</b>에 걸릴 때까지 기다린다.
        /// 지형 탐색이 목적이 아니므로 자리는 옮기되 홀드 시간은 그대로 채운다.
        /// <c>autoSyncTransforms</c>가 꺼져 있어 앞뒤로 물리를 맞춰 주지 않으면
        /// 옛 중심으로 델타를 재고, 옮긴 자리는 다음 틱까지 조준에 보이지 않는다.
        /// </summary>
        static IEnumerator 눈앞에_놓고_겨눈다(HarvestNode node)
        {
            var cam = E2EHarness.Eye;
            var body = node.GetComponentInChildren<Collider>(true);
            Vector3 aim = cam.transform.position + cam.transform.forward * 2.2f;

            if (body != null)
            {
                E2EHarness.SyncPhysics();
                node.transform.position += aim - body.bounds.center;
            }
            else node.transform.position = aim;

            E2EHarness.SyncPhysics();
            yield return null;

            var it = E2EHarness.Player.Interactor;
            Vector3 AimPoint() => body != null ? body.bounds.center : node.transform.position;

            float t = 0f;
            while (t < 2.5f && !ReferenceEquals(it.Current, node))
            {
                E2EHarness.LookAt(AimPoint());
                t += Time.deltaTime;
                yield return null;
            }
        }

        // ── 3. 어둠에서 시작한다 ─────────────────────────────────

        static IEnumerator 어둠에서_시작한다()
        {
            E2EHarness.Log("— 배터리를 다 쓴다 —");

            E2EHarness.Assert(E2EHarness.DarkenLantern(), "배터리가 다하자 불이 꺼졌다");
            yield return null;

            E2EHarness.AssertEqual(_lantern.Battery, 0f, "남은 배터리");
            E2EHarness.Assert(!_lantern.IsOn, "랜턴이 꺼져 있다");
            E2EHarness.Assert(!LitZoneRegistry.IsLit(E2EHarness.Player.transform.position),
                              "서 있는 자리가 캄캄하다 — 여기서부터가 사슬의 출발점이다");
            E2EHarness.AssertEqual(Inv.CountOf(LanternController.BatteryCellId), 0,
                                   "여분 셀 (없어야 어둠이 유지된다)");
        }

        // ── 4. 불 곁에서 스크랩이 셀이 된다 ──────────────────────

        static IEnumerator 불_곁에서_스크랩이_셀이_된다()
        {
            E2EHarness.Log("— 불 곁에서 스크랩의 에너지를 셀로 옮긴다 —");

            // 스크랩의 발견이 배터리 설계를 연다(disc_scrap -> bp_scrapworks).
            // 실채집으로 이미 열렸을 것이 정상이고, 앞선 시나리오가 원장을 비운
            // 판에서도 이 시나리오가 재려는 것은 연료 사슬이지 해금 경로가 아니다.
            if (_recipe.requiredBlueprint != null)
                E2EResearchStation.원장에_적는다(_recipe.requiredBlueprint.id);

            채운다("scrap", _스크랩수);
            채운다(MushroomLumberRule.WoodItemId, 8);   // 불을 되살릴 목재
            yield return null;

            _fire = 세운다<Campfire>("campfire");
            E2EHarness.Assert(_fire != null, "화톳불을 세웠다");
            if (_fire == null) yield break;
            E2EHarness.Assert(_fire.IsBurning, "세우자마자 타고 있다");

            int 스크랩전 = Inv.CountOf("scrap");
            int 셀전 = Inv.CountOf(LanternController.BatteryCellId);

            yield return 행을_누른다(_fire, _recipe);
            E2EHarness.AssertEqual(_fire.Work.Queue.Count, 1, "불에 걸린 작업 수");
            E2EHarness.AssertEqual(스크랩전 - Inv.CountOf("scrap"), _스크랩수,
                                   "불에 들어간 스크랩 수");

            UI.Close();
            yield return null;

            // 30초를 실시간으로 기다릴 이유가 없다. 시간만 접고 절차는 그대로 지난다.
            float 이전 = Time.timeScale;
            Time.timeScale = 8f;
            yield return E2EHarness.WaitUntil(() => _fire.Work.HasOutput,
                                              "스크랩의 에너지가 셀로 옮겨졌다",
                                              _recipe.craftSeconds + 8f);
            Time.timeScale = 이전;

            // 만들어진 것은 아직 불 곁에 있다.
            E2EHarness.AssertEqual(Inv.CountOf(LanternController.BatteryCellId), 셀전,
                                   "회수 전 손에 든 셀");

            // <b>불 곁은 이미 밝다.</b> 화톳불은 거저 채워 주는 자리이므로
            // (거점의 값이다) 여기서 어둠을 재려 들면 규칙이 제대로 도는 모습을
            // 고장으로 읽게 된다. 셀이 무엇을 하는지는 불에서 떠난 뒤에 본다.
            E2EHarness.Log($"  불 곁의 배터리 {_lantern.Battery:F1} — 거점에서는 거저 찬다");

            // 회수하는 순간 배터리가 0이면 랜턴이 셀을 즉시 먹어 버린다. 여기서
            // 재려는 것은 회수이지 삽입이므로, 삽입은 다음 대목에서 어둠을 만들고 본다.
            _lantern.Recharge(LanternRule.MaxBattery);
            yield return null;

            _fire.Interact(E2EHarness.Player);
            yield return null;
            E2EHarness.AssertEqual(Inv.CountOf(LanternController.BatteryCellId), 셀전 + 1,
                                   "회수해서 손에 넣은 셀");
        }

        // ── 5. 어둠에서 셀 하나가 빛이 된다 ──────────────────────

        /// <summary>
        /// <b>이것이 이 시나리오의 알맹이다.</b> 거점을 떠나 배터리가 다하면,
        /// 셀을 지니고 있는 한 랜턴이 <b>스스로</b> 갈아 끼운다
        /// (<c>LanternController.TryInsertBatteryCell</c>) — 어둠 속에서 창을 열어
        /// 버튼을 찾게 하는 대신 "몇 개를 들고 갈 것인가"를 출발 전의 결정으로
        /// 만든 자리다. 그래서 여기서 누르는 것은 아무것도 없다.
        ///
        /// 불을 먼저 치운다. 화톳불 곁에서는 배터리가 거저 차므로 불을 남겨 두면
        /// 어둠 자체가 성립하지 않는다 — 앞 대목에서 실측으로 확인한 것이다.
        /// </summary>
        static IEnumerator 어둠에서_셀_하나가_빛이_된다()
        {
            E2EHarness.Log("— 거점을 떠나 배터리가 다하면 셀 하나가 대신 선다 —");

            Object.Destroy(_fire.gameObject);
            _fire = null;
            yield return null;
            yield return null;

            // 끄는 조작은 없다. 어둠에 이르는 길은 다 쓰는 것 하나뿐이다.
            _lantern.Drain(LanternRule.MaxBattery * 2f);

            // 이 세 줄은 Drain과 <b>같은 프레임</b>이다. 다음 Update가 오기 전이므로
            // 아직 셀은 손에 있고 세계는 캄캄하다 — 그 자리를 실제로 지나는지 본다.
            E2EHarness.AssertEqual(_lantern.Battery, 0f, "다 쓴 배터리");
            E2EHarness.Assert(!_lantern.IsOn, "불이 꺼졌다");
            E2EHarness.AssertEqual(Inv.CountOf(LanternController.BatteryCellId), 1,
                                   "아직 손에 있는 셀");
            E2EHarness.Assert(!LitZoneRegistry.IsLit(E2EHarness.Player.transform.position),
                              "서 있는 자리가 캄캄해졌다");

            yield return E2EHarness.WaitUntil(() => _lantern.IsOn,
                                              "누른 것 없이 불이 되살아났다", 3f);

            E2EHarness.AssertEqual(Inv.CountOf(LanternController.BatteryCellId), 0,
                                   "셀은 랜턴이 먹었다 (남은 셀)");
            E2EHarness.Assert(_lantern.Battery > LanternRule.MaxBattery * 0.9f,
                              $"셀 하나가 배터리를 거의 가득 채웠다 ({_lantern.Battery:F1}/{LanternRule.MaxBattery:F0})");
            E2EHarness.Assert(LitZoneRegistry.IsLit(_lantern.LitZoneCenter),
                              "빛 웅덩이가 다시 밝은 구역으로 등록됐다");

            // 사슬 전체를 매크로늄 0개로 지났다. 이것이 회신 ①의 내용 그 자체다.
            E2EHarness.AssertEqual(매크로늄수(), 0, "완주까지 손에 든 매크로늄 계열");
        }

        // ── 6. 실측 ──────────────────────────────────────────────

        static IEnumerator 실측()
        {
            _lantern.Recharge(LanternRule.MaxBattery);
            yield return null;

            float 시작 = _lantern.Battery;
            float t0 = Time.time;
            float 잰시간 = 0f;
            while (잰시간 < 2f) { 잰시간 = Time.time - t0; yield return null; }

            float 준양 = 시작 - _lantern.Battery;
            float 초당 = 준양 / Mathf.Max(잰시간, 0.001f);
            float 실측초 = 초당 > 0f ? LanternRule.BatteryPerCell / 초당 : 0f;
            float 규칙초 = LanternRule.SecondsOfLight(LanternRule.BatteryPerCell, 1);

            E2EHarness.Log("— 실측 (수치는 사람의 몫, 손잡이는 Domain/World/LanternRule.cs) —");
            E2EHarness.Log($"  스크랩 {_스크랩수}개 → 배터리 셀 1개 → 티어 1 랜턴 " +
                           $"{실측초:F1}초 (규칙값 {규칙초:F1}초)");
            E2EHarness.Log($"  스크랩 1개 = {LanternRule.BatteryPerCell / _스크랩수:F0} 전하 = " +
                           $"{규칙초 / _스크랩수:F1}초, 초당 소모 실측 {초당:F2} (규칙 {LanternRule.DrainForTier(1):F2})");

            E2EHarness.Assert(Mathf.Abs(실측초 - 규칙초) < 규칙초 * 0.25f,
                              $"실측 지속시간이 규칙과 같다 ({실측초:F1}초 / {규칙초:F1}초)");
        }

        // ── 7. 되돌린다 ──────────────────────────────────────────

        static IEnumerator 되돌린다()
        {
            if (_fire != null)
            {
                Object.Destroy(_fire.gameObject);
                _fire = null;
            }

            if (_lantern != null) _lantern.Recharge(LanternRule.MaxBattery);
            E2EHarness.RestoreWorld();
            yield return null;
        }

        // ── 공통 동작 ────────────────────────────────────────────

        static void 채운다(string id, int 목표)
        {
            var db = PI.Database;
            var item = db != null ? db.GetById(id) : null;
            if (item == null) return;

            int 모자람 = 목표 - Inv.CountOf(id);
            if (모자람 > 0) Inv.TryAdd(item, 모자람);
        }

        /// <summary>
        /// 배치 절차는 <c>E2EBaseBuilding</c>이 이미 본다. 여기서 보려는 것은 연료
        /// 사슬이므로 지형 조준에 시나리오를 걸지 않고 곁에 바로 세운다.
        /// </summary>
        static T 세운다<T>(string buildableId) where T : Component
        {
            var catalog = Resources.FindObjectsOfTypeAll<BuildCatalogSO>().FirstOrDefault();
            var def = catalog?.entries?.FirstOrDefault(b => b != null && b.id == buildableId);
            if (def?.prefab == null) return null;

            var pos = E2EHarness.Player.transform.position +
                      E2EHarness.Player.transform.forward * 2.2f;
            var go = Object.Instantiate(def.prefab, pos, Quaternion.identity);
            return go.GetComponentInChildren<T>(true);
        }

        /// <summary>제작 화면을 열고 그 행을 실제로 클릭한다.</summary>
        static IEnumerator 행을_누른다(ICraftStation station, RecipeSO r)
        {
            UI.Open(station);
            yield return null;
            yield return null;

            var row = UI.GetComponentsInChildren<Button>(true)
                        .FirstOrDefault(b => b.gameObject.name == "Row_" + r.id);
            E2EHarness.Assert(row != null, $"레시피 행이 있다: {r.id}");
            if (row == null) yield break;

            E2EHarness.Assert(row.interactable, $"{r.id}를 걸 수 있다 (재료·설계 모두 충족)");
            ExecuteEvents.Execute(row.gameObject, new PointerEventData(EventSystem.current),
                                  ExecuteEvents.pointerClickHandler);
            yield return null;
            yield return null;
        }
    }
}
