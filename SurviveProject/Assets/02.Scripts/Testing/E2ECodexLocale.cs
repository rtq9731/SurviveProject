using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Survive.Items;
using Survive.Localization;
using Survive.Progression;
using Survive.UI;

namespace Survive.Testing
{
    /// <summary>
    /// 분석 기록(도감)이 <b>열린 채로</b> 로케일을 따라오는가.
    ///
    /// 여기서 보는 것은 두 갈래다.
    ///
    /// ① <b>화면 껍데기</b> — 제목·부제·바닥글·탭·요약. 전부 세울 때 한 번 쓰이고
    /// 아무도 다시 쓰지 않는 자리다. 창을 닫았다 열면 바뀌어 있어서 사람 눈으로는
    /// 언제나 통과한 것처럼 보인다. 그래서 <b>다시 열지 않고</b> 바꾼다.
    ///
    /// ② <b>데이터 에셋에서 온 이름</b> — 목록 줄에 뜨는 물질 이름. 이 줄이 en에서
    /// 그대로 한국어면 그 자리는 <see cref="DataText"/>를 우회해 SO를 직접 읽고
    /// 있다는 뜻이다. 정적 게이트가 이미 그것을 막고 있지만, 정적 검사는 "읽는
    /// 꼴"만 볼 뿐 값이 실제로 화면에 닿았는지는 모른다.
    /// </summary>
    public static class E2ECodexLocale
    {
        const string PartItem = "machine_part";
        const string PartDiscovery = "disc_machine_part";

        static CodexUI Codex => CodexUI.Instance;
        static Inventory Inv => E2EHarness.Player.Inventory.Inventory;

        public static IEnumerator FullRun()
        {
            E2EHarness.ClearLog();
            E2EHarness.Log("=== 분석 기록 로케일 ===");

            string 처음로케일 = Loc.CurrentLocale;

            yield return 표가_서_있다();
            yield return 기록을_연다();
            yield return 열린_채로_영어로_바꾼다();
            yield return 열린_채로_의사_번역으로_바꾼다();
            yield return 되돌리면_원래_글자로_돌아온다(처음로케일);

            Codex.Close();
            yield return null;

            E2EHarness.Log("=== 분석 기록 로케일 통과 ===");
        }

        static IEnumerator 표가_서_있다()
        {
            yield return E2EHarness.WaitUntil(() => Loc.IsLoaded, "번역 표가 실렸다", 8f);

            E2EHarness.Assert(Loc.Catalog.Problems.Count == 0,
                $"표에 건전성 문제가 없다 ({Loc.Catalog.Problems.Count}건)");
            E2EHarness.Assert(Loc.T("UI", "codex_title") != "codex_title",
                              "도감 제목이 표에서 나온다 (키가 그대로 뜨지 않는다)");
        }

        static IEnumerator 기록을_연다()
        {
            E2EHarness.Assert(Codex != null, "도감 화면이 스스로 서 있다");

            // 잠긴 줄은 이름을 주지 않는다. 이름이 로케일을 따라오는지 보려면
            // 한 줄은 열려 있어야 한다 — 실제 습득 길목(Inventory.TryAdd)을 지난다.
            var part = E2EHarness.Player.Inventory.Database.GetById(PartItem);
            E2EHarness.Assert(part != null, "기계 부품 정의가 있다");
            if (!UnlockService.Instance.Ledger.IsUnlocked(PartDiscovery)) Inv.TryAdd(part, 1);

            Codex.Open();
            Codex.ShowSection(CodexSection.Discovery);
            yield return null;
            yield return null;

            E2EHarness.Assert(Codex.IsOpen, "분석 기록이 열렸다");
            E2EHarness.Assert(줄글자(PartDiscovery) != CodexCatalog.UnknownTitle,
                              $"물질 분석 줄이 열려 있다 — \"{줄글자(PartDiscovery)}\"");

            E2EHarness.AssertEqual(글자("Title"), Loc.T("UI", "codex_title"), "제목이 표에서 나왔다");
            E2EHarness.AssertEqual(글자("Caption"), Loc.T("UI", "codex_caption"), "부제도 표에서 나왔다");
            E2EHarness.Log($"  ko 제목 \"{글자("Title")}\" / 탭 \"{탭글자()}\" / " +
                           $"요약 \"{글자("Summary")}\" / 줄 \"{줄글자(PartDiscovery)}\"");
        }

        static IEnumerator 열린_채로_영어로_바꾼다()
        {
            E2EHarness.Log("- 창을 연 채로 en으로 -");

            string 전제목 = 글자("Title");
            string 전탭 = 탭글자();
            string 전요약 = 글자("Summary");
            string 전줄 = 줄글자(PartDiscovery);

            Loc.SetLocale("en");
            yield return null;
            yield return null;

            E2EHarness.Assert(Codex.IsOpen, "창을 닫지 않았다");
            E2EHarness.Log($"  en 제목 \"{글자("Title")}\" / 탭 \"{탭글자()}\" / " +
                           $"요약 \"{글자("Summary")}\" / 줄 \"{줄글자(PartDiscovery)}\"");

            E2EHarness.Assert(글자("Title") != 전제목, "제목이 바뀌었다 (한 번 쓰고 마는 자리)");
            E2EHarness.AssertEqual(글자("Title"), Loc.T("UI", "codex_title"), "en 칸의 값이 섰다");
            E2EHarness.Assert(글자("Caption") != "", "부제가 비지 않았다");
            E2EHarness.Assert(글자("Summary") != 전요약, "요약 줄도 따라 바뀌었다");
            E2EHarness.Assert(글자("Footer") != "", "바닥글이 비지 않았다");

            // 탭의 갈래 이름("물질 분석")은 아직 en에서 바뀌지 않는다. 그 말은
            // Domain/Progression/CodexCatalog.SectionTitle에 있고 다른 갈래의 몫이다.
            // 탭 <b>틀</b>({0}  {1}/{2})은 이미 표에서 나오므로, 그것이 표를 거친다는
            // 증거는 아래 pseudo 단계가 댄다 — 여기서는 사실만 적어 둔다.
            E2EHarness.Log($"  (탭은 아직 그대로: \"{전탭}\" -> \"{탭글자()}\" — " +
                           "갈래 이름은 CodexCatalog에 남아 있다)");

            // 여기가 이 시나리오의 알맹이다. 목록 줄의 이름은 데이터 에셋에서 온다.
            E2EHarness.Assert(줄글자(PartDiscovery) != 전줄,
                "목록 줄의 물질 이름이 en에서 바뀌었다 — 안 바뀌면 그 자리는 " +
                "DataText를 우회해 ItemDataSO.displayName을 직접 읽고 있다");
        }

        static IEnumerator 열린_채로_의사_번역으로_바꾼다()
        {
            E2EHarness.Log("- 창을 연 채로 pseudo로 -");

            Loc.SetLocale(StringCatalog.PseudoLocale);
            yield return null;
            yield return null;

            E2EHarness.Log($"  pseudo 제목 \"{글자("Title")}\" / 부제 \"{글자("Caption")}\" / " +
                           $"바닥글 \"{글자("Footer")}\"");

            E2EHarness.Assert(PseudoLocalizer.IsTransformed(글자("Title")),
                              "제목이 부풀었다 — 부풀지 않으면 아직 코드에 박힌 글자다");
            E2EHarness.Assert(PseudoLocalizer.IsTransformed(글자("Caption")), "부제도 부풀었다");
            E2EHarness.Assert(PseudoLocalizer.IsTransformed(글자("Footer")), "바닥글도 부풀었다");
            E2EHarness.Assert(PseudoLocalizer.IsTransformed(줄글자(PartDiscovery)),
                              $"목록 줄도 부풀었다 — \"{줄글자(PartDiscovery)}\"");

            // 탭도 부푼다. 갈래 이름은 아직 코드에 있지만 <b>줄의 틀</b>은 표에서
            // 나온다는 뜻이다 — en에서 안 바뀌는 것과 이것은 다른 이야기다.
            E2EHarness.Assert(PseudoLocalizer.IsTransformed(탭글자()),
                              $"탭 줄의 틀도 표에서 나온다 — \"{탭글자()}\"");
        }

        static IEnumerator 되돌리면_원래_글자로_돌아온다(string 처음로케일)
        {
            Loc.SetLocale(처음로케일);
            yield return null;
            yield return null;

            E2EHarness.AssertEqual(글자("Title"), Loc.T("UI", "codex_title"),
                                   "되돌리면 원래 글자다 (묵은 글자가 남지 않는다)");
            E2EHarness.Assert(!글자("Title").Contains("—") && !글자("Footer").Contains("—"),
                              "본문 글꼴에 없는 줄표가 화면에 나가지 않는다");
        }

        // ── 화면에 실제로 서 있는 글자를 읽는다 ──────────────────

        static string 글자(string objectName)
        {
            var t = Codex.GetComponentsInChildren<TMP_Text>(true)
                         .FirstOrDefault(x => x.gameObject.name == objectName &&
                                              x.transform.parent != null &&
                                              x.transform.parent.name == "Panel");
            return t != null ? t.text : "";
        }

        static string 탭글자()
        {
            var tab = Codex.GetComponentsInChildren<Button>(true)
                           .FirstOrDefault(b => b.gameObject.name == "Tab_" + CodexSection.Discovery);
            if (tab == null) return "";

            var t = tab.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault();
            return t != null ? t.text : "";
        }

        static string 줄글자(string key)
        {
            var row = Codex.GetComponentsInChildren<Button>(true)
                           .FirstOrDefault(b => b.gameObject.name == "Codex_" + key);
            if (row == null) return "";

            var t = row.GetComponentsInChildren<TMP_Text>(true)
                       .FirstOrDefault(x => x.gameObject.name == "Label");
            return t != null ? t.text : "";
        }
    }
}
