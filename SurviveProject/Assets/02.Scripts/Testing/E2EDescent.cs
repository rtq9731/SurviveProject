using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Survive.Building;
using Survive.Crafting;
using Survive.Items;
using Survive.Progression;
using Survive.UI;
using Survive.Vitals;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// 백로그 36 — 챕터 1의 종막. <b>남의 장치를 켜고 떠나는 것이 아니라 뚫고 내려간다.</b>
    ///
    /// 세 가지를 이어서 본다.
    /// <list type="number">
    /// <item><b>잠항구가 없으면 짙은 층은 여전히 즉사다.</b> 종막이 생겼다고 액면이
    ///       순해지면 세계의 규칙이 무너진다 (기획서 §6.2)</item>
    /// <item><b>잠항구를 실제로 만든다.</b> 제작대에 걸고, 시간을 채우고, 회수한다 —
    ///       재료가 빠지고 시간이 흐르는 것을 건너뛰지 않는다</item>
    /// <item><b>걸을 수 있어도 스스로 내려가야 끝난다.</b> 액면 보행 장비와 잠항구를
    ///       둘 다 걸친 상태가 종막의 정상 상태다. 기본은 위를 걷는 것이고,
    ///       하강 키를 눌러야 가라앉는다</item>
    /// </list>
    ///
    /// <b>왜 층을 런타임에 세우는가.</b> 액면섬과 그 아래 층의 배치는 기획서 §8-4의 일이고,
    /// 씬은 병합할 수 없는 단일 파일이라 이번 작업에서 건드리지 않았다. 여기서 볼 것은
    /// 배치가 아니라 "닿았을 때·다 내려갔을 때 무슨 일이 벌어지는가"이므로 층을 곁에 세운다 —
    /// <c>E2EMacroniumContact</c>가 액면을 세우는 것과 같은 이유다.
    /// </summary>
    public static class E2EDescent
    {
        static Inventory Inv => E2EHarness.Player.Inventory.Inventory;
        static PlayerVitals Vitals => E2EHarness.Player.Vitals;

        static CraftingUI UI => Object.FindAnyObjectByType<CraftingUI>(FindObjectsInactive.Include);

        static ChapterDirector Director =>
            Object.FindAnyObjectByType<ChapterDirector>(FindObjectsInactive.Exclude);

        const string 잠항구 = "submersible";
        const string 보행장비 = "surface_walker";
        const string 매크로늄 = "macronium";
        const string 하강플래그 = "ch1_descended";

        static MacroniumSurfaceZone _surface;
        static DescentZone _layer;
        static CraftingBench _bench;
        static bool _chapterEndedFired;

        public static IEnumerator FullRun()
        {
            yield return Prepare();

            yield return 잠항구_없이는_여전히_즉사다();
            yield return 잠항구를_제작한다();
            yield return 걸을_수_있어도_스스로_내려가야_끝난다();

            yield return 치운다();
            E2EHarness.Log("=== 종막 하강 완주 ===");
        }

        // ── 준비 ────────────────────────────────────────────────

        static IEnumerator Prepare()
        {
            yield return E2EHarness.WaitUntil(() => Director != null && Director.Current != null,
                                              "챕터가 시작된다", 8f);

            E2EHarness.Assert(MacroniumContactService.Instance != null, "액면 접촉 서비스가 스스로 서 있다");
            E2EHarness.Assert(RespawnService.Instance != null, "부활 서비스가 스스로 서 있다");
            yield return E2EHarness.WaitUntil(() => UI != null, "제작 UI가 있다", 8f);

            // 씬에 이미 깔려 있으면 아래에서 세운 것과 구별할 수 없다.
            E2EHarness.Assert(
                Object.FindAnyObjectByType<MacroniumSurfaceZone>(FindObjectsInactive.Include) == null,
                "시작할 때 씬에 액면 구역이 없다");
            E2EHarness.Assert(
                Object.FindAnyObjectByType<DescentZone>(FindObjectsInactive.Include) == null,
                "시작할 때 씬에 하강 구역이 없다 (§8-4 배치 전)");

            var db = E2EHarness.Player.Inventory.Database;
            E2EHarness.Assert(db != null, "아이템 데이터베이스가 연결돼 있다");
            if (db == null) yield break;

            E2EHarness.Assert(db.GetById(매크로늄) != null, $"아이템 정의를 찾았다: {매크로늄}");
            E2EHarness.Assert(db.GetById(잠항구) != null, $"아이템 정의를 찾았다: {잠항구}");
            E2EHarness.Assert(db.GetById(보행장비) != null, $"아이템 정의를 찾았다: {보행장비}");

            var 레시피 = 레시피찾기(잠항구);
            E2EHarness.Assert(레시피 != null, "잠항구 레시피가 제작 목록에 있다");
            if (레시피 != null)
            {
                E2EHarness.AssertEqual(레시피.requiredStation, StationType.Bench,
                                       "잠항구는 제작대에서 만든다");
                E2EHarness.Assert(레시피.craftSeconds > 0f,
                                  $"잠항구 제작에 시간이 든다 ({레시피.craftSeconds:F0}초)");
                E2EHarness.Assert(
                    레시피.ingredients != null &&
                    레시피.ingredients.Any(i => i?.item != null && i.item.id == 매크로늄),
                    "잠항구는 매크로늄을 요구한다 — 마지막 섬의 자원이 출구가 된다");
            }

            // 잠항 설계는 연구대의 산출물이고(백로그 38), 그 연구의 소재인 유물은
            // 낫이 순찰하다 흘린다(백로그 39). 38 시점에는 유물이 세계에 없어 원장에
            // 직접 적는 우회로를 지나갔지만, 이제 실제로 떨구므로 그럴 이유가 없다.
            //
            // 종막에 이른 사람이 거쳐 온 길을 그대로 밟는다 — 낫의 영역에서 기다려
            // 유물 둘을 줍고, 거점에 연구대를 세우고, 스크랩을 태워 밝혀낸다.
            // 그 절차 하나하나는 E2ERelicSupply가 본다. 여기서는 그 산출물이
            // 실제로 종막의 열쇠가 되는지만 확인한다.
            yield return E2ERelicSupply.유물로_진행_설계를_얻는다();
            E2EHarness.Assert(BlueprintGate.Active != null &&
                              BlueprintGate.Active.IsUnlocked("bp_submersible") &&
                              BlueprintGate.Active.IsUnlocked("bp_surface_walker"),
                              "낫에게서 주워 온 것으로 잠항·보행 설계를 밝혀냈다");

            DescentZone.ResetCounters();
            _chapterEndedFired = false;

            벗는다();
            Vitals.Health.Modify(Vitals.Health.Max);
            yield return null;

            E2EHarness.Assert(!Vitals.Health.IsEmpty, "산 채로 시작한다");
        }

        static RecipeSO 레시피찾기(string id)
        {
            var book = Resources.FindObjectsOfTypeAll<RecipeBookSO>().FirstOrDefault();
            var pool = book?.recipes != null ? book.recipes : Resources.FindObjectsOfTypeAll<RecipeSO>();
            return pool.FirstOrDefault(r => r != null && r.id == id);
        }

        /// <summary>이동 장비를 전부 내려놓는다. 보유가 곧 장착이므로 지니고 있으면 안 된다.</summary>
        static void 벗는다()
        {
            foreach (var id in new[] { 보행장비, 잠항구 })
            {
                int 있는것 = Inv.CountOf(id);
                if (있는것 > 0) Inv.TryRemove(id, 있는것);
            }
        }

        static void 준다(string id, int 개수)
        {
            if (개수 <= 0) return;
            var db = E2EHarness.Player.Inventory.Database;
            var item = db != null ? db.GetById(id) : null;
            if (item != null) Inv.TryAdd(item, 개수);
        }

        // ── 1. 잠항구 없이 닿으면 여전히 죽는다 ─────────────────

        static IEnumerator 잠항구_없이는_여전히_즉사다()
        {
            E2EHarness.Log("— 잠항구 없이 짙은 층에 닿는다 —");

            벗는다();
            E2EHarness.AssertEqual(Inv.CountOf(잠항구), 0, "잠항구를 지니지 않았다");
            E2EHarness.AssertEqual(Inv.CountOf(보행장비), 0, "액면 보행 장비도 지니지 않았다");

            int 이전치사 = MacroniumContactService.LethalContacts;

            // 발목까지 잠기게 깐다. 발바닥에 딱 맞추면 "닿았다"가 반올림에 걸린다.
            _surface = 액면을_깐다(발바닥() + 0.5f);

            yield return E2EHarness.WaitUntil(() => Vitals.Health.IsEmpty,
                                              "맨몸으로 짙은 층에 닿자 체력이 0이 된다", 5f);

            E2EHarness.AssertEqual(MacroniumContactService.LastOutcome,
                                   MacroniumContactOutcome.Lethal, "액면이 낸 판정");
            E2EHarness.AssertEqual(MacroniumContactService.LethalContacts, 이전치사 + 1,
                                   "액면이 죽인 횟수");

            액면을_걷는다();
            yield return null;

            int 이전부활 = RespawnService.RespawnCount;
            yield return E2EHarness.WaitUntil(() => RespawnService.RespawnCount > 이전부활,
                                              "잠깐 뒤 스스로 되살아난다", 8f);
            yield return null;
            E2EHarness.Assert(!Vitals.Health.IsEmpty, "체력이 돌아왔다");
        }

        // ── 2. 잠항구를 제작한다 ────────────────────────────────

        static IEnumerator 잠항구를_제작한다()
        {
            E2EHarness.Log("— 매크로늄으로 잠항구를 만든다 —");

            var r = 레시피찾기(잠항구);
            if (r == null) yield break;

            // 재료를 넉넉히 채운다. 매크로늄 광맥의 배치는 §8-4의 일이라 아직 캘 곳이 없다 —
            // 여기서 볼 것은 채집이 아니라 "마지막 섬의 자원이 출구의 재료가 되는가"다.
            foreach (var need in r.ingredients)
            {
                if (need?.item == null) continue;
                준다(need.item.id, need.count - Inv.CountOf(need.item.id));
            }

            var 재료전 = r.ingredients.Where(i => i?.item != null)
                                     .Select(i => (i.item.id, Inv.CountOf(i.item.id))).ToArray();
            foreach (var (id, n) in 재료전) E2EHarness.Log($"  재료 {id} {n}");

            _bench = 제작대를_세운다();
            E2EHarness.Assert(_bench != null, "제작대를 세웠다");
            if (_bench == null) yield break;

            int 결과전 = Inv.CountOf(잠항구);

            UI.Open(_bench);
            yield return null;
            yield return null;

            var row = UI.GetComponentsInChildren<Button>(true)
                        .FirstOrDefault(b => b.gameObject.name == "Row_" + 잠항구);
            E2EHarness.Assert(row != null, "잠항구 행이 제작대 목록에 뜬다");
            if (row == null) yield break;

            E2EHarness.Assert(row.interactable, "재료를 모아 잠항구를 걸 수 있다");
            누른다(row);
            yield return null;
            yield return null;

            E2EHarness.AssertEqual(_bench.Work.Queue.Count, 1, "잠항구가 제작대에 걸렸다");
            foreach (var (id, before) in 재료전)
            {
                int need = r.ingredients.First(i => i.item.id == id).count;
                E2EHarness.AssertEqual(Inv.CountOf(id), before - need,
                                       $"{id}가 걸리는 순간 빠졌다");
            }
            E2EHarness.AssertEqual(Inv.CountOf(잠항구), 결과전,
                                   "누르자마자 손에 들어오지는 않는다");

            UI.Close();
            yield return null;

            yield return E2EHarness.WaitUntil(() => _bench.Work.Queue.IsEmpty,
                                              $"제작대가 다 만들었다 ({r.craftSeconds:F0}초)",
                                              r.craftSeconds + 10f);

            E2EHarness.Assert(_bench.Work.HasOutput, "제작대가 들고 기다린다");
            _bench.Interact(E2EHarness.Player);
            yield return null;

            E2EHarness.AssertEqual(Inv.CountOf(잠항구), 결과전 + 1, "잠항구를 손에 넣었다");
        }

        static CraftingBench 제작대를_세운다()
        {
            var catalog = Resources.FindObjectsOfTypeAll<BuildCatalogSO>().FirstOrDefault();
            var def = catalog?.entries?.FirstOrDefault(b => b != null && b.id == "bench");
            if (def?.prefab == null) return null;

            // 배치 절차는 E2EBaseBuilding이 이미 본다. 여기서 보려는 것은 종막이므로
            // 지형 조준에 시나리오를 걸지 않고 곁에 바로 세운다.
            var pos = E2EHarness.Player.transform.position +
                      E2EHarness.Player.transform.forward * 2.2f;
            var go = Object.Instantiate(def.prefab, pos, Quaternion.identity);
            return go.GetComponentInChildren<CraftingBench>(true);
        }

        static void 누른다(Button b) =>
            ExecuteEvents.Execute(b.gameObject, new PointerEventData(EventSystem.current),
                                  ExecuteEvents.pointerClickHandler);

        // ── 3. 스스로 내려가야 끝난다 ───────────────────────────

        static IEnumerator 걸을_수_있어도_스스로_내려가야_끝난다()
        {
            var dir = Director;
            E2EHarness.Assert(dir != null, "ChapterDirector가 있다");
            E2EHarness.AssertEqual(dir.GetFlag(하강플래그), 0, "아직 내려가지 않았다");

            yield return 여기서_내려간다();

            E2EHarness.AssertEqual(dir.GetFlag(하강플래그), 1,
                                   "진행도에 종막 플래그가 선다 — 마지막 목표가 이것을 읽는다");
        }

        /// <summary>
        /// 지금 선 자리에 짙은 층을 세우고 뚫고 내려간다. <b>종막 구간을 지나야 하는
        /// 시나리오가 공유하는 조각</b>이다 — <c>E2EChapter1</c>·<c>E2EWalkthrough</c>의
        /// 마지막 목표가 이것을 부른다.
        ///
        /// <b>왜 씬의 층으로 걸어가지 않는가.</b> 액면섬과 그 아래 층의 배치는 §8-4의 일이라
        /// 아직 씬에 없다. 여기까지 걸어온 것을 확인하는 것은 앞 구간들이 이미 했고,
        /// 종막에서 볼 것은 "무엇을 걸쳤을 때 무슨 일이 벌어지는가"다.
        ///
        /// 부르기 전에 잠항구를 지니고 있어야 한다. 액면 보행 장비는 없으면 채운다 —
        /// 4번 섬에 닿으려면 필요했으므로 종막에 이른 사람은 반드시 지니고 있고(§5.4의 사슬),
        /// 둘 다 걸친 상태가 종막의 정상 상태다.
        /// </summary>
        public static IEnumerator 여기서_내려간다()
        {
            E2EHarness.Log("— 액면 보행 장비와 잠항구를 둘 다 걸치고 액면에 선다 —");

            E2EHarness.Assert(Inv.CountOf(잠항구) > 0, "잠항구를 지녔다");
            if (Inv.CountOf(보행장비) == 0) 준다(보행장비, 1);
            E2EHarness.Assert(Inv.CountOf(보행장비) > 0, "액면 보행 장비를 지녔다");
            Vitals.Health.Modify(Vitals.Health.Max);

            // 지면 위로 액면을 띄우고 그 위로 떨어뜨린다. 발밑에 맞춰 깔면
            // "액면이 받쳤다"와 "그냥 땅을 밟고 서 있다"가 같은 그림이 된다.
            //
            // <b>층의 자리는 실제로 떨어져 보고 정한다.</b> "발밑에서 5m"로 박아 두었더니
            // 제작대 곁의 거대 버섯 갓 위에 내려앉아 층을 끝까지 지나지 못했다
            // (E2EWalkthrough 실패, 실측: 몸이 발밑 4.1m 위 갓에 걸렸다). 이 세계는
            // 갓과 바위가 머리 위를 덮고 있어 "지면"이 곧 "떨어지면 닿는 곳"이 아니다.
            //
            // 그래서 먼저 한 번 띄워 보고 <b>어디에 내려앉는지</b>를 잰다. 그 자리를
            // 기준으로 층을 세우면 무엇이 밑에 깔려 있든 떨어지는 것만으로 층을 지난다.
            액면을_걷는다();
            E2EHarness.Teleport(E2EHarness.Player.transform.position + Vector3.up * 4f);
            yield return null;
            yield return 내려앉기를_기다린다();
            float 착지 = 발바닥();

            const float 층두께 = 1.5f;
            float 액면높이 = 착지 + 2.0f;      // 아랫면 = 착지 + 0.5
            float 떨어뜨릴높이 = 착지 + 2.8f;   // 방금 이 기둥을 통과해 내려왔으므로 비어 있다

            int 이전돌파 = DescentZone.Breaches;
            int 이전하강 = MacroniumContactService.DescentContacts;

            _surface = 액면을_깐다(액면높이);
            _layer = 층을_깐다(액면높이, 층두께);
            E2EHarness.Log($"  내려앉는 자리 {착지:F2}, 액면 {액면높이:F2}, " +
                           $"층 두께 {층두께:F2}m (아랫면 {_layer.BottomY:F2})");

            _chapterEndedFired = false;
            DescentZone.ChapterEnded += OnChapterEnded;

            var 자리 = E2EHarness.Player.transform.position;
            E2EHarness.Teleport(new Vector3(자리.x, 자리.y + (떨어뜨릴높이 - 발바닥()), 자리.z));
            yield return null;

            // ── 기본은 걷는 것이다 ─────────────────────────────
            yield return E2EHarness.WaitUntil(
                () => MacroniumContactService.LastOutcome == MacroniumContactOutcome.Supported,
                "하강 키를 누르지 않으면 액면 위에 선다", 6f);

            E2EHarness.Assert(MacroniumContactService.Instance.IsSupported, "발밑에 디딜 것이 깔린다");

            float t = 0f;
            while (t < 1.2f) { t += Time.deltaTime; yield return null; }

            float 선발 = 발바닥();
            E2EHarness.Log($"  1.2초 뒤 발바닥 {선발:F2}");
            E2EHarness.Assert(Mathf.Abs(선발 - 액면높이) < 0.5f,
                              $"액면 높이에 그대로 서 있다 (발 {선발:F2}, 액면 {액면높이:F2})");
            E2EHarness.AssertEqual(DescentZone.Breaches, 이전돌파,
                                   "가만히 서 있는 것만으로는 끝나지 않는다");
            E2EHarness.Assert(!_chapterEndedFired, "챕터가 저절로 끝나지 않았다");

            // ── 하강 키를 누르면 내려간다 ──────────────────────
            E2EHarness.Log("  하강 키(Ctrl)를 누른다");
            yield return E2EHarness.PressKey(Key.LeftCtrl);

            bool 가라앉은적있다 = false;
            float 눌린시간 = 0f;
            while (눌린시간 < 12f && DescentZone.Breaches == 이전돌파)
            {
                E2EHarness.QueueKeys();
                if (MacroniumContactService.LastOutcome == MacroniumContactOutcome.Descending)
                    가라앉은적있다 = true;
                눌린시간 += Time.deltaTime;
                yield return null;
            }

            E2EHarness.Assert(가라앉은적있다, "하강 키를 누르자 받침이 걷히고 가라앉는다");
            E2EHarness.Assert(MacroniumContactService.DescentContacts > 이전하강,
                              "잠항구를 걸치고 액면을 통과한 것으로 기록된다");
            E2EHarness.AssertEqual(DescentZone.Breaches, 이전돌파 + 1,
                                   $"층을 뚫었다 (하강 {눌린시간:F1}초)");
            E2EHarness.Assert(_layer.HasBreached, "그 층이 뚫린 것으로 표시된다");
            E2EHarness.Assert(발바닥() <= _layer.BottomY + 0.01f,
                              $"발이 층의 아랫면을 지났다 (발 {발바닥():F2}, 아랫면 {_layer.BottomY:F2})");

            yield return E2EHarness.ReleaseKey(Key.LeftCtrl);

            // ── 종막 신호 ──────────────────────────────────────
            yield return E2EHarness.WaitUntil(() => _chapterEndedFired,
                                              "다음 구역이 없어 챕터 종료 신호가 울린다", 8f);

            DescentZone.ChapterEnded -= OnChapterEnded;
            E2EHarness.Assert(!Vitals.Health.IsEmpty, "내려가는 동안 죽지 않았다");

            // 세운 층을 걷어내고 암전을 되돌린다. 종막 연출이 화면을 덮은 채로 끝나면
            // 뒤이어 도는 시나리오가 캄캄한 화면에서 시작한다.
            액면을_걷는다();
            yield return 화면을_되돌린다();
        }

        static void OnChapterEnded(DescentZone zone) => _chapterEndedFired = true;

        static IEnumerator 화면을_되돌린다()
        {
            var fader = Object.FindAnyObjectByType<ScreenFader>(FindObjectsInactive.Exclude);
            if (fader != null) yield return fader.FadeIn(0.4f);
            yield return null;
        }

        // ── 층 깔기·치우기 ──────────────────────────────────────

        /// <summary>발바닥 높이. 액면·층과 견주는 값이 이것이다.</summary>
        static float 발바닥()
        {
            var p = E2EHarness.Player.transform;
            var cc = p.GetComponent<CharacterController>();
            return cc != null ? p.position.y - cc.height * 0.5f + cc.center.y : p.position.y;
        }

        /// <summary>
        /// 몸이 멈출 때까지 기다린다. 무엇 위에 내려앉는지는 콜라이더를 뒤져서 맞히는 것보다
        /// 한 번 떨어뜨려 보는 편이 정확하다 — 거대 버섯 갓처럼 줄기와 갓이 콜라이더
        /// 하나로 묶인 것은 위쪽 훑기로는 걸러지지 않는다(시작 구가 줄기에 겹쳐 거리 0이 된다).
        /// </summary>
        static IEnumerator 내려앉기를_기다린다(float 최대 = 4f)
        {
            float t = 0f, 이전 = float.MaxValue, 멈춘시간 = 0f;
            while (t < 최대)
            {
                float 지금 = 발바닥();
                멈춘시간 = Mathf.Abs(지금 - 이전) < 0.01f ? 멈춘시간 + Time.deltaTime : 0f;
                if (멈춘시간 > 0.35f) yield break;

                이전 = 지금;
                t += Time.deltaTime;
                yield return null;
            }
        }

        static MacroniumSurfaceZone 액면을_깐다(float 액면높이, float 반경 = 25f, float 폭 = 30f)
        {
            var p = E2EHarness.Player.transform.position;
            var go = new GameObject("E2E_MacroniumSurface");
            go.transform.position = new Vector3(p.x, 액면높이, p.z);

            var zone = go.AddComponent<MacroniumSurfaceZone>();
            zone.Setup(반경, 폭);
            return zone;
        }

        static DescentZone 층을_깐다(float 액면높이, float 두께, float 반경 = 25f)
        {
            var p = E2EHarness.Player.transform.position;
            var go = new GameObject("E2E_DescentLayer");
            go.transform.position = new Vector3(p.x, 액면높이, p.z);

            var zone = go.AddComponent<DescentZone>();
            zone.Setup(반경, 두께, Vector3.zero, 하강플래그);
            return zone;
        }

        static void 액면을_걷는다()
        {
            if (_surface != null) Object.Destroy(_surface.gameObject);
            _surface = null;
            if (_layer != null) Object.Destroy(_layer.gameObject);
            _layer = null;
        }

        static IEnumerator 치운다()
        {
            DescentZone.ChapterEnded -= OnChapterEnded;
            액면을_걷는다();

            if (_bench != null) Object.Destroy(_bench.gameObject);
            _bench = null;

            yield return E2EHarness.ReleaseAllKeys();
            yield return 화면을_되돌린다();
        }
    }
}
