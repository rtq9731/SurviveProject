using System.Collections.Generic;

namespace Survive.World
{
    /// <summary>
    /// 구역 다섯의 <b>데이터</b>. 어느 구역이 어떤 액체를 품는가, 그 액체가 얼마나
    /// 깊고 넓은가.
    ///
    /// <b>여기에는 규칙이 없다.</b> 판정은 전부 <see cref="LiquidCrossing"/>이 하고,
    /// 이 표는 그 판정에 넣을 수만 들고 있다. 구역이 늘거나 폭이 바뀌어도 규칙은
    /// 그대로다 — 그것이 "세 특례가 아니라 한 규칙의 세 값"의 실제 모습이다.
    ///
    /// <b>수는 임시다(§16).</b> 강이 어디를 어떻게 가로지르는지, 얕은 바다가 얼마나
    /// 넓은지는 눈으로 정해지는 것이고 아직 정해지지 않았다. 다만 <b>규칙이 요구하는
    /// 하한</b>은 임시가 아니다 — <see cref="SeaMinimumWidth"/>보다 좁은 바다는
    /// 헤엄쳐 건널 수 있게 되고, 그러면 액면 보행 장비가 아무것도 열지 않는다.
    /// </summary>
    public static class IslandZones
    {
        /// <summary>
        /// 맨몸 체력의 최대치. <c>08.Data/Vitals/Health.asset</c>의 값과 같아야 한다 —
        /// 바다가 죽이는가는 이 수로 갈리므로, 둘이 갈라지면 표가 거짓말을 한다.
        /// <c>IslandZoneTests</c>가 대조한다.
        /// </summary>
        public const float FullHealth = 100f;

        /// <summary>
        /// 걸어 나가는 순서. <b>순서가 곧 배움의 순서다</b> —
        /// 시작 → 아프다 → 뭍 → 무해 → 뭍 → 죽음 → 건너편.
        /// </summary>
        public static readonly IslandZone[] Order =
        {
            IslandZone.A1,
            IslandZone.River,
            IslandZone.A2,
            IslandZone.ShallowSea,
            IslandZone.A3,
            IslandZone.Sea,
            IslandZone.IslandB,
        };

        // ── 액체 셋의 수 ────────────────────────────────────────

        /// <summary>
        /// 강의 폭(m). <b>실측에서 왔다</b> — 임시 지형의 물가 (40,-2)에서 (82,12)까지
        /// 실제로 헤엄쳐 건넌 거리다(<see cref="MacroniumSea.CorrosionPerSecond"/> 주석).
        /// 그 횡단이 체력의 3분의 1쯤을 가져가는 것이 이 구간의 수업이다.
        /// </summary>
        public const float RiverWidth = 41.2f;

        /// <summary>
        /// 강의 깊이(m). <see cref="LiquidCrossing.SwimDepth"/>를 넘기기만 하면
        /// 답이 같다 — 몸이 뜨는 순간부터 값은 폭이 정한다. 임시 값이다.
        /// </summary>
        public const float RiverDepth = 2f;

        /// <summary>
        /// 얕은 바다의 깊이(m). 발목 깊이 — 발은 잠기지만 바닥을 딛는다.
        /// <see cref="LiquidCrossing.WadeDepth"/>와 <see cref="LiquidCrossing.SwimDepth"/>
        /// 사이여야 하고, 그 안이면 어디든 답이 같다.
        /// </summary>
        public const float ShallowSeaDepth = 0.6f;

        /// <summary>
        /// 얕은 바다의 폭(m). <b>이 수는 답을 바꾸지 않는다</b> — 발을 딛고 있는 한
        /// 얼마를 걸어도 무해하다. 그래도 적어 두는 이유는 사람이 지형을 놓을 때
        /// "여기까지가 얕은 바다"라는 눈금이 필요해서다. 임시 값이다.
        /// </summary>
        public const float ShallowSeaWidth = 24f;

        /// <summary>
        /// 바다의 깊이(m). 몸이 뜨기만 하면 답이 같다. 임시 값이다.
        /// </summary>
        public const float SeaDepth = 20f;

        /// <summary>
        /// 바다가 바다이려면 이보다 넓어야 한다(m). <b>규칙에서 나온 수다</b> —
        /// 맨몸으로 헤엄쳐 건널 수 있는 한계가 곧 하한이다.
        /// </summary>
        public static float SeaMinimumWidth => LiquidCrossing.LethalWidth(FullHealth);

        /// <summary>
        /// 하한에 두는 여유(배). 경계에 딱 맞추면 "헤엄쳐서 못 건넌다"가
        /// 아슬아슬해져서, 한 번 살아 돌아오는 사람이 생기고 그 순간 수업이 무너진다.
        /// 설명 없이 읽히려면 여유가 있어야 한다.
        /// </summary>
        public const float SeaWidthMargin = 1.2f;

        /// <summary>바다의 폭(m). 진짜 폭은 §16이 정한다 — 이것은 하한에 여유를 얹은 값.</summary>
        public static float SeaWidth => SeaMinimumWidth * SeaWidthMargin;

        // ── 조회 ────────────────────────────────────────────────

        /// <summary>이 구역이 액체인가.</summary>
        public static bool IsLiquid(IslandZone zone) =>
            zone == IslandZone.River || zone == IslandZone.ShallowSea || zone == IslandZone.Sea;

        /// <summary>이 구역의 액체. 뭍이면 깊이도 폭도 0이다.</summary>
        public static LiquidBody LiquidAt(IslandZone zone)
        {
            switch (zone)
            {
                case IslandZone.River:      return new LiquidBody(RiverDepth, RiverWidth);
                case IslandZone.ShallowSea: return new LiquidBody(ShallowSeaDepth, ShallowSeaWidth);
                case IslandZone.Sea:        return new LiquidBody(SeaDepth, SeaWidth);
                default:                    return new LiquidBody(0f, 0f);
            }
        }

        /// <summary>
        /// 이 구역을 지금 갖춘 것으로 지날 때 벌어지는 일.
        /// 뭍은 언제나 <see cref="CrossingVerdict.Harmless"/>다.
        /// </summary>
        public static CrossingVerdict Verdict(IslandZone zone, IReadOnlyList<GearCapability> loadout,
                                              float health = FullHealth)
        {
            if (!IsLiquid(zone)) return CrossingVerdict.Harmless;
            return LiquidCrossing.Judge(LiquidAt(zone), loadout, health);
        }

        /// <summary>
        /// 위험도를 수로 매긴 것 — 맨몸으로 지날 때 잃는 체력.
        /// <b>강 &gt; 얕은 바다</b>가 이 표의 요점이고, 그 역전이 설계다.
        /// </summary>
        public static float Risk(IslandZone zone) =>
            IsLiquid(zone) ? LiquidCrossing.Toll(LiquidAt(zone)) : 0f;

        /// <summary>
        /// 화면에 나갈 이름의 번역 키. <b>글자는 코드에 적지 않는다.</b>
        /// 표의 갈래는 <c>Zone</c>이다.
        /// </summary>
        public const string NameCategory = "Zone";

        /// <summary>번역 표에서 이 구역의 이름을 찾는 키.</summary>
        public static string NameKey(IslandZone zone)
        {
            switch (zone)
            {
                case IslandZone.A1:         return "a1";
                case IslandZone.River:      return "river";
                case IslandZone.A2:         return "a2";
                case IslandZone.ShallowSea: return "shallow_sea";
                case IslandZone.A3:         return "a3";
                case IslandZone.Sea:        return "sea";
                case IslandZone.IslandB:    return "island_b";
                default:                    return "";
            }
        }
    }
}
