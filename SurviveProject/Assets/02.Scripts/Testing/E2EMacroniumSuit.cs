using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Survive.Building;
using Survive.Crafting;
using Survive.Interaction;
using Survive.Items;
using Survive.Progression;
using Survive.UI;
using Survive.Vitals;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// 실행 스펙 §8-1 — 매크로늄 방호복. <b>물속으로 들어가는 관문.</b>
    ///
    /// 넷을 이어서 본다.
    /// <list type="number">
    /// <item><b>설계는 연구대가 아니라 무광버섯이 연다.</b> 티어 3은 재료 기반이므로
    ///       (기획서 갱신점 _3 §2) 실제로 하나 주워서 열리는지를 본다 —
    ///       원장에 직접 적어 두면 그 채널이 끊겨 있어도 초록불이 켜진다</item>
    /// <item><b>방호복이 없으면 통로가 밀어낸다. 죽이지 않는다.</b>
    ///       위협 계층 원칙 — 환경은 죽이지 않고 생물만 죽인다. 여기서 체력이
    ///       한 톨이라도 깎이면 그 원칙이 무너진 것이다</item>
    /// <item><b>방호복을 실제로 만든다.</b> 제작대에 걸고, 시간을 채우고, 회수한다</item>
    /// <item><b>걸치면 들어가고, 그때부터 숨이 방호복 속도로 준다.</b>
    ///       맨몸일 때보다 느리게 주는 것까지 재야 장비가 값어치를 한 것이다</item>
    /// </list>
    ///
    /// <b>왜 통로를 런타임에 세우는가.</b> B섬 지하로 가는 통로의 배치는 사람의 몫이라
    /// (실행 스펙 §9) 아직 씬에 없다. 여기서 볼 것은 배치가 아니라 "무엇을 걸쳤을 때
    /// 무슨 일이 벌어지는가"이므로 통로를 곁에 세운다 — <c>E2EDescent</c>가 층을
    /// 세우는 것과 같은 이유다. 몇 미터짜리로 파야 하는지는
    /// <see cref="DiveRule.FirstDivePassageMeters"/>가 답하고 EditMode가 지킨다.
    /// </summary>
    public static class E2EMacroniumSuit
    {
        static Inventory Inv => E2EHarness.Player.Inventory.Inventory;
        static PlayerVitals Vitals => E2EHarness.Player.Vitals;

        static CraftingUI UI => Object.FindAnyObjectByType<CraftingUI>(FindObjectsInactive.Include);

        static ChapterDirector Director =>
            Object.FindAnyObjectByType<ChapterDirector>(FindObjectsInactive.Exclude);

        const string 방호복 = "macronium_suit";
        const string 무광버섯 = "matte_mushroom";
        const string 설계 = "bp_macronium_suit";

        static DiveZone _통로;
        static CraftingBench _제작대;

        /// <summary>맨몸으로 막혔던 자리. 방호복을 걸치고 같은 높이에 다시 선다.</summary>
        static float _입구높이;
        static float _지면;
        static readonly List<GameObject> _세운것 = new List<GameObject>();

        public static IEnumerator FullRun()
        {
            yield return 준비();

            yield return 무광버섯이_설계를_연다();
            yield return 방호복_없이는_통로가_밀어낸다();
            yield return 방호복을_제작한다();
            yield return 방호복을_걸치면_들어간다();

            yield return 치운다();
            E2EHarness.Log("=== 매크로늄 방호복 완주 ===");
        }

        // ── 준비 ────────────────────────────────────────────────

        static IEnumerator 준비()
        {
            yield return E2EHarness.WaitUntil(() => Director != null && Director.Current != null,
                                              "챕터가 시작된다", 8f);

            E2EHarness.Assert(DiveGateService.Instance != null, "잠수 문지기가 스스로 서 있다");
            yield return E2EHarness.WaitUntil(() => UI != null, "제작 UI가 있다", 8f);

            // 씬에 이미 깔려 있으면 아래에서 세운 것과 구별할 수 없다.
            E2EHarness.Assert(
                Object.FindAnyObjectByType<DiveZone>(FindObjectsInactive.Include) == null,
                "시작할 때 씬에 잠수 통로가 없다 (배치 전)");

            var db = E2EHarness.Player.Inventory.Database;
            E2EHarness.Assert(db != null, "아이템 데이터베이스가 연결돼 있다");
            if (db == null) yield break;

            E2EHarness.Assert(db.GetById(무광버섯) != null, $"아이템 정의를 찾았다: {무광버섯}");
            E2EHarness.Assert(db.GetById(방호복) != null, $"아이템 정의를 찾았다: {방호복}");

            var r = 레시피찾기(방호복);
            E2EHarness.Assert(r != null, "방호복 레시피가 제작 목록에 있다");
            if (r != null)
            {
                E2EHarness.AssertEqual(r.requiredStation, StationType.Bench, "방호복은 제작대에서 만든다");
                E2EHarness.Assert(r.craftSeconds > 0f, $"방호복 제작에 시간이 든다 ({r.craftSeconds:F0}초)");
                E2EHarness.Assert(
                    r.ingredients != null &&
                    r.ingredients.Any(i => i?.item != null && i.item.id == 무광버섯),
                    "방호복은 무광버섯을 요구한다 — B섬 지상의 재료가 지하로 가는 문이 된다");
                E2EHarness.Assert(r.requiredBlueprint != null && r.requiredBlueprint.id == 설계,
                                  "방호복 레시피가 차폐 설계에 물려 있다");
            }

            // 야생 생물이 무대 위를 지나가면 조준선이 그쪽을 먼저 잡는다.
            E2EHarness.Log($"  야생 생물 {E2EHarness.SleepWildCreatures()}마리를 재웠다");

            DiveGateService.ResetCounters();
            _세운것.Clear();

            벗는다();
            비운다(무광버섯);
            Vitals.Health.Modify(Vitals.Health.Max);
            Vitals.Oxygen.Modify(Vitals.Oxygen.Max);
            yield return null;

            E2EHarness.Assert(!Vitals.Health.IsEmpty, "산 채로 시작한다");
        }

        static RecipeSO 레시피찾기(string id)
        {
            var book = Resources.FindObjectsOfTypeAll<RecipeBookSO>().FirstOrDefault();
            var pool = book?.recipes != null ? book.recipes : Resources.FindObjectsOfTypeAll<RecipeSO>();
            return pool.FirstOrDefault(r => r != null && r.id == id);
        }

        /// <summary>이동 장비를 내려놓는다. 보유가 곧 장착이므로 지니고 있으면 안 된다.</summary>
        static void 벗는다()
        {
            int n = Inv.CountOf(방호복);
            if (n > 0) Inv.TryRemove(방호복, n);
        }

        static void 비운다(string id)
        {
            int n = Inv.CountOf(id);
            if (n > 0) Inv.TryRemove(id, n);
        }

        static void 준다(string id, int 개수)
        {
            if (개수 <= 0) return;
            var db = E2EHarness.Player.Inventory.Database;
            var item = db != null ? db.GetById(id) : null;
            if (item != null) Inv.TryAdd(item, 개수);
        }

        // ── 1. 재료가 설계를 연다 ───────────────────────────────

        static IEnumerator 무광버섯이_설계를_연다()
        {
            E2EHarness.Log("— 무광버섯을 하나 주워 본다 —");

            var ledger = BlueprintGate.Active;
            E2EHarness.Assert(ledger != null, "해금 원장이 있다");
            if (ledger == null) yield break;

            // 이미 열려 있으면(앞 시나리오가 주웠으면) 채널을 볼 수 없다. 원장을 되돌린다.
            // 되잠그는 창구를 도메인에 새로 내지 않는다 — 저장·복원 길이 이미 있고,
            // 게임에 없는 조작을 위해 규칙 쪽에 구멍을 뚫는 것은 검증이 할 일이 아니다.
            if (ledger.IsUnlocked(설계))
            {
                var book = Resources.FindObjectsOfTypeAll<DiscoveryBookSO>().FirstOrDefault();
                var d = book != null ? book.Find(무광버섯) : null;
                var 지울것 = new HashSet<string> { 설계 };
                if (d != null) 지울것.Add(FieldDiscovery.KeyOf(d));

                var state = ledger.Capture();
                state.keys.RemoveAll(지울것.Contains);
                ledger.Restore(state);
                E2EHarness.Log("  앞 판이 열어 둔 설계를 되잠갔다");
            }

            E2EHarness.Assert(!ledger.IsUnlocked(설계), "아직 차폐 설계를 모른다");

            var db = E2EHarness.Player.Inventory.Database;
            var item = db.GetById(무광버섯);

            var cam = E2EHarness.Eye;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "E2E_MatteMushroomDrop";
            go.transform.localScale = Vector3.one * 0.4f;
            go.transform.position = cam.transform.position + cam.transform.forward * 2.2f;
            _세운것.Add(go);

            go.AddComponent<ItemPickup>().Setup(item, 1);

            yield return null;
            E2EHarness.LookAt(go.transform.position);
            yield return null;
            yield return null;

            var it = E2EHarness.Player.Interactor;
            yield return E2EHarness.WaitUntil(() => it.Current != null, "무광버섯이 탐지된다", 4f);

            yield return E2EHarness.TapKey(Key.E);
            yield return E2EHarness.WaitUntil(() => Inv.CountOf(무광버섯) > 0, "무광버섯을 주웠다", 4f);
            E2EHarness.Assert(Inv.CountOf(무광버섯) > 0, "무광버섯이 가방에 들어왔다");

            yield return E2EHarness.WaitUntil(() => ledger.IsUnlocked(설계),
                                              "쥐는 순간 차폐 설계가 열린다", 4f);
            E2EHarness.Assert(ledger.IsUnlocked(설계),
                              "쥐는 순간 차폐 설계가 열린다 (연구대를 거치지 않는다)");
        }

        // ── 2. 방호복 없이 들어가려 하면 밀려난다 ───────────────

        static IEnumerator 방호복_없이는_통로가_밀어낸다()
        {
            E2EHarness.Log("— 맨몸으로 잠수 통로에 들어가 본다 —");

            벗는다();
            E2EHarness.AssertEqual(Inv.CountOf(방호복), 0, "방호복을 지니지 않았다");

            Vitals.Health.Modify(Vitals.Health.Max);
            float 체력전 = Vitals.Health.Current;
            int 이전거절 = DiveGateService.RefusedEntries;

            // 발밑에 통로 입구를 깐다. 발바닥에 딱 맞추면 "닿았다"가 반올림에 걸린다.
            // 이 높이를 기억해 둔다. 뒤에서 방호복을 걸치고 같은 자리에 다시 서야
            // "무엇이 달라졌는가"가 장비 하나로 좁혀진다.
            _지면 = 발바닥();
            _입구높이 = _지면 + 0.5f;
            _통로 = 통로를_깐다(_입구높이);
            E2EHarness.Log($"  지면 {_지면:F2}, 통로 입구 {_입구높이:F2}, " +
                           $"길이 {_통로.Magnitude:F1}초 / {_통로.PassageMeters:F1}m");

            yield return E2EHarness.WaitUntil(
                () => DiveGateService.LastOutcome == DiveOutcome.NoSuit,
                "통로가 방호복이 없다고 답한다", 6f);

            E2EHarness.Assert(DiveGateService.RefusedEntries > 이전거절,
                              "되돌려 보낸 것으로 기록된다");
            E2EHarness.Assert(DiveGateService.Instance.IsBlocked, "발밑이 막힌다");

            // 잠깐 버텨 본다. 밀어내는 것이지 죽이는 것이 아니다.
            float t = 0f;
            while (t < 2.5f) { t += Time.deltaTime; yield return null; }

            E2EHarness.Assert(!Vitals.Health.IsEmpty, "맨몸으로 통로에 붙어 있어도 죽지 않는다");
            E2EHarness.AssertEqual(Mathf.RoundToInt(Vitals.Health.Current), Mathf.RoundToInt(체력전),
                                   "체력이 한 톨도 깎이지 않는다 (환경은 죽이지 않는다)");
            E2EHarness.AssertEqual(DiveGateService.SealedEntries, 0, "들어간 것으로 세지 않았다");

            // 물이 받쳐 올려 입구 위에 떠 있다. 발이 지면보다 높아진 것이 그 증거다.
            E2EHarness.Assert(발바닥() >= _입구높이 - 0.05f,
                              $"물이 입구 위로 밀어 올렸다 (발 {발바닥():F2}, 입구 {_입구높이:F2}, " +
                              $"지면 {_지면:F2})");

            통로를_걷는다();
            yield return null;
        }

        // ── 3. 방호복을 만든다 ──────────────────────────────────

        static IEnumerator 방호복을_제작한다()
        {
            E2EHarness.Log("— 무광버섯으로 방호복을 만든다 —");

            var r = 레시피찾기(방호복);
            if (r == null) yield break;

            // 재료를 채운다. B섬 지형이 아직 없어 무광버섯 군락을 다 캘 수는 없다 —
            // 캐는 절차 자체는 E2ENewMaterials가 이미 본다.
            foreach (var need in r.ingredients)
            {
                if (need?.item == null) continue;
                준다(need.item.id, need.count - Inv.CountOf(need.item.id));
            }

            var 재료전 = r.ingredients.Where(i => i?.item != null)
                                     .Select(i => (i.item.id, Inv.CountOf(i.item.id))).ToArray();
            foreach (var (id, n) in 재료전) E2EHarness.Log($"  재료 {id} {n}");

            _제작대 = 제작대를_세운다();
            E2EHarness.Assert(_제작대 != null, "제작대를 세웠다");
            if (_제작대 == null) yield break;

            int 결과전 = Inv.CountOf(방호복);

            UI.Open(_제작대);
            yield return null;
            yield return null;

            var row = UI.GetComponentsInChildren<Button>(true)
                        .FirstOrDefault(b => b.gameObject.name == "Row_" + 방호복);
            E2EHarness.Assert(row != null, "방호복 행이 제작대 목록에 뜬다 (설계를 알고 있다)");
            if (row == null) yield break;

            E2EHarness.Assert(row.interactable, "재료를 모아 방호복을 걸 수 있다");
            누른다(row);
            yield return null;
            yield return null;

            E2EHarness.AssertEqual(_제작대.Work.Queue.Count, 1, "방호복이 제작대에 걸렸다");
            foreach (var (id, before) in 재료전)
            {
                int need = r.ingredients.First(i => i.item.id == id).count;
                E2EHarness.AssertEqual(Inv.CountOf(id), before - need, $"{id}가 걸리는 순간 빠졌다");
            }

            UI.Close();
            yield return null;

            yield return E2EHarness.WaitUntil(() => _제작대.Work.Queue.IsEmpty,
                                              $"제작대가 다 만들었다 ({r.craftSeconds:F0}초)",
                                              r.craftSeconds + 10f);

            E2EHarness.Assert(_제작대.Work.HasOutput, "제작대가 들고 기다린다");
            _제작대.Interact(E2EHarness.Player);
            yield return null;

            E2EHarness.AssertEqual(Inv.CountOf(방호복), 결과전 + 1, "방호복을 손에 넣었다");
        }

        // ── 4. 걸치면 들어간다 ──────────────────────────────────

        static IEnumerator 방호복을_걸치면_들어간다()
        {
            E2EHarness.Log("— 같은 자리에 방호복을 걸치고 선다 —");

            E2EHarness.Assert(Inv.CountOf(방호복) > 0, "방호복을 지녔다");
            Vitals.Health.Modify(Vitals.Health.Max);
            Vitals.Oxygen.Modify(Vitals.Oxygen.Max);

            int 이전진입 = DiveGateService.SealedEntries;
            int 이전거절 = DiveGateService.RefusedEntries;

            // 앞 판에서 물이 밀어 올려 세워 두었던 바로 그 자리에서 시작한다.
            // 같은 높이에 같은 통로를 깔고 장비 하나만 바꾼다.
            var 자리 = E2EHarness.Player.transform.position;
            E2EHarness.Teleport(new Vector3(자리.x, 자리.y + (_입구높이 + 0.1f - 발바닥()), 자리.z));
            yield return null;

            _통로 = 통로를_깐다(_입구높이);
            E2EHarness.Log($"  발 {발바닥():F2}에서 시작, 통로 입구 {_입구높이:F2}");

            yield return E2EHarness.WaitUntil(
                () => DiveGateService.LastOutcome == DiveOutcome.Sealed,
                "같은 통로가 이번에는 몸을 봉하고 받아들인다", 6f);

            E2EHarness.Assert(DiveGateService.LastOutcome == DiveOutcome.Sealed,
                              "같은 통로가 이번에는 몸을 봉하고 받아들인다");
            E2EHarness.Assert(!DiveGateService.Instance.IsBlocked, "발밑을 막지 않는다");
            E2EHarness.AssertEqual(DiveGateService.RefusedEntries, 이전거절, "거절 횟수가 늘지 않았다");

            // 받침이 걷혔으므로 내려간다. 스스로 가라앉는 데 쓰는 그 키(Ctrl)를 그대로 쓴다.
            yield return E2EHarness.PressKey(Key.LeftCtrl);

            float t = 0f;
            while (t < 6f && DiveGateService.SealedEntries == 이전진입)
            {
                E2EHarness.QueueKeys();
                t += Time.deltaTime;
                yield return null;
            }
            yield return E2EHarness.ReleaseKey(Key.LeftCtrl);

            E2EHarness.Assert(발바닥() < _입구높이 - 0.3f,
                              $"입구 아래로 내려갔다 ({t:F1}초, 발 {발바닥():F2}, 입구 {_입구높이:F2})");
            E2EHarness.Assert(DiveGateService.SealedEntries > 이전진입,
                              "방호복을 걸치고 통로에 들어간 것으로 기록된다");
            E2EHarness.Assert(!Vitals.Health.IsEmpty, "들어가는 동안 죽지 않았다");

            통로를_걷는다();
            yield return null;
        }

        // ── 통로 깔기·치우기 ────────────────────────────────────

        /// <summary>발바닥 높이. 통로 입구와 견주는 값이 이것이다.</summary>
        static float 발바닥()
        {
            var p = E2EHarness.Player.transform;
            var cc = p.GetComponent<CharacterController>();
            return cc != null ? p.position.y - cc.height * 0.5f + cc.center.y : p.position.y;
        }

        static DiveZone 통로를_깐다(float 입구높이, float 반경 = 20f)
        {
            var p = E2EHarness.Player.transform.position;
            var go = new GameObject("E2E_DiveZone");
            go.transform.position = new Vector3(p.x, 입구높이, p.z);

            var zone = go.AddComponent<DiveZone>();
            zone.Setup(반경, DiveRule.FirstDiveSeconds);
            return zone;
        }

        static void 통로를_걷는다()
        {
            if (_통로 != null) Object.Destroy(_통로.gameObject);
            _통로 = null;
        }

        static CraftingBench 제작대를_세운다()
        {
            var catalog = Resources.FindObjectsOfTypeAll<BuildCatalogSO>().FirstOrDefault();
            var def = catalog?.entries?.FirstOrDefault(b => b != null && b.id == "bench");
            if (def?.prefab == null) return null;

            // 배치 절차는 E2EBaseBuilding이 이미 본다. 여기서 보려는 것은 관문이므로
            // 지형 조준에 시나리오를 걸지 않고 곁에 바로 세운다.
            var pos = E2EHarness.Player.transform.position +
                      E2EHarness.Player.transform.forward * 2.2f;
            var go = Object.Instantiate(def.prefab, pos, Quaternion.identity);
            _세운것.Add(go);
            return go.GetComponentInChildren<CraftingBench>(true);
        }

        static void 누른다(Button b) =>
            ExecuteEvents.Execute(b.gameObject, new PointerEventData(EventSystem.current),
                                  ExecuteEvents.pointerClickHandler);

        static IEnumerator 치운다()
        {
            통로를_걷는다();

            foreach (var go in _세운것) if (go != null) Object.Destroy(go);
            _세운것.Clear();
            _제작대 = null;

            yield return E2EHarness.ReleaseAllKeys();
            yield return null;
        }
    }
}
