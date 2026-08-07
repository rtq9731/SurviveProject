using System;

namespace Survive.World
{
    /// <summary>
    /// 액체가 <b>무엇으로 된 것인가</b>. 이 행성에 액체는 두 가지뿐이다.
    ///
    /// <b>왜 0에 이름을 두지 않는가.</b> C#의 열거형은 채워 넣지 않으면 0이 되고,
    /// 0에 이름이 있으면 그것이 조용한 기본값이 된다. 「모르는 액체」에 물이나
    /// 매크로늄을 말없이 돌려주는 순간, 씬에 종류를 안 적은 물웅덩이가 생겨도
    /// 아무도 모르고, 마실 수 없는 것을 마시거나 무해한 것에 살이 깎인다.
    /// 그래서 <b>0은 이름 없는 자리로 비워 두고</b>, <see cref="Liquid"/>의 조회는
    /// 그 값을 받으면 <b>던진다.</b> 모르면 실패해야 한다.
    ///
    /// <b>둘을 가르는 것은 부식 하나뿐이다.</b> 헤엄은 둘 다 되고, 머리가 잠기면
    /// 둘 다 숨이 막히고, 부력도 같다. 종류가 답을 바꾸는 자리는
    /// <see cref="Liquid.CorrosionPerSecond"/>와 <see cref="Liquid.IsPotable"/> 둘이다.
    /// </summary>
    public enum LiquidKind
    {
        /// <summary>
        /// 진짜 물. 증발과 강수로 내려 고인 것이라 <b>마실 수 있고 닿아도 무해하다</b>
        /// (세계관 §2). 지상에서 생물이 사는 자리가 여기뿐이므로 수분과 식량의 출처다.
        /// </summary>
        Water = 1,

        /// <summary>
        /// 매크로늄. <b>70%가 물인데 마실 수 없고, 담그면 살이 깎인다</b>(세계관 §3).
        /// 깊이가 곧 농도이고 농도가 곧 위험도다.
        /// </summary>
        Macronium = 2,
    }

    /// <summary>
    /// 종류마다 값이 다른 것들의 <b>표</b>. 규칙이 아니라 표라는 것이 요점이다.
    ///
    /// <b>특례 둘이 아니라 한 규칙의 두 값이다.</b> 판정은 <see cref="LiquidCrossing"/>
    /// 하나가 하고, 그 판정이 종류에게 묻는 것은 「초당 얼마인가」 하나뿐이다.
    /// 호수가 무해한 것은 판정에서 빠지기 때문이 아니라 <b>같은 판정에 0을 넣기
    /// 때문</b>이고, 그래서 폭을 열 배로 넓혀도, 잠수해도, 답은 그대로 0이다.
    ///
    /// <b>여기에 갈래를 하나 더 만들지 마라.</b> 「어떤 액체는 아프고 어떤 액체는
    /// 안 아프다」가 특례로 적히는 순간 플레이어는 규칙을 배울 수 없게 된다.
    /// 새 성질이 필요하면 이 표에 칸을 하나 더 붙이는 것이 맞다.
    /// </summary>
    public static class Liquid
    {
        /// <summary>종류는 이 둘이 전부다. 검사가 「둘뿐」을 셀 때 쓰는 자리이기도 하다.</summary>
        public static readonly LiquidKind[] All = { LiquidKind.Water, LiquidKind.Macronium };

        /// <summary>이름이 있는 종류인가. 0이나 엉뚱한 정수면 거짓이다.</summary>
        public static bool IsKnown(LiquidKind kind) =>
            kind == LiquidKind.Water || kind == LiquidKind.Macronium;

        /// <summary>
        /// 모르는 종류면 던진다. <b>조용히 넘어가는 길을 두지 않으려고</b> 있는 것이다.
        /// </summary>
        public static LiquidKind Require(LiquidKind kind)
        {
            if (!IsKnown(kind)) throw Unknown(kind);
            return kind;
        }

        /// <summary>
        /// 몸이 잠긴 채 있을 때 <b>초당 깎이는 체력</b>.
        ///
        /// 물은 0이다. 0을 돌려주는 것과 판정을 건너뛰는 것은 다르다.
        /// 앞은 규칙이 답한 것이고 뒤는 규칙이 없는 것이다.
        /// </summary>
        public static float CorrosionPerSecond(LiquidKind kind)
        {
            switch (kind)
            {
                case LiquidKind.Water:     return 0f;
                case LiquidKind.Macronium: return MacroniumSea.CorrosionPerSecond;
                default: throw Unknown(kind);
            }
        }

        /// <summary>
        /// 마실 수 있는가. 매크로늄은 70%가 물이지만 <b>음용은 불가능하다</b>(세계관 §3).
        /// 수분 확보(기획서 §5.15)가 어느 액체 앞에 섰는지를 물을 창구가 이것이다.
        /// </summary>
        public static bool IsPotable(LiquidKind kind)
        {
            switch (kind)
            {
                case LiquidKind.Water:     return true;
                case LiquidKind.Macronium: return false;
                default: throw Unknown(kind);
            }
        }

        /// <summary>
        /// 모르는 종류를 만났을 때 던질 것.
        ///
        /// <b>글을 영어로 적는 이유.</b> 이것은 화면이 아니라 개발자 콘솔에만 닿는
        /// 진단문이고, <c>Domain/World/</c>는 화면 코드 범위여서 한글 리터럴이
        /// 금지되어 있다(<c>LocSentenceGateTests</c>). 표로 옮길 글이 아니므로
        /// 면제 목록을 늘리는 대신 영어로 적는다.
        /// </summary>
        static ArgumentOutOfRangeException Unknown(LiquidKind kind) =>
            new ArgumentOutOfRangeException(
                nameof(kind), kind,
                "Liquid kind is not one of Water/Macronium. " +
                "There is no default on purpose: the caller must say which liquid this is.");
    }
}
