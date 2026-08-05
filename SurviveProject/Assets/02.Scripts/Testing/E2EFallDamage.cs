using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Survive.Items;
using Survive.Player;
using Survive.Vitals;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// 떨어지면 아프다 (백로그 31번).
    ///
    /// 규칙 자체는 <c>FallImpactTests</c>가 Unity 없이 본다. 여기서 보는 것은
    /// <b>실제로 떨어질 때 그 규칙이 걸리는가</b>다 — 그리고 더 중요하게,
    /// <b>떨어지지 않았을 때는 걸리지 않는가</b>다.
    ///
    /// 낙하 피해가 조용히 망가지는 방식은 둘이다.
    /// 하나는 걷다가 아픈 것 — 점프나 완만한 내리막이 피해로 새면 게임이
    /// 걷는 것부터 벌을 주기 시작하고, 아무 검사도 실패하지 않는다.
    /// 다른 하나는 부활 직후 죽는 것 — 부활은 몸을 옮기는 일이라, 그 이동을
    /// 낙하로 오인하면 되살아나자마자 다시 죽는 고리가 생긴다.
    /// 그래서 피해가 나는 쪽만큼이나 나지 않는 쪽을 길게 본다.
    ///
    /// <b>떨어뜨리는 방법.</b> 순간이동으로 공중에 놓으면 그 착지는 낙하로 세지 않는다
    /// (그게 부활을 지키는 규칙이다). 그래서 발판을 세워 그 위에 올려놓고 —
    /// 발이 땅에 닿는 순간 낙하 판정이 되살아난다 — 발판을 치운다.
    /// 순수한 자유낙하다.
    /// </summary>
    public static class E2EFallDamage
    {
        static PlayerLocomotion Loco => E2EHarness.Player.Locomotion;
        static PlayerVitals Vitals => E2EHarness.Player.Vitals;

        /// <summary>발판 크기. 좁으면 올려놓는 사이에 미끄러져 떨어진다.</summary>
        const float PadSize = 8f;

        /// <summary>중간 높이. 자유낙하 15 m/s쯤 — 아프지만 죽지는 않아야 한다.</summary>
        const float MidHeight = 11.5f;

        /// <summary>치명 높이. 24 m/s 임계를 넘긴다.</summary>
        const float LethalHeight = 36f;

        /// <summary>굳은 땅으로 치려면 수면보다 이만큼은 높아야 한다.</summary>
        const float DryMargin = 4f;

        public static IEnumerator FullRun()
        {
            E2EHarness.Assert(Loco != null, "PlayerLocomotion이 있다");
            E2EHarness.Assert(Vitals != null, "PlayerVitals가 있다");
            if (Loco == null || Vitals == null) yield break;

            E2EHarness.Log($"  임계: 무해 {FallImpact.SafeImpactSpeed} m/s, " +
                           $"치명 {FallImpact.LethalImpactSpeed} m/s");

            yield return 걷고_뛰는_동안은_아프지_않다();
            yield return 중간_높이는_부분_피해다();
            yield return 물에는_같은_높이에서_떨어져도_무해하다();
            yield return 깊은_낙하는_죽이고_가방을_남긴다();
            yield return 되살아난_직후에는_아프지_않다();

            // 이어서 도는 시나리오가 물속이나 낯선 자리에서 시작하지 않게 정리한다.
            yield return 마른_땅으로_돌아간다();

            E2EHarness.Log("=== 낙하 대가 완주 ===");
        }

        // ── ① 일상 이동 ─────────────────────────────────────────

        /// <summary>
        /// 점프하고, 언덕을 내려간다. 한 번도 아프지 않아야 한다.
        ///
        /// 최대 착지 속도를 같이 적는다 — 임계와 얼마나 떨어져 있는지가
        /// 통과/실패보다 중요한 정보다. 여유가 얇으면 언젠가 뒤집힌다.
        /// </summary>
        static IEnumerator 걷고_뛰는_동안은_아프지_않다()
        {
            E2EHarness.Log("— 점프와 내리막 —");

            채운다();
            Vitals.Revive();
            yield return 발이_땅에_닿을_때까지();

            float 시작체력 = Vitals.Health.Current;
            float 시작피해 = Loco.TotalFallDamage;
            int 시작착지 = Loco.LandingCount;
            float 최대착지 = 0f;

            // 점프. jumpCooldown이 1초라 그만큼씩 띄운다.
            for (int i = 0; i < 5; i++)
            {
                yield return E2EHarness.TapKey(Key.Space);
                yield return 지켜보며_기다린다(1.2f, v => 최대착지 = Mathf.Max(최대착지, v));
            }

            E2EHarness.Log($"  점프 5회 — 착지 {Loco.LandingCount - 시작착지}회, " +
                           $"최대 착지 속도 {최대착지:F2} m/s");
            E2EHarness.Assert(최대착지 > 0.5f, $"점프가 실제로 일어났다 (착지 {최대착지:F2} m/s)");
            E2EHarness.Assert(Vitals.Health.Current >= 시작체력,
                              $"점프로는 체력이 줄지 않았다 ({시작체력:F0} → {Vitals.Health.Current:F0})");

            // 내리막. 가장 가파르게 낮아지는 쪽으로 뛰어 내려간다.
            float 내리막최대 = 0f;
            yield return 내리막을_뛰어내린다(v => 내리막최대 = Mathf.Max(내리막최대, v));

            E2EHarness.Log($"  내리막 — 최대 착지 속도 {내리막최대:F2} m/s");
            E2EHarness.Assert(Vitals.Health.Current >= 시작체력,
                              $"내리막으로도 체력이 줄지 않았다 ({Vitals.Health.Current:F0})");

            int 착지수 = Loco.LandingCount - 시작착지;
            E2EHarness.Log($"  착지 {착지수}회, 낙하 피해 합계 {Loco.TotalFallDamage - 시작피해:F1}");
            E2EHarness.Assert(착지수 >= 5, $"실제로 여러 번 착지했다 ({착지수}회)");
            E2EHarness.AssertEqual(Loco.TotalFallDamage, 시작피해,
                                   "일상 이동으로는 낙하 피해가 한 번도 없었다");

            float 최대 = Mathf.Max(최대착지, 내리막최대);
            E2EHarness.Assert(최대 < FallImpact.SafeImpactSpeed,
                              $"일상 이동의 최대 착지 속도가 무해 임계 아래다 " +
                              $"({최대:F2} < {FallImpact.SafeImpactSpeed})");
            E2EHarness.Log($"  임계까지 여유 {FallImpact.SafeImpactSpeed - 최대:F2} m/s");
        }

        // ── ② 중간 높이 ─────────────────────────────────────────

        static IEnumerator 중간_높이는_부분_피해다()
        {
            E2EHarness.Log($"— {MidHeight}m에서 굳은 땅으로 —");

            Vitals.Revive();
            float 전 = Vitals.Health.Current;

            bool 떨어졌다 = false;
            yield return 발판에서_떨군다(MidHeight, false, r => 떨어졌다 = r);
            E2EHarness.Assert(떨어졌다, "발판을 세우고 떨어뜨렸다");
            if (!떨어졌다) yield break;

            float 후 = Vitals.Health.Current;
            float 입은피해 = 전 - 후;

            E2EHarness.Log($"  착지 {Loco.LastLandingSpeed:F2} m/s, " +
                           $"피해 {Loco.LastFallDamage:F1}, 체력 {전:F0} → {후:F0}");

            E2EHarness.Assert(Loco.LastLandingSpeed > FallImpact.SafeImpactSpeed,
                              $"무해 임계를 넘겨 부딪혔다 ({Loco.LastLandingSpeed:F2} m/s)");
            E2EHarness.Assert(입은피해 > 1f, $"피해를 입었다 ({입은피해:F1})");
            E2EHarness.Assert(!Vitals.Health.IsEmpty, "이 높이로는 죽지 않는다 — 부분 피해다");
            E2EHarness.Assert(Mathf.Abs(입은피해 - Loco.LastFallDamage) < 1f,
                              $"체력에서 깎인 값이 낙하 피해와 같다 " +
                              $"({입은피해:F1} vs {Loco.LastFallDamage:F1})");
        }

        // ── ④ 물 입수 ───────────────────────────────────────────

        /// <summary>
        /// 죽는 높이에서 물로 떨어진다. 아프지 않아야 한다.
        ///
        /// 굳은 땅이었다면 확실히 죽었을 높이를 쓴다. 중간 높이로 보면
        /// "물이라서 무해한 것"과 "덜 떨어져서 무해한 것"이 구별되지 않는다.
        ///
        /// <b>바다가 살을 깎게 된 뒤(백로그 32)의 개정.</b> 입수한 뒤 잠겨 있는 동안은
        /// 체력이 준다 — 섬 사이 액체가 물이 아니라 묽은 매크로늄이 됐기 때문이다.
        /// 그래서 "체력이 한 점도 줄지 않았다"는 더 이상 성립하지 않는다.
        ///
        /// 기준을 낮춘 것이 아니라 <b>좁혔다</b>: 줄어든 체력이 <i>전부 바다가 낸 것인지</i>를
        /// 본다. 낙하 몫이 1이라도 섞이면 두 값이 어긋나 걸린다. 예전 기준은 "0이면 통과",
        /// 지금 기준은 "낙하 몫이 0이면 통과"다 — R8이 정한 "입수는 무해하다"는 그대로다.
        /// </summary>
        static IEnumerator 물에는_같은_높이에서_떨어져도_무해하다()
        {
            E2EHarness.Log($"— {LethalHeight}m에서 물로 —");

            if (!E2EWaterProbe.TryFindSurface(out float surface, out var water) || water == null)
            {
                E2EHarness.Log("  [건너뜀] 씬에 물이 없다");
                yield break;
            }

            Vitals.Revive();
            float 전 = Vitals.Health.Current;
            float 전피해 = Loco.TotalFallDamage;
            float 전바다 = MacroniumSeaService.TotalDamage;

            bool 떨어졌다 = false;
            yield return 발판에서_떨군다(LethalHeight, true, r => 떨어졌다 = r);
            E2EHarness.Assert(떨어졌다, "물 위에 발판을 세우고 떨어뜨렸다");
            if (!떨어졌다) yield break;

            float 후 = Vitals.Health.Current;
            float 바다몫 = MacroniumSeaService.TotalDamage - 전바다;
            E2EHarness.Log($"  마지막 착지 {Loco.LastLandingSpeed:F2} m/s, " +
                           $"낙하 피해 {Loco.TotalFallDamage - 전피해:F1}, 체력 {전:F0} → {후:F0} " +
                           $"(그중 바다 {바다몫:F1})");

            E2EHarness.Assert(Mathf.Approximately(Loco.TotalFallDamage, 전피해),
                              $"물에서는 낙하 피해가 없다 (이 구간 {Loco.TotalFallDamage - 전피해:F1})");
            E2EHarness.Assert(후 >= 전 - 바다몫 - 0.5f,
                              $"줄어든 체력은 전부 바다가 낸 것이다 ({전:F0} → {후:F0}, 바다 {바다몫:F1}) " +
                              "— 굳은 땅이었으면 죽었을 높이다");
            E2EHarness.Assert(!Vitals.Health.IsEmpty, "물에 빠진 것으로 죽지는 않았다");

            // 물 밖으로 꺼내 둔다. 다음 시나리오가 산소 0에서 시작하면 안 된다.
            yield return 마른_땅으로_돌아간다();
        }

        // ── ③ 치명 낙하와 부활 ──────────────────────────────────

        static IEnumerator 깊은_낙하는_죽이고_가방을_남긴다()
        {
            E2EHarness.Log($"— {LethalHeight}m에서 굳은 땅으로 —");

            채운다();
            Vitals.Revive();

            int 이전가방 = DeathDropBag.Active.Count;
            int 이전부활 = RespawnService.RespawnCount;
            float 이전피해 = Loco.TotalFallDamage;

            E2EHarness.Log($"  출발 {E2EHarness.Player.transform.position.ToString("F1")}, " +
                           $"낙하 피해 누계 {이전피해:F1}");

            bool 떨어졌다 = false;
            yield return 발판에서_떨군다(LethalHeight, false, r => 떨어졌다 = r);
            E2EHarness.Assert(떨어졌다, "발판을 세우고 떨어뜨렸다");
            if (!떨어졌다) yield break;

            E2EHarness.Log($"  착지 {Loco.LastLandingSpeed:F2} m/s, 피해 {Loco.LastFallDamage:F1}");
            E2EHarness.Assert(Loco.LastLandingSpeed >= FallImpact.LethalImpactSpeed,
                              $"치명 임계를 넘겨 부딪혔다 ({Loco.LastLandingSpeed:F2} m/s)");
            E2EHarness.Assert(Loco.LastFallDamage >= FallImpact.LethalDamage - 0.01f,
                              $"치명 피해가 들어갔다 ({Loco.LastFallDamage:F1})");
            E2EHarness.Log($"  이 구간의 낙하 피해 누계 {Loco.TotalFallDamage - 이전피해:F1}");
            E2EHarness.Assert(Vitals.Health.IsEmpty, "이 높이에서는 죽는다");

            yield return E2EHarness.WaitUntil(() => DeathDropBag.Active.Count > 이전가방,
                                              "죽은 자리에 가방이 남는다", 5f);
            yield return E2EHarness.WaitUntil(() => RespawnService.RespawnCount > 이전부활,
                                              "잠깐 뒤 스스로 되살아난다", 8f);

            E2EHarness.Assert(Vitals.Health.IsFull, "체력이 가득 찬 채로 일어섰다");
        }

        // ── ⑤ 부활 직후 ─────────────────────────────────────────

        /// <summary>
        /// 부활은 몸을 옮기는 일이다. 그 이동이 낙하로 세어지면 되살아난 자리에서
        /// 곧바로 다시 깎이기 시작한다 — 앞 시나리오가 방금 부활을 시켰으므로
        /// 여기서는 그 뒤를 지켜보기만 한다.
        /// </summary>
        static IEnumerator 되살아난_직후에는_아프지_않다()
        {
            E2EHarness.Log("— 되살아난 직후 —");

            float 전 = Vitals.Health.Current;
            float 전피해 = Loco.TotalFallDamage;
            int 전착지 = Loco.LandingCount;
            float 최대착지 = 0f;

            yield return 지켜보며_기다린다(2f, v => 최대착지 = Mathf.Max(최대착지, v));

            E2EHarness.Log($"  부활 뒤 2초 — 착지 {Loco.LandingCount - 전착지}회, " +
                           $"최대 착지 속도 {최대착지:F2} m/s, " +
                           $"체력 {전:F0} → {Vitals.Health.Current:F0}");
            E2EHarness.Assert(Mathf.Approximately(Loco.TotalFallDamage, 전피해),
                              $"부활 뒤 첫 착지는 낙하가 아니다 (이 구간 {Loco.TotalFallDamage - 전피해:F1})");
            E2EHarness.Assert(Vitals.Health.Current >= 전,
                              "되살아난 자리에서 체력이 깎이지 않는다");
            E2EHarness.Assert(!Vitals.Health.IsEmpty, "다시 죽지 않았다");

            yield return 옮겨진_몸은_아무리_높아도_아프지_않다();
        }

        /// <summary>
        /// 부활이 실제로 얼마나 떨어뜨리는지는 화톳불 자리에 달렸다. 방금 본 낙차가
        /// 마침 작았을 뿐일 수 있으므로, 확실히 죽을 높이로 <b>옮겨</b> 본다.
        /// 순간이동으로 놓인 몸은 그 높이에서도 아프지 않아야 한다.
        /// </summary>
        static IEnumerator 옮겨진_몸은_아무리_높아도_아프지_않다()
        {
            if (!자리를_고른다(LethalHeight, false, out Vector3 바닥))
            {
                E2EHarness.Log("  [건너뜀] 옮겨 놓을 높은 자리를 못 찾았다");
                yield break;
            }

            Vitals.Revive();
            float 전피해 = Loco.TotalFallDamage;
            float 전 = Vitals.Health.Current;

            E2EHarness.Teleport(new Vector3(바닥.x, 바닥.y + LethalHeight, 바닥.z));
            yield return null;

            float t = 0f;
            while (t < 12f && !Loco.IsGrounded) { t += Time.deltaTime; yield return null; }
            yield return null;

            E2EHarness.Log($"  {LethalHeight}m 위로 옮겨 놓고 떨어뜨렸다 — " +
                           $"착지 {Loco.LastLandingSpeed:F2} m/s, 체력 {전:F0} → {Vitals.Health.Current:F0}");
            E2EHarness.Assert(Loco.LastLandingSpeed > FallImpact.LethalImpactSpeed,
                              $"굳은 땅에 치명 속도로 부딪혔다 ({Loco.LastLandingSpeed:F2} m/s)");
            E2EHarness.Assert(Mathf.Approximately(Loco.TotalFallDamage, 전피해),
                              $"옮겨진 뒤의 첫 착지는 낙하가 아니다 " +
                              $"(이 구간 {Loco.TotalFallDamage - 전피해:F1})");
            E2EHarness.Assert(!Vitals.Health.IsEmpty, "죽지 않았다");
        }

        // ── 떨어뜨리기 ──────────────────────────────────────────

        /// <summary>
        /// 발판을 세워 그 위에 올려놓고, 발이 닿은 뒤 발판을 치운다.
        ///
        /// 순간이동으로 공중에 놓으면 안 된다 — 옮겨진 뒤의 첫 착지는 낙하로 세지 않으므로
        /// 아무리 높이 놓아도 피해가 0으로 나온다. 그건 규칙대로 동작한 것이지
        /// 낙하를 시험한 것이 아니다.
        /// </summary>
        static IEnumerator 발판에서_떨군다(float 높이, bool 물위, System.Action<bool> result)
        {
            if (!자리를_고른다(높이, 물위, out Vector3 바닥))
            {
                E2EHarness.Log($"  떨어뜨릴 자리를 못 찾았다 (높이 {높이}m, 물위={물위})");
                result(false);
                yield break;
            }

            float 발판윗면 = 바닥.y + 높이;
            var 발판 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            발판.name = "E2E_FallPad";
            발판.transform.localScale = new Vector3(PadSize, 0.5f, PadSize);
            발판.transform.position = new Vector3(바닥.x, 발판윗면 - 0.25f, 바닥.z);

            E2EHarness.Log($"  발판 {발판.transform.position.ToString("F1")}, " +
                           $"바닥 y={바닥.y:F1}, 낙차 {높이:F1}m");

            // 발판 위에 세운다. 순간이동이므로 이 착지는 세지 않는다 — 그래서 먼저 딛는다.
            E2EHarness.Teleport(new Vector3(바닥.x, 발판윗면 + 1.1f, 바닥.z));
            yield return null;

            bool 딛었다 = false;
            float t = 0f;
            while (t < 3f)
            {
                if (Loco.IsGrounded) { 딛었다 = true; break; }
                t += Time.deltaTime;
                yield return null;
            }

            if (!딛었다)
            {
                E2EHarness.Log("  발판을 딛지 못했다");
                Object.Destroy(발판);
                result(false);
                yield break;
            }

            // 한 프레임 더 딛고 있어야 낙하 판정이 확실히 되살아난다
            yield return null;

            E2EHarness.Log($"  발판을 딛었다 — 여기까지 낙하 피해 누계 {Loco.TotalFallDamage:F1}, " +
                           $"체력 {Vitals.Health.Current:F0}");

            Object.Destroy(발판);
            yield return null;

            // 떨어져 무언가에 닿을 때까지. 물이면 잠기는 것으로 끝난다.
            float 기다림 = 0f;
            bool 닿았다 = false;
            while (기다림 < 12f)
            {
                기다림 += Time.deltaTime;
                yield return null;

                if (물위)
                {
                    if (물에_잠겼나()) { 닿았다 = true; break; }
                }
                else if (Loco.IsGrounded) { 닿았다 = true; break; }
            }

            // 피해는 착지한 프레임에 들어간다. 체력에 반영될 한 프레임을 준다.
            yield return null;
            if (물위)
            {
                // 물속에서는 바닥에 닿을 때까지 더 내려간다. 그 착지까지 봐야
                // "물이면 어떤 착지도 무해하다"가 확인된다.
                float 더 = 0f;
                while (더 < 3f) { 더 += Time.deltaTime; yield return null; }
            }

            var 닿은자리 = E2EHarness.Player.transform.position;
            E2EHarness.Log($"  닿은 자리 {닿은자리.ToString("F1")} " +
                           $"(계획한 바닥 y={바닥.y:F1}, 실제 낙차 {발판윗면 - 닿은자리.y:F1}m)");
            if (!물위 && 물에_잠겼나())
                E2EHarness.Log("  [주의] 굳은 땅을 노렸는데 발이 물에 잠겼다 — 자리 고르기가 어긋났다");

            E2EHarness.Assert(닿았다, 물위 ? "물에 닿았다" : "땅에 닿았다");
            result(닿았다);
        }

        /// <summary>
        /// 위가 트인 자리를 고른다. 머리 위가 막혀 있으면 발판을 세울 수 없고,
        /// 떨어지는 도중에 무언가에 걸리면 낙차가 계획과 달라진다.
        /// </summary>
        static bool 자리를_고른다(float 높이, bool 물위, out Vector3 바닥)
        {
            바닥 = Vector3.zero;

            Vector3 중심 = E2EHarness.Player.transform.position;
            float 수면 = 0f;
            bool 물있음 = 물위 && E2EWaterProbe.TryFindSurface(out 수면, out _);
            if (물위 && !물있음) return false;

            for (float r = 0f; r <= 60f; r += 6f)
            {
                int 갈래 = r < 0.1f ? 1 : 12;
                for (int a = 0; a < 갈래; a++)
                {
                    var dir = Quaternion.Euler(0f, a * (360f / 갈래), 0f) * Vector3.forward;
                    Vector3 p = 중심 + dir * r;

                    if (!Physics.Raycast(new Vector3(p.x, 중심.y + 40f, p.z), Vector3.down,
                                         out var hit, 200f, ~0, QueryTriggerInteraction.Ignore))
                        continue;

                    bool 물있다 = WaterBody.TryGetSurfaceAt(hit.point, out float s);

                    if (물위)
                    {
                        // 헤엄칠 만큼 깊어야 한다. 얕으면 물이 아니라 바닥에 부딪힌다.
                        if (!물있다 || s - hit.point.y < 2.5f) continue;
                    }
                    else
                    {
                        // 수면보다 확실히 높아야 한다. 물가에 아슬아슬하게 걸친 자리를 고르면,
                        // 계산상 낙차보다 조금만 더 떨어져도 발이 물에 잠겨 무해해진다 —
                        // 그러면 "굳은 땅에 부딪힌다"를 시험하지 못한 채 통과한다.
                        if (물있다 && hit.point.y < s + DryMargin) continue;
                    }

                    // 딛는 자리와 떨어질 길이 트여 있어야 한다
                    float 기준 = 물위 ? 수면 : hit.point.y;
                    var 발판중심 = new Vector3(p.x, 기준 + 높이 - 0.25f, p.z);
                    if (Physics.CheckBox(발판중심,
                                         new Vector3(PadSize * 0.5f, 1.6f, PadSize * 0.5f),
                                         Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                        continue;
                    if (Physics.Raycast(new Vector3(p.x, 기준 + 0.6f, p.z), Vector3.up,
                                        높이 + 2.5f, ~0, QueryTriggerInteraction.Ignore))
                        continue;

                    바닥 = new Vector3(p.x, 기준, p.z);
                    return true;
                }
            }
            return false;
        }

        // ── 거들기 ──────────────────────────────────────────────

        static bool 물에_잠겼나()
        {
            var p = E2EHarness.Player.transform.position;
            return WaterBody.TryGetSurfaceAt(p, out float s) && p.y < s;
        }

        /// <summary>
        /// 주어진 시간 동안 착지를 지켜본다.
        ///
        /// 착지했는지는 속도가 <b>바뀌었는지</b>가 아니라 <see cref="PlayerLocomotion.LandingCount"/>가
        /// 늘었는지로 본다 — 같은 점프를 반복하면 착지 속도가 소수점까지 같아서,
        /// 값의 변화를 보면 두 번째부터는 착지를 놓친다.
        /// </summary>
        static IEnumerator 지켜보며_기다린다(float seconds, System.Action<float> 착지속도)
        {
            int 이전 = Loco.LandingCount;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                yield return null;

                if (Loco.LandingCount != 이전)
                {
                    이전 = Loco.LandingCount;
                    착지속도(Loco.LastLandingSpeed);
                }
            }
        }

        static IEnumerator 발이_땅에_닿을_때까지()
        {
            float t = 0f;
            while (t < 3f && !Loco.IsGrounded) { t += Time.deltaTime; yield return null; }
            yield return null;
        }

        /// <summary>가장 가파르게 낮아지는 쪽으로 달려 내려간다.</summary>
        static IEnumerator 내리막을_뛰어내린다(System.Action<float> 착지속도)
        {
            Vector3 여기 = E2EHarness.Player.transform.position;
            float 여기높이 = 지면높이(여기.x, 여기.z, 여기.y);

            float 최대낙차 = 0f;
            Vector3 내리막 = Vector3.zero;

            for (int a = 0; a < 16; a++)
            {
                var dir = Quaternion.Euler(0f, a * 22.5f, 0f) * Vector3.forward;
                Vector3 p = 여기 + dir * 10f;
                float h = 지면높이(p.x, p.z, 여기.y);
                if (float.IsNaN(h)) continue;

                float 낙차 = 여기높이 - h;
                if (낙차 > 최대낙차) { 최대낙차 = 낙차; 내리막 = dir; }
            }

            if (최대낙차 < 0.8f)
            {
                E2EHarness.Log($"  주변에 이렇다 할 내리막이 없다 (최대 {최대낙차:F1}m)");
                yield break;
            }

            E2EHarness.Log($"  10m 앞이 {최대낙차:F1}m 낮은 쪽으로 달려 내려간다");

            var rig = E2EHarness.Player.CameraRig;
            rig.SetLook(Mathf.Atan2(내리막.x, 내리막.z) * Mathf.Rad2Deg, 0f);
            yield return null;

            yield return E2EHarness.PressKey(Key.LeftShift);
            yield return E2EHarness.PressKey(Key.W);
            yield return 지켜보며_기다린다(5f, 착지속도);
            yield return E2EHarness.ReleaseAllKeys();
            yield return 지켜보며_기다린다(1f, 착지속도);
        }

        static float 지면높이(float x, float z, float from)
        {
            if (!Physics.Raycast(new Vector3(x, from + 40f, z), Vector3.down, out var hit, 200f,
                                 ~0, QueryTriggerInteraction.Ignore))
                return float.NaN;
            return hit.point.y;
        }

        /// <summary>
        /// 물에서 꺼내 마른 땅에 세운다.
        ///
        /// 발판을 세울 만큼 트인 자리를 먼저 찾고, 없으면 조건을 낮춰 그냥 마른 땅을 찾는다 —
        /// 여기서 필요한 것은 낙하 실험장이 아니라 그저 물 밖이다.
        /// </summary>
        static IEnumerator 마른_땅으로_돌아간다()
        {
            Vector3 마른땅;
            bool 찾았다 = 자리를_고른다(2f, false, out 마른땅) || 마른_땅을_찾는다(out 마른땅);

            if (찾았다)
            {
                E2EHarness.Teleport(마른땅 + Vector3.up * 1.2f);
                yield return null;
                E2EHarness.Log($"  마른 땅 {마른땅.ToString("F1")}으로 돌아왔다");
            }
            else E2EHarness.Log("  [주의] 마른 땅을 못 찾았다");

            Vitals.Revive();
            yield return 발이_땅에_닿을_때까지();
        }

        /// <summary>머리 위 여유는 따지지 않고, 물이 아닌 지면만 찾는다.</summary>
        static bool 마른_땅을_찾는다(out Vector3 바닥)
        {
            바닥 = Vector3.zero;
            Vector3 중심 = E2EHarness.Player.transform.position;

            for (float r = 4f; r <= 90f; r += 6f)
            for (int a = 0; a < 12; a++)
            {
                var dir = Quaternion.Euler(0f, a * 30f, 0f) * Vector3.forward;
                Vector3 p = 중심 + dir * r;

                if (!Physics.Raycast(new Vector3(p.x, 중심.y + 60f, p.z), Vector3.down,
                                     out var hit, 300f, ~0, QueryTriggerInteraction.Ignore))
                    continue;

                if (WaterBody.TryGetSurfaceAt(hit.point, out float s) &&
                    hit.point.y < s + DryMargin)
                    continue;

                바닥 = hit.point;
                return true;
            }
            return false;
        }

        /// <summary>죽을 때 떨굴 것을 채운다. 빈 손으로 죽으면 가방이 생기지 않는다.</summary>
        static void 채운다()
        {
            var pi = E2EHarness.Player.Inventory;
            if (pi == null || pi.Database == null) return;

            foreach (var id in new[] { "scrap", "alien_alloy" })
            {
                var item = pi.Database.GetById(id);
                if (item != null && pi.Inventory.CountOf(id) < 5) pi.Inventory.TryAdd(item, 5);
            }
        }
    }
}
