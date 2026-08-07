using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;
using Survive.Building;
using Survive.Core;
using Survive.Crafting;
using Survive.Interaction;
using Survive.Items;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// <b>몸에 딸린 것이 저장을 건넌다.</b>
    ///
    /// 앞 라운드가 몸을 실었다 — 세운 것과 떨군 것이 불러오기를 건넌다. 그런데
    /// 그 라운드는 「논리로만 맞췄고 E2E로 안 쳤다」를 세 자리 남겼고, 셋 다
    /// <b>시간이 아니라 물건</b>을 먹는 갈래다.
    ///
    /// <list type="number">
    /// <item><b>건축된 보관함의 내용물.</b> 몸은 돌아오는데 안이 비면 저장이
    ///   맡긴 물건을 먹는다 — 규칙 §5.7의 「대가는 시간뿐」보다 나쁘다.</item>
    /// <item><b>걸어 둔 제작과 뽑아 놓은 결과물.</b> 재료는 걸 때 이미 빠졌다.
    ///   §5.4가 「걸어두고 자리를 뜰 수 있다」고 하는데 저장으로 자리를 뜨면
    ///   잃는다면 그 설계가 반만 참이다.</item>
    /// <item><b>쌓아 올린 벽.</b> 자리만 맞고 스냅이 안 이어지면 다음에 붙일 때
    ///   어긋난다. 그래서 불러온 뒤 <b>거기에 하나 더 붙여</b> 본다 —
    ///   그것이 스냅이 살아 있다는 증거다.</item>
    /// </list>
    ///
    /// <b>실제 파일을 쓰고 읽는다.</b> <c>SaveService.Save</c>가
    /// <c>File.WriteAllText</c>를, <c>Load</c>가 <c>File.ReadAllText</c>를 지난다.
    /// 그 파일이 정말 물건을 담고 있는지도 글자로 확인한다 — 메모리 안에서만
    /// 오갔으면 「저장이 물건을 먹는다」를 못 잡는다.
    ///
    /// <b>기본 슬롯을 밟지 않는다.</b> 세 클론과 사람의 에디터가 같은 저장 폴더를
    /// 나눠 쓴다. 이 검사는 자기 슬롯만 쓰고 끝나면 지운다.
    /// </summary>
    public static class E2ESaveCompleteness
    {
        const string 슬롯 = "e2e_save_full";
        const string 옛슬롯 = "e2e_save_legacy";

        static readonly List<string> _표 = new List<string>();

        public static IEnumerator FullRun()
        {
            _표.Clear();

            yield return 준비();
            yield return 보관함의_내용물이_저장을_건넌다();
            yield return 걸어_둔_제작이_저장을_건넌다();
            yield return 손_제작도_저장을_건넌다();
            yield return 쌓아_올린_벽이_저장을_건너고_스냅이_이어진다();
            yield return 옛_저장본이_그냥_열린다();
            yield return 뒷정리();

            E2EHarness.Log("");
            E2EHarness.Log("═══ 실측표 ═══");
            foreach (var line in _표) E2EHarness.Log("  " + line);
            E2EHarness.Log("=== 저장 완전성 완주 ===");
        }

        static void 적는다(string 항목, string 값) => _표.Add($"{항목} | {값}");

        // ── 준비 ────────────────────────────────────────────────

        static SaveCoordinator 저장소;
        static RecipeSO 추출법;

        static BuildPlacer Placer =>
            Object.FindAnyObjectByType<BuildPlacer>(FindObjectsInactive.Exclude);

        static Inventory Bag => E2EHarness.Player.Inventory.Inventory;
        static ItemDatabaseSO Db => E2EHarness.Player.Inventory.Database;

        static IEnumerator 준비()
        {
            int 잠든것 = E2EHarness.SleepWildCreatures();
            E2EHarness.Log($"  야생 생물 {잠든것}마리를 재웠다");

            저장소 = Object.FindAnyObjectByType<SaveCoordinator>(FindObjectsInactive.Include);
            E2EHarness.Assert(저장소 != null, "SaveCoordinator가 씬에 있다");
            E2EHarness.Assert(WorldLedgerService.Instance != null, "세계 원장이 스스로 붙었다");

            int 모은것 = 저장소.Collect();
            E2EHarness.Log($"  저장 대상 {모은것}개를 모았다");

            yield return E2EHarness.WaitUntil(() => Placer != null, "BuildPlacer가 준비된다", 8f);
            E2EHarness.Assert(Db != null, "아이템 데이터베이스가 연결돼 있다");

            재료를_채운다();

            // 화톳불에서 도는 레시피를 세계에서 읽는다. 어느 것이 화톳불의 일인지를
            // 검사가 정하면, 그 정의가 바뀌는 날 검사만 초록으로 남는다.
            var book = Resources.FindObjectsOfTypeAll<RecipeBookSO>().FirstOrDefault();
            var pool = book?.recipes ?? Resources.FindObjectsOfTypeAll<RecipeSO>();
            추출법 = pool.FirstOrDefault(r => r != null &&
                                              r.requiredStation == StationType.Campfire);
            E2EHarness.Assert(추출법 != null, "화톳불에서 도는 레시피가 있다");
            if (추출법 != null)
                E2EHarness.Log($"  화톳불 레시피: {추출법.id} ({추출법.craftSeconds:F0}초/개)");

            yield return null;
        }

        /// <summary>
        /// 세울 재료를 카탈로그에게 물어 채운다. 목록을 손으로 적으면 비용이
        /// 바뀔 때마다 조용히 낡는다 (<c>E2ESpawnLedger</c>와 같은 수법).
        ///
        /// <b>시나리오마다 다시 채운다.</b> 앞 시나리오가 걸어 둔 추출이 재료를
        /// 통째로 먹으므로, 한 번만 채우면 뒤엣것이 「자리를 못 찾았다」로 실패하고
        /// 사람은 그것을 배치 문제로 읽는다 — 실제로 한 번 당했다.
        /// </summary>
        static void 재료를_채운다()
        {
            var 필요한것 = new HashSet<string> { "scrap", "alien_alloy" };
            var placer = Placer;
            if (placer != null)
            {
                foreach (var id in new[] { "storage", "campfire", "bench",
                                           "piece_foundation", "piece_wall", "piece_doorway" })
                {
                    placer.SelectById(id);
                    var cost = placer.Selected?.cost;
                    if (cost == null) continue;
                    foreach (var c in cost) if (c?.item != null) 필요한것.Add(c.item.id);
                }
                placer.Cancel();
            }

            // <b>쌓지 않고 맞춘다.</b> 가방은 열다섯 칸뿐이라 채울 때마다 더 넣으면
            // 몇 판 만에 꽉 차고, 그 뒤로는 <b>새 재료가 조용히 안 들어간다</b> —
            // 그러면 다음 시나리오가 「놓을 자리를 못 찾았다」로 실패하고 사람은
            // 그것을 배치 문제로 읽는다. 실제로 한 번 그렇게 헤맸다
            // (판정은 NotEnoughResources였다).
            const int 한몫 = 40;
            foreach (var id in 필요한것)
            {
                var item = Db.GetById(id);
                if (item == null) continue;

                int 있는것 = Bag.CountOf(id);
                if (있는것 > 0) Bag.TryRemove(id, 있는것);
                Bag.TryAdd(item, 한몫);
            }

            E2EHarness.Log("  재료 주입: " +
                string.Join(", ", 필요한것.Select(id => $"{id} {Bag.CountOf(id)}")));
        }

        static IEnumerator 뒷정리()
        {
            치운다();
            저장소?.Delete(슬롯);
            저장소?.Delete(옛슬롯);
            E2EHarness.Log("  검사용 슬롯을 지웠다 (기본 슬롯은 건드리지 않았다)");
            yield return null;
        }

        // ── ① 보관함의 내용물 ───────────────────────────────────

        /// <summary>
        /// <b>짓고 → 넣고 → 저장 → 몸을 없애고 → 불러오기 → 그대로인가.</b>
        ///
        /// <b>세 번 연속으로 본다.</b> 첫 판은 아무것도 없는 세계에서 통과하고,
        /// 두 번째 판부터 앞 판이 남긴 것과 얽힌다 — 특히 이 갈래는 되살리기가
        /// 「먼저 거두고 다시 세운다」라, 거두는 순간에 등록이 안 풀리면 살아날
        /// 몸과 죽을 몸이 <b>같은 저장 열쇠</b>를 갖는다.
        ///
        /// <b>불러오기 전에 손으로 부순다.</b> 안 부수면 「원래 서 있던 것이 계속
        /// 서 있었다」와 구별되지 않는다.
        /// </summary>
        static IEnumerator 보관함의_내용물이_저장을_건넌다()
        {
            E2EHarness.Log("— 건축된 보관함의 내용물이 저장을 건넌다 (3회 연속) —");

            var 합금 = Db.GetById("alien_alloy");
            var 잔해 = Db.GetById("scrap");
            E2EHarness.Assert(합금 != null && 잔해 != null, "맡길 물건 정의를 찾았다");
            if (합금 == null || 잔해 == null) yield break;

            for (int 회 = 1; 회 <= 3; 회++)
            {
                치운다();
                재료를_채운다();
                yield return null;

                GameObject 세운것 = null;
                yield return 세운다("storage", go => 세운것 = go);
                E2EHarness.Assert(세운것 != null, $"{회}회차: 보관함을 세웠다");
                if (세운것 == null) yield break;

                var 보관함 = 세운것.GetComponent<StorageContainer>();
                E2EHarness.Assert(보관함 != null, $"{회}회차: 세운 것에 보관 기능이 있다");
                if (보관함 == null) yield break;

                var 자리 = 세운것.transform.position;

                // 실제로 다가서서 연다. 창이 열리는 몸이라야 사람이 맡길 수 있는 몸이다.
                yield return 열어_본다(세운것, 회);

                // 넣는 것은 화면의 문(PutIn)으로 한다 — 사람이 지나는 길이다.
                int 가진합금 = Bag.CountOf("alien_alloy");
                var ui = Object.FindAnyObjectByType<Survive.UI.StorageUI>(FindObjectsInactive.Include);
                if (ui != null && ui.IsOpen)
                {
                    E2EHarness.Assert(ui.PutIn(합금, 4), $"{회}회차: 합금 4개를 맡겼다");
                    E2EHarness.Assert(ui.PutIn(잔해, 7), $"{회}회차: 잔해 7개를 맡겼다");
                    ui.Close();
                }
                else
                {
                    Bag.TryRemove("alien_alloy", 4);
                    보관함.Contents.TryAdd(합금, 4);
                    Bag.TryRemove("scrap", 7);
                    보관함.Contents.TryAdd(잔해, 7);
                }
                yield return null;

                E2EHarness.AssertEqual(보관함.Contents.CountOf("alien_alloy"), 4,
                                       $"{회}회차: 보관함에 합금 4");
                E2EHarness.AssertEqual(보관함.Contents.CountOf("scrap"), 7,
                                       $"{회}회차: 보관함에 잔해 7");
                E2EHarness.AssertEqual(Bag.CountOf("alien_alloy"), 가진합금 - 4,
                                       $"{회}회차: 내 가방에서 빠졌다");

                저장소.Save(슬롯);
                yield return null;

                // <b>파일이 정말 그것을 담고 있는가.</b> 메모리 안에서만 오갔으면
                // 이 검사는 아무것도 못 잡는다.
                string 경로 = E2EHarness.SlotPath(슬롯);
                E2EHarness.Assert(File.Exists(경로), $"{회}회차: 저장본 파일이 생겼다");

                string 세계 = 파일에서_세계절을_읽는다(경로);
                E2EHarness.Assert(세계 != null, $"{회}회차: 파일에 「세계」 절이 있다");
                E2EHarness.Assert(세계 != null && 세계.Contains("holds"),
                                  $"{회}회차: 생성 목록의 줄이 딸림 칸을 싣는다");
                E2EHarness.Assert(세계 != null && 세계.Contains("alien_alloy"),
                    $"{회}회차: <b>파일의 「세계」 절 안에</b> 맡긴 물건이 적혀 있다 — " +
                    "메모리가 아니라 디스크를 건넜다");

                // 몸을 없앤다. 이제 세계에 보관함이 없다.
                세운것.SetActive(false);
                Object.Destroy(세운것);
                yield return null;
                yield return null;
                E2EHarness.Assert(!살아있는_보관함().Any(), $"{회}회차: 보관함이 세계에서 사라졌다");

                E2EHarness.Assert(저장소.Load(슬롯), $"{회}회차: 저장본이 열렸다");
                yield return null;
                yield return null;

                var 되살아난것 = 살아있는_보관함()
                    .OrderBy(s => Vector3.Distance(s.transform.position, 자리))
                    .FirstOrDefault();

                E2EHarness.Assert(되살아난것 != null, $"{회}회차: 보관함이 다시 섰다");
                if (되살아난것 == null) yield break;

                float 어긋남 = Vector3.Distance(되살아난것.transform.position, 자리);
                E2EHarness.Assert(어긋남 < 0.01f, $"{회}회차: 세웠던 그 자리다 ({어긋남:F4}m)");

                E2EHarness.AssertEqual(되살아난것.Contents.CountOf("alien_alloy"), 4,
                                       $"{회}회차: <b>맡긴 합금이 그대로 있다</b>");
                E2EHarness.AssertEqual(되살아난것.Contents.CountOf("scrap"), 7,
                                       $"{회}회차: 맡긴 잔해도 그대로다");
                E2EHarness.AssertEqual(살아있는_보관함().Count(), 1,
                                       $"{회}회차: 두 벌로 늘지 않았다");

                yield return 비켜선다();
            }

            적는다("보관함 내용물", "짓고 맡기고 저장 → 몸을 없앰 → 불러오기: " +
                                   "3회 연속 합금 4 · 잔해 7 그대로 (실제 파일 write/read)");
        }

        // ── ② 걸어 둔 제작 ──────────────────────────────────────

        /// <summary>
        /// <b>걸어 둔 추출과 뽑아 놓은 결과물이 저장을 건넌다.</b>
        ///
        /// 재료는 걸 때 전부 빠진다. 불러오기가 대기열을 지우면 잃는 것은 시간이
        /// 아니라 <b>물건</b>이다. 그래서 이쪽은 「취소하면 재료가 돌아온다」와
        /// <b>다른 답</b>을 낸다 — 돌려주는 것이 아니라 이어 간다. 사람이 스무 개를
        /// 걸어 놓고 나갔으면 돌아왔을 때 봐야 하는 것은 재료 더미가 아니다.
        /// </summary>
        static IEnumerator 걸어_둔_제작이_저장을_건넌다()
        {
            E2EHarness.Log("— 화톳불에 걸어 둔 제작이 저장을 건넌다 (3회 연속) —");
            if (추출법 == null) yield break;

            for (int 회 = 1; 회 <= 3; 회++)
            {
                치운다();
                재료를_채운다();
                yield return null;

                GameObject 세운것 = null;
                yield return 세운다("campfire", go => 세운것 = go);
                E2EHarness.Assert(세운것 != null, $"{회}회차: 화톳불을 세웠다");
                if (세운것 == null) yield break;

                var 불 = 세운것.GetComponent<Campfire>();
                var 스테이션 = 세운것.GetComponent<ICraftStation>();
                E2EHarness.Assert(스테이션?.Work != null, $"{회}회차: 화톳불이 대기열을 갖는다");
                if (스테이션?.Work == null) yield break;

                var 자리 = 세운것.transform.position;

                // 대기열의 문은 하나다 — 화면이 누르는 그 함수를 그대로 지난다.
                int 걸것 = 4;
                int 재료전 = 세는다(추출법);
                bool 걸렸나 = CraftQueueService.TryEnqueue(
                    스테이션.Work.Queue, 추출법, 걸것, Bag, StationType.Campfire,
                    Survive.Progression.BlueprintGate.Active);

                E2EHarness.Assert(걸렸나, $"{회}회차: 추출 {걸것}개를 걸었다");
                if (!걸렸나) yield break;

                int 재료후 = 세는다(추출법);
                E2EHarness.Assert(재료후 < 재료전,
                    $"{회}회차: 재료가 걸 때 빠졌다 ({재료전} → {재료후}) — " +
                    "이것이 잃으면 안 되는 이유다");

                // 회수함에도 하나 넣어 둔다. 뽑아 놓고 안 가져간 것은 이미 <b>완성된</b>
                // 물건이라, 잃으면 시간이 아니라 결과물을 잃는다.
                var 결과물 = 추출법.result?.item;
                if (결과물 != null) 스테이션.Work.Output.TryAdd(결과물, 2);

                // 조금 굴려 진행도를 만든다. 0에서 재면 「진행도가 저장을 건너는가」를
                // 못 본다.
                for (int i = 0; i < 30; i++) yield return null;

                float 진행도전 = 스테이션.Work.Queue.Active?.Elapsed ?? -1f;
                int 남은것전 = 스테이션.Work.Queue.Active?.Remaining ?? -1;
                E2EHarness.Log($"  {회}회차: 걸린 것 {스테이션.Work.Queue.Count}개 · " +
                               $"남은 개수 {남은것전} · 진행도 {진행도전:F2}초 · " +
                               $"회수함 {스테이션.Work.OutputCount}개 · " +
                               $"불 {(불 != null && 불.IsBurning ? "탄다" : "꺼짐")}");

                저장소.Save(슬롯);
                yield return null;

                string 세계 = 파일에서_세계절을_읽는다(E2EHarness.SlotPath(슬롯));
                E2EHarness.Assert(세계 != null && 세계.Contains("queued") &&
                                  세계.Contains(추출법.id),
                                  $"{회}회차: 파일의 「세계」 절에 걸어 둔 제작이 적혀 있다");

                세운것.SetActive(false);
                Object.Destroy(세운것);
                yield return null;
                yield return null;

                E2EHarness.Assert(저장소.Load(슬롯), $"{회}회차: 저장본이 열렸다");
                yield return null;
                yield return null;

                var 되살아난것 = BuiltStructure.Active
                    .Where(b => b != null && b.Spawned && b.Definition != null &&
                                b.Definition.id == "campfire")
                    .OrderBy(b => Vector3.Distance(b.transform.position, 자리))
                    .FirstOrDefault();

                E2EHarness.Assert(되살아난것 != null, $"{회}회차: 화톳불이 다시 섰다");
                if (되살아난것 == null) yield break;

                var 되살아난일 = 되살아난것.GetComponent<ICraftStation>();
                E2EHarness.Assert(되살아난일?.Work != null, $"{회}회차: 되살아난 불이 대기열을 갖는다");
                if (되살아난일?.Work == null) yield break;

                E2EHarness.AssertEqual(되살아난일.Work.Queue.Count, 1,
                                       $"{회}회차: <b>걸어 둔 추출이 그대로 걸려 있다</b>");
                E2EHarness.AssertEqual(되살아난일.Work.Queue.Active?.Remaining ?? -1, 남은것전,
                                       $"{회}회차: 남은 개수도 그대로다");
                E2EHarness.Assert(되살아난일.Work.Queue.Active?.Recipe == 추출법,
                                  $"{회}회차: 같은 레시피다");
                E2EHarness.AssertEqual(되살아난일.Work.OutputCount, 2,
                                       $"{회}회차: 뽑아 놓은 결과물도 그대로다");

                int 재료복구 = 세는다(추출법);
                E2EHarness.AssertEqual(재료복구, 재료후,
                    $"{회}회차: <b>재료를 돌려주지 않았다</b> — 이어 가는 것과 " +
                    "되돌려 주는 것은 다르다");

                yield return 비켜선다();
            }

            적는다("걸어 둔 제작", "화톳불에 4개를 걸고 저장 → 몸을 없앰 → 불러오기: " +
                                   "3회 연속 대기열·진행도·회수함이 이어졌고 재료는 안 돌아왔다");
        }

        /// <summary>
        /// <b>손 제작도 같은가.</b> 이쪽은 몸이 없어서 생성 목록의 줄이 될 수 없고,
        /// 주인이 사람이라 제 절을 갖는다. 규칙은 그대로다 — 몸을 따라간다.
        /// </summary>
        static IEnumerator 손_제작도_저장을_건넌다()
        {
            E2EHarness.Log("— 손에 걸어 둔 제작도 저장을 건넌다 —");

            var 손 = HandCraftingService.Instance;
            E2EHarness.Assert(손 != null, "손 제작 서비스가 스스로 붙었다");
            if (손 == null) yield break;

            저장소.Collect();

            // 손에서 도는 것 중 <b>가장 긴</b> 것을 고른다. 짧은 것을 고르면
            // 저장하기 전에 끝나 버려서 「걸어 둔 것이 건넌다」를 잴 수 없다.
            var book = Resources.FindObjectsOfTypeAll<RecipeBookSO>().FirstOrDefault();
            var pool = book?.recipes ?? Resources.FindObjectsOfTypeAll<RecipeSO>();
            var 손레시피 = pool.Where(r => r != null && r.requiredStation == StationType.None &&
                                            r.craftSeconds > 1f && r.result?.item != null)
                                .OrderByDescending(r => r.craftSeconds)
                                .FirstOrDefault(r => 재료를_들려준다(r));

            if (손레시피 == null)
            {
                E2EHarness.Log("  [확인 필요] 지금 손으로 걸 수 있는 긴 레시피가 없다 — 건너뛴다");
                yield break;
            }
            E2EHarness.Log($"  손 레시피: {손레시피.id} ({손레시피.craftSeconds:F0}초/개)");

            CraftQueueService.CancelAll(손.Queue, Bag);
            E2EHarness.Assert(손.TryEnqueue(손레시피, 2), $"손으로 {손레시피.id} 2개를 걸었다");
            int 남은것 = 손.Queue.Active?.Remaining ?? -1;

            저장소.Save(슬롯);
            yield return null;

            CraftQueueService.CancelAll(손.Queue, Bag);
            E2EHarness.Assert(손.Queue.IsEmpty, "손 대기열을 비웠다");

            E2EHarness.Assert(저장소.Load(슬롯), "저장본이 열렸다");
            yield return null;

            E2EHarness.AssertEqual(손.Queue.Count, 1, "손에 걸어 둔 것이 돌아왔다");
            E2EHarness.AssertEqual(손.Queue.Active?.Remaining ?? -1, 남은것, "남은 개수도 그대로다");

            CraftQueueService.CancelAll(손.Queue, Bag);
            적는다("손 제작", $"{손레시피.id} 2개를 걸고 저장 → 비움 → 불러오기: 그대로 돌아왔다");
            yield return null;
        }

        // ── ③ 쌓아 올린 벽 ──────────────────────────────────────

        /// <summary>
        /// <b>서로 물린 것으로 확인한다.</b> 화톳불 하나는 자리만 맞으면 되지만
        /// 모듈 조각은 <b>다음에 붙일 수 있어야</b> 온전한 것이다. 스냅 관계는
        /// 저장하지 않고 자리·자세만 재생하는데, 스냅 자리가 스스로 다시 등록하므로
        /// 같아야 한다 — 그 「같아야 한다」를 여기서 실제로 친다.
        ///
        /// 증거는 좌표가 아니라 <b>하나 더 붙는 것</b>이다. 벽은 붙일 자리가 없으면
        /// 못 세우므로(<c>E2EModularBuild</c>가 못 박아 둔 규칙), 불러온 뒤에
        /// 벽이 서면 그것은 스냅 그래프가 살아 있다는 뜻이다.
        /// </summary>
        static IEnumerator 쌓아_올린_벽이_저장을_건너고_스냅이_이어진다()
        {
            E2EHarness.Log("— 쌓아 올린 벽이 저장을 건너고 스냅이 이어진다 —");

            치운다();
            재료를_채운다();
            yield return null;

            const float 칸 = 4f;

            GameObject 토대 = null;
            yield return 토대를_놓는다(go => 토대 = go);
            E2EHarness.Assert(토대 != null, "토대를 놓았다");
            if (토대 == null) yield break;

            var 기준 = 토대.transform.position;
            int 스냅_토대뒤 = SnapGraph.Count;
            E2EHarness.Assert(스냅_토대뒤 >= 12, $"토대가 붙일 자리를 내놓는다 ({스냅_토대뒤}곳)");

            GameObject 벽 = null;
            yield return 곁에_놓는다("piece_wall", 기준 + new Vector3(0f, 0.4f, -칸 * 0.5f),
                                    go => 벽 = go);
            E2EHarness.Assert(벽 != null, "토대 모서리에 벽을 세웠다");

            GameObject 문간 = null;
            yield return 곁에_놓는다("piece_doorway", 기준 + new Vector3(-칸 * 0.5f, 0.4f, 0f),
                                    go => 문간 = go);
            E2EHarness.Assert(문간 != null, "다른 모서리에 문간을 세웠다");
            if (벽 == null || 문간 == null) yield break;

            var 세운자리 = new List<(string id, Vector3 pos, Quaternion rot)>();
            foreach (var b in BuiltStructure.Active.Where(x => x != null && x.Spawned))
                세운자리.Add((b.Definition != null ? b.Definition.id : "?",
                              b.transform.position, b.transform.rotation));

            int 스냅_쌓은뒤 = SnapGraph.Count;
            E2EHarness.Log($"  토대·벽·문간 {세운자리.Count}조각, 스냅 자리 {스냅_쌓은뒤}곳");

            저장소.Save(슬롯);
            yield return null;

            // 통째로 없앤다.
            치운다();
            yield return null;
            yield return null;

            E2EHarness.AssertEqual(BuiltStructure.Active.Count(b => b != null && b.Spawned), 0,
                                   "쌓아 올린 것이 세계에서 사라졌다");
            E2EHarness.Log($"  없앤 뒤 스냅 자리 {SnapGraph.Count}곳");

            E2EHarness.Assert(저장소.Load(슬롯), "저장본이 열렸다");
            yield return null;
            yield return null;

            var 되살아난것 = BuiltStructure.Active.Where(b => b != null && b.Spawned).ToList();
            E2EHarness.AssertEqual(되살아난것.Count, 세운자리.Count, "조각 수가 같다");

            // 자리와 자세가 조각마다 그대로인가. 하나라도 어긋나면 다음에 붙일 때 벌어진다.
            float 최대어긋남 = 0f, 최대각 = 0f;
            foreach (var (id, pos, rot) in 세운자리)
            {
                var 짝 = 되살아난것
                    .Where(b => b.Definition != null && b.Definition.id == id)
                    .OrderBy(b => Vector3.Distance(b.transform.position, pos))
                    .FirstOrDefault();

                E2EHarness.Assert(짝 != null, $"{id}가 다시 섰다");
                if (짝 == null) continue;

                최대어긋남 = Mathf.Max(최대어긋남, Vector3.Distance(짝.transform.position, pos));
                최대각 = Mathf.Max(최대각, Quaternion.Angle(짝.transform.rotation, rot));
            }

            E2EHarness.Assert(최대어긋남 < 0.01f, $"자리가 그대로다 (최대 {최대어긋남:F4}m)");
            E2EHarness.Assert(최대각 < 0.5f, $"자세도 그대로다 (최대 {최대각:F3}도)");

            int 스냅_되살린뒤 = SnapGraph.Count;
            E2EHarness.Log($"  되살린 뒤 스냅 자리 {스냅_되살린뒤}곳 (쌓았을 때 {스냅_쌓은뒤}곳)");
            E2EHarness.AssertEqual(스냅_되살린뒤, 스냅_쌓은뒤,
                                   "스냅 자리가 스스로 다시 등록했다 — 저장하지 않는 것의 값");

            // <b>여기가 증거다.</b> 하나 더 붙여 본다.
            GameObject 더붙인것 = null;
            yield return 곁에_놓는다("piece_wall", 기준 + new Vector3(칸 * 0.5f, 0.4f, 0f),
                                    go => 더붙인것 = go);

            E2EHarness.Assert(더붙인것 != null,
                "<b>불러온 벽에 하나 더 붙었다</b> — 붙일 자리가 없으면 벽은 못 선다");
            if (더붙인것 != null)
            {
                float y = 더붙인것.transform.position.y;
                E2EHarness.Assert(y > 기준.y + 0.3f, "더 붙인 벽도 토대 윗면에서 시작한다");
                float yaw = Mathf.Repeat(더붙인것.transform.eulerAngles.y, 90f);
                E2EHarness.Assert(yaw < 0.05f || yaw > 89.95f,
                                  $"격자에 맞춰 돌아갔다 (yaw 나머지 {yaw:F3}도)");
            }

            적는다("쌓아 올린 벽",
                   $"토대·벽·문간 {세운자리.Count}조각이 어긋남 {최대어긋남:F4}m · " +
                   $"{최대각:F3}도로 돌아왔고, 스냅 자리 {스냅_되살린뒤}곳에 " +
                   $"벽을 하나 더 붙였다");

            치운다();
            yield return null;
        }

        // ── ④ 옛 저장본 ─────────────────────────────────────────

        /// <summary>
        /// <b>딸림이 제 절에 실려 있던 저장본이 그냥 열린다.</b>
        ///
        /// 세운 보관함의 내용물은 예전에 <c>storage_x_y_z</c>라는 제 절에 실렸다.
        /// 지금은 생성 목록의 줄이 싣고 그 절은 아예 안 나간다. <b>이미 있는
        /// 저장본은 옛 모양</b>이므로, 그 모양을 손으로 지어 읽혀 본다.
        ///
        /// <b>읽는 문을 안 닫아 둔 것</b>이 여기서 값을 한다 —
        /// <c>StorageContainer.RestoreState</c>는 그대로 살아 있고, 되살아난 몸이
        /// 그 자리에서 등록하므로 뒤에 오는 절이 그 몸을 찾는다.
        /// </summary>
        static IEnumerator 옛_저장본이_그냥_열린다()
        {
            E2EHarness.Log("— 딸림이 제 절에 실려 있던 옛 저장본이 그냥 열린다 —");

            치운다();
            재료를_채운다();
            yield return null;

            var 합금 = Db.GetById("alien_alloy");
            if (합금 == null) yield break;

            GameObject 세운것 = null;
            yield return 세운다("storage", go => 세운것 = go);
            E2EHarness.Assert(세운것 != null, "보관함을 세웠다");
            if (세운것 == null) yield break;

            var 보관함 = 세운것.GetComponent<StorageContainer>();
            Bag.TryRemove("alien_alloy", 5);
            보관함.Contents.TryAdd(합금, 5);
            string 열쇠 = 보관함.SaveKey;

            저장소.Save(옛슬롯);
            yield return null;

            string 경로 = E2EHarness.SlotPath(옛슬롯);
            E2EHarness.Assert(SaveSerializer.TryDeserialize(File.ReadAllText(경로),
                                                           out var 저장본, out var 사유),
                              $"저장본을 읽었다 ({사유})");
            if (저장본 == null) yield break;

            // 「세계」 절에서 딸림을 도려내고, 옛 모양의 절을 뒤에 붙인다.
            var 세계절 = 저장본.entries.FirstOrDefault(e => e.key == WorldLedgerService.Key);
            E2EHarness.Assert(세계절 != null, "저장본에 「세계」 절이 있다");
            if (세계절 == null) yield break;

            string 전 = 세계절.json;
            세계절.json = Regex.Replace(전, "\"holds\":\\[[^\\]]*\\]", "\"holds\":[]");
            E2EHarness.Assert(세계절.json != 전, "세계 절에서 딸림을 도려냈다");
            E2EHarness.Assert(!저장본.entries.Any(e => e.key == 열쇠),
                              "지금 저장본에는 보관함의 제 절이 아예 없다 — 창구가 하나다");

            var 옛절 = new StorageContainer.SaveState();
            옛절.itemIds.Add("alien_alloy");
            옛절.counts.Add(5);
            저장본.Add(열쇠, typeof(StorageContainer.SaveState).AssemblyQualifiedName,
                      JsonUtility.ToJson(옛절));

            File.WriteAllText(경로, SaveSerializer.Serialize(저장본));
            yield return null;

            세운것.SetActive(false);
            Object.Destroy(세운것);
            yield return null;
            yield return null;

            bool 열렸나 = 저장소.Load(옛슬롯);
            yield return null;
            yield return null;

            E2EHarness.Assert(열렸나, "옛 모양의 저장본이 그냥 열렸다");

            var 되살아난것 = 살아있는_보관함().FirstOrDefault();
            E2EHarness.Assert(되살아난것 != null, "보관함이 다시 섰다");
            if (되살아난것 == null) yield break;

            E2EHarness.AssertEqual(되살아난것.Contents.CountOf("alien_alloy"), 5,
                "옛 절에 실려 있던 내용물이 돌아왔다 — 읽는 문을 안 닫아 둔 값이다");

            적는다("옛 저장본", "딸림을 도려내고 storage_x_y_z 절을 되살린 파일이 " +
                                "그냥 열렸고 내용물 5개가 돌아왔다");

            치운다();
            yield return null;
        }

        // ── 잔 도구들 ───────────────────────────────────────────

        /// <summary>
        /// 디스크에서 다시 읽어 「세계」 절의 본문을 돌려준다.
        ///
        /// <b>글자를 통째로 훑지 않는다.</b> 저장본에는 사람의 가방도 실려 있어서
        /// 「파일에 alien_alloy가 있다」는 아무것도 증명하지 못한다 —
        /// 보관함이 텅 비어도 그 글자는 있다.
        /// </summary>
        static string 파일에서_세계절을_읽는다(string 경로)
        {
            if (!File.Exists(경로)) return null;
            if (!SaveSerializer.TryDeserialize(File.ReadAllText(경로), out var 저장본, out _))
                return null;

            return 저장본?.entries.FirstOrDefault(e => e.key == WorldLedgerService.Key)?.json;
        }

        static IEnumerable<StorageContainer> 살아있는_보관함() =>
            BuiltStructure.Active
                .Where(b => b != null && b.Spawned)
                .Select(b => b.GetComponent<StorageContainer>())
                .Where(s => s != null);

        /// <summary>
        /// 이 레시피를 걸 만큼 재료를 들려준다. 못 들려주면(청사진이 잠겼거나
        /// 재료 정의가 없거나) false — 부르는 쪽이 다른 레시피를 고른다.
        /// </summary>
        static bool 재료를_들려준다(RecipeSO 레시피)
        {
            if (레시피.ingredients != null)
            {
                foreach (var need in 레시피.ingredients)
                {
                    if (need?.item == null || need.count <= 0) continue;
                    int 모자란것 = need.count * 4 - Bag.CountOf(need.item.id);
                    if (모자란것 > 0) Bag.TryAdd(need.item, 모자란것);
                }
            }

            return CraftQueueService.MaxCraftable(레시피, Bag, StationType.None,
                                                  Survive.Progression.BlueprintGate.Active) >= 2;
        }

        static int 세는다(RecipeSO 레시피)
        {
            var 재료 = 레시피?.ingredients?.FirstOrDefault(i => i?.item != null);
            return 재료 != null ? Bag.CountOf(재료.item.id) : 0;
        }

        /// <summary>사람이 하듯 다가서서 E로 연다.</summary>
        static IEnumerator 열어_본다(GameObject 몸, int 회)
        {
            E2EHarness.LookAt(몸.transform.position + Vector3.up * 0.5f);
            yield return null;
            yield return null;

            var it = E2EHarness.Player.Interactor;
            bool 조준됐나 = false;
            for (int i = 0; i < 60 && !조준됐나; i++)
            {
                조준됐나 = it.Current is StorageContainer;
                if (!조준됐나) yield return null;
            }

            if (!조준됐나)
            {
                E2EHarness.Log($"  {회}회차: [확인 필요] 보관함이 조준되지 않았다 — " +
                               "직접 넣는다");
                yield break;
            }

            yield return E2EHarness.TapKey(Key.E);
            yield return null;
        }

        /// <summary>지금 세계에 서 있는 「태어난 것」을 전부 없앤다.</summary>
        static void 치운다()
        {
            var 거둘것 = BuiltStructure.Active
                .Where(b => b != null && b.Spawned)
                .Select(b => b.gameObject)
                .ToList();

            foreach (var go in 거둘것)
            {
                if (go == null) continue;
                go.SetActive(false);
                Object.Destroy(go);
            }
        }

        /// <summary>
        /// 놓을 자리를 스스로 찾아 세운다 (<c>E2ESpawnLedger</c>와 같은 수법).
        /// 좌표를 박아 두면 지형이 바뀔 때 그 좌표만 조용히 낡는다.
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
                yield break;
            }

            E2EHarness.Log($"  [배치 문제] {id}를 놓을 자리를 못 찾았다 " +
                           $"(마지막 판정 {placer.LastResult})");
            placer.Cancel();
            result(null);
        }

        /// <summary>첫 토대는 붙을 곳이 없으니 평지를 찾아 지면에 놓는다.</summary>
        static IEnumerator 토대를_놓는다(System.Action<GameObject> result)
        {
            var placer = Placer;
            placer.SelectById("piece_foundation");
            yield return null;

            var from = E2EHarness.Player.transform.position;
            var 본판정 = new HashSet<PlacementResult>();

            for (int 고리 = 0; 고리 < 6; 고리++)
            for (int a = 0; a < 12; a++)
            {
                var dir = Quaternion.Euler(0f, a * 30f, 0f) * Vector3.forward;
                var 탐침 = from + dir * (2.5f + 고리 * 2.5f);

                if (!Physics.Raycast(탐침 + Vector3.up * 6f, Vector3.down, out var g, 20f, ~0,
                                     QueryTriggerInteraction.Ignore))
                    continue;

                // 멀리서 겨누면 광선이 중간 지형에 먼저 맞는다. 곁으로 옮겨 선다.
                E2EHarness.Teleport(g.point + Vector3.up * 1.6f - dir * 3.0f);
                yield return null;

                E2EHarness.LookAt(g.point);
                yield return null;
                yield return null;

                var 판정 = placer.Evaluate(out _, out _);
                본판정.Add(판정);
                if (판정 != PlacementResult.Ok) continue;

                var go = placer.TryBuild();
                if (go != null) { placer.Cancel(); result(go); yield break; }
            }

            E2EHarness.Log("  [배치 문제] 토대를 놓을 평지를 찾지 못했다 (본 판정: " +
                           string.Join(", ", 본판정) + ")");
            placer.Cancel();
            result(null);
        }

        /// <summary>
        /// 붙일 자리 곁으로 옮겨 그쪽을 보고 놓는다 (<c>E2EModularBuild</c>와 같다).
        /// 멀리서 좌표만 겨누면 광선이 중간 지형에 먼저 맞는다.
        /// </summary>
        static IEnumerator 곁에_놓는다(string id, Vector3 want, System.Action<GameObject> result)
        {
            var placer = Placer;
            placer.SelectById(id);
            yield return null;

            for (int a = 0; a < 8; a++)
            {
                var dir = Quaternion.Euler(0f, a * 45f, 0f) * Vector3.forward;
                E2EHarness.Teleport(want + dir * 3.2f + Vector3.up * 1.6f);
                yield return null;

                E2EHarness.LookAt(want);
                yield return null;
                yield return null;

                if (placer.Evaluate(out var pos, out _) != PlacementResult.Ok) continue;
                if (Vector3.Distance(pos, want) > 1.2f) continue;

                var go = placer.TryBuild();
                if (go != null) { placer.Cancel(); result(go); yield break; }
            }

            E2EHarness.Log($"  [배치 문제] {id}를 {want.ToString("F1")}에 못 놓았다 " +
                           $"(판정 {placer.Evaluate(out _, out _)})");
            placer.Cancel();
            result(null);
        }

        /// <summary>몇 걸음 옮긴다. 방금 자리에 다음 것을 지으면 제 자취에 걸린다.</summary>
        static IEnumerator 비켜선다()
        {
            var 처음 = E2EHarness.Player.transform.position;

            for (int a = 0; a < 8; a++)
            {
                var dir = Quaternion.Euler(0f, a * 45f + 20f, 0f) * Vector3.forward;
                if (!UnityEngine.AI.NavMesh.SamplePosition(처음 + dir * 5f, out var hit, 3f,
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
