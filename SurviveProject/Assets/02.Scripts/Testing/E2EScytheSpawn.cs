using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Survive.Creatures;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// <b>낫이 실제로 세계에 선다</b> (기획서 §4.5 "경계 상태", 스펙 §5).
    ///
    /// <b>이 시나리오가 이 라운드의 본체다.</b> 지금까지 규칙만 있고 몸이 없었다 —
    /// 서식 범위도 4상태도 밤낮도 다 섰는데 세계에 낫이 한 마리도 없어서 밤이 되어도
    /// 아무 일이 일어나지 않았다. 여기서 재는 것은 <b>그 몸이 도는가</b>다.
    ///
    /// 넷을 본다.
    /// <list type="number">
    /// <item><b>평시에 한 마리가 선다</b> — 그리고 액면 위, 사람에게서 먼 자리에</item>
    /// <item><b>등급이 오르면 수가 따라 오른다</b> — 1 → 2 → 5</item>
    /// <item><b>등급이 내려가면 먼 것부터 흩어진다</b> — 가까운 하나가 남는다</item>
    /// <item><b>낮이 와도 개체는 사라지지 않는다</b> — 물러날 뿐이다</item>
    /// </list>
    /// </summary>
    public static class E2EScytheSpawn
    {
        static ScytheSpawner _몸;
        static DayNightService _시계;

        static Vector3 사람자리 => E2EHarness.Player.transform.position;

        public static IEnumerator FullRun()
        {
            yield return 준비();
            if (_몸 == null) yield break;

            yield return 평시에_한_마리가_선다();
            yield return 등급이_오르면_수가_따라_오른다();
            yield return 내려가면_먼_것부터_흩어진다();
            yield return 낮이_와도_사라지지_않는다();

            yield return 치운다();
            E2EHarness.Log("=== 낫 스포너 완주 ===");
        }

        static IEnumerator 준비()
        {
            E2EHarness.SleepWildCreatures();
            E2EHarness.MuteAmbientLitZones();
            ScytheWatch.Reset();
            E2EScytheNight.밤에_세운다();

            // 이 시나리오는 스포너 자체를 잰다. 위에서 멈춘 것을 도로 켠다.
            ScytheSpawner.Suspended = false;
            E2EHarness.DarkenLantern();

            _시계 = DayNightService.Instance;
            _몸 = ScytheSpawner.Instance;

            E2EHarness.Assert(_시계 != null, "밤낮 시계가 있다");
            E2EHarness.Assert(_몸 != null, "스포너가 스스로 서 있다 — 씬을 고치지 않았다");
            if (_몸 == null) yield break;

            _몸.전부_치운다();
            yield return null;

            E2EHarness.Log($"  시각 {_시계.TimeOfDay:F3} · 밤={_시계.IsNight} · " +
                           $"등급 {ScytheWatch.Alert} · 목표 {ScytheWatch.Population}마리");
        }

        // ── 1. 평시에 한 마리 ───────────────────────────────────

        static IEnumerator 평시에_한_마리가_선다()
        {
            E2EHarness.Log("— 평시: 한 마리가 선다 —");

            E2EHarness.AssertEqual(ScytheWatch.Alert, ScytheAlert.Calm, "평시로 시작한다");
            E2EHarness.AssertEqual(ScytheWatch.Population, 1, "평시 목표는 한 마리다");

            yield return 수가_맞을_때까지(1);

            E2EHarness.AssertEqual(_몸.Alive, 1, "한 마리가 실제로 섰다");

            var 낫 = 세워진것들();
            E2EHarness.Assert(낫.Count == 1, "센 것과 실제가 같다");
            if (낫.Count == 0) yield break;

            Vector3 자리 = 낫[0].transform.position;
            float 거리 = Vector3.Distance(자리, 사람자리);
            var 몸 = 낫[0].GetComponent<HoverDrifter>();

            E2EHarness.Log($"  선 자리 {자리.ToString("F1")} · 사람과 {거리:F1}m · 구역 {몸?.Zone}");

            // <b>세우자마자 덮치면 「나타났다」가 아니라 「덮쳤다」가 된다.</b>
            var def = 낫[0].GetComponent<Survive.Combat.CreatureHealth>()?.Definition;
            float 감지 = def != null ? def.detectRadius : 14f;
            E2EHarness.Assert(거리 > 감지,
                              $"감지 반경 밖에 섰다 ({거리:F1}m > {감지}m)");

            E2EHarness.Assert(!LitZoneRegistry.IsLit(자리), "빛 밖에 섰다");
            E2EHarness.AssertEqual(몸 != null ? 몸.Zone : HabitatZone.Inland, HabitatZone.Liquid,
                                   "액면 위에 섰다");
        }

        // ── 2. 등급이 오르면 ────────────────────────────────────

        static IEnumerator 등급이_오르면_수가_따라_오른다()
        {
            E2EHarness.Log("— 등급이 오른다: 1 → 2 → 5 —");

            ScytheWatch.Set(ScytheAlert.Awake);
            yield return 수가_맞을_때까지(2);
            E2EHarness.AssertEqual(_몸.Alive, 2, "각성이면 두 마리다");

            ScytheWatch.Set(ScytheAlert.Alarmed);
            yield return 수가_맞을_때까지(5);
            E2EHarness.AssertEqual(_몸.Alive, 5, "발령이면 다섯 마리다");

            var 자리들 = new List<string>();
            foreach (var 낫 in 세워진것들())
                자리들.Add($"{낫.transform.position.ToString("F1")}" +
                          $"({Vector3.Distance(낫.transform.position, 사람자리):F1}m)");

            E2EHarness.Log($"  다섯 마리: {string.Join(" · ", 자리들)}");

            // 한 자리에 겹쳐 세우면 다섯이 하나처럼 보인다.
            var 본것 = new HashSet<string>(자리들);
            E2EHarness.AssertEqual(본것.Count, 5, "다섯이 서로 다른 자리에 섰다");
        }

        // ── 3. 내려가면 먼 것부터 ───────────────────────────────

        static IEnumerator 내려가면_먼_것부터_흩어진다()
        {
            E2EHarness.Log("— 등급이 내려간다: 먼 것부터 흩어진다 —");

            var 전 = 세워진것들();
            float 가장가까운거리 = float.MaxValue;
            Transform 가장가까운것 = null;
            foreach (var 낫 in 전)
            {
                float d = Vector3.Distance(낫.transform.position, 사람자리);
                if (d >= 가장가까운거리) continue;
                가장가까운거리 = d;
                가장가까운것 = 낫.transform;
            }

            E2EHarness.Assert(가장가까운것 != null, "가장 가까운 개체를 집었다");
            Vector3 남아야할자리 = 가장가까운것 != null ? 가장가까운것.position : Vector3.zero;

            ScytheWatch.Set(ScytheAlert.Calm);
            yield return 수가_맞을_때까지(1);

            E2EHarness.AssertEqual(_몸.Alive, 1, "평시로 내려오면 한 마리만 남는다");

            var 후 = 세워진것들();
            if (후.Count == 0) yield break;

            float 남은거리 = Vector3.Distance(후[0].transform.position, 사람자리);
            E2EHarness.Log($"  남은 것 {후[0].transform.position.ToString("F1")} " +
                           $"({남은거리:F1}m) · 남아야 할 것 {남아야할자리.ToString("F1")} " +
                           $"({가장가까운거리:F1}m)");

            // <b>가까운 하나가 남는다.</b> 시선이 남은 하나에 모이는 것이
            // §4.5 회수 연출이 노리는 그림이다.
            E2EHarness.Assert(Vector3.Distance(후[0].transform.position, 남아야할자리) < 3f,
                              $"가장 가까웠던 개체가 남았다 " +
                              $"({남은거리:F1}m vs {가장가까운거리:F1}m)");
        }

        // ── 4. 낮이 와도 사라지지 않는다 ────────────────────────

        static IEnumerator 낮이_와도_사라지지_않는다()
        {
            E2EHarness.Log("— 낮으로 돌린다: 물러나되 사라지지 않는다 —");

            var 전 = 세워진것들();
            int 전개체수 = 전.Count;
            E2EHarness.Assert(전개체수 > 0, "밤에 서 있던 개체가 있다");

            _시계.SetTimeOfDay(0.5f);
            yield return null;
            yield return null;

            E2EHarness.Assert(!_시계.IsNight, $"한낮이다 (시각 {_시계.TimeOfDay:F3})");

            float t = 0f;
            var 본상태 = new HashSet<ScytheState>();
            while (t < 8f)
            {
                foreach (var 낫 in 세워진것들())
                {
                    var m = 낫.GetComponent<ScytheMind>();
                    if (m != null) 본상태.Add(m.State);
                }
                t += Time.deltaTime;
                yield return null;
            }

            var 후 = 세워진것들();
            E2EHarness.Log($"  낮 8초 뒤 개체 {후.Count}마리 · 본 4상태 {string.Join("·", 본상태)} · " +
                           $"목표 {ScytheWatch.Population}마리");

            // <b>이것이 「없앴다 만드는 것과 자리를 옮기는 것은 다른 물건」의 내용이다.</b>
            // 낮에 지웠다가 밤에 다시 만들면 도감의 「본 적 있다」와 유물 굴림 시계가
            // 매일 처음부터 시작한다.
            E2EHarness.AssertEqual(후.Count, 전개체수,
                                   "낮이 와도 개체 수가 줄지 않는다 — 지운 것이 아니라 물러난 것이다");
            E2EHarness.Assert(!본상태.Contains(ScytheState.Beware) &&
                              !본상태.Contains(ScytheState.Attack),
                              $"낮에는 쫓지도 덤비지도 않는다 ({string.Join("·", 본상태)})");
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

        static List<CreatureBrain> 세워진것들()
        {
            var 것들 = new List<CreatureBrain>();
            foreach (var b in Object.FindObjectsByType<CreatureBrain>(FindObjectsInactive.Exclude))
                if (b != null && b.gameObject.name == ScytheSpawner.SpawnedName) 것들.Add(b);

            return 것들;
        }

        static IEnumerator 치운다()
        {
            if (_몸 != null) _몸.전부_치운다();
            ScytheWatch.Reset();
            E2EScytheNight.시계를_되돌린다();
            E2EHarness.RestoreWorld();
            yield return null;
        }
    }
}
