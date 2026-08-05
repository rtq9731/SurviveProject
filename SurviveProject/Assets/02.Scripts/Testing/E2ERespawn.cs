using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Survive.Building;
using Survive.Items;
using Survive.Vitals;

namespace Survive.Testing
{
    /// <summary>
    /// 죽고, 다시 일어선다 (백로그 26번).
    ///
    /// 부활 규칙은 한 줄이지만 세 가지가 동시에 성립해야 말이 된다:
    /// <b>화톳불이 없으면 시작 지점으로 돌아가고</b>,
    /// <b>화톳불이 있으면 마지막으로 지핀 그 불 곁에서 일어서고</b>,
    /// <b>그러는 동안 떨군 가방은 죽은 자리에 그대로 남는다.</b>
    ///
    /// 셋째가 특히 조용히 깨진다 — 부활이 몸을 옮기는 순간 사망 드롭이 "죽은 자리"를
    /// 잘못 잡으면, 가방이 부활 지점에 같이 따라와 왕복의 대가가 통째로 사라진다.
    /// 그러면 아무 검사도 실패하지 않은 채 압박만 없어진다. 그래서 여기서 같이 본다.
    ///
    /// <see cref="E2EDeathDrop"/>은 사망 <b>이후의 소유물</b>을 보고,
    /// 여기서는 사망 <b>이후의 몸</b>을 본다.
    /// </summary>
    public static class E2ERespawn
    {
        static Inventory Inv => E2EHarness.Player.Inventory.Inventory;
        static PlayerVitals Vitals => E2EHarness.Player.Vitals;

        static BuildPlacer Placer =>
            Object.FindAnyObjectByType<BuildPlacer>(FindObjectsInactive.Exclude);

        /// <summary>죽으면서 떨굴 것. 가방이 죽은 자리에 남는지 보려면 담길 것이 있어야 한다.</summary>
        static readonly string[] 떨굴것 = { "scrap", "alien_alloy" };

        /// <summary>
        /// 불에 넣을 것. 화톳불 <b>건축</b> 비용에는 목재가 없지만
        /// 불을 <b>지키는</b> 데는 목재가 든다(백로그 35) — 비용에서 자동으로
        /// 끌어올 수 없는 유일한 재료라 따로 적는다.
        /// </summary>
        static string 연료 => Survive.Harvesting.MushroomLumberRule.WoodItemId;

        /// <summary>두 번째 시나리오에서 세운 화톳불. 셋째 시나리오가 상대로 삼는다.</summary>
        static Campfire _첫불;

        public static IEnumerator FullRun()
        {
            yield return Prepare();

            yield return 화톳불이_없으면_시작_지점();
            yield return 화톳불이_있으면_그_곁();
            yield return 나중에_지핀_불이_이긴다();

            E2EHarness.Log("=== 부활 완주 ===");
        }

        // ── 준비 ────────────────────────────────────────────────

        static IEnumerator Prepare()
        {
            var dir = Object.FindAnyObjectByType<Survive.Progression.ChapterDirector>(
                FindObjectsInactive.Exclude);
            yield return E2EHarness.WaitUntil(() => dir != null && dir.Current != null,
                                              "챕터가 시작된다", 8f);

            E2EHarness.Assert(DeathDropService.Instance != null, "사망 드롭 서비스가 스스로 서 있다");
            E2EHarness.Assert(RespawnService.Instance != null, "부활 서비스가 스스로 서 있다");

            yield return E2EHarness.WaitUntil(() => Placer != null, "BuildPlacer가 준비된다", 8f);

            _첫불 = null;

            var 시작 = RespawnService.Instance.StartPoint;
            E2EHarness.Assert(RespawnService.Instance.HasStartPoint, "시작 지점을 기억하고 있다");
            E2EHarness.Log($"  시작 지점 {시작.ToString("F1")}");
        }

        /// <summary>재료와 떨굴 것을 채운다. 죽을 때마다 비므로 매번 다시 채운다.</summary>
        static void 채운다()
        {
            var db = E2EHarness.Player.Inventory.Database;
            if (db == null) return;

            var 필요 = new HashSet<string>(떨굴것) { 연료 };

            var placer = Placer;
            if (placer != null)
            {
                placer.SelectById("campfire");
                var cost = placer.Selected?.cost;
                if (cost != null)
                    foreach (var c in cost) if (c?.item != null) 필요.Add(c.item.id);
                placer.Cancel();
            }

            foreach (var id in 필요)
            {
                var item = db.GetById(id);
                if (item != null && Inv.CountOf(id) < 20) Inv.TryAdd(item, 20);
            }
        }

        // ── 1. 화톳불이 없을 때 ─────────────────────────────────

        static IEnumerator 화톳불이_없으면_시작_지점()
        {
            E2EHarness.Log("— 화톳불 없이 죽는다 —");

            E2EHarness.Assert(타는화톳불().Count == 0, "아직 세운 화톳불이 없다");

            var 시작 = RespawnService.Instance.StartPoint;
            채운다();

            yield return 멀어진다(시작, 12f);
            float 떠난거리 = 평면거리(E2EHarness.Player.transform.position, 시작);
            E2EHarness.Assert(떠난거리 > 6f, $"시작 지점에서 충분히 떠났다 ({떠난거리:F1}m)");

            DeathDropBag bag = null;
            Vector3 죽은자리 = Vector3.zero;
            yield return 죽는다(r => bag = r, r => 죽은자리 = r);

            yield return 부활을_기다린다();

            E2EHarness.Assert(!RespawnService.LastRespawnWasCampfire,
                              "화톳불이 없으니 시작 지점으로 돌아갔다고 보고한다");

            float 오차 = 평면거리(E2EHarness.Player.transform.position, 시작);
            E2EHarness.Log($"  부활 {E2EHarness.Player.transform.position.ToString("F1")}, " +
                           $"시작 지점에서 {오차:F1}m");
            E2EHarness.Assert(오차 < 2f, $"시작 지점에서 되살아났다 ({오차:F1}m)");

            가방은_죽은_자리에(bag, 죽은자리);
        }

        // ── 2. 화톳불이 있을 때 ─────────────────────────────────

        static IEnumerator 화톳불이_있으면_그_곁()
        {
            E2EHarness.Log("— 화톳불을 세우고 죽는다 —");

            채운다();

            Campfire 불 = null;
            yield return 화톳불을_세운다(r => 불 = r);
            if (불 == null) yield break;

            _첫불 = 불;
            var 불자리 = 불.transform.position;
            E2EHarness.Assert(불.IsBurning, "세운 화톳불이 타고 있다");

            채운다();
            yield return 멀어진다(불자리, 14f);
            float 떠난거리 = 평면거리(E2EHarness.Player.transform.position, 불자리);
            E2EHarness.Assert(떠난거리 > 6f, $"화톳불에서 충분히 떠났다 ({떠난거리:F1}m)");

            DeathDropBag bag = null;
            Vector3 죽은자리 = Vector3.zero;
            yield return 죽는다(r => bag = r, r => 죽은자리 = r);

            yield return 부활을_기다린다();

            E2EHarness.Assert(RespawnService.LastRespawnWasCampfire,
                              "화톳불에서 되살아났다고 보고한다");

            float 불에서 = 평면거리(E2EHarness.Player.transform.position, 불자리);
            E2EHarness.Log($"  부활 {E2EHarness.Player.transform.position.ToString("F1")}, " +
                           $"화톳불에서 {불에서:F1}m");
            E2EHarness.Assert(불에서 < 3.5f, $"화톳불 곁에서 되살아났다 ({불에서:F1}m)");
            E2EHarness.Assert(불에서 > 0.3f, "불 속이 아니라 불 곁에 선다");

            // 여기가 이 시나리오의 핵심이다. 몸은 불로 돌아가고 물건은 죽은 자리에 남는다 —
            // 둘이 같이 움직이면 왕복의 대가가 사라진다.
            가방은_죽은_자리에(bag, 죽은자리);
            if (bag != null)
            {
                float 불에서가방까지 = 평면거리(bag.transform.position, 불자리);
                E2EHarness.Assert(불에서가방까지 > 5f,
                                  $"가방은 부활 지점까지 따라오지 않았다 ({불에서가방까지:F1}m)");
            }
        }

        // ── 3. 마지막에 지핀 불이 이긴다 ────────────────────────

        /// <summary>
        /// 화톳불이 둘이면 <b>나중에 지핀</b> 쪽으로 간다.
        /// 규칙 자체는 <c>RespawnRuleTests</c>가 Unity 없이 본다. 여기서 보는 것은
        /// 그 규칙에 넘기는 시각이 실제로 화톳불에서 온 값인가 — 배선이다.
        /// </summary>
        static IEnumerator 나중에_지핀_불이_이긴다()
        {
            E2EHarness.Log("— 두 번째 화톳불을 세우고 죽는다 —");

            var 첫불 = _첫불;
            E2EHarness.Assert(첫불 != null, "앞에서 세운 화톳불이 남아 있다");
            if (첫불 == null) yield break;
            var 첫불자리 = 첫불.transform.position;

            채운다();
            yield return 불을_지킨다(첫불);

            // 첫 불에서 떨어진 데에 두 번째를 세운다. 붙여 세우면 어느 쪽에서
            // 일어섰는지 좌표로 가릴 수 없다.
            yield return 멀어진다(첫불자리, 14f);

            Campfire 둘째불 = null;
            yield return 화톳불을_세운다(r => 둘째불 = r);
            if (둘째불 == null) yield break;

            var 둘째자리 = 둘째불.transform.position;
            E2EHarness.Assert(둘째불.KindledAt > 첫불.KindledAt,
                              $"두 번째 불이 나중에 붙었다 ({첫불.KindledAt:F1} → {둘째불.KindledAt:F1})");
            E2EHarness.Assert(첫불.IsBurning, "첫 불도 아직 타고 있다 — 둘 중에서 고르는 상황이 맞다");
            E2EHarness.Assert(평면거리(둘째자리, 첫불자리) > 5f,
                              $"두 불이 떨어져 있다 ({평면거리(둘째자리, 첫불자리):F1}m)");

            채운다();
            yield return 멀어진다(둘째자리, 12f);

            DeathDropBag bag = null;
            Vector3 죽은자리 = Vector3.zero;
            yield return 죽는다(r => bag = r, r => 죽은자리 = r);

            yield return 부활을_기다린다();

            float 둘째까지 = 평면거리(E2EHarness.Player.transform.position, 둘째자리);
            float 첫불까지 = 평면거리(E2EHarness.Player.transform.position, 첫불자리);
            E2EHarness.Log($"  두 번째 불에서 {둘째까지:F1}m, 첫 불에서 {첫불까지:F1}m");

            E2EHarness.Assert(둘째까지 < 3.5f, $"나중에 지핀 불에서 일어섰다 ({둘째까지:F1}m)");
            E2EHarness.Assert(둘째까지 < 첫불까지, "먼저 세운 불이 아니라 나중 불이다");

            가방은_죽은_자리에(bag, 죽은자리);
        }

        // ── 공통 동작 ───────────────────────────────────────────

        static List<Campfire> 타는화톳불() =>
            Object.FindObjectsByType<Campfire>(FindObjectsInactive.Exclude)
                  .Where(c => c.IsBurning).ToList();

        static float 평면거리(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        /// <summary>
        /// 죽는다. 체력을 코드로 0으로 만드는 이유는 <see cref="E2EDeathDrop"/>과 같다 —
        /// 무엇으로 죽는가는 <see cref="E2ESuffocation"/>이 실제 경로로 이미 본다.
        /// </summary>
        static IEnumerator 죽는다(System.Action<DeathDropBag> 가방, System.Action<Vector3> 자리)
        {
            E2EHarness.Assert(Inv.CountOf(떨굴것[0]) > 0, "죽기 전에 떨굴 것이 있다");

            int 이전가방수 = DeathDropBag.Active.Count;
            var 죽은자리 = E2EHarness.Player.transform.position;
            자리(죽은자리);

            Vitals.Health.Modify(-(Vitals.Health.Max + 10f));
            E2EHarness.Assert(Vitals.Health.IsEmpty, "체력이 0이 됐다");

            yield return E2EHarness.WaitUntil(() => DeathDropBag.Active.Count > 이전가방수,
                                              "죽은 자리에 가방이 남는다", 5f);
            가방(DeathDropService.LastBag);
        }

        static IEnumerator 부활을_기다린다()
        {
            int 이전 = RespawnService.RespawnCount;

            yield return E2EHarness.WaitUntil(() => RespawnService.RespawnCount > 이전,
                                              "잠깐 뒤 스스로 되살아난다", 6f);
            yield return null;

            E2EHarness.Assert(!Vitals.Health.IsEmpty, "체력이 돌아왔다");
            E2EHarness.Assert(Vitals.Health.IsFull, "체력이 가득 찼다");
            E2EHarness.Assert(Vitals.Oxygen.IsFull, "산소도 가득 찼다 — 되살아나자마자 또 죽지 않는다");
        }

        static void 가방은_죽은_자리에(DeathDropBag bag, Vector3 죽은자리)
        {
            E2EHarness.Assert(bag != null, "남긴 가방을 집을 수 있다");
            if (bag == null) return;

            float 거리 = 평면거리(bag.transform.position, 죽은자리);
            E2EHarness.Assert(거리 < 3f, $"가방은 죽은 자리에 그대로 남는다 ({거리:F1}m)");
        }

        /// <summary>
        /// 불에 버섯 목재를 넣어 살려 둔다. 검사가 오래 도는 동안 연료가 다 타 버리면
        /// "둘 중 나중 것을 고른다"가 아니라 "하나밖에 안 남았다"를 보게 된다.
        ///
        /// 곁들여 확인하는 것이 하나 더 있다 — <b>연료를 넣는 것은 불붙이는 것이 아니다.</b>
        /// 타고 있는 불에 장작을 더한다고 그 불이 "마지막에 지핀 불"이 되지는 않는다.
        /// 이게 뒤집히면 셋째 시나리오의 전제가 조용히 무너진다.
        ///
        /// 경로 개정(백로그 34): 화톳불이 가공 스테이션이 되면서 <b>타고 있는 불</b> 앞의
        /// E는 가공 화면을 연다(꺼진 불 앞에서는 여전히 지피기다 — 어둠 속에서 창을
        /// 찾게 할 수는 없다). 연료 보급은 그 화면 안의 한 줄로 옮겨갔다. 그래서
        /// 여기서도 E로 화면을 열고 그 줄을 눌러 넣는다 — 검사 범위는 오히려 늘었다.
        /// </summary>
        static IEnumerator 불을_지킨다(Campfire 불)
        {
            float 이전연료 = 불.FuelNormalized;
            float 이전시각 = 불.KindledAt;

            var it = E2EHarness.Player.Interactor;
            bool 조준 = false;
            yield return 불을_조준한다(불, r => 조준 = r);
            E2EHarness.Assert(조준, "화톳불이 조준된다");
            if (!조준) yield break;

            E2EHarness.Log("  프롬프트: " + it.Current.InteractionPrompt);
            yield return E2EHarness.TapKey(UnityEngine.InputSystem.Key.E);
            yield return null;
            yield return null;

            var ui = Object.FindAnyObjectByType<Survive.UI.CraftingUI>(FindObjectsInactive.Include);
            E2EHarness.Assert(ui != null && ui.IsOpen, "E로 화톳불 화면이 열린다");
            E2EHarness.Assert(ui != null && ui.CurrentStationHost == (object)불,
                              "열린 화면이 이 불에 매여 있다");

            var 연료줄 = ui.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                          .FirstOrDefault(b => b.gameObject.name == "Row_StationAction" &&
                                               b.gameObject.activeInHierarchy);
            E2EHarness.Assert(연료줄 != null && 연료줄.interactable, "연료 넣기 줄을 누를 수 있다");
            if (연료줄 == null) yield break;

            UnityEngine.EventSystems.ExecuteEvents.Execute(
                연료줄.gameObject,
                new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current),
                UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            yield return null;
            yield return null;

            ui.Close();
            yield return null;

            E2EHarness.Assert(불.FuelNormalized > 이전연료,
                              $"목재를 넣어 불을 살렸다 ({이전연료:P0} → {불.FuelNormalized:P0})");
            E2EHarness.AssertEqual(불.KindledAt, 이전시각,
                                   "연료를 넣어도 '불붙인 시각'은 그대로다");
        }

        /// <summary>
        /// 불을 조준할 수 있는 자리를 찾는다.
        ///
        /// 겨눌 점은 <c>transform.position</c>이 아니라 콜라이더 한가운데다 —
        /// 원점은 발밑이라 그쪽을 보면 레이가 지면을 먼저 맞는다.
        /// (<see cref="E2EBaseBuilding"/>이 철거 조준에서 같은 데 걸렸다.)
        /// </summary>
        static IEnumerator 불을_조준한다(Campfire 불, System.Action<bool> result)
        {
            var col = 불.GetComponentInChildren<Collider>();
            var 겨눌곳 = col != null ? col.bounds.center : 불.transform.position + Vector3.up * 0.4f;
            var it = E2EHarness.Player.Interactor;

            for (int a = 0; a < 8; a++)
            {
                foreach (float dist in new[] { 2.0f, 2.6f })
                {
                    var dir = Quaternion.Euler(0f, a * 45f, 0f) * Vector3.forward;
                    var stand = 겨눌곳 + dir * dist;

                    if (Physics.Raycast(stand + Vector3.up * 4f, Vector3.down, out var hit, 14f,
                                        ~0, QueryTriggerInteraction.Ignore))
                        stand.y = hit.point.y + 1.0f;

                    E2EHarness.Teleport(stand);
                    yield return null;
                    E2EHarness.LookAt(겨눌곳);
                    yield return null;
                    yield return null;

                    if (it.Current is Campfire) { result(true); yield break; }
                }
            }

            result(false);
        }

        // ── 옮겨 다니기 ─────────────────────────────────────────

        /// <summary>
        /// 지정한 자리에서 멀어진다. 걸어서 가는 것을 먼저 시도하고,
        /// 동굴 지형에 막히면 옮겨서라도 떨어뜨린다 — 여기서 보려는 것은
        /// 보행이 아니라 <b>떨어진 자리에서 죽으면 어디로 돌아오는가</b>다.
        /// </summary>
        static IEnumerator 멀어진다(Vector3 from, float meters)
        {
            for (int a = 0; a < 8; a++)
            {
                var dir = Quaternion.Euler(0f, a * 45f + 20f, 0f) * Vector3.forward;
                if (!NavMesh.SamplePosition(from + dir * meters, out var hit, 4f, NavMesh.AllAreas))
                    continue;
                if (평면거리(hit.position, from) < meters * 0.5f) continue;

                yield return E2EHarness.TryWalkTo(hit.position, 2.0f, 25f);
                if (E2EHarness.LastWalkArrived) yield break;

                E2EHarness.Log("  걸어서 못 갔다 — 옮겨서 떨어뜨린다");
                E2EHarness.Teleport(hit.position + Vector3.up * 0.5f);
                yield return null;
                yield break;
            }

            E2EHarness.Log("  [주의] 멀어질 자리를 NavMesh에서 찾지 못했다");
        }

        // ── 화톳불 세우기 ───────────────────────────────────────

        /// <summary>
        /// 실제 조작으로 세운다 — 목록에서 고르고, 놓을 자리를 조준하고, 좌클릭한다.
        /// <see cref="E2EBaseBuilding"/>과 같은 경로이되 화톳불 하나만 본다.
        /// </summary>
        static IEnumerator 화톳불을_세운다(System.Action<Campfire> result)
        {
            var placer = Placer;
            E2EHarness.Assert(placer != null, "BuildPlacer가 있다");
            if (placer == null) { result(null); yield break; }

            placer.SelectById("campfire");
            yield return null;
            E2EHarness.Assert(placer.Selected != null, "화톳불 정의를 찾았다");

            var 이전 = new HashSet<Campfire>(
                Object.FindObjectsByType<Campfire>(FindObjectsInactive.Exclude));

            bool found = false;
            yield return 놓을자리를_조준한다(placer, r => found = r);
            E2EHarness.Assert(found, "화톳불을 놓을 자리를 찾았다");
            if (!found) { placer.Cancel(); result(null); yield break; }

            yield return E2EHarness.ClickAttack();
            yield return null;
            yield return null;

            var 새불 = Object.FindObjectsByType<Campfire>(FindObjectsInactive.Exclude)
                            .FirstOrDefault(c => !이전.Contains(c));

            placer.Cancel();
            yield return null;

            E2EHarness.Assert(새불 != null, "좌클릭으로 화톳불을 세웠다");
            if (새불 != null)
                E2EHarness.Log($"  세웠다: {새불.transform.position.ToString("F0")} " +
                               $"(불붙인 시각 {새불.KindledAt:F1})");
            result(새불);
        }

        static IEnumerator 놓을자리를_조준한다(BuildPlacer placer, System.Action<bool> result)
        {
            var player = E2EHarness.Player.transform;

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

                    if (placer.Evaluate(out _, out _) == PlacementResult.Ok)
                    {
                        result(true);
                        yield break;
                    }
                }
            }

            E2EHarness.Log($"  놓을 자리를 못 찾았다 (마지막 판정 {placer.LastResult})");
            result(false);
        }
    }
}
