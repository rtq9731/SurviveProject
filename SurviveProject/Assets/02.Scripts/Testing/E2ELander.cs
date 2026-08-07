using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using Survive.Building;
using Survive.Core;
using Survive.Vitals;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// 착륙선 — 첫 거점이자 최종 개조 대상(기획서 §5.16).
    ///
    /// <b>배는 아직 씬에 없다.</b> 모델도 자리도 사람의 몫이고 지상 지형은
    /// 통째로 다시 만들어진다(기획서 §13). 그래서 여기서는 <b>임시로 세운 배</b>를
    /// 놓고 규칙과 배선만 본다 — 진짜 배치를 기다린다.
    ///
    /// <b>임시라도 검사가 무의미해지지는 않는다.</b> 이 시나리오가 보는 것은
    /// 「배가 어디 있느냐」가 아니라 <b>배가 있을 때 부활·저장이 어떻게 갈리느냐</b>이고,
    /// 그것은 좌표와 무관하다. 오히려 임시로 세우는 편이 낫다 — 배를 죽은 자리
    /// <b>코앞</b>에 놓아도 타는 화톳불이 이긴다는 것을 실제로 볼 수 있다.
    ///
    /// <see cref="E2ERespawn"/>이 「화톳불이 없으면 시작 지점」을 보고,
    /// 여기서는 <b>그 「없으면」의 자리에 배가 들어온 뒤</b>를 본다.
    /// </summary>
    public static class E2ELander
    {
        /// <summary>이 시나리오가 세운 임시 배. 끝나면 거둔다.</summary>
        static LanderBase _임시배;

        public static IEnumerator FullRun()
        {
            yield return Prepare();

            try
            {
                yield return 화톳불이_없으면_착륙선에서_일어선다();
                yield return 전진_기지가_있으면_거기서_일어선다();
            }
            finally
            {
                거둔다();
            }

            E2EHarness.Log("=== 착륙선 완주 ===");
        }

        // ── 준비 ────────────────────────────────────────────────

        static IEnumerator Prepare()
        {
            var dir = Object.FindAnyObjectByType<Survive.Progression.ChapterDirector>(
                FindObjectsInactive.Exclude);
            yield return E2EHarness.WaitUntil(() => dir != null && dir.Current != null,
                                              "챕터가 시작된다", 8f);

            E2EHarness.Assert(RespawnService.Instance != null, "부활 서비스가 스스로 서 있다");
            E2EHarness.Assert(RespawnService.Instance.HasStartPoint, "시작 지점을 기억하고 있다");

            // 배가 없는 세계가 출발선이다. 있으면 앞 판이 남긴 임시 배이므로
            // 그것부터 거둬야 "배가 들어와서 달라졌다"를 잴 수 있다.
            거둔다();
            E2EHarness.AssertEqual(LanderBase.Active.Count, 0, "출발선에는 배가 없다");
            E2EHarness.Assert(!LanderBase.CurrentHome().Standing,
                              "배가 없으면 본거지도 없다고 답한다");

            E2EHarness.Assert(타는화톳불() == 0, "아직 세운 화톳불이 없다");
        }

        static void 거둔다()
        {
            for (int i = LanderBase.Active.Count - 1; i >= 0; i--)
            {
                var l = LanderBase.Active[i];
                if (l != null) Object.DestroyImmediate(l.gameObject);
            }
            _임시배 = null;
        }

        static int 타는화톳불() =>
            Object.FindObjectsByType<Campfire>(FindObjectsInactive.Exclude).Count(c => c.IsBurning);

        static float 평면거리(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        /// <summary>
        /// 임시 배를 세운다. 지면 위에 올려 두는 것은 부활한 몸이 지면 위에 서는지까지
        /// 보기 위해서다 — 공중에 뜬 배를 기준으로 재면 낙하가 결과를 흐린다.
        /// </summary>
        static LanderBase 임시_착륙선을_세운다(Vector3 at)
        {
            if (Physics.Raycast(at + Vector3.up * 30f, Vector3.down, out var hit, 80f,
                                ~0, QueryTriggerInteraction.Ignore))
                at.y = hit.point.y;

            var go = new GameObject("E2E_TempLander");
            go.transform.position = at;

            // 일어설 자리를 따로 꽂지 않는다. 그것은 사람이 배를 놓을 때 붙이는 것이고,
            // 비어 있을 때의 기본값(배 앞으로 물러선 자리)이 오히려 검사 대상이다 —
            // 프리팹에 자리를 안 꽂아 두면 배 속에서 일어서는 사고가 여기서 잡힌다.
            var lander = go.AddComponent<LanderBase>();

            E2EHarness.Log($"  임시 착륙선 {go.transform.position.ToString("F1")} " +
                           $"(일어설 자리 {lander.HomeStandPoint.ToString("F1")})");
            return lander;
        }

        // ── 1. 화톳불이 없으면 착륙선 ───────────────────────────

        static IEnumerator 화톳불이_없으면_착륙선에서_일어선다()
        {
            E2EHarness.Log("— 배만 있는 채로 죽는다 —");

            var 시작 = RespawnService.Instance.StartPoint;
            _임시배 = 임시_착륙선을_세운다(시작);
            yield return null;

            E2EHarness.Assert(LanderBase.Current == _임시배, "세운 배가 본거지로 잡힌다");
            E2EHarness.Assert(LanderBase.CurrentHome().Standing, "배가 서 있다고 답한다");

            var 설자리 = _임시배.HomeStandPoint;

            E2ERespawn.채운다();
            yield return E2ERespawn.멀어진다(설자리, 14f);

            float 떠난거리 = 평면거리(E2EHarness.Player.transform.position, 설자리);
            E2EHarness.Assert(떠난거리 > 6f, $"배에서 충분히 떠났다 ({떠난거리:F1}m)");

            yield return 죽는다();
            yield return 부활을_기다린다();

            E2EHarness.AssertEqual(RespawnService.LastRespawnSite, RespawnSite.Home,
                                   "착륙선에서 일어섰다고 보고한다");
            E2EHarness.Assert(!RespawnService.LastRespawnWasCampfire,
                              "화톳불은 아니었다고도 함께 보고한다");

            float 배에서 = 평면거리(E2EHarness.Player.transform.position, 설자리);
            E2EHarness.Log($"  부활 {E2EHarness.Player.transform.position.ToString("F1")}, " +
                           $"배가 정한 자리에서 {배에서:F1}m");
            E2EHarness.Assert(배에서 < 2f, $"배가 정한 자리에서 되살아났다 ({배에서:F1}m)");

            // 배 안이 아니라 배 곁이다. 원점에서 재면 콜라이더 속이다.
            float 배원점에서 = 평면거리(E2EHarness.Player.transform.position,
                                        _임시배.transform.position);
            E2EHarness.Assert(배원점에서 > 1.5f,
                              $"배 안이 아니라 배 곁에 선다 ({배원점에서:F1}m)");
        }

        // ── 2. 전진 기지가 있으면 거기서 ────────────────────────

        /// <summary>
        /// <b>이 대목이 이 라운드의 알맹이다.</b> 배를 죽은 자리 <b>코앞</b>에 두고
        /// 화톳불은 멀리 둔다. 규칙이 거리로 재고 있으면 여기서 배가 이기고,
        /// 그 순간 「목재 재고가 곧 안전 재고」(기획서 §5.3)가 조용히 사라진다 —
        /// 호수 근방에서는 화톳불을 아무리 세워도 소용이 없어지기 때문이다.
        /// </summary>
        static IEnumerator 전진_기지가_있으면_거기서_일어선다()
        {
            E2EHarness.Log("— 배를 코앞에 두고, 멀리 세운 화톳불에서 죽는다 —");

            E2EHarness.Assert(_임시배 != null, "앞 대목에서 세운 배가 남아 있다");
            if (_임시배 == null) yield break;

            E2ERespawn.채운다();

            // 배에서 멀리 나가 거기에 화톳불을 세운다.
            yield return E2ERespawn.멀어진다(_임시배.HomeStandPoint, 16f);

            Campfire 불 = null;
            yield return E2ERespawn.화톳불을_세운다(r => 불 = r);
            if (불 == null) yield break;

            var 불자리 = 불.transform.position;
            E2EHarness.Assert(불.IsBurning, "세운 화톳불이 타고 있다");
            E2EHarness.Assert(평면거리(불자리, _임시배.HomeStandPoint) > 8f,
                              "불과 배가 충분히 떨어져 있다 — 좌표로 가릴 수 있어야 한다");

            // 배 코앞으로 돌아가서 죽는다. 거리로 재는 규칙이면 배가 이기는 배치다.
            E2ERespawn.채운다();
            var 배코앞 = _임시배.HomeStandPoint;
            E2EHarness.Teleport(배코앞 + Vector3.up * 0.5f);
            yield return null;

            float 죽은자리에서_배까지 = 평면거리(E2EHarness.Player.transform.position, 배코앞);
            float 죽은자리에서_불까지 = 평면거리(E2EHarness.Player.transform.position, 불자리);
            E2EHarness.Log($"  죽는 자리 — 배까지 {죽은자리에서_배까지:F1}m, " +
                           $"불까지 {죽은자리에서_불까지:F1}m");
            E2EHarness.Assert(죽은자리에서_배까지 < 죽은자리에서_불까지,
                              "배 쪽이 가깝다 — 거리로 재는 규칙이면 배가 이길 배치다");

            yield return 죽는다();
            yield return 부활을_기다린다();

            E2EHarness.AssertEqual(RespawnService.LastRespawnSite, RespawnSite.ForwardCamp,
                                   "가까운 배가 아니라 전진 기지에서 일어섰다");

            float 불에서 = 평면거리(E2EHarness.Player.transform.position, 불자리);
            float 배에서 = 평면거리(E2EHarness.Player.transform.position, 배코앞);
            E2EHarness.Log($"  부활 — 불에서 {불에서:F1}m, 배에서 {배에서:F1}m");
            E2EHarness.Assert(불에서 < 3.5f, $"화톳불 곁에서 되살아났다 ({불에서:F1}m)");
            E2EHarness.Assert(불에서 < 배에서, "더 가까운 배를 지나쳐 불로 갔다");
        }

        // ── 공통 동작 ───────────────────────────────────────────

        static IEnumerator 죽는다()
        {
            var vitals = E2EHarness.Player.Vitals;
            vitals.Health.Modify(-(vitals.Health.Max + 10f));
            E2EHarness.Assert(vitals.Health.IsEmpty, "체력이 0이 됐다");
            yield return null;
        }

        static IEnumerator 부활을_기다린다()
        {
            int 이전 = RespawnService.RespawnCount;

            yield return E2EHarness.WaitUntil(() => RespawnService.RespawnCount > 이전,
                                              "잠깐 뒤 스스로 되살아난다", 6f);
            yield return null;

            E2EHarness.Assert(!E2EHarness.Player.Vitals.Health.IsEmpty, "체력이 돌아왔다");
        }

        // ── 3. 저장 왕복 ────────────────────────────────────────

        /// <summary>
        /// 착륙선의 상태가 저장을 넘어간다.
        ///
        /// <b>실제로 파일을 쓰고 다시 읽는다.</b> 코드로 <c>CaptureState</c>와
        /// <c>RestoreState</c>를 이어 붙여 보는 것으로는 직렬화도, 열쇠 찾기도,
        /// 파괴된 대상이 새 대상을 가리는 문제도 잡히지 않는다.
        ///
        /// <b>슬롯을 따로 쓴다.</b> 세 클론과 메인 에디터가 같은 저장 경로를
        /// 나눠 쓰므로(<c>LocalLow/DefaultCompany/SurviveProject</c>), 기본 슬롯을
        /// 건드리면 동시에 도는 다른 갈래와 사람이 하던 게임을 밟는다.
        ///
        /// <b>무엇이 저장되는가.</b> 개조 장부와 처리장치가 모아 둔 양이다.
        /// 자리는 씬이 가지므로 적지 않는다(<see cref="LanderBase.State"/>).
        /// 처리장치의 속도는 수분 축이 정할 값이라 여기서 <b>검사가 넣어 준다</b> —
        /// 게임에 박아 두는 값이 아니다.
        /// </summary>
        public static IEnumerator SaveRoundTrip()
        {
            const string 슬롯 = "lander_e2e";
            const float 초당 = 4f;
            const float 통 = 50f;

            var coordinator = Object.FindAnyObjectByType<SaveCoordinator>(FindObjectsInactive.Exclude);
            E2EHarness.Assert(coordinator != null, "SaveCoordinator가 있다");
            if (coordinator == null) yield break;

            E2EHarness.Log($"  저장 경로: {SaveService.Root}");
            E2EHarness.Log($"  슬롯: {슬롯} (기본 슬롯을 건드리지 않는다)");

            거둔다();

            var 배 = 임시_착륙선을_세운다(RespawnService.Instance.StartPoint);
            _임시배 = 배;
            yield return null;

            try
            {
                // ── 값이 없으면 아무것도 모이지 않는다 ──────────
                E2EHarness.Assert(!배.Configured, "속도가 안 들어왔으면 처리장치는 서 있다");
                yield return new WaitForSeconds(0.3f);
                E2EHarness.AssertEqual(배.Pooled, 0f, "값이 없는 동안에는 한 방울도 안 모인다");

                // ── 개조 축은 열려만 있다 ───────────────────────
                foreach (var s in LanderRefit.Systems)
                    E2EHarness.AssertEqual(배.Refit.TryComplete(s), RefitVerdict.NotInThisRelease,
                                           $"{s}는 이 릴리스에서 열리지 않는다");
                E2EHarness.Assert(!배.Refit.IsSpacefaring, "배는 아직 성간 이동을 못 한다");

                // ── 값을 넣고 물을 모은다 ───────────────────────
                배.Configure(초당, 통);
                E2EHarness.Assert(배.Configured, "값이 들어오면 처리장치가 돈다");

                yield return E2EHarness.WaitUntil(() => 배.Pooled > 1f,
                                                  "처리장치가 물을 모은다", 5f);

                // ── 저장한다 ────────────────────────────────────
                // 적어 두는 값은 <b>저장한 바로 그 프레임</b>에 읽는다. 한 프레임이라도
                // 벌어지면 그 사이에 처리장치가 더 모으고, 그러면 "왕복이 어긋났다"가
                // 아니라 "시간이 흘렀다"를 재게 된다.
                coordinator.Save(슬롯);
                float 모은것 = 배.Pooled;
                E2EHarness.Log($"  저장 시점에 모인 양 {모은것:F3} (초당 {초당}, 통 {통})");

                yield return null;
                E2EHarness.Assert(coordinator.HasSave(슬롯), "저장본이 생겼다");

                string 파일 = E2EHarness.SlotPath(슬롯);
                E2EHarness.Assert(File.Exists(파일), "파일이 실제로 디스크에 있다");
                string 본문 = File.ReadAllText(파일);
                E2EHarness.Assert(본문.Contains(LanderBase.Key),
                                  $"저장본에 「{LanderBase.Key}」 칸이 적혔다");

                // ── 배를 갈아 끼운다 ────────────────────────────
                // 같은 객체에 되돌리면 "아무 일도 안 하고 통과"가 가능하다.
                // 새로 세운 배가 받아야 직렬화를 실제로 지나간 것이다.
                Object.DestroyImmediate(배.gameObject);
                yield return null;

                // 새 배에는 값을 넣지 않는다. 넣으면 불러오기 전에 스스로 모으기
                // 시작하고, 그러면 "복원된 것"과 "그 사이에 모은 것"이 섞인다.
                var 새배 = 임시_착륙선을_세운다(RespawnService.Instance.StartPoint);
                _임시배 = 새배;
                E2EHarness.AssertEqual(새배.Pooled, 0f, "새로 세운 배는 비어 있다");

                yield return null;
                E2EHarness.AssertEqual(새배.Pooled, 0f, "값이 없으니 기다려도 안 모인다");

                // ── 불러온다 ────────────────────────────────────
                coordinator.Collect();
                E2EHarness.Assert(coordinator.Load(슬롯), "저장본을 불러왔다");

                // 여기서도 프레임을 넘기지 않는다. 넘기면 이번엔 새 배가 모은다.
                float 복원된것 = 새배.Pooled;
                float 어긋남 = Mathf.Abs(복원된것 - 모은것);
                E2EHarness.Log($"  복원된 양 {복원된것:F3} (어긋남 {어긋남:F4})");
                E2EHarness.Assert(어긋남 < 0.001f,
                                  $"처리장치가 모아 둔 것이 그대로 돌아왔다 ({어긋남:F4})");
                E2EHarness.AssertEqual(새배.Refit.DoneCount, 0,
                                       "개조 장부는 빈 채로 왕복한다 — 채운 것이 없으니 돌아올 것도 없다");

                // 받아 가는 것까지 본다. 모으기만 하고 못 꺼내면 저장한 보람이 없다.
                float 받은것 = 새배.Draw(1f);
                E2EHarness.AssertEqual(받은것, 1f, "통에서 실제로 받아 간다");
                E2EHarness.Assert(Mathf.Abs(새배.Pooled - (복원된것 - 1f)) < 0.0001f,
                                  "받아 간 만큼 줄었다");
            }
            finally
            {
                // 내가 만든 슬롯만 지운다. 기본 슬롯은 건드리지 않았다.
                coordinator.Delete(슬롯);
                거둔다();
            }

            E2EHarness.Log("=== 착륙선이 저장을 넘겼다 ===");
        }
    }
}
