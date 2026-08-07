using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Survive.Localization;
using Survive.Narrative;

namespace Survive.Testing
{
    /// <summary>
    /// <b>프롤로그 자막이 로케일을 따라오는가.</b> 화면에 실제로 뜬 글자를 읽어서 본다.
    ///
    /// <b>왜 화면을 읽는가.</b> <see cref="DataText.Line(SequenceSO,int)"/>가 옳은 값을
    /// 내는지는 EditMode(<c>SequenceSubtitleLocTests</c>)가 이미 잰다. 여기서만 잴 수
    /// 있는 것은 <b>그 값이 자막판까지 닿는가</b>다 — 2026-08-07 전까지 조회 함수는
    /// 멀쩡히 있었고 <see cref="SequenceDirector"/>가 그것을 부르지 않았을 뿐이다.
    /// 조회를 검사하는 것으로는 그 구멍을 못 본다.
    ///
    /// <b>세 로케일을 각각 처음부터 재생한다.</b> 자막은 한 줄을 띄우고 몇 초 뒤
    /// 갈아끼우는 물건이라, 재생 도중에 로케일을 바꾸면 <b>이미 뜬 줄</b>은 바뀌지
    /// 않는다(그 줄은 다시 그려지지 않는다). 그것을 요구하면 자막판이 줄의 열쇠를
    /// 기억하고 있어야 하는데, 3초 뒤 사라질 글자를 위한 장치로는 과하다.
    /// 대신 <b>다음 줄부터는 새 로케일</b>이라는 것이 계약이고, 처음부터 다시
    /// 재생하면 전수를 그 계약대로 볼 수 있다.
    /// </summary>
    public static class E2ESequenceLocale
    {
        public static IEnumerator FullRun()
        {
            E2EHarness.ClearLog();
            E2EHarness.Log("=== 연출 자막 로케일 ===");

            E2EHarness.Assert(SceneManager.GetActiveScene().name == "StartScene",
                              "프롤로그 씬에서 시작한다");
            yield return E2EHarness.WaitUntil(() => Loc.IsLoaded, "번역 표가 실렸다", 8f);

            var director = Object.FindAnyObjectByType<SequenceDirector>(FindObjectsInactive.Exclude);
            E2EHarness.Assert(director != null, "SequenceDirector가 있다");

            var 자막판 = Object.FindAnyObjectByType<SubtitleView>(FindObjectsInactive.Include);
            E2EHarness.Assert(자막판 != null, "자막판이 있다");
            var 글자 = 자막판.GetComponentInChildren<TMP_Text>(true);
            E2EHarness.Assert(글자 != null, "자막판에 글자칸이 있다");

            var 시퀀스 = 자동_재생될_시퀀스(director);
            E2EHarness.Assert(시퀀스 != null, "프롤로그 시퀀스가 배선되어 있다");

            string 처음로케일 = Loc.CurrentLocale;

            // 씬이 뜨자마자 한 번 자동으로 돈다. 끼어들면 두 재생이 같은 자막판을
            // 서로 덮어쓰므로 끝나기를 기다린다. startDelay(0.5초)보다 넉넉히 준 뒤
            // 재생 중인지를 보는 것은, 아직 시작도 안 한 순간에 "안 돌고 있다"로
            // 읽고 지나가는 일을 막기 위해서다.
            yield return new WaitForSeconds(1.5f);
            yield return E2EHarness.WaitUntil(() => !director.IsPlaying, "첫 자동 재생이 끝난다", 45f);

            var ko = new List<string>();
            var en = new List<string>();
            var 의사 = new List<string>();

            yield return 재생해_화면을_읽는다(director, 시퀀스, 글자, StringCatalog.DefaultLocale, ko);
            yield return 재생해_화면을_읽는다(director, 시퀀스, 글자, "en", en);
            yield return 재생해_화면을_읽는다(director, 시퀀스, 글자, StringCatalog.PseudoLocale, 의사);

            // ── 판정 ────────────────────────────────────────────
            E2EHarness.Assert(ko.Count > 0, "ko에서 자막을 주웠다");
            E2EHarness.AssertEqual(en.Count, ko.Count, "en 자막 줄 수가 ko와 같다");
            E2EHarness.AssertEqual(의사.Count, ko.Count, "pseudo 자막 줄 수가 ko와 같다");

            for (int i = 0; i < ko.Count; i++)
            {
                E2EHarness.Assert(en[i] != ko[i],
                    $"{i + 1}번째 자막이 en에서 바뀌었다 — 안 바뀌면 그 자리는 " +
                    $"표를 우회해 에셋을 직접 띄우고 있다 (\"{ko[i]}\")");
                E2EHarness.Assert(PseudoLocalizer.IsTransformed(의사[i]),
                    $"{i + 1}번째 자막이 pseudo에서 부풀었다 — \"{의사[i]}\"");
            }

            // 되돌린다. 다음 시나리오가 en으로 시작하면 엉뚱한 곳에서 실패한다.
            Loc.SetLocale(처음로케일);
            yield return null;

            E2EHarness.Log("=== 연출 자막 로케일 통과 ===");
        }

        /// <summary>
        /// 로케일을 세우고 시퀀스를 처음부터 재생하면서, <b>자막판에 실제로 뜬 글자</b>를
        /// 프레임마다 줍는다. 자막은 한 줄씩 갈아끼우므로 끝나고 나서 보면 마지막
        /// 한 줄밖에 남지 않는다.
        /// </summary>
        static IEnumerator 재생해_화면을_읽는다(SequenceDirector director, SequenceSO 시퀀스,
                                              TMP_Text 글자, string 로케일, List<string> 담을곳)
        {
            Loc.SetLocale(로케일);
            yield return null;

            담을곳.Clear();
            director.StartCoroutine(director.Play(시퀀스));
            yield return E2EHarness.WaitUntil(() => director.IsPlaying,
                                              $"{로케일} 자막이 시작된다", 8f);

            while (director.IsPlaying)
            {
                string 지금 = 글자.text;
                if (!string.IsNullOrWhiteSpace(지금) &&
                    (담을곳.Count == 0 || 담을곳[담을곳.Count - 1] != 지금))
                    담을곳.Add(지금);
                yield return null;
            }

            E2EHarness.Log($"  [{로케일}] {담을곳.Count}줄");
            foreach (var 줄 in 담을곳) E2EHarness.Log("    " + 줄);
        }

        /// <summary>
        /// 씬에 실제로 물려 있는 시퀀스. 경로로 다시 읽어 오면 배선이 끊어져도
        /// 시나리오가 통과한다 — <c>E2EPrologue</c>가 같은 이유로 같은 수를 쓴다.
        /// </summary>
        static SequenceSO 자동_재생될_시퀀스(SequenceDirector director) =>
            typeof(SequenceDirector)
                .GetField("playOnStart", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(director) as SequenceSO;
    }
}
