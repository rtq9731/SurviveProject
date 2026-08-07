using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Survive.Combat;
using Survive.Harvesting;
using Survive.Items;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// <b>세계가 마르지 않는다</b> — 다 캔 자리가 실제로 돌아온다.
    ///
    /// 규칙(<see cref="HarvestRespawnRule"/>)은 EditMode가 경계까지 재고, 여기서는
    /// 그 규칙이 <b>실제 씬의 물체에 걸려 있는가</b>를 본다. 셋을 나눠 본다.
    /// <list type="number">
    /// <item><b>흔적이 남는다</b> — 통째로 사라지지 않는다. 남은 자국이
    ///   "여기는 다시 찬다"는 말이다.</item>
    /// <item><b>보고 있는 앞에서는 안 돌아온다</b> — 주기를 다 채워도 기다린다.
    ///   눈앞에서 물체가 솟는 것은 규칙이 아니라 사고로 읽힌다.</item>
    /// <item><b>합금 더미는 안 돌아온다</b> — 0이 실수가 아니라 설계라는 것을
    ///   세계에서도 확인한다.</item>
    /// </list>
    ///
    /// 300초를 실시간으로 기다리는 검사는 아무도 돌리지 않는다. 그래서 노드
    /// <b>하나의</b> 주기만 잠깐 줄여서 본다(<see cref="HarvestNode.OverrideRespawnSeconds"/>) —
    /// 에셋은 건드리지 않는다. 에셋을 고치면 에디터가 디스크에 저장해 버려
    /// 검증 한 번이 게임의 수치를 바꾼다.
    /// </summary>
    public static class E2EHarvestRespawn
    {
        /// <summary>검증용으로 줄인 재생 주기(초).</summary>
        const float 검증용재생초 = 4f;

        static readonly List<string> _표 = new List<string>();

        public static IEnumerator FullRun()
        {
            _표.Clear();

            yield return 준비();
            yield return 장부();
            yield return 맨손_잔해가_돌아온다();
            yield return 부수는_잔해가_돌아온다();
            yield return 멀어지면_돌아온다();
            yield return 합금_더미는_돌아오지_않는다();

            E2EHarness.Log("");
            E2EHarness.Log("═══ 실측표 ═══");
            foreach (var line in _표) E2EHarness.Log("  " + line);
            E2EHarness.Log("=== 채집물 재생 완주 ===");
        }

        static void 적는다(string 항목, string 값) => _표.Add($"{항목} | {값}");

        // ── 준비 ────────────────────────────────────────────────

        static IEnumerator 준비()
        {
            int 잠든것 = E2EHarness.SleepWildCreatures();
            E2EHarness.Log($"  야생 생물 {잠든것}마리를 재웠다 (재려는 것은 시간이다)");

            var db = E2EHarness.Player.Inventory.Database;
            var inv = E2EHarness.Player.Inventory.Inventory;
            foreach (var id in new[] { "pickaxe", "axe" })
            {
                var it = db != null ? db.GetById(id) : null;
                if (it != null && inv.CountOf(id) == 0) inv.TryAdd(it, 1);
            }
            yield return null;
        }

        // ── 장부 — 세계가 분당 몇 개를 내놓는가 ─────────────────

        /// <summary>
        /// <b>이 라운드의 숫자.</b> 씬에 실제로 선 노드를 세어 정상 상태 공급을
        /// 계산하고, 랜턴이 태우는 양과 나란히 둔다. 판정하지 않고 적는 부분과
        /// 판정하는 부분이 섞여 있다 — 공급이 소모를 넘는 것만은 단언한다.
        /// </summary>
        static IEnumerator 장부()
        {
            E2EHarness.Log("— 장부 (세계의 정상 상태 공급) —");

            float 공급 = 0f, 재고 = 0f;
            foreach (var grp in 모든노드().Where(n => n.Definition != null)
                                          .GroupBy(n => n.Definition))
            {
                var def = grp.Key;
                int 곳 = grp.Count();
                float 몫 = 기댓값(def.drops, "scrap");
                if (몫 <= 0f) continue;

                float 분당 = HarvestRespawnRule.SupplyPerMinute(몫, 곳, def.respawnSeconds);
                공급 += 분당;
                재고 += 몫 * 곳;

                E2EHarness.Log($"  {def.name} x{곳} — 스크랩 {몫:F2}/곳 · 재생 " +
                               $"{def.respawnSeconds:F0}초 → 분당 {분당:F2}개");
                적는다($"공급 {def.name}",
                       $"{곳}곳 × {몫:F2}개 ÷ {def.respawnSeconds:F0}초 = 분당 {분당:F2}");
            }

            float 소모 = HarvestRespawnRule.ScrapBurnedPerMinute(1);
            E2EHarness.Log($"  <b>공급 {공급:F2}/분 − 랜턴 {소모:F2}/분 = 순증가 {공급 - 소모:F2}/분 " +
                           $"({공급 / 소모:F2}배)</b> · 서 있는 재고 {재고:F1}개");

            적는다("세계의 정상 상태 공급", $"분당 {공급:F2} 스크랩");
            적는다("랜턴 소모(티어 1)", $"분당 {소모:F2} 스크랩");
            적는다("순증가", $"분당 {공급 - 소모:F2} 스크랩 (소모의 {공급 / 소모:F2}배 공급)");
            적는다("서 있는 재고", $"{재고:F1}개 = 랜턴 {재고 / 소모:F1}분어치");

            E2EHarness.Assert(HarvestRespawnRule.Sustains(공급, 1),
                              $"세계의 공급이 랜턴 소모를 넘는다 ({공급:F2} > {소모:F2})");

            yield return null;
        }

        // ── 1. 맨손 잔해 ────────────────────────────────────────

        /// <summary>
        /// 「흩어진 잔해」를 <b>실제로 눌러</b> 캔다. 그러고 나서 셋을 본다 —
        /// 흔적이 남는가, 보고 있는 동안 참는가, 돌아서면 돌아오는가.
        /// </summary>
        static IEnumerator 맨손_잔해가_돌아온다()
        {
            E2EHarness.Log("— 흩어진 잔해: 맨손으로 캐고 돌아오는 것을 본다 —");

            var node = 노드("LooseScrap");
            E2EHarness.Assert(node != null, "흩어진 잔해가 씬에 있다");
            if (node == null) yield break;

            node.OverrideRespawnSeconds(검증용재생초);
            var 캐기전크기 = node.transform.localScale;
            yield return 눈앞에_둔다(node);

            int 전 = E2EHarness.Player.Inventory.ScrapCount;
            yield return E2EHarness.HoldKey(Key.E, node.HoldDuration + 0.6f);
            yield return null;

            // 조준이 남의 것을 잡았을 수 있다. 홀드 경로 자체는 E2EScenarios가 이미
            // 통과시켰으므로, 여기서 볼 "다 캔 뒤에 무슨 일이 나는가"는 직접 넣어 끝낸다.
            if (!node.IsDepleted)
            {
                E2EHarness.Log("    조준이 빗나갔다 — 상호작용을 직접 넣는다");
                node.Interact(E2EHarness.Player);
                yield return null;
            }

            E2EHarness.Assert(node.IsDepleted, "흩어진 잔해를 캤다");
            E2EHarness.Assert(E2EHarness.Player.Inventory.ScrapCount > 전,
                              $"스크랩이 들어왔다 ({전} → {E2EHarness.Player.Inventory.ScrapCount})");
            if (!node.IsDepleted) yield break;

            yield return 흔적을_본다(node, 캐기전크기);
            yield return 보는_동안은_안_돌아온다(node);
            yield return 돌아서면_돌아온다(node, 캐기전크기);
        }

        // ── 2. 부수는 잔해 ──────────────────────────────────────

        /// <summary>
        /// 「기계 잔해」는 곡괭이로 부순다. 캐는 길이 둘인데 재생은 하나여야 한다 —
        /// 홀드로 캔 것만 돌아오면 부순 자리는 영영 빈 채로 남는다.
        /// </summary>
        static IEnumerator 부수는_잔해가_돌아온다()
        {
            E2EHarness.Log("— 기계 잔해: 부수고 돌아오는 것을 본다 —");

            var node = 노드("Debris");
            E2EHarness.Assert(node != null, "기계 잔해가 씬에 있다");
            if (node == null) yield break;

            node.OverrideRespawnSeconds(검증용재생초);
            var 캐기전크기 = node.transform.localScale;

            var user = Object.FindAnyObjectByType<Survive.Player.PlayerToolUser>(
                FindObjectsInactive.Exclude);
            E2EHarness.Assert(user != null && user.EquipFirst("pickaxe"), "곡괭이를 들었다");

            yield return 눈앞에_둔다(node);

            for (int 회 = 0; 회 < 3 && !node.IsDepleted; 회++)
                yield return E2EHarness.HoldAttack(1.4f);

            if (!node.IsDepleted)
            {
                E2EHarness.Log("    휘두른 것이 닿지 않았다 — 남은 내구도를 직접 넣어 넘긴다");
                var swing = Object.FindAnyObjectByType<MeleeSwing>(FindObjectsInactive.Exclude);
                var 몸 = node.GetComponentInChildren<Collider>(true);
                ((IDamageable)node).TakeDamage(new DamageInfo(
                    node.Definition.durability + 1f, swing != null ? swing.gameObject : null,
                    몸 != null ? 몸.bounds.center : node.transform.position, Vector3.forward));
                yield return null;
            }

            E2EHarness.Assert(node.IsDepleted, "기계 잔해를 부쉈다");
            if (!node.IsDepleted) yield break;

            yield return 흔적을_본다(node, 캐기전크기);
            yield return 돌아서면_돌아온다(node, 캐기전크기);
        }

        // ── 3. 멀어져도 돌아온다 ────────────────────────────────

        /// <summary>
        /// 조건은 <b>둘 중 하나</b>다 — 시야 밖이거나 멀거나. 앞 둘이 「돌아선다」로
        /// 풀었으니 여기서는 <b>등을 돌리지 않고 멀어지기만</b> 한다. 이쪽 길이
        /// 막혀 있으면 방을 나가도 세계가 그대로 멈춰 있다.
        /// </summary>
        static IEnumerator 멀어지면_돌아온다()
        {
            E2EHarness.Log("— 멀어지기만 해도 돌아온다 —");

            var node = 노드("LooseScrap");
            if (node == null) { E2EHarness.Log("  남은 흩어진 잔해가 없다"); yield break; }

            node.OverrideRespawnSeconds(검증용재생초);
            yield return 눈앞에_둔다(node);

            node.Interact(E2EHarness.Player);
            yield return null;
            E2EHarness.Assert(node.IsDepleted, "잔해를 캤다");
            if (!node.IsDepleted) yield break;

            // 계속 쳐다본 채로 사이를 벌린다. 거리만으로 조건이 열리는지 보는 것이다.
            // 사람을 순간이동시키지 않고 <b>물체를 민다</b> — 재는 것은 사이의 거리이고,
            // 사람을 옮기면 지형에 끼거나 절벽 밖으로 나가는 사고가 검증을 오염시킨다.
            var cam = E2EHarness.Eye.transform;
            node.transform.position = cam.position
                                      + cam.forward * (HarvestRespawnRule.UnseenDistance + 6f);
            E2EHarness.SyncPhysics();
            yield return null;
            E2EHarness.LookAt(node.transform.position);
            yield return null;

            float 거리 = Vector3.Distance(E2EHarness.Eye.transform.position, node.transform.position);
            E2EHarness.Log($"  {거리:F1}m 밖에서 계속 보고 있다 (문턱 {HarvestRespawnRule.UnseenDistance:F1}m)");
            E2EHarness.Assert(거리 > HarvestRespawnRule.UnseenDistance, "문턱보다 멀리 물러났다");

            yield return E2EHarness.WaitUntil(() => !node.IsDepleted,
                                              "멀리서는 보고 있어도 돌아왔다",
                                              검증용재생초 + 6f);
            E2EHarness.Assert(!node.IsDepleted,
                              $"{거리:F1}m 밖에서는 보고 있어도 돌아왔다 — 거리만으로 조건이 열린다");
            적는다("돌아오는 조건", $"시야 반각 {HarvestRespawnRule.ViewHalfAngleDegrees:F0}도 밖 " +
                                    $"또는 {HarvestRespawnRule.UnseenDistance:F1}m 밖");
        }

        // ── 4. 합금 더미는 돌아오지 않는다 ──────────────────────

        /// <summary>
        /// <b>0이 실수가 아니라는 것을 세계에서도 확인한다.</b> 이종 합금이 무한히
        /// 나오면 티어 3 관문이 시간만 들이면 열리는 것이 된다 — 노동은 게이트가 아니다.
        /// 그래서 합금 더미는 다 캐면 흔적도 없이 사라지고, 그것이 "여기는 끝났다"는 말이다.
        /// </summary>
        static IEnumerator 합금_더미는_돌아오지_않는다()
        {
            E2EHarness.Log("— 합금 더미는 돌아오지 않는다 (그것이 설계다) —");

            var node = 노드("OreVein");
            E2EHarness.Assert(node != null, "합금 더미가 씬에 있다");
            if (node == null) yield break;

            E2EHarness.AssertEqual(node.RespawnSeconds, 0f, "합금 더미의 재생 주기");

            // 곡괭이 없이는 흠집도 안 난다 — 그 규칙은 「도구는 전용이다」가 이미
            // 지키고 있으므로 여기서는 도구를 쥐여 주고 넘어간다.
            var user = Object.FindAnyObjectByType<Survive.Player.PlayerToolUser>(
                FindObjectsInactive.Exclude);
            E2EHarness.Assert(user != null && user.EquipFirst("pickaxe"), "곡괭이를 들었다");
            node.CanInteract(E2EHarness.Player);      // 지금 든 도구를 물려 준다
            yield return null;

            var go = node.gameObject;
            var 몸 = node.GetComponentInChildren<Collider>(true);
            var swing = Object.FindAnyObjectByType<MeleeSwing>(FindObjectsInactive.Exclude);
            ((IDamageable)node).TakeDamage(new DamageInfo(
                node.Definition.durability + 1f, swing != null ? swing.gameObject : null,
                몸 != null ? 몸.bounds.center : node.transform.position, Vector3.forward));
            yield return null;

            E2EHarness.Assert(node.IsDepleted, "합금 더미를 부쉈다");

            // <b>겉을 본다.</b> 겉모습 오브젝트가 노드 자신일 수도, 자식일 수도 있어
            // (씬의 합금 더미는 자식이다) 오브젝트의 활성 상태로는 물을 수 없다.
            // 그려지는 것이 하나도 없다는 것이 "자취가 없다"의 뜻이다.
            var 겉 = node.GetComponentsInChildren<Renderer>(true);
            E2EHarness.Assert(겉.All(r => !r.enabled),
                              "다 캔 매장지는 자취 없이 사라진다 (흔적을 남기지 않는다)");
            E2EHarness.Assert(go.activeInHierarchy == false ||
                              (node.GetComponentsInChildren<Collider>(true).All(c => !c.enabled)),
                              "다 캔 매장지는 겨눠지지 않는다");

            // 흔적이 없는 것과 돌아오지 않는 것이 같은 말이라는 것을 시간으로도 확인한다.
            float t = 0f;
            while (t < 검증용재생초 + 2f) { t += Time.deltaTime; yield return null; }
            E2EHarness.Assert(node.IsDepleted && 겉.All(r => !r.enabled),
                              $"{t:F0}초가 지나도 돌아오지 않았다");

            적는다("합금 더미", "재생 0초 — 다 캐면 없어지는 매장지다 (노동은 게이트가 아니다)");
        }

        // ── 공통 검사 ───────────────────────────────────────────

        static IEnumerator 흔적을_본다(HarvestNode node, Vector3 캐기전크기)
        {
            yield return null;

            E2EHarness.Assert(node.gameObject.activeInHierarchy,
                              "캔 자리는 남는다 — 오브젝트째 꺼지면 다시 찰 수 없다");

            var 겉 = node.GetComponentsInChildren<Renderer>(true);
            E2EHarness.Assert(겉.Any(r => r.enabled),
                              "캔 자리에 흔적이 보인다 — 통째로 사라지면 여기가 되살아나는 자리인지 알 수 없다");

            var 지금 = node.transform.localScale;
            E2EHarness.Assert(지금.y < 캐기전크기.y * 0.5f,
                              $"흔적은 납작하다 (세로 {캐기전크기.y:F2} → {지금.y:F2})");
            E2EHarness.Assert(지금.x < 캐기전크기.x,
                              $"흔적은 작다 (가로 {캐기전크기.x:F2} → {지금.x:F2})");

            var 몸들 = node.GetComponentsInChildren<Collider>(true);
            E2EHarness.Assert(몸들.All(c => !c.enabled), "흔적은 겨눠지지도 부딪히지도 않는다");
        }

        /// <summary>
        /// 주기를 다 채우고도 <b>더</b> 기다리는데 안 돌아온다 — 보고 있기 때문이다.
        /// 이 검사가 없으면 다음 검사가 "그냥 시간이 돼서 돌아왔다"와 구별되지 않는다.
        /// </summary>
        static IEnumerator 보는_동안은_안_돌아온다(HarvestNode node)
        {
            E2EHarness.LookAt(node.transform.position);
            yield return null;

            float 거리 = Vector3.Distance(E2EHarness.Eye.transform.position, node.transform.position);
            E2EHarness.Assert(거리 < HarvestRespawnRule.UnseenDistance,
                              $"눈앞에 있다 ({거리:F1}m < {HarvestRespawnRule.UnseenDistance:F1}m)");

            float t = 0f;
            float 지켜볼시간 = 검증용재생초 * 2f;
            while (t < 지켜볼시간)
            {
                t += Time.deltaTime;
                E2EHarness.LookAt(node.transform.position);   // 계속 응시한다
                yield return null;
            }

            E2EHarness.Assert(node.IsDepleted,
                              $"주기({검증용재생초:F0}초)의 두 배인 {t:F0}초를 지켜봤는데 " +
                              "눈앞에서 돌아오지 않았다");
        }

        static IEnumerator 돌아서면_돌아온다(HarvestNode node, Vector3 캐기전크기)
        {
            var 눈 = E2EHarness.Eye.transform.position;
            var 반대 = 눈 - node.transform.position;
            반대.y = 0f;
            if (반대.sqrMagnitude < 0.01f) 반대 = -E2EHarness.Eye.transform.forward;
            E2EHarness.LookAt(눈 + 반대.normalized * 10f);
            yield return null;

            yield return E2EHarness.WaitUntil(() => !node.IsDepleted,
                                              "등을 돌리자 다 캔 자리가 돌아왔다",
                                              검증용재생초 + 6f);
            E2EHarness.Assert(!node.IsDepleted, "등을 돌리자 다 캔 자리가 돌아왔다");

            // 부풀어 오르는 데 시간이 걸린다. 다 자랄 때까지 본다.
            float t = 0f;
            while (t < HarvestRespawnRule.RegrowTweenSeconds + 0.4f) { t += Time.deltaTime; yield return null; }

            var 지금 = node.transform.localScale;
            E2EHarness.Assert(Vector3.Distance(지금, 캐기전크기) < 0.05f * 캐기전크기.magnitude + 0.01f,
                              $"제 크기로 돌아왔다 ({지금.ToString("F2")}, 캐기 전 {캐기전크기.ToString("F2")})");

            var 몸들 = node.GetComponentsInChildren<Collider>(true);
            E2EHarness.Assert(몸들.All(c => c.enabled), "돌아온 잔해는 다시 겨눠진다");
            E2EHarness.Assert(!node.IsDepleted, "다시 캘 수 있다");
        }

        // ── 잔 도구들 ───────────────────────────────────────────

        static IEnumerable<HarvestNode> 모든노드() =>
            Object.FindObjectsByType<HarvestNode>(FindObjectsInactive.Include)
                  .Where(n => n != null);

        /// <summary>이 정의의, 아직 안 캔 노드 하나. 사람에게서 먼 것을 골라 앞선 시나리오와 겹치지 않게 한다.</summary>
        static HarvestNode 노드(string 정의이름)
        {
            var 눈 = E2EHarness.Eye != null ? E2EHarness.Eye.transform.position : Vector3.zero;
            return 모든노드()
                .Where(n => n.Definition != null && n.Definition.name == 정의이름 && !n.IsDepleted)
                .OrderByDescending(n => Vector3.Distance(눈, n.transform.position))
                .FirstOrDefault();
        }

        /// <summary>
        /// 조준 경로만 확인하면 되므로 눈앞으로 옮긴다 — E2EChapter1·E2EMushroomLumber와
        /// 같은 관례다. autoSyncTransforms가 꺼져 있어 옮기기 전후로 물리와 맞춘다.
        /// </summary>
        static IEnumerator 눈앞에_둔다(HarvestNode node)
        {
            var cam = E2EHarness.Eye;
            var 몸 = node.GetComponentInChildren<Collider>(true);
            Vector3 겨냥 = cam.transform.position + cam.transform.forward * 2.2f;

            E2EHarness.SyncPhysics();
            if (몸 != null) node.transform.position += 겨냥 - 몸.bounds.center;
            else node.transform.position = 겨냥;
            E2EHarness.SyncPhysics();

            yield return null;
            E2EHarness.LookAt(몸 != null ? 몸.bounds.center : node.transform.position);
            yield return null;
            yield return null;
        }

        static float 기댓값(LootTableSO 표, string id)
        {
            if (표?.entries == null) return 0f;
            float sum = 0f;
            foreach (var e in 표.entries)
            {
                if (e?.item == null || e.item.id != id) continue;
                sum += e.chance * (Mathf.Min(e.minCount, e.maxCount) +
                                   Mathf.Max(e.minCount, e.maxCount)) * 0.5f;
            }
            return sum;
        }
    }
}
