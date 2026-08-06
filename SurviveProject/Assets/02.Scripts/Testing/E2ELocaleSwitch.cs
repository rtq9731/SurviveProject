using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using Survive.Crafting;
using Survive.Localization;
using Survive.UI;

namespace Survive.Testing
{
    /// <summary>
    /// 로케일을 바꾸면 <b>이미 열려 있는 화면</b>의 글자가 바뀐다.
    ///
    /// 번역 층에서 조용히 새는 자리는 "한 번 쓰고 마는 글자"다. 목록 줄처럼 매
    /// 프레임 다시 그려지는 것은 저절로 따라오지만, 버튼 캡션은 만들 때 한 번
    /// 쓰이고 아무도 다시 쓰지 않는다. 창을 닫았다 열면 바뀌어 있으므로
    /// 사람 눈으로는 통과한 것처럼 보인다 — 그 차이를 여기서 잡는다.
    ///
    /// 그래서 <b>창을 연 채로</b> 로케일을 바꾸고, 화면에 실제로 서 있는(activeInHierarchy)
    /// 글자 오브젝트를 직접 읽는다.
    /// </summary>
    public static class E2ELocaleSwitch
    {
        static CraftingUI UI =>
            Object.FindAnyObjectByType<CraftingUI>(FindObjectsInactive.Include);

        /// <summary>제작 행의 "최대" 버튼 글자들. 한 번 쓰고 마는 자리의 대표다.</summary>
        static TMP_Text[] MaxCaptions() =>
            Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include)
                  .Where(t => t.transform.parent != null &&
                              t.transform.parent.name == "Max" &&
                              t.transform.parent.parent != null &&
                              t.transform.parent.parent.name.StartsWith("Row_") &&
                              t.gameObject.activeInHierarchy)
                  .ToArray();

        public static IEnumerator FullRun()
        {
            string 처음로케일 = Loc.CurrentLocale;

            yield return 표가_서_있다();
            yield return 창을_연다();
            yield return 열린_채로_의사_번역으로_바꾼다();
            yield return 열린_채로_영어로_바꾼다();
            yield return 되돌리면_원래_글자로_돌아온다(처음로케일);
            yield return 창을_닫는다();

            E2EHarness.Log("=== 로케일 전환 완주 ===");
        }

        static IEnumerator 표가_서_있다()
        {
            yield return E2EHarness.WaitUntil(() => Loc.IsLoaded, "번역 표가 실렸다", 8f);

            E2EHarness.Assert(Loc.Catalog.Problems.Count == 0,
                $"표에 건전성 문제가 없다 ({Loc.Catalog.Problems.Count}건)");
            E2EHarness.Log($"  로케일 {string.Join(", ", Loc.AvailableLocales)} / " +
                           $"이름표 {Loc.Catalog.Keys.Count}개");

            // 표가 안 실렸으면 아래 검사는 전부 "키 == 키"라 공회전한다.
            E2EHarness.Assert(Loc.T("UI", "craft_max_button") != "craft_max_button",
                              "표에서 실제 값이 나온다");
        }

        static IEnumerator 창을_연다()
        {
            yield return E2EHarness.WaitUntil(() => UI != null, "제작 UI가 있다", 8f);
            UI.Open(StationType.None);

            yield return E2EHarness.WaitUntil(() => UI.IsOpen, "제작 창이 열렸다", 5f);
            yield return null;
            yield return null;

            yield return E2EHarness.WaitUntil(() => MaxCaptions().Length > 0,
                                              "화면에 선 제작 행이 있다", 5f);

            var captions = MaxCaptions();
            E2EHarness.Log($"  화면에 선 \"최대\" 버튼 {captions.Length}개, 지금 글자 \"{captions[0].text}\"");
            E2EHarness.AssertEqual(captions[0].text, Loc.T("UI", "craft_max_button"),
                                   "버튼 글자가 표에서 나왔다");
        }

        static IEnumerator 열린_채로_의사_번역으로_바꾼다()
        {
            E2EHarness.Log("— 창을 연 채로 pseudo로 —");

            var captions = MaxCaptions();
            string 전 = captions[0].text;
            var 줄글자 = 목록줄_글자();

            Loc.SetLocale(StringCatalog.PseudoLocale);
            yield return null;
            yield return null;

            captions = MaxCaptions();
            E2EHarness.Assert(captions.Length > 0, "행이 그대로 서 있다");
            string 후 = captions[0].text;

            E2EHarness.Log($"  \"{전}\" -> \"{후}\"");
            E2EHarness.Assert(후 != 전, "창을 닫지 않았는데 글자가 바뀌었다");
            E2EHarness.Assert(PseudoLocalizer.IsTransformed(후), $"의사 번역 꼴이다 — \"{후}\"");
            E2EHarness.Assert(후.Contains(전), "원문이 안쪽에 남아 있다");

            foreach (var c in captions)
                E2EHarness.Assert(PseudoLocalizer.IsTransformed(c.text),
                                  "모든 행의 버튼이 함께 바뀌었다");

            // 목록 줄도 이제 표에서 나온다. 줄 전체가 부풀어야 한다 —
            // 부풀지 않으면 그 자리는 아직 코드에 박힌 문장이라는 신호다.
            if (줄글자 != null)
            {
                string 줄후 = 줄글자.text;
                E2EHarness.Log($"  목록 줄: \"{줄후}\"");
                E2EHarness.Assert(PseudoLocalizer.IsTransformed(줄후),
                    "목록 줄이 통째로 표에서 나온다 — 부풀지 않으면 아직 코드에 박힌 문장이다");
            }
        }

        static IEnumerator 열린_채로_영어로_바꾼다()
        {
            E2EHarness.Log("— 창을 연 채로 en으로 —");

            Loc.SetLocale("en");
            yield return null;
            yield return null;

            var captions = MaxCaptions();
            E2EHarness.Assert(captions.Length > 0, "행이 그대로 서 있다");
            E2EHarness.Log($"  en: \"{captions[0].text}\"");
            E2EHarness.AssertEqual(captions[0].text, "Max", "en 칸의 값이 화면에 나왔다");

            yield return 어순이_뒤집혔다();
        }

        /// <summary>
        /// <b>어순이 실제로 뒤집혀 화면에 섰는가.</b>
        ///
        /// 표의 en 열은 재료 한 항목을 "가진수/드는수 이름" 순서로 적어 두었다.
        /// 한국어는 "이름 가진수/드는수"다. 코드는 한 줄도 다르지 않으므로,
        /// 화면 문자열의 앞뒤가 바뀌었다면 그것은 표가 순서를 바꾼 것이다.
        /// </summary>
        static IEnumerator 어순이_뒤집혔다()
        {
            var 줄 = 목록줄_글자();
            if (줄 == null)
            {
                E2EHarness.Log("  (목록 줄이 없어 어순 검사를 건너뛴다)");
                yield break;
            }

            string en줄 = 줄.text;
            E2EHarness.Log($"  en 목록 줄: \"{en줄}\"");

            Loc.SetLocale("ko");
            yield return null;
            yield return null;

            줄 = 목록줄_글자();
            string ko줄 = 줄 != null ? 줄.text : "";
            E2EHarness.Log($"  ko 목록 줄: \"{ko줄}\"");

            E2EHarness.Assert(en줄 != ko줄, "같은 줄이 로케일에 따라 다르게 적힌다");

            // 표에 적힌 en 항목 틀이 실제로 다른 순서인지 여기서 확인한다.
            // 화면 문자열만 비교하면 값이 달라서 다른 것인지 순서가 달라서 다른 것인지 모른다.
            Loc.SetLocale("en");
            yield return null;
            string en항목 = Loc.F("UI", "ingredient_entry", "Wood", 2, 5);
            Loc.SetLocale("ko");
            yield return null;
            string ko항목 = Loc.F("UI", "ingredient_entry", "Wood", 2, 5);

            E2EHarness.Log($"  같은 값으로: ko \"{ko항목}\" / en \"{en항목}\"");
            E2EHarness.Assert(ko항목.StartsWith("Wood"), "ko는 이름이 앞이다");
            E2EHarness.Assert(en항목.StartsWith("2/5"), "en은 수가 앞이다 — 자리가 통째로 뒤집혔다");

            Loc.SetLocale("en");
            yield return null;
        }

        static IEnumerator 되돌리면_원래_글자로_돌아온다(string 처음로케일)
        {
            Loc.SetLocale(처음로케일);
            yield return null;
            yield return null;

            var captions = MaxCaptions();
            E2EHarness.Assert(captions.Length > 0, "행이 그대로 서 있다");
            E2EHarness.AssertEqual(captions[0].text, Loc.T("UI", "craft_max_button"),
                                   "되돌리면 원래 글자다 (묵은 글자가 남지 않는다)");
        }

        static IEnumerator 창을_닫는다()
        {
            UI.Close();
            yield return null;
        }

        /// <summary>제작 행의 본문 줄. 아직 번역 표로 옮기지 않은 자리의 대표다.</summary>
        static TMP_Text 목록줄_글자() =>
            Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include)
                  .FirstOrDefault(t => t.name == "Label" &&
                                       t.transform.parent != null &&
                                       t.transform.parent.name.StartsWith("Row_") &&
                                       t.gameObject.activeInHierarchy &&
                                       !string.IsNullOrEmpty(t.text));
    }
}
