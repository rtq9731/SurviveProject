using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using Survive.Building;
using Survive.Core;
using Survive.Crafting;
using Survive.Harvesting;
using Survive.Interaction;
using Survive.Items;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// <b>쌓아 둔 저장을 실제로 믿을 수 있는가.</b> 새 계층을 만들지 않는다.
    ///
    /// 앞 세 라운드가 저장을 세 겹으로 쌓았다 — 원장(달라진 것) · 생성 목록(없던 것) ·
    /// 딸림(몸에 붙은 것). 그런데 그 셋을 실제로 <b>씬 로드 너머</b>로 재 본 검사가
    /// 하나도 없었다. 앞 검사들은 몸을 <b>손으로 없애고</b> 불러왔다. 그것은
    /// 「원래 서 있던 것이 계속 서 있었다」와는 구별되지만, <b>씬 로드가 정적·등록
    /// 상태에 하는 일</b>은 하나도 재지 않는다.
    ///
    /// 여기서 넷을 본다.
    /// <list type="number">
    /// <item><b>여섯 계층이 진짜 씬 로드를 건넌다</b> (3회 연속). 짓고 · 넣고 ·
    ///   걸고 · 캐고 · 떨구고 → 저장 → <b>씬을 실제로 다시 올리고</b> → 불러오기.</item>
    /// <item><b>절 순서가 반대인 저장본이 그냥 열린다.</b> 딸림이 제 절을 갖던
    ///   시절의 저장본은 그 절이 「세계」 절 <b>앞에</b> 있을 수 있다.</item>
    /// <item><b>씬에 놓인 제작대</b>의 대기열이 저장과 씬 로드를 건넌다.
    ///   ①의 여섯 번째 계층이 이것이다.</item>
    /// <item><b>레시피 조회가 빌드에서 도는 길로 답한다.</b> 에디터에서만 도는
    ///   길에 기대면 걸어 둔 제작이 빌드에서만 조용히 사라진다.</item>
    /// </list>
    ///
    /// <b>기본 슬롯을 밟지 않는다.</b> 세 클론과 사람의 에디터가 같은 저장 폴더를
    /// 나눠 쓴다. 이 검사는 자기 슬롯만 쓰고 끝나면 지운다.
    /// </summary>
    public static class E2ESaveTrust
    {
        const string 슬롯 = "e2e_save_trust";
        const string 뒤집은슬롯 = "e2e_save_trust_flip";

        /// <summary>맡길 물건의 수. 회차마다 달리해 앞 판의 값이 새 나오는 것을 잡는다.</summary>
        const int 기본합금 = 4;

        static readonly List<string> _표 = new List<string>();

        public static IEnumerator FullRun()
        {
            _표.Clear();

            yield return 준비();
            yield return 여섯_계층이_씬_로드를_건넌다();
            yield return 절_순서가_반대인_저장본이_그냥_열린다();
            yield return 레시피_조회가_빌드에서_도는_길로_답한다();
            yield return 뒷정리();

            E2EHarness.Log("");
            E2EHarness.Log("═══ 실측표 ═══");
            foreach (var line in _표) E2EHarness.Log("  " + line);
            E2EHarness.Log("=== 저장 신뢰 완주 ===");
        }

        static void 적는다(string 항목, string 값) => _표.Add($"{항목} | {값}");

        // ── 준비 ────────────────────────────────────────────────

        static SaveCoordinator 저장소;
        static RecipeSO 불레시피;
        static RecipeSO 제작대레시피;

        static BuildPlacer Placer =>
            Object.FindAnyObjectByType<BuildPlacer>(FindObjectsInactive.Exclude);

        static Inventory Bag => E2EHarness.Player.Inventory.Inventory;
        static ItemDatabaseSO Db => E2EHarness.Player.Inventory.Database;

        static IEnumerator 준비()
        {
            yield return 씬이_깨어나기를_기다린다();

            E2EHarness.Assert(WorldLedgerService.Instance != null, "세계 원장이 스스로 붙었다");
            E2EHarness.Assert(Db != null, "아이템 데이터베이스가 연결돼 있다");

            불레시피 = 레시피들().FirstOrDefault(r => r != null &&
                                                      r.requiredStation == StationType.Campfire);
            E2EHarness.Assert(불레시피 != null, "화톳불에서 도는 레시피가 있다");
            if (불레시피 != null)
                E2EHarness.Log($"  화톳불 레시피: {불레시피.id} ({불레시피.craftSeconds:F0}초/개)");

            // 제작대 레시피는 <b>지금 걸 수 있는 것</b> 중에서 고른다. 가장 긴 것을
            // 손으로 박아 두면 그것이 청사진에 잠기는 날 검사만 조용히 빨개진다.
            재료를_채운다();
            제작대레시피 = 걸_수_있는_제작대레시피();
            E2EHarness.Assert(제작대레시피 != null, "지금 제작대에 걸 수 있는 레시피가 있다");
            if (제작대레시피 != null)
                E2EHarness.Log($"  제작대 레시피: {제작대레시피.id} " +
                               $"({제작대레시피.craftSeconds:F0}초/개)");

            yield return null;
        }

        /// <summary>
        /// 지금 제작대에 두 개 걸 수 있는 레시피 중 가장 긴 것. 재료를 들려주면서
        /// 실제로 <c>MaxCraftable</c>에 물어본다 — 청사진에 잠긴 것을 고르면
        /// 「걸었다」가 거짓이 되고, 짧은 것을 고르면 저장하기 전에 끝나 버린다.
        /// </summary>
        static RecipeSO 걸_수_있는_제작대레시피()
        {
            var 후보들 = 레시피들()
                .Where(r => r != null && r.requiredStation == StationType.Bench &&
                            r.craftSeconds > 1f && r.result?.item != null)
                .OrderByDescending(r => r.craftSeconds);

            foreach (var r in 후보들)
            {
                재료를_들려준다(r);
                if (CraftQueueService.MaxCraftable(r, Bag, StationType.Bench,
                                                   Survive.Progression.BlueprintGate.Active) >= 2)
                    return r;
            }
            return null;
        }

        /// <summary>
        /// 씬이 올라온 직후에 부른다. 재생 시작과 <b>씬을 다시 올린 뒤</b> 양쪽에서
        /// 같은 것을 기다린다 — 사람·배치기·저장소·원장이 전부 서 있어야
        /// 시나리오가 무엇을 재는지 말할 수 있다.
        /// </summary>
        static IEnumerator 씬이_깨어나기를_기다린다()
        {
            yield return E2EHarness.WaitUntil(
                () => Object.FindAnyObjectByType<SaveCoordinator>(FindObjectsInactive.Include) != null,
                "SaveCoordinator가 씬에 있다", 15f);

            저장소 = Object.FindAnyObjectByType<SaveCoordinator>(FindObjectsInactive.Include);

            yield return E2EHarness.WaitUntil(() => Placer != null, "BuildPlacer가 준비된다", 15f);
            yield return E2EHarness.WaitUntil(() => 씬제작대() != null,
                                              "씬에 놓인 제작대가 서 있다", 15f);

            // 씬이 다시 올라오면 앞 씬에서 재운 생물은 사라지고 새 무리가 깨어 있다.
            int 잠든것 = E2EHarness.SleepWildCreatures();
            E2EHarness.Log($"  야생 생물 {잠든것}마리를 재웠다");

            // 실행 시점에 붙은 것들(원장·시계)까지 저장 대상 목록에 들여야 한다.
            int 모은것 = 저장소.Collect();
            E2EHarness.Log($"  저장 대상 {모은것}개를 모았다");
            yield return null;
        }

        static IEnumerator 뒷정리()
        {
            치운다();
            저장소?.Delete(슬롯);
            저장소?.Delete(뒤집은슬롯);
            E2EHarness.Log("  검사용 슬롯을 지웠다 (기본 슬롯은 건드리지 않았다)");
            yield return null;
        }

        // ══ ① 여섯 계층이 진짜 씬 로드를 건넌다 ═════════════════

        /// <summary>
        /// <b>이것이 본체다.</b> 지금까지 세운 것 전부가 한 시나리오를 지난다.
        ///
        /// <list type="number">
        /// <item><b>원장 등록부</b> — 캔 자리가 캔 채로 있다.</item>
        /// <item><b>생성 목록 · 몸</b> — 세운 보관함과 화톳불이 그 자리에 다시 선다.</item>
        /// <item><b>생성 목록 · 낙하물</b> — 떨군 것이 내려앉은 자리에 그대로 있다.</item>
        /// <item><b>딸림 · 보관함</b> — 맡긴 물건이 그대로다.</item>
        /// <item><b>딸림 · 걸어 둔 제작</b> — 대기열과 회수함이 이어진다.</item>
        /// <item><b>씬에 놓인 제작대</b> — 몸이 씬의 것이라 제 절을 쓰는 갈래다.</item>
        /// </list>
        /// 시계와 시드는 여섯을 떠받치는 값이라 따로 잰다.
        ///
        /// <b>손으로 없애지 않는다.</b> <c>SceneManager.LoadSceneAsync</c>로 씬을
        /// 통째로 다시 올린다 — 등록부가 비워지고, 정적인 것만 살아 넘어오고,
        /// 새 <c>SaveService</c>가 새로 모은다. 앞 검사들이 재지 못한 것이 그 사이다.
        ///
        /// <b>손에 든 원장을 저장 직후에 비운다.</b> 원장 서비스는
        /// <c>DontDestroyOnLoad</c>라 씬 로드를 살아 넘는다. 안 비우면 새로 선
        /// 채집물이 태어나는 자리에서 <b>메모리에 남아 있던 줄</b>을 받아,
        /// 이 검사가 파일을 거치지 않고도 통과한다.
        /// </summary>
        static IEnumerator 여섯_계층이_씬_로드를_건넌다()
        {
            E2EHarness.Log("— 여섯 계층이 진짜 씬 로드를 건넌다 (3회 연속) —");

            for (int 회 = 1; 회 <= 3; 회++)
            {
                E2EHarness.Log($"  ── {회}회차 ──");

                치운다();
                재료를_채운다();
                yield return null;

                int 맡길합금 = 기본합금 + 회;        // 회차마다 다르게. 앞 판의 값과 섞이면 드러난다
                int 맡길잔해 = 5 + 회 * 2;
                int 걸것 = 1 + 회;

                // ── ① 캐고 ──────────────────────────────────────
                var 노드 = 안_캔_노드();
                E2EHarness.Assert(노드 != null, $"{회}회차: 아직 안 캔 흩어진 잔해가 있다");
                if (노드 == null) yield break;

                string 노드신원 = 노드.WorldId;
                노드.Interact(E2EHarness.Player);
                yield return null;
                E2EHarness.Assert(노드.IsDepleted, $"{회}회차: 잔해를 캤다");

                // ── ② 짓고 · ④ 넣고 ────────────────────────────
                GameObject 보관몸 = null;
                yield return 세운다("storage", go => 보관몸 = go);
                E2EHarness.Assert(보관몸 != null, $"{회}회차: 보관함을 세웠다");
                if (보관몸 == null) yield break;

                var 보관함 = 보관몸.GetComponent<StorageContainer>();
                var 보관자리 = 보관몸.transform.position;

                var 합금 = Db.GetById("alien_alloy");
                var 잔해 = Db.GetById("scrap");
                E2EHarness.Assert(합금 != null && 잔해 != null, $"{회}회차: 맡길 물건 정의를 찾았다");
                if (합금 == null || 잔해 == null) yield break;

                Bag.TryRemove("alien_alloy", 맡길합금);
                보관함.Contents.TryAdd(합금, 맡길합금);
                Bag.TryRemove("scrap", 맡길잔해);
                보관함.Contents.TryAdd(잔해, 맡길잔해);

                // ── ② 짓고 · ⑤ 걸고 ────────────────────────────
                GameObject 불몸 = null;
                yield return 세운다("campfire", go => 불몸 = go);
                E2EHarness.Assert(불몸 != null, $"{회}회차: 화톳불을 세웠다");
                if (불몸 == null) yield break;

                var 불자리 = 불몸.transform.position;
                var 불일 = 불몸.GetComponent<ICraftStation>();
                E2EHarness.Assert(불일?.Work != null, $"{회}회차: 화톳불이 대기열을 갖는다");
                if (불일?.Work == null) yield break;

                // 세우고 맡기느라 재료가 빠졌다. 걸기 직전에 걸 만큼만 맞춘다.
                재료를_들려준다(불레시피, 걸것 + 1);

                bool 걸렸나 = CraftQueueService.TryEnqueue(
                    불일.Work.Queue, 불레시피, 걸것, Bag, StationType.Campfire,
                    Survive.Progression.BlueprintGate.Active);
                E2EHarness.Assert(걸렸나, $"{회}회차: 화톳불에 추출 {걸것}개를 걸었다");
                if (!걸렸나) yield break;

                var 불결과물 = 불레시피.result?.item;
                if (불결과물 != null) 불일.Work.Output.TryAdd(불결과물, 2);

                int 불남은것 = 불일.Work.Queue.Active?.Remaining ?? -1;

                // ── ⑥ 씬에 놓인 제작대 ─────────────────────────
                var 제작대 = 씬제작대();
                E2EHarness.Assert(제작대 != null, $"{회}회차: 씬에 놓인 제작대를 찾았다");
                if (제작대 == null) yield break;

                string 제작대열쇠 = 제작대.SaveKey;
                CraftQueueService.CancelAll(제작대.Work.Queue, Bag);
                제작대레시피 = 걸_수_있는_제작대레시피() ?? 제작대레시피;
                E2EHarness.Assert(제작대레시피 != null, $"{회}회차: 제작대 레시피를 골랐다");
                if (제작대레시피 == null) yield break;

                bool 제작대에걸렸나 = CraftQueueService.TryEnqueue(
                    제작대.Work.Queue, 제작대레시피, 2, Bag, StationType.Bench,
                    Survive.Progression.BlueprintGate.Active);
                E2EHarness.Assert(제작대에걸렸나,
                                  $"{회}회차: 씬 제작대에 {제작대레시피.id} 2개를 걸었다");
                if (!제작대에걸렸나) yield break;

                var 제작대결과물 = 제작대레시피.result?.item;
                if (제작대결과물 != null) 제작대.Work.Output.TryAdd(제작대결과물, 1);
                int 제작대남은것 = 제작대.Work.Queue.Active?.Remaining ?? -1;

                // 회수함은 <b>회차를 넘어 쌓인다</b>. 씬의 제작대는 몸이 하나뿐이고
                // 앞 회차가 되살린 것이 그대로 남아 있기 때문이다. 기댓값을 손으로
                // 박으면 2회차에서 어긋나므로 지금 값을 그대로 적어 둔다.
                int 제작대회수함 = 제작대.Work.OutputCount;

                // ── ③ 떨구고 ────────────────────────────────────
                var 눈 = E2EHarness.Eye.transform;
                var 떨군것 = ItemDropper.Drop(합금, 3, 눈.position + 눈.forward * 3f, occasion: 회);
                E2EHarness.Assert(떨군것 != null, $"{회}회차: 합금을 떨궜다");
                if (떨군것 == null) yield break;

                for (int i = 0; i < 40; i++) yield return null;   // 착지 트윈을 기다린다
                var 줍기 = 떨군것.GetComponent<ItemPickup>();
                E2EHarness.Assert(줍기 != null && 줍기.Spawned,
                                  $"{회}회차: 떨군 것이 태어난 것으로 표시됐다");
                if (줍기 == null) yield break;
                var 내려앉은자리 = 줍기.RestAt;

                // ── 시계와 시드 ────────────────────────────────
                float 저장시각 = WorldClock.Seconds;
                int 저장시드 = WorldSeed.Value;

                // ── 저장 ────────────────────────────────────────
                저장소.Save(슬롯);
                yield return null;

                string 경로 = E2EHarness.SlotPath(슬롯);
                E2EHarness.Assert(File.Exists(경로), $"{회}회차: 저장본 파일이 생겼다");

                var 저장본 = 파일에서_읽는다(경로);
                E2EHarness.Assert(저장본 != null, $"{회}회차: 저장본을 다시 읽어 냈다");
                if (저장본 == null) yield break;

                var 세계절 = 저장본.Find(WorldLedgerService.Key);
                E2EHarness.Assert(세계절 != null, $"{회}회차: 파일에 「세계」 절이 있다");
                E2EHarness.Assert(세계절 != null && 세계절.json.Contains("holds"),
                                  $"{회}회차: 생성 목록의 줄이 딸림 칸을 싣는다");
                E2EHarness.Assert(세계절 != null && 세계절.json.Contains(불레시피.id),
                                  $"{회}회차: 파일에 걸어 둔 제작이 적혀 있다");
                E2EHarness.Assert(저장본.Find(제작대열쇠) != null,
                    $"{회}회차: 씬에 놓인 제작대가 제 절을 갖는다 ({제작대열쇠})");
                E2EHarness.Assert(세계절 != null && 세계절.json.Contains("\"seed\""),
                                  $"{회}회차: 파일에 시드 칸이 있다");

                int 파일크기 = new FileInfo(경로).Length > 0 ? (int)new FileInfo(경로).Length : 0;
                E2EHarness.Log($"  {회}회차: 저장 {파일크기}바이트 · 절 {저장본.Count}개 · " +
                               $"시계 {저장시각:F1}초 · 시드 {저장시드}");

                // ── 진실을 파일에만 남긴다 ──────────────────────
                WorldLedgerService.Instance?.Ledger.Clear();

                // ── 씬을 실제로 다시 올린다 ─────────────────────
                E2EHarness.Log($"  {회}회차: 씬을 실제로 다시 올린다");
                var op = SceneManager.LoadSceneAsync("MainScene");
                while (op != null && !op.isDone) yield return null;
                yield return null;
                yield return null;

                yield return 씬이_깨어나기를_기다린다();

                // ── 음성 확인: 새 세계는 아무것도 모른다 ────────
                // 이것이 거짓이면 아래 단언은 전부 「원래 있던 것을 다시 본 것」이다.
                E2EHarness.AssertEqual(BuiltStructure.Active.Count(b => b != null && b.Spawned), 0,
                    $"{회}회차: 다시 올린 씬에는 세운 것이 하나도 없다");
                E2EHarness.AssertEqual(ItemPickup.Active.Count(p => p != null && p.Spawned), 0,
                    $"{회}회차: 다시 올린 씬에는 떨군 것도 없다");

                var 새노드 = 신원으로_찾는다(노드신원);
                E2EHarness.Assert(새노드 != null,
                    $"{회}회차: 같은 신원의 채집물이 다시 올라온 씬에 있다 ({노드신원})");
                E2EHarness.Assert(새노드 == null || !새노드.IsDepleted,
                    $"{회}회차: 새로 선 채집물은 아무것도 모른 채 서 있다");

                var 새제작대 = 씬제작대();
                E2EHarness.Assert(새제작대 != null && 새제작대.Work.Queue.IsEmpty,
                    $"{회}회차: 다시 올린 씬의 제작대는 대기열이 비어 있다");
                E2EHarness.AssertEqual(새제작대 != null ? 새제작대.SaveKey : null, 제작대열쇠,
                    $"{회}회차: 다시 올린 제작대의 저장 열쇠가 같다");

                // 시드를 일부러 딴 값으로 바꿔 둔다. 안 바꾸면 정적인 값이 그냥
                // 살아 넘어온 것과 「파일에서 돌아온 것」을 구별할 수 없다.
                int 흐트러뜨린시드 = 저장시드 ^ 0x5EED5EED;
                if (흐트러뜨린시드 == 저장시드) 흐트러뜨린시드++;
                WorldSeed.Restore(흐트러뜨린시드);
                E2EHarness.Assert(WorldSeed.Value != 저장시드,
                                  $"{회}회차: 불러오기 전에 시드를 딴 값으로 흐트러뜨렸다");

                float 씬로드뒤시각 = WorldClock.Seconds;

                // ── 불러오기 ────────────────────────────────────
                E2EHarness.Assert(저장소.Load(슬롯), $"{회}회차: 저장본이 열렸다");
                yield return null;
                yield return null;

                // ── 계층 ① 원장 등록부 ─────────────────────────
                var 되찾은노드 = 신원으로_찾는다(노드신원);
                E2EHarness.Assert(되찾은노드 != null && 되찾은노드.IsDepleted,
                    $"{회}회차: [① 원장] 캔 자리가 씬 로드를 건너서도 캔 채로 있다");

                // ── 계층 ② 생성 목록 · 몸 ──────────────────────
                var 되살아난보관 = BuiltStructure.Active
                    .Where(b => b != null && b.Spawned && b.Definition != null &&
                                b.Definition.id == "storage")
                    .OrderBy(b => Vector3.Distance(b.transform.position, 보관자리))
                    .FirstOrDefault();
                E2EHarness.Assert(되살아난보관 != null,
                    $"{회}회차: [② 생성 목록] 보관함이 씬 로드를 건너 다시 섰다");
                if (되살아난보관 == null) yield break;

                float 보관어긋남 = Vector3.Distance(되살아난보관.transform.position, 보관자리);
                E2EHarness.Assert(보관어긋남 < 0.01f,
                    $"{회}회차: [② 생성 목록] 세웠던 그 자리다 ({보관어긋남:F4}m)");

                var 되살아난불 = BuiltStructure.Active
                    .Where(b => b != null && b.Spawned && b.Definition != null &&
                                b.Definition.id == "campfire")
                    .OrderBy(b => Vector3.Distance(b.transform.position, 불자리))
                    .FirstOrDefault();
                E2EHarness.Assert(되살아난불 != null,
                    $"{회}회차: [② 생성 목록] 화톳불도 다시 섰다");
                if (되살아난불 == null) yield break;

                // ── 계층 ③ 생성 목록 · 낙하물 ──────────────────
                var 되살아난낙하물 = ItemPickup.Active
                    .Where(p => p != null && p.Spawned && p.Item != null &&
                                p.Item.id == "alien_alloy")
                    .OrderBy(p => Vector3.Distance(p.RestAt, 내려앉은자리))
                    .FirstOrDefault();
                E2EHarness.Assert(되살아난낙하물 != null,
                    $"{회}회차: [③ 낙하물] 떨군 것이 씬 로드를 건너 다시 놓였다");
                if (되살아난낙하물 != null)
                {
                    float 낙하어긋남 = Vector3.Distance(되살아난낙하물.RestAt, 내려앉은자리);
                    E2EHarness.Assert(낙하어긋남 < 0.01f,
                        $"{회}회차: [③ 낙하물] 내려앉은 그 자리다 ({낙하어긋남:F4}m)");
                    E2EHarness.AssertEqual(되살아난낙하물.Count, 3,
                        $"{회}회차: [③ 낙하물] 수량도 그대로다");
                }

                // ── 계층 ④ 딸림 · 보관함 ───────────────────────
                var 되살아난내용물 = 되살아난보관.GetComponent<StorageContainer>();
                E2EHarness.Assert(되살아난내용물 != null,
                    $"{회}회차: 되살아난 보관함에 보관 기능이 있다");
                if (되살아난내용물 != null)
                {
                    E2EHarness.AssertEqual(되살아난내용물.Contents.CountOf("alien_alloy"), 맡길합금,
                        $"{회}회차: [④ 딸림] 맡긴 합금 {맡길합금}개가 그대로다");
                    E2EHarness.AssertEqual(되살아난내용물.Contents.CountOf("scrap"), 맡길잔해,
                        $"{회}회차: [④ 딸림] 맡긴 잔해 {맡길잔해}개도 그대로다");
                }

                // ── 계층 ⑤ 딸림 · 걸어 둔 제작 ─────────────────
                var 되살아난불일 = 되살아난불.GetComponent<ICraftStation>();
                E2EHarness.Assert(되살아난불일?.Work != null,
                    $"{회}회차: 되살아난 화톳불이 대기열을 갖는다");
                if (되살아난불일?.Work != null)
                {
                    E2EHarness.AssertEqual(되살아난불일.Work.Queue.Count, 1,
                        $"{회}회차: [⑤ 딸림] 걸어 둔 추출이 그대로 걸려 있다");
                    E2EHarness.AssertEqual(되살아난불일.Work.Queue.Active?.Remaining ?? -1, 불남은것,
                        $"{회}회차: [⑤ 딸림] 남은 개수도 그대로다");
                    E2EHarness.Assert(되살아난불일.Work.Queue.Active?.Recipe == 불레시피,
                        $"{회}회차: [⑤ 딸림] 같은 레시피다 (id에서 레시피로 돌아왔다)");
                    E2EHarness.AssertEqual(되살아난불일.Work.OutputCount, 2,
                        $"{회}회차: [⑤ 딸림] 회수함에 뽑아 놓은 것도 그대로다");
                }

                // ── 계층 ⑥ 씬에 놓인 제작대 ────────────────────
                var 되살아난제작대 = 씬제작대();
                E2EHarness.Assert(되살아난제작대 != null,
                    $"{회}회차: 씬에 놓인 제작대가 여전히 있다");
                if (되살아난제작대 != null)
                {
                    E2EHarness.AssertEqual(되살아난제작대.Work.Queue.Count, 1,
                        $"{회}회차: [⑥ 씬 제작대] 걸어 둔 것이 씬 로드를 건넜다");
                    E2EHarness.AssertEqual(되살아난제작대.Work.Queue.Active?.Remaining ?? -1,
                                           제작대남은것,
                        $"{회}회차: [⑥ 씬 제작대] 남은 개수도 그대로다");
                    E2EHarness.Assert(되살아난제작대.Work.Queue.Active?.Recipe == 제작대레시피,
                        $"{회}회차: [⑥ 씬 제작대] 같은 레시피다");
                    E2EHarness.AssertEqual(되살아난제작대.Work.OutputCount, 제작대회수함,
                        $"{회}회차: [⑥ 씬 제작대] 회수함도 그대로다");
                }

                // ── 시계와 시드 ────────────────────────────────
                E2EHarness.AssertEqual(WorldSeed.Value, 저장시드,
                    $"{회}회차: [시드] 흐트러뜨린 시드가 저장본의 값으로 돌아왔다");
                E2EHarness.Assert(WorldClock.Seconds >= 저장시각,
                    $"{회}회차: [시계] 세계 시각이 저장 시점보다 뒤에 있다 " +
                    $"({저장시각:F1} → {WorldClock.Seconds:F1}초)");
                E2EHarness.Log($"  {회}회차: 씬 로드 직후 시계 {씬로드뒤시각:F1}초 · " +
                               $"불러온 뒤 {WorldClock.Seconds:F1}초");

                적는다($"{회}회차 여섯 계층",
                       $"캔 자리 · 보관함({보관어긋남:F4}m) · 낙하물 · 맡긴 것 " +
                       $"{맡길합금}/{맡길잔해} · 대기열 {불남은것} · 씬 제작대 {제작대남은것}");
            }

            적는다("씬 로드 왕복", "3회 연속. 손으로 없앤 것이 아니라 " +
                                  "SceneManager.LoadSceneAsync로 씬을 통째로 다시 올렸다");
        }

        // ══ ② 절 순서가 반대인 저장본 ═══════════════════════════

        /// <summary>
        /// <b>딸림이 제 절을 갖던 시절의 저장본이, 그 절이 「세계」 절 앞에 있어도
        /// 그냥 열린다.</b>
        ///
        /// 앞 라운드의 검사는 <c>storage_x_y_z</c> 절을 세계 절 <b>뒤에</b> 붙였다.
        /// 그것은 실제 게임이 만들던 순서이긴 하지만, <b>옛 저장본이 우리가 만든
        /// 순서로만 오지는 않는다.</b> 그리고 앞에 있으면 아직 없는 몸을 찾다
        /// 실패해 <b>맡긴 물건이 조용히 사라졌다.</b>
        ///
        /// 이제 순서에 기대지 않는다(<see cref="SaveRestoreRule"/>). 여기서
        /// <b>양쪽 순서를 다 친다</b> — 뒤에 붙인 것과 앞에 끼운 것.
        /// </summary>
        static IEnumerator 절_순서가_반대인_저장본이_그냥_열린다()
        {
            E2EHarness.Log("— 절 순서가 반대인 저장본이 그냥 열린다 —");

            foreach (bool 앞에끼운다 in new[] { false, true })
            {
                string 자리말 = 앞에끼운다 ? "「세계」 절 앞" : "「세계」 절 뒤";

                치운다();
                재료를_채운다();
                yield return null;

                var 합금 = Db.GetById("alien_alloy");
                if (합금 == null) { E2EHarness.Assert(false, "합금 정의를 찾았다"); yield break; }

                GameObject 몸 = null;
                yield return 세운다("storage", go => 몸 = go);
                E2EHarness.Assert(몸 != null, $"{자리말}: 보관함을 세웠다");
                if (몸 == null) yield break;

                var 보관함 = 몸.GetComponent<StorageContainer>();
                const int 맡긴것 = 6;
                Bag.TryRemove("alien_alloy", 맡긴것);
                보관함.Contents.TryAdd(합금, 맡긴것);
                string 열쇠 = 보관함.SaveKey;

                저장소.Save(뒤집은슬롯);
                yield return null;

                string 경로 = E2EHarness.SlotPath(뒤집은슬롯);
                var 저장본 = 파일에서_읽는다(경로);
                E2EHarness.Assert(저장본 != null, $"{자리말}: 저장본을 읽었다");
                if (저장본 == null) yield break;

                // 「세계」 절에서 딸림을 도려내고, 옛 모양의 절을 만들어 끼운다.
                var 세계절 = 저장본.Find(WorldLedgerService.Key);
                E2EHarness.Assert(세계절 != null, $"{자리말}: 저장본에 「세계」 절이 있다");
                if (세계절 == null) yield break;

                string 전 = 세계절.json;
                세계절.json = Regex.Replace(전, "\"holds\":\\[[^\\]]*\\]", "\"holds\":[]");
                E2EHarness.Assert(세계절.json != 전, $"{자리말}: 세계 절에서 딸림을 도려냈다");
                E2EHarness.Assert(!저장본.entries.Any(e => e.key == 열쇠),
                                  $"{자리말}: 지금 저장본에는 보관함의 제 절이 아예 없다");

                var 옛절 = new StorageContainer.SaveState();
                옛절.itemIds.Add("alien_alloy");
                옛절.counts.Add(맡긴것);

                var 끼울것 = new SaveEntry
                {
                    key = 열쇠,
                    type = typeof(StorageContainer.SaveState).AssemblyQualifiedName,
                    json = JsonUtility.ToJson(옛절),
                };

                int 세계자리 = 저장본.entries.IndexOf(세계절);
                if (앞에끼운다) 저장본.entries.Insert(0, 끼울것);
                else 저장본.entries.Add(끼울것);

                int 끼운자리 = 저장본.entries.IndexOf(끼울것);
                E2EHarness.Assert(앞에끼운다 ? 끼운자리 < 세계자리 : 끼운자리 > 세계자리,
                    $"{자리말}: 옛 절을 실제로 그 자리에 두었다 " +
                    $"(옛 절 {끼운자리}번째, 세계 절 {저장본.entries.IndexOf(세계절)}번째)");

                File.WriteAllText(경로, SaveSerializer.Serialize(저장본));
                yield return null;

                몸.SetActive(false);
                Object.Destroy(몸);
                yield return null;
                yield return null;
                E2EHarness.Assert(!살아있는_보관함().Any(), $"{자리말}: 보관함이 세계에서 사라졌다");

                bool 열렸나 = 저장소.Load(뒤집은슬롯);
                yield return null;
                yield return null;

                E2EHarness.Assert(열렸나, $"{자리말}: 옛 모양의 저장본이 그냥 열렸다");

                var 되살아난것 = 살아있는_보관함().FirstOrDefault();
                E2EHarness.Assert(되살아난것 != null, $"{자리말}: 보관함이 다시 섰다");
                if (되살아난것 == null) yield break;

                E2EHarness.AssertEqual(되살아난것.Contents.CountOf("alien_alloy"), 맡긴것,
                    $"{자리말}: 옛 절에 실려 있던 내용물 {맡긴것}개가 돌아왔다");
            }

            치운다();
            적는다("절 순서", "storage_x_y_z 절을 「세계」 절 뒤에 붙인 것과 " +
                              "앞에 끼운 것 둘 다 그냥 열렸고 내용물 6개가 돌아왔다");
            yield return null;
        }

        // ══ ④ 레시피 조회가 빌드에서 도는 길로 답한다 ═══════════

        /// <summary>
        /// <b>에디터에서만 도는 길에 기대고 있지 않은가.</b>
        ///
        /// <c>RecipeIndex</c>는 예전에 <c>Resources.FindObjectsOfTypeAll</c> 하나로
        /// 표를 지었다. 그것은 <b>이미 메모리에 올라와 있는</b> 것만 주는데,
        /// 에디터에서는 프로젝트의 에셋이 대개 올라와 있어서 <b>언제나 성공한다.</b>
        /// 빌드에서 끊기면 걸어 둔 제작 하나가 경고와 함께 사라지고, 재료는 이미
        /// 빠진 뒤다. <b>에디터에서는 영영 안 보이는 사고</b>다.
        ///
        /// 그래서 <b>그 길 하나만</b>으로 표를 지어 본다
        /// (<see cref="RecipeIndex.BuildFromResourcesOnly"/>). 훑기를 섞으면
        /// 에디터에서는 무조건 초록이 나오므로 아무것도 증명하지 못한다.
        /// </summary>
        static IEnumerator 레시피_조회가_빌드에서_도는_길로_답한다()
        {
            E2EHarness.Log("— 레시피 조회가 빌드에서 도는 길로 답한다 —");

            var locator = Resources.Load<RecipeBookLocatorSO>(RecipeBookLocatorSO.ResourceName);
            E2EHarness.Assert(locator != null,
                $"Resources.Load로 레시피 목록의 종이에 닿았다 ({RecipeBookLocatorSO.ResourceName})");
            E2EHarness.Assert(locator != null && locator.Book != null,
                              "그 종이에 레시피 목록이 꽂혀 있다");

            RecipeIndex.Forget();
            int 담긴것 = RecipeIndex.BuildFromResourcesOnly();
            E2EHarness.Assert(담긴것 > 0,
                $"훑기를 빼고 Resources 길 하나만으로 레시피 {담긴것}개를 담았다");
            E2EHarness.AssertEqual(RecipeIndex.LastSource, RecipeIndex.Source.Resources,
                                   "표를 내놓은 길이 Resources다");

            // 저장이 실제로 지나는 문으로 확인한다. 걸어 둔 제작이 되살아나는
            // 자리에서 부르는 것이 바로 이 함수다.
            E2EHarness.Assert(불레시피 != null, "화톳불 레시피를 알고 있다");
            if (불레시피 != null)
            {
                var 찾은것 = RecipeIndex.Find(불레시피.id);
                E2EHarness.Assert(찾은것 == 불레시피,
                    $"Resources 길로 지은 표에서 {불레시피.id}를 찾았다");
            }
            if (제작대레시피 != null)
                E2EHarness.Assert(RecipeIndex.Find(제작대레시피.id) == 제작대레시피,
                    $"Resources 길로 지은 표에서 {제작대레시피.id}도 찾았다");

            // 기본 경로도 같은 길로 답하는가. 표를 통째로 잊고 평소처럼 물어본다.
            RecipeIndex.Forget();
            var 다시 = 불레시피 != null ? RecipeIndex.Find(불레시피.id) : null;
            E2EHarness.Assert(다시 == 불레시피, "평소 경로로도 같은 레시피를 찾았다");
            E2EHarness.AssertEqual(RecipeIndex.LastSource, RecipeIndex.Source.Resources,
                "평소 경로도 <b>빌드에서 도는 길</b>로 답했다 (훑기가 아니라)");

            적는다("레시피 조회", $"Resources 길 하나로 레시피 {담긴것}개를 담았고, " +
                                  $"평소 경로의 답도 그 길에서 나왔다");
            yield return null;
        }

        // ── 잔 도구들 ───────────────────────────────────────────

        static SaveSnapshot 파일에서_읽는다(string 경로)
        {
            if (!File.Exists(경로)) return null;
            return SaveSerializer.TryDeserialize(File.ReadAllText(경로), out var 저장본, out _)
                ? 저장본 : null;
        }

        static IEnumerable<RecipeSO> 레시피들()
        {
            var locator = Resources.Load<RecipeBookLocatorSO>(RecipeBookLocatorSO.ResourceName);
            if (locator?.Book?.recipes != null && locator.Book.recipes.Length > 0)
                return locator.Book.recipes;

            var book = Resources.FindObjectsOfTypeAll<RecipeBookSO>().FirstOrDefault();
            return book?.recipes ?? Resources.FindObjectsOfTypeAll<RecipeSO>();
        }

        /// <summary>씬에 놓인 제작대. MainScene에 하나 서 있다.</summary>
        static CraftingBench 씬제작대() =>
            Object.FindObjectsByType<CraftingBench>(FindObjectsInactive.Include)
                  .FirstOrDefault(b => b != null &&
                                       !(b.TryGetComponent<BuiltStructure>(out var s) && s.Spawned));

        static IEnumerable<StorageContainer> 살아있는_보관함() =>
            BuiltStructure.Active
                .Where(b => b != null && b.Spawned)
                .Select(b => b.GetComponent<StorageContainer>())
                .Where(s => s != null);

        /// <summary>사람에게서 먼, 아직 안 캔 흩어진 잔해 하나.</summary>
        static HarvestNode 안_캔_노드()
        {
            var 눈 = E2EHarness.Eye != null ? E2EHarness.Eye.transform.position : Vector3.zero;
            return Object.FindObjectsByType<HarvestNode>(FindObjectsInactive.Include)
                .Where(n => n != null && n.Definition != null &&
                            n.Definition.name == "LooseScrap" && !n.IsDepleted)
                .OrderByDescending(n => Vector3.Distance(눈, n.transform.position))
                .FirstOrDefault();
        }

        static HarvestNode 신원으로_찾는다(string 신원) =>
            Object.FindObjectsByType<HarvestNode>(FindObjectsInactive.Include)
                  .FirstOrDefault(n => n != null && n.WorldId == 신원);

        /// <summary>
        /// 세울 재료를 카탈로그에게 물어 채운다. 목록을 손으로 적으면 비용이
        /// 바뀔 때마다 조용히 낡는다.
        ///
        /// <b>쌓지 않고 맞춘다.</b> 가방은 열다섯 칸뿐이라 채울 때마다 더 넣으면
        /// 몇 판 만에 꽉 차고, 그 뒤로는 새 재료가 조용히 안 들어간다.
        /// </summary>
        static void 재료를_채운다()
        {
            var 필요한것 = new HashSet<string> { "scrap", "alien_alloy" };
            var placer = Placer;
            if (placer != null)
            {
                foreach (var id in new[] { "storage", "campfire", "bench" })
                {
                    placer.SelectById(id);
                    var cost = placer.Selected?.cost;
                    if (cost == null) continue;
                    foreach (var c in cost) if (c?.item != null) 필요한것.Add(c.item.id);
                }
                placer.Cancel();
            }

            const int 한몫 = 40;
            foreach (var id in 필요한것)
            {
                var item = Db.GetById(id);
                if (item == null) continue;

                int 있는것 = Bag.CountOf(id);
                if (있는것 > 0) Bag.TryRemove(id, 있는것);
                Bag.TryAdd(item, 한몫);
            }
        }

        /// <summary>
        /// 이 레시피를 <paramref name="몫"/>개 걸 만큼 재료를 들려준다.
        ///
        /// <b>채우는 것이 아니라 맞춘다.</b> 가방은 열다섯 칸뿐이라 걸 때마다 더
        /// 부으면 몇 판 만에 꽉 차고, 그 뒤로는 새 재료가 조용히 안 들어간다.
        /// </summary>
        static void 재료를_들려준다(RecipeSO 레시피, int 몫 = 3)
        {
            if (레시피?.ingredients == null) return;

            foreach (var need in 레시피.ingredients)
            {
                if (need?.item == null || need.count <= 0) continue;
                int 모자란것 = need.count * 몫 - Bag.CountOf(need.item.id);
                if (모자란것 > 0) Bag.TryAdd(need.item, 모자란것);
            }
        }

        /// <summary>지금 세계에 서 있는 「태어난 것」을 전부 없앤다.</summary>
        static void 치운다()
        {
            var 거둘것 = BuiltStructure.Active
                .Where(b => b != null && b.Spawned)
                .Select(b => b.gameObject)
                .ToList();

            거둘것.AddRange(ItemPickup.Active
                .Where(p => p != null && p.Spawned)
                .Select(p => p.gameObject));

            foreach (var go in 거둘것)
            {
                if (go == null) continue;
                go.SetActive(false);
                Object.Destroy(go);
            }
        }

        /// <summary>
        /// 놓을 자리를 스스로 찾아 세운다. 좌표를 박아 두면 지형이 바뀔 때
        /// 그 좌표만 조용히 낡는다.
        /// </summary>
        static IEnumerator 세운다(string id, System.Action<GameObject> result)
        {
            var placer = Placer;
            if (placer == null) { result(null); yield break; }

            placer.SelectById(id);
            yield return null;
            E2EHarness.Assert(placer.Selected != null, $"건축물 정의를 찾았다: {id}");

            var 사람 = E2EHarness.Player.transform;

            for (int 고리 = 0; 고리 < 3; 고리++)
            for (int a = 0; a < 12; a++)
            {
                var dir = Quaternion.Euler(0f, a * 30f, 0f) * Vector3.forward;
                var 탐침 = 사람.position + dir * (1.6f + 고리 * 0.9f) + Vector3.up * 2f;

                if (!Physics.Raycast(탐침, Vector3.down, out var hit, 8f, ~0,
                                     QueryTriggerInteraction.Ignore))
                    continue;

                E2EHarness.LookAt(hit.point);
                yield return null;
                yield return null;

                if (placer.Evaluate(out _, out _) != PlacementResult.Ok) continue;

                yield return E2EHarness.ClickAttack();
                yield return null;
                yield return null;

                var 세운것 = BuiltStructure.Active
                    .LastOrDefault(b => b != null && b.Spawned &&
                                        b.Definition != null && b.Definition.id == id);
                placer.Cancel();
                yield return null;
                result(세운것 != null ? 세운것.gameObject : null);
                yield break;
            }

            E2EHarness.Log($"  [배치 문제] {id}를 놓을 자리를 못 찾았다 " +
                           $"(마지막 판정 {placer.LastResult})");
            placer.Cancel();
            result(null);
        }
    }
}
