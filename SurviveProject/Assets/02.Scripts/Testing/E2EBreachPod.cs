using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Survive.Building;
using Survive.Items;
using Survive.Localization;
using Survive.Progression;
using Survive.UI;
using Survive.Vehicles;
using Survive.Vitals;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// 스펙 §6 — <b>돌파정은 배치 판정이 있는 탈것이다.</b>
    ///
    /// 세 가지를 이어서 본다.
    /// <list type="number">
    /// <item><b>아무 데나 못 놓는다.</b> 층이 없는 자리와, 층은 있는데 무언가가 덮고 있는
    ///       자리에서 각각 거부된다. 거부 사유는 화면에 뜰 수 있는 문구여야 한다 —
    ///       "왜 안 놓이지"만 알 수 있는 상태가 이 라운드가 막으려는 것이다</item>
    /// <item><b>짙은 구간이 드러난 자리에는 놓인다.</b> 손에서 하나가 빠지고 세계에 하나가 선다</item>
    /// <item><b>놓은 뒤 탄다.</b> 걸어가 [E]를 눌러 타면 진행 원장에 종막이 적히고
    ///       챕터 종료 신호가 울린다</item>
    /// </list>
    ///
    /// <b>왜 층을 런타임에 세우는가.</b> B섬 지하의 배치는 §16(사람의 몫)이고, 씬은
    /// 병합할 수 없는 단일 파일이라 이 라운드에서 건드리지 않았다. 실제로 재어 보면
    /// <b>씬에 짙은 구간이 한 곳도 없다</b> — 아래 <see cref="Prepare"/>가 그것을 단언한다.
    /// 여기서 볼 것은 배치가 아니라 <b>판정</b>이므로 층을 곁에 세운다.
    /// <c>E2EDescent</c>가 같은 이유로 같은 일을 한다.
    /// </summary>
    public static class E2EBreachPod
    {
        const string 돌파정 = "breach_pod";
        const string 하강플래그 = "ch1_descended";

        static Inventory Inv => E2EHarness.Player.Inventory.Inventory;
        static PlayerVitals Vitals => E2EHarness.Player.Vitals;

        static ChapterDirector Director =>
            Object.FindAnyObjectByType<ChapterDirector>(FindObjectsInactive.Exclude);

        static DescentZone _layer;
        static BreachPod _pod;
        static bool _chapterEnded;

        public static IEnumerator FullRun()
        {
            yield return Prepare();

            yield return 층이_없는_자리에서는_거부된다();
            yield return 층을_무언가가_덮고_있으면_거부된다();
            yield return 층이_드러난_자리에는_놓인다();
            yield return 놓인_돌파정에_타면_챕터가_끝난다();

            yield return 치운다();
            E2EHarness.Log("=== 돌파정 배치·탑승 완주 ===");
        }

        // ── 준비 ────────────────────────────────────────────────

        static IEnumerator Prepare()
        {
            yield return E2EHarness.WaitUntil(() => Director != null && Director.Current != null,
                                              "챕터가 시작된다", 8f);

            E2EHarness.Assert(BreachPodService.Instance != null,
                              "돌파정 배치 서비스가 스스로 서 있다");

            // 진짜 지형이 아직 없다는 것을 여기서 못 박는다. 씬에 층이 생기는 날
            // 이 단언이 먼저 걸리고, 그때 이 시나리오는 세운 층 대신 그 층으로 간다.
            E2EHarness.Assert(
                Object.FindAnyObjectByType<DescentZone>(FindObjectsInactive.Include) == null,
                "시작할 때 씬에 짙은 구간이 없다 (§16 배치 전 — 진짜 배치를 기다린다)");

            var db = E2EHarness.Player.Inventory.Database;
            E2EHarness.Assert(db != null && db.GetById(돌파정) != null,
                              $"아이템 정의를 찾았다: {돌파정}");

            // 돌파 설계는 낫이 흘린 유물을 연구해서 얻는다. 원장에 직접 적는 우회로는
            // 쓰지 않는다 — 그 우회를 걷어낸 것이 §6의 완료 조건이다.
            yield return E2ERelicSupply.유물로_진행_설계를_얻는다();
            E2EHarness.Assert(BlueprintGate.Active != null &&
                              BlueprintGate.Active.IsUnlocked("bp_breach_pod"),
                              "유물을 연구해 돌파 설계를 밝혀냈다");

            BreachPodService.ResetCounters();
            BreachPod.ResetCounters();
            DescentZone.ResetCounters();
            _chapterEnded = false;

            비운다();
            Vitals.Health.Modify(Vitals.Health.Max);
            yield return null;

            E2EHarness.AssertEqual(Director.GetFlag(하강플래그), 0, "아직 내려가지 않았다");
        }

        // ── 1. 층이 없는 자리 ───────────────────────────────────

        static IEnumerator 층이_없는_자리에서는_거부된다()
        {
            E2EHarness.Log("— 층이 없는 자리에 놓아 본다 —");

            층을_걷는다();
            준다(1);
            발밑을_본다();
            yield return null;

            var result = BreachPodService.Instance.Evaluate();
            E2EHarness.AssertEqual(result, PlacementResult.NotDenseLayer,
                                   "층이 없는 자리의 판정");

            string 사유 = PlacementCheckText.Describe(result);
            E2EHarness.Assert(!string.IsNullOrWhiteSpace(사유),
                              $"거부 사유가 화면에 뜰 문구를 갖는다: \"{사유}\"");
            E2EHarness.Assert(사유 != PlacementCheckText.Describe(PlacementResult.WrongSurface),
                              "돌파정의 사유가 건축의 「여기엔 놓을 수 없다」와 구별된다");

            int 손에 = Inv.CountOf(돌파정);
            int 놓은것 = BreachPodService.Deploys;

            var pod = BreachPodService.Instance.TryDeploy();
            yield return null;

            E2EHarness.Assert(pod == null, "놓이지 않았다");
            E2EHarness.AssertEqual(BreachPodService.Deploys, 놓은것, "놓은 수가 늘지 않았다");
            E2EHarness.AssertEqual(Inv.CountOf(돌파정), 손에, "손에서 빠지지도 않았다");
        }

        // ── 2. 층은 있는데 덮여 있는 자리 ───────────────────────

        static IEnumerator 층을_무언가가_덮고_있으면_거부된다()
        {
            E2EHarness.Log("— 층 위에 무언가가 얹힌 자리에 놓아 본다 —");

            // 층의 윗면을 발밑보다 한참 아래에 둔다. 수평으로는 층 안이지만
            // 조준이 맞히는 면(지면)은 층의 윗면보다 훨씬 높다 — 덮여 있다는 뜻이다.
            층을_깐다(발바닥() - 5f);
            발밑을_본다();
            yield return null;

            var result = BreachPodService.Instance.Evaluate();
            E2EHarness.AssertEqual(result, PlacementResult.NotDenseLayer,
                                   "덮인 자리의 판정 (층 윗면 " +
                                   $"{_layer.TopY:F2}, 조준면 {발바닥():F2})");

            var pod = BreachPodService.Instance.TryDeploy();
            yield return null;
            E2EHarness.Assert(pod == null, "덮인 자리에는 놓이지 않았다");
        }

        // ── 3. 드러난 자리 ──────────────────────────────────────

        static IEnumerator 층이_드러난_자리에는_놓인다()
        {
            E2EHarness.Log("— 짙은 구간이 드러난 자리에 놓는다 —");

            층을_걷는다();
            발밑을_본다();
            yield return null;

            // 조준이 실제로 맞히는 면의 높이에 층의 윗면을 맞춘다.
            // 그 자리가 곧 「층이 드러난 자리」다.
            float 조준면 = 조준면높이();
            층을_깐다(조준면);
            yield return null;

            int 손에 = Inv.CountOf(돌파정);
            E2EHarness.Assert(손에 > 0, "손에 돌파정이 있다");

            var result = BreachPodService.Instance.Evaluate();
            E2EHarness.AssertEqual(result, PlacementResult.Ok,
                                   $"드러난 자리의 판정 (층 윗면 {_layer.TopY:F2}, 조준면 {조준면:F2})");

            _pod = BreachPodService.Instance.TryDeploy();
            yield return null;

            E2EHarness.Assert(_pod != null, "돌파정이 세계에 섰다");
            if (_pod == null) yield break;

            E2EHarness.AssertEqual(BreachPodService.Deploys, 1, "한 대만 놓였다");
            E2EHarness.AssertEqual(Inv.CountOf(돌파정), 손에 - 1, "손에서 하나가 빠졌다");
            E2EHarness.Assert(_pod.Layer == _layer, "놓인 돌파정이 그 층 위에 있다");
            E2EHarness.Assert(Mathf.Abs(_pod.transform.position.y - _layer.TopY) < 0.01f,
                              $"층의 윗면에 얹혔다 (돌파정 {_pod.transform.position.y:F2}, " +
                              $"윗면 {_layer.TopY:F2})");

            // 그 자리는 이제 차 있다. 두 대째의 판정에 들어가는 값이 이것이다.
            //
            // 「겹친다」는 답 자체는 Domain 검사가 본다 — 여기서 조준으로 재현하려면
            // 놓인 돌파정의 몸이 광선을 먼저 맞혀 「층이 아니다」가 나온다. 그것도 옳은
            // 답이라(건축도 면을 겹침보다 먼저 묻는다) 여기서는 <b>차 있다는 사실</b>이
            // 판정에 들어가는지까지만 본다.
            E2EHarness.Assert(BreachPodService.Occupied(_pod.transform.position),
                              "그 자리가 찼다고 세계가 답한다");
            E2EHarness.AssertEqual(
                BreachPodPlacement.Evaluate(
                    BreachPodSite.OnLayer(_layer.TopY, _layer.TopY, occupied: true), true, true),
                PlacementResult.Blocked,
                "찬 자리에는 두 대째가 서지 않는다");
        }

        // ── 4. 타면 챕터가 끝난다 ───────────────────────────────

        static IEnumerator 놓인_돌파정에_타면_챕터가_끝난다()
        {
            if (_pod == null) yield break;

            E2EHarness.Log("— 걸어가 [E]로 탄다 —");

            var dir = Director;
            E2EHarness.AssertEqual(dir.GetFlag(하강플래그), 0, "타기 전에는 원장이 비어 있다");
            E2EHarness.AssertEqual(_pod.Evaluate(), BoardingResult.Ok, "탈 수 있는 상태다");

            DescentZone.ChapterEnded += OnChapterEnded;

            E2EHarness.LookAt(_pod.transform.position + Vector3.up * 0.9f);
            yield return null;
            yield return null;

            var it = E2EHarness.Player.Interactor;
            yield return E2EHarness.WaitUntil(() => it.Current is BreachPod,
                                              "돌파정이 상호작용 대상으로 잡힌다", 4f);
            E2EHarness.Log("  프롬프트: " + it.Current.InteractionPrompt);
            E2EHarness.AssertEqual(it.Current.InteractionPrompt, Loc.T("Build", "pod_prompt_board"),
                                   "탑승 문구가 번역 표를 거친다");

            yield return E2EHarness.TapKey(Key.E);
            yield return null;

            E2EHarness.Assert(_pod.HasLaunched, "돌파정이 떠났다");
            E2EHarness.AssertEqual(BreachPod.Launches, 1, "한 번만 떠났다");
            E2EHarness.AssertEqual(dir.GetFlag(하강플래그), 1,
                                   "진행 원장에 종막이 적혔다 — 마지막 목표가 이것을 읽는다");
            E2EHarness.AssertEqual(_layer.HasBreached, true, "그 층이 뚫린 것으로 표시된다");

            yield return E2EHarness.WaitUntil(() => _chapterEnded,
                                              "다음 구역이 없어 챕터 종료 신호가 울린다", 8f);

            DescentZone.ChapterEnded -= OnChapterEnded;

            // 두 번 타도 한 번이다.
            E2EHarness.AssertEqual(_pod.Board(), BoardingResult.AlreadyGone, "두 번은 타지지 않는다");
            E2EHarness.AssertEqual(BreachPod.Launches, 1, "종막이 두 번 세어지지 않았다");
        }

        static void OnChapterEnded(DescentZone zone) => _chapterEnded = true;

        // ── 도구 ────────────────────────────────────────────────

        /// <summary>발바닥 높이. 층의 윗면과 견주는 값이 이것이다.</summary>
        static float 발바닥()
        {
            var p = E2EHarness.Player.transform;
            var cc = p.GetComponent<CharacterController>();
            return cc != null ? p.position.y - cc.height * 0.5f + cc.center.y : p.position.y;
        }

        /// <summary>발밑 조금 앞을 본다. 배치 조준이 지면을 맞히게 하려는 것이다.</summary>
        static void 발밑을_본다()
        {
            var p = E2EHarness.Player.transform;
            E2EHarness.LookAt(p.position + p.forward * 1.5f + Vector3.down * 1.6f);
        }

        /// <summary>조준이 실제로 맞히는 면의 높이. 지형은 평평하지 않으므로 재어서 쓴다.</summary>
        static float 조준면높이()
        {
            var eye = E2EHarness.Eye.transform;
            return Physics.Raycast(eye.position, eye.forward, out var hit,
                                   BreachPodService.Reach, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point.y
                : 발바닥();
        }

        static void 준다(int 개수)
        {
            int 있는것 = Inv.CountOf(돌파정);
            if (있는것 >= 개수) return;

            var db = E2EHarness.Player.Inventory.Database;
            var item = db != null ? db.GetById(돌파정) : null;
            if (item != null) Inv.TryAdd(item, 개수 - 있는것);
        }

        static void 비운다()
        {
            int 있는것 = Inv.CountOf(돌파정);
            if (있는것 > 0) Inv.TryRemove(돌파정, 있는것);
        }

        static void 층을_깐다(float 윗면, float 반경 = 25f, float 두께 = 1.5f)
        {
            층을_걷는다();

            var p = E2EHarness.Player.transform.position;
            var go = new GameObject("E2E_BreachLayer");
            go.transform.position = new Vector3(p.x, 윗면, p.z);

            _layer = go.AddComponent<DescentZone>();
            _layer.Setup(반경, 두께, Vector3.zero, 하강플래그);
        }

        static void 층을_걷는다()
        {
            if (_layer != null) Object.Destroy(_layer.gameObject);
            _layer = null;
        }

        static IEnumerator 치운다()
        {
            DescentZone.ChapterEnded -= OnChapterEnded;

            if (_pod != null) Object.Destroy(_pod.gameObject);
            _pod = null;
            층을_걷는다();
            비운다();

            yield return E2EHarness.ReleaseAllKeys();

            // 종막 연출이 화면을 덮은 채로 끝나면 뒤이어 도는 시나리오가 캄캄하게 시작한다.
            var fader = Object.FindAnyObjectByType<ScreenFader>(FindObjectsInactive.Exclude);
            if (fader != null) yield return fader.FadeIn(0.4f);
            yield return null;
        }
    }
}
