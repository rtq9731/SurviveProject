using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Survive.Player;
using Survive.Vitals;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// 물에 걸어 들어가서, 헤엄쳐 돌아다니다, 걸어 나온다.
    ///
    /// 지금까지 수영 검사는 전부 <b>물속 한가운데로 옮겨 놓고</b> 시작했다
    /// (<see cref="E2EBaseBuilding"/>의 수영 항목). 그러면 조작 하나하나는 확인되지만
    /// 정작 플레이어가 실제로 겪는 것 — 물가에서 잠기고, 헤엄치고, 다시 뭍으로
    /// 기어오르는 <b>왕복</b> — 은 한 번도 지나지 않는다.
    ///
    /// 왕복이 끊기는 방식은 조용하다. 들어가는 것은 되는데 나올 수 없으면
    /// 그 물은 지형이 아니라 함정이고, 산소가 돌아오지 않으면 한 번 잠긴 것만으로
    /// 이후 모든 판정이 어긋난다. 둘 다 상태 검사 하나로는 걸리지 않는다.
    ///
    /// 그래서 여기서는 처음부터 끝까지 <b>키를 눌러</b> 오간다.
    /// 순간이동은 출발선에 서는 데만 쓴다.
    ///
    /// <b>바다가 살을 깎게 된 뒤(백로그 32)의 개정.</b> 이 시나리오는 왕복하는 데
    /// 1분 남짓 물에 머무는데, 초당 3의 값이 붙은 뒤로는 그동안 체력이 두 번 넘게
    /// 사라진다 — 조작을 보러 온 검사가 익사 실험이 되어 버린다. 그래서 구간 사이마다
    /// 몸을 되돌린다. <b>값을 안 보는 것이 아니다</b>: 값은 구간 하나
    /// (<see cref="바다가_값을_매긴다"/>)에서 실제로 재고, 나머지 구간은 그 값에
    /// 방해받지 않고 원래 보던 것을 본다.
    /// </summary>
    public static class E2ESwimRoundTrip
    {
        static PlayerSwimming Swim =>
            UnityEngine.Object.FindAnyObjectByType<PlayerSwimming>(FindObjectsInactive.Exclude);

        static PlayerVitals Vitals => E2EHarness.Player.Vitals;

        public static IEnumerator FullRun()
        {
            var swim = Swim;
            E2EHarness.Assert(swim != null, "PlayerSwimming이 있다");
            E2EHarness.Assert(Vitals != null, "PlayerVitals가 있다");
            if (swim == null || Vitals == null) yield break;

            E2EHarness.Assert(E2EWaterProbe.TryFindSurface(out float surface, out var water),
                              "물이 씬에 있다");
            if (water == null) yield break;
            E2EHarness.Log($"  수면 y={surface:F1} ({water.name})");

            Vector3 shore, towardWater;
            float depth;
            bool found = E2EWaterProbe.TryFindShore(E2EHarness.Player.transform.position, surface,
                                                    60f, out shore, out towardWater, out depth);
            E2EHarness.Assert(found, "걸어 들어갈 수 있는 물가가 있다");
            if (!found) yield break;

            E2EHarness.Log($"  물가 {shore.ToString("F1")} → {towardWater.ToString("F1")} 방향, " +
                           $"앞쪽 수심 {depth:F1}m");

            yield return WalkIn(swim, shore, towardWater);
            yield return 바다가_값을_매긴다(swim);
            yield return 몸을_되돌린다();
            yield return OxygenFallsWhileSubmerged(swim);
            yield return 몸을_되돌린다();
            yield return SprintIsFasterUnderwater();
            // 여기서는 되돌리지 않는다. Revive는 산소도 가득 채우는데, 바로 뒤의
            // OxygenComesBack이 "산소가 돌아온다"를 보려면 돌아올 자리가 남아 있어야 한다.
            yield return SurfaceWithSpace(swim, surface);
            yield return OxygenComesBack();
            yield return 몸을_되돌린다();
            yield return WalkOut(swim, shore, surface);

            E2EHarness.Log("=== 수영 왕복 완주 ===");
        }

        /// <summary>
        /// 바다가 깎은 만큼 돌려준다. 다음 구간이 보려는 것은 조작이지 생존이 아니다.
        /// 되돌리지 않으면 왕복 도중에 죽어, 실패한 것이 조작인지 체력인지 알 수 없게 된다.
        /// </summary>
        static IEnumerator 몸을_되돌린다()
        {
            Vitals.Revive();
            yield return null;
        }

        /// <summary>
        /// 긴 구간 도중에 바다가 다 깎아 버리지 않게 반쯤에서 숨을 붙여 준다.
        ///
        /// 물가로 돌아오는 데 최대 45초가 걸릴 수 있고, 그동안 바다는 100을 넘게 깎는다.
        /// 되돌리지 않으면 "물에서 나올 수 있는가"를 묻는 자리에서 "물에서 나오기 전에
        /// 죽는다"가 답이 되어, 정작 보려던 물가 기어오르기는 한 번도 시험되지 않는다.
        /// </summary>
        static void 숨을_붙여_둔다()
        {
            if (Vitals.Health.Normalized < 0.5f) Vitals.Revive();
        }

        // ── 건너는 데 값이 든다 ─────────────────────────────────

        /// <summary>
        /// 물에 잠긴 채로 있는 동안 실제로 체력이 깎이는지, 그리고 그 값이
        /// <b>한 번 건널 만한 값</b>인지 본다 (백로그 32).
        ///
        /// 규칙 자체는 <c>MacroniumSeaTests</c>가, 실제 1→2 횡단은
        /// <see cref="E2EMacroniumSea"/>가 본다. 여기서 굳이 한 번 더 재는 이유는
        /// 이 시나리오가 <b>물가에서 걸어 들어간 진짜 몸</b>으로 물속에 있는 유일한
        /// 자리이기 때문이다 — 순간이동으로 넣은 몸에서만 값이 붙고 걸어 들어간
        /// 몸에서는 안 붙는 어긋남은 여기서만 걸린다.
        /// </summary>
        static IEnumerator 바다가_값을_매긴다(PlayerSwimming swim)
        {
            Vitals.Revive();
            yield return null;

            float 전체력 = Vitals.Health.Current;
            float 전바다 = MacroniumSeaService.TotalDamage;

            float t = 0f, 잠김 = 0f;
            while (t < 5f)
            {
                if (MacroniumSeaService.IsCorroding) 잠김 += Time.deltaTime;
                t += Time.deltaTime;
                yield return null;
            }

            float 잃은것 = 전체력 - Vitals.Health.Current;
            float 바다가낸것 = MacroniumSeaService.TotalDamage - 전바다;

            E2EHarness.Log($"  물에 잠긴 채 {t:F1}초 (그중 깎인 시간 {잠김:F1}초) — " +
                           $"체력 {전체력:F1} → {Vitals.Health.Current:F1}, 그중 바다 {바다가낸것:F1}");

            E2EHarness.Assert(잠김 > 3f, $"물속에 있는 동안 계속 깎인다 ({잠김:F1}초)");
            E2EHarness.Assert(잃은것 > 0f, "몸이 잠기면 체력이 준다");
            E2EHarness.Assert(Mathf.Abs(잃은것 - 바다가낸것) < 0.01f,
                              $"잃은 체력은 전부 바다가 낸 것이다 (잃음 {잃은것:F1}, 바다 {바다가낸것:F1})");

            // 초당 얼마인지가 이 기능의 값이다. 규칙과 어긋나면 여기서 걸린다.
            float 초당 = 바다가낸것 / Mathf.Max(0.01f, 잠김);
            E2EHarness.Assert(Mathf.Abs(초당 - MacroniumSea.CorrosionPerSecond)
                              <= MacroniumSea.BiteDamage,
                              $"초당 피해가 규칙과 맞는다 (실측 {초당:F2}, 규칙 {MacroniumSea.CorrosionPerSecond:F2})");

            // 한 번 건너는 값. 실측 횡단 시간(1→2, 맨몸 보통 수영 10.3초)으로 환산한다.
            float 횡단 = MacroniumSea.DamageOver(SeaImmersion.Swimming, false, 10.3f)
                       / Vitals.Health.Max;
            E2EHarness.Log($"  이 값으로 1→2를 건너면 체력의 {횡단:P0}");
            E2EHarness.Assert(횡단 >= 0.25f && 횡단 <= 0.35f,
                              $"한 번 건너는 값이 체력의 25~35%다 ({횡단:P0})");
        }

        // ── 걸어 들어간다 ───────────────────────────────────────

        /// <summary>
        /// 뭍에 서서 물 쪽으로 걷는다. 상태가 Dry → Wading → Swimming으로
        /// 순서대로 바뀌어야 한다. 중간의 Wading이 없으면 물가가 절벽이라는 뜻이고,
        /// 그런 물은 걸어 들어갈 수 없다.
        /// </summary>
        static IEnumerator WalkIn(PlayerSwimming swim, Vector3 shore, Vector3 towardWater)
        {
            E2EHarness.Teleport(shore + Vector3.up * 1.2f);
            yield return null;
            yield return null;

            // 발이 땅에 닿을 때까지 잠깐 둔다. 공중에서 시작하면 첫 상태가 Dry가 아니다.
            float settle = 0f;
            while (settle < 0.6f) { settle += Time.deltaTime; yield return null; }

            E2EHarness.AssertEqual(swim.Current, PlayerSwimming.State.Dry, "출발할 때는 물 밖이다");

            var rig = E2EHarness.Player.CameraRig;
            float yaw = Mathf.Atan2(towardWater.x, towardWater.z) * Mathf.Rad2Deg;
            rig.SetLook(yaw, 0f);
            yield return null;

            bool sawWading = false, sawSwimming = false;
            yield return E2EHarness.PressKey(Key.W);

            float t = 0f;
            while (t < 15f && !sawSwimming)
            {
                rig.SetLook(yaw, 0f);
                E2EHarness.QueueKeys();

                if (swim.Current == PlayerSwimming.State.Wading) sawWading = true;
                if (swim.Current == PlayerSwimming.State.Swimming) sawSwimming = true;

                t += Time.deltaTime;
                yield return null;
            }
            yield return E2EHarness.ReleaseKey(Key.W);

            E2EHarness.Log($"  {t:F1}초 걸어 들어갔다 — 위치 {E2EHarness.Player.transform.position.ToString("F1")}");
            E2EHarness.Assert(sawWading, "허리까지 잠기는 구간을 지났다");
            E2EHarness.Assert(sawSwimming, "걸어 들어가면 헤엄치는 상태가 된다");
        }

        // ── 잠기면 산소가 준다 ──────────────────────────────────

        /// <summary>
        /// Ctrl로 가라앉아 머리를 담근다. 뜬 채로는 머리가 물 위에 남아
        /// 산소가 줄지 않는다 — 그러면 "산소가 안 준다"가 아니라 "안 잠겼다"인데,
        /// 그 둘을 구별하지 못하면 산소 소모가 죽어도 이 검사는 통과한다.
        /// </summary>
        static IEnumerator OxygenFallsWhileSubmerged(PlayerSwimming swim)
        {
            yield return E2EHarness.PressKey(Key.LeftCtrl);

            float sink = 0f;
            while (sink < 4f && !swim.IsHeadSubmerged)
            {
                E2EHarness.QueueKeys();
                sink += Time.deltaTime;
                yield return null;
            }
            E2EHarness.Assert(swim.IsHeadSubmerged, $"Ctrl로 가라앉아 머리가 잠긴다 ({sink:F1}초)");

            float before = Vitals.Oxygen.Current;
            float held = 0f;
            while (held < 2.5f)
            {
                E2EHarness.QueueKeys();
                held += Time.deltaTime;
                yield return null;
            }
            float after = Vitals.Oxygen.Current;
            yield return E2EHarness.ReleaseKey(Key.LeftCtrl);

            E2EHarness.Log($"  잠긴 채 {held:F1}초 — 산소 {before:F1} → {after:F1} " +
                           $"(초당 {(after - before) / held:F1})");
            E2EHarness.Assert(after < before - 1f, "머리가 잠기면 산소가 준다");
        }

        // ── Shift는 물속에서도 가속이다 ─────────────────────────

        static IEnumerator SprintIsFasterUnderwater()
        {
            float normal = 0f, sprint = 0f;
            yield return MeasureSpeed(false, r => normal = r);
            yield return MeasureSpeed(true, r => sprint = r);

            E2EHarness.Log($"  헤엄 속도 보통 {normal:F1}m/s → Shift {sprint:F1}m/s");
            E2EHarness.Assert(normal > 0.5f, "앞으로 헤엄쳐 나간다");
            E2EHarness.Assert(sprint > normal * 1.15f, "물속에서도 Shift로 빨라진다");
        }

        /// <summary>앞으로 1초 헤엄쳐 수평 속도를 잰다. 두 측정의 조건을 같게 맞춘다.</summary>
        static IEnumerator MeasureSpeed(bool sprinting, Action<float> result)
        {
            var player = E2EHarness.Player.transform;
            E2EHarness.LookAt(player.position + player.forward * 10f);
            yield return null;

            if (sprinting) yield return E2EHarness.PressKey(Key.LeftShift);
            yield return E2EHarness.PressKey(Key.W);

            float warm = 0f;
            while (warm < 0.3f) { E2EHarness.QueueKeys(); warm += Time.deltaTime; yield return null; }

            var from = player.position;
            float t = 0f;
            while (t < 1f) { E2EHarness.QueueKeys(); t += Time.deltaTime; yield return null; }
            var to = player.position;

            yield return E2EHarness.ReleaseKey(Key.W);
            if (sprinting) yield return E2EHarness.ReleaseKey(Key.LeftShift);

            result(new Vector2(to.x - from.x, to.z - from.z).magnitude / Mathf.Max(0.01f, t));
        }

        // ── Space로 올라온다 ────────────────────────────────────

        static IEnumerator SurfaceWithSpace(PlayerSwimming swim, float surface)
        {
            float startY = E2EHarness.Player.transform.position.y;
            yield return E2EHarness.PressKey(Key.Space);

            float t = 0f;
            while (t < 20f && swim.IsHeadSubmerged && !Vitals.Oxygen.IsEmpty)
            {
                E2EHarness.QueueKeys();
                t += Time.deltaTime;
                yield return null;
            }
            yield return E2EHarness.ReleaseKey(Key.Space);

            float rose = E2EHarness.Player.transform.position.y - startY;
            E2EHarness.Log($"  Space {t:F1}초에 {rose:F1}m 올라왔다 (산소 {Vitals.Oxygen.Current:F0})");
            E2EHarness.Assert(!swim.IsHeadSubmerged, "Space를 누르고 있으면 수면으로 올라온다");
            E2EHarness.Assert(rose > 0.3f, $"실제로 떠올랐다 ({rose:F1}m)");
        }

        /// <summary>
        /// 머리를 물 밖에 둔 채 기다리면 산소가 돌아온다.
        ///
        /// <b>Space를 계속 누르는 이유.</b> 손을 놓으면 몸이 부력과 중력 사이에서
        /// 오르내리며 머리가 수면을 넘나든다 — 3초 중 절반쯤 잠겨 있으면 소모가
        /// 회복을 이겨 산소가 오히려 준다. 그러면 실패하는 것은 "회복이 안 된다"가
        /// 아니라 "머리가 안 나와 있었다"인데, 재는 쪽에서 그 둘을 구별하지 못한다.
        /// 수면에 머무는 것은 사람이 하는 일이기도 하다.
        /// </summary>
        static IEnumerator OxygenComesBack()
        {
            var swim = Swim;
            float before = Vitals.Oxygen.Current;

            yield return E2EHarness.PressKey(Key.Space);

            float t = 0f;
            bool 잠긴적있다 = false;
            while (t < 3f)
            {
                E2EHarness.QueueKeys();
                if (swim != null && swim.IsHeadSubmerged) 잠긴적있다 = true;
                t += Time.deltaTime;
                yield return null;
            }
            yield return E2EHarness.ReleaseKey(Key.Space);

            E2EHarness.Log($"  수면에서 {t:F1}초 — 산소 {before:F1} → {Vitals.Oxygen.Current:F1} " +
                           $"(도중에 잠긴 적 {잠긴적있다})");
            E2EHarness.Assert(!잠긴적있다, "재는 동안 머리가 물 밖에 있었다");
            E2EHarness.Assert(Vitals.Oxygen.Current > before, "머리가 나오면 산소가 돌아온다");
        }

        // ── 걸어 나온다 ─────────────────────────────────────────

        /// <summary>
        /// 들어온 물가로 돌아가 뭍에 오른다.
        ///
        /// 물에 들어갈 수는 있는데 나올 수 없으면 그 물은 함정이다.
        /// 부력을 누르는 처리(DampBuoyancyNearSurface)와 물가 기어오르기(CanClimbOut)가
        /// 서로 어긋나면 정확히 그렇게 되고, 그건 상태 검사로는 보이지 않는다.
        /// </summary>
        static IEnumerator WalkOut(PlayerSwimming swim, Vector3 shore, float surface)
        {
            var rig = E2EHarness.Player.CameraRig;

            // W와 Space를 함께 누른다. Space가 없으면 바닥을 긁으며 돌아오다
            // 물속 바위에 걸리고, 그러면 나오지 못한 것이 아니라 가지 못한 것이
            // 실패로 기록된다. 수면을 따라 돌아오는 것이 사람이 하는 일이기도 하다.
            yield return E2EHarness.PressKey(Key.W);
            yield return E2EHarness.PressKey(Key.Space);

            float t = 0f, nextReport = 1f;
            while (t < 45f)
            {
                var here = E2EHarness.Player.transform.position;
                var d = shore - here;
                d.y = 0f;

                // 수면에 발끝만 걸친 것을 "나왔다"로 치면, 물가에서 못 올라오는
                // 결함이 그대로 통과한다. 지면을 딛고 설 때까지 걷는다.
                bool ashore = swim.Current == PlayerSwimming.State.Dry &&
                              here.y >= surface - 0.05f &&
                              (E2EHarness.Player.Locomotion == null ||
                               E2EHarness.Player.Locomotion.IsGrounded);
                if (ashore) break;

                // 물가를 지나쳐 버리면 되돌아올 수 없다. 매 프레임 물가를 겨눈다.
                if (d.sqrMagnitude > 0.04f)
                    rig.SetLook(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0f);

                E2EHarness.QueueKeys();
                숨을_붙여_둔다();
                t += Time.deltaTime;

                if (t >= nextReport)
                {
                    E2EHarness.Log($"    돌아가는 중 {here.ToString("F1")} — 물가까지 {d.magnitude:F1}m, " +
                                   $"상태 {swim.Current}");
                    nextReport = t + 3f;
                }
                yield return null;
            }
            yield return E2EHarness.ReleaseAllKeys();

            var end = E2EHarness.Player.transform.position;
            E2EHarness.Log($"  {t:F1}초 만에 {end.ToString("F1")}로 나왔다 (수면 {surface:F1})");
            E2EHarness.AssertEqual(swim.Current, PlayerSwimming.State.Dry, "헤엄쳐서 뭍으로 나왔다");
            E2EHarness.Assert(end.y >= surface - 0.05f,
                              $"발이 수면 아래로 잠겨 있지 않다 ({end.y:F2} >= {surface - 0.05f:F2})");
            E2EHarness.Assert(E2EHarness.Player.Locomotion == null ||
                              E2EHarness.Player.Locomotion.IsGrounded,
                              "뭍에 발을 딛고 서 있다");

            // 나왔으면 산소는 차야 한다. 안 차면 다음 잠수는 이미 진 싸움이다.
            yield return E2EHarness.WaitUntil(() => Vitals.Oxygen.Normalized > 0.99f,
                                              "뭍에서 산소가 가득 찬다", 12f);
        }
    }
}
