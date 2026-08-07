using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
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
    /// <b>2026-08-07에 재는 것을 바꿨다.</b> 예전에는 <c>GameObject.Find("CaveEntrance")</c>로
    /// 무대의 오브젝트를 이름으로 집어 "그것이 있다"를 단언했다. 그런데 그 무대가
    /// 통째로 폐기됐다 — 새 설정에서 주인공은 스스로 측량해 호수 근방에 내려앉고,
    /// 어디로도 피신하지 않는다 (기획서 §6.1). 폐기된 무대의 오브젝트 이름을
    /// 단언하는 시나리오는 <b>그 무대를 지키는 자물쇠</b>가 된다. 사람이 지형을
    /// 새로 깔면 초록불이던 검사가 이유 없이 빨개지고, 그러면 다음 사람은 무대가
    /// 아니라 검사를 고친다.
    ///
    /// <b>새 프롤로그의 순서는 아직 설계되지 않았다</b>(기획서 §6.1 [미결]). 그래서
    /// 여기서는 <b>지금 참인 것만</b> 잰다.
    /// <list type="number">
    /// <item><b>폐기된 것이 무대에 없다.</b> 지표를 덮던 기상 연출 넷과 그 안에 들어
    ///       있던 산소 감소 지대가 씬에서 빠졌다. 자막이 그것을 말하지 않는 것과
    ///       무대가 그것을 보여 주지 않는 것은 <b>다른 문제</b>여서 따로 잰다 —
    ///       파티클 이름은 라틴 글자라 저장소 훑개(<c>AssetTextScan</c>)가 못 본다.</item>
    /// <item><b>자막이 폐기된 것을 말하지 않는다.</b> 에셋을 읽는 것이 아니라
    ///       <b>자막판에 실제로 뜬 글자</b>를 모아서 본다. 에셋에서 지웠는데
    ///       배선이 옛 줄을 계속 띄우는 상태를 에셋 검사로는 구별할 수 없다.</item>
    /// <item><b>챕터 1로 이어진다.</b> 넘어가는 지점은 <b>부품(<see cref="SceneTransitionTrigger"/>)으로</b>
    ///       찾는다. 이름이 아니라 하는 일로 찾으면, 사람이 그 자리를 어떤 모양으로
    ///       다시 짓든 이 시나리오는 따라간다.</item>
    /// </list>
    ///
    /// <b>무대 자체는 손대지 않았다.</b> 지형과 배치는 사람의 몫이고(스펙 §10),
    /// 씬 전환 지점을 지우면 프롤로그가 막다른 길이 된다 — <c>StartScene</c>은
    /// 빌드 설정의 첫 씬이라 거기서 못 나가면 게임이 시작되지 않는다.
    /// </summary>
    public static class E2EPrologue
    {
        /// <summary>
        /// 자막에 있으면 안 되는 말. <b>조각으로 짓는다</b> — 이 파일도
        /// <c>AssetTextScan.검사범위</c> 안(<c>Assets/02.Scripts</c>)이라, 그대로 적으면
        /// 폐기어 게이트가 이 파일을 물어 저장소가 빨개진다.
        ///
        /// 앞 넷은 <c>Plan/세계관.md</c> §8의 폐기 목록이고, 뒤 셋은 <b>옛 프롤로그에만</b>
        /// 있던 말이다. 뒤 셋을 저장소 전체 게이트에 넣지 않는 이유는 "동굴"과
        /// "대피"가 다른 자리에서는 멀쩡한 말이기 때문이다 — 지하는 폐기된 것이
        /// 아니라 목적지다. 프롤로그 자막이라는 좁은 자리에서만 금지한다.
        /// </summary>
        static readonly string[] 폐기된_무대 =
        {
            "화성" + "인", "모래" + "폭풍", "모래 " + "폭풍",
            "불시" + "착", "격" + "추", "비상 " + "착륙",
            "피난" + "처", "동" + "굴", "대" + "피",
        };

        public static IEnumerator FullRun()
        {
            E2EHarness.Assert(SceneManager.GetActiveScene().name == "StartScene",
                              "프롤로그 씬에서 시작한다");

            // ── ① 폐기된 것이 무대에 없다 ───────────────────────
            무대에서_걷어낸_것을_확인한다();

            // ── ② 자막 ──────────────────────────────────────────
            var director = Object.FindAnyObjectByType<SequenceDirector>(FindObjectsInactive.Exclude);
            E2EHarness.Assert(director != null, "SequenceDirector가 있다");

            var 자막판 = Object.FindAnyObjectByType<SubtitleView>(FindObjectsInactive.Include);
            E2EHarness.Assert(자막판 != null, "자막판이 있다");
            var 글자 = 자막판.GetComponentInChildren<TMP_Text>(true);
            E2EHarness.Assert(글자 != null, "자막판에 글자칸이 있다");

            // 씬이 뜨자마자 자동으로 한 번 돈다. 시나리오를 붙이는 시각에 따라
            // 그것을 몇 줄이나 볼 수 있는지가 달라지므로, <b>끝나기를 기다렸다
            // 처음부터 다시 재생시켜</b> 전수를 본다. 자동 재생분을 주워 담으려 하면
            // 사람이 붙인 시각에 따라 결과가 달라지는 시나리오가 된다.
            yield return E2EHarness.WaitUntil(() => !director.IsPlaying, "첫 자동 재생이 끝난다", 45f);

            var 시퀀스 = 자동_재생될_시퀀스(director);
            E2EHarness.Assert(시퀀스 != null, "프롤로그 시퀀스가 배선되어 있다");
            int 있어야할줄 = 시퀀스.lines.Count(l => l != null && !string.IsNullOrWhiteSpace(l.text));

            director.StartCoroutine(director.Play(시퀀스));
            yield return E2EHarness.WaitUntil(() => director.IsPlaying, "프롤로그 자막이 시작된다", 8f);
            E2EHarness.Log("  자막 재생 중");

            // 화면에 뜬 글을 프레임마다 줍는다. 자막은 한 줄씩 갈아끼우므로
            // 끝나고 나서 보면 마지막 한 줄밖에 남지 않는다.
            var 본것 = new List<string>();
            while (director.IsPlaying)
            {
                string 지금 = 글자.text;
                if (!string.IsNullOrWhiteSpace(지금) && (본것.Count == 0 || 본것[본것.Count - 1] != 지금))
                    본것.Add(지금);
                yield return null;
            }
            E2EHarness.Log("  자막 종료 — " + 본것.Count + "줄");

            E2EHarness.AssertEqual(본것.Count, 있어야할줄,
                                   "자막판에 뜬 줄 수 — 못 주우면 아래 검사가 공회전한다");

            var 걸린것 = 본것
                .SelectMany(줄 => 폐기된_무대
                    .Where(말 => 줄.IndexOf(말, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(말 => $"\"{줄}\" <- {말}"))
                .ToList();
            E2EHarness.Assert(걸린것.Count == 0,
                              "자막이 폐기된 무대를 말하지 않는다" +
                              (걸린것.Count == 0 ? "" : " (걸린 것: " + string.Join(" / ", 걸린것) + ")"));
            foreach (var 줄 in 본것) E2EHarness.Log("    " + 줄);

            // ── ③ 챕터 1로 ──────────────────────────────────────
            var trigger = Object.FindAnyObjectByType<SceneTransitionTrigger>(FindObjectsInactive.Include);
            E2EHarness.Assert(trigger != null, "챕터 1로 넘어가는 지점이 있다");

            var from = E2EHarness.Player.transform.position;
            var 목적지 = trigger.transform.position;
            E2EHarness.Log($"  {from.ToString("F0")} -> {목적지.ToString("F0")} " +
                           $"({Vector3.Distance(from, 목적지):F0}m)");

            yield return E2EHarness.WalkTo(목적지, 3.5f, 60f);

            yield return E2EHarness.WaitUntil(() => trigger == null || trigger.Triggered,
                                              "전환 지점을 밟았다", 6f);

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
                    chapter = Object.FindAnyObjectByType<ChapterDirector>(FindObjectsInactive.Exclude);
                return chapter != null && chapter.Current != null;
            }, "챕터 1의 첫 목표가 뜬다", 15f);

            E2EHarness.Assert(chapter.CurrentIndex == 0, "첫 번째 목표에서 시작한다");
            E2EHarness.Log("  목표[0] " + chapter.Current.displayText);

            // 암전이 걷혔는지. 검은 화면 그대로면 넘어와도 아무것도 보이지 않는다.
            var fader = Object.FindAnyObjectByType<Survive.UI.ScreenFader>(FindObjectsInactive.Exclude);
            if (fader != null)
            {
                var group = fader.GetComponent<CanvasGroup>();
                yield return E2EHarness.WaitUntil(() => group == null || group.alpha < 0.05f,
                                                  "암전이 걷힌다", 8f);
            }
        }

        /// <summary>
        /// <see cref="SequenceDirector"/>가 씬 시작에 재생하는 시퀀스.
        ///
        /// <b>반사로 꺼낸다.</b> 그 필드는 인스펙터 배선용이라 비공개이고, 여기서
        /// 필요한 것은 <b>씬에 실제로 물려 있는 것</b>이지 시나리오가 따로 고른
        /// 에셋이 아니다. 경로로 다시 읽어 오면 배선이 끊어져도 시나리오는 통과한다.
        /// </summary>
        static SequenceSO 자동_재생될_시퀀스(SequenceDirector director) =>
            typeof(SequenceDirector)
                .GetField("playOnStart", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(director) as SequenceSO;

        /// <summary>
        /// 폐기된 기상 연출과 그것이 들고 있던 압박이 무대에서 빠졌는지.
        ///
        /// <b>둘을 함께 본다.</b> 파티클만 지우고 산소 감소 지대를 남기면 화면에는
        /// 아무것도 안 보이는데 플레이어만 숨이 막히고, 반대로 두면 폐기된 기상이
        /// 그대로 보인다. 프롤로그에 산소를 깎는 자리는 지금 하나도 없다 —
        /// 압박을 무엇으로 대신할지는 사람이 정한다 (기획서 §6.1 [미결]).
        /// </summary>
        static void 무대에서_걷어낸_것을_확인한다()
        {
            var 남은것 = Object
                .FindObjectsByType<Transform>(FindObjectsInactive.Include)
                .Where(t => t.name.IndexOf("Dust" + "Storm", System.StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(t => t.name)
                .ToList();
            E2EHarness.Assert(남은것.Count == 0,
                              "폐기된 기상 연출이 무대에 없다" +
                              (남은것.Count == 0 ? "" : " (남은 것: " + string.Join(", ", 남은것) + ")"));

            int 산소지대 = Object
                .FindObjectsByType<OxygenZone>(FindObjectsInactive.Include)
                .Length;
            E2EHarness.Assert(산소지대 == 0,
                              $"프롤로그에 산소를 깎는 지대가 없다 (찾은 것 {산소지대}개)");
        }
    }
}
