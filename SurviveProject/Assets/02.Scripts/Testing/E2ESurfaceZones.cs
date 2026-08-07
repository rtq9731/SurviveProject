using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Survive.Player;
using Survive.Vitals;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// 지상에서 액체가 <b>세 번 다른 얼굴</b>로 나온다 (스펙 §9).
    ///
    /// 규칙 자체는 <c>SurfaceZoneTests</c>가 Unity 없이 본다. 여기서 보는 것은
    /// <b>그 규칙이 실제 지형 위에서 실제 조작으로 같은 답을 내는가</b>다 —
    /// 셋을 <b>한 시나리오 안에 나란히</b> 놓는 것이 요점이다. 따로 돌리면
    /// 각자 통과하면서도 셋이 서로 다른 답을 낸다는 사실은 아무도 확인하지 않는다.
    ///
    /// <list type="number">
    /// <item><b>여울</b> — 발을 담근 채 걸어서 건넌다. 체력 손실 0</item>
    /// <item><b>수로</b> — 헤엄쳐 건너면 깎이지만 살아서 닿는다.
    ///       잃은 양이 <see cref="LiquidCrossing.Toll"/>이 예측한 값과 맞는가까지 본다</item>
    /// <item><b>먼바다</b> — 규칙이 「못 건넌다」고 한 폭만큼 헤엄치면 실제로 죽는다</item>
    /// <item><b>통로</b> — 뭍에서 뭍으로 걸어서 이어지는가 (캡슐 스윕)</item>
    /// </list>
    ///
    /// <b>지형이 아직 임시다(스펙 §13).</b> 새 지도의 구역은 아직 놓이지 않았고,
    /// 지금 서 있는 것은 옛 뭍덩이들이다. 그래서 여기서는 좌표를 적어 두지 않고
    /// <b>매번 지형에 물어</b> 얕은 자리·물가·깊은 자리를 찾는다. 못 찾으면
    /// 조용히 넘어가지 않고 <b>무엇을 못 찾았는지 로그로 남긴다</b> — 그것이
    /// 사람에게 넘길 목록이다.
    ///
    /// <b>다만 「씬에 그 물건이 있는가」는 로그가 아니라 단언이다.</b> 뭍덩이를
    /// 못 찾는 것은 지형이 덜 만들어진 것이 아니라 <b>이 시나리오가 눈이 먼 것</b>이므로,
    /// <see cref="E2ELandmass"/>가 그 자리에서 던진다.
    /// </summary>
    public static class E2ESurfaceZones
    {
        static PlayerVitals Vitals => E2EHarness.Player.Vitals;

        static PlayerSwimming Swim =>
            Object.FindAnyObjectByType<PlayerSwimming>(FindObjectsInactive.Exclude);

        static float _surface;

        /// <summary>사람에게 넘길 것. 임시 지형이라 못 본 것들이 여기 쌓인다.</summary>
        static readonly List<string> _사람의몫 = new List<string>();

        public static IEnumerator FullRun()
        {
            _사람의몫.Clear();

            yield return 준비();

            yield return 여울은_걸어서_건너도_무해하다();
            yield return 수로는_헤엄치면_깎이지만_건넌다();
            yield return 먼바다는_헤엄치면_죽는다();
            yield return 통로가_걸어서_이어지는가();

            E2EHarness.Log("=== 액체 넷 요약 ===");
            foreach (var 구역 in SurfaceZones.Order)
                E2EHarness.Log($"  {구역,-11} 위험도 {SurfaceZones.Risk(구역),6:F1} " +
                               $"판정 {SurfaceZones.Verdict(구역, 맨몸)}");

            if (_사람의몫.Count > 0)
            {
                E2EHarness.Log("=== 진짜 지형을 기다리는 것 (스펙 §13) ===");
                foreach (var 줄 in _사람의몫) E2EHarness.Log("  " + 줄);
            }

            E2EHarness.Log("=== 지상 구역 완주 ===");
        }

        static readonly List<GearCapability> 맨몸 = new List<GearCapability>();

        // ── 준비 ────────────────────────────────────────────────

        static IEnumerator 준비()
        {
            var dir = Object.FindAnyObjectByType<Survive.Progression.ChapterDirector>(
                FindObjectsInactive.Exclude);
            yield return E2EHarness.WaitUntil(() => dir != null && dir.Current != null,
                                              "챕터가 시작된다", 8f);

            E2EHarness.Assert(Swim != null, "PlayerSwimming이 있다");
            E2EHarness.Assert(LiquidContactService.Instance != null, "바다 서비스가 스스로 서 있다");
            E2EHarness.Assert(E2EWaterProbe.TryFindSurface(out _surface, out var water),
                              "액면이 씬에 있다");
            E2EHarness.Log($"  액면 y={_surface:F2} ({(water == null ? "?" : water.name)})");

            // 뭍덩이가 실제로 서 있는지 여기서 한 번에 확인한다. 아래 두 대목이 이것을
            // 쓰는데, 각자 찾다 각자 넘어가면 "재지 않았다"가 로그에 흩어진다.
            foreach (var 이름 in new[] { E2ELandmass.StartName, E2ELandmass.AcrossName,
                                        E2ELandmass.BeyondName })
            {
                var b = E2ELandmass.RequireBounds(이름);
                E2EHarness.Log($"  뭍 {이름} 중심 {b.center.ToString("F1")} 크기 {b.size.ToString("F1")}");
            }

            // 생물이 물어서 준 피해를 액체가 준 것으로 세면 "체력 손실 0"이 거짓이 된다.
            E2EHarness.SleepWildCreatures();

            Vitals.Revive();
            yield return null;
        }

        // ── ① 여울 — 걸어서 건넌다, 체력 손실 0 ─────────────────

        static IEnumerator 여울은_걸어서_건너도_무해하다()
        {
            E2EHarness.Log("— 여울을 장비 없이 걸어서 건넌다 —");

            // 규칙이 먼저 답한다. 세계가 그 답과 같은지를 아래에서 본다.
            E2EHarness.AssertEqual(SurfaceZones.Verdict(SurfaceZone.Shallows, 맨몸),
                                   CrossingVerdict.Harmless, "규칙이 여울을 무해하다고 답한다");

            if (!얕은_구간을_찾는다(out Vector3 시작, out Vector3 끝, out float 최대수심))
            {
                _사람의몫.Add("걸어서 건널 수 있는 여울이 임시 지형에 없다 — " +
                            "뭍덩이 사이는 바닥이 없는 깊은 액면이다");
                E2EHarness.Assert(false, "여울 구간을 찾았다");
                yield break;
            }

            float 폭 = 평면거리(시작, 끝);
            E2EHarness.Log($"  {시작.ToString("F1")} → {끝.ToString("F1")} " +
                           $"({폭:F1}m, 최대 수심 {최대수심:F2}m)");

            Vitals.Revive();
            E2EHarness.Teleport(시작 + Vector3.up * 0.8f);
            yield return null;

            float settle = 0f;
            while (settle < 1.2f) { settle += Time.deltaTime; yield return null; }

            float 전체력 = Vitals.Health.Current;
            float 전바다 = LiquidContactService.TotalDamage;

            var rig = E2EHarness.Player.CameraRig;
            var body = E2EHarness.Player.transform;

            겨눈다(rig, body, 끝);
            yield return null;
            yield return E2EHarness.PressKey(Key.W);

            float t = 0f, 잠긴시간 = 0f, 헤엄친시간 = 0f;
            bool 닿았다 = false;
            while (t < 40f)
            {
                겨눈다(rig, body, 끝);
                E2EHarness.QueueKeys();

                var 상태 = Swim.Current;
                if (상태 != PlayerSwimming.State.Dry) 잠긴시간 += Time.deltaTime;
                if (상태 == PlayerSwimming.State.Swimming) 헤엄친시간 += Time.deltaTime;

                if (평면거리(body.position, 끝) < 1.5f) { 닿았다 = true; break; }

                t += Time.deltaTime;
                yield return null;
            }
            yield return E2EHarness.ReleaseAllKeys();

            float 잃은것 = 전체력 - Vitals.Health.Current;
            float 바다가낸것 = LiquidContactService.TotalDamage - 전바다;

            E2EHarness.Log($"  {t:F1}초 걸었다 (물에 잠긴 시간 {잠긴시간:F1}초, " +
                           $"헤엄친 시간 {헤엄친시간:F1}초) — 체력 -{잃은것:F1}, 그중 액체 {바다가낸것:F1}");

            E2EHarness.Assert(닿았다, $"걸어서 반대편에 닿았다 ({폭:F1}m)");
            E2EHarness.Assert(잠긴시간 > 1f, "실제로 물에 발을 담근 채 걸었다");
            E2EHarness.Assert(헤엄친시간 < 0.5f, $"헤엄치지 않고 걸었다 (헤엄 {헤엄친시간:F1}초)");
            E2EHarness.Assert(바다가낸것 <= 0.001f, $"액체가 한 번도 물지 않았다 ({바다가낸것:F1})");
            E2EHarness.Assert(잃은것 <= 0.001f, $"체력 손실 0 (실제 {잃은것:F2})");
        }

        // ── ② 수로 — 헤엄치면 깎이지만 건넌다 ───────────────────

        static IEnumerator 수로는_헤엄치면_깎이지만_건넌다()
        {
            E2EHarness.Log("— 수로를 헤엄쳐 건넌다 —");

            E2EHarness.AssertEqual(SurfaceZones.Verdict(SurfaceZone.Inlet, 맨몸),
                                   CrossingVerdict.Costly, "규칙이 수로를 「깎이지만 건넌다」고 답한다");

            if (!횡단_구간을_찾는다(out Vector3 출발, out Vector3 도착))
            {
                _사람의몫.Add("뭍을 파고든 수로가 임시 지형에 없다 — 뭍덩이 사이의 물목으로 대신했다");
                E2EHarness.Assert(false, "헤엄쳐 건널 물목을 찾았다");
                yield break;
            }

            float 폭 = 평면거리(출발, 도착);
            E2EHarness.Log($"  {출발.ToString("F1")} → {도착.ToString("F1")} ({폭:F1}m)");

            Vitals.Revive();
            E2EHarness.Teleport(출발 + Vector3.up * 1.3f);
            yield return null;

            float settle = 0f;
            while (settle < 0.9f) { settle += Time.deltaTime; yield return null; }

            float 전체력 = Vitals.Health.Current;
            float 전바다 = LiquidContactService.TotalDamage;

            var rig = E2EHarness.Player.CameraRig;
            var body = E2EHarness.Player.transform;

            겨눈다(rig, body, 도착);
            yield return null;
            yield return E2EHarness.PressKey(Key.W);

            float t = 0f, 잠김 = 0f;
            bool 닿았다 = false;
            while (t < 45f)
            {
                겨눈다(rig, body, 도착);
                E2EHarness.QueueKeys();

                if (LiquidContactService.IsCorroding) 잠김 += Time.deltaTime;
                if (잠김 > 1f && 평면거리(body.position, 도착) < 3f) { 닿았다 = true; break; }
                if (Vitals.Health.IsEmpty) break;

                t += Time.deltaTime;
                yield return null;
            }
            yield return E2EHarness.ReleaseAllKeys();

            float 잃은것 = 전체력 - Vitals.Health.Current;
            float 바다가낸것 = LiquidContactService.TotalDamage - 전바다;

            // 규칙이 이 폭에 대해 예측한 값. 세계가 그 근처인지 본다.
            float 예측 = LiquidCrossing.Toll(new LiquidBody(SurfaceZones.KindOf(SurfaceZone.Inlet),
                                                             SurfaceZones.InletDepth, 폭));

            E2EHarness.Log($"  {t:F1}초 (잠김 {잠김:F1}초) — 체력 -{잃은것:F1}, 그중 액체 {바다가낸것:F1} " +
                           $"/ 규칙의 예측 {예측:F1}");

            E2EHarness.Assert(닿았다, "헤엄쳐서 건너편 물가에 닿았다");
            E2EHarness.Assert(!Vitals.Health.IsEmpty, "수로는 죽이지 않는다 — 값을 매길 뿐이다");
            E2EHarness.Assert(바다가낸것 > 0f, "헤엄치는 동안 실제로 깎였다");
            E2EHarness.Assert(Mathf.Abs(잃은것 - 바다가낸것) < 0.01f,
                              $"잃은 체력은 전부 액체가 낸 것이다 (잃음 {잃은것:F1}, 액체 {바다가낸것:F1})");

            // 예측과 실측이 갈리는 이유는 실제 경로가 직선이 아니고 물가에서 걸어 들어가는
            // 구간이 있기 때문이다. 절반~두 배 안이면 같은 규칙이 도는 것으로 본다.
            E2EHarness.Assert(바다가낸것 >= 예측 * 0.5f && 바다가낸것 <= 예측 * 2f,
                              $"실측이 규칙의 예측과 같은 자리에 있다 (실측 {바다가낸것:F1}, 예측 {예측:F1})");
        }

        // ── ③ 먼바다 — 헤엄치면 죽는다 ──────────────────────────

        static IEnumerator 먼바다는_헤엄치면_죽는다()
        {
            E2EHarness.Log("— 먼바다를 헤엄친다 —");

            E2EHarness.AssertEqual(SurfaceZones.Verdict(SurfaceZone.OpenWater, 맨몸),
                                   CrossingVerdict.Fatal, "규칙이 먼바다를 「못 건넌다」고 답한다");

            // 임시 지형에는 「먼바다」라는 이름의 구간이 없다. 있는 것은 바닥 없는 열린
            // 액면이고, 그것이 바로 규칙이 말하는 먼바다다 — 건너편이 헤엄쳐 닿을 거리에
            // 없는 물.
            if (!바닥_없는_자리를_찾는다(out Vector3 한가운데))
            {
                _사람의몫.Add("바닥 없는 열린 액면을 찾지 못했다");
                E2EHarness.Assert(false, "먼바다 한가운데를 찾았다");
                yield break;
            }

            float 규칙이준시간 = LiquidCrossing.CrossingSeconds(SurfaceZones.LiquidAt(SurfaceZone.OpenWater));
            E2EHarness.Log($"  {한가운데.ToString("F1")} — 먼바다 폭 {SurfaceZones.OpenWaterWidth:F1}m는 " +
                           $"헤엄쳐 {규칙이준시간:F1}초 거리다 (하한 {SurfaceZones.OpenWaterMinimumWidth:F1}m)");

            Vitals.Revive();
            E2EHarness.Teleport(한가운데);
            yield return null;

            float 전체력 = Vitals.Health.Current;
            float t = 0f;
            bool 죽었다 = false;

            // 규칙이 준 시간에 여유를 얹는다. 그 안에 죽지 않으면 규칙이 거짓말한 것이다.
            float 한계 = 규칙이준시간 + 12f;
            while (t < 한계)
            {
                if (Vitals.Health.IsEmpty) { 죽었다 = true; break; }
                t += Time.deltaTime;
                yield return null;
            }

            E2EHarness.Log($"  {t:F1}초 만에 {(죽었다 ? "죽었다" : "살아 있다")} " +
                           $"— 체력 {전체력:F1} → {Vitals.Health.Current:F1}");

            E2EHarness.Assert(죽었다,
                $"먼바다에 잠긴 채 {한계:F0}초를 버티면 죽는다 (규칙: {규칙이준시간:F1}초 거리)");
            E2EHarness.Assert(t <= 규칙이준시간 + 0.5f,
                $"죽기까지 걸린 시간이 규칙이 예측한 횡단 시간 안이다 (실측 {t:F1}초, 예측 {규칙이준시간:F1}초)");

            Vitals.Revive();
            yield return null;
        }

        // ── ④ 통로 — 걸어서 이어지는가 ──────────────────────────

        /// <summary>
        /// 뭍에서 뭍으로 걸어서 이어지는가를 캡슐 스윕으로 실측한다.
        ///
        /// <b>「이어지는가」가 실패해도 시나리오를 죽이지 않는다.</b> 임시 지형에서는
        /// 끊기는 것이 정상이고(스펙 §13), 여기서 알고 싶은 것은 <b>어디서 끊기는가</b>다.
        /// <b>그러나 「뭍이 있는가」는 다르다</b> — 그것이 거짓이면 스윕이 잰 것이
        /// 아무것도 없다는 뜻이므로 <see cref="E2ELandmass.Require"/>가 던진다.
        /// </summary>
        static IEnumerator 통로가_걸어서_이어지는가()
        {
            E2EHarness.Log("— 통로 캡슐 스윕 —");

            var 구간 = new (string 이름, string 출발뭍, string 도착뭍)[]
            {
                ("착륙지 → 건너편 뭍", E2ELandmass.StartName,  E2ELandmass.AcrossName),
                ("건너편 뭍 → 더 먼 뭍", E2ELandmass.AcrossName, E2ELandmass.BeyondName),
            };

            foreach (var (이름, 출발뭍, 도착뭍) in 구간)
            {
                Vector3 출발 = 뭍_위의_한_점(E2ELandmass.RequireBounds(출발뭍));
                Vector3 도착 = 뭍_위의_한_점(E2ELandmass.RequireBounds(도착뭍));

                List<Vector3> 길 = null;
                yield return E2ETerrainPath.Find(출발, 도착, r => 길 = r,
                                                 searchRadius: 260f, budget: 40000);

                bool 이어진다 = E2ETerrainPath.LastPathComplete;
                Vector3 끝점 = (길 != null && 길.Count > 0) ? 길[길.Count - 1] : 출발;
                float 남은거리 = 평면거리(끝점, 도착);

                E2EHarness.Log($"  {이름}: {(이어진다 ? "이어진다" : "끊긴다")} — " +
                               $"{출발.ToString("F1")} 에서 {끝점.ToString("F1")}까지 " +
                               $"(셀 {E2ETerrainPath.LastExpandedCells}개, 목표까지 {남은거리:F1}m 남음)");

                if (!이어진다)
                    _사람의몫.Add($"{이름} 걸어서 끊긴다 — 막힌 자리 {끝점.ToString("F1")}, " +
                                $"목표 {도착.ToString("F1")}까지 {남은거리:F1}m");
            }
        }

        // ── 지형에 묻는 것들 ────────────────────────────────────

        /// <summary>
        /// 발은 잠기지만 바닥을 딛는 깊이가 <b>곧게 이어지는</b> 구간.
        ///
        /// 한 점만 찾으면 "서 있어도 무해하다"밖에 못 본다. 걸어서 건너는 것을 보려면
        /// 그 깊이가 이어져 있어야 한다.
        /// </summary>
        static bool 얕은_구간을_찾는다(out Vector3 시작, out Vector3 끝, out float 최대수심)
        {
            시작 = 끝 = Vector3.zero;
            최대수심 = 0f;

            // 임계에 붙이면 반올림 한 번에 Dry나 Swimming으로 떨어진다. 안쪽으로 물려 잡는다.
            const float 얕은쪽 = LiquidCrossing.WadeDepth + 0.1f;
            const float 깊은쪽 = LiquidCrossing.SwimDepth - 0.15f;
            const float 걸음 = 2f;
            const float 최소폭 = 8f;

            bool 잠기는가(float x, float z, out float d)
            {
                d = _surface - E2EWaterProbe.BedAt(x, z, _surface);
                return !float.IsNaN(d) && d >= 얕은쪽 && d <= 깊은쪽;
            }

            var 기준 = E2EHarness.Player.transform.position;
            float 최고 = 0f;

            for (float dx = -80f; dx <= 80f; dx += 걸음)
            for (float dz = -80f; dz <= 80f; dz += 걸음)
            {
                float x = 기준.x + dx, z = 기준.z + dz;
                if (!잠기는가(x, z, out _)) continue;

                for (int a = 0; a < 8; a++)
                {
                    var dir = Quaternion.Euler(0f, a * 45f, 0f) * Vector3.forward;

                    float 뻗은거리 = 0f, 이구간최대 = 0f;
                    for (float s = 걸음; s <= 60f; s += 걸음)
                    {
                        if (!잠기는가(x + dir.x * s, z + dir.z * s, out float d)) break;
                        뻗은거리 = s;
                        if (d > 이구간최대) 이구간최대 = d;
                    }

                    if (뻗은거리 <= 최고) continue;

                    최고 = 뻗은거리;
                    시작 = new Vector3(x, _surface, z);
                    끝 = new Vector3(x + dir.x * 뻗은거리, _surface, z + dir.z * 뻗은거리);
                    최대수심 = 이구간최대;
                }
            }

            return 최고 >= 최소폭;
        }

        /// <summary>
        /// 물가에서 물가로 헤엄쳐 건널 수 있는 한 쌍. 임시 지형의 두 뭍 사이를 쓴다.
        ///
        /// <b>뭍이 없으면 여기서 던진다</b> — 「못 찾았다」와 「없는 것을 찾았다」는
        /// 다른 사건이고, 뒤엣것은 사람이 고쳐야 할 것이다.
        /// </summary>
        static bool 횡단_구간을_찾는다(out Vector3 출발, out Vector3 도착)
        {
            출발 = 도착 = Vector3.zero;

            Bounds b1 = E2ELandmass.RequireBounds(E2ELandmass.StartName);
            Bounds b2 = E2ELandmass.RequireBounds(E2ELandmass.AcrossName);

            float 최소 = float.MaxValue;
            float minZ = Mathf.Max(b1.min.z, b2.min.z) - 20f;
            float maxZ = Mathf.Min(b1.max.z, b2.max.z) + 20f;

            for (float z = minZ; z <= maxZ; z += 2f)
            {
                if (!물가를_찾는다(b1.center.x, b1.max.x + 10f, 2f, z, out Vector3 왼쪽)) continue;
                if (!물가를_찾는다(b2.center.x, b2.min.x - 10f, -2f, z, out Vector3 오른쪽)) continue;

                float d = 평면거리(왼쪽, 오른쪽);
                if (d < 8f || d >= 최소) continue;

                최소 = d;
                출발 = 왼쪽;
                도착 = 오른쪽;
            }
            return 최소 < float.MaxValue;
        }

        /// <summary>이 z선에서 뭍이 물로 바뀌는 마지막 뭍 자리.</summary>
        static bool 물가를_찾는다(float from, float to, float step, float z, out Vector3 자리)
        {
            자리 = Vector3.zero;
            bool 찾았다 = false;

            for (float x = from; step > 0 ? x <= to : x >= to; x += step)
            {
                float bed = E2EWaterProbe.BedAt(x, z, _surface);
                if (float.IsNaN(bed)) break;
                if (bed < _surface - 0.2f) break;

                자리 = new Vector3(x, bed, z);
                찾았다 = true;
            }
            return 찾았다;
        }

        /// <summary>바닥이 없는 열린 액면. 규칙이 말하는 「먼바다」가 임시 지형에서는 이것이다.</summary>
        static bool 바닥_없는_자리를_찾는다(out Vector3 자리)
        {
            자리 = Vector3.zero;

            var 기준 = E2EHarness.Player.transform.position;
            float 최소 = float.MaxValue;

            for (float dx = -140f; dx <= 140f; dx += 4f)
            for (float dz = -140f; dz <= 140f; dz += 4f)
            {
                float x = 기준.x + dx, z = 기준.z + dz;
                if (!float.IsNaN(E2EWaterProbe.BedAt(x, z, _surface))) continue;

                // 둘레도 바닥이 없어야 한다. 가장자리 한 칸이면 떠밀려 뭍에 닿는다.
                bool 둘레도비었다 = true;
                for (int a = 0; a < 8 && 둘레도비었다; a++)
                {
                    var dir = Quaternion.Euler(0f, a * 45f, 0f) * Vector3.forward;
                    if (!float.IsNaN(E2EWaterProbe.BedAt(x + dir.x * 6f, z + dir.z * 6f, _surface)))
                        둘레도비었다 = false;
                }
                if (!둘레도비었다) continue;

                float cost = new Vector2(dx, dz).magnitude;
                if (cost >= 최소) continue;

                최소 = cost;
                // 머리까지 확실히 잠기도록 수면 아래로 넣는다.
                자리 = new Vector3(x, _surface - LiquidCrossing.SwimDepth - 0.6f, z);
            }
            return 최소 < float.MaxValue;
        }

        // ── 잔손 ────────────────────────────────────────────────

        static Vector3 뭍_위의_한_점(Bounds b)
        {
            float bed = E2EWaterProbe.BedAt(b.center.x, b.center.z, _surface);
            if (float.IsNaN(bed)) bed = _surface + 1f;
            return new Vector3(b.center.x, bed + 0.5f, b.center.z);
        }

        static void 겨눈다(PlayerCameraRig rig, Transform body, Vector3 목표)
        {
            var d = 목표 - body.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.25f) rig.SetLook(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0f);
        }

        static float 평면거리(Vector3 a, Vector3 b) =>
            Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }
}
