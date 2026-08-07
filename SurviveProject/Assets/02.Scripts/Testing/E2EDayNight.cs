using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// <b>하루가 실제로 화면에서 밝아지고 어두워지는가</b>를 픽셀로 재는 시나리오.
    ///
    /// <b>왜 EditMode로는 부족한가.</b> <see cref="DayNightCycle"/>의 곡선이 옳다는
    /// 것과 화면이 실제로 그만큼 밝아진다는 것은 다른 말이다. 그 사이에는 태양을
    /// 붙잡는 일, 환경광을 넣는 일, 그리고 후처리가 있고, 셋 중 하나만 어긋나도
    /// 곡선은 완벽한데 화면은 종일 캄캄하다. 그래서 <b>같은 자리 같은 카메라</b>에서
    /// 시각만 바꿔 가며 잰다.
    ///
    /// <b>여기서 답하는 물음 넷.</b>
    /// <list type="number">
    /// <item>낮이 해질녘보다 밝고 해질녘이 밤보다 밝은가 — <b>화면에서</b></item>
    /// <item>낮이 랜턴을 무의미하게 만들 만큼 밝지는 않은가</item>
    /// <item>밤에 랜턴을 켜면 실제로 밝아지는가</item>
    /// <item><b>밤에도 화톳불은 밝은 구역인가</b> — 이것이 이 라운드의 회귀선이다</item>
    /// </list>
    /// </summary>
    public static class E2EDayNight
    {
        /// <summary>사람이 읽을 실측표. 시나리오가 끝나면 여기 앉는다.</summary>
        public static string LastReport { get; private set; } = "";

        static Vector3 _자리;
        static Vector3 _겨냥;

        public static IEnumerator Run()
        {
            var 시계 = DayNightService.Instance;
            E2EHarness.Assert(시계 != null, "밤낮 시계가 스스로 섰다");

            시계.Frozen = true;
            자리를_잡는다();
            yield return null;

            var 표 = new List<string> { "국면\t시각\t평균 휘도\t암부 비율\t태양 세기" };

            yield return 랜턴을_끈다();

            float 낮 = 0f, 해질녘 = 0f, 밤 = 0f, 랜턴켠밤 = 0f;

            yield return 재고_적는다(시계, "낮", 0.5f, 표, v => 낮 = v);
            yield return 재고_적는다(시계, "해질녘",
                (DayNightCycle.DuskStart + DayNightCycle.DuskEnd) * 0.5f, 표, v => 해질녘 = v);
            yield return 재고_적는다(시계, "밤", 0f, 표, v => 밤 = v);

            yield return 랜턴을_켠다();
            yield return 재고_적는다(시계, "밤+랜턴", 0f, 표, v => 랜턴켠밤 = v);
            yield return 랜턴을_끈다();

            E2EHarness.Log("[밤낮 실측]\n  " + string.Join("\n  ", 표));

            // ── 1. 순서가 화면에서도 지켜진다 ────────────────────
            E2EHarness.Assert(낮 > 해질녘,
                              $"낮이 해질녘보다 밝다 (낮 {낮:F5} > 해질녘 {해질녘:F5})");
            E2EHarness.Assert(해질녘 > 밤,
                              $"해질녘이 밤보다 밝다 (해질녘 {해질녘:F5} > 밤 {밤:F5})");

            // 순서만 맞고 차이가 없으면 하루가 도는 것이 화면에 없는 것과 같다.
            E2EHarness.Assert(낮 > 밤 * 1.5f,
                              $"낮과 밤의 차이가 눈에 띈다 (낮 {낮:F5} vs 밤 {밤:F5}, " +
                              $"{(밤 > 0f ? 낮 / 밤 : 0f):F2}배)");

            // ── 2. 낮에도 랜턴이 쓸모 있다 ───────────────────────
            E2EHarness.Assert(낮 < 0.35f,
                              $"한낮이 형광등 정도로 어둑하다 (평균 휘도 {낮:F5}) — " +
                              "이 선이 무너지면 랜턴이 밤 전용 물건이 된다");

            // ── 3. 밤에 랜턴이 실제로 일한다 ─────────────────────
            E2EHarness.Assert(랜턴켠밤 > 밤,
                              $"밤에 랜턴을 켜면 밝아진다 (켬 {랜턴켠밤:F5} > 끔 {밤:F5})");

            // ── 4. 하루를 한 바퀴 ────────────────────────────────
            yield return 한_바퀴_돈다(시계);

            // ── 5. 회귀선: 밤에도 화톳불은 밝은 구역이다 ─────────
            yield return 밤에도_밝은_구역은_밝다(시계);

            시계.Frozen = false;
            시계.SetTimeOfDay(DayNightService.StartTimeOfDay);

            LastReport = string.Join("\n", 표);
        }

        // ── 자리 ────────────────────────────────────────────────

        /// <summary>
        /// 한 번 고른 자리와 겨냥을 끝까지 쓴다. 잴 때마다 다시 고르면
        /// 넉 장이 서로 다른 구도의 값이 되어 비교가 성립하지 않는다.
        /// </summary>
        static void 자리를_잡는다()
        {
            var 사람 = E2EHarness.Player.transform;
            _자리 = 사람.position;

            // 지평선을 향해 조금 아래를 본다. 하늘만 담으면 지형이 밝아지는 것을
            // 못 보고, 발밑만 담으면 하늘이 밝아지는 것을 못 본다.
            _겨냥 = _자리 + 사람.forward * 20f + Vector3.down * 2f;
        }

        static void 자리로_돌아간다()
        {
            E2EHarness.Teleport(_자리);
            E2EHarness.LookAt(_겨냥);
        }

        // ── 재기 ────────────────────────────────────────────────

        static IEnumerator 재고_적는다(DayNightService 시계, string 이름, float 시각,
                                       List<string> 표, System.Action<float> 받는다)
        {
            시계.SetTimeOfDay(시각);

            // 자리를 다시 눌러 두고 몇 프레임 그린다. 후처리의 블렌딩이
            // 도달하기 전에 읽으면 앞 시각의 잔상을 재게 된다.
            for (int i = 0; i < 8; i++) { 자리로_돌아간다(); yield return null; }

            string 표본 = PostFxProbe.Sample();
            float 평균 = 값을_뽑는다(표본, "mean");
            float 암부 = 값을_뽑는다(표본, "darkRatio");
            float 태양 = 시계.Sun != null && 시계.Sun.isActiveAndEnabled ? 시계.Sun.intensity : 0f;

            표.Add($"{이름}\t{시각:F3}\t{평균:F5}\t{암부:F3}\t{태양:F3}");
            받는다(평균);
        }

        /// <summary>
        /// 하루를 스무 걸음으로 돌며 밝기가 <b>올랐다가 내려오는지</b> 본다.
        ///
        /// 세 점만 재면 "낮에만 밝다"를 확인할 뿐이고, 중간에 값이 튀거나
        /// 해가 두 번 뜨는 것 같은 사고는 보이지 않는다.
        /// </summary>
        static IEnumerator 한_바퀴_돈다(DayNightService 시계)
        {
            const int 걸음 = 20;
            var 값 = new float[걸음];
            var 줄 = new List<string> { "시각\t평균 휘도" };

            for (int i = 0; i < 걸음; i++)
            {
                float t = i / (float)걸음;
                시계.SetTimeOfDay(t);
                for (int f = 0; f < 3; f++) { 자리로_돌아간다(); yield return null; }

                값[i] = 값을_뽑는다(PostFxProbe.Sample(320, 180), "mean");
                줄.Add($"{t:F2}\t{값[i]:F5}");
            }

            E2EHarness.Log("[하루 한 바퀴]\n  " + string.Join("\n  ", 줄));

            int 가장밝은 = 0, 가장어두운 = 0;
            for (int i = 1; i < 걸음; i++)
            {
                if (값[i] > 값[가장밝은]) 가장밝은 = i;
                if (값[i] < 값[가장어두운]) 가장어두운 = i;
            }

            float 밝은시각 = 가장밝은 / (float)걸음;
            float 어두운시각 = 가장어두운 / (float)걸음;

            E2EHarness.Assert(DayNightCycle.PhaseAt(밝은시각) == DayPhase.Day,
                              $"가장 밝은 순간이 낮이다 (시각 {밝은시각:F2}, " +
                              $"{DayNightCycle.PhaseAt(밝은시각)})");
            E2EHarness.Assert(DayNightCycle.PhaseAt(어두운시각) == DayPhase.Night,
                              $"가장 어두운 순간이 밤이다 (시각 {어두운시각:F2}, " +
                              $"{DayNightCycle.PhaseAt(어두운시각)})");
        }

        // ── 회귀선 ──────────────────────────────────────────────

        /// <summary>
        /// <b>밤이 온다는 것은 세계 전체가 어두워진다는 뜻이지 화톳불이 꺼진다는
        /// 뜻이 아니다.</b> 시계와 밝은 구역이 얽히면 「밤이 되면 거점이 사라진다」가
        /// 조용히 들어오고, 그것은 화면에서 버그로 보이지 않는다.
        /// </summary>
        static IEnumerator 밤에도_밝은_구역은_밝다(DayNightService 시계)
        {
            var 불 = E2ECampfire.세운다();
            E2EHarness.Assert(불 != null, "화톳불을 세웠다");

            var 불자리 = 불.LitZoneCenter;
            yield return null;

            foreach (float 시각 in new[] { 0.5f, 0f, 0.25f, 0.75f })
            {
                시계.SetTimeOfDay(시각);
                yield return null;

                E2EHarness.Assert(LitZoneRegistry.IsLit(불자리),
                                  $"시각 {시각:F2}({DayNightCycle.PhaseAt(시각)})에도 화톳불 자리가 밝다");
                E2EHarness.Assert(!LitZoneRegistry.IsBlindSide(불자리),
                                  $"시각 {시각:F2}에도 화톳불 안은 내준 쪽이 아니다");
            }

            // 랜턴도 마찬가지다 — 밤이라고 반경이 줄어들지 않는다.
            yield return 랜턴을_켠다();
            var 램프 = E2EHarness.Lantern;
            if (램프 != null)
            {
                foreach (float 시각 in new[] { 0.5f, 0f })
                {
                    시계.SetTimeOfDay(시각);
                    yield return null;
                    E2EHarness.Assert(LitZoneRegistry.IsLit(램프.LitZoneCenter),
                                      $"시각 {시각:F2}에도 랜턴 자리가 밝다");
                }
            }
            yield return 랜턴을_끈다();

            Object.Destroy(불.gameObject);
            yield return null;
        }

        // ── 랜턴 ────────────────────────────────────────────────

        static IEnumerator 랜턴을_켠다()
        {
            E2ECampfire.랜턴을_준다();
            var 램프 = E2EHarness.Lantern;
            if (램프 != null)
            {
                램프.Recharge(LanternRule.MaxBattery * 10f);
                램프.SetSwitch(true);
            }
            yield return null;
        }

        static IEnumerator 랜턴을_끈다()
        {
            var 램프 = E2EHarness.Lantern;
            if (램프 != null) 램프.SetSwitch(false);
            yield return null;
        }

        // ── JSON 한 값 뽑기 ─────────────────────────────────────

        static float 값을_뽑는다(string json, string 열쇠)
        {
            string 무늬 = "\"" + 열쇠 + "\":";
            int i = json.IndexOf(무늬, System.StringComparison.Ordinal);
            if (i < 0) return 0f;

            i += 무늬.Length;
            int j = i;
            while (j < json.Length && (char.IsDigit(json[j]) || json[j] == '.' ||
                                       json[j] == '-' || json[j] == 'e' || json[j] == 'E')) j++;

            return float.TryParse(json.Substring(i, j - i), NumberStyles.Float,
                                  CultureInfo.InvariantCulture, out float v) ? v : 0f;
        }
    }
}
