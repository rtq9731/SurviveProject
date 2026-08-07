using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Survive.Player;
using Survive.Vitals;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// 액체에 <b>종류</b>가 섰다 (스펙 §3, 사용자 판단 2026-08-07).
    ///
    /// <b>같은 시나리오 안에서 같은 자리를 두 번 건넌다.</b> 한 번은 매크로늄 바다로,
    /// 한 번은 진짜 물이 고인 호수로. 출발점도 도착점도 조작도 같고 <b>다른 것은
    /// 종류 하나뿐</b>이므로, 체력 손실이 갈리면 그것은 종류가 낸 차이다.
    /// 두 시나리오로 나누어 재면 지형·경로·프레임이 달라져서 그 단언을 못 한다.
    ///
    /// <b>호수를 왜 여기서 파는가.</b> 지상 지형 재작업은 사람의 몫이라(스펙 §13)
    /// 씬에는 아직 진짜 물이 한 방울도 없다. 그렇다고 "바다만 검사한다"로 두면
    /// 이 라운드가 세운 규칙의 절반이 한 번도 세계에 닿아 보지 못한다. 그래서
    /// <b>같은 물길 위에 물을 임시로 채웠다 뺀다</b> - 호수 바닥을 파는 것은 사람의
    /// 일이지만, 물이 물인지 매크로늄인지는 기계가 지금 물을 수 있다.
    /// </summary>
    public static class E2ELiquidKind
    {
        const string 임시호수이름 = "E2E_TempLake";

        /// <summary>호수의 수면을 바다보다 이만큼 올린다(m). 겹치면 높은 쪽이 이긴다.</summary>
        const float 호수를_올리는_높이 = 0.25f;

        static PlayerVitals Vitals => E2EHarness.Player.Vitals;
        static PlayerSwimming Swim =>
            Object.FindAnyObjectByType<PlayerSwimming>(FindObjectsInactive.Exclude);

        static float _바다수면;
        static Vector3 _출발;
        static Vector3 _도착;

        static float _바다초, _바다잠김, _바다피해;
        static float _호수초, _호수잠김, _호수피해;

        public static IEnumerator FullRun()
        {
            yield return 준비();

            yield return 바다를_건넌다();
            yield return 호수를_채운다();
            yield return 호수를_건넌다();
            yield return 호수에서_잠수하면_산소는_그대로_준다();
            호수를_비운다();

            결산();
        }

        // ── 준비 ────────────────────────────────────────────────

        static IEnumerator 준비()
        {
            치운다();   // 앞 판이 남긴 호수가 있으면 먼저 없앤다

            E2EHarness.Assert(Swim != null, "PlayerSwimming이 있다");
            E2EHarness.Assert(Vitals != null, "PlayerVitals가 있다");
            E2EHarness.Assert(MacroniumSeaService.Instance != null, "액체 서비스가 스스로 서 있다");

            E2EHarness.Assert(E2EWaterProbe.TryFindSurface(out _바다수면, out var 액체),
                              "씬에 액체가 있다");
            E2EHarness.Assert(액체 != null, "액체 오브젝트를 집었다");

            var body = 액체 != null ? 액체.GetComponent<WaterBody>() : null;
            E2EHarness.Assert(body != null, "그 액체에 WaterBody가 붙어 있다");
            E2EHarness.Assert(body != null && body.HasKind, "씬의 액체에 종류가 적혀 있다");
            E2EHarness.AssertEqual(body.Kind, LiquidKind.Macronium,
                                   $"씬의 액체는 매크로늄 바다다 ({액체.name})");
            E2EHarness.Log($"  {액체.name} 수면 y={_바다수면:F2}, 종류 {body.Kind}");

            Vitals.Revive();
            yield return null;

            E2EHarness.Assert(물길을_찾는다(out _출발, out _도착),
                              "물가에서 물 쪽으로 뻗은 물길을 찾았다");
            E2EHarness.Log($"  물길 {_출발.ToString("F1")} → {_도착.ToString("F1")} " +
                           $"({평면거리(_출발, _도착):F1}m)");
        }

        /// <summary>
        /// 뭍에서 시작해 깊은 데로 뻗는 직선 하나.
        ///
        /// 좌표를 적어 두지 않는다 - 지상 지형은 사람이 다시 만들 것이고, 그때
        /// 이 시나리오가 조용히 물속 벽을 향해 헤엄치게 두느니 매번 물어 찾는 편이 낫다.
        /// </summary>
        static bool 물길을_찾는다(out Vector3 출발, out Vector3 도착)
        {
            출발 = Vector3.zero;
            도착 = Vector3.zero;

            var 기준 = E2EHarness.Player.transform.position;
            if (!E2EWaterProbe.TryFindShore(기준, _바다수면, 70f,
                                            out var 물가, out var 물쪽, out _)) return false;

            출발 = 물가;

            // 뭍에서 물 쪽으로 뻗으며, 계속 헤엄칠 깊이가 나오는 데까지 간다.
            // 도중에 바닥이 올라오면 거기서 끊는다 - 걸어 넘는 구간이 섞이면
            // 두 횡단의 잠긴 시간이 달라져서 종류 말고 다른 것이 답을 바꾼다.
            //
            // <b>바닥이 안 잡히는 곳(NaN)은 얕은 것이 아니라 바닥이 없는 것이다.</b>
            // 섬과 섬 사이의 열린 물이 그렇다. 그것을 끊는 조건으로 읽으면 물길이
            // 물가 앞 10m에서 끝나 버린다(실측: d=14부터 NaN).
            // <b>물가 경사는 얕은 것이 정상이다.</b> 처음 몇 미터가 얕다고 끊으면
            // 물길이 아예 서지 않는다(실측: d=4에서 수심 1.4m). 한 번 깊어진 뒤에
            // 다시 얕아지는 자리가 건너편 물가이고, 거기서 끊는다.
            const float 가장먼끝 = 40f;
            float 끝 = 0f;
            bool 깊어졌다 = false;

            for (float d = 4f; d <= 가장먼끝; d += 2f)
            {
                var p = 물가 + 물쪽 * d;
                float bed = E2EWaterProbe.BedAt(p.x, p.z, _바다수면);
                bool 깊다 = float.IsNaN(bed) ||
                           _바다수면 - bed >= LiquidCrossing.SwimDepth + 0.5f;

                if (깊다) { 깊어졌다 = true; 끝 = d; continue; }
                if (깊어졌다) break;
            }
            if (끝 < 14f) return false;

            도착 = 물가 + 물쪽 * 끝;
            도착.y = _바다수면;
            return true;
        }

        // ── 바다 ────────────────────────────────────────────────

        static IEnumerator 바다를_건넌다()
        {
            E2EHarness.Log("[ 매크로늄 바다를 건넌다 ]");

            yield return 건넌다(LiquidKind.Macronium, (초, 잠김, 피해) =>
            {
                _바다초 = 초; _바다잠김 = 잠김; _바다피해 = 피해;
            });

            E2EHarness.Assert(_바다잠김 > 2f,
                              $"바다에 실제로 잠겨 있었다 ({_바다잠김:F1}초)");
            E2EHarness.Assert(_바다피해 > 0f,
                              "매크로늄에 잠긴 채 건너면 체력이 준다");

            // 규칙이 예측한 값 근처인가. 세계와 규칙이 갈라지면 여기서 걸린다.
            float 예측 = LiquidCrossing.DamageOver(LiquidKind.Macronium,
                                                  SeaImmersion.Swimming, false, _바다잠김);
            E2EHarness.Assert(Mathf.Abs(_바다피해 - 예측) <= MacroniumSea.BiteDamage + 0.01f,
                              $"잠긴 시간만큼 물렸다 (실측 {_바다피해:F1}, 규칙 {예측:F1})");
        }

        // ── 호수 ────────────────────────────────────────────────

        /// <summary>
        /// 같은 물길 위에 진짜 물을 채운다.
        ///
        /// 바다보다 수면을 조금 올린다 - 겹친 액체 중 높은 쪽이 이기므로(<see cref="WaterBody"/>)
        /// 이 한 뼘이 "여기는 호수다"가 된다. 뭍 위의 호수가 바다보다 높은 것은
        /// 물리적으로도 그쪽이 자연스럽다.
        /// </summary>
        static IEnumerator 호수를_채운다()
        {
            E2EHarness.Log("[ 같은 자리에 호수를 채운다 ]");

            var 한가운데 = (_출발 + _도착) * 0.5f;
            float 폭 = 평면거리(_출발, _도착) + 60f;

            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = 임시호수이름;

            // 재생 중에만 서 있을 물이라 저장되지 않는다. 다만 재생을 멈추면 남으므로
            // 시나리오 끝에서 스스로 치운다.
            go.SetActive(false);
            Object.Destroy(go.GetComponent<Collider>());   // 지형 조사와 이동을 막으면 안 된다

            var rend = go.GetComponent<Renderer>();
            if (rend != null) rend.enabled = false;         // 화면에 흰 판이 뜨지 않게

            go.transform.position = new Vector3(한가운데.x, _바다수면 + 호수를_올리는_높이,
                                                한가운데.z);
            go.transform.localScale = new Vector3(폭 / 10f, 1f, 폭 / 10f);

            var lake = go.AddComponent<WaterBody>();
            lake.SetKind(LiquidKind.Water);                 // 켜지기 전에 종류를 적는다
            go.SetActive(true);

            yield return null;

            E2EHarness.Assert(WaterBody.TryGetAt(_도착, out float 수면, out var 종류),
                              "물길 한가운데에 액체가 있다");
            E2EHarness.AssertEqual(종류, LiquidKind.Water,
                                   "이제 그 자리의 액체는 진짜 물이다");
            E2EHarness.Log($"  호수 수면 y={수면:F2} (바다보다 {수면 - _바다수면:F2}m 높다)");
        }

        static IEnumerator 호수를_건넌다()
        {
            E2EHarness.Log("[ 같은 자리를 호수로 건넌다 ]");

            yield return 건넌다(LiquidKind.Water, (초, 잠김, 피해) =>
            {
                _호수초 = 초; _호수잠김 = 잠김; _호수피해 = 피해;
            });

            E2EHarness.Assert(_호수잠김 > 2f,
                              $"호수에도 실제로 잠겨 있었다 ({_호수잠김:F1}초). " +
                              "안 잠긴 채 「안 깎였다」를 말하면 아무것도 잰 것이 아니다");
            E2EHarness.Assert(_호수피해 <= 0.001f,
                              $"호수에서는 체력이 한 점도 줄지 않는다 (실측 {_호수피해:F2})");
            E2EHarness.Assert(MacroniumSeaService.Bites == _건너기전_물린횟수,
                              "호수는 한 번도 물지 않았다");
        }

        /// <summary>
        /// <b>호수에서 잠수해도 산소는 똑같이 준다.</b>
        ///
        /// 종류가 가르는 것은 부식 하나뿐이라는 판단(스펙 §3)의 나머지 반쪽이다.
        /// 숨은 액체의 성분이 아니라 머리가 잠겼는가의 문제이므로, 여기서 산소가
        /// 안 줄면 「호수에서는 숨을 쉴 수 있다」가 되어 잠수 설계가 통째로 갈린다.
        ///
        /// <b>Ctrl로 가라앉는 이유.</b> 뜬 채로는 머리가 물 위에 남아 산소가 줄지
        /// 않는다. 그러면 "안 줄었다"가 아니라 "안 잠겼다"인데 그 둘을 구별하지
        /// 못하면 산소 소모가 죽어도 이 검사가 통과한다.
        /// </summary>
        static IEnumerator 호수에서_잠수하면_산소는_그대로_준다()
        {
            E2EHarness.Log("[ 호수에서 잠수한다 ]");

            var swim = Swim;
            Vitals.Revive();
            yield return null;

            yield return E2EHarness.PressKey(Key.LeftCtrl);

            float 가라앉음 = 0f;
            while (가라앉음 < 5f && !swim.IsHeadSubmerged)
            {
                E2EHarness.QueueKeys();
                가라앉음 += Time.deltaTime;
                yield return null;
            }
            E2EHarness.Assert(swim.IsHeadSubmerged,
                              $"Ctrl로 가라앉아 머리가 잠긴다 ({가라앉음:F1}초)");
            E2EHarness.AssertEqual(MacroniumSeaService.LastLiquid, LiquidKind.Water,
                                   "잠긴 곳은 호수다");

            float 전산소 = Vitals.Oxygen.Current;
            float 전체력 = Vitals.Health.Current;

            float t = 0f;
            while (t < 2.5f)
            {
                E2EHarness.QueueKeys();
                t += Time.deltaTime;
                yield return null;
            }
            yield return E2EHarness.ReleaseKey(Key.LeftCtrl);

            float 후산소 = Vitals.Oxygen.Current;
            E2EHarness.Log($"    잠긴 채 {t:F1}초 - 산소 {전산소:F1} → {후산소:F1} " +
                           $"(초당 {(후산소 - 전산소) / t:F1}), 체력 {전체력:F1} → {Vitals.Health.Current:F1}");

            E2EHarness.Assert(후산소 < 전산소 - 1f,
                              "호수에 잠겨도 산소는 똑같이 준다 - 숨은 성분의 문제가 아니다");
            E2EHarness.Assert(Vitals.Health.Current >= 전체력 - 0.01f,
                              "그래도 체력은 한 점도 줄지 않는다 - 갈리는 것은 부식뿐이다");

            Vitals.Revive();
            yield return null;
        }

        static void 호수를_비운다()
        {
            치운다();
            E2EHarness.Assert(!WaterBody.TryGetAt(_도착, out _, out var 종류) ||
                              종류 == LiquidKind.Macronium,
                              "호수를 비우면 그 자리는 다시 매크로늄 바다다");
        }

        static void 치운다()
        {
            foreach (var w in Object.FindObjectsByType<WaterBody>(FindObjectsInactive.Include))
                if (w != null && w.name == 임시호수이름) Object.DestroyImmediate(w.gameObject);
        }

        // ── 공통 횡단 ───────────────────────────────────────────

        static int _건너기전_물린횟수;

        /// <summary>
        /// 물가에서 물 쪽으로 실제로 키를 눌러 나아간다. <b>두 종류가 같은 코드를 탄다.</b>
        ///
        /// 순간이동으로 물 한가운데 넣고 시간을 재면 "값이 매겨지는가"는 보이지만
        /// 실제 지형에서 얼마인지는 보이지 않는다. 그리고 무엇보다 <b>두 번을 똑같이</b>
        /// 해야 하므로, 출발점도 조준도 종료 조건도 한 곳에 적어 둔다.
        /// </summary>
        static IEnumerator 건넌다(LiquidKind 기대종류, System.Action<float, float, float> 결과)
        {
            var rig = E2EHarness.Player.CameraRig;
            var body = E2EHarness.Player.transform;

            Vitals.Revive();
            E2EHarness.Teleport(_출발 + Vector3.up * 1.2f);
            yield return null;

            float settle = 0f;
            while (settle < 1.0f) { settle += Time.deltaTime; yield return null; }

            겨눈다(rig, body, _도착);
            yield return null;

            float 전체력 = Vitals.Health.Current;
            float 전피해 = MacroniumSeaService.TotalDamage;
            _건너기전_물린횟수 = MacroniumSeaService.Bites;

            yield return E2EHarness.PressKey(Key.W);

            float t = 0f, 잠김 = 0f;
            bool 닿았다 = false;
            bool 종류를봤다 = false;

            while (t < 40f)
            {
                겨눈다(rig, body, _도착);
                E2EHarness.QueueKeys();

                // 종류를 묻지 않는 「노출」로 잰다. 부식으로 재면 호수 쪽 시간이
                // 언제나 0이 되어 "안 깎였다"와 "안 잠겼다"를 가를 수 없다.
                if (MacroniumSeaService.LastExposed)
                {
                    잠김 += Time.deltaTime;
                    if (MacroniumSeaService.LastLiquid == 기대종류) 종류를봤다 = true;
                }

                if (잠김 > 0.5f && 평면거리(body.position, _도착) < 4f) { 닿았다 = true; break; }
                if (Vitals.Health.IsEmpty) break;

                t += Time.deltaTime;
                yield return null;
            }
            yield return E2EHarness.ReleaseAllKeys();

            float 잃은것 = 전체력 - Vitals.Health.Current;
            float 액체가낸것 = MacroniumSeaService.TotalDamage - 전피해;

            E2EHarness.Log($"    {기대종류}: {t:F1}초 (헤엄 {잠김:F1}초) " +
                           $"{_출발.ToString("F1")} → {body.position.ToString("F1")} " +
                           $"체력 {전체력:F1} → {Vitals.Health.Current:F1} " +
                           $"(-{잃은것:F1}, 그중 액체 {액체가낸것:F1})");

            E2EHarness.Assert(닿았다, $"{기대종류}: 헤엄쳐서 목표에 닿았다");
            E2EHarness.Assert(종류를봤다, $"{기대종류}: 헤엄치는 동안 세계가 그 종류를 읽고 있었다");
            E2EHarness.Assert(Mathf.Abs(잃은것 - 액체가낸것) < 0.01f,
                              $"{기대종류}: 잃은 체력은 전부 액체가 낸 것이다 " +
                              $"(잃음 {잃은것:F1}, 액체 {액체가낸것:F1})");

            결과(t, 잠김, 액체가낸것);

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

        static float 평면거리(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        // ── 결산 ────────────────────────────────────────────────

        static void 결산()
        {
            E2EHarness.Log("=== 같은 자리, 종류만 갈았다 ===");
            E2EHarness.Log($"  물길 {_출발.ToString("F1")} → {_도착.ToString("F1")} " +
                           $"({평면거리(_출발, _도착):F1}m)");
            E2EHarness.Log($"  매크로늄 바다  {_바다초:F1}초 (헤엄 {_바다잠김:F1}초) " +
                           $"체력 -{_바다피해:F1}");
            E2EHarness.Log($"  호수          {_호수초:F1}초 (헤엄 {_호수잠김:F1}초) " +
                           $"체력 -{_호수피해:F1}");

            // 이 한 줄이 이 라운드의 알맹이다. 같은 자리, 같은 조작, 다른 답.
            E2EHarness.Assert(_바다피해 > 0f && _호수피해 <= 0.001f,
                              $"같은 자리를 건너는데 바다는 {_바다피해:F1}, 호수는 {_호수피해:F1}");
        }
    }
}
