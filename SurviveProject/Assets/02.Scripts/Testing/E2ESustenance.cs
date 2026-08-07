using System.Collections;
using UnityEngine;
using Survive.UI;
using Survive.Vitals;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// 수분과 식량 (스펙 §6).
    ///
    /// <b>이 시나리오의 절반은 「아무 일도 일어나지 않는다」를 잰다.</b> 보통 E2E는
    /// 무언가가 작동하는 것을 확인하는데, 이 항목의 설계 목표는 <b>눈에 띄지 않는
    /// 것</b>이다 — 이전 기획이 수분·식량을 명시적으로 기각했고 그 근거(*"시계가
    /// 둘이면 하나는 반드시 잡무가 된다"*)가 지금도 옳기 때문이다. 그래서
    /// <b>원정 한 번을 통째로 돌려 보고 화면에 아무것도 뜨지 않는 것</b>을 단언한다.
    ///
    /// <b>호수를 왜 여기서 파는가.</b> 앞 라운드(액체 종류)와 같은 사정이다 —
    /// 지상 지형 재작업은 사람의 몫이라 씬에는 아직 진짜 물이 한 방울도 없다.
    /// 같은 물길 위에 물을 임시로 채웠다 뺀다. 호수 바닥을 파는 것은 사람의 일이지만,
    /// <b>물가에 서면 채워지는가</b>는 기계가 지금 물을 수 있다.
    /// </summary>
    public static class E2ESustenance
    {
        const string 임시호수이름 = "E2E_TempLake_Sustenance";

        /// <summary>호수의 수면을 바다보다 이만큼 올린다(m). 겹치면 높은 쪽이 이긴다.</summary>
        const float 호수를_올리는_높이 = 0.25f;

        static PlayerVitals Vitals => E2EHarness.Player.Vitals;

        static float _바다수면;
        static Vector3 _물가;
        static Vector3 _물쪽;
        static Vector3 _깊은곳;

        static float _원정초, _원정수분, _원정식량;
        static float _바다에서_늘어난_수분;
        static float _채우는데_걸린초;
        static float _바닥에서_잃은_체력;

        public static IEnumerator FullRun()
        {
            yield return 준비();

            yield return 원정_한_번을_돌아도_경고가_뜨지_않는다();
            yield return 바다는_목을_축여_주지_않는다();
            yield return 호수에서_채운다();
            yield return 바닥을_쳐도_죽지_않는다();

            치운다();
            결산();
        }

        // ── 준비 ────────────────────────────────────────────────

        static IEnumerator 준비()
        {
            치운다();
            SustenanceService.PurifierAvailable = false;

            E2EHarness.Assert(SustenanceService.Instance != null,
                              "수분·식량 서비스가 스스로 서 있다 (RuntimeInitializeOnLoadMethod)");
            E2EHarness.Assert(SustenanceView.Instance != null,
                              "수분·식량 화면이 스스로 서 있다");
            E2EHarness.Assert(Vitals != null && Vitals.Hydration != null && Vitals.Food != null,
                              "몸이 수분과 식량을 갖고 있다");

            E2EHarness.Assert(Vitals.TryGet(Sustenance.HydrationId, out var byId) &&
                              byId == Vitals.Hydration,
                              "게이지가 아이디로도 집힌다 — 하드코딩된 필드 둘이 아니다");

            E2EHarness.AssertEqual(Vitals.All.Count, 4, "게이지는 넷이다 (체력·산소·수분·식량)");

            E2EHarness.Assert(E2EWaterProbe.TryFindSurface(out _바다수면, out var 액체),
                              "씬에 액체가 있다");
            var body = 액체 != null ? 액체.GetComponent<WaterBody>() : null;
            E2EHarness.Assert(body != null && body.HasKind, "그 액체에 종류가 적혀 있다");
            E2EHarness.AssertEqual(body.Kind, LiquidKind.Macronium, "씬의 액체는 매크로늄 바다다");

            Vitals.Revive();
            가득_채운다();
            yield return null;

            E2EHarness.Assert(물길을_찾는다(), "물가에서 물 쪽으로 뻗은 물길을 찾았다");
            E2EHarness.Log($"  물가 {_물가.ToString("F1")} → 깊은 곳 {_깊은곳.ToString("F1")}");
        }

        /// <summary>
        /// 뭍에서 시작해 깊은 데로 뻗는 직선 하나. 좌표를 적어 두지 않는 이유는
        /// 지상 지형을 사람이 다시 만들 것이기 때문이다 (앞 라운드와 같은 판단).
        /// </summary>
        static bool 물길을_찾는다()
        {
            var 기준 = E2EHarness.Player.transform.position;
            if (!E2EWaterProbe.TryFindShore(기준, _바다수면, 70f, out _물가, out _물쪽, out _))
                return false;

            const float 가장먼끝 = 40f;
            float 끝 = 0f;
            bool 깊어졌다 = false;

            for (float d = 4f; d <= 가장먼끝; d += 2f)
            {
                var p = _물가 + _물쪽 * d;
                float bed = E2EWaterProbe.BedAt(p.x, p.z, _바다수면);
                bool 깊다 = float.IsNaN(bed) ||
                           _바다수면 - bed >= LiquidCrossing.SwimDepth + 0.5f;

                if (깊다) { 깊어졌다 = true; 끝 = d; continue; }
                if (깊어졌다) break;
            }
            if (끝 < 14f) return false;

            _깊은곳 = _물가 + _물쪽 * 끝;
            _깊은곳.y = _바다수면;
            return true;
        }

        // ── ① 원정 한 번 ────────────────────────────────────────

        /// <summary>
        /// <b>이것이 이 라운드의 게이트다.</b> 원정 한 번(실측 62초)을 통째로 돌려
        /// 보고, 그동안 화면에 아무것도 뜨지 않는지 본다.
        ///
        /// <b>물에 닿지 않는 것을 함께 단언한다.</b> 도중에 발이 물에 잠기면 그 사이
        /// 게이지가 도로 차오르고, 그러면 「안 줄었다」가 아니라 「채워졌다」인데
        /// 그 둘을 가르지 못하면 감소 속도가 죽어도 이 검사가 통과한다.
        /// </summary>
        static IEnumerator 원정_한_번을_돌아도_경고가_뜨지_않는다()
        {
            E2EHarness.Log("[ 원정 한 번(62초)을 돈다 ]");

            가득_채운다();
            yield return null;

            var 출발 = E2EHarness.Player.transform.position;
            float 전수분 = Vitals.Hydration.Current;
            float 전식량 = Vitals.Food.Current;

            _시작시각 = Time.time;
            _경고가떴다 = false;
            _화면에떴다 = false;
            _물에닿았다 = false;
            int 왕복 = 0;

            // 실제로 걷는다. 걸어도 값이 같다는 것까지가 「시간으로만 준다」의 확인이다.
            // 물 반대쪽으로 뻗는다 - 발이 물에 잠기면 그 사이 게이지가 도로 차오른다.
            while (!표본을_찍는다())
            {
                if (!_물에닿았다 &&
                    E2EHarness.TryFindClearSpot(출발, -_물쪽, 12f, out var 저기))
                {
                    yield return E2EHarness.WalkTo(저기, 2.0f, 20f, false, 표본을_찍는다);
                    yield return E2EHarness.WalkTo(출발, 2.0f, 20f, false, 표본을_찍는다);
                    왕복++;
                }
                else
                {
                    // 걸을 자리를 못 찾아도 시간은 흘러야 한다 - 감소는 걸음이
                    // 아니라 시간이 정하므로, 서 있어도 재는 것은 같다.
                    yield return null;
                }
            }

            float t = Time.time - _시작시각;
            bool 경고가떴다 = _경고가떴다;
            bool 화면에떴다 = _화면에떴다;
            bool 물에닿았다 = _물에닿았다;

            _원정초 = t;
            _원정수분 = 전수분 - Vitals.Hydration.Current;
            _원정식량 = 전식량 - Vitals.Food.Current;

            E2EHarness.Log($"    {_원정초:F1}초, 왕복 {왕복}번 " +
                           $"수분 {전수분:F1} → {Vitals.Hydration.Current:F1} (-{_원정수분:F1}) " +
                           $"식량 {전식량:F1} → {Vitals.Food.Current:F1} (-{_원정식량:F1})");

            E2EHarness.Assert(!물에닿았다,
                              "원정 내내 발이 마른 채였다 — 물에 닿았으면 잰 것이 감소가 아니다");
            E2EHarness.Assert(_원정수분 > 0f && _원정식량 > 0f,
                              "실제로 줄기는 줄었다 (0이면 규칙이 아예 안 돌고 있는 것이다)");

            E2EHarness.Assert(!경고가떴다,
                              $"원정 한 번에 경고가 떴다. 수분 {Vitals.Hydration.Normalized:P0} · " +
                              $"식량 {Vitals.Food.Normalized:P0} — 이 선이 무너지면 두 번째 시계다");
            E2EHarness.Assert(!화면에떴다,
                              "원정 한 번에 게이지가 화면에 나타났다. 평소에는 보이지 않아야 한다");

            // 규칙이 예측한 값 근처인가. 세계와 규칙이 갈라지면 여기서 걸린다.
            float 예측 = Sustenance.LossOver(SustenanceKind.Hydration, _원정초);
            E2EHarness.Assert(Mathf.Abs(_원정수분 - 예측) <= 0.5f,
                              $"흐른 시간만큼 줄었다 (실측 {_원정수분:F2}, 규칙 {예측:F2})");
        }

        static float _시작시각;
        static bool _경고가떴다, _화면에떴다, _물에닿았다;

        /// <summary>
        /// <b>매 프레임 불린다.</b> <see cref="E2EHarness.WalkTo"/>의 「도착했는가」
        /// 자리에 끼워 넣은 것이라, 걷는 동안에도 한 프레임도 빼놓지 않고 본다.
        /// 왕복이 끝날 때만 보면 경고가 떴다 사라진 것을 놓친다.
        /// </summary>
        static bool 표본을_찍는다()
        {
            if (SustenanceService.AnyWarning) _경고가떴다 = true;
            if (SustenanceView.Instance != null && SustenanceView.Instance.AnyVisible) _화면에떴다 = true;
            if (SustenanceService.AtLiquid) _물에닿았다 = true;

            return _물에닿았다 || Time.time - _시작시각 >= Sustenance.ExpeditionSeconds;
        }

        // ── ② 바다 ──────────────────────────────────────────────

        /// <summary>
        /// <b>매크로늄은 목을 축여 주지 않는다.</b> 70%가 물이지만 음용은 불가능하고
        /// (세계관 §3), 그래서 바다 한복판에서는 목이 마른 채로 있어야 한다.
        /// 이 0이 「거점으로 돌아갈 이유」의 절반이다.
        /// </summary>
        static IEnumerator 바다는_목을_축여_주지_않는다()
        {
            E2EHarness.Log("[ 매크로늄 바다에 잠긴다 ]");

            Vitals.Revive();
            가득_채운다();
            Vitals.Hydration.Modify(-40f);   // 채워질 자리를 만들어 둔다
            Vitals.Food.Modify(-40f);

            E2EHarness.Teleport(_깊은곳 + Vector3.up * 0.5f);
            yield return null;

            float 들어감 = 0f;
            while (들어감 < 6f && !SustenanceService.AtLiquid)
            {
                들어감 += Time.deltaTime;
                yield return null;
            }
            E2EHarness.Assert(SustenanceService.AtLiquid, "바다에 몸이 닿았다");
            E2EHarness.AssertEqual(SustenanceService.LastLiquid, LiquidKind.Macronium,
                                   "닿은 것은 매크로늄이다");
            E2EHarness.Assert(!SustenanceService.Refilling,
                              "매크로늄 앞에서는 채워지지 않는다 — 정제 수단이 없다");

            float 전수분 = Vitals.Hydration.Current;
            float 전식량 = Vitals.Food.Current;

            float t = 0f;
            while (t < 4f) { t += Time.deltaTime; yield return null; }

            _바다에서_늘어난_수분 = Vitals.Hydration.Current - 전수분;
            float 식량변화 = Vitals.Food.Current - 전식량;

            E2EHarness.Log($"    {t:F1}초 잠긴 채 - 수분 {전수분:F1} → {Vitals.Hydration.Current:F1}, " +
                           $"식량 {전식량:F1} → {Vitals.Food.Current:F1}");

            E2EHarness.Assert(_바다에서_늘어난_수분 < 0f,
                              $"바다에서는 수분이 늘지 않는다 (실측 {_바다에서_늘어난_수분:+0.00;-0.00})");
            E2EHarness.Assert(식량변화 < 0f,
                              "바다에서는 먹을 것도 나오지 않는다 — 생물이 사는 자리는 호수뿐이다");

            Vitals.Revive();
            yield return null;
        }

        // ── ③ 호수 ──────────────────────────────────────────────

        static IEnumerator 호수에서_채운다()
        {
            E2EHarness.Log("[ 같은 자리에 호수를 채우고 물가에 선다 ]");

            채운다_임시호수();
            yield return null;

            E2EHarness.Assert(WaterBody.TryGetAt(_깊은곳, out _, out var 종류),
                              "물길 한가운데에 액체가 있다");
            E2EHarness.AssertEqual(종류, LiquidKind.Water, "이제 그 자리의 액체는 진짜 물이다");

            // <b>먼저 뭍으로 올라선다.</b> 물속에 선 채로 게이지를 내리면 그 자리에서
            // 도로 차오르므로, 「낮아지면 뜬다」를 재는 동안 값이 위아래로 움직인다.
            // 낮아진 채로 물가에 도착하는 것이 실제 순서이기도 하다.
            Vitals.Revive();
            E2EHarness.Teleport(_물가 + Vector3.up * 1.2f);
            yield return null;

            // 경고선 아래로 내려 둔다. 벌(0.10)보다는 위라 걸음은 아직 온전하다.
            비운다(0.15f);
            yield return null;

            E2EHarness.Assert(!SustenanceService.AtLiquid,
                              "뭍에 서 있다 — 여기서는 채워지지 않으므로 값이 흔들리지 않는다");

            E2EHarness.Assert(SustenanceService.HydrationWarning && SustenanceService.FoodWarning,
                              "낮아지면 경고가 선다");

            float 뜨는데 = 0f;
            while (뜨는데 < 2f && !SustenanceView.Instance.AnyVisible)
            {
                뜨는데 += Time.deltaTime;
                yield return null;
            }
            E2EHarness.Assert(SustenanceView.Instance.AnyVisible,
                              $"낮아지면 화면에 나타난다 ({뜨는데:F2}초 만에)");

            float 전체력 = Vitals.Health.Current;
            E2EHarness.Teleport(_깊은곳 + Vector3.up * 0.5f);
            yield return null;

            float t = 0f;
            while (t < 25f &&
                   !(Vitals.Hydration.Normalized > 0.99f && Vitals.Food.Normalized > 0.99f))
            {
                t += Time.deltaTime;
                yield return null;
            }
            _채우는데_걸린초 = t;

            E2EHarness.Log($"    {t:F1}초 만에 수분 {Vitals.Hydration.Normalized:P0}, " +
                           $"식량 {Vitals.Food.Normalized:P0}, 체력 {전체력:F1} → {Vitals.Health.Current:F1}");

            E2EHarness.AssertEqual(SustenanceService.LastLiquid, LiquidKind.Water, "선 곳은 호수다");
            E2EHarness.Assert(SustenanceService.Refilling, "호수 앞에서는 채워진다");
            E2EHarness.Assert(Vitals.Hydration.Normalized > 0.95f,
                              $"호수가 수분을 채운다 ({Vitals.Hydration.Normalized:P0})");
            E2EHarness.Assert(Vitals.Food.Normalized > 0.95f,
                              $"호수와 그 근방이 먹을 것을 준다 ({Vitals.Food.Normalized:P0})");
            E2EHarness.Assert(Vitals.Health.Current >= 전체력 - 0.01f,
                              "그동안 체력은 한 점도 줄지 않았다 — 호수는 무해하다");

            E2EHarness.Assert(!SustenanceService.AnyWarning, "채우면 경고가 풀린다");

            float 사라지는데 = 0f;
            while (사라지는데 < 2f && SustenanceView.Instance.AnyVisible)
            {
                사라지는데 += Time.deltaTime;
                yield return null;
            }
            E2EHarness.Assert(!SustenanceView.Instance.AnyVisible,
                              $"채우면 화면에서 사라진다 ({사라지는데:F2}초 만에)");
        }

        // ── ④ 바닥 ──────────────────────────────────────────────

        /// <summary>
        /// <b>환경은 죽이지 않고 생물만 죽인다</b>(기획서 §5.9). 바닥을 쳐도 체력은
        /// 한 점도 줄지 않고, 대신 걸음이 느려진다.
        /// </summary>
        static IEnumerator 바닥을_쳐도_죽지_않는다()
        {
            E2EHarness.Log("[ 둘 다 바닥까지 떨어뜨린다 ]");

            Vitals.Revive();
            E2EHarness.Teleport(_물가 + Vector3.up * 1.2f);   // 물가 밖, 마른 땅
            yield return null;

            비운다(0f);
            yield return null;

            E2EHarness.Assert(!SustenanceService.AtLiquid, "마른 땅에 서 있다");
            E2EHarness.Assert(Vitals.Hydration.IsEmpty && Vitals.Food.IsEmpty, "둘 다 바닥이다");

            float 전체력 = Vitals.Health.Current;
            float t = 0f;
            bool 한번이라도_죽었다 = false;

            // 단언을 프레임마다 적지 않는다 - 300줄이 「OK」로 채워지면 로그가
            // 아무것도 알려 주지 않게 된다. 보기는 프레임마다 보고 말은 한 번만 한다.
            while (t < 5f)
            {
                t += Time.deltaTime;
                if (Vitals.Health.IsEmpty) 한번이라도_죽었다 = true;
                yield return null;
            }
            E2EHarness.Assert(!한번이라도_죽었다, "바닥을 친 채 5초를 버텨도 죽지 않는다");

            _바닥에서_잃은_체력 = 전체력 - Vitals.Health.Current;
            var loco = E2EHarness.Player.Locomotion;

            E2EHarness.Log($"    {t:F1}초 바닥 - 체력 {전체력:F1} → {Vitals.Health.Current:F1}, " +
                           $"걸음 배율 {loco.ExternalSpeedFactor:F2}");

            E2EHarness.Assert(_바닥에서_잃은_체력 <= 0.01f,
                              $"굶어도 체력은 줄지 않는다 (실측 -{_바닥에서_잃은_체력:F2}). " +
                              "환경이 사람을 죽이기 시작하면 위협 계층이 무너진다");
            E2EHarness.Assert(Mathf.Abs(loco.ExternalSpeedFactor - Sustenance.SlowestMoveFactor) < 0.01f,
                              $"대신 느려진다 (배율 {loco.ExternalSpeedFactor:F2})");
            E2EHarness.Assert(loco.ExternalSpeedFactor > 0f, "느려지는 것이지 갇히는 것이 아니다");

            가득_채운다();
            yield return null;
            E2EHarness.Assert(Mathf.Abs(E2EHarness.Player.Locomotion.ExternalSpeedFactor - 1f) < 0.01f,
                              "채우면 걸음이 돌아온다");
        }

        // ── 임시 호수 ───────────────────────────────────────────

        static void 채운다_임시호수()
        {
            var 한가운데 = (_물가 + _깊은곳) * 0.5f;
            float 폭 = Vector3.Distance(new Vector3(_물가.x, 0f, _물가.z),
                                       new Vector3(_깊은곳.x, 0f, _깊은곳.z)) + 60f;

            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = 임시호수이름;
            go.SetActive(false);
            Object.Destroy(go.GetComponent<Collider>());

            var rend = go.GetComponent<Renderer>();
            if (rend != null) rend.enabled = false;

            go.transform.position = new Vector3(한가운데.x, _바다수면 + 호수를_올리는_높이, 한가운데.z);
            go.transform.localScale = new Vector3(폭 / 10f, 1f, 폭 / 10f);

            var lake = go.AddComponent<WaterBody>();
            lake.SetKind(LiquidKind.Water);
            go.SetActive(true);
        }

        static void 치운다()
        {
            foreach (var w in Object.FindObjectsByType<WaterBody>(FindObjectsInactive.Include))
                if (w != null && w.name == 임시호수이름) Object.DestroyImmediate(w.gameObject);
        }

        // ── 게이지 손잡이 ───────────────────────────────────────

        static void 가득_채운다()
        {
            Vitals.Hydration.Modify(Vitals.Hydration.Max);
            Vitals.Food.Modify(Vitals.Food.Max);
        }

        static void 비운다(float 남길비율)
        {
            Vitals.Hydration.Modify(-Vitals.Hydration.Max);
            Vitals.Food.Modify(-Vitals.Food.Max);
            if (남길비율 <= 0f) return;

            Vitals.Hydration.Modify(Vitals.Hydration.Max * 남길비율);
            Vitals.Food.Modify(Vitals.Food.Max * 남길비율);
        }

        // ── 결산 ────────────────────────────────────────────────

        static void 결산()
        {
            E2EHarness.Log("=== 수분과 식량 ===");
            E2EHarness.Log($"  원정 {_원정초:F1}초 - 수분 -{_원정수분:F1}%, 식량 -{_원정식량:F1}%");
            E2EHarness.Log($"  규칙: 정찰(11.2초) 수분 {Sustenance.PercentPerScout(SustenanceKind.Hydration):F2}% · " +
                           $"식량 {Sustenance.PercentPerScout(SustenanceKind.Food):F2}%");
            E2EHarness.Log($"  규칙: 본 원정(62초) 수분 {Sustenance.PercentPerExpedition(SustenanceKind.Hydration):F1}% · " +
                           $"식량 {Sustenance.PercentPerExpedition(SustenanceKind.Food):F1}%");
            E2EHarness.Log($"  규칙: 하루(1200초) 수분 {Sustenance.PercentPerDay(SustenanceKind.Hydration):F0}% · " +
                           $"식량 {Sustenance.PercentPerDay(SustenanceKind.Food):F0}%");
            E2EHarness.Log($"  규칙: 경고까지 원정 " +
                           $"{Sustenance.ExpeditionsUntilWarning(SustenanceKind.Hydration, Sustenance.TypicalBaseCampSeconds):F1}번" +
                           $"(거점 체류 포함) / " +
                           $"{Sustenance.ExpeditionsUntilWarning(SustenanceKind.Hydration, 0f):F1}번(쉬지 않을 때)");
            E2EHarness.Log($"  바다에서 늘어난 수분 {_바다에서_늘어난_수분:+0.00;-0.00} · " +
                           $"호수에서 가득까지 {_채우는데_걸린초:F1}초 · " +
                           $"바닥에서 잃은 체력 {_바닥에서_잃은_체력:F2}");

            // 이 한 줄이 이 라운드의 알맹이다. 원정을 돌아도 뜨지 않고, 물가에 서면 찬다.
            // 「가득」을 IsFull로 묻지 않는다 - 채우고 나서 한 프레임만 지나도
            // 감소가 한 톨 먹으므로 언제나 거짓이다. 물어야 하는 것은 「채워졌는가」다.
            E2EHarness.Assert(_원정수분 > 0f && _바다에서_늘어난_수분 < 0f &&
                              Vitals.Hydration.Normalized > 0.95f &&
                              _바닥에서_잃은_체력 <= 0.01f,
                              "원정에는 줄고, 바다는 안 채우고, 호수는 채우고, 바닥은 죽이지 않는다");
        }
    }
}
