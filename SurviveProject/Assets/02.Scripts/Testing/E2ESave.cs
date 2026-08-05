using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Survive.Core;
using Survive.Items;
using Survive.Progression;
using Survive.Vitals;

namespace Survive.Testing
{
    /// <summary>
    /// B3 — 저장하고, 게임을 다시 시작하고, 이어서 할 수 있는지 본다.
    ///
    /// 저장이 "파일이 써졌다"로 끝나면 의미가 없다. 씬을 통째로 다시 올린 뒤
    /// 인벤토리와 목표 번호가 그대로인지까지 확인해야 이어하기가 된다.
    /// </summary>
    public static class E2ESave
    {
        public static IEnumerator FullRun()
        {
            var coordinator = Object.FindAnyObjectByType<SaveCoordinator>(FindObjectsInactive.Exclude);
            E2EHarness.Assert(coordinator != null, "SaveCoordinator가 있다");

            // 이전 실행의 저장본이 결과를 가리지 않게 한다
            coordinator.Delete();
            E2EHarness.Assert(!coordinator.HasSave(), "저장본 없는 상태에서 시작한다");

            var chapter = Object.FindAnyObjectByType<ChapterDirector>(FindObjectsInactive.Exclude);
            E2EHarness.Assert(chapter != null, "ChapterDirector가 있다");
            yield return E2EHarness.WaitUntil(() => chapter.Current != null, "챕터가 시작된다", 5f);

            // ── 진행도를 만든다 ─────────────────────────────────
            var inv = E2EHarness.Player.Inventory;
            var db = inv.Database;
            E2EHarness.Assert(db != null, "아이템 데이터베이스가 연결돼 있다");

            var scrap = db.GetById("scrap");
            var part = db.GetById("machine_part");
            E2EHarness.Assert(scrap != null && part != null, "저장에 쓸 아이템 정의를 찾았다");

            inv.Inventory.TryAdd(scrap, 7);
            inv.Inventory.TryAdd(part, 3);

            // 목표를 하나 넘긴다. 넘어가는 순간 자동 저장이 걸린다.
            chapter.ForceCompleteCurrent();
            yield return null;

            int expectedIndex = chapter.CurrentIndex;
            int expectedScrap = inv.Inventory.CountOf("scrap");
            int expectedPart = inv.Inventory.CountOf("machine_part");
            E2EHarness.Log($"  저장 시점: 목표 {expectedIndex}, 스크랩 {expectedScrap}, 부품 {expectedPart}");

            // 자동 저장이 걸렸어야 한다
            E2EHarness.Assert(coordinator.HasSave(), "목표 전환에서 자동 저장이 걸렸다");

            // 명시적으로 한 번 더 저장해 최신 상태를 확실히 한다
            coordinator.Save();
            yield return null;

            // ── 게임을 다시 시작한다 ────────────────────────────
            E2EHarness.Log("  씬을 다시 올린다");
            var op = SceneManager.LoadSceneAsync("MainScene");
            while (op != null && !op.isDone) yield return null;
            yield return null;
            yield return null;

            var fresh = Object.FindAnyObjectByType<SaveCoordinator>(FindObjectsInactive.Exclude);
            E2EHarness.Assert(fresh != null, "새 씬에 SaveCoordinator가 있다");

            var freshChapter = Object.FindAnyObjectByType<ChapterDirector>(FindObjectsInactive.Exclude);
            yield return E2EHarness.WaitUntil(() => freshChapter != null && freshChapter.Current != null,
                                              "새 씬의 챕터가 시작된다", 8f);

            var freshInv = E2EHarness.Player.Inventory;
            E2EHarness.AssertEqual(freshInv.Inventory.CountOf("scrap"), 0, "다시 시작하면 인벤토리가 비어 있다");

            // ── 이어하기 ────────────────────────────────────────
            fresh.Collect();
            E2EHarness.Assert(fresh.Load(), "저장본을 불러왔다");
            yield return null;

            E2EHarness.AssertEqual(freshInv.Inventory.CountOf("scrap"), expectedScrap, "스크랩 복원");
            E2EHarness.AssertEqual(freshInv.Inventory.CountOf("machine_part"), expectedPart, "기계 부품 복원");
            E2EHarness.AssertEqual(freshChapter.CurrentIndex, expectedIndex, "목표 진행도 복원");
            E2EHarness.Log("  복원된 목표: " +
                           (freshChapter.Current != null ? freshChapter.Current.displayText : "(완주)"));

            // 뒷정리 — 다음 실행이 이 저장본에 영향받지 않게 한다
            fresh.Delete();
        }

        /// <summary>
        /// 죽은 자리에 남긴 가방이 저장을 넘기는가.
        ///
        /// 넘기지 못하면 저장이 "죽기 전으로 돌아가는 버튼"이 된다 —
        /// 잃은 것이 저장본에 없으니 다시 켜면 그냥 사라지고, 되찾으러 갈
        /// 이유도 같이 사라진다. 왕복 압박(P2 §6-3)은 그 자리에 그것이
        /// 남아 있다는 사실 하나로 성립한다.
        ///
        /// 가방은 플레이 중에 생겨나는 물건이라 씬에 되받을 주체가 없다.
        /// 그 주체가 <see cref="DeathDropService"/>이고, 이 시나리오가
        /// 확인하는 것은 결국 그것이 실제로 주체 노릇을 하는가이다.
        /// </summary>
        public static IEnumerator BagRoundTrip()
        {
            var coordinator = Object.FindAnyObjectByType<SaveCoordinator>(FindObjectsInactive.Exclude);
            E2EHarness.Assert(coordinator != null, "SaveCoordinator가 있다");
            coordinator.Delete();

            E2EHarness.Assert(DeathDropService.Instance != null, "사망 드롭 서비스가 스스로 서 있다");

            var chapter = Object.FindAnyObjectByType<ChapterDirector>(FindObjectsInactive.Exclude);
            yield return E2EHarness.WaitUntil(() => chapter != null && chapter.Current != null,
                                              "챕터가 시작된다", 8f);

            // ── 죽어서 가방을 남긴다 ────────────────────────────
            var inv = E2EHarness.Player.Inventory;
            var db = inv.Database;
            E2EHarness.Assert(db != null, "아이템 데이터베이스가 연결돼 있다");

            var scrap = db.GetById("scrap");
            var alloy = db.GetById("alien_alloy");
            E2EHarness.Assert(scrap != null && alloy != null, "떨굴 아이템 정의를 찾았다");

            inv.Inventory.TryAdd(scrap, 9);
            inv.Inventory.TryAdd(alloy, 4);
            int 벌어온스크랩 = inv.Inventory.CountOf("scrap");
            int 벌어온합금 = inv.Inventory.CountOf("alien_alloy");

            int 이전가방수 = DeathDropBag.Active.Count;
            var vitals = E2EHarness.Player.Vitals;
            vitals.Health.Modify(-(vitals.Health.Max + 10f));

            yield return E2EHarness.WaitUntil(() => DeathDropBag.Active.Count > 이전가방수,
                                              "죽은 자리에 가방이 남는다", 5f);

            var 남긴가방 = DeathDropService.LastBag;
            E2EHarness.Assert(남긴가방 != null, "남긴 가방을 집을 수 있다");
            var 가방자리 = 남긴가방.transform.position;
            E2EHarness.Log($"  가방 {가방자리.ToString("F2")}, " +
                           $"스크랩 {벌어온스크랩} / 합금 {벌어온합금}");

            // ── 저장한다 ────────────────────────────────────────
            // 여기서 Collect()를 다시 부르지 않는다. 자동 저장은 목표 전환에 걸리고
            // 그때 모아 둔 목록은 씬이 올라올 때 한 번 훑은 것뿐이다 —
            // 씬을 넘어 사는 서비스가 그 훑기에 잡히는지까지가 검증 대상이다.
            coordinator.Save();
            yield return null;
            E2EHarness.Assert(coordinator.HasSave(), "저장본이 생겼다");

            // ── 게임을 다시 시작한다 ────────────────────────────
            E2EHarness.Log("  씬을 다시 올린다");
            var op = SceneManager.LoadSceneAsync("MainScene");
            while (op != null && !op.isDone) yield return null;
            yield return null;
            yield return null;

            E2EHarness.AssertEqual(DeathDropBag.Active.Count, 0,
                                   "다시 시작하면 가방이 하나도 없다");

            var fresh = Object.FindAnyObjectByType<SaveCoordinator>(FindObjectsInactive.Exclude);
            E2EHarness.Assert(fresh != null, "새 씬에 SaveCoordinator가 있다");

            var freshChapter = Object.FindAnyObjectByType<ChapterDirector>(FindObjectsInactive.Exclude);
            yield return E2EHarness.WaitUntil(() => freshChapter != null && freshChapter.Current != null,
                                              "새 씬의 챕터가 시작된다", 8f);

            // ── 이어하기 ────────────────────────────────────────
            fresh.Collect();
            E2EHarness.Assert(fresh.Load(), "저장본을 불러왔다");
            yield return null;

            E2EHarness.AssertEqual(DeathDropBag.Active.Count, 1, "가방이 하나 되살아났다");

            var 되살아난가방 = DeathDropBag.Active[0];
            E2EHarness.Assert(되살아난가방 != null, "되살아난 가방을 집을 수 있다");

            float 어긋남 = Vector3.Distance(되살아난가방.transform.position, 가방자리);
            E2EHarness.Log($"  되살아난 자리 {되살아난가방.transform.position.ToString("F2")} " +
                           $"(어긋남 {어긋남:F3}m)");
            E2EHarness.Assert(어긋남 < 0.05f, $"가방은 죽은 그 자리에 다시 선다 ({어긋남:F3}m)");

            E2EHarness.AssertEqual(되살아난가방.Contents.CountOf("scrap"), 벌어온스크랩,
                                   "가방 속 스크랩이 그대로다");
            E2EHarness.AssertEqual(되살아난가방.Contents.CountOf("alien_alloy"), 벌어온합금,
                                   "가방 속 합금이 그대로다");

            var freshInv = E2EHarness.Player.Inventory.Inventory;
            E2EHarness.AssertEqual(freshInv.CountOf("scrap"), 0,
                                   "되살린 판에서도 떨군 것은 아직 소지품에 없다");

            // ── 되찾는다 ────────────────────────────────────────
            // 되살아난 가방이 "보이기만 하는 상자"가 아니라 실제로 회수되는지까지 본다.
            yield return E2EHarness.StandInFrontOf(되살아난가방.transform, 1.8f);
            E2EHarness.LookAt(되살아난가방.transform.position);
            yield return null;
            yield return null;

            var it = E2EHarness.Player.Interactor;
            yield return E2EHarness.WaitUntil(() => it.Current is DeathDropBag,
                                              "되살아난 가방이 조준된다", 5f);
            E2EHarness.Log("  프롬프트: " + it.Current.InteractionPrompt);

            yield return E2EHarness.TapKey(Key.E);
            yield return E2EHarness.WaitUntil(() => freshInv.CountOf("scrap") > 0,
                                              "E 한 번으로 되찾는다", 4f);

            E2EHarness.AssertEqual(freshInv.CountOf("scrap"), 벌어온스크랩, "스크랩이 돌아왔다");
            E2EHarness.AssertEqual(freshInv.CountOf("alien_alloy"), 벌어온합금, "합금이 돌아왔다");

            yield return null;
            E2EHarness.Assert(되살아난가방 == null, "다 비운 가방은 세계에서 사라진다");

            fresh.Delete();
            E2EHarness.Log("=== 가방이 저장을 넘겼다 ===");
        }
    }
}
