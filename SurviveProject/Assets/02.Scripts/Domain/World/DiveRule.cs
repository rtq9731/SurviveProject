using System.Collections.Generic;
using UnityEngine;

namespace Survive.World
{
    /// <summary>잠수 구간 앞에서 무슨 일이 벌어지는가.</summary>
    public enum DiveOutcome
    {
        /// <summary>아무 일도 없다 — 잠수 구간이 아니거나 아직 들어가지 않았다.</summary>
        None,

        /// <summary>
        /// 방호복이 없다. <b>죽지 않고 막힌다.</b> 위협 계층 원칙 —
        /// 환경은 죽이지 않고 생물만 죽인다.
        /// </summary>
        NoSuit,

        /// <summary>
        /// 방호복은 걸쳤는데 이 통로를 지날 만큼 숨이 이어지지 않는다. 역시 막힌다.
        ///
        /// <see cref="NoSuit"/>와 나누는 이유는 <b>플레이어에게 다른 말</b>이기
        /// 때문이다 — 하나는 "만들어 와라", 하나는 "이 통로는 아직 아니다".
        /// <see cref="EnvironmentThreat"/>가 <see cref="PassageResult.MissingGear"/>와
        /// <see cref="PassageResult.NotEnough"/>를 가르는 것과 같은 이유이고, 같은 판정에서 나온다.
        /// </summary>
        NotEnoughAir,

        /// <summary>방호복이 몸을 봉한다. 들어간다. 이때부터 산소가 닳는다.</summary>
        Sealed,
    }

    /// <summary>
    /// 잠수의 규칙. <b>잠수 수치를 돌리는 자리는 여기 하나다.</b>
    ///
    /// <b>산소는 가짜 압박이다</b>(기획서 갱신점 _3 §2). 통로 안 사망은 사실상
    /// 일어나지 않게 잡는다 — 위협 계층 원칙에 따라 <b>환경은 죽이지 않고 생물만
    /// 죽인다</b>. 그래서 방호복 없이 잠수 구간에 들어가려 하면 익사시키지 않고
    /// <b>막는다</b>(<see cref="DiveOutcome.NoSuit"/>).
    ///
    /// 그러면 압박은 무엇이 지는가 — <b>연출</b>이다. 게이지가 줄어드는 속도,
    /// 화면 가장자리가 좁아지는 정도, 심박이 빨라지는 간격. 셋 다 수치가 아니라
    /// <b>같은 시계</b>(남은 산소)를 서로 다른 감각으로 보여 주는 것이고, 그 시계의
    /// 손잡이가 아래에 있다. 소리는 이 저장소에 클립이 하나도 없으므로
    /// (<c>AudioCueSO</c> 참고) 자리만 잡아 두었다 —
    /// <c>AudioCueBookSO.diveHeartbeat</c>·<c>diveBreath</c>.
    ///
    /// <b>첫 잠수만 아슬아슬하게.</b> 도착했을 때 게이지가 바닥 근처가 되도록
    /// 통로 길이를 맞춘다. 그 "맞춘다"를 사람의 눈대중에 맡기지 않으려고
    /// <see cref="FirstDivePassageMeters"/>가 <b>길이를 역산</b>한다 —
    /// 씬에 놓는 사람은 그 값을 그대로 자로 쓰면 된다.
    ///
    /// <b>수치는 아직 확정이 아니다.</b> 랜턴 반경·초당 소모·깊이별 산출이라는
    /// 튜닝 3값이 정해져야 여기도 따라 움직인다(실행 스펙 §16). 그래서 아래
    /// <b>손잡이 여섯</b>만 돌리면 전부 따라오도록 짜 두었다. 나머지는 전부
    /// 파생값이고, <c>SubmergedOxygenDrain</c>은 직렬화 필드를 갖지 않고 여기를
    /// 그대로 읽는다 — 프리팹에 사본이 있으면 상수를 돌려도 게임이 안 바뀐다
    /// (<see cref="LanternRule"/>·<c>CampfireFuelRule</c>에서 실제로 겪은 일이다).
    /// </summary>
    public static class DiveRule
    {
        // ══ 튜닝 손잡이 여섯 ════════════════════════════════════
        // 잠수 압박을 돌릴 때 손대는 곳은 이 여섯뿐이다.

        /// <summary>
        /// 산소 게이지의 최대치. <b>축의 단위</b>다.
        ///
        /// <c>Assets/08.Data/Vitals/Oxygen.asset</c>의 <c>maxValue</c>와 같아야 한다 —
        /// 어긋나면 여기서 역산한 통로 길이가 게임 안의 게이지와 다른 말을 하게 된다.
        /// 그 일치는 <c>DiveRuleTests</c>가 에셋을 열어 지킨다.
        /// </summary>
        public const float OxygenMax = 100f;

        /// <summary>
        /// 방호복을 걸치고 잠겼을 때의 초당 소모. <b>잠수 시간의 환율이다.</b>
        ///
        /// 이것 하나가 통로 길이를 정한다 — 작을수록 긴 통로를 낼 수 있고,
        /// 크면 첫 잠수가 짧고 답답해진다. 죽이지 않는 것이 전제이므로
        /// "빠듯하게"의 폭은 <see cref="FirstDiveArrivalOxygenRatio"/>가 따로 잡는다.
        /// </summary>
        public const float SuitDrainPerSecond = 2.5f;

        /// <summary>
        /// 방호복 없이 머리까지 잠겼을 때의 초당 소모. <b>학습 장치의 값이다.</b>
        ///
        /// 예전부터 6이었고 그대로 둔다 — 강을 헤엄쳐 건너며 "숨이 시계다"를
        /// 배우는 자리라, 방호복을 넣는 것과 이 값을 흔드는 것을 한 번에 하면
        /// 숨이 가쁜 것이 무엇 탓인지 가릴 수 없다(화톳불 45초·랜턴 1.6과 같은 이유).
        ///
        /// 방호복의 세 배가 넘는다는 사실 자체가 장비의 값어치다.
        /// </summary>
        public const float BareDrainPerSecond = 6f;

        /// <summary>물 밖에서 초당 회복량. 뭍에 올라오면 금방 채워진다.</summary>
        public const float RefillPerSecond = 20f;

        /// <summary>
        /// 헤엄치는 속도(m/s). 길이를 역산하는 자다.
        ///
        /// <c>PlayerLocomotion.swimSpeed</c>와 같아야 한다 — 다르면 여기서 낸 길이가
        /// 실제로 지나는 데 걸리는 시간과 어긋난다. 그 일치는 <c>DiveRuleTests</c>가
        /// 플레이어 프리팹을 열어 지킨다.
        ///
        /// <b>스프린트는 세지 않는다.</b> "정상 속도로 지나면 아슬아슬하다"가
        /// 규격이고, 전력으로 헤엄치는 것은 플레이어가 스스로 사는 여유다.
        /// </summary>
        public const float SwimSpeedMetersPerSecond = 3.2f;

        /// <summary>
        /// 첫 잠수 통로에 도착했을 때 남기려는 산소 비율. <b>"아슬아슬함"의 값이다.</b>
        ///
        /// 규격은 <c>0 &lt; 잔량 &lt; 20%</c>이고(실행 스펙 §8-1), 그 창의 한가운데를
        /// 고른다. 0에 붙이면 프레임 하나 늦게 도착한 사람이 죽고, 그러면
        /// "환경은 죽이지 않는다"가 거짓이 된다. 20%에 붙이면 여유가 눈에 보여서
        /// 압박이 서지 않는다.
        /// </summary>
        public const float FirstDiveArrivalOxygenRatio = 0.10f;

        // ══ 규격 ════════════════════════════════════════════════

        /// <summary>
        /// 첫 잠수 도착 잔량의 상한. 이보다 많이 남으면 아슬아슬하지 않다.
        /// 실행 스펙 §8-1의 실측 기준이 그대로 상수가 된 것이다.
        /// </summary>
        public const float ArrivalRatioCeiling = 0.20f;

        // ══ 파생값 ══════════════════════════════════════════════

        /// <summary>방호복 한 벌이 물속에서 버티는 시간(초). 장비 에셋의 용량이 이 값이다.</summary>
        public static float SuitAirSeconds => OxygenMax / SuitDrainPerSecond;

        /// <summary>맨몸으로 머리를 담근 채 버티는 시간(초). 견줄 값으로만 쓴다.</summary>
        public static float BareAirSeconds => OxygenMax / BareDrainPerSecond;

        /// <summary>첫 잠수 통로를 정상 속도로 지나는 데 걸리는 시간(초).</summary>
        public static float FirstDiveSeconds => SuitAirSeconds * (1f - FirstDiveArrivalOxygenRatio);

        /// <summary>
        /// <b>첫 잠수 통로의 길이(m) — 편도.</b> 씬에 놓는 사람이 자로 쓸 값이다.
        ///
        /// 편도인 이유: 통로 저쪽은 지하 공동이고 그곳은 테라포밍이 끝나 숨을
        /// 쉴 수 있다. 나와서 게이지가 다시 차므로 돌아오는 길은 <b>새 잠수</b>다.
        /// 한 숨에 왕복해야 한다면 이 값의 절반이 자가 된다.
        /// </summary>
        public static float FirstDivePassageMeters => PassageMetersFor(FirstDiveSeconds);

        /// <summary>규격을 지키는 통로 길이의 아래 끝(m). 이보다 짧으면 20% 넘게 남는다.</summary>
        public static float ShortestTunedPassageMeters =>
            PassageMetersFor(SuitAirSeconds * (1f - ArrivalRatioCeiling));

        /// <summary>규격을 지키는 통로 길이의 위 끝(m). 이 값에서 잔량이 정확히 0이 된다.</summary>
        public static float LongestSurvivablePassageMeters => PassageMetersFor(SuitAirSeconds);

        // ══ 계산 — 숨 ═══════════════════════════════════════════

        /// <summary>지금 산소가 어떻게 움직이는가. 양수는 회복, 음수는 소모.</summary>
        public static float OxygenDeltaPerSecond(bool submerged, bool suited)
        {
            if (!submerged) return RefillPerSecond;
            return suited ? -SuitDrainPerSecond : -BareDrainPerSecond;
        }

        /// <summary>이 산소로 잠긴 채 버티는 시간(초).</summary>
        public static float SecondsOfAir(float oxygen, bool suited)
        {
            float drain = suited ? SuitDrainPerSecond : BareDrainPerSecond;
            if (drain <= 0f) return float.PositiveInfinity;
            return Mathf.Max(0f, oxygen) / drain;
        }

        /// <summary>가득 찬 산소로 이만큼 걸리는 통로를 지났을 때 남는 산소.</summary>
        public static float ArrivalOxygen(float passageSeconds) =>
            Mathf.Max(0f, OxygenMax - SuitDrainPerSecond * Mathf.Max(0f, passageSeconds));

        /// <summary>같은 것을 비율로. 0이면 도착과 동시에 숨이 다한 것이다.</summary>
        public static float ArrivalRatio(float passageSeconds) => ArrivalOxygen(passageSeconds) / OxygenMax;

        /// <summary>
        /// 이 통로가 <b>첫 잠수의 규격</b>을 지키는가 — 도착 잔량이 0보다 크되 20% 아래.
        /// 실측 보고가 읽는 창구가 이것이다.
        /// </summary>
        public static bool IsFirstDiveTuned(float passageSeconds)
        {
            float ratio = ArrivalRatio(passageSeconds);
            return ratio > 0f && ratio < ArrivalRatioCeiling;
        }

        // ══ 계산 — 자 ═══════════════════════════════════════════

        /// <summary>이 길이의 통로를 정상 속도로 지나는 데 걸리는 시간(초).</summary>
        public static float PassageSecondsFor(float meters) =>
            Mathf.Max(0f, meters) / SwimSpeedMetersPerSecond;

        /// <summary>이 시간만큼 걸리는 통로의 길이(m).</summary>
        public static float PassageMetersFor(float seconds) =>
            Mathf.Max(0f, seconds) * SwimSpeedMetersPerSecond;

        // ══ 판정 ════════════════════════════════════════════════

        /// <summary>
        /// 방호복을 걸치고 있는가. <b>보유가 곧 장착이다</b> —
        /// <see cref="TraversalLoadout"/>이 잡은 규칙을 그대로 따른다.
        /// 용량은 보지 않는다. 얼마나 긴 통로를 지나는가는 통로가 묻는다.
        /// </summary>
        public static bool HasSuit(IReadOnlyList<GearCapability> loadout)
        {
            if (loadout == null) return false;
            for (int i = 0; i < loadout.Count; i++)
                if (loadout[i].Gear == TraversalGear.MacroniumSuit) return true;
            return false;
        }

        /// <summary>
        /// 잠수 구간에 들어가려 할 때 무슨 일이 벌어지는가.
        ///
        /// <b>판정을 새로 만들지 않는다.</b> "이 통로를 지날 수 있는가"는 이미
        /// <see cref="EnvironmentThreat.Evaluate"/>가 답하는 물음이고, 여기서는 그 답을
        /// 잠수의 말로 옮길 뿐이다 — <see cref="MacroniumContact"/>가 액면에서 한 것과
        /// 같은 자세다. 잠수 전용 규칙을 따로 두면 관문은 열리는데 들어가면 막히는
        /// (또는 그 반대) 상태가 언제든 생긴다.
        ///
        /// <b>결과에 죽음이 없다.</b> 액면과 갈리는 자리가 여기다. 액면은 밟으면
        /// 죽이고 잠수는 밀어낸다 — 액면은 물질이 몸을 녹이는 것이고 잠수는 그냥
        /// 물이기 때문이다. 위협 계층 원칙이 코드에서 보이는 자리이기도 하다.
        /// </summary>
        public static DiveOutcome Resolve(bool entering, HazardZone zone,
                                          IReadOnlyList<GearCapability> loadout)
        {
            if (!entering) return DiveOutcome.None;

            // 다른 위협이 걸린 구역은 잠수로 묻지 않는다. 어둠도 액면도 층도
            // "잠겨서 지나는" 종류가 아니다 — 이 규칙은 잠수 하나에만 붙는다.
            if (zone.Hazard != EnvironmentHazard.Submersion) return DiveOutcome.None;

            var check = EnvironmentThreat.Evaluate(zone, loadout);
            if (check.CanPass) return DiveOutcome.Sealed;

            return check.Result == PassageResult.MissingGear
                ? DiveOutcome.NoSuit
                : DiveOutcome.NotEnoughAir;
        }

        /// <summary>들어갈 수 있는가. 가부만 필요할 때.</summary>
        public static bool CanEnter(HazardZone zone, IReadOnlyList<GearCapability> loadout) =>
            Resolve(true, zone, loadout) == DiveOutcome.Sealed;

        // ══ 연출 — 압박은 수치가 아니라 이쪽이 진다 ══════════════

        /// <summary>
        /// 화면 가장자리가 조여들기 시작하는 산소 비율.
        ///
        /// 잠기자마자 조이면 <b>가득 찬 게이지가 위험 신호를 낸다</b> — 그러면
        /// 신호가 상태를 말하지 못하고 그냥 "물속"이라는 배경이 된다.
        /// 절반쯤부터 서서히 시작해야 조여드는 것 자체가 정보가 된다.
        /// </summary>
        public const float EdgeOnsetRatio = 0.55f;

        /// <summary>가장 조였을 때의 어둠(0~1). 1이면 화면이 닫힌다.</summary>
        public const float EdgeMaxDarkness = 0.72f;

        /// <summary>가장 여유로울 때의 심박 간격(초).</summary>
        public const float CalmBeatSeconds = 1.15f;

        /// <summary>숨이 다해 갈 때의 심박 간격(초).</summary>
        public const float PanicBeatSeconds = 0.42f;

        /// <summary>
        /// 지금 화면 가장자리가 얼마나 조여야 하는가(0~1). 남은 산소 비율만 본다.
        ///
        /// <b>어둠을 지킨다.</b> 이것이 내는 것은 검은 테두리뿐이고 세계에 빛을
        /// 더하지 않는다. 밝아지는 연출이었다면 잠수하는 동안 어둠이 걷혀
        /// 설계가 통째로 무너졌을 것이다.
        /// </summary>
        public static float EdgeDarkness(float oxygenNormalized)
        {
            float o = Mathf.Clamp01(oxygenNormalized);
            if (o >= EdgeOnsetRatio) return 0f;

            // 남은 산소가 줄수록 자란다. 제곱을 씌워 끝에서 급해지게 한다 —
            // 선형이면 절반 지점에서 이미 눈에 띄어 "아직 괜찮다"가 안 읽힌다.
            float t = 1f - o / EdgeOnsetRatio;
            return EdgeMaxDarkness * t * t;
        }

        /// <summary>지금 심박 사이의 간격(초). 소리가 들어오면 이 간격으로 뛴다.</summary>
        public static float BeatSeconds(float oxygenNormalized) =>
            Mathf.Lerp(PanicBeatSeconds, CalmBeatSeconds, Mathf.Clamp01(oxygenNormalized));
    }
}
