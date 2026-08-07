using UnityEngine;
using Survive.World;

namespace Survive.Vitals
{
    /// <summary>
    /// 몸이 <b>스스로 줄어드는</b> 두 가지. 물을 마셔야 하고 무언가를 먹어야 한다.
    ///
    /// <b>왜 0에 이름을 두지 않는가.</b> <see cref="LiquidKind"/>와 같은 이유다 —
    /// 채워 넣지 않으면 0이 되고, 0에 이름이 있으면 그것이 조용한 기본값이 된다.
    /// 「무슨 게이지인지 안 적은 회복」이 수분으로 새는 것보다 <b>그 자리에서
    /// 던지는 편</b>이 낫다.
    /// </summary>
    public enum SustenanceKind
    {
        /// <summary>수분. 채우는 자리가 여럿이고 그래서 빨리 준다.</summary>
        Hydration = 1,

        /// <summary>식량. 채우는 자리가 호수 하나뿐이고 그래서 천천히 준다.</summary>
        Food = 2,
    }

    /// <summary>
    /// <b>수분과 식량은 시계가 아니라 거리다</b>(기획서 §0.2 · §5.15).
    ///
    /// <b>이 항목은 한 번 기각됐던 것이다.</b> 근거는 *"시계가 둘이면 하나는 반드시
    /// 잡무가 된다. 배터리가 이미 시계다"*였고 <b>그 근거는 지금도 옳다.</b>
    /// 되살아난 이유는 역할이 다르기 때문이다 — 무대가 지상으로 나오며 호수가
    /// 생겼고 착륙 지점이 그 근방이라, 이 둘은 <b>거점으로 돌아갈 이유</b>로 쓴다.
    /// 「돌아가는 김에 채운다」가 되어야지 「물 마시러 돌아간다」가 되면 안 된다.
    ///
    /// <b>그래서 이 파일의 숫자는 전부 「눈에 띄지 않을 만큼」을 향해 잡혀 있다.</b>
    /// 배터리는 가득 채워도 <c>62.5</c>초다(<see cref="LanternRule.MaxBattery"/> /
    /// <see cref="LanternRule.Tier1DrainPerSecond"/>) — <b>본 원정 한 번이 배터리 한
    /// 셀</b>이라는 뜻이고, 그것이 이 게임의 시계다. 수분은 그보다 <b>여덟 배</b>,
    /// 식량은 <b>열여섯 배</b> 느리게 준다. 이 배율을 좁히는 순간 시계가 둘이 되고
    /// 기각 근거가 그대로 되살아난다.
    ///
    /// <b>수분과 식량의 속도가 다른 것은 꾸밈이 아니다.</b> 채울 수 있는 자리의 수가
    /// 다르기 때문이다 — 수분은 진짜 물이 있는 곳이면 어디서나 채우고 정제 수단이
    /// 생기면 매크로늄에서도 얻는다(§5.15의 셋). 식량은 <b>호수와 그 근방에만</b>
    /// 있다(세계관 §5 — 지상에 생물이 남은 자리가 거기뿐이다). 그래서 빨리 주는
    /// 쪽에는 들판에서의 답이 있고, 천천히 주는 쪽이 실제로 사람을 집으로 당긴다.
    /// <b>관리 대상이 게이지가 아니라 거리</b>라는 말이 이 비대칭에 들어 있다.
    ///
    /// <b>바닥을 쳐도 죽지 않는다.</b> 위협 계층이 이미 그렇게 서 있다(기획서 §5.9) —
    /// <i>환경은 막거나 값을 매기고, 실제로 목숨을 앗는 것은 움직이는 것뿐이다.</i>
    /// 굶어 죽게 만들면 이 게임에서 환경이 사람을 죽이는 두 번째 자리가 생기고,
    /// 그것은 낫이 하는 일을 게이지가 대신하는 것이다. 대신 <b>느려진다</b> —
    /// 돌아가는 데 더 오래 걸리는 것이 벌이므로, 벌 자체가 이 항목의 주제와 같다.
    /// 그리고 느려진 만큼 밖에 더 오래 머무르게 되어 <b>낫에게 잡힐 확률이 오른다.</b>
    /// 환경이 값을 매기고 목숨은 생물이 가져가는 순서가 여기서도 지켜진다.
    /// </summary>
    public static class Sustenance
    {
        // ══ 이름 ════════════════════════════════════════════════

        /// <summary>에셋(<see cref="VitalDefinitionSO"/>)과 맞춰야 하는 아이디.</summary>
        public const string HydrationId = "hydration";

        /// <inheritdoc cref="HydrationId"/>
        public const string FoodId = "food";

        /// <summary>둘이 전부다. 검사가 「둘뿐」을 셀 때 쓰는 자리이기도 하다.</summary>
        public static readonly SustenanceKind[] All =
            { SustenanceKind.Hydration, SustenanceKind.Food };

        public static bool IsKnown(SustenanceKind kind) =>
            kind == SustenanceKind.Hydration || kind == SustenanceKind.Food;

        public static string IdOf(SustenanceKind kind)
        {
            switch (kind)
            {
                case SustenanceKind.Hydration: return HydrationId;
                case SustenanceKind.Food:      return FoodId;
                default: throw Unknown(kind);
            }
        }

        // ══ 튜닝 손잡이 여섯 ════════════════════════════════════
        //
        // 아래 여섯이 이 항목의 전부다. <b>최종값이 아니다</b> — 기획서 §13의
        // 「수분·식량 감소 속도」는 사람이 정할 값이고, 여기 적힌 것은 실측을
        // 붙여 올리기 위한 후보다. 다만 「두세 번 나갔다 와야 채운다」가 수로
        // 성립하는 범위는 좁혀 두었다(<see cref="ExpeditionsUntilWarning"/>).

        /// <summary>둘 다 가득이 100이다. 눈금이 곧 퍼센트라 실측을 읽기 쉽다.</summary>
        public const float MaxValue = 100f;

        /// <summary>
        /// 수분의 초당 감소. 가득에서 바닥까지 <b>500초</b>다.
        ///
        /// 배터리 한 셀(62.5초)의 <b>여덟 배</b>이고 하루(1200초)의 <b>0.42배</b>다.
        /// 올리면 게이지를 쳐다보게 되고, 그 순간 이것은 두 번째 시계가 된다.
        /// </summary>
        public const float HydrationDrainPerSecond = 0.20f;

        /// <summary>
        /// 식량의 초당 감소. 가득에서 바닥까지 <b>1000초</b> — <b>하루(1200초)에
        /// 한 번쯤</b>이다.
        ///
        /// 수분의 절반인 이유는 위(요약)에 적었다: 들판에 답이 없는 쪽이므로
        /// 자주 물으면 그것이 곧 「먹으러 돌아간다」가 된다.
        /// </summary>
        public const float FoodDrainPerSecond = 0.10f;

        /// <summary>
        /// 화면에 나타나는 선. <b>위로는 아무것도 보이지 않는다.</b>
        ///
        /// 늘 띄워 두면 그것이 곧 두 번째 시계다 — 눈에 들어오는 게이지는 쳐다보게
        /// 되고, 쳐다보는 게이지는 관리 대상이 된다. 그래서 <b>낮아질 때만</b> 나온다.
        /// </summary>
        public const float WarningThreshold = 0.30f;

        /// <summary>
        /// 느려지기 시작하는 선. 경고선(0.30)보다 <b>한참 아래</b>다.
        ///
        /// 경고와 벌 사이에 <see cref="SecondsBetween"/>(0.30 → 0.10)만큼의 여유가
        /// 있어야 「보고 나서 돌아갈 시간」이 생긴다. 예고 없이 벌어지는 일은
        /// 긴장이 아니라 억울함이 된다(기획서 §5.14의 박명과 같은 판단이다).
        /// </summary>
        public const float SlowdownThreshold = 0.10f;

        /// <summary>
        /// 바닥에서의 이동 배율. <b>0이 아니다</b> — 못 움직이게 만들면 그것은
        /// 느려지는 것이 아니라 갇히는 것이고, 갇히면 결국 죽는다.
        /// </summary>
        public const float SlowestMoveFactor = 0.70f;

        // ══ 실측한 원정 길이 ════════════════════════════════════
        //
        // 이 셋은 튜닝값이 아니라 <b>이미 재어 놓은 세계의 치수</b>다. 감소 속도를
        // 이 위에서 역산하므로 여기 적어 두고 검사가 읽게 한다.

        /// <summary>정찰 한 번(앞마당 여섯 곳 순회 왕복). 실측.</summary>
        public const float ScoutSeconds = 11.2f;

        /// <summary>본 원정 한 번(거점 → 바다 건너 → 하나 캐고 복귀). 실측.</summary>
        public const float ExpeditionSeconds = 62f;

        /// <summary>하루. <see cref="DayNightCycle.DayLengthSeconds"/>를 그대로 쓴다.</summary>
        public const float DaySeconds = DayNightCycle.DayLengthSeconds;

        /// <summary>
        /// 원정과 원정 사이 거점에 머무는 시간의 <b>기준값</b>. 캐 온 것을 넣고
        /// 짓고 만드는 데 드는 시간이다.
        ///
        /// <b>이 값은 잴 수 없다</b> — 사람마다 다르다. 그래서 상수로 못 박지 않고
        /// <see cref="ExpeditionsUntilWarning"/>의 인자로 두고, 검사는 <b>0(쉬지 않고
        /// 나간다)과 이 값(원정만큼 쉰다) 두 모형을 다 잰다.</b> 한 모형만 적으면
        /// 그 모형이 조용히 사실이 된다.
        /// </summary>
        public const float TypicalBaseCampSeconds = ExpeditionSeconds;

        // ══ 채우는 곳 ═══════════════════════════════════════════
        //
        // 수분 확보 수단 셋(기획서 §5.15): 착륙선의 처리장치 / 강물 정제 /
        // 매크로늄 표면에서 확보. <b>셋이 아니라 한 규칙의 세 값이다</b> —
        // 액체 앞에 서면 <see cref="LiquidKind"/>와 정제 수단의 유무가 답을 정한다.
        // 착륙선의 처리장치는 「마르지 않는 정제 수단」이라 세 번째 값이 아니라
        // <see cref="HydrationPerSecondFrom"/>의 두 번째 인자가 참인 자리다(스펙 §7).

        /// <summary>
        /// 진짜 물에서 채우는 속도. <b>4초면 가득</b>이다.
        ///
        /// 빠른 것이 요점이다 — 마시는 데 시간이 들면 그것이 잡무다. 물가에 서
        /// 있기만 하면 채워지므로 플레이어가 「채우기」라는 행동을 배울 필요가 없다.
        /// </summary>
        public const float LakeHydrationPerSecond = 25f;

        /// <summary>
        /// 호수에서 먹을 것을 얻는 속도. 물보다 두 배 오래 걸린다(8초면 가득) —
        /// 물은 뜨는 것이고 먹을 것은 잡거나 뜯는 것이기 때문이다.
        /// </summary>
        public const float LakeFoodPerSecond = 12.5f;

        /// <summary>
        /// 매크로늄에서 <b>정제해</b> 얻는 속도. 호수의 5분의 1이다(20초면 가득).
        ///
        /// 그동안 몸은 액체에 잠겨 있으므로 <see cref="MacroniumSea.CorrosionPerSecond"/>가
        /// 계속 청구된다 — 가득 채우면 체력 60쯤이 날아간다. <b>원정 중의 응급
        /// 수단이지 살림이 아니다</b>는 것이 이 숫자의 뜻이다.
        /// </summary>
        public const float PurifiedHydrationPerSecond = 5f;

        // ══ 감소 ════════════════════════════════════════════════

        public static float DrainPerSecond(SustenanceKind kind)
        {
            switch (kind)
            {
                case SustenanceKind.Hydration: return HydrationDrainPerSecond;
                case SustenanceKind.Food:      return FoodDrainPerSecond;
                default: throw Unknown(kind);
            }
        }

        /// <summary>이만큼 흐르면 눈금이 얼마나 빠지는가.</summary>
        public static float LossOver(SustenanceKind kind, float seconds) =>
            DrainPerSecond(kind) * Mathf.Max(0f, seconds);

        /// <summary>이만큼 흐르면 <b>몇 퍼센트</b>가 빠지는가. 실측 보고가 읽는 창구다.</summary>
        public static float PercentLostOver(SustenanceKind kind, float seconds) =>
            LossOver(kind, seconds) / MaxValue * 100f;

        /// <summary>정찰 한 번에 몇 퍼센트.</summary>
        public static float PercentPerScout(SustenanceKind kind) =>
            PercentLostOver(kind, ScoutSeconds);

        /// <summary>본 원정 한 번에 몇 퍼센트. <b>이 값이 이 항목의 게이트다.</b></summary>
        public static float PercentPerExpedition(SustenanceKind kind) =>
            PercentLostOver(kind, ExpeditionSeconds);

        /// <summary>하루에 몇 퍼센트. 100을 넘으면 하루에 한 번 넘게 채운다는 뜻이다.</summary>
        public static float PercentPerDay(SustenanceKind kind) =>
            PercentLostOver(kind, DaySeconds);

        /// <summary>가득에서 바닥까지 몇 초.</summary>
        public static float SecondsFromFullToEmpty(SustenanceKind kind) =>
            MaxValue / DrainPerSecond(kind);

        /// <summary>이 비율에서 저 비율까지 몇 초. 되감아도(위로 가도) 0을 준다.</summary>
        public static float SecondsBetween(SustenanceKind kind, float from, float to)
        {
            float drop = (Mathf.Clamp01(from) - Mathf.Clamp01(to)) * MaxValue;
            return drop <= 0f ? 0f : drop / DrainPerSecond(kind);
        }

        /// <summary>가득에서 경고선까지 몇 초.</summary>
        public static float SecondsFromFullToWarning(SustenanceKind kind) =>
            SecondsBetween(kind, 1f, WarningThreshold);

        /// <summary>
        /// <b>몇 번 나갔다 와야 채우는가.</b> 이 항목의 설계가 성립하는지를 묻는
        /// 하나의 물음이고, 그래서 규칙으로 서 있다.
        ///
        /// 한 사이클은 <b>원정 한 번 + 거점 체류</b>다. 거점 체류는 잴 수 없으므로
        /// 인자로 받는다 — 0을 넣으면 「쉬지 않고 계속 나간다」는 가장 빡빡한 모형이다.
        /// </summary>
        public static float ExpeditionsUntilWarning(SustenanceKind kind, float baseCampSeconds)
        {
            float cycle = ExpeditionSeconds + Mathf.Max(0f, baseCampSeconds);
            return SecondsFromFullToWarning(kind) / cycle;
        }

        // ══ 화면과 벌 ═══════════════════════════════════════════

        /// <summary>
        /// 화면에 나타나야 하는가. <b>이 함수가 「눈에 띄지 않는다」의 실체다</b> —
        /// 원정 한 번을 돌아도 참이 되지 않아야 한다.
        /// </summary>
        public static bool IsWarning(float normalized) =>
            Mathf.Clamp01(normalized) <= WarningThreshold;

        /// <summary>
        /// 이 비율에서의 이동 배율.
        ///
        /// 경고선까지는 <b>1.0 그대로다</b>. <see cref="SlowdownThreshold"/> 아래로
        /// 내려가야 비로소 느려지기 시작하고, 바닥에서 <see cref="SlowestMoveFactor"/>가 된다.
        /// 절벽처럼 뚝 떨어지지 않게 그 사이를 선형으로 잇는다 — 사람 눈은 값보다
        /// 값의 변화에 민감해서, 한 프레임에 배율이 바뀌면 그것을 「고장」으로 읽는다.
        /// </summary>
        public static float MoveFactor(float normalized)
        {
            float t = Mathf.Clamp01(normalized);
            if (t >= SlowdownThreshold) return 1f;
            return Mathf.Lerp(SlowestMoveFactor, 1f, t / SlowdownThreshold);
        }

        /// <summary>
        /// 둘이 함께 낮을 때의 이동 배율. <b>곱하지 않고 나쁜 쪽 하나만 쓴다.</b>
        ///
        /// <see cref="Survive.World.EnvironmentThreat"/>가 장비를 고를 때 합산하지 않고
        /// 가장 유리한 하나를 쓰는 것과 같은 규율이다. 곱하면 둘이 동시에 바닥일 때
        /// 0.49가 되어 사실상 갇히고, 무엇보다 <b>플레이어가 배울 규칙이 둘</b>이 된다.
        /// </summary>
        public static float WorstMoveFactor(float hydrationNormalized, float foodNormalized) =>
            Mathf.Min(MoveFactor(hydrationNormalized), MoveFactor(foodNormalized));

        // ══ 채우기 ══════════════════════════════════════════════

        /// <summary>
        /// 이 액체에서 <b>초당 얼마나 수분을 얻는가.</b>
        ///
        /// <b>매크로늄은 정제 수단 없이는 0이다.</b> 70%가 물이지만 음용은 불가능하고
        /// (<see cref="Liquid.IsPotable"/>), 그 사실을 여기서 뒤집으면 규칙 둘이
        /// 서로 다른 말을 하게 된다. 정제 수단이 붙어야 비로소 값이 열린다 —
        /// 그것이 §5.15의 「매크로늄 표면에서 확보」이고, 착륙선의 처리장치도
        /// 같은 자리에 붙는다(마르지 않는 정제 수단이라는 점만 다르다).
        ///
        /// <b>정제는 물길의 이름이 요구하는 것이 아니라 액체의 종류가 요구한다.</b>
        /// 「강물 정제」의 강물이 진짜 물이면 그냥 마시는 것이고, 매크로늄이 섞여
        /// 흐르는 것이면 정제해야 한다. 규칙이 이름을 묻지 않으므로 지형이 어떻게
        /// 만들어지든 답이 흔들리지 않는다.
        /// </summary>
        public static float HydrationPerSecondFrom(LiquidKind kind, bool hasPurifier)
        {
            // Require가 모르는 종류를 이미 던진다. 아래 default는 컴파일러를
            // 달래는 자리이지 규칙이 아니다.
            switch (Liquid.Require(kind))
            {
                case LiquidKind.Water:     return LakeHydrationPerSecond;
                case LiquidKind.Macronium: return hasPurifier ? PurifiedHydrationPerSecond : 0f;
                default: throw Unknown(SustenanceKind.Hydration);
            }
        }

        /// <summary>
        /// 이 액체에서 <b>초당 얼마나 먹을 것을 얻는가.</b>
        ///
        /// <b>매크로늄은 언제나 0이고 정제로도 열리지 않는다.</b> 물은 걸러 낼 수
        /// 있지만 없는 생물을 걸러 낼 수는 없다 — 지상에서 생물이 남은 자리는
        /// 호수와 그 근방뿐이다(세계관 §5). <b>이 한 줄이 「거점으로 돌아갈 이유」다.</b>
        /// 여기에 값을 넣는 순간 바다 위에서도 살 수 있게 되고, 이 항목 전체가
        /// 다시 잡무가 된다.
        /// </summary>
        public static float FoodPerSecondFrom(LiquidKind kind)
        {
            switch (Liquid.Require(kind))
            {
                case LiquidKind.Water:     return LakeFoodPerSecond;
                case LiquidKind.Macronium: return 0f;
                default: throw Unknown(SustenanceKind.Food);
            }
        }

        /// <summary>
        /// 모르는 종류를 만났을 때 던질 것. 글이 영어인 이유는
        /// <see cref="Liquid"/>와 같다 — 개발자 콘솔에만 닿는 진단문이다.
        /// </summary>
        static System.ArgumentOutOfRangeException Unknown(SustenanceKind kind) =>
            new System.ArgumentOutOfRangeException(
                nameof(kind), kind,
                "Sustenance kind is not one of Hydration/Food. " +
                "There is no default on purpose: the caller must say which gauge this is.");
    }
}
