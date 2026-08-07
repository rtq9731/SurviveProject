using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Survive.Building;
using Survive.Harvesting;
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

            E2EHarness.Log("");
            E2EHarness.Log("═══ 실측표 ═══");
            foreach (var line in _표) E2EHarness.Log("  " + line);
            E2EHarness.Log("=== 압박 곡선 측량 완주 ===");
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
        /// <b>바다를 건넌다.</b> 2번 섬 물가까지 갔다가 돌아온다.
        /// 기획서가 "강 건너 A-2"라 부르는 자리인데, 지금 씬에는 강도 얕은 바다도
        /// 없고 <b>열린 바다</b> 하나뿐이다 — 그 사실 자체가 실측 결과다.
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
