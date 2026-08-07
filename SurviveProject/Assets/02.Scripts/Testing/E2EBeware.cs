using System.Collections;
using UnityEngine;
using Survive.Building;
using Survive.Creatures;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// <b>화톳불로 걸어 들어가면 낫이 따라붙기를 푼다</b> — 이 라운드의 회귀선.
    ///
    /// <b>왜 이것을 따로 재는가.</b> 2026-08-07에 「이만큼 센 고정 광원은 밝은
    /// 구역이다」라는 자동 등록 규칙이 섰고, 그 규칙의 유일한 대상이 시작 지점의
    /// 빛기둥이었다. 빛기둥을 없애면서 규칙도 함께 지웠는데, 낫이 따라붙기를 푸는
    /// 조건이 바로 <b>「고정 조명 접근」</b>이다. 그러니 물어야 할 것은 하나다 —
    /// <b>그 규칙이 없어도 화톳불이 그 노릇을 하는가.</b>
    ///
    /// 답은 「한다」여야 한다. 화톳불은 자기 주인(<see cref="Campfire"/>)이 직접
    /// <see cref="LitZoneRegistry"/>에 올리는 밝은 구역이라 자동 규칙과 무관하기
    /// 때문이다. 오히려 <b>플레이어가 지은 불만이 낫을 물린다</b>는 쪽이 설계에 맞는다.
    ///
    /// <b>무엇을 재고 무엇을 재지 않는가.</b> 전이는 <see cref="ScytheFsm"/>이
    /// 판정하고 그 입력은 <b>지금 도는 세계에서 실제로 읽는다</b> — 사람이 제 발로
    /// 걸어간 자리를 <see cref="LitZoneRegistry"/>에 물어서 만든다. 값을 손으로
    /// 세워 넣으면 이 시나리오는 EditMode 테스트를 한 번 더 쓴 것에 지나지 않는다.
    ///
    /// <b>살아 있는 낫 개체에 붙여 보지는 않는다.</b> 그 배선은 아직 서지 않았다 —
    /// 2026-08-07 기준 <see cref="ScytheFsm"/>을 부르는 런타임 코드가 하나도 없고,
    /// 그 일은 따로 돌고 있다. 배선이 서면 이 시나리오가 그 위로 옮겨 가야 한다.
    /// </summary>
    public static class E2EBeware
    {
        /// <summary>한 판에 이만큼 왕복한다. 한 번 통과한 것은 우연일 수 있다.</summary>
        public const int 왕복 = 3;

        /// <summary>
        /// 불을 놓을 거리. <b>가깝게 놓는다</b> — 사람이 이미 서 있는 자리 옆이라
        /// 지면이 평평하다는 것이 보장된다.
        ///
        /// <b>멀리 놓아 봤다가 한 번 잃었다.</b> 15m 앞에 놓았더니 그 자리가 6m
        /// 높은 벼랑 위였고, 사람이 4m를 남기고 열두 번 우회하다 시간이 끝났다
        /// (실측: 사람 y=51.5, 불 y=57.7, 중심까지 10.7m로 반경 10m 밖). 재려던
        /// 것은 <b>밝은 구역 판정</b>이지 길찾기가 아니므로, 어두운 자리는
        /// <b>걸어가서 찾는다</b>(아래 <see cref="어두운_자리를_찾는다"/>).
        /// </summary>
        const float 불까지 = 2.5f;

        /// <summary>어두운 자리를 찾을 때 밝은 구역 반경의 몇 배까지 물러나는가.</summary>
        const float 물러날배수 = 1.7f;

        public static IEnumerator Run()
        {
            // 시작 지점 일대는 발광 군락의 밝은 구역이 거의 덮고 있다. 그대로 두면
            // "불에서 멀어졌는데도 밝다"가 되어 이 시나리오가 아무것도 재지 못한다.
            int 뺀것 = E2EHarness.MuteAmbientLitZones();
            E2EHarness.Log($"  주변 광원 {뺀것}개를 잠시 뺐다");

            // 랜턴도 끈다. 랜턴은 <b>고정 조명이 아니므로</b> 따라붙기를 풀지 못하고,
            // 켜 두면 「불 안이라 풀렸다」와 「랜턴 때문에 밝다」가 섞인다.
            E2ECampfire.랜턴을_준다();
            var 램프 = E2EHarness.Lantern;
            if (램프 != null) 램프.SetSwitch(false);
            yield return null;

            var 시작자리 = E2EHarness.Player.transform.position;

            Campfire 불 = E2ECampfire.세운다(불까지);
            E2EHarness.Assert(불 != null, "화톳불을 세웠다");
            E2EHarness.Assert(불.IsBurning, "세운 불이 타고 있다");
            yield return null;

            var 불자리 = 불.LitZoneCenter;
            float 반경 = 불.LitZoneRadius;
            E2EHarness.Log($"  화톳불 {불자리.ToString("F1")} 반경 {반경:F1}m");
            E2EHarness.Assert(LitZoneRegistry.IsLit(시작자리),
                              "불을 세운 자리는 밝은 구역이다");

            // 어두운 자리를 <b>걸어가서</b> 찾는다. 계산으로 고르면 벼랑 위나
            // 물속이 나오고, 그러면 다음 걸음이 통째로 무너진다.
            Vector3 어두운자리 = Vector3.zero;
            yield return 어두운_자리를_찾는다(불자리, 반경, v => 어두운자리 = v);

            try
            {
                for (int 판 = 1; 판 <= 왕복; 판++)
                    yield return 한_판(불, 불자리, 어두운자리, 판);
            }
            finally
            {
                if (불 != null) Object.Destroy(불.gameObject);
                E2EHarness.RestoreAmbientLitZones();
            }
        }

        /// <summary>
        /// 불에서 멀어지는 쪽으로 <b>실제로 걸어가</b> 어두운 자리를 하나 잡는다.
        /// 여러 방향을 차례로 시도하고, 처음으로 밝은 구역을 벗어난 자리를 쓴다.
        /// 그 자리는 <b>걸어서 닿은 곳</b>이므로 되돌아오는 길도 있다.
        /// </summary>
        static IEnumerator 어두운_자리를_찾는다(Vector3 불자리, float 반경,
                                                System.Action<Vector3> 받는다)
        {
            float 거리 = 반경 * 물러날배수;
            var 방향들 = new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };

            foreach (var d in 방향들)
            {
                var 후보 = 불자리 + d * 거리;
                if (Physics.Raycast(후보 + Vector3.up * 40f, Vector3.down, out var hit, 200f,
                                    ~0, QueryTriggerInteraction.Ignore))
                    후보.y = hit.point.y + 1.0f;

                yield return E2EHarness.TryWalkTo(후보, 3f, 25f);
                yield return null;

                var 지금 = E2EHarness.Player.transform.position;
                if (!LitZoneRegistry.IsLit(지금))
                {
                    E2EHarness.Log($"  어두운 자리 {지금.ToString("F1")} " +
                                   $"(불에서 {Vector3.Distance(지금, 불자리):F1}m)");
                    받는다(지금);
                    yield break;
                }
            }

            throw new System.InvalidOperationException(
                $"불(반경 {반경:F1}m)에서 걸어 벗어날 수 있는 어두운 자리를 못 찾았다. " +
                "주변 광원을 뺐는데도 밝다면 뺀 목록을 의심하라");
        }

        static IEnumerator 한_판(Campfire 불, Vector3 불자리, Vector3 어두운자리, int 판)
        {
            E2EHarness.Log($"— {판}번째 왕복 —");

            // 불이 꺼지면 이 판은 아무 뜻이 없다.
            E2EHarness.Assert(불.IsBurning, $"{판}: 불이 아직 타고 있다");

            // ── 불 밖: 따라붙기가 풀리지 않는다 ──────────────────
            var 밖 = 지금_상황();
            float 밖거리 = Vector3.Distance(E2EHarness.Player.transform.position, 불자리);
            E2EHarness.Assert(!밖.PlayerNearFixedLight,
                              $"{판}: 불에서 {밖거리:F1}m 떨어진 자리는 어둡다");
            E2EHarness.AssertEqual(ScytheFsm.Next(ScytheState.Beware, 밖), ScytheState.Beware,
                                   $"{판}: 어두운 데서는 따라붙기가 풀리지 않는다");

            // ── 걸어 들어간다 ────────────────────────────────────
            // 되돌아오는 길이다 — 방금 걸어 나온 자리에서 출발하므로 길이 있다.
            yield return E2EHarness.TryWalkTo(불자리, 3f, 30f);
            yield return null;
            if (!LitZoneRegistry.IsLit(E2EHarness.Player.transform.position))
            {
                // 한 발짝이 모자란 경우가 있다. 더 좁게 한 번 더 다가간다.
                yield return E2EHarness.TryWalkTo(불자리, 1.2f, 20f);
                yield return null;
            }

            var 안 = 지금_상황();
            float 안거리 = Vector3.Distance(E2EHarness.Player.transform.position, 불자리);
            E2EHarness.Assert(안.PlayerNearFixedLight,
                              $"{판}: 걸어 들어간 자리(중심에서 {안거리:F1}m)가 밝은 구역이다");

            // ── 여기가 회귀선이다 ────────────────────────────────
            E2EHarness.AssertEqual(ScytheFsm.Next(ScytheState.Beware, 안), ScytheState.Patrol,
                                   $"{판}: 화톳불 접근으로 따라붙기가 풀린다");

            // 「풀린다」와 「닿지 못한다」는 다른 말이다. 둘 다 봐 두어야
            // 규칙이 반쪽만 산 채로 통과하지 않는다.
            E2EHarness.AssertEqual(ScytheFsm.Next(ScytheState.Attack, 안), ScytheState.Beware,
                                   $"{판}: 불 안에서는 붙어 있던 것도 물러난다");

            // ── 다시 걸어 나온다 ─────────────────────────────────
            yield return E2EHarness.TryWalkTo(어두운자리, 2.5f, 30f);
            yield return null;

            if (LitZoneRegistry.IsLit(E2EHarness.Player.transform.position))
            {
                // 걸어 나오지 못했으면 다음 판의 「밖」이 성립하지 않는다.
                // 조용히 통과시키지 않고 되돌린 뒤 그 사실을 적는다.
                E2EHarness.Log("  걸어 나오지 못했다 — 어두운 자리로 되돌린다");
                E2EHarness.Teleport(어두운자리);
                yield return null;
            }
        }

        /// <summary>
        /// 지금 세계에서 읽은 상황.
        ///
        /// <b>손으로 세우는 값은 둘뿐이다</b> — 「감지되고 있다」와 「따라잡고 있지는
        /// 않다」. 앞엣것은 이 시나리오가 따라붙기 상태를 전제로 하기 때문이고,
        /// 뒤엣것은 따라잡는 중이면 어두운 자리에서 <b>덤벼드는</b> 쪽으로 빠져나가
        /// 「풀리는가」를 물을 수 없기 때문이다. 나머지는 전부 실제 자리에서
        /// 실제 레지스트리에 묻는다.
        /// </summary>
        static ScytheSituation 지금_상황()
        {
            var 자리 = E2EHarness.Player.transform.position;

            bool 밝은구역 = LitZoneRegistry.IsLit(자리);
            bool 사각 = LitZoneRegistry.IsBlindSide(자리);

            var 낫 = new CreatureTraits(BehaviorProfile.Aggressive, 20f, 2f, avoidsLight: true);
            var 감각 = new CreatureSenses(distanceToThreat: 6f, aggroLeft: 0f, stateTimer: 0f,
                                          selfInLight: false, threatInLight: 밝은구역,
                                          threatBlindSide: 사각);
            var 빛 = CreatureDecision.JudgeLight(낫, 감각);

            return new ScytheSituation(true, 빛, false, 밝은구역);
        }
    }
}
