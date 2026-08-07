using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Survive.Core;
using Survive.Progression;
using Survive.Vitals;

namespace Survive.Testing
{
    /// <summary>
    /// <b>저장이 지금 무엇을 하고 무엇을 안 하는가.</b> 두 가지를 본다.
    ///
    /// <list type="number">
    /// <item><b>검사는 사람의 저장본에 한 바이트도 쓰지 않는다</b>
    ///   (<see cref="DefaultSlotUntouched"/>). 저장 경로는 이 기계의 에디터 전부와
    ///   공유되고, 사람은 바깥 에디터에서 직접 플레이한다.</item>
    /// <item><b>게이지 넷이 저장을 넘긴다</b>(<see cref="VitalsRoundTrip"/>)
    ///   — 그리고 <b>게이지 열쇠가 없는 옛 저장본은 그냥 열린다</b>
    ///   (<see cref="OldSaveOpens"/>).</item>
    /// </list>
    ///
    /// 둘이 한 파일에 있는 이유: 게이지가 붙으면서 저장이 실제로 무거워졌고,
    /// 무거워진 저장이 공유 슬롯 위에서 돌면 밟히는 것도 그만큼 커진다.
    /// </summary>
    public static class E2ESaveHygiene
    {
        // ══ 과제 하나 — 사람의 저장본 ═══════════════════════════

        /// <summary>
        /// <b>이 시나리오가 재는 것은 파일 하나다.</b> <c>save_auto.json</c>의 크기와
        /// 쓴 시각이 시작할 때와 끝날 때 같아야 한다 — 그 사이에 자동 저장을 걸고,
        /// 기본 슬롯을 콕 집어 저장하고, 지우기까지 하면서.
        /// </summary>
        public static IEnumerator DefaultSlotUntouched()
        {
            // ── 하네스가 실제로 슬롯을 갈았는가 ─────────────────
            E2EHarness.Assert(SaveSlots.IsIsolated, "하네스가 저장 슬롯을 갈아 끼웠다");
            E2EHarness.Assert(!string.IsNullOrEmpty(E2EHarness.IsolatedSlot),
                              "전용 슬롯 이름이 정해졌다");
            E2EHarness.Assert(SaveSlots.IsIsolationSlot(E2EHarness.IsolatedSlot),
                              $"전용 슬롯 이름이 격리 슬롯이다: {E2EHarness.IsolatedSlot}");

            // 규칙의 핵심 — 기본 슬롯을 콕 집어도 그리로 가지 않는다.
            E2EHarness.Assert(!SaveSlots.TouchesDefault(null),
                              "이름 없는 저장이 사람의 슬롯으로 가지 않는다");
            E2EHarness.Assert(!SaveSlots.TouchesDefault(SaveCoordinator.DefaultSlot),
                              "기본 슬롯을 콕 집어도 사람의 슬롯으로 가지 않는다");

            string 사람슬롯 = SaveSlots.Default;
            string 전용슬롯 = E2EHarness.IsolatedSlot;

            string 시작지문 = E2EHarness.Fingerprint(사람슬롯);
            E2EHarness.Log($"  사람의 저장본: {E2EHarness.SlotPath(사람슬롯)}");
            E2EHarness.Log($"  시작 지문: {시작지문}");
            E2EHarness.Log($"  전용 슬롯: {E2EHarness.SlotPath(전용슬롯)}");

            var coordinator = Object.FindAnyObjectByType<SaveCoordinator>(FindObjectsInactive.Exclude);
            E2EHarness.Assert(coordinator != null, "SaveCoordinator가 있다");

            // ── 자동 저장을 실제로 건다 ─────────────────────────
            // 끄지 않았으므로 여전히 걸린다. 그것이 이 라운드의 판단이다 —
            // 끄면 안전하지만 자동 저장이 어느 검사도 지나가지 않는 길이 된다.
            var chapter = Object.FindAnyObjectByType<ChapterDirector>(FindObjectsInactive.Exclude);
            E2EHarness.Assert(chapter != null, "ChapterDirector가 있다");
            yield return E2EHarness.WaitUntil(() => chapter.Current != null, "챕터가 시작된다", 8f);

            E2EHarness.Assert(E2EHarness.Fingerprint(전용슬롯) == "없음",
                              "전용 슬롯은 빈 자리에서 시작한다");

            chapter.ForceCompleteCurrent();
            yield return null;
            yield return null;

            E2EHarness.Assert(E2EHarness.Fingerprint(전용슬롯) != "없음",
                              "목표 전환에서 자동 저장이 걸렸다 (전용 슬롯에)");
            E2EHarness.Assert(coordinator.HasSave(), "코디네이터도 저장본이 있다고 말한다");
            E2EHarness.Log($"  자동 저장 뒤 전용 슬롯: {E2EHarness.Fingerprint(전용슬롯)}");

            // ── 기본 슬롯을 콕 집어 저장하고 지운다 ─────────────
            // 시나리오가 실수로 이렇게 부르는 것이 원래 사고의 모양이었다.
            coordinator.Save(SaveCoordinator.DefaultSlot);
            yield return null;
            coordinator.Delete(SaveCoordinator.DefaultSlot);
            yield return null;
            E2EHarness.Assert(E2EHarness.Fingerprint(전용슬롯) == "없음",
                              "기본 슬롯을 콕 집은 삭제도 전용 슬롯으로 갔다");

            coordinator.Save();
            yield return null;
            E2EHarness.Assert(E2EHarness.Fingerprint(전용슬롯) != "없음", "다시 저장했다");

            // ── 이 시나리오의 본체 ──────────────────────────────
            string 끝지문 = E2EHarness.Fingerprint(사람슬롯);
            E2EHarness.Log($"  끝 지문: {끝지문}");
            E2EHarness.AssertEqual(끝지문, 시작지문,
                                   "사람의 저장본은 크기도 쓴 시각도 그대로다");

            // ── 뒷정리가 실제로 도는가 ──────────────────────────
            // 러너가 끝에서 부르는 것을 여기서 미리 한 번 불러 눈으로 본다.
            E2EHarness.ReleaseSave();
            E2EHarness.Assert(E2EHarness.Fingerprint(전용슬롯) == "없음",
                              "끝나면 자기 슬롯을 지운다");
            E2EHarness.Assert(!SaveSlots.IsIsolated, "격리가 풀린다");
            E2EHarness.Assert(SaveSlots.TouchesDefault(null),
                              "격리를 풀면 이름 없는 저장이 다시 사람의 슬롯으로 간다");

            // 풀어 둔 채로 나가면 이 재생에 남은 것들이 사람의 슬롯에 쓴다. 다시 건다.
            E2EHarness.IsolateSave();
            E2EHarness.Assert(SaveSlots.IsIsolated, "다시 격리했다");

            E2EHarness.AssertEqual(E2EHarness.Fingerprint(사람슬롯), 시작지문,
                                   "뒷정리를 지나고도 사람의 저장본은 그대로다");
            E2EHarness.Log("=== 사람의 저장본을 밟지 않았다 ===");
        }

        /// <summary>
        /// <b>격리를 걸지 않아도 사람의 저장본은 안 밟힌다.</b>
        ///
        /// <see cref="DefaultSlotUntouched"/>가 재는 것은 <b>하네스를 지나는 실행</b>이다.
        /// 그런데 우리는 시나리오 없이도 재생을 켠다 — 실측·스크린샷·눈으로 확인.
        /// 그때는 슬롯 격리가 걸리지 않고 <c>autoSaveOnObjective</c>가 기본 슬롯에 쓴다.
        /// 그 한 번이 사람의 이어하기를 덮었다(2026-08-07, 지문이 실제로 바뀌었다).
        ///
        /// 그래서 이 시나리오는 <b>일부러 격리를 푼다.</b> 슬롯 규칙을 전부 끄고,
        /// 기본 슬롯에 자동 저장과 손 저장과 삭제를 다 걸고 나서, <b>공유 자리의
        /// 파일 지문</b>이 그대로인지 본다. 지켜 주는 것은 이제 슬롯이 아니라
        /// <b>폴더가 갈렸다는 사실</b>뿐이다(<see cref="SaveLocation"/>).
        ///
        /// <b>남의 자리를 되돌려 놓고 나간다.</b> 이 갈래 폴더의 기본 슬롯은 이
        /// 에디터의 진짜 이어하기다 — 검사가 쓰고 갔으면 검사가 되돌린다.
        /// </summary>
        public static IEnumerator SharedDefaultUntouched()
        {
            // ── 갈래가 실제로 걸려 있는가 (음성 확인) ───────────
            // 이것이 거짓이면 아래 단언들은 전부 같은 파일을 두 번 보는 것이고,
            // 그러면 통과해도 아무것도 증명하지 못한다.
            string 공유폴더 = Application.persistentDataPath;
            E2EHarness.Assert(SaveService.Root != 공유폴더,
                              $"에디터의 저장 폴더가 공유 폴더와 갈렸다\n" +
                              $"    공유: {공유폴더}\n    이 에디터: {SaveService.Root}");
            E2EHarness.Assert(SaveService.Root.StartsWith(공유폴더),
                              "갈래 폴더는 공유 폴더 아래에 있다 (밖으로 나가지 않는다)");

            string 사람파일 = E2EHarness.SharedSlotPath(SaveSlots.Default);
            string 이곳파일 = E2EHarness.SlotPath(SaveSlots.Default);
            E2EHarness.Assert(사람파일 != 이곳파일, "사람의 파일과 이 에디터의 파일이 다른 자리다");

            string 시작지문 = E2EHarness.FingerprintOf(사람파일);
            E2EHarness.Log($"  사람의 저장본: {사람파일}");
            E2EHarness.Log($"  시작 지문: {시작지문}");
            E2EHarness.Log($"  이 에디터의 기본 슬롯: {이곳파일}");

            // ── 이 갈래의 기본 슬롯을 잠시 빌린다 ───────────────
            bool 있었나 = File.Exists(이곳파일);
            string 원래것 = 있었나 ? File.ReadAllText(이곳파일) : null;

            var coordinator = Object.FindAnyObjectByType<SaveCoordinator>(FindObjectsInactive.Exclude);
            E2EHarness.Assert(coordinator != null, "SaveCoordinator가 있다");
            var chapter = Object.FindAnyObjectByType<ChapterDirector>(FindObjectsInactive.Exclude);
            E2EHarness.Assert(chapter != null, "ChapterDirector가 있다");
            yield return E2EHarness.WaitUntil(() => chapter.Current != null, "챕터가 시작된다", 8f);

            // ── 격리를 푼다. 여기서부터가 「그냥 재생」이다 ──────
            E2EHarness.ReleaseSave();
            E2EHarness.Assert(!SaveSlots.IsIsolated, "슬롯 격리를 풀었다");
            E2EHarness.Assert(SaveSlots.TouchesDefault(null),
                              "이름 없는 저장이 기본 슬롯으로 간다 (규칙이 아무것도 안 막는 상태)");
            E2EHarness.AssertEqual(SaveSlots.Resolve(SaveSlots.Default), SaveSlots.Default,
                                   "기본 슬롯 요청이 기본 슬롯 그대로다");

            // ── 밟힐 만한 짓을 전부 한다 ────────────────────────
            chapter.ForceCompleteCurrent();      // 자동 저장 — 원래 사고의 모양
            yield return null;
            yield return null;

            coordinator.Save();                  // 이름 없는 손 저장
            yield return null;
            coordinator.Save(SaveCoordinator.DefaultSlot);   // 기본 슬롯을 콕 집은 저장
            yield return null;

            E2EHarness.Assert(File.Exists(이곳파일),
                              "저장이 실제로 일어났다 (이 에디터의 갈래 폴더에)");
            E2EHarness.Log($"  이 에디터의 기본 슬롯: {E2EHarness.FingerprintOf(이곳파일)}");

            coordinator.Delete(SaveCoordinator.DefaultSlot); // 기본 슬롯을 콕 집은 삭제
            yield return null;
            E2EHarness.Assert(!File.Exists(이곳파일), "삭제도 갈래 폴더로 갔다");

            // ── 이 시나리오의 본체 ──────────────────────────────
            string 끝지문 = E2EHarness.FingerprintOf(사람파일);
            E2EHarness.Log($"  끝 지문: {끝지문}");
            E2EHarness.AssertEqual(끝지문, 시작지문,
                                   시작지문 == "없음"
                                       ? "사람의 저장본은 생기지도 않았다"
                                       : "사람의 저장본은 크기도 쓴 시각도 그대로다");

            // ── 빌린 자리를 되돌린다 ────────────────────────────
            if (있었나) File.WriteAllText(이곳파일, 원래것);
            E2EHarness.AssertEqual(File.Exists(이곳파일), 있었나,
                                   "이 에디터의 기본 슬롯을 원래대로 돌려놓았다");

            // 러너의 뒷정리가 격리를 기대하므로 다시 건다.
            E2EHarness.IsolateSave();
            E2EHarness.Assert(SaveSlots.IsIsolated, "다시 격리했다");

            E2EHarness.AssertEqual(E2EHarness.FingerprintOf(사람파일), 시작지문,
                                   "뒷정리를 지나고도 사람의 저장본은 그대로다");
            E2EHarness.Log("=== 격리 없이도 사람의 저장본을 밟지 않았다 ===");
        }

        // ══ 과제 둘 — 게이지가 저장을 넘긴다 ════════════════════

        /// <summary>
        /// 게이지 넷을 서로 다른 값으로 만들고 <b>실제 파일에 쓴 뒤</b> 씬을 다시
        /// 올려 <b>실제 파일에서 읽는다.</b> 값이 그대로여야 하고, 무엇보다
        /// <b>가득 차 있으면 안 된다</b> — 불러오기가 회복 수단이 되는 순간
        /// 물가로 돌아갈 이유가 사라진다.
        /// </summary>
        public static IEnumerator VitalsRoundTrip()
        {
            var coordinator = Object.FindAnyObjectByType<SaveCoordinator>(FindObjectsInactive.Exclude);
            E2EHarness.Assert(coordinator != null, "SaveCoordinator가 있다");
            E2EHarness.Assert(SaveSlots.IsIsolated, "전용 슬롯 위에서 시험한다");

            var vitals = E2EHarness.Player.Vitals;
            E2EHarness.Assert(vitals != null, "PlayerVitals가 있다");
            E2EHarness.AssertEqual(vitals.All.Count, 4, "게이지가 넷이다");

            // ── 넷을 서로 다르게 만든다 ─────────────────────────
            // 값이 같으면 한 게이지의 값이 다른 게이지에 실려도 통과해 버린다.
            Set(vitals.Health, 63f);
            Set(vitals.Oxygen, 41f);
            Set(vitals.Hydration, 27f);
            Set(vitals.Food, 88f);
            yield return null;

            var 저장값 = new float[vitals.All.Count];

            coordinator.Collect();
            coordinator.Save();
            // 저장이 훑은 그 프레임의 값을 그대로 적어 둔다. 수분·식량은 매 프레임
            // 줄어들므로(SustenanceService), 나중에 읽으면 저장본과 어긋난다.
            for (int i = 0; i < 저장값.Length; i++) 저장값[i] = vitals.All[i].Current;
            yield return null;

            string 파일 = E2EHarness.SlotPath(E2EHarness.IsolatedSlot);
            E2EHarness.Assert(File.Exists(파일), "저장본 파일이 실제로 생겼다");

            string 본문 = File.ReadAllText(파일);
            E2EHarness.Assert(본문.Contains(VitalsSave.Key),
                              $"파일에 게이지 칸이 있다 ({VitalsSave.Key})");
            E2EHarness.Log($"  저장: 체력 {저장값[0]:F1} / 산소 {저장값[1]:F1} / " +
                           $"수분 {저장값[2]:F1} / 식량 {저장값[3]:F1} — {본문.Length}바이트");

            // ── 게임을 다시 시작한다 ────────────────────────────
            E2EHarness.Log("  씬을 다시 올린다");
            var op = SceneManager.LoadSceneAsync("MainScene");
            while (op != null && !op.isDone) yield return null;
            yield return null;
            yield return null;

            var fresh = Object.FindAnyObjectByType<SaveCoordinator>(FindObjectsInactive.Exclude);
            E2EHarness.Assert(fresh != null, "새 씬에 SaveCoordinator가 있다");

            var 새몸 = E2EHarness.Player.Vitals;
            E2EHarness.Assert(새몸 != vitals, "새 몸이다");
            E2EHarness.Assert(새몸.Health.Current > 저장값[0] + 10f,
                              $"다시 시작하면 체력이 저장값보다 높다 ({새몸.Health.Current:F1})");

            // ── 이어하기 ────────────────────────────────────────
            fresh.Collect();
            E2EHarness.Assert(fresh.Load(), "저장본을 불러왔다");
            yield return null;

            Near(새몸.Health.Current, 저장값[0], 1.0f, "체력");
            Near(새몸.Oxygen.Current, 저장값[1], 1.0f, "산소");
            Near(새몸.Hydration.Current, 저장값[2], 2.0f, "수분");
            Near(새몸.Food.Current, 저장값[3], 2.0f, "식량");

            // 이 넷이 이 라운드의 판단이다.
            foreach (var v in 새몸.All)
                E2EHarness.Assert(!v.IsFull,
                                  $"불러오기는 회복 수단이 아니다 ({v.Current:F1}/{v.Max:F0})");

            E2EHarness.Log("=== 게이지 넷이 저장을 넘겼다 ===");
        }

        /// <summary>
        /// <b>게이지 열쇠가 없는 저장본이 그냥 열린다.</b> 세이브 포맷이 추가형이라는
        /// 말을 실제 파일로 확인한다 — 게이지 칸을 들어낸 파일을 만들어 놓고 연다.
        ///
        /// 열쇠가 없을 때 0으로 채우면 옛 저장본을 연 사람이 <b>바닥에서 깨어난다.</b>
        /// 그래서 「적혀 있지 않으면 손대지 않는다」가 규칙이다.
        /// </summary>
        public static IEnumerator OldSaveOpens()
        {
            var coordinator = Object.FindAnyObjectByType<SaveCoordinator>(FindObjectsInactive.Exclude);
            E2EHarness.Assert(coordinator != null, "SaveCoordinator가 있다");
            E2EHarness.Assert(SaveSlots.IsIsolated, "전용 슬롯 위에서 시험한다");

            var vitals = E2EHarness.Player.Vitals;
            var inv = E2EHarness.Player.Inventory;
            var scrap = inv.Database != null ? inv.Database.GetById("scrap") : null;
            E2EHarness.Assert(scrap != null, "스크랩 정의를 찾았다");

            // ── 게이지 칸이 있는 저장본을 만든다 ────────────────
            inv.Inventory.TryAdd(scrap, 7);
            int 저장한스크랩 = inv.Inventory.CountOf("scrap");
            Set(vitals.Health, 33f);

            coordinator.Collect();
            coordinator.Save();
            yield return null;

            string 파일 = E2EHarness.SlotPath(E2EHarness.IsolatedSlot);
            E2EHarness.Assert(File.Exists(파일), "저장본 파일이 생겼다");

            // ── 게이지 칸을 들어낸다 ────────────────────────────
            string 본문 = File.ReadAllText(파일);
            E2EHarness.Assert(본문.Contains(VitalsSave.Key), "들어내기 전에는 게이지 칸이 있다");

            E2EHarness.Assert(SaveSerializer.TryDeserialize(본문, out var snapshot, out var 사유),
                              $"저장본을 읽어 냈다 ({사유})");
            int 지운수 = snapshot.entries.RemoveAll(e => e.key == VitalsSave.Key);
            E2EHarness.AssertEqual(지운수, 1, "게이지 칸 하나를 들어냈다");

            string 옛것 = SaveSerializer.Serialize(snapshot);
            E2EHarness.Assert(!옛것.Contains(VitalsSave.Key), "들어낸 파일에는 게이지 칸이 없다");
            File.WriteAllText(파일, 옛것);
            E2EHarness.Log($"  게이지 칸 없는 저장본을 썼다: {옛것.Length}바이트 " +
                           $"(칸 {snapshot.entries.Count}개)");

            // ── 몸과 소지품을 딴판으로 만들어 둔다 ──────────────
            Set(vitals.Health, 77f);
            inv.Inventory.TryAdd(scrap, 5);
            float 열기전체력 = vitals.Health.Current;
            E2EHarness.Assert(inv.Inventory.CountOf("scrap") > 저장한스크랩, "소지품이 달라졌다");

            // ── 옛 저장본을 연다 ────────────────────────────────
            E2EHarness.Assert(coordinator.Load(), "게이지 열쇠가 없는 저장본이 그냥 열린다");
            yield return null;

            E2EHarness.AssertEqual(inv.Inventory.CountOf("scrap"), 저장한스크랩,
                                   "게이지 칸이 없어도 나머지 칸은 그대로 복원된다");
            Near(vitals.Health.Current, 열기전체력, 0.5f, "적혀 있지 않은 체력");
            E2EHarness.Assert(!float.IsNaN(vitals.Health.Current), "체력이 숫자다");

            E2EHarness.Log("=== 옛 저장본이 그냥 열렸다 ===");
        }

        // ── 도구 ─────────────────────────────────────────────────

        /// <summary>게이지에는 대입 창구가 없다. 차이만큼 밀어 넣는다.</summary>
        static void Set(Vital vital, float value) => vital.Modify(value - vital.Current);

        static void Near(float actual, float expected, float tolerance, string what)
        {
            float 어긋남 = Mathf.Abs(actual - expected);
            E2EHarness.Assert(어긋남 <= tolerance,
                              $"{what} 복원: {actual:F2} (기댓값 {expected:F2}, 어긋남 {어긋남:F3})");
        }
    }
}
