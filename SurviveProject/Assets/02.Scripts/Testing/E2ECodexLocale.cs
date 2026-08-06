using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Survive.Localization;
using Survive.Progression;
using Survive.UI;

namespace Survive.Testing
{
    /// <summary>
    /// 도감이 <b>자기 목소리로</b> 쓰는 글이 로케일을 따라오는가.
    ///
    /// <see cref="E2EDataTextLocale"/>은 에셋에서 온 글(이름·설명)을 본다.
    /// 여기서 보는 것은 그 옆에 붙는 <b>도감 자신의 말</b>이다 — 갈래 이름,
    /// 잠긴 항목의 안내, 관측 보고의 틀. 전부 <see cref="CodexCatalog"/>가 짓는다.
    ///
    /// <b>이 자리가 왜 위험했는가.</b> 그 말들은 <c>const string</c>이었다.
    /// const는 컴파일할 때 값이 박혀 로케일을 영영 따라오지 못하는데, 한국어로
    /// 도는 동안에는 화면이 멀쩡해 보인다. EditMode도 잡을 수 있지만, 진짜
    /// 화면에 선 글자를 읽어야 "표 → 규칙 → 화면"이 끝까지 이어졌다고 말할 수 있다.
    ///
    /// <b>다시 그리기는 여기서 시킨다.</b> <see cref="CodexUI"/>는 원장이 바뀐
    /// 프레임에만 다시 그리고 아직 <c>Loc.LocaleChanged</c>를 듣지 않는다.
    /// 그것은 화면 쪽 몫이라, 여기서는 탭을 다시 눌러 새로 그리게 한 뒤
    /// <b>화면에 실제로 선 글자</b>를 읽는다.
    /// </summary>
    public static class E2ECodexLocale
    {
        static CodexUI Codex => CodexUI.Instance;

        public static IEnumerator FullRun()
        {
            E2EHarness.ClearLog();
            E2EHarness.Log("=== 도감 로케일 ===");

            string 처음로케일 = Loc.CurrentLocale;

            yield return 표가_서_있다();
            yield return 도감을_연다();
            yield return 연_채로_영어로_바꾼다();
            yield return 연_채로_의사_번역으로_바꾼다();
            yield return 관측_보고도_따라온다();
            yield return 되돌린다(처음로케일);

            E2EHarness.Log("=== 도감 로케일 통과 ===");
        }

        // ── 1. 표 ────────────────────────────────────────────────

        static IEnumerator 표가_서_있다()
        {
            yield return E2EHarness.WaitUntil(() => Loc.IsLoaded, "번역 표가 실렸다", 8f);

            E2EHarness.Assert(Loc.Catalog.Problems.Count == 0,
                $"표에 건전성 문제가 없다 ({Loc.Catalog.Problems.Count}건)");

            // 표가 안 실렸으면 아래 검사는 전부 "키 == 키"라 공회전한다.
            E2EHarness.Assert(Loc.T("Codex", "section_discovery") != "section_discovery",
                              "Codex 구역이 표에 있다");
            E2EHarness.Assert(Codex != null, "도감 화면이 스스로 서 있다");
        }

        // ── 2. 열기 ──────────────────────────────────────────────

        static IEnumerator 도감을_연다()
        {
            Codex.Close();
            yield return null;

            yield return E2EHarness.TapKey(Key.J);
            yield return E2EHarness.WaitUntil(() => Codex.IsOpen, "J 키로 도감이 열렸다", 3f);

            Codex.ShowSection(CodexSection.Discovery);
            yield return null;
            yield return null;

            E2EHarness.Assert(Codex.Entries.Count > 0,
                              $"물질 분석 줄이 있다 ({Codex.Entries.Count}개)");

            E2EHarness.Log($"  ko 탭 \"{TabText(CodexSection.Discovery)}\"");
            E2EHarness.Assert(TabText(CodexSection.Discovery)
                                  .StartsWith(Loc.T("Codex", "section_discovery")),
                              "탭 이름이 표에서 나왔다");
        }

        // ── 3. en ────────────────────────────────────────────────

        static IEnumerator 연_채로_영어로_바꾼다()
        {
            E2EHarness.Log("— 창을 연 채로 en으로 —");

            int 잠긴줄 = 잠긴_줄_번호();
            E2EHarness.Assert(잠긴줄 >= 0, "아직 모르는 줄이 하나는 있다");

            Codex.Select(잠긴줄);
            yield return null;

            string 탭ko = TabText(CodexSection.Discovery);
            string 본문ko = Detail();
            E2EHarness.Log($"  ko 본문 \"{본문ko}\"");
            E2EHarness.AssertEqual(본문ko, CodexCatalog.UnknownDiscoveryBody,
                                   "잠긴 줄의 안내가 화면에 그대로 섰다");

            Loc.SetLocale("en");
            yield return 다시_그린다(CodexSection.Discovery, 잠긴줄);

            string 탭en = TabText(CodexSection.Discovery);
            string 본문en = Detail();
            E2EHarness.Log($"  en 탭 \"{탭en}\" / 본문 \"{본문en}\"");

            E2EHarness.Assert(탭en != 탭ko, "탭 이름이 바뀌었다");
            E2EHarness.Assert(본문en != 본문ko, "잠긴 줄의 안내가 바뀌었다 — const로 박혀 있지 않다");
            E2EHarness.AssertEqual(본문en, Loc.T("Codex", "unknown_discovery"),
                                   "화면에 선 글이 en 칸의 값과 같다");
            E2EHarness.Assert(!LocSentenceRules.HasHangul(본문en),
                              $"en으로 바꿨는데 한국어가 남아 있다 — \"{본문en}\"");
        }

        // ── 4. pseudo ────────────────────────────────────────────

        static IEnumerator 연_채로_의사_번역으로_바꾼다()
        {
            E2EHarness.Log("— 창을 연 채로 pseudo로 —");

            int 잠긴줄 = 잠긴_줄_번호();
            Loc.SetLocale(StringCatalog.PseudoLocale);
            yield return 다시_그린다(CodexSection.Discovery, 잠긴줄);

            string 탭 = TabText(CodexSection.Discovery);
            string 본문 = Detail();
            E2EHarness.Log($"  pseudo 탭 \"{탭}\" / 본문 \"{본문}\"");

            E2EHarness.Assert(PseudoLocalizer.IsTransformed(본문),
                "잠긴 줄의 안내가 부풀었다 — 부풀지 않으면 아직 코드에 박힌 문장이다");
            // 탭은 이름 뒤에 "3/5"를 코드에서 이어 붙인다(UI/CodexUI.cs, 아직 화면 쪽 몫).
            // 그래서 탭 문자열 전체가 아니라 그 안에 부푼 이름이 들어 있는지를 본다.
            E2EHarness.Assert(탭.Contains(Loc.T("Codex", "section_discovery")),
                "탭 이름도 함께 부풀었다");
        }

        // ── 5. 관측 보고 ─────────────────────────────────────────

        static IEnumerator 관측_보고도_따라온다()
        {
            E2EHarness.Log("— 개체 관측: 잰 값 줄도 표에서 나온다 —");

            Loc.SetLocale("ko");
            yield return 다시_그린다(CodexSection.Creature, -1);

            int 열린줄 = 열린_줄_번호();
            if (열린줄 < 0)
            {
                // 이 시나리오는 관측 경로를 만들지 않는다. 하나 열어 두고 본다.
                var 첫줄 = Codex.Entries.FirstOrDefault();
                E2EHarness.Assert(첫줄 != null, "생물 목록을 읽었다");
                UnlockService.Instance.Ledger.Unlock(첫줄.Key);

                yield return 다시_그린다(CodexSection.Creature, -1);
                열린줄 = 열린_줄_번호();
            }

            E2EHarness.Assert(열린줄 >= 0, "관측 기록이 열린 줄이 있다");
            Codex.Select(열린줄);
            yield return null;

            string ko = Detail();
            E2EHarness.Log($"  ko 보고 \"{ko.Replace("\n", " / ")}\"");
            E2EHarness.Assert(ko.Split('\n').Length >= 2,
                              "관측 보고는 성질 줄과 잰 값 줄로 나뉘어 선다");

            Loc.SetLocale("en");
            yield return 다시_그린다(CodexSection.Creature, 열린줄);

            string en = Detail();
            E2EHarness.Log($"  en 보고 \"{en.Replace("\n", " / ")}\"");

            E2EHarness.Assert(en != ko, "관측 보고가 로케일을 따라왔다");
            E2EHarness.AssertEqual(en.Split('\n').Length, ko.Split('\n').Length,
                                   "줄 수는 언어와 무관하다 (줄바꿈은 배치다)");
        }

        // ── 6. 되돌리기 ──────────────────────────────────────────

        static IEnumerator 되돌린다(string 처음로케일)
        {
            Loc.SetLocale(처음로케일);
            yield return 다시_그린다(CodexSection.Discovery, -1);

            E2EHarness.Assert(TabText(CodexSection.Discovery)
                                  .StartsWith(Loc.T("Codex", "section_discovery")),
                              "되돌리면 원래 글자다 (묵은 글자가 남지 않는다)");

            Codex.Close();
            yield return null;
        }

        // ── 도우미 ───────────────────────────────────────────────

        /// <summary>
        /// 도감은 아직 <c>Loc.LocaleChanged</c>를 듣지 않는다. 창을 <b>닫지 않고</b>
        /// 탭만 다시 눌러 새로 그리게 한다 — 닫았다 열면 언제나 통과한다.
        /// </summary>
        static IEnumerator 다시_그린다(CodexSection section, int select)
        {
            Codex.ShowSection(section);
            yield return null;
            if (select >= 0 && select < Codex.Entries.Count) Codex.Select(select);
            yield return null;
        }

        static int 잠긴_줄_번호()
        {
            for (int i = 0; i < Codex.Entries.Count; i++)
                if (!Codex.Entries[i].Unlocked) return i;
            return -1;
        }

        static int 열린_줄_번호()
        {
            for (int i = 0; i < Codex.Entries.Count; i++)
                if (Codex.Entries[i].Unlocked) return i;
            return -1;
        }

        static string TabText(CodexSection section)
        {
            var tab = Codex.GetComponentsInChildren<Button>(true)
                           .FirstOrDefault(b => b.gameObject.name == "Tab_" + section);
            if (tab == null) return "";

            var t = tab.GetComponentInChildren<TMP_Text>(true);
            return t != null ? t.text : "";
        }

        static string Detail()
        {
            var t = Codex.GetComponentsInChildren<TMP_Text>(true)
                         .FirstOrDefault(x => x.gameObject.name == "DetailBody");
            return t != null ? t.text : "";
        }
    }
}
