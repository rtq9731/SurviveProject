using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Survive.Building;
using Survive.Combat;
using Survive.Creatures;
using Survive.Harvesting;
using Survive.Interaction;
using Survive.Items;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// <b>압박 곡선을 재는 자.</b> 고치지 않고 <b>잰다</b>.
    ///
    /// 랜턴 지속시간·오프셋·목재 용량 세 값은 기획서 §16이 사람의 몫으로 남긴 것이다.
    /// 그런데 사람이 고를 수 있으려면 <b>"이 값이면 무엇이 성립하는가"</b>가 숫자로
    /// 놓여 있어야 한다. 여기서 재는 것이 그 숫자다 — 판정하지 않고 적는다.
    ///
    /// 이 시나리오가 <b>단언을 거의 하지 않는 이유</b>가 그것이다. 실패는 곧
    /// "잴 수 없었다"는 뜻이고, 값이 나쁜 것은 실패가 아니다.
    /// </summary>
    public static class E2ETuningSurvey
    {
        static Vector3 _거점;
        static readonly List<string> _표 = new List<string>();

        public static IEnumerator FullRun()
        {
            _표.Clear();

            yield return 준비();
            yield return 왕복_사다리();
            yield return 채집_순회();
            yield return 본_원정();
            yield return 사각의_크기();
            yield return 자원_전수조사();
            yield return 산출();
            yield return 관찰의_보상();

            E2EHarness.Log("");
            E2EHarness.Log("═══ 실측표 ═══");
            foreach (var line in _표) E2EHarness.Log("  " + line);
            E2EHarness.Log("=== 압박 곡선 측량 완주 ===");
        }

        /// <summary>
        /// <b>관찰 보상 한 대목만 잰다.</b>
        ///
        /// 이 값은 튜닝 5값과 <b>같은 표</b>에 들어가야 한다 — "탐사 1분당 순증가"에
        /// 직접 얹히므로 따로 잡으면 두 번 맞춰야 한다(기획서 §5.2·§3.4). 그래서
        /// 자리는 여기이되, 왕복 사다리와 본 원정만으로 몇 분이 드는 <see cref="FullRun"/>을
        /// 이 한 항목 때문에 통째로 돌릴 이유는 없다. 문을 둘 낸다.
        /// </summary>
        public static IEnumerator PayoffRun()
        {
            _표.Clear();

            yield return 준비();
            yield return 관찰의_보상();

            E2EHarness.Log("");
            E2EHarness.Log("═══ 실측표 (관찰 보상) ═══");
            foreach (var line in _표) E2EHarness.Log("  " + line);
            E2EHarness.Log("=== 관찰 보상 측량 완주 ===");
        }

        static void 적는다(string 항목, string 값) => _표.Add($"{항목} | {값}");

        // ── 준비 ────────────────────────────────────────────────

        static IEnumerator 준비()
        {
            _거점 = E2EHarness.Player.transform.position;
            E2EHarness.Log($"— 거점 {_거점.ToString("F1")} (스폰 자리) —");
            적는다("거점 좌표", _거점.ToString("F1"));

            int 잠든것 = E2EHarness.SleepWildCreatures();
            E2EHarness.Log($"  야생 생물 {잠든것}마리를 재웠다 (재려는 것은 거리와 시간이다)");

            // 곡괭이·도끼가 있어야 부수는 노드까지 셀 수 있다.
            var db = E2EHarness.Player.Inventory.Database;
            var inv = E2EHarness.Player.Inventory.Inventory;
            foreach (var id in new[] { "pickaxe", "axe", LanternRule.ItemId })
            {
                var it = db != null ? db.GetById(id) : null;
                if (it != null && inv.CountOf(id) == 0) inv.TryAdd(it, 1);
            }
            yield return null;
        }

        // ── 1. 왕복 사다리 — 거리마다 몇 초가 드는가 ────────────

        /// <summary>
        /// <b>정찰 한 번이 몇 초인가에 답이 하나일 수 없다.</b> 얼마나 멀리 나가느냐에
        /// 따라 달라지기 때문이다. 그래서 하나를 재지 않고 <b>사다리</b>를 놓는다 —
        /// 10·20·30·40·60m를 각각 나갔다 돌아오며 실제로 잰다.
        ///
        /// 그러면 사람이 고를 것이 "지속시간 몇 초"가 아니라 <b>"반경 몇 미터까지
        /// 나갈 수 있게 할 것인가"</b>가 된다. 값이 아니라 <b>권한</b>을 고르는 것이고,
        /// 그것이 기획서 §16이 사람에게 남긴 판단의 모양이다.
        /// </summary>
        static IEnumerator 왕복_사다리()
        {
            E2EHarness.Log("— 왕복 사다리 (거점에서 나갔다 돌아온다) —");

            foreach (float 반경 in new[] { 10f, 20f, 30f, 40f, 60f })
            {
                var 목표 = 그_거리의_땅(반경);
                if (목표 == Vector3.zero)
                {
                    E2EHarness.Log($"  {반경:F0}m — 설 자리를 못 찾았다");
                    continue;
                }

                yield return 되돌린다();

                float 직선 = 평면거리(_거점, 목표);
                float t0 = Time.time;
                yield return E2EHarness.TryWalkTo(목표, 2.5f, 90f);
                bool 닿음 = E2EHarness.LastWalkArrived;
                float 가는데 = Time.time - t0;

                float t1 = Time.time;
                yield return E2EHarness.TryWalkTo(_거점, 2.5f, 90f);
                float 오는데 = Time.time - t1;

                float 합 = 가는데 + 오는데;
                float 배터리 = 합 * LanternRule.Tier1DrainPerSecond;
                E2EHarness.Log($"  {반경:F0}m ({목표.ToString("F0")}, 직선 {직선:F1}m) — " +
                               $"왕복 {합:F1}초 (감 {가는데:F1} / 옴 {오는데:F1}){(닿음 ? "" : " ※도달 실패")} · " +
                               $"배터리 {배터리:F0} · 실효 속도 {(합 > 0f ? 직선 * 2f / 합 : 0f):F2}m/s");
                적는다($"왕복 반경 {반경:F0}m",
                       $"직선 {직선:F1}m · 왕복 {합:F1}초 · 배터리 {배터리:F0}/100" +
                       $"{(닿음 ? "" : " ※도달 실패")}");
            }
        }

        /// <summary>
        /// 거점에서 그만큼 떨어진, 사람이 설 수 있는 <b>마른 땅</b>. 없으면 0.
        ///
        /// <b>소품 꼭대기를 고르면 안 된다.</b> 처음에는 광선이 맞은 것 중 가장 높은
        /// 곳을 골랐는데, 그러면 목표가 거대 버섯 갓 위(y 58~69)에 서고 사람은
        /// 그 밑에서 40번을 헛돌았다 — 실측 20m 왕복이 99초로 나온 것이 그것이다.
        /// 지금은 <b>섬 지형에 맞은 점</b>만 고른다.
        /// </summary>
        static Vector3 그_거리의_땅(float 반경)
        {
            for (int a = 0; a < 36; a++)
            {
                var dir = Quaternion.Euler(0f, a * 10f, 0f) * Vector3.forward;
                var xz = _거점 + dir * 반경;
                foreach (var h in Physics.RaycastAll(new Vector3(xz.x, 300f, xz.z), Vector3.down,
                                                     400f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!섬지형인가(h.collider.transform)) continue;
                    if (h.point.y < 50.4f || h.point.y > 56f) continue;   // 물속도 절벽 위도 아니다
                    return h.point + Vector3.up * 0.3f;
                }
            }
            return Vector3.zero;
        }

        /// <summary>이 콜라이더가 섬 지형인가. 루트는 Archipelago이므로 조상을 훑는다.</summary>
        static bool 섬지형인가(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
                if (p.name.StartsWith("Island")) return true;
            return false;
        }

        /// <summary>다음 눈금을 재기 전에 거점으로 되돌린다. 앞 눈금이 흘린 자리를 물려받지 않는다.</summary>
        static IEnumerator 되돌린다()
        {
            if (평면거리(_거점, E2EHarness.Player.transform.position) > 3f)
                E2EHarness.Teleport(_거점 + Vector3.up * 0.4f);
            yield return null;
        }

        // ── 2. 채집 순회 — 탐사 1분당 스크랩 ────────────────────

        /// <summary>
        /// <b>1분당 스크랩 순증가</b>가 양수인가 (회신 ①). 음수면 안 나가는 것이
        /// 최적해가 되어 게임이 멈춘다.
        ///
        /// <b>제자리 산출로는 답할 수 없다.</b> 노드 앞에 서 있으면 초당 몇 개가
        /// 나오는가는 대단히 크지만, 실제 탐사는 <b>걸어다니는 시간</b>이 대부분이다.
        /// 그래서 거점에서 가까운 순서로 여섯 곳을 <b>실제로 걸어</b> 돌고,
        /// 걸린 시간 전체로 나눈다. 캐는 시간은 도구 규칙에서 셈한다.
        /// </summary>
        static IEnumerator 채집_순회()
        {
            E2EHarness.Log("— 채집 순회 (실제로 걸어 도는 1분당 스크랩) —");

            yield return 되돌린다();

            var 곡괭이 = 도구("pickaxe");
            var 돈곳 = new List<HarvestNode>();
            float 스크랩 = 0f, 캐는데 = 0f;
            float t0 = Time.time;
            var 여기 = _거점;

            for (int i = 0; i < 6; i++)
            {
                var 다음 = 스크랩노드(여기, 돈곳);
                if (다음 == null) break;

                yield return E2EHarness.TryWalkTo(다음.transform.position, 2.5f, 60f);
                돈곳.Add(다음);
                여기 = 다음.transform.position;

                float 몫 = 기댓값(다음.Definition.drops, "scrap");
                float 초 = 캐는시간(다음.Definition, 곡괭이);
                스크랩 += 몫;
                캐는데 += 초;
                E2EHarness.Log($"    {다음.name} — 스크랩 {몫:F2} · 캐기 {초:F1}초 " +
                               $"(누적 {Time.time - t0 + 캐는데:F1}초)");
            }

            // 돌아오는 시간도 탐사의 일부다. 짐을 두고 와야 다음 순회가 시작된다.
            yield return E2EHarness.TryWalkTo(_거점, 2.5f, 90f);
            float 전체 = (Time.time - t0) + 캐는데;

            if (전체 <= 0f || 돈곳.Count == 0) { E2EHarness.Log("  잴 것이 없었다"); yield break; }

            float 분당산출 = 스크랩 / 전체 * 60f;
            float 분당비용 = HarvestRespawnRule.ScrapBurnedPerMinute(1);
            E2EHarness.Log($"  {돈곳.Count}곳 · 총 {전체:F1}초 (걷기 {전체 - 캐는데:F1} + 캐기 {캐는데:F1}) · " +
                           $"스크랩 {스크랩:F1}개");
            E2EHarness.Log($"  <b>분당 산출 {분당산출:F1} − 랜턴 비용 {분당비용:F1} = " +
                           $"순증가 {분당산출 - 분당비용:F1} 스크랩/분</b>");

            적는다("탐사 1분당 스크랩 (실제 순회)",
                   $"{돈곳.Count}곳 {전체:F1}초 · 산출 {분당산출:F1} − 랜턴 {분당비용:F1} = " +
                   $"<b>순증가 {분당산출 - 분당비용:F1}/분</b>");
            적는다("랜턴이 태우는 스크랩",
                   $"{LanternRule.Tier1DrainPerSecond:F2} 배터리/s ÷ {LanternRule.BatteryPerCell:F0} × 스크랩 " +
                   $"{HarvestRespawnRule.ScrapPerBatteryCell}개 = {분당비용 / 60f:F3}/s = {분당비용:F1}/분");
        }

        /// <summary>스크랩이 나오는 노드 중 여기서 가장 가깝고 아직 안 들른 곳.</summary>
        static HarvestNode 스크랩노드(Vector3 from, List<HarvestNode> 뺄것)
        {
            return Object.FindObjectsByType<HarvestNode>(FindObjectsInactive.Include)
                         .Where(n => n != null && n.Definition != null && !n.IsDepleted)
                         .Where(n => !뺄것.Contains(n))
                         .Where(n => 기댓값(n.Definition.drops, "scrap") > 0f)
                         .OrderBy(n => 평면거리(from, n.transform.position))
                         .FirstOrDefault();
        }

        // ── 3. 본 원정 ──────────────────────────────────────────

        /// <summary>
        /// <b>액면을 건넌다.</b> 건너편 뭍의 물가까지 갔다가 돌아온다.
        /// 기획서가 "수로 건너 내륙"이라 부르는 자리인데, 지금 씬에는 수로도 여울도
        /// 없고 <b>열린 액면</b> 하나뿐이다 — 그 사실 자체가 실측 결과다.
        /// </summary>
        static IEnumerator 본_원정()
        {
            E2EHarness.Log("— 본 원정 (거점 → 2번 섬 → 거점) —");

            // MacroniumSea가 실측으로 남긴 물가 좌표를 그대로 쓴다.
            var 이쪽물가 = 지면(new Vector3(40f, 0f, -2f));
            var 저쪽물가 = 지면(new Vector3(82f, 0f, 12f));
            var 섬2노드 = 가까운노드(저쪽물가, 0f);
            var 섬2목표 = 섬2노드 != null ? 섬2노드.transform.position : 저쪽물가;

            E2EHarness.Log($"  이쪽 물가 {이쪽물가.ToString("F1")} · 저쪽 물가 {저쪽물가.ToString("F1")} " +
                           $"(사이 {평면거리(이쪽물가, 저쪽물가):F1}m)");
            E2EHarness.Log($"  2번 섬 목표 {(섬2노드 != null ? 섬2노드.name : "물가")} {섬2목표.ToString("F1")}");

            // 바다는 몸을 문다. 재려는 것이 시간이지 죽음이 아니므로 계속 살려 둔다.
            var 간호 = E2ERunner.Instance != null
                ? E2ERunner.Instance.StartCoroutine(살려둔다())
                : null;

            float t0 = Time.time;
            yield return E2EHarness.TryWalkTo(이쪽물가, 3f, 60f);
            float 물가까지 = Time.time - t0;

            float t1 = Time.time;
            yield return E2EHarness.TryWalkTo(섬2목표, 4f, 120f);
            float 건너는데 = Time.time - t1;
            bool 닿음 = E2EHarness.LastWalkArrived;
            var 도착 = E2EHarness.Player.transform.position;

            float 캐는데 = 섬2노드 != null && 닿음 ? 캐는시간(섬2노드) : 0f;

            float t2 = Time.time;
            yield return E2EHarness.TryWalkTo(_거점, 3f, 180f);
            float 돌아오는데 = Time.time - t2;
            bool 복귀 = E2EHarness.LastWalkArrived;

            if (간호 != null && E2ERunner.Instance != null) E2ERunner.Instance.StopCoroutine(간호);

            float 합 = 물가까지 + 건너는데 + 캐는데 + 돌아오는데;
            E2EHarness.Log($"  물가까지 {물가까지:F1}초 · 건너기 {건너는데:F1}초" +
                           $"{(닿음 ? "" : $" (도달 실패, {도착.ToString("F1")}에서 멈춤)")} · " +
                           $"캐는 데 {캐는데:F1}초 · 복귀 {돌아오는데:F1}초{(복귀 ? "" : " (실패)")}");
            E2EHarness.Log($"  <b>본 원정 한 번 = {합:F1}초</b> · " +
                           $"배터리 {합 * LanternRule.Tier1DrainPerSecond:F0} " +
                           $"= 셀 {합 * LanternRule.Tier1DrainPerSecond / LanternRule.BatteryPerCell:F1}개");

            적는다("본 원정 (2번 섬 왕복)",
                   $"왕복 {합:F1}초 · 배터리 {합 * LanternRule.Tier1DrainPerSecond:F0} " +
                   $"= 셀 {합 * LanternRule.Tier1DrainPerSecond / LanternRule.BatteryPerCell:F1}개" +
                   $"{(닿음 && 복귀 ? "" : " ※ 도달/복귀 실패 포함")}");

            // 3번 섬은 걸어서 재지 않는다 — 열린 바다 두 번이라 죽거나 끼는 것이
            // 시간을 오염시킨다. 거리로만 적어 사람이 환산할 수 있게 한다.
            var 섬3 = 지면(new Vector3(210f, 0f, 28f));
            float 섬3직선 = 평면거리(_거점, 섬3);
            적는다("3번 섬 (걷지 않고 거리만)",
                   $"거점에서 직선 {섬3직선:F0}m · 왕복 최소 {섬3직선 * 2f / 5f:F0}초 (도보 5m/s 환산)");
            E2EHarness.Log($"  3번 섬 상륙 예정지 {섬3.ToString("F1")} — 직선 {섬3직선:F0}m, " +
                           $"왕복 도보 환산 {섬3직선 * 2f / 5f:F0}초");
        }

        /// <summary>바다가 깎는 체력을 채워 둔다. 재려는 것이 시간이지 생존이 아니다.</summary>
        static IEnumerator 살려둔다()
        {
            while (true)
            {
                var h = E2EHarness.Player.Vitals.Health;
                if (h.Current < h.Max * 0.9f) h.Modify(h.Max);
                yield return null;
            }
        }

        // ── 4. 사각의 크기 ──────────────────────────────────────

        /// <summary>
        /// <b>사각을 정의대로 잰다</b> — 랜턴 빛 밖이면서 낫이 닿을 수 있는 곳.
        /// 규칙이 내놓는 넓이·각도와, 세계를 실제로 격자로 훑어 센 넓이를 나란히 둔다.
        /// 둘이 갈리면 규칙이 화면과 다른 말을 하고 있다는 뜻이다.
        /// </summary>
        static IEnumerator 사각의_크기()
        {
            E2EHarness.Log("— 사각의 크기 (빛 밖 ∩ 낫 사거리 안) —");

            var lantern = E2EHarness.Lantern;
            if (lantern == null) { E2EHarness.Log("  랜턴이 없다"); yield break; }

            E2EHarness.MuteAmbientLitZones();
            yield return null;
            E2EHarness.LightLantern();
            yield return null;
            yield return null;

            int 티어 = lantern.Tier;
            float r = LanternRule.RadiusForTier(티어);
            float o = LanternRule.OffsetForTier(티어);
            float 뒤끝 = LanternRule.BackReachForTier(티어);
            float 사거리 = 낫사거리();

            float 규칙넓이 = LanternRule.DarkStrikeArea(r, o, 사거리);
            float 규칙각도 = LanternRule.DarkStrikeAngle(r, o, 사거리);
            float 여유 = LanternRule.DarkStrikeMargin(r, o, 사거리);

            E2EHarness.Log($"  반경 {r:F1}m · 오프셋 {o:F1}m · 등 뒤 도달 {뒤끝:F1}m · 낫 사거리 {사거리:F2}m");
            E2EHarness.Log($"  <b>여유 = 사거리 − 등 뒤 도달 = {사거리:F2} − {뒤끝:F2} = {여유:F2}m</b>");
            E2EHarness.Log($"  규칙이 말하는 사각: 넓이 {규칙넓이:F2}m² · 각도 {규칙각도:F1}도");

            // 실제 세계를 훑는다. 사람 발치 평면에서 5cm 격자로 센다.
            var 자리 = lantern.LitAnchor;
            var 앞 = lantern.LitForward;
            var 옆 = Vector3.Cross(Vector3.up, 앞);
            const float 눈금 = 0.05f;
            int 어두운칸 = 0, 전체칸 = 0, 사각으로읽힌칸 = 0;
            for (float a = -사거리; a <= 사거리; a += 눈금)
                for (float b = -사거리; b <= 사거리; b += 눈금)
                {
                    if (a * a + b * b > 사거리 * 사거리) continue;
                    전체칸++;
                    var p = 자리 + 앞 * a + 옆 * b;
                    if (!LitZoneRegistry.IsLit(p)) 어두운칸++;
                    if (LitZoneRegistry.IsBlindSide(p)) 사각으로읽힌칸++;
                }

            float 잰넓이 = 어두운칸 * 눈금 * 눈금;
            float 읽힌넓이 = 사각으로읽힌칸 * 눈금 * 눈금;
            E2EHarness.Log($"  실측 훑기: 사거리 원 {전체칸 * 눈금 * 눈금:F2}m² 중 " +
                           $"<b>빛 밖 {잰넓이:F2}m²</b> · IsBlindSide가 참인 곳 {읽힌넓이:F2}m²");

            E2EHarness.Assert(Mathf.Abs(잰넓이 - 규칙넓이) < 0.4f,
                              $"규칙과 세계가 같은 넓이를 말한다 (규칙 {규칙넓이:F2} / 실측 {잰넓이:F2})");

            적는다("사각 (빛 밖 ∩ 낫 사거리)",
                   $"넓이 {잰넓이:F2}m² · 각도 {규칙각도:F1}도 · 여유 {여유:F2}m");
            적는다("IsBlindSide가 참인 넓이 (같은 원 안)",
                   $"{읽힌넓이:F2}m² / {전체칸 * 눈금 * 눈금:F2}m²");

            yield return 사각이_등_뒤로_얼마나_뻗는가(자리, 앞, 옆, r, o, 사거리);

            // 오프셋을 얼마나 밀면 사각이 열리는가.
            float 필요오프셋 = LanternRule.OffsetThatOpensDark(r, 사거리);
            float 필요사거리 = LanternRule.ReachThatOpensDark(r, o);
            E2EHarness.Log($"  사각이 열리려면: 오프셋 > {필요오프셋:F2}m (지금 {o:F2}) " +
                           $"또는 낫 사거리 > {필요사거리:F2}m (지금 {사거리:F2})");
            적는다("사각이 열리는 문턱",
                   $"오프셋 > {필요오프셋:F2}m 또는 낫 사거리 > {필요사거리:F2}m");

            E2EHarness.RestoreAmbientLitZones();
            yield return null;
        }

        /// <summary>
        /// <b>사각이 등 뒤로 어디까지 뻗는가.</b> 낫 사거리 안만 훑으면 이 물음에
        /// 답할 수 없다 — 사거리(2.2m)가 재려는 것보다 짧아서, 그 안에서는 반평면이든
        /// 원이든 똑같이 "뒤는 전부 참"으로 보인다.
        ///
        /// 그래서 여기서는 <b>훨씬 멀리까지</b> 뒤로 걸어 나가며 IsBlindSide가
        /// 언제 거짓이 되는지를 찾는다. 지금 이 값은 <b>무한</b>이어야 한다
        /// (반평면이므로 뒤로는 끝이 없다). 다음 라운드가 사각을 원형 범위로
        /// 고치면 여기가 유한한 숫자로 바뀌고, <b>그 차이가 그 변경의 크기다</b>.
        /// 재는 자를 미리 놓아 두는 것이 이 함수가 하는 일의 전부다.
        /// </summary>
        static IEnumerator 사각이_등_뒤로_얼마나_뻗는가(Vector3 자리, Vector3 앞, Vector3 옆,
                                                     float r, float o, float 사거리)
        {
            const float 눈금 = 0.1f;
            const float 끝까지 = 40f;

            float 뒤끝 = -1f;
            for (float d = 눈금; d <= 끝까지; d += 눈금)
            {
                if (!LitZoneRegistry.IsBlindSide(자리 - 앞 * d)) { 뒤끝 = d; break; }
            }

            // 옆으로도 같은 것을 잰다. 반평면이면 사람 바로 옆(정확히 90도)에서
            // 끊기고, 원이면 그 원의 반지름에서 끊긴다.
            float 옆끝 = -1f;
            for (float d = 눈금; d <= 끝까지; d += 눈금)
            {
                if (!LitZoneRegistry.IsBlindSide(자리 - 앞 * 0.1f + 옆 * d)) { 옆끝 = d; break; }
            }

            string 뒤말 = 뒤끝 < 0f ? $"{끝까지:F0}m까지 계속 참 (끝이 없다 = 반평면)" : $"{뒤끝:F1}m에서 끊긴다";
            string 옆말 = 옆끝 < 0f ? $"{끝까지:F0}m까지 계속 참" : $"{옆끝:F1}m에서 끊긴다";

            E2EHarness.Log($"  IsBlindSide의 등 뒤 도달: {뒤말} · 옆으로: {옆말}");
            E2EHarness.Log($"    (견줄 값 — 반경 {r:F1}m · 오프셋 {o:F1}m · 낫 사거리 {사거리:F2}m)");

            적는다("IsBlindSide가 등 뒤로 뻗는 거리", 뒤말);
            적는다("IsBlindSide가 옆으로 뻗는 거리", 옆말);

            yield return null;
        }

        static float 낫사거리()
        {
            var def = Resources.FindObjectsOfTypeAll<Survive.Creatures.CreatureDefinitionSO>()
                               .FirstOrDefault(d => d != null && d.id == "scythe");
            return def != null ? def.attackRange : 2.2f;
        }

        // ── 4-b. 자원 전수 조사 ─────────────────────────────────

        /// <summary>
        /// <b>거점에서 얼마나 나가야 얼마가 모이는가.</b> 반경 띠마다 스크랩을 센다.
        /// "깊이에 비례해 커지는가"(회신 ①)에 답하려면 산출이 <b>거리의 함수</b>로
        /// 놓여 있어야 하고, 놓아 보면 그렇지 않다는 것도 답이다.
        /// </summary>
        static IEnumerator 자원_전수조사()
        {
            E2EHarness.Log("— 자원 전수 조사 (거점에서의 거리별 스크랩) —");

            var 곡괭이 = 도구("pickaxe");
            var 노드들 = Object.FindObjectsByType<HarvestNode>(FindObjectsInactive.Include)
                               .Where(n => n != null && n.Definition != null)
                               .Where(n => 기댓값(n.Definition.drops, "scrap") > 0f)
                               .ToList();

            float 총스크랩 = 0f, 총캐기 = 0f, 최원 = 0f;
            foreach (float 띠 in new[] { 10f, 20f, 30f, 50f, 100f, 400f })
            {
                var 안쪽 = 노드들.Where(n => 평면거리(_거점, n.transform.position) <= 띠).ToList();
                float 몫 = 안쪽.Sum(n => 기댓값(n.Definition.drops, "scrap"));
                float 초 = 안쪽.Sum(n => 캐는시간(n.Definition, 곡괭이));
                E2EHarness.Log($"  반경 {띠:F0}m 안 — 노드 {안쪽.Count}곳 · 스크랩 {몫:F1}개 · 캐는 시간 합 {초:F1}초");
                적는다($"반경 {띠:F0}m 안의 스크랩", $"노드 {안쪽.Count}곳 · {몫:F1}개");
                총스크랩 = 몫; 총캐기 = 초;
            }
            foreach (var n in 노드들) 최원 = Mathf.Max(최원, 평면거리(_거점, n.transform.position));

            E2EHarness.Log($"  <b>세계에 서 있는 스크랩 {총스크랩:F1}개</b>, 가장 먼 것이 {최원:F1}m");

            // 재고와 <b>소득</b>은 다른 것이다. 재고는 한 번 캐면 끝이고, 압박 곡선을
            // 정하는 것은 재생이 내놓는 분당 산출이다.
            float 공급 = 0f;
            foreach (var grp in 노드들.GroupBy(n => n.Definition))
                공급 += HarvestRespawnRule.SupplyPerMinute(
                    기댓값(grp.Key.drops, "scrap"), grp.Count(), grp.Key.respawnSeconds);
            float 소모 = HarvestRespawnRule.ScrapBurnedPerMinute(1);

            E2EHarness.Log($"  <b>정상 상태 공급 {공급:F2}/분 − 랜턴 {소모:F2}/분 = " +
                           $"{공급 - 소모:F2}/분</b> (공급이 소모의 {공급 / 소모:F2}배)");
            적는다("스크랩의 지리", $"서 있는 재고 {총스크랩:F1}개 · 가장 먼 노드 {최원:F1}m — " +
                                    "재생을 켜도 <b>거리에 비례해 커지지는 않는다</b>(배치의 몫, §16)");
            적는다("스크랩 정상 상태 공급",
                   $"분당 {공급:F2}개 − 랜턴 {소모:F2} = 순증가 {공급 - 소모:F2}/분 " +
                   $"(소모의 {공급 / 소모:F2}배)");

            yield return null;
        }

        // ── 5. 산출 — 탐사 1분당 스크랩 ─────────────────────────

        static IEnumerator 산출()
        {
            E2EHarness.Log("— 산출 (탐사 1분당 스크랩) —");

            var 곡괭이 = 도구("pickaxe");
            var 도끼 = 도구("axe");

            var 노드들 = Object.FindObjectsByType<HarvestNode>(FindObjectsInactive.Include);
            foreach (var grp in 노드들.Where(n => n != null && n.Definition != null)
                                      .GroupBy(n => n.Definition))
            {
                var def = grp.Key;
                float 스크랩 = 기댓값(def.drops, "scrap");
                float 부품 = 기댓값(def.drops, "machine_part");
                float 초 = 캐는시간(def, 곡괭이);
                E2EHarness.Log($"  {def.name} x{grp.Count()} — 스크랩 {스크랩:F2}개 · " +
                               $"부품 {부품:F2}개 · {초:F1}초 → 분당 스크랩 {스크랩 / 초 * 60f:F1}");
                적는다($"채집물 {def.name}",
                       $"{grp.Count()}곳 · 스크랩 {스크랩:F2}/개 · {초:F1}초 → 제자리 분당 {스크랩 / 초 * 60f:F1}");
            }

            // ── 벌목 — 목재는 머무는 값이다 ─────────────────────
            // <b>분당 산출을 도끼질 시간으로 나누면 안 된다.</b> 그렇게 세면 분당
            // 375개가 나오는데, 실제 상한을 정하는 것은 도끼질이 아니라
            // <b>다시 자라는 시간</b>이다. 한 그루가 300초에 한 번만 5개를 준다.
            float 벌목초 = 스윙수(MushroomLumberRule.Durability, 도끼) * 스윙간격(도끼);
            float 목재 = (MushroomLumberRule.MinYield + MushroomLumberRule.MaxYield) * 0.5f;
            float 그루당불 = 목재 * CampfireFuelRule.SecondsPerLog;
            float 한그루지속 = 그루당불 / MushroomLumberRule.RegrowSeconds;
            int 나무수 = Object.FindObjectsByType<HarvestNode>(FindObjectsInactive.Include)
                               .Count(n => n != null && n.Definition != null &&
                                           MushroomLumberRule.IsGiant(n.name));
            E2EHarness.Log($"  거대 버섯 1그루 — 목재 {목재:F1}개 · 도끼질 {벌목초:F1}초 · " +
                           $"불 {그루당불:F0}초");
            E2EHarness.Log($"  재생 {MushroomLumberRule.RegrowSeconds:F0}초 → 한 그루가 유지하는 불은 " +
                           $"{한그루지속:F2}배 — <b>불 하나를 끊이지 않게 하려면 " +
                           $"{Mathf.CeilToInt(1f / 한그루지속)}그루가 필요하다</b>");
            E2EHarness.Log($"  씬에 선 거대 버섯 {나무수}그루 → 동시에 유지 가능한 불 " +
                           $"{나무수 * 한그루지속:F1}개 (벌목 왕복 시간은 뺀 상한)");
            적는다("벌목 (거대 버섯 1그루)",
                   $"목재 {목재:F1}개 · 도끼질 {벌목초:F1}초 · 불 {그루당불:F0}초 · 재생 " +
                   $"{MushroomLumberRule.RegrowSeconds:F0}초 → 불 하나에 {Mathf.CeilToInt(1f / 한그루지속)}그루");
            적는다("씬의 거대 버섯", $"{나무수}그루 → 상한 불 {나무수 * 한그루지속:F1}개 동시");

            적는다("화톳불",
                   $"용량 {CampfireFuelRule.CapacityLogs}개 = {CampfireFuelRule.MaxFuelSeconds:F0}초 " +
                   $"({CampfireFuelRule.MaxFuelSeconds / 60f:F1}분) · 불씨 {CampfireFuelRule.StarterLogs}개 " +
                   $"= {CampfireFuelRule.StarterFuelSeconds:F0}초");
            적는다("랜턴",
                   $"가득 {LanternRule.FullBatterySecondsAtTier1:F1}초 · 소모 {LanternRule.Tier1DrainPerSecond:F2}/s · " +
                   $"셀 1개 = 스크랩 5개 = {LanternRule.BatteryPerCell / LanternRule.Tier1DrainPerSecond:F1}초");

            yield return null;
        }

        // ── 6. 관찰의 보상 ──────────────────────────────────────

        /// <summary>
        /// <b>생태계를 읽으면 얼마나 이득인가</b> (기획서 §3.4).
        ///
        /// 순환 네 단계는 이미 코드로 돈다 — 식물이 자라고, 생산자가 먹어 쌓고,
        /// 잡으면 쌓은 만큼 나오고, 방치하면 분해자가 가져간다. <b>남은 것은
        /// 이득의 크기다.</b> 배부른 개체를 골라 두세 개 더 나오는 정도라면 아무도
        /// 안 고르고, 그러면 구현되어 있으되 없는 것과 같아진다.
        ///
        /// 그래서 다섯을 잰다 — 지금 배율, 배부르기까지의 시간, 분해자가 가져가는
        /// 시간, 눈으로 읽히는 정도, 그리고 「기다린 시간 대 더 얻은 것」.
        /// <b>고르지는 않는다.</b> 여기서 하는 일은 사람이 고를 수 있도록 숫자를
        /// 놓아 두는 것까지다.
        /// </summary>
        static IEnumerator 관찰의_보상()
        {
            E2EHarness.Log("— 관찰의 보상 (굶은 개체 대 배부른 개체, §3.4) —");

            var 원본 = PayoffGlowProbe.생산자원본();
            if (원본 == null) { E2EHarness.Log("  씬에 생산자가 없다"); yield break; }

            var def = 원본.GetComponent<CreatureHealth>().Definition;
            float 정원 = 원본.Capacity;
            float 쿨다운 = 원본.BiteCooldown;
            float 기본 = 기댓값(def.drops, "scrap");

            yield return 규칙이_말하는_배율(def, 기본, 정원, 쿨다운);
            yield return 몇_초에_배부른가(원본, 정원);
            yield return 나란히_세워_잡는다(원본, 기본);
        }

        /// <summary>
        /// 먼저 규칙에 묻는다. 세계에서 잰 값과 나란히 두어야, 갈릴 때 어느 쪽이
        /// 거짓말을 하고 있는지 짚을 수 있다.
        /// </summary>
        static IEnumerator 규칙이_말하는_배율(CreatureDefinitionSO def, float 기본,
                                              float 정원, float 쿨다운)
        {
            int 얹히는것 = FeedingPayoff.Bonus(정원);
            float 배율 = FeedingPayoff.Multiplier(기본, 정원);

            E2EHarness.Log($"  {def.displayName}: 기본 스크랩 기댓값 {기본:F2}개 · 정원 {정원:F0} · " +
                           $"한 입 사이 {쿨다운:F0}초");
            E2EHarness.Log($"  <b>가득 찬 개체는 +{얹히는것}개 = {배율:F2}배</b> " +
                           $"(축적 1당 스크랩 {FeedingPayoff.ScrapPerNutrition:F0}개)");
            적는다("관찰 보상 — 규칙이 말하는 배율",
                   $"{def.displayName} 기본 {기본:F2}개 → 가득 {기본 + 얹히는것:F2}개 " +
                   $"(<b>+{얹히는것} · {배율:F2}배</b>)");

            // 종마다 기본이 다르므로 배율도 다르다. 배율만 보면 안 되는 자리다 —
            // 실제로 더 얻는 개수는 같은데 기본이 작은 종이 더 좋아 보인다.
            foreach (var 종 in Resources.FindObjectsOfTypeAll<CreatureDefinitionSO>()
                                        .Where(d => d != null && d.drops != null)
                                        .Where(d => 기댓값(d.drops, "scrap") > 0f)
                                        .OrderBy(d => d.id))
            {
                float b = 기댓값(종.drops, "scrap");
                E2EHarness.Log($"    {종.id}: 기본 {b:F2} → 가득 {b + 얹히는것:F2} " +
                               $"({FeedingPayoff.Multiplier(b, 정원):F2}배)");
            }

            // 먹이별로 몇 입인지가 갈린다. 「몇 초 기다리는가」의 바닥이 여기서 난다.
            foreach (var 식물 in Resources.FindObjectsOfTypeAll<PlantNodeSO>()
                                          .Where(p => p != null).OrderBy(p => p.name))
            {
                int 입 = FeedingPayoff.BitesToFull(정원, 식물.nutritionPerStage);
                float 초 = FeedingPayoff.SecondsToFull(정원, 식물.nutritionPerStage, 쿨다운);
                float 분당 = FeedingPayoff.BonusPerMinute(정원, 식물.nutritionPerStage, 쿨다운);
                E2EHarness.Log($"    {식물.name}(영양 {식물.nutritionPerStage:F0}, " +
                               $"단계 {식물.maxStage}, 성장 {식물.growSeconds:F0}초) — " +
                               $"{입}입 · 바닥 {초:F0}초 · 분당 이득 {분당:F1}개");
                적는다($"관찰 보상 — {식물.name}으로 배 채우기",
                       $"{입}입 · 먹는 시간만 {초:F0}초 · 분당 이득 {분당:F1}개 " +
                       $"(찾아가기·재성장 제외한 <b>바닥</b>)");
            }

            yield return null;
        }

        /// <summary>
        /// <b>실제로 먹여서 잰다.</b> 규칙이 내놓는 초는 입과 입 사이만 센 바닥이고,
        /// 세계에서는 찾아가는 시간과 식물이 다시 자라는 시간이 얹힌다. 그 얹힌
        /// 만큼이 「기다림」의 실체이므로 재지 않으면 값어치를 셈할 수 없다.
        ///
        /// 식물을 전부 다 자란 상태로 되돌리고 시작한다. 반쯤 뜯긴 군락에서 재면
        /// 「먹는 데 걸리는 시간」이 아니라 「앞선 판이 남긴 것」을 재게 된다.
        /// </summary>
        static IEnumerator 몇_초에_배부른가(CreatureFeeding 원본, float 정원)
        {
            int 되돌린식물 = 0;
            foreach (var p in Object.FindObjectsByType<PlantNode>(FindObjectsInactive.Include))
            {
                if (p == null) continue;
                p.RestoreWorld(null);
                되돌린식물++;
            }
            E2EHarness.Log($"  식물 {되돌린식물}그루를 다 자란 상태로 되돌렸다");

            yield return 닿을_수_있는_식물이_몇_그루인가(원본);

            원본.Prefill(0f);

            // 이 한 마리만 깨운다. 다른 생산자가 함께 먹으면 같은 군락을 나눠 먹게
            // 되어, 재는 것이 「한 마리가 배부르는 시간」이 아니게 된다.
            var brain = 원본.GetComponent<CreatureBrain>();
            if (brain != null) brain.enabled = true;
            var agent = 원본.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.isStopped = false;

            const float 제한 = 150f;
            float t0 = Time.time;
            float 이전 = 0f;
            var 입시각 = new List<float>();

            while (Time.time - t0 < 제한 && !원본.IsFull)
            {
                if (원본.Stored > 이전 + 0.001f)
                {
                    입시각.Add(Time.time - t0);
                    이전 = 원본.Stored;
                }
                yield return null;
            }

            float 걸린 = Time.time - t0;
            bool 찼다 = 원본.IsFull;
            string 간격 = 입시각.Count == 0 ? "없음"
                        : string.Join(", ", 입시각.Select(s => s.ToString("F1")));

            E2EHarness.Log($"  {(찼다 ? "배가 찼다" : $"{제한:F0}초 안에 다 못 채웠다")} — " +
                           $"{입시각.Count}입 · 축적 {원본.Stored:F1}/{정원:F0} · {걸린:F1}초");
            E2EHarness.Log($"    입을 댄 시각(초): {간격}");

            // 한 입도 못 먹었으면 <b>왜</b>인지가 곧 결과다. "느리다"와 "닿지 못한다"는
            // 전혀 다른 이야기인데, 0이라는 숫자 하나로는 갈리지 않는다.
            if (입시각.Count == 0) 왜_못_먹었나(원본);

            float 실측분당 = 걸린 > 0f ? FeedingPayoff.Bonus(원본.Stored) / 걸린 * 60f : 0f;
            E2EHarness.Log($"  <b>실측 분당 이득 {실측분당:F1} 스크랩/분</b> " +
                           $"(기다린 {걸린:F0}초에 +{FeedingPayoff.Bonus(원본.Stored)}개)");

            적는다("관찰 보상 — 배부르기까지 (실측)",
                   찼다 ? $"{걸린:F0}초 · {입시각.Count}입 → +{FeedingPayoff.Bonus(원본.Stored)}개 " +
                          $"(<b>분당 {실측분당:F1}개</b>)"
                        : $"{제한:F0}초에 {원본.Stored:F1}/{정원:F0}만 찼다 · {입시각.Count}입 " +
                          $"(분당 {실측분당:F1}개)");
        }

        /// <summary>
        /// 굶은 채로 멈춰 선 개체가 <b>무엇 앞에서</b> 멈췄는지 적는다.
        /// 수평으로 코앞인데 높이차 때문에 못 먹는 것이 이 순환의 고질이라,
        /// 두 값을 따로 적어야 「배치」와 「사거리」가 갈린다.
        /// </summary>
        static void 왜_못_먹었나(CreatureFeeding 원본)
        {
            var 몸 = 원본.transform.position;
            var 가까운것 = Object.FindObjectsByType<PlantNode>(FindObjectsInactive.Exclude)
                              .Where(p => p != null && p.IsEdible)
                              .OrderBy(p => Vector3.Distance(p.transform.position, 몸))
                              .FirstOrDefault();
            var 모터 = 원본.GetComponent<ICreatureMotor>();
            float 순항 = 모터 != null ? 모터.CruiseHeight : 0f;

            if (가까운것 == null) { E2EHarness.Log("    [왜 못 먹었나] 먹을 수 있는 식물이 없다"); return; }

            var 밥 = 가까운것.transform.position;
            float 수평 = Vector2.Distance(new Vector2(몸.x, 몸.z), new Vector2(밥.x, 밥.z));
            E2EHarness.Log($"    [왜 못 먹었나] {원본.name} {몸.ToString("F1")} → " +
                           $"{가까운것.name} {밥.ToString("F1")} · 수평 {수평:F2}m · " +
                           $"높이차 {몸.y - 밥.y:F2}m · 순항 고도 {순항:F1}m " +
                           "(수평이 코앞인데 높이차가 크면 <b>배치</b>가 막은 것이다)");
            적는다("관찰 보상 — 굶은 채 멈춘 자리",
                   $"수평 {수평:F2}m · 높이차 {몸.y - 밥.y:F2}m — " +
                   "<b>바로 위 선반에서 멈춘다</b>");
        }

        /// <summary>
        /// <b>세워 놓은 식물 가운데 실제로 닿을 수 있는 것이 몇 그루인가.</b>
        ///
        /// 사거리를 원기둥으로 고친 뒤에도 순환의 속도는 <b>배치</b>가 정한다.
        /// 걷는 개체는 NavMesh 위로만 다니는데, 식물이 거대 버섯 갓 아래나
        /// 턱 밑에 서 있으면 그 위 선반으로 올라가 <b>바로 위에서 굶는다</b> —
        /// 실측에서 열매게가 재양치 수평 1.75m·높이 4.57m 위에 서서 멈춰 있었다.
        ///
        /// 그래서 그루 수를 센다. 이 수가 곧 순환이 돌 수 있는 자리의 수이고,
        /// 사거리를 고쳐도 이 수가 작으면 이득은 여전히 안 생긴다.
        /// <b>고치는 것은 배치이므로 사람의 몫(§16)이다.</b> 여기서는 센다.
        /// </summary>
        static IEnumerator 닿을_수_있는_식물이_몇_그루인가(CreatureFeeding 원본)
        {
            float 사거리 = 1.6f;                       // CreatureFeeding의 기본 먹이 사거리
            var 식물들 = Object.FindObjectsByType<PlantNode>(FindObjectsInactive.Exclude)
                              .Where(p => p != null && p.IsEdible).ToList();

            int 닿는것 = 0;
            var 좋은자리 = new List<(PlantNode 식물, Vector3 자리)>();

            foreach (var p in 식물들)
            {
                if (!NavMesh.SamplePosition(p.transform.position, out var hit, 3f, NavMesh.AllAreas))
                    continue;

                if (!CreatureDecision.IsWithinReach(hit.position, p.transform.position, 사거리, 사거리))
                    continue;

                닿는것++;
                좋은자리.Add((p, hit.position));
            }

            E2EHarness.Log($"  <b>걷는 개체가 닿을 수 있는 식물 {닿는것}/{식물들.Count}그루</b> " +
                           "(NavMesh 위 가장 가까운 자리에서 원기둥 사거리 안)");
            적는다("관찰 보상 — 닿을 수 있는 식물",
                   $"{닿는것}/{식물들.Count}그루 — 나머지는 갓·턱 아래라 걷는 개체가 " +
                   "<b>바로 위에서 굶는다</b>(배치의 몫, §16)");

            // 실제로 먹을 수 있는 자리 하나를 골라, 거기서 조금 떨어뜨려 놓는다.
            // 지형 운에 맡기면 재는 것이 「먹는 속도」가 아니라 「자리를 잘 만났는가」가 된다.
            if (좋은자리.Count > 0)
            {
                var 목표 = 좋은자리[0];
                var agent = 원본.GetComponent<NavMeshAgent>();
                var 출발 = 목표.자리 + new Vector3(6f, 0f, 0f);
                if (NavMesh.SamplePosition(출발, out var s, 6f, NavMesh.AllAreas)) 출발 = s.position;

                if (agent != null && agent.isActiveAndEnabled) agent.Warp(출발);
                else 원본.transform.position = 출발;

                E2EHarness.Log($"  {원본.name}를 {목표.식물.name} 곁 " +
                               $"{Vector3.Distance(출발, 목표.자리):F1}m에 놓고 시작한다");
            }
            else
            {
                E2EHarness.Log("  닿을 수 있는 식물이 하나도 없다 — 있는 자리에서 그대로 잰다");
            }

            yield return null;
        }

        /// <summary>
        /// <b>나란히 세워 놓고 밝기를 읽고, 그대로 둘 다 잡는다.</b>
        ///
        /// 따로 재면 안 된다 — 두 장을 따로 찍으면 노출도 각도도 달라 "더 밝다"를
        /// 말할 수 없고, 따로 잡으면 자리가 달라 전리품 난수(세계 시드는 <b>죽은
        /// 자리</b>로 파생한다)까지 갈린다. 같은 무대에서 이어 재는 것이 요점이다.
        ///
        /// 마지막에 분해자를 붙인다. 쌓인 전리품이 <b>몇 초 만에 사라지는가</b>가
        /// 곧 「경쟁」의 실체이고, 그것은 잡은 뒤에만 잴 수 있다.
        /// </summary>
        static IEnumerator 나란히_세워_잡는다(CreatureFeeding 원본, float 기본)
        {
            string 무대 = PayoffGlowProbe.Stage();
            E2EHarness.Log("  무대: " + 무대);
            if (무대.Contains("error")) yield break;

            // Cinemachine이 카메라를 옮기는 데 몇 프레임 걸린다. 옮기기 전에 재면
            // 두 개체가 화면 밖이라 조각이 배경만 담는다.
            for (int i = 0; i < 8; i++) yield return null;

            PayoffGlowProbe.SetLantern(true);
            for (int i = 0; i < 3; i++) yield return null;
            string 켠것 = PayoffGlowProbe.Measure();

            PayoffGlowProbe.SetLantern(false);
            for (int i = 0; i < 3; i++) yield return null;
            string 끈것 = PayoffGlowProbe.Measure();

            E2EHarness.Log("  랜턴 켬 — " + 켠것);
            E2EHarness.Log("  랜턴 끔 — " + 끈것);
            적는다("관찰 보상 — 눈으로 읽히는가 (랜턴 켬)", 켠것);
            적는다("관찰 보상 — 눈으로 읽히는가 (랜턴 끔, 발광만)", 끈것);

            PayoffGlowProbe.SetLantern(true);
            yield return null;

            var 굶 = PayoffGlowProbe.Starved;
            var 배 = PayoffGlowProbe.Full;
            if (굶 == null || 배 == null) { E2EHarness.Log("  무대가 비었다"); yield break; }

            var 자리 = 배.transform.position;

            int 굶스크랩 = 0, 배스크랩 = 0;
            yield return 잡아서_센다(굶, "굶은 개체", n => 굶스크랩 = n);
            yield return 잡아서_센다(배, "배부른 개체", n => 배스크랩 = n);

            float 실측배율 = 굶스크랩 > 0 ? (float)배스크랩 / 굶스크랩 : 0f;
            E2EHarness.Log($"  <b>실측: 굶은 것 {굶스크랩}개 대 배부른 것 {배스크랩}개 " +
                           $"= {실측배율:F2}배 (차이 +{배스크랩 - 굶스크랩}개)</b>");
            E2EHarness.Assert(배스크랩 > 굶스크랩,
                              $"배부른 개체가 실제로 더 떨군다 ({굶스크랩} → {배스크랩})");

            적는다("관찰 보상 — 실측 배율 (나란히 잡음)",
                   $"굶음 {굶스크랩}개 → 가득 {배스크랩}개 = <b>{실측배율:F2}배 · +{배스크랩 - 굶스크랩}개</b> " +
                   $"(규칙 기댓값 {기본:F2} → {기본 + FeedingPayoff.Bonus(배.Capacity):F2})");

            yield return 분해자가_언제_가져가는가(자리);

            PayoffGlowProbe.Teardown();
        }

        /// <summary>한 마리를 잡고 그 자리에 떨어진 스크랩을 센다.</summary>
        static IEnumerator 잡아서_센다(CreatureFeeding who, string 이름, System.Action<int> 결과)
        {
            var hp = who.GetComponent<CreatureHealth>();
            var 자리 = who.transform.position;
            float 축적 = who.Stored;

            var 이전 = new HashSet<ItemPickup>(
                Object.FindObjectsByType<ItemPickup>());

            hp.TakeDamage(new DamageInfo(hp.Definition.maxHealth * 3f, null, 자리, Vector3.up));

            // 떨군 것이 튀어 오르고 내려앉는 데 시간이 든다.
            float t = 0f;
            while (t < 2f) { t += Time.deltaTime; yield return null; }

            var 새것 = Object.FindObjectsByType<ItemPickup>()
                            .Where(p => p != null && !이전.Contains(p))
                            .Where(p => Vector3.Distance(p.transform.position, 자리) < 8f)
                            .ToList();

            int 스크랩 = 새것.Where(p => p.Item != null && p.Item.id == "scrap").Sum(p => p.Count);
            int 전부 = 새것.Sum(p => p.Count);

            E2EHarness.Log($"    {이름}(축적 {축적:F1}) — 스크랩 {스크랩}개 · " +
                           $"전리품 전부 {전부}개 · 바닥에 놓인 덩이 {새것.Count}개");
            결과(스크랩);
        }

        /// <summary>
        /// <b>방치하면 몇 초 만에 사라지는가.</b> 이것이 「경쟁」의 실체다 —
        /// 회수가 느리면 전투 뒤에 서두를 이유가 없고, 그러면 순환의 네 번째
        /// 단계가 그림으로만 남는다.
        ///
        /// 붙여 세우지 않는다. 분해자는 <b>찾아와야</b> 하므로, 탐색 반경 안이되
        /// 걸어와야 하는 자리에서 출발시켜야 실제로 걸리는 시간이 나온다.
        /// </summary>
        static IEnumerator 분해자가_언제_가져가는가(Vector3 자리)
        {
            var 원본 = Object.FindObjectsByType<ScavengerBehavior>(FindObjectsInactive.Exclude)
                            .FirstOrDefault(s => s != null && !s.name.StartsWith("E2E_"));
            if (원본 == null) { E2EHarness.Log("  씬에 분해자가 없다"); yield break; }

            var 출발 = 자리 + new Vector3(8f, 0f, 0f);
            if (NavMesh.SamplePosition(출발, out var hit, 6f, NavMesh.AllAreas)) 출발 = hit.position;

            var go = Object.Instantiate(원본.gameObject, 출발 + Vector3.up * 0.2f, Quaternion.identity);
            go.name = "E2E_분해자";
            var brain = go.GetComponent<CreatureBrain>();
            if (brain != null) brain.enabled = true;
            var agent = go.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.isStopped = false;

            int 처음 = 남은덩이(자리);
            float 출발거리 = Vector3.Distance(go.transform.position, 자리);
            E2EHarness.Log($"  분해자를 {출발거리:F1}m 밖에 놓았다 — 바닥에 덩이 {처음}개");

            const float 제한 = 90f;
            float t0 = Time.time, 첫회수 = -1f, 다사라짐 = -1f;
            while (Time.time - t0 < 제한)
            {
                int 남 = 남은덩이(자리);
                if (첫회수 < 0f && 남 < 처음) 첫회수 = Time.time - t0;
                if (남 <= 0) { 다사라짐 = Time.time - t0; break; }
                yield return null;
            }

            int 끝에남은 = 남은덩이(자리);
            int 가져간것 = 처음 - 끝에남은;

            // 못 가져갔으면 <b>왜</b>인지가 곧 결과다. "느리다"와 "길이 없다"는
            // 전혀 다른 이야기인데, 개수만 적으면 둘이 같은 0으로 보인다.
            if (가져간것 == 0 && agent != null && agent.isActiveAndEnabled)
                E2EHarness.Log($"    [왜 못 가져갔나] 길 상태 {agent.pathStatus} · " +
                               $"목적지 {agent.destination.ToString("F1")} · " +
                               $"남은 거리 {agent.remainingDistance:F1} · 속도 {agent.velocity.magnitude:F2} · " +
                               $"자리 {go.transform.position.ToString("F1")}");
            float 잰시간 = Mathf.Min(Time.time - t0, 제한);
            float 초당 = 잰시간 > 0f ? 가져간것 / 잰시간 : 0f;

            E2EHarness.Log($"  첫 회수 {(첫회수 < 0f ? "없음" : 첫회수.ToString("F1") + "초")} · " +
                           $"{잰시간:F0}초에 {가져간것}/{처음}개 회수 " +
                           $"(개당 {(가져간것 > 0 ? (잰시간 / 가져간것).ToString("F1") : "-")}초)");
            E2EHarness.Log($"  <b>{처음}개짜리 더미가 통째로 사라지는 데 " +
                           $"{(다사라짐 >= 0f ? $"{다사라짐:F0}초" : (초당 > 0f ? $"{처음 / 초당:F0}초 환산" : "재지 못함"))}</b> " +
                           $"(회수 간격 규칙값과 견줄 것)");

            적는다("관찰 보상 — 분해자가 가져가는 속도",
                   $"{출발거리:F0}m 밖에서 출발 · 첫 회수 " +
                   $"{(첫회수 < 0f ? "없음" : $"{첫회수:F1}초")} · " +
                   $"{잰시간:F0}초에 {가져간것}/{처음}개 " +
                   $"(<b>더미 전체 " +
                   $"{(다사라짐 >= 0f ? $"{다사라짐:F0}초" : (초당 > 0f ? $"{처음 / 초당:F0}초 환산" : "미측정"))}</b>)");

            Object.Destroy(go);
            yield return null;
        }

        static int 남은덩이(Vector3 자리) =>
            Object.FindObjectsByType<ItemPickup>()
                  .Count(p => p != null && Vector3.Distance(p.transform.position, 자리) < 8f);

        // ── 잔 도구들 ───────────────────────────────────────────

        static float 기댓값(LootTableSO 표, string id)
        {
            if (표 == null || 표.entries == null) return 0f;
            float sum = 0f;
            foreach (var e in 표.entries)
            {
                if (e?.item == null || e.item.id != id) continue;
                sum += e.chance * (Mathf.Min(e.minCount, e.maxCount) + Mathf.Max(e.minCount, e.maxCount)) * 0.5f;
            }
            return sum;
        }

        static ToolItemSO 도구(string id)
        {
            var db = E2EHarness.Player.Inventory.Database;
            return db != null ? db.GetById(id) as ToolItemSO : null;
        }

        static int 스윙수(float 내구, ToolItemSO 도구)
        {
            float d = 도구 != null ? Mathf.Max(1f, 도구.damage) : 10f;
            return Mathf.Max(1, Mathf.CeilToInt(내구 / d));
        }

        static float 스윙간격(ToolItemSO 도구) => 도구 != null ? Mathf.Max(0.1f, 도구.attackCooldown) : 0.6f;

        static float 캐는시간(HarvestNode 노드) =>
            노드 == null || 노드.Definition == null ? 0f : 캐는시간(노드.Definition, 도구("pickaxe"));

        static float 캐는시간(HarvestNodeSO def, ToolItemSO 도구)
        {
            if (def == null) return 0f;
            if (def.requiredTool == ToolType.None)
            {
                float power = 도구 != null ? Mathf.Max(0.01f, 도구.harvestPower) : 1f;
                return def.baseDuration / power;
            }
            return 스윙수(def.durability, 도구) * 스윙간격(도구);
        }

        static float 평면거리(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        static Vector3 지면(Vector3 xz)
        {
            if (Physics.Raycast(new Vector3(xz.x, 300f, xz.z), Vector3.down, out var h, 400f, ~0,
                                QueryTriggerInteraction.Ignore))
                return h.point + Vector3.up * 0.3f;
            return new Vector3(xz.x, 50.4f, xz.z);
        }

        static HarvestNode 가까운노드(Vector3 from, float 최소)
        {
            return Object.FindObjectsByType<HarvestNode>(FindObjectsInactive.Include)
                         .Where(n => n != null && n.Definition != null && !n.IsDepleted)
                         .Where(n => 평면거리(from, n.transform.position) >= 최소)
                         .OrderBy(n => 평면거리(from, n.transform.position))
                         .FirstOrDefault();
        }

        static HarvestNode 먼노드(Vector3 from, float 최대)
        {
            return Object.FindObjectsByType<HarvestNode>(FindObjectsInactive.Include)
                         .Where(n => n != null && n.Definition != null && !n.IsDepleted)
                         .Where(n => 평면거리(from, n.transform.position) <= 최대)
                         .OrderByDescending(n => 평면거리(from, n.transform.position))
                         .FirstOrDefault();
        }
    }
}
