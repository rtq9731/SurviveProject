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

            // 의사 번역의 두 번째 목적: 아직 안 옮긴 글자가 눈에 띈다.
            // 목록 줄은 이번 라운드에서 옮기지 않았으므로 부풀지 않는다.
            if (줄글자 != null)
            {
                string 줄후 = 줄글자.text;
                E2EHarness.Log($"  아직 안 옮긴 줄: \"{줄후}\"");
                E2EHarness.Assert(!PseudoLocalizer.IsTransformed(줄후),
                    "옮기지 않은 줄은 부풀지 않는다 — 이것이 무엇이 남았는지를 화면에서 보여 주는 신호다");
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
