using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using Survive.Core;
using Survive.Items;
using Survive.Progression;
using Survive.UI;

namespace Survive.Testing
{
    /// <summary>
    /// 분석 기록(도감) — 알아낸 것이 실제로 <b>화면에서 읽히는가</b> (백로그 43).
    ///
    /// 목록을 짓는 규칙 자체는 EditMode(CodexCatalogTests)가 이미 지킨다.
    /// 여기서 보는 것은 그 규칙이 진짜 원장·진짜 소지품·진짜 키보드에 닿는지다:
    /// 모르는 것이 정말 실루엣으로 뜨는가, 재료를 손에 넣은 그 순간 화면이
    /// 따라 바뀌는가, 거기 적힌 문장이 그때 AI가 한 말과 <b>같은 문장</b>인가,
    /// 그리고 ESC가 이 화면만 걷어내고 아래 있던 화면은 남기는가.
    /// </summary>
    public static class E2ECodex
    {
        const string PartItem = "machine_part";
        const string PartDiscovery = "disc_machine_part";
        const string MechanismKey = "bp_salvaged_mechanism";
        const string EyeKey = "codex_eye";

        static Inventory Inv => E2EHarness.Player.Inventory.Inventory;
        static UnlockLedger Ledger => UnlockService.Instance.Ledger;
        static CodexUI Codex => CodexUI.Instance;

        public static IEnumerator FullRun()
        {
            E2EHarness.ClearLog();
            E2EHarness.Log("=== 분석 기록 ===");

            E2EHarness.Assert(Codex != null, "도감 화면이 스스로 서 있다 (씬 배선 없음)");
            E2EHarness.Assert(UnlockService.Instance != null, "해금 서비스가 서 있다");
            E2EHarness.Assert(BlueprintGate.Active != null, "원장이 GameServices에 올라와 있다");

            yield return 모르는_것은_실루엣으로_뜬다();
            yield return 청사진_탭도_잠긴_채로_보인다();
            yield return 손에_넣는_순간_기록에_나타난다();
            yield return 열린_청사진은_출처를_적는다();
            yield return 생물은_관측_열쇠로_열린다();
            yield return ESC는_이것만_걷어낸다();

            E2EHarness.Log("=== 분석 기록 통과 ===");
        }

        // ── 1. 아무것도 모르는 상태 ──────────────────────────────

        static IEnumerator 모르는_것은_실루엣으로_뜬다()
        {
            E2EHarness.Log("[1] 아무것도 모르는 채로 기록을 연다");

            Codex.Close();
            Ledger.Clear();
            비운다(PartItem, "scrap");
            yield return null;

            // 실제 조작으로 연다. 화면이 있는 것과 손이 닿는 것은 다른 문제다.
            yield return E2EHarness.TapKey(Key.J);
            yield return E2EHarness.WaitUntil(() => Codex.IsOpen, "J 키로 분석 기록이 열렸다", 3f);

            Codex.ShowSection(CodexSection.Discovery);
            yield return null;

            var row = Row(PartDiscovery);
            E2EHarness.Assert(row != null, "기계 부품 줄이 목록에 <b>남아</b> 있다 (숨기지 않는다)");
            E2EHarness.AssertEqual(Label(row), CodexCatalog.UnknownTitle,
                                   "모르는 것은 이름조차 주지 않는다");

            Codex.Select(IndexOf(PartDiscovery));
            yield return null;

            string body = Detail();
            E2EHarness.AssertEqual(body, CodexCatalog.UnknownDiscoveryBody,
                                   "설명 자리에는 무엇을 하면 열리는지만 적힌다");
            E2EHarness.Assert(!body.Contains("기계 부품"),
                              $"잠긴 줄이 물질 이름을 흘리지 않는다 — \"{body}\"");
        }

        // ── 2. 청사진 탭 ─────────────────────────────────────────

        static IEnumerator 청사진_탭도_잠긴_채로_보인다()
        {
            E2EHarness.Log("[2] 청사진 갈래로 옮긴다");

            ClickTab(CodexSection.Blueprint);
            yield return null;

            E2EHarness.AssertEqual(Codex.Section, CodexSection.Blueprint, "탭을 눌러 갈래가 바뀌었다");
            E2EHarness.Assert(Codex.Entries.Count > 0, $"청사진 줄이 있다 ({Codex.Entries.Count}개)");

            var row = Row(MechanismKey);
            E2EHarness.Assert(row != null, "부품 재조립 줄이 목록에 있다");
            E2EHarness.AssertEqual(Label(row), CodexCatalog.UnknownTitle, "아직 확보하지 않았다");

            Codex.Select(IndexOf(MechanismKey));
            yield return null;
            E2EHarness.Log($"  잠긴 청사진 설명: {Detail()}");
            E2EHarness.Assert(Detail().Contains("기계 부품"),
                              "청사진은 여는 방법(힌트)만은 알려 준다 — 찾아 나설 수 있어야 한다");
        }

        // ── 3. 습득 ──────────────────────────────────────────────

        static IEnumerator 손에_넣는_순간_기록에_나타난다()
        {
            E2EHarness.Log("[3] 기계 부품을 손에 넣는다 (화면은 열어 둔 채)");

            ClickTab(CodexSection.Discovery);
            yield return null;

            var db = E2EHarness.Player.Inventory.Database;
            var part = db.GetById(PartItem);
            E2EHarness.Assert(part != null, "기계 부품 정의가 있다");

            var discovery = UnlockService.Instance.Book.Find(PartItem);
            E2EHarness.Assert(discovery != null, "매핑표에 기계 부품 발견이 있다");

            // 실제 습득 경로와 같은 길목(Inventory.TryAdd)을 지난다.
            Inv.TryAdd(part, 1);
            E2EHarness.Log("  [주입] machine_part +1 (줍는 동작 자체는 다른 시나리오가 지킨다)");

            E2EHarness.Assert(Ledger.IsUnlocked(PartDiscovery), "발견이 원장에 적혔다");

            // 화면을 다시 열지 않는다. 열어 둔 채로 따라 바뀌어야 한다.
            yield return E2EHarness.WaitUntil(
                () => Label(Row(PartDiscovery)) != CodexCatalog.UnknownTitle,
                "열어 둔 기록이 그 자리에서 갱신됐다", 3f);

            var row = Row(PartDiscovery);
            E2EHarness.AssertEqual(Label(row), part.displayName, "이제 이름이 뜬다");

            Codex.Select(IndexOf(PartDiscovery));
            yield return null;

            E2EHarness.Log($"  기록: {Detail()}");
            E2EHarness.AssertEqual(Detail(), discovery.line.text,
                                   "설명문은 그때 AI가 한 말 <b>그대로</b>다 (도감용 문장을 따로 쓰지 않는다)");

            // 자막은 대기열을 타고 나가므로 몇 프레임 늦을 수 있다.
            yield return E2EHarness.WaitUntil(
                () => UnlockService.Instance.LastLine == discovery.line.text,
                "같은 문장이 자막으로도 나갔다", 10f);
            E2EHarness.AssertEqual(Detail(), UnlockService.Instance.LastLine,
                                   "자막으로 나간 문장과 기록에 남은 문장이 같다");
        }

        // ── 4. 청사진 출처 ───────────────────────────────────────

        static IEnumerator 열린_청사진은_출처를_적는다()
        {
            E2EHarness.Log("[4] 열린 청사진에는 어느 경로로 알게 됐는지가 적힌다");

            ClickTab(CodexSection.Blueprint);
            yield return null;

            E2EHarness.Assert(Ledger.IsUnlocked(MechanismKey), "부품 재조립이 열려 있다");

            var row = Row(MechanismKey);
            E2EHarness.AssertEqual(Label(row), "부품 재조립", "청사진 이름이 뜬다");

            Codex.Select(IndexOf(MechanismKey));
            yield return null;

            string body = Detail();
            E2EHarness.Log($"  기록: {body}");
            E2EHarness.Assert(body.Contains("현장 분석"),
                              $"주운 것과 밝혀낸 것을 구별해 적는다 — \"{body}\"");
        }

        // ── 5. 생물 ──────────────────────────────────────────────

        static IEnumerator 생물은_관측_열쇠로_열린다()
        {
            E2EHarness.Log("[5] 개체 관측 — codex_<생물id> 열쇠가 여는 자리");

            ClickTab(CodexSection.Creature);
            yield return null;

            E2EHarness.Assert(Codex.Entries.Count > 0,
                              $"생물 목록을 읽었다 ({Codex.Entries.Count}종)");
            E2EHarness.Assert(Codex.Entries.All(e => !e.Unlocked),
                              "아직 아무것도 관측하지 않았으므로 전부 실루엣이다");

            var row = Row(EyeKey);
            E2EHarness.Assert(row != null, $"{EyeKey} 줄이 있다 (열쇠 계약)");
            E2EHarness.AssertEqual(Label(row), CodexCatalog.UnknownTitle, "못 본 것은 이름이 없다");

            // 이 열쇠를 여는 경로(관측·처치)는 다른 작업이 붙인다. 여기서 지키는 것은
            // "그 열쇠가 열리면 이 화면이 무엇을 보여 주는가"라는 계약 하나다.
            Ledger.Unlock(EyeKey);
            yield return E2EHarness.WaitUntil(
                () => Label(Row(EyeKey)) != CodexCatalog.UnknownTitle,
                "열쇠가 열리자 개체 기록이 드러났다", 3f);

            Codex.Select(IndexOf(EyeKey));
            yield return null;

            string body = Detail();
            E2EHarness.Log($"  기록: {body.Replace("\n", " / ")}");
            E2EHarness.Assert(body.Contains("분해자"), "영양 단계가 적힌다");
            E2EHarness.Assert(body.Contains("비행"), "이동 방식이 적힌다");
            E2EHarness.Assert(body.Contains("탐지"), "잰 값이 적힌다");
            E2EHarness.Assert(!body.Contains("—") && !body.Contains("−"),
                              "본문 글꼴에 없는 줄표가 화면에 나가지 않는다");
        }

        // ── 6. 닫기와 순서 ───────────────────────────────────────

        static IEnumerator ESC는_이것만_걷어낸다()
        {
            E2EHarness.Log("[6] ESC 한 번은 맨 나중에 연 것 하나만 닫는다");

            E2EHarness.Assert(GameServices.TryGet<UIStateService>(out var ui),
                              "UIStateService가 화면들을 쥐고 있다");

            Codex.Close();
            yield return null;

            var inventory = Object.FindObjectsByType<InventoryUI>(FindObjectsInactive.Include)
                                  .FirstOrDefault();
            E2EHarness.Assert(inventory != null, "소지품 화면이 있다");

            inventory.Open();
            yield return null;
            E2EHarness.Assert(inventory.IsOpen, "소지품을 먼저 열었다");

            Codex.Open();
            yield return null;
            E2EHarness.Assert(Codex.IsOpen && inventory.IsOpen, "그 위에 기록을 겹쳐 열었다");
            E2EHarness.AssertEqual(Codex.PanelKind, UIPanelKind.Codex, "패널 종류가 등록돼 있다");

            // 실제 ESC. 도감은 ESC를 스스로 듣지 않으므로, 여기서 닫힌다면
            // UIStateService의 순서 판단을 타고 닫힌 것이다.
            yield return E2EHarness.TapKey(Key.Escape);
            yield return E2EHarness.WaitUntil(() => !Codex.IsOpen, "ESC가 기록을 닫았다", 3f);
            E2EHarness.Assert(inventory.IsOpen,
                              "아래 있던 소지품은 그대로 남았다 — 한 번에 하나만 닫는다");

            yield return E2EHarness.TapKey(Key.Escape);
            yield return E2EHarness.WaitUntil(() => !inventory.IsOpen, "다음 ESC가 소지품을 닫았다", 3f);

            E2EHarness.Assert(!ui.IsPaused, "패널을 닫는 동안 일시정지로 새지 않았다");

            // 다시 열 수 있어야 한다. 닫고 끝나는 화면은 검증한 적이 없는 것과 같다.
            yield return E2EHarness.TapKey(Key.J);
            yield return E2EHarness.WaitUntil(() => Codex.IsOpen, "J로 다시 열린다", 3f);

            yield return E2EHarness.TapKey(Key.J);
            yield return E2EHarness.WaitUntil(() => !Codex.IsOpen, "J로 다시 닫힌다", 3f);
        }

        // ── 도우미 ───────────────────────────────────────────────

        static void ClickTab(CodexSection section)
        {
            var tab = Codex.GetComponentsInChildren<Button>(true)
                           .FirstOrDefault(b => b.gameObject.name == "Tab_" + section);
            E2EHarness.Assert(tab != null, $"{section} 탭 단추가 있다");
            tab.onClick.Invoke();
        }

        static Button Row(string key) =>
            Codex.GetComponentsInChildren<Button>(true)
                 .FirstOrDefault(b => b.gameObject.name == "Codex_" + key);

        static int IndexOf(string key)
        {
            for (int i = 0; i < Codex.Entries.Count; i++)
                if (Codex.Entries[i].Key == key) return i;
            return -1;
        }

        static string Label(Component row)
        {
            if (row == null) return "";
            var t = row.GetComponentsInChildren<TMP_Text>(true)
                       .FirstOrDefault(x => x.gameObject.name == "Label");
            return t != null ? t.text : "";
        }

        static string Detail()
        {
            var t = Codex.GetComponentsInChildren<TMP_Text>(true)
                         .FirstOrDefault(x => x.gameObject.name == "DetailBody");
            return t != null ? t.text : "";
        }

        static void 비운다(params string[] itemIds)
        {
            foreach (var id in itemIds)
            {
                int have = Inv.CountOf(id);
                if (have > 0) Inv.TryRemove(id, have);
            }
        }
    }
}
