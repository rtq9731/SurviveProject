using System.Collections;
using UnityEngine;
using Survive.Creatures;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// <b>둥지와 코어</b> (기획서 §2.1 · §4.5, 스펙 §9).
    ///
    /// 한 바퀴를 돈다 — <b>훔치면 다섯, 떨구면 하나가 물어다 놓고, 그러면 다시 하나.</b>
    /// 그 한 바퀴가 도는 것이 "소프트락이 없다"의 화면 위 모습이다.
    ///
    /// <b>둥지 자리는 검증이 세운다.</b> 지상 지형이 아직 없으므로(스펙 §13) 진짜
    /// 배치를 기다린다 — 기획서는 둥지로 가는 길이 "액면이 뭍을 파고든 수로"라고
    /// 적어 두었고, 그 수로가 생기는 날 <c>NestSite</c>를 거기 놓으면 된다.
    /// </summary>
    public static class E2ENestAndCore
    {
        static NestSite _둥지;
        static Transform _코어;
        static ScytheSpawner _몸;

        static Vector3 사람자리 => E2EHarness.Player.transform.position;

        public static IEnumerator FullRun()
        {
            yield return 준비();
            if (_둥지 == null) yield break;

            yield return 평시에는_코어가_둥지에_있다();
            yield return 훔치면_다섯이_된다();
            yield return 떨구면_하나가_물어_간다();
            yield return 놓이면_다시_하나다();
            yield return 몇_번을_해도_되돌아온다();

            yield return 치운다();
            E2EHarness.Log("=== 둥지와 코어 완주 ===");
        }

        // ── 준비 ────────────────────────────────────────────────

        static IEnumerator 준비()
        {
            E2EHarness.SleepWildCreatures();
            E2EHarness.MuteAmbientLitZones();
            ScytheWatch.Reset();
            E2EScytheNight.밤에_세운다();
            E2EHarness.DarkenLantern();

            _몸 = ScytheSpawner.Instance;
            E2EHarness.Assert(_몸 != null, "스포너가 서 있다");
            if (_몸 == null) yield break;

            _몸.전부_치운다();
            남은것을_치운다();
            yield return null;

            // 둥지는 사람에게서 멀찍이 — 코어를 훔치러 걸어가는 것이 이 항목의 뼈대다.
            var 둥지자리 = 사람자리 + new Vector3(0f, 0f, 18f);
            var 둥지오브젝트 = new GameObject("E2E_둥지");
            둥지오브젝트.transform.position = 둥지자리;
            _둥지 = 둥지오브젝트.AddComponent<NestSite>();

            var 코어오브젝트 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            코어오브젝트.name = "E2E_코어";
            코어오브젝트.transform.position = 둥지자리;
            코어오브젝트.transform.localScale = Vector3.one * 0.6f;
            Object.Destroy(코어오브젝트.GetComponent<Collider>());
            자홍으로_빛낸다(코어오브젝트);
            _코어 = 코어오브젝트.transform;

            _둥지.SetCore(_코어);
            yield return null;

            E2EHarness.Log($"  둥지 {둥지자리.ToString("F1")} · 코어 {_코어.position.ToString("F1")} · " +
                           $"사람과 {Vector3.Distance(둥지자리, 사람자리):F1}m");
        }

        /// <summary>
        /// 코어는 자홍으로 빛난다(기획서 §2.1). <b>광원을 붙이지 않는다</b> —
        /// 어둠을 지키는 쪽이 언제나 우선이고, 에미션만으로 어둠 속 이동이 읽힌다.
        /// 값은 <c>ScytheTail</c>이 쓰는 매크로늄 자홍과 같다.
        /// </summary>
        static void 자홍으로_빛낸다(GameObject go)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;

            var m = new Material(r.sharedMaterial);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor(Shader.PropertyToID("_EmissionColor"),
                       new Color(0xA1 / 255f, 0x2E / 255f, 0xE0 / 255f, 1f) * 3f);
            r.sharedMaterial = m;
        }

        // ── 한 바퀴 ─────────────────────────────────────────────

        static IEnumerator 평시에는_코어가_둥지에_있다()
        {
            E2EHarness.Log("— 평시: 코어가 둥지에 있다 —");

            _둥지.훑는다();
            yield return 수가_맞을_때까지(1);

            E2EHarness.AssertEqual(_둥지.Where, CoreWhere.Nest, "코어가 둥지에 있다");
            E2EHarness.AssertEqual(ScytheWatch.Alert, ScytheAlert.Calm, "평시다");
            E2EHarness.AssertEqual(_몸.Alive, 1, "한 마리다");
        }

        static IEnumerator 훔치면_다섯이_된다()
        {
            E2EHarness.Log("— 훔친다: 다섯이 된다 —");

            // 사람이 집었다. 코어를 손에 붙인다.
            _둥지.Report(CoreEvent.Taken);
            _코어.position = 사람자리 + Vector3.up * 1.2f;
            _둥지.훑는다();
            yield return null;

            E2EHarness.AssertEqual(_둥지.Where, CoreWhere.Held, "손에 있다");
            E2EHarness.AssertEqual(ScytheWatch.Alert, ScytheAlert.Alarmed, "발령이다");
            E2EHarness.AssertEqual(ScytheWatch.Population, 5, "목표가 다섯이다");

            yield return 수가_맞을_때까지(5);
            E2EHarness.AssertEqual(_몸.Alive, 5, "다섯이 실제로 섰다");
        }

        static IEnumerator 떨구면_하나가_물어_간다()
        {
            E2EHarness.Log("— 떨군다: 하나가 물어 간다 —");

            // 둥지에서 먼 자리에 떨군다. 가까우면 곧바로 놓인 것이 되어
            // 회수를 재지 못한다.
            var 떨군자리 = 사람자리 + new Vector3(6f, 0f, 0f);
            _코어.position = 떨군자리;
            _둥지.Report(CoreEvent.Dropped);
            yield return null;

            E2EHarness.Assert(!_둥지.CoreAtHome, "떨군 자리는 둥지 밖이다");

            float t = 0f;
            while (t < 10f && _둥지.Retriever == null)
            {
                _둥지.훑는다();
                t += Time.deltaTime;
                yield return null;
            }

            E2EHarness.Assert(_둥지.Retriever != null, $"하나가 물었다 ({t:F1}초)");
            E2EHarness.AssertEqual(_둥지.Where, CoreWhere.Carried, "물고 가는 중이다");

            // <b>정확히 하나다.</b> 무리 지어 가는 것은 생물의 그림이다.
            int 회수중 = 0;
            foreach (var m in Object.FindObjectsByType<ScytheMind>(FindObjectsInactive.Exclude))
                if (m != null && m.State == ScytheState.Retrieve) 회수중++;

            E2EHarness.AssertEqual(회수중, 1, "회수하는 개체가 정확히 하나다");

            // <b>짐을 든 꼬리는 무기가 아니다.</b> 규칙이 아니라 몸이 막는다.
            E2EHarness.Assert(!ScytheFsm.CanAttack(_둥지.Retriever.State),
                              "회수 중인 개체는 공격 판정에서 빠진다");
            E2EHarness.Log($"  회수자 {_둥지.Retriever.name} · 4상태 {_둥지.Retriever.State}");
        }

        static IEnumerator 놓이면_다시_하나다()
        {
            E2EHarness.Log("— 놓인다: 다시 하나다 —");

            // 물고 가던 개체를 둥지에 데려다 놓는다. 실제로 헤엄쳐 가는 것을
            // 기다리는 것은 이 항목이 재려는 것이 아니다.
            var 회수자 = _둥지.Retriever;
            E2EHarness.Assert(회수자 != null, "물고 가던 개체가 있다");
            if (회수자 == null) yield break;

            회수자.transform.position = _둥지.transform.position;
            E2EHarness.SyncPhysics();
            _둥지.훑는다();
            yield return null;

            E2EHarness.AssertEqual(_둥지.Where, CoreWhere.Nest, "코어가 둥지에 놓였다");
            E2EHarness.AssertEqual(ScytheWatch.Alert, ScytheAlert.Calm, "평시로 내려왔다");

            yield return 수가_맞을_때까지(1);
            E2EHarness.AssertEqual(_몸.Alive, 1, "넷이 흩어지고 하나만 남았다");

            E2EHarness.Log($"  남은 것 {_몸.Alive}마리 · 흩어진 총 {_몸.DespawnedTotal}마리");
        }

        static IEnumerator 몇_번을_해도_되돌아온다()
        {
            E2EHarness.Log("— 다시 훔쳐도 되돌아온다: 대가는 시간뿐이다 —");

            for (int 판 = 1; 판 <= 2; 판++)
            {
                _둥지.Report(CoreEvent.Taken);
                _코어.position = 사람자리 + Vector3.up * 1.2f;
                _둥지.훑는다();
                yield return null;
                E2EHarness.AssertEqual(ScytheWatch.Alert, ScytheAlert.Alarmed, $"{판}번째 발령");

                // 사람이 직접 되돌려 놓는다 — 낫을 기다리지 않는 길도 있어야 한다.
                _코어.position = _둥지.transform.position;
                _둥지.훑는다();
                yield return null;

                E2EHarness.AssertEqual(_둥지.Where, CoreWhere.Nest, $"{판}번째 반환");
                E2EHarness.AssertEqual(ScytheWatch.Alert, ScytheAlert.Calm, $"{판}번째 해제");
            }

            yield return 수가_맞을_때까지(1);
            E2EHarness.AssertEqual(_몸.Alive, 1, "끝나고 한 마리다");
        }

        // ── 무대 ────────────────────────────────────────────────

        static IEnumerator 수가_맞을_때까지(int 목표)
        {
            float t = 0f;
            while (t < 12f && _몸.Alive != 목표)
            {
                _몸.수를_맞춘다();
                t += Time.deltaTime;
                yield return null;
            }
            yield return null;
        }

        static IEnumerator 치운다()
        {
            if (_몸 != null) _몸.전부_치운다();
            남은것을_치운다();
            ScytheWatch.Reset();
            E2EScytheNight.시계를_되돌린다();
            E2EHarness.RestoreWorld();
            yield return null;
        }

        static void 남은것을_치운다()
        {
            foreach (var n in Object.FindObjectsByType<NestSite>(FindObjectsInactive.Include))
                if (n != null && n.gameObject.name.StartsWith("E2E_둥지"))
                    Object.DestroyImmediate(n.gameObject);

            var 코어 = GameObject.Find("E2E_코어");
            if (코어 != null) Object.DestroyImmediate(코어);

            _둥지 = null;
            _코어 = null;
        }
    }
}
