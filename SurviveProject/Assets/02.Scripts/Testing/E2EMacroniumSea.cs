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
    /// 섬 사이 바다는 물이 아니다 — 옅은 매크로늄이고, 건너는 데 값이 든다 (백로그 32).
    ///
    /// 규칙 자체는 <c>MacroniumSeaTests</c>가 Unity 없이 본다. 여기서 보는 것은
    /// 그 규칙이 <b>실제 지형 위에서 실제 조작으로 무엇을 만드는가</b>다:
    ///
    /// <list type="number">
    /// <item>건너편 물가에서 발을 담그고 선 채로는 아무 일도 없다 — 채집이 잡무가 되면 안 된다</item>
    /// <item>이쪽 물가에서 건너편 물가까지 실제로 헤엄쳐 건너면 체력이 실제로 준다</item>
    /// <item>Shift로 건너면 유의미하게 덜 든다 — 가속이 생존기다</item>
    /// <item>잠긴 채 버티면 죽고, 가방을 남기고, 다시 일어선다</item>
    /// </list>
    ///
    /// <b>1번과 2번을 함께 보는 이유.</b> 값을 매기는 것만 확인하면 "해안에 서 있기만 해도
    /// 깎인다"를 놓치고, 무해만 확인하면 "바다가 아무것도 하지 않는다"를 놓친다.
    /// 둘 사이의 경계가 이 기능의 전부다.
    /// </summary>
    public static class E2EMacroniumSea
    {
        static Inventory Inv => E2EHarness.Player.Inventory.Inventory;
        static PlayerVitals Vitals => E2EHarness.Player.Vitals;
        static PlayerSwimming Swim =>
            Object.FindAnyObjectByType<PlayerSwimming>(FindObjectsInactive.Exclude);

        /// <summary>죽으면서 빠져나가야 하는 것. 가방이 무엇을 담는지 보려면 담길 것이 있어야 한다.</summary>
        static readonly string[] 벌어온것 = { "scrap", "alien_alloy" };

        static float _surface;

        public static IEnumerator FullRun()
        {
            yield return 준비();

            yield return 해안에_발을_담근_채로는_아무_일도_없다();
            yield return 헤엄쳐_건너면_값을_치른다();
            yield return 잠긴_채_버티면_죽고_가방을_남긴다();

            E2EHarness.Log("=== 매크로늄 바다 완주 ===");
        }

        // ── 준비 ────────────────────────────────────────────────

        static IEnumerator 준비()
        {
            var dir = Object.FindAnyObjectByType<Survive.Progression.ChapterDirector>(
                FindObjectsInactive.Exclude);
            yield return E2EHarness.WaitUntil(() => dir != null && dir.Current != null,
                                              "챕터가 시작된다", 8f);

            E2EHarness.Assert(Swim != null, "PlayerSwimming이 있다");
            E2EHarness.Assert(Vitals != null, "PlayerVitals가 있다");
            E2EHarness.Assert(MacroniumSeaService.Instance != null, "바다 서비스가 스스로 서 있다");
            E2EHarness.Assert(DeathDropService.Instance != null, "사망 드롭 서비스가 서 있다");
            E2EHarness.Assert(RespawnService.Instance != null, "부활 서비스가 서 있다");

            E2EHarness.Assert(E2EWaterProbe.TryFindSurface(out _surface, out var water),
                              "바다가 씬에 있다");
            if (water != null) E2EHarness.Log($"  액면 y={_surface:F2} ({water.name})");

            Vitals.Revive();
            yield return null;
            E2EHarness.Assert(!Vitals.Health.IsEmpty, "산 채로 시작한다");
        }

        static void 채운다()
        {
            var db = E2EHarness.Player.Inventory.Database;
            if (db == null) return;

            foreach (var id in 벌어온것)
            {
                var item = db.GetById(id);
                if (item != null && Inv.CountOf(id) < 6) Inv.TryAdd(item, 6);
            }
        }

        // ── 1. 해안은 무해하다 ──────────────────────────────────

        /// <summary>
        /// 바닥을 딛고 설 만큼 얕은 물에 들어가 가만히 있는다.
        ///
        /// 여기서 체력이 한 점이라도 줄면 물가 채집이 곧바로 잡무가 된다 —
        /// 양치 섬유 하나 뜯을 때마다 체력을 내야 하는 세계는 사용자가 정한 것이 아니다.
        /// </summary>
        static IEnumerator 해안에_발을_담근_채로는_아무_일도_없다()
        {
            E2EHarness.Log("— 해안에 발을 담그고 선다 —");

            if (!발목까지_잠기는_자리를_찾는다(out Vector3 자리, out float 수심))
            {
                E2EHarness.Assert(false, "발목까지만 잠기는 물가를 찾았다");
                yield break;
            }

            Vitals.Revive();
            E2EHarness.Teleport(자리 + Vector3.up * 1.0f);
            yield return null;

            // 발이 바닥에 앉을 때까지 둔다. 떨어지는 도중에 재면 아직 딛지 않은 상태다.
            float settle = 0f;
            while (settle < 1.2f) { settle += Time.deltaTime; yield return null; }

            var swim = Swim;
            var loco = E2EHarness.Player.Locomotion;
            E2EHarness.Log($"  {자리.ToString("F1")} 수심 {수심:F2}m — 상태 {swim.Current}, " +
                           $"딛음 {(loco != null && loco.IsGrounded)}");

            E2EHarness.AssertEqual(swim.Current, PlayerSwimming.State.Wading,
                                   "발은 잠겼지만 헤엄치지는 않는다");
            E2EHarness.Assert(loco == null || loco.IsGrounded, "바닥을 딛고 서 있다");

            float 전 = Vitals.Health.Current;
            float 전바다 = MacroniumSeaService.TotalDamage;

            float t = 0f;
            while (t < 6f) { t += Time.deltaTime; yield return null; }

            float 후 = Vitals.Health.Current;
            E2EHarness.Log($"  {t:F1}초 담그고 서 있었다 — 체력 {전:F1} → {후:F1}");

            E2EHarness.Assert(후 >= 전 - 0.01f, $"체력이 줄지 않았다 ({전:F1} → {후:F1})");
            E2EHarness.Assert(Mathf.Approximately(MacroniumSeaService.TotalDamage, 전바다),
                              "바다가 한 번도 물지 않았다");
            E2EHarness.Assert(!MacroniumSeaService.IsCorroding, "깎이는 중이 아니다");
        }

        /// <summary>
        /// 서 있을 수 있으면서 발은 잠기는 자리. 수심 0.5~1.0m 사이를 고른다.
        ///
        /// 0.35m(여울 임계)에 너무 붙이면 파도 없는 물에서도 반올림에 걸려 Dry로 떨어지고,
        /// 1.15m(헤엄 임계)에 붙이면 몸이 뜨면서 검사하려던 상태가 아니게 된다.
        /// </summary>
        static bool 발목까지_잠기는_자리를_찾는다(out Vector3 자리, out float 수심)
        {
            자리 = Vector3.zero;
            수심 = 0f;

            var 기준 = E2EHarness.Player.transform.position;
            float best = float.MaxValue;

            for (float dx = -70f; dx <= 70f; dx += 2f)
            for (float dz = -70f; dz <= 70f; dz += 2f)
            {
                float x = 기준.x + dx, z = 기준.z + dz;
                float bed = E2EWaterProbe.BedAt(x, z, _surface);
                if (float.IsNaN(bed)) continue;

                float d = _surface - bed;
                if (d < 0.5f || d > 1.0f) continue;

                float cost = new Vector2(dx, dz).magnitude;
                if (cost >= best) continue;

                best = cost;
                자리 = new Vector3(x, bed, z);
                수심 = d;
            }
            return best < float.MaxValue;
        }

        // ── 2. 건너면 값을 치른다 ───────────────────────────────

        /// <summary>
        /// 이쪽 물가에서 건너편 물가까지 실제로 키를 눌러 건넌다.
        ///
        /// 순간이동으로 물 한가운데 넣고 시간을 재면 "값이 매겨지는가"는 보이지만
        /// <b>그 값이 실제 지형에서 얼마인가</b>는 보이지 않는다. 후자가 이 기능의 전부다 —
        /// 너무 싸면 액면이 아무것도 아니고, 너무 비싸면 건너편이 갈 수 없는 곳이 된다.
        ///
        /// <b>못 찾으면 건너뛰지 않는다(2026-08-07).</b> 여기는 오래 「건너뜀」을 찍고
        /// 초록불로 지나갔는데, 그 로그를 읽는 사람이 없으면 이 대목은 <b>있으면서 없는
        /// 검사</b>다. 뭍이 없으면 <see cref="E2ELandmass"/>가 던지고, 뭍은 있는데 물목이
        /// 없으면 여기서 단언이 깨진다 — 둘 다 사람이 봐야 하는 사건이다.
        /// </summary>
        static IEnumerator 헤엄쳐_건너면_값을_치른다()
        {
            E2EHarness.Log("— 이쪽 물가에서 건너편 물가로 건넌다 —");

            bool 찾았다 = 횡단_구간을_찾는다(out Vector3 출발, out Vector3 도착);
            E2EHarness.Assert(찾았다,
                $"두 뭍({E2ELandmass.StartName}·{E2ELandmass.AcrossName}) 사이 횡단 구간을 찾았다");

            float 폭 = 평면거리(출발, 도착);
            E2EHarness.Log($"  {출발.ToString("F1")} → {도착.ToString("F1")} ({폭:F1}m)");

            float 보통 = 0f, 가속 = 0f, 보통초 = 0f, 가속초 = 0f;
            yield return 건넌다(출발, 도착, false, (비용, 초) => { 보통 = 비용; 보통초 = 초; });
            yield return 건넌다(출발, 도착, true, (비용, 초) => { 가속 = 비용; 가속초 = 초; });

            float 최대 = Vitals.Health.Max;
            E2EHarness.Log($"  보통 수영 {보통초:F1}초에 체력 {보통:F1} ({보통 / 최대:P0}), " +
                           $"Shift {가속초:F1}초에 {가속:F1} ({가속 / 최대:P0})");

            // 값이 실제로 든다. 0이면 규칙이 세계에 닿지 않은 것이다.
            E2EHarness.Assert(보통 > 0f, "맨몸으로 건너면 체력이 준다");

            // 한 번 건너는 것으로 죽지는 않는다. 죽이는 것은 짙은 구간(액면)의 몫이다.
            E2EHarness.Assert(보통 / 최대 >= 0.2f,
                              $"횡단이 너무 싸다 ({보통 / 최대:P0}) — 바다가 아무것도 아니게 된다");
            E2EHarness.Assert(보통 / 최대 <= 0.4f,
                              $"횡단이 너무 비싸다 ({보통 / 최대:P0}) — 건너편이 갈 수 없는 곳이 된다");

            // 가속이 생존기여야 한다. 몇 퍼센트 아끼는 정도면 아무도 누르지 않는다.
            E2EHarness.Assert(가속 < 보통 * 0.85f,
                              $"Shift로 건너면 유의미하게 덜 든다 (보통 {보통:F1} → 가속 {가속:F1})");

            Vitals.Revive();
            yield return null;
        }

        /// <summary>
        /// 한 번 건너고 그동안 잃은 체력을 돌려준다.
        ///
        /// 잃은 체력을 <b>바다가 낸 피해와 견주어</b> 확인한다. 총량만 보면 다른 무엇이
        /// 깎았어도 통과하고, 그러면 이 시나리오는 바다를 검사하는 것이 아니게 된다.
        /// </summary>
        static IEnumerator 건넌다(Vector3 출발, Vector3 도착, bool 가속,
                                System.Action<float, float> 결과)
        {
            var swim = Swim;
            var rig = E2EHarness.Player.CameraRig;
            var body = E2EHarness.Player.transform;

            Vitals.Revive();
            E2EHarness.Teleport(출발 + Vector3.up * 1.3f);
            yield return null;

            float settle = 0f;
            while (settle < 0.9f) { settle += Time.deltaTime; yield return null; }

            겨눈다(rig, body, 도착);
            yield return null;

            float 전체력 = Vitals.Health.Current;
            float 전바다 = MacroniumSeaService.TotalDamage;

            if (가속) yield return E2EHarness.PressKey(Key.LeftShift);
            yield return E2EHarness.PressKey(Key.W);

            float t = 0f, 잠김 = 0f;
            bool 닿았다 = false;
            while (t < 45f)
            {
                겨눈다(rig, body, 도착);
                E2EHarness.QueueKeys();

                if (MacroniumSeaService.IsCorroding) 잠김 += Time.deltaTime;
                if (잠김 > 1f && 평면거리(body.position, 도착) < 3f) { 닿았다 = true; break; }
                if (Vitals.Health.IsEmpty) break;

                t += Time.deltaTime;
                yield return null;
            }
            yield return E2EHarness.ReleaseAllKeys();

            float 잃은것 = 전체력 - Vitals.Health.Current;
            float 바다가낸것 = MacroniumSeaService.TotalDamage - 전바다;

            E2EHarness.Log($"    가속={가속} 도착={닿았다} {t:F1}초 (잠김 {잠김:F1}초) " +
                           $"체력 -{잃은것:F1}, 그중 바다 {바다가낸것:F1}");

            E2EHarness.Assert(닿았다, $"헤엄쳐서 건너편 물가에 닿았다 (가속={가속})");
            E2EHarness.Assert(Mathf.Abs(잃은것 - 바다가낸것) < 0.01f,
                              $"잃은 체력은 전부 바다가 낸 것이다 (잃음 {잃은것:F1}, 바다 {바다가낸것:F1})");

            // 규칙이 그대로 적용됐는가. 물린 횟수는 잠긴 시간을 간격으로 나눈 값이어야 한다.
            float 기대 = Mathf.Floor(잠김 / MacroniumSea.BiteInterval) * MacroniumSea.BiteDamage;
            E2EHarness.Assert(Mathf.Abs(바다가낸것 - 기대) <= MacroniumSea.BiteDamage + 0.01f,
                              $"잠긴 시간만큼 물렸다 (실제 {바다가낸것:F1}, 기대 {기대:F1})");

            결과(잃은것, 잠김);

            Vitals.Revive();
            float 쉼 = 0f;
            while (쉼 < 0.3f) { 쉼 += Time.deltaTime; yield return null; }
        }

        static void 겨눈다(PlayerCameraRig rig, Transform body, Vector3 목표)
        {
            var d = 목표 - body.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.25f) rig.SetLook(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0f);
        }

        /// <summary>
        /// 두 뭍 사이에서 가장 가까운 물가 한 쌍.
        ///
        /// 좌표를 적어 두지 않는 이유: 지형은 통째로 다시 만들어진다(스펙 §13). 그때 이
        /// 시나리오가 조용히 물속 벽을 향해 헤엄치게 두느니, 매번 지형에 물어 찾는 편이 낫다.
        ///
        /// <b>뭍 자체가 없으면 여기서 던진다.</b> 이름이 하나 바뀐 것을 「물목이 없다」로
        /// 읽으면 시나리오가 눈이 먼 채로 초록불이 된다 — <see cref="E2ELandmass"/> 참고.
        /// </summary>
        static bool 횡단_구간을_찾는다(out Vector3 출발, out Vector3 도착)
        {
            출발 = Vector3.zero;
            도착 = Vector3.zero;

            Bounds b1 = E2ELandmass.RequireBounds(E2ELandmass.StartName);
            Bounds b2 = E2ELandmass.RequireBounds(E2ELandmass.AcrossName);

            // 두 뭍 사이의 중간선. 이 선을 기준으로 물가를 양쪽으로 가른다.
            float 가름 = (b1.max.x + b2.min.x) * 0.5f;

            var 왼쪽 = new System.Collections.Generic.List<Vector3>();
            var 오른쪽 = new System.Collections.Generic.List<Vector3>();

            float minZ = Mathf.Max(b1.min.z, b2.min.z) - 20f;
            float maxZ = Mathf.Min(b1.max.z, b2.max.z) + 20f;

            for (float x = b1.center.x; x <= b2.center.x; x += 2f)
            for (float z = minZ; z <= maxZ; z += 2f)
            {
                float bed = E2EWaterProbe.BedAt(x, z, _surface);
                if (float.IsNaN(bed)) continue;

                // 물가: 수면보다 확실히 위이면서, 걸어 들어갈 수 있을 만큼 낮은 곳
                if (bed < _surface + 0.4f || bed > _surface + 4f) continue;

                (x < 가름 ? 왼쪽 : 오른쪽).Add(new Vector3(x, bed, z));
            }
            if (왼쪽.Count == 0 || 오른쪽.Count == 0) return false;

            float best = float.MaxValue;
            foreach (var a in 왼쪽)
            foreach (var b in 오른쪽)
            {
                float d = 평면거리(a, b);
                if (d >= best) continue;
                best = d;
                출발 = a;
                도착 = b;
            }
            return best < float.MaxValue;
        }

        // ── 3. 버티면 죽는다 ────────────────────────────────────

        /// <summary>
        /// 깊은 곳에 잠긴 채 두면 바다가 끝까지 깎는다. 그리고 기존 고리가 받는다 —
        /// 가방을 남기고, 다시 일어선다.
        ///
        /// <b>체력을 미리 덜어 두는 이유.</b> 온전한 체력으로 죽으려면 33초를 기다려야 하는데,
        /// 여기서 볼 것은 <i>죽는 데 걸리는 시간</i>이 아니라 <i>죽음이 고리를 타는가</i>다.
        /// 초당 얼마인지는 바로 위에서 실제 횡단으로 이미 쟀다.
        /// </summary>
        static IEnumerator 잠긴_채_버티면_죽고_가방을_남긴다()
        {
            E2EHarness.Log("— 잠긴 채 버틴다 —");

            bool 찾음 = E2EWaterProbe.TryFindDeepest(E2EHarness.Player.transform.position, _surface,
                                                    60f, out var bed, out float 수심);
            E2EHarness.Assert(찾음 && 수심 > 2.5f, $"몸이 잠길 만큼 깊은 곳이 있다 (수심 {수심:F1}m)");
            if (!찾음 || 수심 <= 2.5f) yield break;

            Vitals.Revive();
            채운다();
            E2EHarness.Assert(Inv.CountOf(벌어온것[0]) > 0, "잠기기 전에 떨굴 것이 있다");

            E2EHarness.Teleport(bed + Vector3.up * 1.0f);
            yield return null;
            yield return null;

            yield return E2EHarness.WaitUntil(() => MacroniumSeaService.IsCorroding,
                                              "몸이 잠기자 바다가 물기 시작한다", 5f);

            var 벌어온수량 = new int[벌어온것.Length];
            for (int i = 0; i < 벌어온것.Length; i++) 벌어온수량[i] = Inv.CountOf(벌어온것[i]);

            int 이전가방수 = DeathDropBag.Active.Count;
            int 이전부활 = RespawnService.RespawnCount;

            // 남은 체력을 한 줌만 남긴다. 위 주석 참고 — 시간을 재는 자리가 아니다.
            Vitals.Health.Modify(-(Vitals.Health.Current - 12f));
            E2EHarness.Log($"  체력 {Vitals.Health.Current:F0}에서 시작 (수심 {수심:F1}m)");

            float t = 0f;
            while (t < 25f && !Vitals.Health.IsEmpty) { t += Time.deltaTime; yield return null; }

            var 죽은자리 = E2EHarness.Player.transform.position;
            E2EHarness.Log($"  {t:F1}초 만에 체력이 0이 됐다 (바다 누적 {MacroniumSeaService.TotalDamage:F0})");
            E2EHarness.Assert(Vitals.Health.IsEmpty, "잠긴 채 두면 바다가 끝까지 깎는다");

            yield return E2EHarness.WaitUntil(() => DeathDropBag.Active.Count > 이전가방수,
                                              "빠져 죽은 자리에 가방이 남는다", 5f);

            var bag = DeathDropService.LastBag;
            E2EHarness.Assert(bag != null, "남긴 가방을 집을 수 있다");

            if (bag != null)
            {
                for (int i = 0; i < 벌어온것.Length; i++)
                {
                    E2EHarness.AssertEqual(Inv.CountOf(벌어온것[i]), 0,
                                           $"바다에 녹으면서 {벌어온것[i]}를 전부 잃었다");
                    E2EHarness.AssertEqual(bag.Contents.CountOf(벌어온것[i]), 벌어온수량[i],
                                           $"가방에 {벌어온것[i]}가 그대로 들어 있다");
                }

                float 거리 = 평면거리(bag.transform.position, 죽은자리);
                E2EHarness.Assert(거리 < 3f, $"가방은 죽은 자리에 남는다 ({거리:F1}m)");
            }

            yield return E2EHarness.WaitUntil(() => RespawnService.RespawnCount > 이전부활,
                                              "잠깐 뒤 스스로 되살아난다", 8f);
            yield return null;

            E2EHarness.Assert(!Vitals.Health.IsEmpty, "체력이 돌아왔다");

            // 부활 지점이 물속이면 일어서자마자 다시 녹는다. 그건 배치(§8-4)의 문제이지
            // 규칙의 문제가 아니므로, 여기서는 되살아난 뒤 마른 땅에 세워 두고 끝낸다.
            yield return 마른_땅으로_돌아간다();
        }

        /// <summary>이어서 도는 시나리오가 물속에서 시작하지 않게 정리한다.</summary>
        static IEnumerator 마른_땅으로_돌아간다()
        {
            var 기준 = E2EHarness.Player.transform.position;

            for (float r = 0f; r <= 80f; r += 4f)
            for (float a = 0f; a < 360f; a += 30f)
            {
                var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                float x = 기준.x + dir.x * r, z = 기준.z + dir.z * r;

                float bed = E2EWaterProbe.BedAt(x, z, _surface);
                if (float.IsNaN(bed) || bed < _surface + 1.5f) continue;

                E2EHarness.Teleport(new Vector3(x, bed + 1.2f, z));
                yield return null;
                float settle = 0f;
                while (settle < 0.8f) { settle += Time.deltaTime; yield return null; }

                Vitals.Revive();
                E2EHarness.Log($"  마른 땅 {E2EHarness.Player.transform.position.ToString("F1")}로 나왔다");
                yield break;
            }

            E2EHarness.Log("  [주의] 마른 땅을 찾지 못했다");
        }

        // ── 공통 ────────────────────────────────────────────────

        static float 평면거리(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
