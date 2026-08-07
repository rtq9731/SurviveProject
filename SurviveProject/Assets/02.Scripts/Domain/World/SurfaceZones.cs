using System.Collections.Generic;

namespace Survive.World
{
    /// <summary>
    /// 지상 구역의 <b>데이터</b>. 어느 구역이 어떤 액체를 품는가, 그 액체가 무엇으로
    /// 되어 있고 얼마나 깊고 넓은가.
    ///
    /// <b>여기에는 규칙이 없다.</b> 판정은 전부 <see cref="LiquidCrossing"/>이 하고,
    /// 이 표는 그 판정에 넣을 수만 들고 있다. 구역이 늘거나 폭이 바뀌어도 규칙은
    /// 그대로다 - 그것이 "세 특례가 아니라 한 규칙의 세 값"의 실제 모습이다.
    ///
    /// <b>이 표의 액체는 넷이고 종류는 둘이다</b>(<see cref="KindOf"/>). 셋은 매크로늄이고
    /// 하나는 진짜 물이다. 호수를 표에서 빼 두지 않는 이유는 <see cref="LiquidKind"/>의
    /// 주석과 같다 - <b>빠지는 것과 0을 계산하는 것은 다르다.</b>
    ///
    /// <b>수는 임시다(스펙 §13).</b> 수로가 어디를 어떻게 파고드는지, 여울이 얼마나
    /// 넓은지는 눈으로 정해지는 것이고 아직 정해지지 않았다. 다만 <b>규칙이 요구하는
    /// 하한</b>은 임시가 아니다 — <see cref="OpenWaterMinimumWidth"/>보다 좁은 먼바다는
    /// 헤엄쳐 건널 수 있게 되고, 그러면 액면 보행 장비가 아무것도 열지 않는다.
    /// </summary>
    public static class SurfaceZones
    {
        /// <summary>
        /// 맨몸 체력의 최대치. <c>08.Data/Vitals/Health.asset</c>의 값과 같아야 한다 —
        /// 먼바다가 죽이는가는 이 수로 갈리므로, 둘이 갈라지면 표가 거짓말을 한다.
        /// <c>SurfaceZoneTests</c>가 대조한다.
        /// </summary>
        public const float FullHealth = 100f;

        /// <summary>
        /// 걸어 나가는 순서. <b>순서가 곧 배움의 순서다</b> —
        /// 거점 → 마실 물 → 아프다 → 뭍 → 무해 → 뭍 → 죽음 → 건너편.
        ///
        /// <b>호수가 두 번째인 것이 설계다.</b> 처음 만나는 액체가 무해한 물이고
        /// 그다음이 똑같이 생긴 아픈 액면이라, 플레이어는 첫 수업에서
        /// <b>"액체는 종류가 있다"</b>를 배운 뒤에 값을 치른다.
        /// </summary>
        public static readonly SurfaceZone[] Order =
        {
            SurfaceZone.Landing,
            SurfaceZone.Lake,
            SurfaceZone.Inlet,
            SurfaceZone.Inland,
            SurfaceZone.Shallows,
            SurfaceZone.Shore,
            SurfaceZone.OpenWater,
            SurfaceZone.Farside,
        };

        // ── 액체 넷의 수 ────────────────────────────────────────

        /// <summary>
        /// 수로의 폭(m). <b>실측에서 왔다</b> — 임시 지형의 물가 (40,-2)에서 (82,12)까지
        /// 실제로 헤엄쳐 건넌 거리다(<see cref="MacroniumSea.CorrosionPerSecond"/> 주석).
        /// 그 횡단이 체력의 3분의 1쯤을 가져가는 것이 이 구간의 수업이다.
        /// </summary>
        public const float InletWidth = 41.2f;

        /// <summary>
        /// 수로의 깊이(m). <see cref="LiquidCrossing.SwimDepth"/>를 넘기기만 하면
        /// 답이 같다 — 몸이 뜨는 순간부터 값은 폭이 정한다. 임시 값이다.
        /// </summary>
        public const float InletDepth = 2f;

        /// <summary>
        /// 여울의 깊이(m). 발목 깊이 — 발은 잠기지만 바닥을 딛는다.
        /// <see cref="LiquidCrossing.WadeDepth"/>와 <see cref="LiquidCrossing.SwimDepth"/>
        /// 사이여야 하고, 그 안이면 어디든 답이 같다.
        /// </summary>
        public const float ShallowsDepth = 0.6f;

        /// <summary>
        /// 여울의 폭(m). <b>이 수는 답을 바꾸지 않는다</b> — 발을 딛고 있는 한
        /// 얼마를 걸어도 무해하다. 그래도 적어 두는 이유는 사람이 지형을 놓을 때
        /// "여기까지가 여울"이라는 눈금이 필요해서다. 임시 값이다.
        /// </summary>
        public const float ShallowsWidth = 24f;

        /// <summary>
        /// 호수의 깊이(m). <b>일부러 헤엄쳐야 하는 깊이로 잡았다.</b> 발이 닿을 만큼
        /// 얕게 잡으면 「무해한 이유가 물이라서」인지 「얕아서」인지 갈리지 않는다.
        /// </summary>
        public const float LakeDepth = 4f;

        /// <summary>
        /// 호수의 폭(m). 임시 값이지만 <b>수로보다는 넓게</b> 잡는다 — 더 오래 잠기고도
        /// 값이 0이어야 종류 축이 실제로 일한 것이 보인다.
        /// </summary>
        public const float LakeWidth = 60f;

        /// <summary>
        /// 먼바다의 깊이(m). 몸이 뜨기만 하면 답이 같다. 임시 값이다.
        /// </summary>
        public const float OpenWaterDepth = 20f;

        /// <summary>
        /// 먼바다가 먼바다이려면 이보다 넓어야 한다(m). <b>규칙에서 나온 수다</b> -
        /// 맨몸으로 헤엄쳐 건널 수 있는 한계가 곧 하한이다.
        /// </summary>
        public static float OpenWaterMinimumWidth =>
            LiquidCrossing.LethalWidth(LiquidKind.Macronium, FullHealth);

        /// <summary>
        /// 하한에 두는 여유(배). 경계에 딱 맞추면 "헤엄쳐서 못 건넌다"가
        /// 아슬아슬해져서, 한 번 살아 돌아오는 사람이 생기고 그 순간 수업이 무너진다.
        /// 설명 없이 읽히려면 여유가 있어야 한다.
        /// </summary>
        public const float OpenWaterWidthMargin = 1.2f;

        /// <summary>먼바다의 폭(m). 진짜 폭은 사람이 정한다 — 이것은 하한에 여유를 얹은 값.</summary>
        public static float OpenWaterWidth => OpenWaterMinimumWidth * OpenWaterWidthMargin;

        // ── 조회 ────────────────────────────────────────────────

        /// <summary>이 구역이 액체인가.</summary>
        public static bool IsLiquid(SurfaceZone zone) =>
            zone == SurfaceZone.Lake || zone == SurfaceZone.Inlet ||
            zone == SurfaceZone.Shallows || zone == SurfaceZone.OpenWater;

        /// <summary>
        /// 이 구역의 액체가 무엇으로 되어 있는가.
        ///
        /// <b>뭍에는 답이 없다.</b> 「액체가 없는 자리의 액체 종류」를 물으면 던진다 -
        /// 물이나 매크로늄 중 하나를 조용히 돌려주면, 종류를 안 정한 구역이 생겨도
        /// 아무도 모른 채 지나간다. 부르기 전에 <see cref="IsLiquid"/>로 갈라라.
        /// </summary>
        public static LiquidKind KindOf(SurfaceZone zone)
        {
            switch (zone)
            {
                case SurfaceZone.Lake:
                    return LiquidKind.Water;
                case SurfaceZone.Inlet:
                case SurfaceZone.Shallows:
                case SurfaceZone.OpenWater:
                    return LiquidKind.Macronium;
                default:
                    // 글이 영어인 이유는 Liquid.Unknown의 주석과 같다 - 화면이 아니라
                    // 개발자 콘솔에만 닿는 진단문이고, 이 폴더는 한글 리터럴 금지 범위다.
                    throw new System.ArgumentOutOfRangeException(
                        nameof(zone), zone,
                        "Dry land holds no liquid. Ask IsLiquid before asking for one.");
            }
        }

        /// <summary>
        /// 이 구역의 액체. <b>뭍을 물으면 던진다</b> - 이유는 <see cref="KindOf"/>와 같다.
        /// </summary>
        public static LiquidBody LiquidAt(SurfaceZone zone)
        {
            var kind = KindOf(zone);
            switch (zone)
            {
                case SurfaceZone.Lake:     return new LiquidBody(kind, LakeDepth, LakeWidth);
                case SurfaceZone.Inlet:    return new LiquidBody(kind, InletDepth, InletWidth);
                case SurfaceZone.Shallows: return new LiquidBody(kind, ShallowsDepth, ShallowsWidth);
                default:                   return new LiquidBody(kind, OpenWaterDepth, OpenWaterWidth);
            }
        }

        /// <summary>
        /// 이 구역을 지금 갖춘 것으로 지날 때 벌어지는 일.
        /// 뭍은 언제나 <see cref="CrossingVerdict.Harmless"/>다.
        /// </summary>
        public static CrossingVerdict Verdict(SurfaceZone zone, IReadOnlyList<GearCapability> loadout,
                                              float health = FullHealth)
        {
            if (!IsLiquid(zone)) return CrossingVerdict.Harmless;
            return LiquidCrossing.Judge(LiquidAt(zone), loadout, health);
        }

        /// <summary>
        /// 위험도를 수로 매긴 것 — 맨몸으로 지날 때 잃는 체력.
        /// <b>수로 &gt; 여울</b>이 이 표의 요점이고, 그 역전이 설계다.
        /// </summary>
        public static float Risk(SurfaceZone zone) =>
            IsLiquid(zone) ? LiquidCrossing.Toll(LiquidAt(zone)) : 0f;

        /// <summary>
        /// 화면에 나갈 이름의 번역 키. <b>글자는 코드에 적지 않는다.</b>
        /// 표의 갈래는 <c>Zone</c>이다.
        /// </summary>
        public const string NameCategory = "Zone";

        /// <summary>번역 표에서 이 구역의 이름을 찾는 키.</summary>
        public static string NameKey(SurfaceZone zone)
        {
            switch (zone)
            {
                case SurfaceZone.Landing:   return "landing";
                case SurfaceZone.Lake:      return "lake";
                case SurfaceZone.Inlet:     return "inlet";
                case SurfaceZone.Inland:    return "inland";
                case SurfaceZone.Shallows:  return "shallows";
                case SurfaceZone.Shore:     return "shore";
                case SurfaceZone.OpenWater: return "open_water";
                case SurfaceZone.Farside:   return "farside";
                default:                    return "";
            }
        }
    }
}
