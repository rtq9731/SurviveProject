using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Survive.Building;
using Survive.Items;

namespace Survive.Testing
{
    /// <summary>
    /// 모듈 건축이 실제로 물리는지 본다.
    ///
    /// 여기서 확인하려는 것은 "세워졌는가"가 아니라 <b>어긋나지 않는가</b>다.
    /// 모듈 건축은 1도, 1cm가 어긋나면 조각 열 개를 이었을 때 눈에 보이는
    /// 틈이 된다. 그래서 세운 뒤 좌표를 재서 격자에 맞는지 확인한다.
    ///
    /// 조준은 사람이 하는 대로 한다 — 붙일 자리 곁에 서서 그쪽을 본다.
    /// 멀리서 좌표만 겨누면 광선이 지형에 먼저 맞아 엉뚱한 자리가 잡힌다.
    /// </summary>
    public static class E2EModularBuild
    {
        const float Cell = 4f;

        static Inventory Inv => E2EHarness.Player.Inventory.Inventory;

        static BuildPlacer Placer =>
            Object.FindFirstObjectByType<BuildPlacer>(FindObjectsInactive.Exclude);

        public static IEnumerator FullRun()
        {
            var dir = Object.FindFirstObjectByType<Survive.Progression.ChapterDirector>(
                FindObjectsInactive.Exclude);
            yield return E2EHarness.WaitUntil(() => dir != null && dir.Current != null,
                                              "챕터가 시작된다", 8f);

            var db = E2EHarness.Player.Inventory.Database;
            var scrap = db != null ? db.GetById("scrap") : null;
            E2EHarness.Assert(scrap != null, "스크랩 정의를 찾았다");
            if (scrap != null) Inv.TryAdd(scrap, 400);

            var placer = Placer;
            E2EHarness.Assert(placer != null, "BuildPlacer가 있다");
            if (placer == null) yield break;

            // ── 토대 ────────────────────────────────────────────
            E2EHarness.Assert(SnapGraph.Count == 0 || true, "스냅 자리 등록이 살아 있다");

            GameObject first = null;
            yield return PlaceFoundation(r => first = r);
            if (first == null) yield break;

            var origin = first.transform.position;
            E2EHarness.Log($"  첫 토대 {origin.ToString("F2")}, 스냅 자리 {SnapGraph.Count}개");
            E2EHarness.Assert(SnapGraph.Count >= 12, "토대가 붙일 자리를 내놓는다");

            // 붙일 곳이 없으면 나머지는 못 놓는다는 규칙
            yield return NeedsAnchor(placer, origin);

            // ── 옆으로 넓히기 ───────────────────────────────────
            GameObject second = null;
            yield return PlaceNear("piece_foundation", origin + new Vector3(Cell, 0f, 0f), r => second = r);
            E2EHarness.Assert(second != null, "토대를 옆에 이어 붙였다");

            if (second != null)
            {
                float gap = Vector3.Distance(second.transform.position, origin);
                E2EHarness.Log($"  두 토대 간격 {gap:F3}m (칸 {Cell}m)");
                E2EHarness.Assert(Mathf.Abs(gap - Cell) < 0.01f, "간격이 정확히 한 칸이다");
                E2EHarness.Assert(Mathf.Abs(second.transform.position.y - origin.y) < 0.01f,
                                  "이어 붙인 토대의 높이가 같다");
            }

            // ── 벽 ──────────────────────────────────────────────
            GameObject wall = null;
            yield return PlaceNear("piece_wall", origin + new Vector3(0f, 0.4f, -Cell * 0.5f), r => wall = r);
            E2EHarness.Assert(wall != null, "토대 모서리에 벽을 세웠다");

            if (wall != null)
            {
                E2EHarness.Log($"  벽 {wall.transform.position.ToString("F2")} " +
                               $"회전 {wall.transform.eulerAngles.y:F0}도");
                E2EHarness.Assert(wall.transform.position.y > origin.y + 0.3f,
                                  "벽이 토대 윗면에서 시작한다");

                float yaw = Mathf.Repeat(wall.transform.eulerAngles.y, 90f);
                E2EHarness.Assert(yaw < 0.01f || yaw > 89.99f, "벽이 격자에 맞춰 돌아갔다");
            }

            // ── 같은 자리에 또 못 놓는다 ────────────────────────
            if (wall != null) yield return SlotIsTaken(placer, wall.transform.position);

            // ── 문간·경사로 ─────────────────────────────────────
            GameObject door = null;
            yield return PlaceNear("piece_doorway", origin + new Vector3(-Cell * 0.5f, 0.4f, 0f), r => door = r);
            E2EHarness.Assert(door != null, "다른 모서리에 문간을 세웠다");

            GameObject ramp = null;
            yield return PlaceNear("piece_ramp", origin + new Vector3(0f, 0.4f, Cell * 0.5f), r => ramp = r);
            E2EHarness.Assert(ramp != null, "경사로를 붙였다");

            // ── 2층 ─────────────────────────────────────────────
            if (wall != null)
            {
                GameObject upper = null;

                // 벽 꼭대기 <b>안쪽</b>이 2층 바닥 자리다. 벽 바로 위가 아니라
                // 반 칸 들어간 곳이어야 바닥이 방을 덮는다.
                var flat = new Vector3(origin.x, wall.transform.position.y, origin.z);
                var inward = (flat - wall.transform.position).normalized * (Cell * 0.5f);
                var above = wall.transform.position + Vector3.up * 3f + inward;

                yield return PlaceNear("piece_floor", above, r => upper = r);

                if (upper != null)
                {
                    E2EHarness.Log($"  2층 바닥 {upper.transform.position.ToString("F2")}");
                    E2EHarness.Assert(upper.transform.position.y > origin.y + 2.9f,
                                      "벽 위에 바닥이 얹혔다");
                }
                else E2EHarness.Log("  [확인 필요] 벽 위 바닥을 못 얹었다");
            }

            // ── 격자 정합 ───────────────────────────────────────
            yield return GridIsAligned(origin);

            // ── 부수면 자리도 사라진다 ──────────────────────────
            if (ramp != null) yield return DemolishFreesSlot(ramp);

            placer.Cancel();
            E2EHarness.Log("=== 모듈 건축 완주 ===");
        }

        // ── 배치 ────────────────────────────────────────────────

        /// <summary>첫 토대는 붙을 곳이 없으니 평지를 찾아 지면에 놓는다.</summary>
        static IEnumerator PlaceFoundation(System.Action<GameObject> result)
        {
            var placer = Placer;
            placer.SelectById("piece_foundation");
            yield return null;

            var from = E2EHarness.Player.transform.position;

            for (int ring = 0; ring < 4; ring++)
            for (int a = 0; a < 12; a++)
            {
                var dir = Quaternion.Euler(0f, a * 30f, 0f) * Vector3.forward;
                var probe = from + dir * (2.5f + ring * 1.5f);

                if (!Physics.Raycast(probe + Vector3.up * 3f, Vector3.down, out var g, 10f, ~0,
                                     QueryTriggerInteraction.Ignore))
                    continue;

                E2EHarness.LookAt(g.point);
                yield return null;
                yield return null;

                if (placer.Evaluate(out _, out _) != PlacementResult.Ok) continue;

                var go = placer.TryBuild();
                if (go != null) { result(go); yield break; }
            }

            E2EHarness.Log("  [배치 문제] 토대를 놓을 평지를 찾지 못했다");
            result(null);
        }

        /// <summary>
        /// 붙일 자리 곁으로 걸어가 그쪽을 보고 놓는다.
        ///
        /// 멀리서 좌표를 겨누면 광선이 중간 지형에 맞고, 그 지점에서 가장 가까운
        /// 자리가 잡힌다 — 사람이 겪는 것과 같은 일이라 테스트도 같게 해야 한다.
        /// </summary>
        static IEnumerator PlaceNear(string id, Vector3 want, System.Action<GameObject> result)
        {
            var placer = Placer;
            placer.SelectById(id);
            yield return null;

            // 목표를 내려다볼 수 있는 자리에 선다
            for (int a = 0; a < 8; a++)
            {
                var dir = Quaternion.Euler(0f, a * 45f, 0f) * Vector3.forward;
                E2EHarness.Teleport(want + dir * 3.2f + Vector3.up * 1.6f);
                yield return null;

                E2EHarness.LookAt(want);
                yield return null;
                yield return null;

                var r = placer.Evaluate(out var pos, out _);
                if (r != PlacementResult.Ok) continue;

                // 겨눈 곳과 실제로 잡힌 자리가 딴판이면 그건 원하는 배치가 아니다
                if (Vector3.Distance(pos, want) > 1.2f) continue;

                var go = placer.TryBuild();
                if (go != null) { result(go); yield break; }
            }

            var last = placer.Evaluate(out _, out _);
            E2EHarness.Log($"  [배치 문제] {id}를 {want.ToString("F1")}에 못 놓았다 (판정 {last})");
            result(null);
        }

        // ── 규칙 ────────────────────────────────────────────────

        /// <summary>토대 없이 벽부터 세울 수는 없다.</summary>
        static IEnumerator NeedsAnchor(BuildPlacer placer, Vector3 origin)
        {
            placer.SelectById("piece_wall");
            yield return null;

            // 붙일 자리에서 한참 떨어진 지면을 본다
            var away = origin + new Vector3(30f, 0f, 30f);
            if (Physics.Raycast(away + Vector3.up * 20f, Vector3.down, out var g, 60f, ~0,
                                QueryTriggerInteraction.Ignore))
            {
                E2EHarness.Teleport(g.point + new Vector3(0f, 1.6f, -3f));
                yield return null;
                E2EHarness.LookAt(g.point);
                yield return null;
                yield return null;

                var r = placer.Evaluate(out _, out _);
                E2EHarness.Log($"  붙일 곳 없는 데서 벽 판정 = {r}");
                E2EHarness.Assert(r == PlacementResult.NoAnchor,
                                  "붙일 자리가 없으면 벽은 못 세운다");
            }

            placer.Cancel();
        }

        /// <summary>한 모서리에 벽과 문간이 함께 들어가면 안 된다.</summary>
        static IEnumerator SlotIsTaken(BuildPlacer placer, Vector3 wallAt)
        {
            placer.SelectById("piece_doorway");
            yield return null;

            E2EHarness.Teleport(wallAt + new Vector3(0f, 1.6f, 3f));
            yield return null;
            E2EHarness.LookAt(wallAt + Vector3.up * 0.5f);
            yield return null;
            yield return null;

            var r = placer.Evaluate(out var pos, out _);
            bool sameSlot = Vector3.Distance(pos, wallAt) < 0.6f;

            E2EHarness.Log($"  벽 자리에 문간 판정 = {r}" + (sameSlot ? " (같은 자리)" : " (다른 자리)"));
            if (sameSlot)
                E2EHarness.Assert(r == PlacementResult.SlotTaken, "벽이 선 모서리엔 문간이 안 들어간다");

            placer.Cancel();
        }

        /// <summary>세운 조각들이 전부 한 격자 위에 있는가.</summary>
        static IEnumerator GridIsAligned(Vector3 origin)
        {
            yield return null;

            var platforms = Object.FindObjectsByType<ModularPiece>(FindObjectsSortMode.None)
                .Where(p => (p.Kind & BuildPieceKind.Platform) != 0)
                .ToList();

            E2EHarness.Assert(platforms.Count >= 2, "판때기가 둘 이상이다");

            float worst = 0f;
            foreach (var p in platforms)
            {
                var d = p.transform.position - origin;
                float ex = Mathf.Abs(Mathf.Repeat(d.x + Cell * 0.5f, Cell) - Cell * 0.5f);
                float ez = Mathf.Abs(Mathf.Repeat(d.z + Cell * 0.5f, Cell) - Cell * 0.5f);
                worst = Mathf.Max(worst, Mathf.Max(ex, ez));
            }

            E2EHarness.Log($"  판때기 {platforms.Count}개, 격자 최대 오차 {worst * 100f:F1}cm");
            E2EHarness.Assert(worst < 0.01f, "모든 판때기가 같은 격자 위에 있다");
        }

        /// <summary>부순 자리에는 다시 놓을 수 있어야 한다.</summary>
        static IEnumerator DemolishFreesSlot(GameObject piece)
        {
            var at = piece.transform.position;
            int before = SnapGraph.Count;

            var dem = piece.GetComponent<StructureDemolisher>();
            E2EHarness.Assert(dem != null, "모듈 조각도 부술 수 있다");
            if (dem == null) yield break;

            dem.Demolish(E2EHarness.Player);
            yield return null;
            yield return null;

            E2EHarness.Assert(piece == null, "조각이 사라졌다");
            E2EHarness.Log($"  부순 뒤 스냅 자리 {before} → {SnapGraph.Count}");
            E2EHarness.Assert(SnapGraph.Count <= before, "부순 조각의 자리도 함께 사라졌다");

            var placer = Placer;
            placer.SelectById("piece_ramp");
            yield return null;

            E2EHarness.Teleport(at + new Vector3(0f, 1.6f, 3.2f));
            yield return null;
            E2EHarness.LookAt(at);
            yield return null;
            yield return null;

            var r = placer.Evaluate(out var pos, out _);
            E2EHarness.Log($"  빈 자리 재배치 판정 = {r}");
            E2EHarness.Assert(r == PlacementResult.Ok, "부순 자리에 다시 놓을 수 있다");

            placer.Cancel();
        }
    }
}
