using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Survive.Building;
using Survive.Core;
using Survive.Interaction;
using Survive.Items;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// <b>사람이 세운 것과 떨군 것이 저장을 건넌다.</b>
    ///
    /// 세계 원장은 <b>씬이 놓은 것의 변화</b>만 담았다. 실행 중에 태어난 것 —
    /// 세운 건축물과 떨군 낙하물 — 은 「달라진 것」이 아니라 「없던 것」이라
    /// 되살릴 몸이 없었고, 그래서 불러올 때마다 거점이 통째로 사라졌다.
    /// 생성 목록(<see cref="SpawnLedger"/>)이 그 나머지 절반이다.
    ///
    /// 여기서 넷을 본다.
    /// <list type="number">
    /// <item><b>지었다 저장하고 불러오면 그대로 서 있다</b> — 3회 연속.
    ///   자리도 그대로다.</item>
    /// <item><b>떨군 것이 같은 자리에 그대로 있다</b> — 착지 지점을 시드에서
    ///   다시 뽑지 않고 적어 둔 자리에 놓는다는 것의 확인이다.</item>
    /// <item><b>철거한 것은 안 돌아온다</b> — 목록이 매번 세계를 다시 적으므로
    ///   철거는 아무 일도 안 해도 반영된다.</item>
    /// <item><b>생성 목록이 없던 옛 저장본이 그냥 열린다</b> — 덧붙임이지
    ///   형식 변경이 아니다.</item>
    /// </list>
    ///
    /// <b>기본 슬롯을 밟지 않는다.</b> 세 클론과 사람의 에디터가 같은 저장 폴더를
    /// 나눠 쓰던 자리다. 이 검사는 자기 슬롯만 쓰고 끝나면 지운다.
    /// </summary>
    public static class E2ESpawnLedger
    {
        const string 슬롯 = "e2e_spawn";
        const string 옛슬롯 = "e2e_spawn_legacy";

        static readonly List<string> _표 = new List<string>();

        public static IEnumerator FullRun()
        {
            _표.Clear();

            yield return 준비();
            yield return 세운_것이_저장을_건넌다();
            yield return 떨군_것이_같은_자리에_남는다();
            yield return 철거한_것은_안_돌아온다();
            yield return 생성_목록이_없는_저장본도_그냥_열린다();
            yield return 뒷정리();

            E2EHarness.Log("");
            E2EHarness.Log("═══ 실측표 ═══");
            foreach (var line in _표) E2EHarness.Log("  " + line);
            E2EHarness.Log("=== 생성 목록 완주 ===");
        }

        static void 적는다(string 항목, string 값) => _표.Add($"{항목} | {값}");

        // ── 준비 ────────────────────────────────────────────────

        static SaveCoordinator 저장소;

        static BuildPlacer Placer =>
            Object.FindAnyObjectByType<BuildPlacer>(FindObjectsInactive.Exclude);

        static Inventory Bag => E2EHarness.Player.Inventory.Inventory;

        static IEnumerator 준비()
        {
            int 잠든것 = E2EHarness.SleepWildCreatures();
            E2EHarness.Log($"  야생 생물 {잠든것}마리를 재웠다");

            저장소 = Object.FindAnyObjectByType<SaveCoordinator>(FindObjectsInactive.Include);
            E2EHarness.Assert(저장소 != null, "SaveCoordinator가 씬에 있다");

            E2EHarness.Assert(WorldLedgerService.Instance != null,
                              "세계 원장이 스스로 붙었다 (씬을 고치지 않는다)");

            int 모은것 = 저장소.Collect();
            E2EHarness.Log($"  저장 대상 {모은것}개를 모았다");

            yield return E2EHarness.WaitUntil(() => Placer != null, "BuildPlacer가 준비된다", 8f);

            // 세울 재료를 채운다. 목록을 손으로 적으면 비용이 바뀔 때마다 낡으므로
            // 카탈로그가 요구하는 것을 그대로 넣는다 (E2EBaseBuilding과 같은 수법).
            var db = E2EHarness.Player.Inventory.Database;
            E2EHarness.Assert(db != null, "아이템 데이터베이스가 연결돼 있다");

            var 필요한것 = new HashSet<string>();
            var placer = Placer;
            if (placer != null)
            {
                foreach (var id in new[] { "campfire", "fence", "bench" })
                {
                    placer.SelectById(id);
                    var cost = placer.Selected?.cost;
                    if (cost == null) continue;
                    foreach (var c in cost) if (c?.item != null) 필요한것.Add(c.item.id);
                }
                placer.Cancel();
            }

            foreach (var id in 필요한것)
            {
                var item = db.GetById(id);
                if (item != null) Bag.TryAdd(item, 40);
            }

            E2EHarness.Log("  재료 주입: " +
                string.Join(", ", 필요한것.Select(id => $"{id} {Bag.CountOf(id)}")));
            yield return null;
        }

        static IEnumerator 뒷정리()
        {
            저장소?.Delete(슬롯);
            저장소?.Delete(옛슬롯);
            E2EHarness.Log("  검사용 슬롯을 지웠다 (기본 슬롯은 건드리지 않았다)");
            yield return null;
        }

        // ── 1. 세운 것이 저장을 건넌다 ─────────────────────────

        /// <summary>
        /// <b>세 번 연속으로 본다.</b> 한 번 통과한 것은 우연일 수 있다 —
        /// 특히 이 갈래는 「거두고 다시 세운다」라 첫 판에는 아무것도 없는 세계에서
        /// 통과하고 두 번째 판부터 앞 판이 남긴 것과 얽힌다.
        ///
        /// <b>불러오기 전에 손으로 부순다.</b> 저장본이 정말로 몸을 되살리는지
        /// 보려면 그 사이에 몸이 없어야 한다. 안 부수면 「원래 서 있던 것이
        /// 계속 서 있었다」와 구별되지 않는다.
        /// </summary>
        static IEnumerator 세운_것이_저장을_건넌다()
        {
            E2EHarness.Log("— 지었다 저장하고 불러오면 그대로 서 있다 (3회 연속) —");

            for (int 회 = 1; 회 <= 3; 회++)
            {
                GameObject 세운것 = null;
                yield return 세운다("campfire", go => 세운것 = go);

                E2EHarness.Assert(세운것 != null, $"{회}회차: 화톳불을 세웠다");
                if (세운것 == null) yield break;

                var 자리 = 세운것.transform.position;
                var 불 = 세운것.GetComponent<Campfire>();
                float 연료 = 불 != null ? 불.FuelSeconds : 0f;

                저장소.Save(슬롯);
                yield return null;

                // 손으로 부순다. 이제 세계에 그것이 없다.
                세운것.SetActive(false);
                Object.Destroy(세운것);
                yield return null;
                yield return null;

                E2EHarness.Assert(!BuiltStructure.Active.Any(b => b != null && b.Spawned),
                                  $"{회}회차: 세운 것이 세계에서 사라졌다");

                bool 열렸나 = 저장소.Load(슬롯);
                yield return null;
                yield return null;

                E2EHarness.Assert(열렸나, $"{회}회차: 저장본이 열렸다");

                var 되살아난것 = BuiltStructure.Active
                    .FirstOrDefault(b => b != null && b.Spawned &&
                                         b.Definition != null && b.Definition.id == "campfire");

                E2EHarness.Assert(되살아난것 != null,
                                  $"{회}회차: 불러온 세계에 화톳불이 <b>다시 서 있다</b>");
                if (되살아난것 == null) yield break;

                float 어긋남 = Vector3.Distance(되살아난것.transform.position, 자리);
                E2EHarness.Assert(어긋남 < 0.01f,
                                  $"{회}회차: 세웠던 그 자리다 (어긋남 {어긋남:F4}m)");

                var 되살아난불 = 되살아난것.GetComponent<Campfire>();
                E2EHarness.Assert(되살아난불 != null, $"{회}회차: 화톳불의 몸이 온전하다");
                if (되살아난불 != null)
                {
                    // 남은 초가 아니라 다 타는 시각을 적었으므로, 저장과 불러오기
                    // 사이에 흐른 만큼 줄어 있다. 늘어나 있으면 안 된다.
                    E2EHarness.Assert(되살아난불.FuelSeconds <= 연료 + 0.5f,
                        $"{회}회차: 연료가 저장할 때보다 늘지 않았다 " +
                        $"({연료:F1}초 -> {되살아난불.FuelSeconds:F1}초)");
                    E2EHarness.Assert(되살아난불.FuelSeconds > 0f,
                        $"{회}회차: 불이 살아 있다");
                }

                E2EHarness.Assert(되살아난것.GetComponent<StructureDemolisher>() != null,
                                  $"{회}회차: 되살아난 것도 부술 수 있다 (껍데기가 아니다)");

                // 다음 회차를 위해 치운다.
                되살아난것.gameObject.SetActive(false);
                Object.Destroy(되살아난것.gameObject);
                yield return null;

                yield return 비켜선다();
            }

            적는다("세운 것", "저장 → 손으로 부숨 → 불러오기: 3회 연속 그 자리에 다시 섰다");
        }

        // ── 2. 떨군 것이 같은 자리에 남는다 ────────────────────

        /// <summary>
        /// <b>착지 지점을 시드에서 다시 뽑지 않는다.</b> 뽑은 자리에서 아래로
        /// 광선을 쏘아 그때 거기 있던 것 위에 얹기 때문에, 다시 뽑으면 물건이
        /// 저장할 때와 다른 자리에 놓인다. 목록은 내려앉은 자리를 그대로 적는다 —
        /// 그래서 시드가 같든 다르든 <b>같은 자리</b>에 놓인다.
        /// </summary>
        static IEnumerator 떨군_것이_같은_자리에_남는다()
        {
            E2EHarness.Log("— 떨군 것이 같은 자리에 남는다 —");

            var db = E2EHarness.Player.Inventory.Database;
            var 물건 = db != null ? db.GetById("scrap") : null;
            E2EHarness.Assert(물건 != null, "잔해 정의를 찾았다");
            if (물건 == null) yield break;

            var 눈 = E2EHarness.Eye.transform;
            var 떨굴자리 = 눈.position + 눈.forward * 3f;

            int 전 = ItemPickup.Active.Count(p => p != null && p.Spawned);
            var 떨군것 = ItemDropper.Drop(물건, 3, 떨굴자리, occasion: 7);
            E2EHarness.Assert(떨군것 != null, "잔해를 떨궜다");
            if (떨군것 == null) yield break;

            // 착지 트윈이 끝나기를 기다린다. 적히는 것은 지금 자리가 아니라
            // 내려앉은 자리이므로 사실 기다릴 필요가 없는데, 기다려야
            // 「떠다니는 중에 적었다」와 구별된다.
            yield return E2EHarness.WaitUntil(() => true, "착지", 1.5f);
            for (int i = 0; i < 60; i++) yield return null;

            var 줍기 = 떨군것.GetComponent<ItemPickup>();
            E2EHarness.Assert(줍기 != null && 줍기.Spawned, "떨군 것은 태어난 것으로 표시됐다");
            if (줍기 == null) yield break;

            var 내려앉은자리 = 줍기.RestAt;
            E2EHarness.Log($"  내려앉은 자리 {내려앉은자리.ToString("F3")} " +
                           $"(지금 자리 {떨군것.transform.position.ToString("F3")})");

            저장소.Save(슬롯);
            yield return null;

            떨군것.SetActive(false);
            Object.Destroy(떨군것);
            yield return null;

            E2EHarness.Assert(저장소.Load(슬롯), "저장본이 열렸다");
            yield return null;

            var 되살아난것 = ItemPickup.Active
                .Where(p => p != null && p.Spawned && p.Item != null && p.Item.id == "scrap")
                .OrderByDescending(p => Vector3.Distance(p.RestAt, 눈.position) * -1f)
                .FirstOrDefault();

            E2EHarness.Assert(되살아난것 != null, "떨군 것이 다시 놓였다");
            if (되살아난것 == null) yield break;

            float 어긋남 = Vector3.Distance(되살아난것.RestAt, 내려앉은자리);
            E2EHarness.Assert(어긋남 < 0.01f,
                              $"불러온 뒤에도 같은 자리다 (어긋남 {어긋남:F4}m)");
            E2EHarness.AssertEqual(되살아난것.Count, 3, "수량도 그대로다");

            int 후 = ItemPickup.Active.Count(p => p != null && p.Spawned);
            E2EHarness.Assert(후 == 전 + 1,
                              $"두 벌로 늘지 않았다 ({전} -> {후}) — 되살리기 전에 거둔다");

            적는다("떨군 것", $"{내려앉은자리.ToString("F2")}에 놓았다가 " +
                              $"불러오니 어긋남 {어긋남:F4}m · 수량 3 그대로");

            // 뒷정리: 이 검사가 남긴 것을 치운다
            if (되살아난것 != null)
            {
                되살아난것.gameObject.SetActive(false);
                Object.Destroy(되살아난것.gameObject);
            }
            yield return null;
        }

        // ── 3. 철거한 것은 안 돌아온다 ─────────────────────────

        /// <summary>
        /// 목록은 매번 세계를 통째로 다시 적으므로 철거·줍기가 <b>아무 일도
        /// 하지 않아도</b> 반영된다. 그것을 여기서 못 박는다 — 안 그러면
        /// 「한 번 세운 것이 영영 되살아나는」 목록이 된다.
        /// </summary>
        static IEnumerator 철거한_것은_안_돌아온다()
        {
            E2EHarness.Log("— 철거한 것은 안 돌아온다 —");

            GameObject 세운것 = null;
            yield return 세운다("campfire", go => 세운것 = go);
            E2EHarness.Assert(세운것 != null, "화톳불을 세웠다");
            if (세운것 == null) yield break;

            저장소.Save(슬롯);
            yield return null;

            var 원장 = WorldLedgerService.Instance;
            E2EHarness.Assert(원장 != null && 원장.Spawns.Count > 0,
                              $"목록에 적혔다 ({원장?.Spawns.Count ?? 0}줄)");

            // 부순다. 그리고 다시 저장한다.
            세운것.SetActive(false);
            Object.Destroy(세운것);
            yield return null;
            yield return null;

            저장소.Save(슬롯);
            yield return null;

            int 남은줄 = 원장 != null ? 원장.Spawns.Count : -1;
            E2EHarness.Assert(남은줄 == 0,
                              $"부순 것은 목록에서 빠졌다 (남은 줄 {남은줄})");

            E2EHarness.Assert(저장소.Load(슬롯), "저장본이 열렸다");
            yield return null;
            yield return null;

            E2EHarness.Assert(!BuiltStructure.Active.Any(b => b != null && b.Spawned),
                              "불러온 세계에도 그것은 없다");

            적는다("철거", "부순 뒤 저장하니 목록에서 빠졌고 불러와도 안 돌아왔다");
        }

        // ── 4. 옛 저장본 ────────────────────────────────────────

        /// <summary>
        /// <b>생성 목록이 없던 저장본이 그냥 열린다.</b> 이 목록은 「세계」 절에
        /// <b>덧붙인</b> 칸이라, 그 칸이 없는 파일을 읽어도 <c>JsonUtility</c>가
        /// 빈 목록을 남긴다 — 곧 사람이 세운 것이 하나도 없는 세계이고,
        /// 실제로 그 저장본을 쓰던 세계에는 정말로 하나도 없었다.
        /// </summary>
        static IEnumerator 생성_목록이_없는_저장본도_그냥_열린다()
        {
            E2EHarness.Log("— 생성 목록이 없는 옛 저장본이 그냥 열린다 —");

            GameObject 세운것 = null;
            yield return 세운다("campfire", go => 세운것 = go);
            E2EHarness.Assert(세운것 != null, "화톳불을 세웠다");

            저장소.Save(옛슬롯);
            yield return null;

            string 경로 = E2EHarness.SlotPath(옛슬롯);
            E2EHarness.Assert(File.Exists(경로), "저장본 파일이 생겼다");

            E2EHarness.Assert(SaveSerializer.TryDeserialize(File.ReadAllText(경로),
                                                           out var 저장본, out var 사유),
                              $"저장본을 읽었다 ({사유})");
            if (저장본 == null) yield break;

            // 「세계」 절에서 spawned 칸만 도려낸다. 목록이 생기기 전의 파일과
            // 글자까지 같은 모양이다.
            var 세계절 = 저장본.entries.FirstOrDefault(e => e.key == WorldLedgerService.Key);
            E2EHarness.Assert(세계절 != null, "저장본에 「세계」 절이 있다");
            if (세계절 == null) yield break;

            string 전 = 세계절.json;
            세계절.json = 칸을_도려낸다(전, "spawned");
            E2EHarness.Assert(세계절.json != 전 && !세계절.json.Contains("spawned"),
                              "생성 목록 칸을 도려냈다");

            File.WriteAllText(경로, SaveSerializer.Serialize(저장본));
            yield return null;

            // 세운 것이 서 있는 채로 옛 저장본을 연다.
            bool 열렸나 = 저장소.Load(옛슬롯);
            yield return null;
            yield return null;

            E2EHarness.Assert(열렸나, "생성 목록이 없는 저장본이 그냥 열렸다");

            var 원장 = WorldLedgerService.Instance;
            E2EHarness.AssertEqual(원장 != null ? 원장.Spawns.Count : -1, 0,
                                   "그때 목록은 비어 있다");
            E2EHarness.Assert(!BuiltStructure.Active.Any(b => b != null && b.Spawned),
                              "사람이 세운 것이 하나도 없는 세계로 열렸다 — " +
                              "그 저장본을 쓰던 세계가 실제로 그랬다");

            적는다("옛 저장본", "「세계」 절에서 생성 목록 칸을 도려낸 파일이 그냥 열렸다");
            yield return null;
        }

        /// <summary>
        /// JSON 한 겹에서 이름이 <paramref name="칸"/>인 항목을 통째로 뺀다.
        /// <c>JsonUtility</c>는 도려내는 문을 안 주므로 글자로 자른다 —
        /// 값이 중첩 객체라 여는 괄호와 닫는 괄호를 세어야 한다.
        /// </summary>
        static string 칸을_도려낸다(string json, string 칸)
        {
            string 표 = "\"" + 칸 + "\":";
            int 시작 = json.IndexOf(표);
            if (시작 < 0) return json;

            int i = 시작 + 표.Length;
            while (i < json.Length && json[i] != '{') i++;
            if (i >= json.Length) return json;

            int 깊이 = 0;
            for (; i < json.Length; i++)
            {
                if (json[i] == '{') 깊이++;
                else if (json[i] == '}' && --깊이 == 0) { i++; break; }
            }

            // 앞뒤 쉼표 중 하나만 남긴다.
            int 끝 = i;
            if (끝 < json.Length && json[끝] == ',') 끝++;
            else if (시작 > 0 && json[시작 - 1] == ',') 시작--;

            return json.Substring(0, 시작) + json.Substring(끝);
        }

        // ── 잔 도구들 ───────────────────────────────────────────

        /// <summary>
        /// 놓을 자리를 스스로 찾아 세운다. 좌표를 박아 두면 지형이 바뀔 때
        /// 그 좌표만 조용히 낡으므로 실제 판정 함수에게 물어본다
        /// (<c>E2EBaseBuilding</c>과 같은 수법).
        /// </summary>
        static IEnumerator 세운다(string id, System.Action<GameObject> result)
        {
            var placer = Placer;
            E2EHarness.Assert(placer != null, "BuildPlacer가 있다");
            if (placer == null) { result(null); yield break; }

            placer.SelectById(id);
            yield return null;
            E2EHarness.Assert(placer.Selected != null, $"건축물 정의를 찾았다: {id}");

            bool 찾았나 = false;
            yield return 놓을_자리를_찾는다(placer, r => 찾았나 = r);

            if (!찾았나)
            {
                E2EHarness.Log($"  [배치 문제] {id}를 놓을 자리를 못 찾았다 " +
                               $"(마지막 판정 {placer.LastResult})");
                placer.Cancel();
                result(null);
                yield break;
            }

            // 실제 조작대로 좌클릭으로 세운다
            yield return E2EHarness.ClickAttack();
            yield return null;
            yield return null;

            var 세운것 = BuiltStructure.Active
                .LastOrDefault(b => b != null && b.Spawned &&
                                    b.Definition != null && b.Definition.id == id);

            placer.Cancel();
            yield return null;

            result(세운것 != null ? 세운것.gameObject : null);
        }

        static IEnumerator 놓을_자리를_찾는다(BuildPlacer placer, System.Action<bool> result)
        {
            var 사람 = E2EHarness.Player.transform;

            for (int 고리 = 0; 고리 < 3; 고리++)
            {
                float 거리 = 1.6f + 고리 * 0.9f;

                for (int a = 0; a < 12; a++)
                {
                    var dir = Quaternion.Euler(0f, a * 30f, 0f) * Vector3.forward;
                    var 탐침 = 사람.position + dir * 거리 + Vector3.up * 2f;

                    if (!Physics.Raycast(탐침, Vector3.down, out var hit, 8f, ~0,
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

            result(false);
        }

        /// <summary>
        /// 몇 걸음 옮긴다. 방금 자리에 다음 것을 지으려 하면 시나리오가 스스로
        /// 만든 자취에 걸린다.
        /// </summary>
        static IEnumerator 비켜선다()
        {
            var 처음 = E2EHarness.Player.transform.position;

            for (int a = 0; a < 8; a++)
            {
                var dir = Quaternion.Euler(0f, a * 45f + 20f, 0f) * Vector3.forward;
                var 가고싶은데 = 처음 + dir * 5f;

                if (!UnityEngine.AI.NavMesh.SamplePosition(가고싶은데, out var hit, 3f,
                                                           UnityEngine.AI.NavMesh.AllAreas))
                    continue;

                yield return E2EHarness.TryWalkTo(hit.position, 2.0f, 20f);
                if (E2EHarness.LastWalkArrived &&
                    Vector3.Distance(E2EHarness.Player.transform.position, 처음) > 3f)
                    yield break;
            }
        }
    }
}
