using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Survive.Combat;
using Survive.Creatures;
using Survive.Interaction;
using Survive.Items;
using Survive.Player;
using Survive.Progression;
using Survive.UI;
using Survive.Vitals;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// 백로그 39 — 연구 소재 공급. <b>연구대 앞에 놓을 것이 어디서 오는가.</b>
    ///
    /// 38이 연구대를 세웠지만 그때 세계에는 유물이 하나도 없었다. 소재는 두 갈래로 온다.
    /// <list type="number">
    /// <item><b>유물</b> — 낫이 순찰하다 흘린다. 죽여서 얻는 것이 아니다. 낫은 이기라고
    ///       놓은 상대가 아니므로(백로그 30), 대가는 <b>그 영역에 머문 시간</b>이다.
    ///       머물러 기다리려면 감지 반경 밖에 서 있어야 하고, 떨어진 것을 주우려면
    ///       랜턴을 켜고 다가가야 한다 — 이 시나리오가 그대로 그 동선을 밟는다</item>
    /// <item><b>생물 부품</b> — 잡으면 몸에서 나온다(기존 전리품 표). 그것을 연구대에
    ///       넣으면 제작법이 아니라 <b>도감 한 줄</b>이 열린다</item>
    /// </list>
    ///
    /// pity 규칙 자체는 <c>RelicDropRuleTests</c>가 Unity 없이 본다. 여기서 볼 것은
    /// <b>그 규칙이 실제로 세계에 걸려 있는가</b>다 — 흘리는 것이 정말 순찰 중인지,
    /// 두 번째가 정말 다른 종인지, 다 가진 뒤에 정말 그치는지.
    /// </summary>
    public static class E2ERelicSupply
    {
        const string 낫프리팹 = "Assets/05.Prefabs/Creatures/Creature_낫.prefab";

        const string 막 = "relic_membrane";
        const string 핵 = "relic_core";
        const string 낫부품 = "part_scythe";
        const string 낫도감연구 = "res_codex_scythe";
        const string 낫도감열쇠 = "codex_scythe";
        const string 보행설계 = "bp_surface_walker";
        const string 잠항설계 = "bp_submersible";
        const string 막연구 = "res_surface_walker";
        const string 핵연구 = "res_submersible";
        const string 연구대 = "research_bench";

        /// <summary>
        /// 굴리는 간격을 1초로 줄인다. 실제 값은 25초마다 25%(절반이 60초 안에 하나)로,
        /// 사람이 <b>낫의 영역에 머무는 시간</b>을 재라고 정한 수치다. 그것을 실시간으로
        /// 기다리면 유물 하나에 1~2분, 이 시나리오 하나가 십 분짜리가 된다.
        /// 줄이는 것은 간격뿐이고 <b>확률과 pity 순서는 그대로 둔다</b> — 여기서 볼
        /// 규칙이 그것이다.
        /// </summary>
        const float 굴림간격 = 1f;

        static Inventory Inv => E2EHarness.Player.Inventory.Inventory;
        static PlayerVitals Vitals => E2EHarness.Player.Vitals;
        static UnlockLedger Ledger => UnlockService.Instance.Ledger;
        static Vector3 사람자리 => E2EHarness.Player.transform.position;

        static CraftingUI UI => Object.FindAnyObjectByType<CraftingUI>(FindObjectsInactive.Include);

        static LanternController _lantern;
        static CreatureDefinitionSO _def;
        static CreatureBrain _낫;
        static ResearchStation _station;
        static Vector3 _기다리는자리;

        public static IEnumerator FullRun()
        {
            yield return 준비();

            string 첫번째 = null, 두번째 = null;

            yield return 낫을_세운다();
            yield return 순찰하다_흘린_것을_줍는다(id => 첫번째 = id, "첫 유물");
            yield return 순찰하다_흘린_것을_줍는다(id => 두번째 = id, "다음 유물");
            yield return 다른_종이었는지_본다(첫번째, 두번째);
            yield return 둘_다_쥐면_그친다();

            yield return 잡으면_제_부품을_떨군다();
            yield return 부품을_분석하면_도감이_열린다();

            yield return 치운다();
            E2EHarness.Log("=== 연구 소재 공급 완주 ===");
        }

        /// <summary>
        /// <b>다른 시나리오가 쓰는 창구</b> — 유물을 실제로 주워 진행 설계 둘을 얻는다.
        ///
        /// 종막을 지나려면 액면 보행 장비와 잠항구가 필요하고, 그 둘은 이제 연구대의
        /// 산출물 뒤에 선다(백로그 38). 38 시점에는 유물이 세계에 없어 원장에 직접
        /// 적는 우회로를 두었는데, 이제 낫이 실제로 떨구므로 그 우회로가 필요 없다.
        /// <c>E2EDescent</c>가 이것을 부른다.
        /// </summary>
        public static IEnumerator 유물로_진행_설계를_얻는다()
        {
            yield return 준비();

            yield return 낫을_세운다();
            yield return 순찰하다_흘린_것을_줍는다(_ => { }, "첫 유물");
            yield return 순찰하다_흘린_것을_줍는다(_ => { }, "다음 유물");

            E2EHarness.Assert(Inv.CountOf(막) > 0 && Inv.CountOf(핵) > 0,
                              $"유물 둘을 다 주웠다 (막 {Inv.CountOf(막)}, 핵 {Inv.CountOf(핵)})");

            yield return 연구대를_세운다();
            yield return 분석한다(막연구, 보행설계);
            yield return 분석한다(핵연구, 잠항설계);

            E2EHarness.Assert(Ledger.IsUnlocked(보행설계) && Ledger.IsUnlocked(잠항설계),
                              "낫에게서 주워 온 것으로 진행 설계 둘을 밝혀냈다");

            yield return 치운다();
        }

        // ── 준비 ────────────────────────────────────────────────

        static IEnumerator 준비()
        {
            var dir = Object.FindAnyObjectByType<ChapterDirector>(FindObjectsInactive.Exclude);
            yield return E2EHarness.WaitUntil(() => dir != null && dir.Current != null,
                                              "챕터가 시작된다", 8f);
            yield return E2EHarness.WaitUntil(() => UI != null, "제작 UI가 있다", 8f);
            yield return E2EHarness.WaitUntil(() => UnlockService.Instance != null,
                                              "해금 서비스가 스스로 서 있다", 8f);

            var prefab = 프리팹();
            E2EHarness.Assert(prefab != null, "낫 프리팹을 찾았다");
            if (prefab == null) yield break;

            _def = prefab.GetComponent<CreatureHealth>()?.Definition;
            E2EHarness.Assert(_def != null && _def.id == "scythe", "낫 정의를 집었다");
            if (_def == null) yield break;

            // 데이터가 실제로 배선돼 있는가. 없으면 아래 기다림이 전부 시간 초과로만
            // 실패해서 "왜 안 나오는가"를 말해 주지 못한다.
            E2EHarness.Assert(_def.relicShed != null, "낫 정의에 유물 흘림 규칙이 물려 있다");
            if (_def.relicShed == null) yield break;
            E2EHarness.AssertEqual(_def.relicShed.relics.Length, 2, "흘릴 수 있는 유물 수");
            E2EHarness.Log($"  흘림: {_def.relicShed.intervalSeconds:F0}초마다 " +
                           $"{_def.relicShed.chance:P0} (평균 {_def.relicShed.MeanSecondsToDrop:F0}초), " +
                           $"관측 반경 {_def.relicShed.witnessRadius:F0}m");

            _lantern = Object.FindAnyObjectByType<LanternController>(FindObjectsInactive.Include);
            E2EHarness.Assert(_lantern != null, "랜턴이 있다");

            준다("lantern", 1 - Inv.CountOf("lantern"));
            준다("pickaxe", 1 - Inv.CountOf("pickaxe"));
            var toolUser = E2EHarness.Player.GetComponentInChildren<PlayerToolUser>(true);
            if (toolUser != null) toolUser.EquipFirst("pickaxe");

            // 앞선 시나리오가 남긴 앎과 물건을 지운다.
            //
            // <b>인벤토리를 통째로 비우는 것은 편의가 아니라 조건이다.</b> pity는
            // "무엇을 가졌는가"로 도는 규칙이라 손에 든 것이 그대로 입력이고, 자리가
            // 모자라면 주워도 들어오지 않는다 — 실제로 앞 시나리오가 채워 둔 자원
            // 때문에 두 번째 유물이 바닥에서 사라지지 않았다(실측: 줍고도 0개).
            // 랜턴과 곡괭이만 남긴다. 뒤에 필요한 것은 그때 다시 채운다.
            Ledger.Clear();
            소지품을_비운다();

            int 빈자리 = Inv.Slots.Count(s => s.item == null);
            E2EHarness.Assert(빈자리 >= 4, $"주운 것을 담을 자리가 있다 (빈 칸 {빈자리})");

            // 앞선 실행이 도중에 엎어졌으면 세워 둔 낫과 바닥의 것이 그대로 남는다.
            // 그 상태에서 다시 돌면 두 마리가 동시에 덤벼 무엇이 무엇인지 알 수 없다.
            남은것을_치운다();

            RelicShedder.ResetCounters();
            RelicShedder.IntervalOverrideSeconds = 굴림간격;
            E2EHarness.Log($"  굴림 간격을 {굴림간격:F0}초로 줄인다 (확률·pity는 그대로)");

            yield return 랜턴(false);
            Vitals.Health.Modify(Vitals.Health.Max);
            yield return null;
        }

        static GameObject 프리팹()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(낫프리팹);
#else
            return null;
#endif
        }

        /// <summary>랜턴과 곡괭이만 남기고 전부 내려놓는다.</summary>
        static void 소지품을_비운다()
        {
            var 남길것 = new[] { "lantern", "pickaxe" };

            for (int 회 = 0; 회 < 6; 회++)
            {
                var ids = Inv.Slots.Where(s => s.item != null && !남길것.Contains(s.item.id))
                                   .Select(s => s.item.id).Distinct().ToArray();
                if (ids.Length == 0) break;

                foreach (var id in ids) Inv.TryRemove(id, Inv.CountOf(id));
            }
        }

        static void 준다(string id, int 개수)
        {
            if (개수 <= 0) return;
            var db = E2EHarness.Player.Inventory.Database;
            var item = db != null ? db.GetById(id) : null;
            if (item != null) Inv.TryAdd(item, 개수);
        }

        // ── 낫을 세운다 ─────────────────────────────────────────

        /// <summary>
        /// 감지 반경 밖, 관측 반경 안, <b>어두운 곳</b>에 세운다.
        ///
        /// <b>앞의 두 숫자 사이가 이 기능이 사는 자리다.</b> 안쪽이면 낫이 쫓아와서
        /// 순찰이 성립하지 않고(쫓는 중에는 흘리지 않는다), 바깥이면 흘려 봐야
        /// 아무도 못 본다. "낫의 영역에 머문다"는 말의 내용이 이 띠다.
        ///
        /// <b>어둠은 나중에 붙인 조건이다.</b> 처음에는 거리만 보고 세웠더니 낫이
        /// 40초 내내 Flee였다 — 세운 자리가 발광 군락(<c>GlowGrove</c>)의 빛 안이었고,
        /// 빛을 꺼리는 이 생물은 거기서 영원히 물러나기만 한다(실측: 상태 Flee 고정,
        /// 흘린 것 0개). 그것은 결함이 아니라 규칙이 제대로 도는 모습이므로,
        /// 고칠 곳은 규칙이 아니라 세우는 자리다.
        /// </summary>
        static IEnumerator 낫을_세운다()
        {
            var prefab = 프리팹();
            if (prefab == null) yield break;

            _기다리는자리 = 사람자리;
            E2EHarness.Assert(_def.detectRadius + 12f < _def.relicShed.witnessRadius,
                              $"쫓기지 않는 거리({_def.detectRadius + 12f:F0}m)가 관측 반경 " +
                              $"{_def.relicShed.witnessRadius:F0}m 안에 있다");

            bool 놓았다 = false;
            for (int ring = 0; ring < 3 && !놓았다; ring++)
            {
                float 거리 = _def.detectRadius + 12f + ring * 6f;
                if (거리 > _def.relicShed.witnessRadius - 4f) break;

                for (int a = 0; a < 12 && !놓았다; a++)
                {
                    var dir = Quaternion.Euler(0f, a * 30f, 0f) * Vector3.forward;
                    if (!NavMesh.SamplePosition(_기다리는자리 + dir * 거리, out var hit, 10f, NavMesh.AllAreas))
                        continue;
                    if (Vector3.Distance(hit.position, _기다리는자리) < _def.detectRadius + 4f) continue;
                    if (!어둡다(hit.position)) continue;

                    var go = Object.Instantiate(prefab, hit.position, Quaternion.identity);
                    go.name = "E2E_낫";
                    _낫 = go.GetComponent<CreatureBrain>();
                    놓았다 = true;
                }
            }

            E2EHarness.Assert(놓았다, "감지 반경 밖 어두운 NavMesh 위에 낫을 세웠다");
            if (!놓았다) yield break;

            yield return null;
            yield return null;

            E2EHarness.Assert(_낫.GetComponent<RelicShedder>() != null,
                              "프리팹을 고치지 않았는데도 유물 흘리기가 붙어 있다");
            E2EHarness.Log($"  낫 {_낫.transform.position.ToString("F0")}, " +
                           $"사람과 {Vector3.Distance(_낫.transform.position, 사람자리):F1}m");
        }

        /// <summary>
        /// 이 자리도, 배회하다 닿을 만한 둘레도 어두운가. 중심만 보면 발광 군락
        /// 가장자리에 세워 놓고 곧 빛 속으로 걸어 들어가게 된다.
        /// </summary>
        static bool 어둡다(Vector3 자리)
        {
            if (LitZoneRegistry.IsLit(자리)) return false;

            for (int i = 0; i < 4; i++)
            {
                var 옆 = 자리 + Quaternion.Euler(0f, i * 90f, 0f) * Vector3.forward * 7f;
                if (LitZoneRegistry.IsLit(옆)) return false;
            }
            return true;
        }

        // ── 순찰 중 흘린 것을 줍는다 ────────────────────────────

        static IEnumerator 순찰하다_흘린_것을_줍는다(System.Action<string> result, string 무엇)
        {
            if (_낫 == null) { result(null); yield break; }

            E2EHarness.Log($"— {무엇}: 감지 밖에서 기다린다 —");
            yield return 랜턴(false);
            yield return 기다리는_자리로();

            int 이전 = RelicShedder.ShedCount;
            var 흘리기_직전상태 = CreatureState.Dead;

            float t = 0f;
            while (t < 60f && RelicShedder.ShedCount == 이전)
            {
                // 띠 안에 머문다. 안쪽으로 들어오면 쫓겨서 순찰이 아니게 되고,
                // 바깥으로 벗어나면 흘려도 아무도 못 본 것이 되어 굴리지도 않는다
                // (실측: 줍고 돌아온 사이 낫이 멀어져 54.7m, 60초 내내 하나도 안 나왔다).
                yield return 띠_안에_선다();

                // 흘리는 것은 Update 안에서 일어난다. 그 프레임의 상태를 남겨 두었다가
                // 세기가 늘어난 것을 확인한 다음에 읽는다 — 확인한 시점의 상태는
                // 이미 바뀌어 있을 수 있다.
                흘리기_직전상태 = _낫.State;

                t += Time.deltaTime;
                yield return null;
            }

            E2EHarness.Assert(RelicShedder.ShedCount > 이전,
                              $"{무엇}: 순찰 중 낫이 무언가를 흘렸다 " +
                              $"({t:F1}초, 상태 {_낫.State}, " +
                              $"제자리 밝음={LitZoneRegistry.IsLit(_낫.transform.position)}, " +
                              $"거리 {Vector3.Distance(_낫.transform.position, 사람자리):F1}m)");
            if (RelicShedder.ShedCount == 이전) { result(null); yield break; }

            E2EHarness.Assert(흘리기_직전상태 == CreatureState.Wander ||
                              흘리기_직전상태 == CreatureState.Idle,
                              $"흘릴 때 낫은 배회 중이었다 (상태 {흘리기_직전상태}) — " +
                              "쫓기다 떨어뜨린 것이 아니다");

            string id = RelicShedder.LastShedItemId;
            E2EHarness.Assert(id == 막 || id == 핵, $"{무엇}: 흘린 것이 유물이다 ({id})");
            E2EHarness.Log($"  흘렸다: {id} ({t:F1}초 기다림)");

            var pickup = 바닥에서_찾는다(id);
            E2EHarness.Assert(pickup != null, $"{무엇}: 바닥에 실제 줍기 대상이 놓였다");
            if (pickup == null) { result(null); yield break; }

            // 다가가려면 빛이 필요하다. 그것이 이 생물에 대한 이 게임의 답이다.
            yield return 랜턴(true);

            int 전 = Inv.CountOf(id);
            yield return 줍는다(pickup);
            E2EHarness.AssertEqual(Inv.CountOf(id), 전 + 1, $"{무엇}: {id}를 손에 넣었다");

            yield return 랜턴(false);
            result(id);
        }

        static ItemPickup 바닥에서_찾는다(string itemId)
        {
            string 이름 = "Drop_" + itemId;
            return Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Exclude)
                         .Where(p => p.name == 이름)
                         .OrderBy(p => Vector3.Distance(p.transform.position, _낫.transform.position))
                         .FirstOrDefault();
        }

        /// <summary>
        /// 눈앞에 서서 E를 눌러 줍는다. 실제 상호작용 경로를 그대로 쓴다.
        ///
        /// <b>설 자리는 수평으로 정한다.</b> 처음에는 카메라 정면의 반대편에 섰는데,
        /// 앞 시나리오가 위나 아래를 본 채로 끝나면 물건의 머리 위나 발밑에 서게 되어
        /// 조준에 잡히지 않았다(실측: 종막 시나리오 뒤에 4초 초과로 실패).
        /// 그래서 물건을 중심으로 수평 여덟 방향을 돌아 가며 잡히는 자리를 찾는다.
        /// </summary>
        static IEnumerator 줍는다(ItemPickup pickup)
        {
            Vitals.Health.Modify(Vitals.Health.Max);

            var 목표 = pickup.transform.position;
            Vector3 옆 = 사람자리 - 목표;
            옆.y = 0f;
            if (옆.sqrMagnitude < 0.04f) 옆 = Vector3.forward;
            옆.Normalize();

            var it = E2EHarness.Player.Interactor;

            for (int 시도 = 0; 시도 < 8; 시도++)
            {
                var dir = Quaternion.Euler(0f, 시도 * 45f, 0f) * 옆;
                var 설자리 = 목표 + dir * 1.7f + Vector3.up * 0.6f;
                if (NavMesh.SamplePosition(설자리, out var hit, 3f, NavMesh.AllAreas))
                    설자리 = hit.position + Vector3.up * 0.15f;

                for (int i = 0; i < 3; i++)
                {
                    E2EHarness.Teleport(설자리);
                    yield return null;
                }
                E2EHarness.LookAt(목표);
                yield return null;
                yield return null;

                float t = 0f;
                while (t < 0.5f && it.Current == null) { t += Time.deltaTime; yield return null; }

                if (it.Current == null) continue;

                E2EHarness.Log("  프롬프트: " + it.Current.InteractionPrompt);
                yield return E2EHarness.TapKey(Key.E);
                yield return null;
                yield return null;
                yield break;
            }

            E2EHarness.Assert(false,
                $"떨어진 것이 조준에 잡힌다 ({pickup.name} {목표.ToString("F1")}, " +
                $"사람 {사람자리.ToString("F1")})");
        }

        // ── pity ────────────────────────────────────────────────

        static IEnumerator 다른_종이었는지_본다(string 첫번째, string 두번째)
        {
            E2EHarness.Log("— 두 번째는 반드시 다른 종이다 —");
            E2EHarness.Assert(첫번째 != null && 두번째 != null, "유물 둘을 다 받았다");

            E2EHarness.Assert(첫번째 != 두번째,
                              $"미보유 우선이 실제로 걸려 있다 ({첫번째} 다음에 {두번째})");
            E2EHarness.AssertEqual(Inv.CountOf(막), 1, "낫의 막 하나");
            E2EHarness.AssertEqual(Inv.CountOf(핵), 1, "낫의 핵 하나");
            yield return null;
        }

        static IEnumerator 둘_다_쥐면_그친다()
        {
            E2EHarness.Log("— 둘 다 가졌으면 더 떨구지 않는다 —");
            if (_낫 == null) yield break;

            yield return 랜턴(false);
            yield return 기다리는_자리로();

            int 이전 = RelicShedder.ShedCount;

            // 평소라면 여러 번 나올 만큼 기다린다. 간격 1초·확률 25%면 20초에
            // 다섯 개쯤 나오는 것이 정상이다.
            float t = 0f;
            while (t < 20f)
            {
                yield return 띠_안에_선다();
                t += Time.deltaTime;
                yield return null;
            }

            E2EHarness.AssertEqual(RelicShedder.ShedCount, 이전,
                                   $"{t:F0}초 동안 하나도 더 떨구지 않았다 — 쓸 데 없는 스팸이 없다");
        }

        // ── 부품 ────────────────────────────────────────────────

        static IEnumerator 잡으면_제_부품을_떨군다()
        {
            E2EHarness.Log("— 잡으면 몸에서 부품이 나온다 —");

            var 필요 = 항목(낫도감연구)?.materials?.FirstOrDefault();
            int 모아야할것 = 필요 != null ? 필요.count : 2;
            E2EHarness.Log($"  {낫도감연구}는 {낫부품} {모아야할것}개를 요구한다");

            for (int 회차 = 1; Inv.CountOf(낫부품) < 모아야할것 && 회차 <= 4; 회차++)
            {
                if (_낫 == null) yield return 낫을_세운다();
                if (_낫 == null) yield break;

                yield return 때려_죽인다();
                yield return 떨어진_부품을_줍는다();
                E2EHarness.Log($"  {회차}마리째: {낫부품} {Inv.CountOf(낫부품)}개");
            }

            E2EHarness.Assert(Inv.CountOf(낫부품) >= 모아야할것,
                              $"잡아서 부품 {모아야할것}개를 모았다 (실제 {Inv.CountOf(낫부품)})");
        }

        static IEnumerator 때려_죽인다()
        {
            var 체력 = _낫.GetComponent<CreatureHealth>();
            yield return 랜턴(false);

            int 필요타수 = Mathf.CeilToInt(_def.maxHealth / 12f);
            for (int i = 0; i < 필요타수 + 8 && !체력.IsDead && _낫 != null; i++)
            {
                Vitals.Health.Modify(Vitals.Health.Max);
                yield return 붙어_선다(1.6f);
                E2EHarness.LookAt(_낫.transform.position + Vector3.up * 0.9f);
                yield return null;
                yield return E2EHarness.ClickAttack();

                float 쉼 = 0f;
                while (쉼 < 0.45f && !체력.IsDead) { 쉼 += Time.deltaTime; yield return null; }
            }

            E2EHarness.Assert(체력.IsDead, "곡괭이를 휘둘러 죽였다");
            Vitals.Health.Modify(Vitals.Health.Max);
        }

        static IEnumerator 떨어진_부품을_줍는다()
        {
            yield return E2EHarness.WaitUntil(
                () => Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Exclude)
                            .Any(p => p.name == "Drop_" + 낫부품),
                "죽은 자리에 부품이 떨어졌다", 5f);

            // 한 번에 여럿 나올 수 있다(1~2개). 남김없이 줍는다.
            for (int i = 0; i < 4; i++)
            {
                var p = Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Exclude)
                              .FirstOrDefault(x => x.name == "Drop_" + 낫부품);
                if (p == null) break;
                yield return 줍는다(p);
            }
        }

        // ── 도감 ────────────────────────────────────────────────

        static IEnumerator 부품을_분석하면_도감이_열린다()
        {
            E2EHarness.Log("— 부품을 연구대에 넣으면 도감 한 줄이 열린다 —");

            yield return 연구대를_세운다();
            if (_station == null) yield break;

            E2EHarness.Assert(!Ledger.IsUnlocked(낫도감열쇠), "아직 낫이 무엇인지 모른다");

            var entry = 항목(낫도감연구);
            E2EHarness.Assert(entry != null, "낫 도감 항목을 찾았다");
            if (entry == null) yield break;

            E2EHarness.AssertEqual(entry.unlocks.Length, 0,
                                   "도감은 청사진을 열지 않는다 — 만드는 법이 아니다");
            E2EHarness.AssertEqual(entry.unlockKeys.Length, 1, "여는 열쇠 수");
            E2EHarness.AssertEqual(entry.unlockKeys[0], 낫도감열쇠, "여는 열쇠 이름");

            yield return 분석한다(낫도감연구, 낫도감열쇠);

            E2EHarness.Log($"  AI: {UnlockService.Instance.LastLine}");
            E2EHarness.Assert(UnlockService.Instance.LastLine.EndsWith("개체의 정보가 정리되었습니다."),
                              "도감의 정형구로 닫는다 — 제작법을 얻은 것과 다른 문장이다");
            E2EHarness.Assert(!UnlockService.Instance.LastLine.Contains("제작법"),
                              "제작법을 얻은 것처럼 말하지 않는다");
        }

        // ── 연구대 ──────────────────────────────────────────────

        static IEnumerator 연구대를_세운다()
        {
            if (_station != null) yield break;

            // 짓는 비용은 여기서 볼 것이 아니다. 배치 절차는 E2EBaseBuilding이,
            // 연구대의 비용 사슬은 E2EResearchStation이 이미 실제 조작으로 본다.
            준다("scrap", 60 - Inv.CountOf("scrap"));
            준다("machine_part", 60 - Inv.CountOf("machine_part"));
            준다(Survive.Harvesting.MushroomLumberRule.WoodItemId,
                 60 - Inv.CountOf(Survive.Harvesting.MushroomLumberRule.WoodItemId));

            E2EHarness.Assert(Ledger.IsUnlocked("bp_salvaged_mechanism"),
                              "부품을 쥐자 '부품 재조립'이 열렸다 — 연구대는 그 뒤에 선다");

            yield return 기다리는_자리로();

            var go = new GameObject[1];
            yield return E2EResearchStation.세운다(연구대, g => go[0] = g);
            E2EHarness.Assert(go[0] != null, "연구대를 세웠다");
            if (go[0] == null) yield break;

            // 이름을 붙여 둔다. 앞선 실행이 엎어져 남은 연구대가 그대로 있으면
            // 다음 실행이 놓을 자리를 못 찾는다(실측: 겹침 판정에 막혀 배치 실패).
            go[0].name = "E2E_연구대";
            _station = go[0].GetComponentInChildren<ResearchStation>(true);
            E2EHarness.Assert(_station != null, "세운 것에 연구대 부품이 붙어 있다");
        }

        /// <summary>연구대 앞에 서서 줄을 눌러 걸고, 다 볼 때까지 기다린다.</summary>
        static IEnumerator 분석한다(string 연구id, string 열릴열쇠)
        {
            if (_station == null) yield break;

            var entry = 항목(연구id);
            E2EHarness.Assert(entry != null, $"연구 항목을 찾았다: {연구id}");
            if (entry == null) yield break;

            int 끝낸것 = _station.CompletedCount;
            int 말한줄수 = UnlockService.Instance.LinesSpoken;

            _station.Interact(E2EHarness.Player);
            yield return null;
            yield return null;

            var row = UI.GetComponentsInChildren<Button>(true)
                        .FirstOrDefault(b => b.gameObject.name == "Row_" + 연구id);
            E2EHarness.Assert(row != null, $"{연구id} 줄이 연구대 목록에 뜬다");
            if (row == null) yield break;

            E2EHarness.Assert(row.interactable, $"{연구id}를 걸 수 있다 — 소재도 스크랩도 있다");
            ExecuteEvents.Execute(row.gameObject, new PointerEventData(EventSystem.current),
                                  ExecuteEvents.pointerClickHandler);
            yield return null;

            E2EHarness.Assert(_station.Work.Count > 0, $"{연구id}가 연구대에 걸렸다");
            E2EHarness.Assert(!Ledger.IsUnlocked(열릴열쇠), "걸었다고 바로 알게 되지는 않는다");

            UI.Close();
            yield return null;

            float 이전배속 = Time.timeScale;
            Time.timeScale = 8f;
            yield return E2EHarness.WaitUntil(() => _station.CompletedCount > 끝낸것,
                                              $"연구대가 {연구id}를 다 보았다 " +
                                              $"({entry.researchSeconds:F0}초)",
                                              entry.researchSeconds + 12f);
            Time.timeScale = 이전배속;

            E2EHarness.Assert(Ledger.IsUnlocked(열릴열쇠),
                              $"{열릴열쇠}가 원장에 적혔다 — 산출물은 물건이 아니라 앎이다");

            yield return E2EHarness.WaitUntil(
                () => UnlockService.Instance.LinesSpoken > 말한줄수,
                "우주복 AI가 알아낸 것을 말했다", 8f);
        }

        static ResearchEntrySO 항목(string id)
        {
            var book = _station != null && _station.Book != null
                     ? _station.Book
                     : Resources.Load<ResearchBookSO>("ResearchBook");
            return book != null ? book.Find(id) : null;
        }

        // ── 자리 잡기 ───────────────────────────────────────────

        static IEnumerator 기다리는_자리로()
        {
            if (_기다리는자리 == Vector3.zero) yield break;

            for (int i = 0; i < 4; i++)
            {
                E2EHarness.Teleport(_기다리는자리);
                yield return null;
            }

            if (_낫 != null && Vector3.Distance(_낫.transform.position, 사람자리) < _def.detectRadius + 3f)
                yield return 물러난다(_def.detectRadius + 12f);
        }

        /// <summary>
        /// 낫에게서 <paramref name="거리m"/>만큼, <b>걸을 수 있는 자리로</b> 물러난다.
        /// 직선 뒤가 언제나 땅인 것은 아니다 — 부유섬 가장자리로 물러나면 허공이다
        /// (<c>E2EConsumer</c>가 같은 이유로 같은 방식을 쓴다).
        /// </summary>
        static IEnumerator 물러난다(float 거리m)
        {
            Vector3 기준 = _낫.transform.position;
            Vector3 뒤 = 사람자리 - 기준;
            뒤.y = 0f;
            if (뒤.sqrMagnitude < 0.01f) 뒤 = Vector3.forward;
            뒤.Normalize();

            for (int i = 0; i < 12; i++)
            {
                float 각 = (i % 2 == 0 ? 1f : -1f) * 30f * ((i + 1) / 2);
                Vector3 후보 = 기준 + (Quaternion.Euler(0f, 각, 0f) * 뒤) * 거리m;
                if (!NavMesh.SamplePosition(후보, out var hit, 15f, NavMesh.AllAreas)) continue;
                if (Vector3.Distance(hit.position, 기준) < 거리m * 0.7f) continue;

                for (int k = 0; k < 4; k++)
                {
                    E2EHarness.Teleport(hit.position + Vector3.up * 0.2f);
                    yield return null;
                }
                yield break;
            }

            for (int k = 0; k < 4; k++)
            {
                E2EHarness.Teleport(기준 + 뒤 * 거리m + Vector3.up * 0.2f);
                yield return null;
            }
        }

        /// <summary>
        /// 감지 반경 밖·관측 반경 안이라는 <b>띠</b> 안에 머문다. 어느 쪽으로 벗어났든
        /// 다시 그 한가운데로 선다 — 안쪽이면 쫓기고, 바깥이면 굴리지도 않는다.
        /// </summary>
        static IEnumerator 띠_안에_선다()
        {
            if (_낫 == null) yield break;

            float d = Vector3.Distance(_낫.transform.position, 사람자리);
            if (d >= _def.detectRadius + 3f && d <= _def.relicShed.witnessRadius - 6f) yield break;

            yield return 물러난다(_def.detectRadius + 12f);
        }

        static IEnumerator 붙어_선다(float 거리m)
        {
            Vector3 기준 = _낫.transform.position;
            Vector3 dir = 사람자리 - 기준;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = _낫.transform.forward;

            Vector3 목표 = 기준 + dir.normalized * 거리m;
            if (NavMesh.SamplePosition(목표, out var hit, 3f, NavMesh.AllAreas)) 목표 = hit.position;
            목표.y += 0.1f;

            for (int i = 0; i < 4; i++)
            {
                E2EHarness.Teleport(목표);
                yield return null;
            }
        }

        static IEnumerator 랜턴(bool 켜기)
        {
            if (_lantern == null) yield break;

            for (int i = 0; i < 3 && _lantern.IsOn != 켜기; i++)
            {
                yield return E2EHarness.TapKey(Key.F);
                yield return null;
            }

            if (켜기) _lantern.Recharge(100f);
            yield return null;
        }

        // ── 치우기 ──────────────────────────────────────────────

        /// <summary>
        /// 이 시나리오가 세계에 놓은 것을 걷어낸다. 시작할 때도 부른다 —
        /// 앞선 실행이 도중에 엎어지면 <see cref="치운다"/>까지 가지 못하고,
        /// 남은 낫이 다음 시나리오의 생물과 섞여 엉뚱한 실패를 만든다.
        /// </summary>
        static void 남은것을_치운다()
        {
            _낫 = null;
            _station = null;

            foreach (var b in Object.FindObjectsByType<CreatureBrain>(FindObjectsInactive.Include))
                if (b != null && b.name.StartsWith("E2E_낫"))
                    Object.Destroy(b.gameObject);

            foreach (var s in Object.FindObjectsByType<ResearchStation>(FindObjectsInactive.Include))
                if (s != null && s.transform.root.name == "E2E_연구대")
                    Object.Destroy(s.transform.root.gameObject);

            foreach (var p in Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Exclude))
                if (p.name == "Drop_" + 막 || p.name == "Drop_" + 핵 || p.name == "Drop_" + 낫부품)
                    Object.Destroy(p.gameObject);
        }

        static IEnumerator 치운다()
        {
            RelicShedder.IntervalOverrideSeconds = 0f;

            남은것을_치운다();

            yield return 랜턴(false);
            yield return E2EHarness.ReleaseAllKeys();
            Time.timeScale = 1f;
            Vitals.Health.Modify(Vitals.Health.Max);
            yield return null;
        }
    }
}

