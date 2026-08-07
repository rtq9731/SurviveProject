using System.Collections;
using UnityEngine;
using Survive.Creatures;
using Survive.Localization;
using Survive.Narrative;
using Survive.Progression;

namespace Survive.Testing
{
    /// <summary>
    /// <b>코어를 훔치기 전에 AI가 한 번 경고한다</b> (기획서 §4.5, 스펙 §8-4).
    ///
    /// 재는 것은 셋이다.
    /// <list type="number">
    /// <item><b>멀리서는 조용하다.</b> 경고가 판이 시작하자마자 울리면 그것은
    ///       경고가 아니라 안내문이다.</item>
    /// <item><b>다가가면 코어가 아직 둥지에 있는 채로 울린다.</b> "훔치기 전"이
    ///       참이려면 울린 그 순간의 코어 자리가 <c>Nest</c>여야 한다.</item>
    /// <item><b>두 번은 울리지 않는다 — 그리고 그것을 지키는 것이 원장이다.</b>
    ///       컴포넌트의 기억을 지워 놓고 다시 걸어 들어가도 조용해야 한다.
    ///       저장본을 불러와 부품이 새로 선 상태가 정확히 그 모양이다.</item>
    /// </list>
    ///
    /// <b>걸어서 다가간다.</b> 자리를 코드로 옮겨 놓고 재면 반경을 재는 것이 아니라
    /// 반경을 적은 상수를 다시 읽는 것이 된다.
    /// </summary>
    public static class E2ECoreWarning
    {
        static NestSite _둥지;
        static Transform _코어;
        static CoreTheftWarner _경고기;
        static SequenceSO _대사;
        static UnlockLedgerState _원장백업;

        static Vector3 사람자리 => E2EHarness.Player.transform.position;

        static UnlockService 서비스 => UnlockService.Instance;

        public static IEnumerator FullRun()
        {
            yield return 준비();
            if (_둥지 == null) yield break;

            yield return 멀리서는_조용하다();
            yield return 다가가면_코어가_아직_둥지에_있는_채로_울린다();
            yield return 기억을_지워도_원장이_두_번째를_막는다();

            yield return 치운다();
            E2EHarness.Log("=== 코어 사전 경고 완주 ===");
        }

        // ── 준비 ────────────────────────────────────────────────

        static IEnumerator 준비()
        {
            E2EHarness.SleepWildCreatures();

            // 낫이 서면 걸어가는 길에 얻어맞는다. 여기서 재는 것은 경고 한 줄이지
            // 습격이 아니다 — 습격 쪽은 E2ENestAndCore가 이미 재고 있다.
            ScytheSpawner.Suspended = true;
            ScytheWatch.Reset();

            E2EHarness.Assert(서비스 != null, "UnlockService가 서 있다");
            if (서비스 == null) yield break;

            _경고기 = CoreTheftWarner.Instance;
            E2EHarness.Assert(_경고기 != null, "CoreTheftWarner가 스스로 서 있다");
            if (_경고기 == null) yield break;

            _대사 = Resources.Load<SequenceSO>(CoreTheftWarning.ResourceName);
            E2EHarness.Assert(_대사 != null,
                              $"Resources/{CoreTheftWarning.ResourceName} 경고 대사를 찾았다");
            if (_대사 == null) yield break;

            // 원장을 통째로 떠 두고 비운다. 앞 시나리오가 이미 이 열쇠를 세워 놓았으면
            // "처음 울린다"를 잴 수 없고, 그렇다고 열쇠 하나만 빼는 문은 원장에 없다.
            _원장백업 = 서비스.Ledger.Capture();
            서비스.Ledger.Restore(null);
            _경고기.기억을_지운다();

            남은것을_치운다();

            // 경고 반경(15m) 바깥에 세운다. 걸어 들어가는 것이 이 항목의 뼈대다.
            var 둥지자리 = 사람자리 + Vector3.forward * 22f;
            var 둥지오브젝트 = new GameObject("E2E_경고둥지");
            둥지오브젝트.transform.position = 둥지자리;
            _둥지 = 둥지오브젝트.AddComponent<NestSite>();

            var 코어오브젝트 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            코어오브젝트.name = "E2E_경고코어";
            코어오브젝트.transform.position = 둥지자리;
            코어오브젝트.transform.localScale = Vector3.one * 0.6f;
            Object.Destroy(코어오브젝트.GetComponent<Collider>());
            _코어 = 코어오브젝트.transform;

            _둥지.SetCore(_코어);
            E2EHarness.SyncPhysics();
            yield return null;

            E2EHarness.Log($"  둥지 {둥지자리.ToString("F1")} · 사람과 " +
                           $"{CoreTheftWarning.PlaneDistance(둥지자리, 사람자리):F1}m " +
                           $"(경고 반경 {CoreTheftWarning.WarnRadius:F0}m)");
        }

        // ── ① 멀리서는 조용하다 ──────────────────────────────────

        static IEnumerator 멀리서는_조용하다()
        {
            E2EHarness.Log("— 멀리 서 있는 동안에는 아무 말도 없다 —");

            int 기준 = 서비스.LinesSpoken;

            float t = 0f;
            while (t < 1.5f) { t += Time.deltaTime; yield return null; }

            float 거리 = CoreTheftWarning.PlaneDistance(사람자리, _둥지.transform.position);
            E2EHarness.Assert(거리 > CoreTheftWarning.WarnRadius,
                              $"아직 경고 반경 밖이다 ({거리:F1}m)");
            E2EHarness.Assert(!_경고기.Warned, "경고기가 아직 울리지 않았다");
            E2EHarness.AssertEqual(서비스.LinesSpoken, 기준, "AI가 한 줄도 말하지 않았다");
            E2EHarness.Assert(!서비스.Ledger.IsUnlocked(CoreTheftWarning.Key),
                              "원장에 경고 열쇠가 아직 없다");
        }

        // ── ② 다가가면 울린다, 그리고 코어는 아직 둥지에 있다 ────

        static IEnumerator 다가가면_코어가_아직_둥지에_있는_채로_울린다()
        {
            E2EHarness.Log("— 둥지 쪽으로 걸어 들어간다 —");

            int 기준 = 서비스.LinesSpoken;

            // 울리는 순간 멈춘다. 끝까지 걸어가 놓고 재면 "언제 울렸는가"를 잃는다.
            yield return E2EHarness.WalkTo(_둥지.transform.position, 4f, 45f,
                                           throwOnTimeout: false,
                                           arrived: () => _경고기.Warned);

            float 거리 = CoreTheftWarning.PlaneDistance(사람자리, _둥지.transform.position);
            E2EHarness.Assert(_경고기.Warned, $"경고가 울렸다 (둥지까지 {거리:F1}m)");
            E2EHarness.Assert(거리 <= CoreTheftWarning.WarnRadius + 1f,
                              $"경고 반경 안에서 울렸다 ({거리:F1}m)");

            // <b>훔치기 전이다.</b> 이 단언이 이 시나리오의 알맹이다.
            E2EHarness.AssertEqual(_둥지.Where, CoreWhere.Nest, "코어는 아직 둥지에 있다");
            E2EHarness.Assert(_둥지.CoreAtHome, "코어를 아직 집지 않았다");

            E2EHarness.Assert(서비스.Ledger.IsUnlocked(CoreTheftWarning.Key),
                              "원장에 경고 열쇠가 섰다");

            yield return E2EHarness.WaitUntil(() => 서비스.LinesSpoken > 기준,
                                              "자막이 한 줄 나갔다", 5f);

            // <b>표를 거친 글자인가.</b> 에셋에서 곧장 온 글자면 로케일을 바꿔도
            // 이 줄만 한국어로 남는다 (SpokenLine의 문서 주석).
            string 표에적힌것 = DataText.Line(_대사, 0);
            E2EHarness.Assert(!string.IsNullOrWhiteSpace(표에적힌것), "표에 경고 대사가 있다");
            E2EHarness.AssertEqual(서비스.LastLine, 표에적힌것, "화면에 나간 글자가 표에서 왔다");

            E2EHarness.Log($"  \"{서비스.LastLine}\"");
        }

        // ── ③ 두 번은 울리지 않는다 ──────────────────────────────

        static IEnumerator 기억을_지워도_원장이_두_번째를_막는다()
        {
            E2EHarness.Log("— 부품의 기억을 지워도 원장이 막는다 —");

            int 기준 = 서비스.LinesSpoken;

            // 저장본을 불러와 부품이 새로 선 상태를 흉내 낸다. 1회성의 주인이
            // 컴포넌트의 bool이라면 여기서 다시 울릴 것이다.
            _경고기.기억을_지운다();
            E2EHarness.Assert(!_경고기.Warned, "기억이 지워졌다");

            float t = 0f;
            while (t < 1.5f) { t += Time.deltaTime; yield return null; }

            float 거리 = CoreTheftWarning.PlaneDistance(사람자리, _둥지.transform.position);
            E2EHarness.Assert(거리 <= CoreTheftWarning.WarnRadius + 1f,
                              $"여전히 경고 반경 안이다 ({거리:F1}m)");
            E2EHarness.AssertEqual(서비스.LinesSpoken, 기준, "두 번째 줄은 나가지 않았다");
        }

        // ── 무대 ────────────────────────────────────────────────

        static IEnumerator 치운다()
        {
            남은것을_치운다();

            if (서비스 != null && _원장백업 != null) 서비스.Ledger.Restore(_원장백업);
            _원장백업 = null;

            ScytheWatch.Reset();
            ScytheSpawner.Suspended = false;
            E2EHarness.RestoreWorld();
            yield return null;
        }

        static void 남은것을_치운다()
        {
            foreach (var n in Object.FindObjectsByType<NestSite>(FindObjectsInactive.Include))
                if (n != null && n.gameObject.name.StartsWith("E2E_경고둥지"))
                    Object.DestroyImmediate(n.gameObject);

            var 코어 = GameObject.Find("E2E_경고코어");
            if (코어 != null) Object.DestroyImmediate(코어);

            _둥지 = null;
            _코어 = null;
        }
    }
}
