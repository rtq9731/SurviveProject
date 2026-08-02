using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Survive.Core;
using Survive.Narrative;
using Survive.Progression;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// B2 — 프롤로그에서 챕터 1로 실제로 넘어가는지 본다.
    ///
    /// 두 씬이 각자 잘 도는 것과, 하나가 다른 하나로 이어지는 것은 다른 문제다.
    /// 자막이 끝나고, 걸어가서 동굴에 닿고, 암전 뒤 챕터 1의 첫 목표가 뜨는
    /// 한 줄기를 통째로 확인한다.
    /// </summary>
    public static class E2EPrologue
    {
        public static IEnumerator FullRun()
        {
            E2EHarness.Assert(SceneManager.GetActiveScene().name == "StartScene",
                              "프롤로그 씬에서 시작한다");

            // ── 자막 ────────────────────────────────────────────
            var director = Object.FindFirstObjectByType<SequenceDirector>(FindObjectsInactive.Exclude);
            E2EHarness.Assert(director != null, "SequenceDirector가 있다");

            yield return E2EHarness.WaitUntil(() => director.IsPlaying, "프롤로그 자막이 시작된다", 8f);
            E2EHarness.Log("  자막 재생 중");

            yield return E2EHarness.WaitUntil(() => !director.IsPlaying, "프롤로그 자막이 끝난다", 90f);
            E2EHarness.Log("  자막 종료");

            // ── 동굴까지 걸어간다 ───────────────────────────────
            var cave = GameObject.Find("CaveEntrance");
            E2EHarness.Assert(cave != null, "동굴 입구가 있다");

            var trigger = cave.GetComponentInChildren<SceneTransitionTrigger>(true);
            E2EHarness.Assert(trigger != null, "동굴에 씬 전환 트리거가 있다");

            var from = E2EHarness.Player.transform.position;
            E2EHarness.Log($"  {from.ToString("F0")} -> {cave.transform.position.ToString("F0")} " +
                           $"({Vector3.Distance(from, cave.transform.position):F0}m)");

            yield return E2EHarness.WalkTo(cave.transform.position, 3.5f, 60f);

            yield return E2EHarness.WaitUntil(() => trigger == null || trigger.Triggered,
                                              "동굴 트리거를 밟았다", 6f);

            // ── 챕터 1로 ────────────────────────────────────────
            yield return E2EHarness.WaitUntil(
                () => SceneManager.GetActiveScene().name == "MainScene",
                "MainScene으로 넘어간다", 25f);
            E2EHarness.Log("  씬 전환 완료");

            // 씬이 바뀌면 서비스가 새로 등록된다. 잡히기까지 몇 프레임 준다.
            ChapterDirector chapter = null;
            yield return E2EHarness.WaitUntil(() =>
            {
                GameServices.TryGet<ChapterDirector>(out chapter);
                if (chapter == null)
                    chapter = Object.FindFirstObjectByType<ChapterDirector>(FindObjectsInactive.Exclude);
                return chapter != null && chapter.Current != null;
            }, "챕터 1의 첫 목표가 뜬다", 15f);

            E2EHarness.Assert(chapter.CurrentIndex == 0, "첫 번째 목표에서 시작한다");
            E2EHarness.Log("  목표[0] " + chapter.Current.displayText);

            // 암전이 걷혔는지. 검은 화면 그대로면 넘어와도 아무것도 보이지 않는다.
            var fader = Object.FindFirstObjectByType<Survive.UI.ScreenFader>(FindObjectsInactive.Exclude);
            if (fader != null)
            {
                var group = fader.GetComponent<CanvasGroup>();
                yield return E2EHarness.WaitUntil(() => group == null || group.alpha < 0.05f,
                                                  "암전이 걷힌다", 8f);
            }
        }
    }
}
