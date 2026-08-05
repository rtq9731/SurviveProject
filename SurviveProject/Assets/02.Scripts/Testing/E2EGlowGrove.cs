using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Survive.Combat;
using Survive.Creatures;
using Survive.Harvesting;
using Survive.Interaction;
using Survive.Items;
using Survive.UI;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// 발광 버섯 갓 루프 — 빛을 자원으로 바꿔 먹는 거래 (백로그 33).
    ///
    /// 확인하려는 문장은 넷이다.
    /// <list type="number">
    /// <item><b>딸수록 어두워진다</b> — 갓 하나마다 빛 세기가 눈에 보이게 준다.</item>
    /// <item><b>다 따면 문이 열린다</b> — 꺼진 군락은 밝은 구역에서 빠지고,
    ///   빛을 꺼리던 낫이 <b>실제로 그 안까지 걸어 들어온다</b>.</item>
    /// <item><b>영구는 아니다</b> — 갓이 다시 자라면 빛도 돌아오고 낫은 다시 막힌다.</item>
    /// <item><b>표식은 방패가 아니다</b> — 발밑에 놓은 갓은 빛나지만 낫을 막지 못한다.
    ///   막을 수 있으면 "배터리가 생존줄"이라는 압박이 사라진다.</item>
    /// </list>
    ///
    /// 전부 실제 조작으로 한다 — 갓은 E를 길게 눌러 따고, 표식은 퀵슬롯 숫자키로 놓고,
    /// 회수도 E다. 플래그를 코드로 세우는 것은 통과로 치지 않는다.
    /// </summary>
    public static class E2EGlowGrove
    {
        const string ScythePrefabPath = "Assets/05.Prefabs/Creatures/Creature_낫.prefab";

        /// <summary>검증에서 쓰는 재생 시간. 규칙은 그대로 두고 상수만 줄인다.</summary>
        const float 검증용재생초 = 2.5f;

        static Survive.Player.PlayerContext Ctx => E2EHarness.Player;
        static Vector3 PlayerPos => E2EHarness.Player.transform.position;
        static Inventory Inv => E2EHarness.Player.Inventory.Inventory;

        static CreatureDefinitionSO _낫정의;

        /// <summary>
        /// 낫을 세우는 거리. <b>예전 빛 반경 바깥, 감지 반경 안</b>이어야 한다.
        ///
        /// 반경 안에 세우면 "들어왔다"를 확인할 수 없다 — 처음부터 안에 있었으니
        /// 첫 프레임에 조건이 참이 되고, 실제로는 한 발짝도 걷지 않았는데 통과한다
        /// (실제로 그렇게 통과할 뻔했다). 감지 반경 밖에 세우면 이번에는
        /// 불이 켜져 있을 때 "빛이 막는다"를 확인할 수 없다.
        /// </summary>
        static float _소환거리;

        /// <summary>빛이 켜져 있는 동안 낫이 플레이어에게 다가올 수 있었던 가장 가까운 거리.</summary>
        static float _켜졌을때_최소거리;

        public static IEnumerator FullRun()
        {
            GlowGrove 군락 = null;
            CreatureBrain 낫 = null;

            yield return 준비(g => 군락 = g);
            if (군락 == null) yield break;

            Vector3 시험점 = 설_자리(군락);
            _소환거리 = Mathf.Min(_낫정의.detectRadius - 1.5f, 군락.BaseRange + 2f);
            E2EHarness.Log($"  시험점(군락 안에 서는 자리): {시험점.ToString("F1")}, " +
                           $"낫을 세울 거리 {_소환거리:F1}m " +
                           $"(빛 반경 {군락.BaseRange:F0}m 밖, 감지 {_낫정의.detectRadius:F0}m 안)");

            yield return 켜진_군락은_밝은_구역이다(군락, 시험점);
            yield return 소환한다(시험점, r => 낫 = r);
            if (낫 == null) yield break;

            yield return 켜져_있으면_낫이_들어오지_못한다(낫, 시험점);

            재운다(낫);
            yield return 갓을_딸수록_어두워진다(군락);
            yield return 남은_빛도_전부_끈다(시험점);
            yield return 다_따면_꺼지고_충전도_멈춘다(군락, 시험점);

            깨운다(낫, 시험점);
            yield return 꺼지면_낫이_실제로_들어온다(낫, 군락, 시험점);
            yield return 놓아둔_갓은_낫을_막지_못한다(낫, 시험점);

            재운다(낫);
            yield return 놓아둔_갓을_회수한다();
            yield return 다시_자라면_빛이_돌아온다(군락, 시험점);

            깨운다(낫, 시험점);
            yield return 다시_자라면_낫은_또_막힌다(낫, 시험점);

            치운다(낫);
            E2EHarness.Log("=== 발광 버섯 갓 루프 완주 ===");
        }

        // ── 준비 ────────────────────────────────────────────────

        static IEnumerator 준비(System.Action<GlowGrove> result)
        {
            // 설치 서비스는 AfterSceneLoad에 스스로 선다. 도메인 리로드를 끈 채
            // 재생을 반복하면 놓칠 수 있으므로 없으면 여기서 한 번 부른다.
            if (GlowGrove.Active.Count == 0) GlowGroveService.Build();
            yield return null;

            E2EHarness.Assert(GlowGrove.Active.Count > 0,
                              $"군락이 스스로 섰다 ({GlowGrove.Active.Count}곳)");

            foreach (var g in GlowGrove.Active)
                E2EHarness.Log($"  군락 {g.name}: 갓 {g.Total}무더기, " +
                               $"세기 {g.Light.intensity:F2}, 반경 {g.BaseRange:F1}m, " +
                               $"중심 {g.LitZoneCenter.ToString("F1")}");

            E2EHarness.Assert(GlowGrove.Active.All(g => g.Total > 0), "모든 군락에 갓이 붙었다");
            E2EHarness.Assert(GlowGrove.Active.All(g => Mathf.Approximately(g.Brightness, 1f)),
                              "시작할 때는 전부 온전하다");

            // 갓이 가장 많은 군락을 고른다. 밝기 단계가 많을수록 "비례해서 준다"가
            // 우연이 아니라는 것이 분명해진다.
            var 고른군락 = GlowGrove.Active.OrderByDescending(g => g.Total)
                                           .ThenBy(g => g.name)
                                           .First();
            E2EHarness.Log($"  시험 군락: {고른군락.name} (갓 {고른군락.Total}무더기)");

            // 낫 정의는 프리팹에서 꺼낸다. 씬에 배치된 개체가 아직 없다(§8-4는 사람과 함께).
            var prefab = 프리팹();
            E2EHarness.Assert(prefab != null, "소비자 프리팹을 찾았다");
            if (prefab == null) { result(null); yield break; }

            _낫정의 = prefab.GetComponent<CreatureHealth>()?.Definition;
            E2EHarness.Assert(_낫정의 != null && _낫정의.avoidsLight, "낫은 빛을 꺼린다");

            // 랜턴이 켜져 있으면 플레이어 자리가 언제나 밝아 군락의 몫을 가린다.
            var lantern = Object.FindAnyObjectByType<LanternController>(FindObjectsInactive.Include);
            if (lantern != null) lantern.SetOn(false);
            E2EHarness.Assert(lantern == null || !lantern.IsOn, "랜턴은 꺼 둔다");

            result(고른군락);
        }

        static GameObject 프리팹()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ScythePrefabPath);
#else
            return null;
#endif
        }

        /// <summary>이 군락에 물려 있는 충전 구역. 군락의 <b>바닥</b>이 어디인지를 아는 유일한 것이다.</summary>
        static LightZone 구역(GlowGrove 군락) =>
            Object.FindObjectsByType<LightZone>().FirstOrDefault(z => z.Grove == 군락);

        /// <summary>
        /// 군락 안에서 사람이 서고 <b>낫이 걸어올 수 있는</b> 자리.
        ///
        /// 라이트의 좌표에서 찾으면 안 된다. 군락의 빛은 바닥에서 3m 넘게 떠 있어서,
        /// 그 높이에서 NavMesh를 물으면 곁의 바위 턱이 잡힌다 — 실제로 플레이어가
        /// 군락 바닥보다 2.8m 높은 턱에 서는 바람에 낫이 길을 찾지 못하고
        /// 11.7m 앞에서 멈춰 섰다. 충전 구역(LightZone)의 좌표가 곧 군락의 바닥이다.
        /// </summary>
        static Vector3 설_자리(GlowGrove 군락)
        {
            var zone = 구역(군락);
            Vector3 기준 = zone != null ? zone.transform.position : 군락.LitZoneCenter;

            if (NavMesh.SamplePosition(기준, out var nav, 8f, NavMesh.AllAreas))
                return nav.position + Vector3.up * 1.0f;

            if (Physics.Raycast(기준 + Vector3.up * 5f, Vector3.down, out var hit, 40f,
                                ~0, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 1.0f;

            return 기준;
        }

        static IEnumerator 선다(Vector3 자리)
        {
            for (int i = 0; i < 4; i++)
            {
                E2EHarness.Teleport(자리);
                yield return null;
            }
        }

        // ── 켜진 군락 ───────────────────────────────────────────

        static IEnumerator 켜진_군락은_밝은_구역이다(GlowGrove 군락, Vector3 시험점)
        {
            yield return 선다(시험점);

            E2EHarness.Assert(군락.Light != null && 군락.Light.enabled, "군락의 빛이 켜져 있다");
            E2EHarness.Assert(LitZoneRegistry.IsLit(시험점),
                              "군락이 밝은 구역으로 등록돼 있다 — 화톳불·랜턴과 같은 창구다");

            var zone = 구역(군락);
            E2EHarness.Assert(zone != null && zone.Grove != null, "충전 구역에 군락이 물려 있다");
            E2EHarness.Assert(zone.CurrentRechargePerSecond > 0f,
                              $"온전한 군락은 배터리를 채워 준다 ({zone.CurrentRechargePerSecond:F1}/초)");
        }

        // ── 낫 ──────────────────────────────────────────────────

        static IEnumerator 소환한다(Vector3 시험점, System.Action<CreatureBrain> result)
        {
            var prefab = 프리팹();
            if (prefab == null) { result(null); yield break; }

            if (!자리를_찾는다(시험점, _소환거리, out var spawn))
            {
                E2EHarness.Assert(false, "낫을 세울 NavMesh 자리를 찾았다");
                result(null);
                yield break;
            }

            var go = Object.Instantiate(prefab, spawn, Quaternion.identity);
            go.name = "E2E_낫";
            yield return null;
            yield return null;

            var brain = go.GetComponent<CreatureBrain>();
            E2EHarness.Assert(brain != null, "낫의 두뇌가 붙어 있다");
            E2EHarness.Log($"  낫 소환: {spawn.ToString("F1")} " +
                           $"(군락 중심에서 {Vector3.Distance(spawn, 시험점):F1}m, 감지 {_낫정의.detectRadius}m)");
            result(brain);
        }

        /// <summary>
        /// 기준점에서 원하는 거리쯤 되는 NavMesh 자리를 방향을 돌려 가며 찾는다.
        /// 가까워지는 쪽으로 밀리면 안 되므로 하한을 둔다 — 거리 자체가 판정의 전제다.
        /// </summary>
        static bool 자리를_찾는다(Vector3 기준, float 거리, out Vector3 결과)
        {
            float 하한 = 거리 - 2.5f;
            for (int i = 0; i < 24; i++)
            {
                var dir = Quaternion.Euler(0f, i * 15f, 0f) * Vector3.forward;
                if (!NavMesh.SamplePosition(기준 + dir * 거리, out var hit, 4f, NavMesh.AllAreas)) continue;
                if (Vector3.Distance(hit.position, 기준) < 하한) continue;
                결과 = hit.position;
                return true;
            }
            결과 = 기준;
            return false;
        }

        static void 재운다(CreatureBrain 낫)
        {
            if (낫 == null) return;
            낫.enabled = false;
            var agent = 낫.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        }

        static void 깨운다(CreatureBrain 낫, Vector3 시험점)
        {
            if (낫 == null) return;
            if (자리를_찾는다(시험점, _소환거리, out var spawn))
            {
                var agent = 낫.GetComponent<NavMeshAgent>();
                if (agent != null) agent.Warp(spawn);
            }
            낫.enabled = true;
            var a = 낫.GetComponent<NavMeshAgent>();
            if (a != null && a.isOnNavMesh) a.isStopped = false;
        }

        static void 치운다(CreatureBrain 낫)
        {
            if (낫 != null) Object.Destroy(낫.gameObject);
            foreach (var m in GlowMarker.Active.ToArray())
                if (m != null) Object.Destroy(m.gameObject);
        }

        static float 거리(CreatureBrain 낫) => Vector3.Distance(낫.transform.position, PlayerPos);

        static bool 덤비는중(CreatureBrain 낫) =>
            낫.State == CreatureState.Chase || 낫.State == CreatureState.Attack;

        static IEnumerator 켜져_있으면_낫이_들어오지_못한다(CreatureBrain 낫, Vector3 시험점)
        {
            yield return 선다(시험점);
            E2EHarness.Assert(LitZoneRegistry.IsLit(PlayerPos), "플레이어가 켜진 군락 안에 서 있다");

            float 처음 = 거리(낫);
            float 가장가까이 = 처음;
            bool 덤빔 = false;

            float t = 0f;
            while (t < 5f)
            {
                yield return 선다(시험점);   // 밀려나지 않게 자리를 지킨다
                if (덤비는중(낫)) 덤빔 = true;
                가장가까이 = Mathf.Min(가장가까이, Vector3.Distance(낫.transform.position, 시험점));
                t += Time.deltaTime;
                yield return null;
            }

            // 이 값이 다음 단계의 대조군이다 — 빛이 켜져 있을 때 낫이 낼 수 있었던
            // 최선의 거리. 불을 끈 뒤 이보다 한참 안쪽까지 들어와야 "열렸다"가 성립한다.
            _켜졌을때_최소거리 = 가장가까이;

            E2EHarness.Log($"  켜진 동안: 상태 {낫.State}, 플레이어까지 최소 {가장가까이:F1}m");
            E2EHarness.Assert(!덤빔, $"감지 반경 안({처음:F1}m)이어도 빛이 막는다 (상태 {낫.State})");
        }

        // ── 따기 ────────────────────────────────────────────────

        static IEnumerator 갓을_딸수록_어두워진다(GlowGrove 군락)
        {
            int 시작수량 = Inv.CountOf(GlowCapCluster.CapItemId);
            int 전체 = 군락.Total;
            float 기준세기 = 군락.BaseIntensity;

            // 조준점은 미리 잡아 둔다. 따고 나면 렌더러가 꺼져 bounds를 못 쓴다.
            var 갓들 = 군락.Caps.ToArray();
            var 조준점 = 갓들.Select(c => 중심(c)).ToArray();

            for (int i = 0; i < 갓들.Length; i++)
            {
                var 갓 = 갓들[i];
                int 이전수량 = Inv.CountOf(GlowCapCluster.CapItemId);

                yield return 딴다(갓, 조준점[i], $"{i + 1}번째 갓 무더기");

                E2EHarness.Assert(갓.IsHarvested, $"{i + 1}번째 갓 무더기를 땄다");
                E2EHarness.Assert(Inv.CountOf(GlowCapCluster.CapItemId) > 이전수량,
                                  $"발광 버섯 갓이 손에 들어왔다 ({이전수량} -> {Inv.CountOf(GlowCapCluster.CapItemId)})");

                int 남은 = 전체 - (i + 1);
                E2EHarness.AssertEqual(군락.Remaining, 남은, "남은 갓 무더기");

                float 기대세기 = 기준세기 * GlowGroveRule.Brightness(남은, 전체);
                E2EHarness.Log($"  남은 {남은}/{전체} → 세기 {군락.Light.intensity:F2} " +
                               $"(기대 {기대세기:F2}), 반경 {군락.Light.range:F1}m");
                E2EHarness.Assert(Mathf.Abs(군락.Light.intensity - 기대세기) < 0.01f,
                                  "빛 세기가 남은 갓 수에 비례한다");
                E2EHarness.Assert(Mathf.Abs(군락.LitZoneRadius - 군락.BaseRange * GlowGroveRule.Brightness(남은, 전체)) < 0.01f,
                                  "안전 반경도 같은 비율로 줄어든다 — 보이는 빛과 어긋나지 않는다");
            }

            E2EHarness.Assert(Inv.CountOf(GlowCapCluster.CapItemId) >= 시작수량 + 전체,
                              $"무더기마다 최소 하나씩 나왔다 ({Inv.CountOf(GlowCapCluster.CapItemId)}개)");

            // 실제 재생 시간이 걸려 있는지 확인하고 나서 줄인다.
            // 여기서 상수를 확인하지 않으면 시나리오가 무엇을 줄였는지 알 수 없다.
            var 하나 = 군락.Caps[0];
            E2EHarness.Assert(하나.RegrowRemaining > GlowGroveRule.RegrowSeconds - 60f,
                              $"딴 갓은 규칙대로 {GlowGroveRule.RegrowSeconds}초 뒤에 돌아온다 " +
                              $"(남은 {하나.RegrowRemaining:F0}초)");
        }

        /// <summary>
        /// 갓 무더기 하나를 실제로 딴다 — 겨누고, E를 홀드 시간만큼 누르고 있는다.
        ///
        /// 홀드하는 동안 발을 붙들어 둔다. 버섯 덩이는 반경이 2m에 이르러 곁에 서면
        /// 캐릭터 컨트롤러가 몸을 밀어내고, 밀리면 조준이 끊겨 홀드가 취소된다
        /// (실제로 겪었다). 사람이 캘 때는 제자리에 서 있으므로 그 상태를 재현한다.
        /// 홀드 시간 자체는 그대로 채운다.
        /// </summary>
        static IEnumerator 딴다(GlowCapCluster 갓, Vector3 조준점, string 무엇)
        {
            var it = Ctx.Interactor;

            for (int 시도 = 0; 시도 < 3 && !갓.IsHarvested; 시도++)
            {
                yield return 조준한다(갓, 조준점, 무엇);
                E2EHarness.Log("  프롬프트: " + it.Current.InteractionPrompt);

                Vector3 선자리 = PlayerPos;
                yield return E2EHarness.PressKey(Key.E);

                float t = 0f;
                while (t < 갓.HoldDuration + 0.5f && !갓.IsHarvested)
                {
                    E2EHarness.QueueKeys();
                    E2EHarness.Teleport(선자리);
                    t += Time.deltaTime;
                    yield return null;
                }

                yield return E2EHarness.ReleaseKey(Key.E);
                yield return null;

                if (!갓.IsHarvested)
                    E2EHarness.Log($"  {무엇}: {시도 + 1}번째 홀드가 끝나지 않았다 — 다시 겨눈다");
            }
        }

        static Vector3 중심(GlowCapCluster 갓)
        {
            var rend = 갓.GetComponentInChildren<Renderer>();
            return rend != null ? rend.bounds.center : 갓.transform.position + Vector3.up;
        }

        /// <summary>
        /// 조준점에서 <paramref name="거리"/>만큼 떨어진 <b>걸을 수 있는 자리</b>에 서서 바라본다.
        ///
        /// 발밑을 위에서 쏜 광선으로 정하면 안 된다 — 부유섬의 바위와 천장이 머리 위를
        /// 덮고 있어서, 30m 위에서 쏘면 지면이 아니라 그 천장의 윗면을 짚는다.
        /// 실제로 플레이어가 군락보다 11m 높은 곳에 서서 아무것도 조준하지 못했다.
        /// 그래서 NavMesh에 먼저 묻고, 없으면 조준점 바로 위에서 짧게 쏜다.
        /// </summary>
        static IEnumerator 앞에_선다(Vector3 조준점, float 거리)
        {
            Vector3 d = PlayerPos - 조준점;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) d = Vector3.back;

            yield return 자리로(조준점 + d.normalized * 거리, 조준점);
        }

        /// <summary>
        /// 원하는 자리의 <b>발밑</b>을 찾아 서서 조준점을 바라본다.
        ///
        /// 발밑 찾기가 두 번 어긋났고, 그 둘이 이 함수의 모양을 정했다.
        /// <list type="number">
        /// <item>NavMesh에 물으면 안 된다 — 군락 언저리에서는 걸을 수 있는 면이 몇 미터 밖에
        ///   있어서 대상에서 4m 넘게 끌려가고 조준 거리(3m)를 벗어난다(실측 2.6m 이동).</item>
        /// <item>가장 가까운 광선 충돌을 쓰면 안 된다 — 부유섬의 바위와 섬 밑면이
        ///   버섯 위를 덮고 있어서 그 <b>윗면</b>을 짚는다(실측 지면보다 2m 높은 곳에 섰다).</item>
        /// </list>
        /// 그래서 <b>조준점보다 아래에 있는 면 중 가장 높은 것</b>을 발밑으로 삼는다.
        /// 갓의 중심은 언제나 그 갓이 자란 땅보다 위에 있으므로 이 조건이 지면을 고른다.
        /// </summary>
        static IEnumerator 자리로(Vector3 원하는자리, Vector3 조준점)
        {
            Vector3 자리 = 원하는자리;
            var hits = Physics.RaycastAll(new Vector3(자리.x, 조준점.y + 4f, 자리.z), Vector3.down,
                                          24f, ~0, QueryTriggerInteraction.Ignore);

            float 발밑 = float.MinValue;
            for (int i = 0; i < hits.Length; i++)
                if (hits[i].point.y <= 조준점.y && hits[i].point.y > 발밑) 발밑 = hits[i].point.y;

            if (발밑 > float.MinValue) 자리.y = 발밑 + 1.0f;
            else if (NavMesh.SamplePosition(new Vector3(자리.x, 조준점.y, 자리.z), out var nav, 2f,
                                            NavMesh.AllAreas))
                자리 = nav.position + Vector3.up * 1.0f;

            yield return 선다(자리);
            E2EHarness.LookAt(조준점);
            yield return null;
            yield return null;
        }

        /// <summary>
        /// 대상이 실제로 조준될 때까지 둘레를 돌며 자리를 바꿔 본다.
        ///
        /// 한 자리에서만 서 보고 포기하면 안 된다 — 군락 바닥에는 고사리와 잔해가
        /// 함께 깔려 있어서, 방향에 따라 버섯보다 그쪽이 먼저 조준된다(실제로 걸렸다).
        /// 사람도 그럴 때 옆으로 한 발 옮겨 다시 겨눈다. 홀드 시간은 그대로 채우므로
        /// 검증을 건너뛰는 것이 아니다.
        /// </summary>
        static IEnumerator 조준한다(Component 대상, Vector3 조준점, string 무엇,
                                   float[] 거리들 = null)
        {
            var it = Ctx.Interactor;
            거리들 = 거리들 ?? new[] { 2.8f, 2.4f, 3.2f };

            Vector3 기준 = PlayerPos - 조준점;
            기준.y = 0f;
            if (기준.sqrMagnitude < 0.01f) 기준 = Vector3.back;
            기준.Normalize();

            var 흔적 = new List<string>();

            for (int r = 0; r < 거리들.Length; r++)
            {
                for (int a = 0; a < 8; a++)
                {
                    float 각 = (a % 2 == 0 ? 1f : -1f) * 45f * ((a + 1) / 2);
                    Vector3 dir = Quaternion.Euler(0f, 각, 0f) * 기준;

                    yield return 자리로(조준점 + dir * 거리들[r], 조준점);

                    for (int f = 0; f < 4; f++)
                    {
                        if (ReferenceEquals(it.Current, 대상))
                        {
                            E2EHarness.Assert(true, $"{무엇}가 조준된다 " +
                                                    $"({거리들[r]:F1}m, {각:F0}°)");
                            yield break;
                        }
                        yield return null;
                    }

                    흔적.Add($"{거리들[r]:F1}m/{각:F0}° 자리 {PlayerPos.ToString("F1")} " +
                             $"조준거리 {Vector3.Distance(E2EHarness.Eye.transform.position, 조준점):F1}m " +
                             $"→ {(it.Current == null ? "없음" : it.Current.GetType().Name)}");
                }
            }

            throw new System.Exception(
                $"단언 실패: {무엇}를 어느 방향에서도 조준하지 못했다 " +
                $"(조준점 {조준점.ToString("F1")})\n    " + string.Join("\n    ", 흔적));
        }

        /// <summary>
        /// 시험점을 아직도 밝히고 있는 다른 군락이 있으면 그것들도 딴다.
        ///
        /// 씬의 군락 둘은 서로 반경이 겹친다. 하나만 꺼서는 그 자리가 어두워지지 않고,
        /// 그 사실을 모른 채 "낫이 안 들어온다"를 보면 엉뚱한 곳을 고치게 된다.
        /// </summary>
        static IEnumerator 남은_빛도_전부_끈다(Vector3 시험점)
        {
            for (int 회 = 0; 회 < 4 && LitZoneRegistry.IsLit(시험점); 회++)
            {
                var 겹친군락 = GlowGrove.Active.FirstOrDefault(
                    g => g.Remaining > 0 &&
                         Vector3.Distance(g.LitZoneCenter, 시험점) <= g.LitZoneRadius);
                if (겹친군락 == null) break;

                E2EHarness.Log($"  {겹친군락.name}의 빛이 아직 시험점에 닿는다 — 이쪽도 딴다");
                yield return 갓을_딸수록_어두워진다(겹친군락);
            }
        }

        static IEnumerator 다_따면_꺼지고_충전도_멈춘다(GlowGrove 군락, Vector3 시험점)
        {
            yield return 선다(시험점);

            E2EHarness.AssertEqual(군락.Remaining, 0, "남은 갓");
            E2EHarness.Assert(!군락.Light.enabled, "군락의 빛이 꺼졌다");
            E2EHarness.Assert(!LitZoneRegistry.IsLit(시험점),
                              "꺼진 군락은 밝은 구역에서 빠진다 — 낫에게 길이 열린다");

            var zone = 구역(군락);
            E2EHarness.Assert(zone != null && Mathf.Approximately(zone.CurrentRechargePerSecond, 0f),
                              "빛이 없으니 배터리도 채워 주지 않는다 — 자원과 안전을 맞바꾼 대가");
        }

        // ── 낫이 들어온다 ───────────────────────────────────────

        static IEnumerator 꺼지면_낫이_실제로_들어온다(CreatureBrain 낫, GlowGrove 군락, Vector3 시험점)
        {
            yield return 선다(시험점);
            E2EHarness.Assert(!LitZoneRegistry.IsLit(PlayerPos), "플레이어 자리가 어둡다");

            float 처음 = Vector3.Distance(낫.transform.position, 시험점);
            yield return 붙어서_기다린다(낫, 시험점, () => 덤비는중(낫), "불이 꺼지자 낫이 덤빈다", 6f);

            // 상태만 바뀌고 제자리면 "들어온다"가 아니다. 꺼진 군락 한복판에 선
            // 플레이어를 <b>때릴 수 있는 거리</b>까지 실제로 걸어 들어오는지 본다.
            float 문턱 = _낫정의.attackRange + 1.5f;
            yield return 붙어서_기다린다(
                낫, 시험점,
                () => Vector3.Distance(낫.transform.position, 시험점) <= 문턱,
                $"꺼진 군락 안, 플레이어 코앞({문턱:F1}m)까지 걸어 들어왔다", 15f);

            float 지금 = Vector3.Distance(낫.transform.position, 시험점);
            E2EHarness.Log($"  낫: 플레이어까지 {처음:F1}m -> {지금:F1}m (상태 {낫.State}), " +
                           $"켜져 있을 때 최선 {_켜졌을때_최소거리:F1}m");
            E2EHarness.Assert(지금 < _켜졌을때_최소거리 - 2f,
                              $"빛이 있을 때보다 한참 안쪽이다 " +
                              $"({_켜졌을때_최소거리:F1}m -> {지금:F1}m)");
            E2EHarness.Assert(Vector3.Distance(낫.transform.position, 군락.LitZoneCenter) < 군락.BaseRange,
                              $"예전 빛이 닿던 반경({군락.BaseRange:F0}m) 안이다");
        }

        /// <summary>자리를 지키면서 조건을 기다린다. 낫이 밀어 대므로 매 프레임 다시 세운다.</summary>
        static IEnumerator 붙어서_기다린다(CreatureBrain 낫, Vector3 자리,
                                          System.Func<bool> 조건, string 무엇, float 제한)
        {
            float t = 0f;
            while (t < 제한)
            {
                if (조건()) { E2EHarness.Assert(true, 무엇); yield break; }
                if (Vector3.Distance(PlayerPos, 자리) > 1.2f) yield return 선다(자리);
                // 맞아 죽으면 리스폰이 자리를 옮겨 판정이 흐트러진다. 살려 둔다.
                Ctx.Vitals.Health.Modify(Ctx.Vitals.Health.Max);
                t += Time.deltaTime;
                yield return null;
            }
            throw new System.TimeoutException(
                $"기다리기 실패: {무엇} ({제한}초 초과, 상태 {낫.State}, " +
                $"거리 {Vector3.Distance(낫.transform.position, 자리):F1}m)");
        }

        // ── 표식 ────────────────────────────────────────────────

        static IEnumerator 놓아둔_갓은_낫을_막지_못한다(CreatureBrain 낫, Vector3 시험점)
        {
            var bar = Object.FindAnyObjectByType<QuickSlotBar>(FindObjectsInactive.Include);
            E2EHarness.Assert(bar != null, "퀵슬롯이 있다");

            int 칸 = bar.Entries.ToList().FindIndex(e => e != null && e.id == GlowMarker.ItemId);
            E2EHarness.Assert(칸 >= 0, $"발광 버섯 갓이 퀵슬롯 {칸 + 1}번에 올라와 있다");

            int 이전수량 = Inv.CountOf(GlowCapCluster.CapItemId);
            int 이전표식 = GlowMarker.Active.Count;

            yield return 선다(시험점);
            yield return E2EHarness.TapKey((Key)((int)Key.Digit1 + 칸));
            yield return null;
            yield return null;

            E2EHarness.AssertEqual(GlowMarker.Active.Count, 이전표식 + 1, "발밑에 표식이 놓였다");
            E2EHarness.AssertEqual(Inv.CountOf(GlowCapCluster.CapItemId), 이전수량 - 1,
                                   "인벤토리에서 갓 하나가 빠졌다");

            var 표식 = GlowMarker.Active[GlowMarker.Active.Count - 1];
            E2EHarness.Assert(표식.Light != null && 표식.Light.enabled, "표식이 스스로 빛난다");
            E2EHarness.Assert(표식.Light.color == Survive.Domain.Art.ArtPalette.Glowshroom,
                              "표식의 빛은 광원 4색 중 발광 버섯 색이다");
            E2EHarness.Log($"  표식: {표식.transform.position.ToString("F1")}, " +
                           $"세기 {표식.Light.intensity:F2}, 반경 {표식.Light.range:F1}m " +
                           $"(플레이어와 {Vector3.Distance(표식.transform.position, PlayerPos):F2}m)");
            E2EHarness.Assert(Vector3.Distance(표식.transform.position, PlayerPos) < 2.5f,
                              "표식은 발밑에 놓인다");

            // 여기가 이 기능의 핵심 계약이다.
            E2EHarness.Assert(!LitZoneRegistry.IsLit(표식.transform.position),
                              "표식은 밝은 구역으로 등록되지 않는다 — 배터리 압박을 우회할 수 없다");

            // 규칙만이 아니라 몸으로도 확인한다. 표식 위에 서 있어도 낫은 계속 온다.
            float t = 0f;
            bool 계속덤빈다 = false;
            while (t < 3f)
            {
                if (덤비는중(낫)) 계속덤빈다 = true;
                if (Vector3.Distance(PlayerPos, 시험점) > 1.2f) yield return 선다(시험점);
                Ctx.Vitals.Health.Modify(Ctx.Vitals.Health.Max);
                t += Time.deltaTime;
                yield return null;
            }
            E2EHarness.Assert(계속덤빈다,
                              $"표식을 깔고 서 있어도 낫은 물러나지 않는다 (상태 {낫.State})");
        }

        static IEnumerator 놓아둔_갓을_회수한다()
        {
            var 표식 = GlowMarker.Active.LastOrDefault();
            E2EHarness.Assert(표식 != null, "회수할 표식이 있다");
            if (표식 == null) yield break;

            var 자리 = 표식.transform;
            int 이전수량 = Inv.CountOf(GlowCapCluster.CapItemId);

            yield return 조준한다(표식, 자리.position, "놓아둔 표식", new[] { 1.6f, 1.2f, 2.1f });

            var it = Ctx.Interactor;
            E2EHarness.Log("  프롬프트: " + it.Current.InteractionPrompt);

            yield return E2EHarness.TapKey(Key.E);
            yield return null;
            yield return null;

            E2EHarness.AssertEqual(Inv.CountOf(GlowCapCluster.CapItemId), 이전수량 + 1,
                                   "E 한 번에 갓이 돌아왔다");
            E2EHarness.Assert(표식 == null || 표식.gameObject == null,
                              "회수한 표식은 세계에서 사라진다");
        }

        // ── 재생 ────────────────────────────────────────────────

        static IEnumerator 다시_자라면_빛이_돌아온다(GlowGrove 군락, Vector3 시험점)
        {
            E2EHarness.Assert(!LitZoneRegistry.IsLit(시험점), "아직은 어둡다");

            foreach (var g in GlowGrove.Active) g.OverrideRegrowSeconds(검증용재생초);
            E2EHarness.Log($"  재생 시간을 검증용 {검증용재생초}초로 줄인다 " +
                           $"(규칙은 그대로, 상수만 — 실제 값 {GlowGroveRule.RegrowSeconds}초)");

            yield return E2EHarness.WaitUntil(() => 군락.Remaining == 군락.Total,
                                              "갓이 전부 다시 자랐다", 검증용재생초 + 5f);

            E2EHarness.Assert(군락.Light.enabled, "빛이 돌아왔다");
            E2EHarness.Assert(Mathf.Approximately(군락.Light.intensity, 군락.BaseIntensity),
                              $"세기가 원래대로다 ({군락.Light.intensity:F2})");
            E2EHarness.Assert(LitZoneRegistry.IsLit(시험점), "다시 밝은 구역이 됐다");

            var zone = 구역(군락);
            E2EHarness.Assert(zone != null && zone.CurrentRechargePerSecond > 0f,
                              "무료 충전도 돌아왔다");
        }

        static IEnumerator 다시_자라면_낫은_또_막힌다(CreatureBrain 낫, Vector3 시험점)
        {
            yield return 선다(시험점);
            E2EHarness.Assert(LitZoneRegistry.IsLit(PlayerPos), "플레이어 자리가 다시 밝다");

            float t = 0f;
            bool 덤빔 = false;
            while (t < 4f)
            {
                if (덤비는중(낫)) 덤빔 = true;
                if (Vector3.Distance(PlayerPos, 시험점) > 1.2f) yield return 선다(시험점);
                t += Time.deltaTime;
                yield return null;
            }

            E2EHarness.Log($"  재생 뒤: 상태 {낫.State}, 거리 {거리(낫):F1}m");
            E2EHarness.Assert(!덤빔, "빛이 돌아오자 낫은 다시 들어오지 못한다 — 영구 개방이 아니다");
        }
    }
}
