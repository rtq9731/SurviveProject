using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Survive.Core;
using Survive.Items;
using Survive.Progression;

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
            var coordinator = Object.FindFirstObjectByType<SaveCoordinator>(FindObjectsInactive.Exclude);
            E2EHarness.Assert(coordinator != null, "SaveCoordinator가 있다");

            // 이전 실행의 저장본이 결과를 가리지 않게 한다
            coordinator.Delete();
            E2EHarness.Assert(!coordinator.HasSave(), "저장본 없는 상태에서 시작한다");

            var chapter = Object.FindFirstObjectByType<ChapterDirector>(FindObjectsInactive.Exclude);
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

            var fresh = Object.FindFirstObjectByType<SaveCoordinator>(FindObjectsInactive.Exclude);
            E2EHarness.Assert(fresh != null, "새 씬에 SaveCoordinator가 있다");

            var freshChapter = Object.FindFirstObjectByType<ChapterDirector>(FindObjectsInactive.Exclude);
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
    }
}
