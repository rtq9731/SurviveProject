using Survive.Items;
using Survive.Localization;

namespace Survive.Harvesting
{
    /// <summary>
    /// 이 도구로 이 채집물을 캘 수 있는가.
    ///
    /// <b>도구는 전용이다.</b> 곡괭이로는 돌을 깨고 도끼로는 나무를 벤다.
    /// 하나가 전부를 하면 도구를 여러 개 만들 이유가 사라지고,
    /// "이름만 보고 안다"는 티어 원칙도 도구에서는 성립하지 않는다 —
    /// 곡괭이라는 이름이 곡괭이가 하는 일을 말해 주어야 한다.
    ///
    /// <b>등급은 같은 종류 안에서만 견준다.</b> 더 좋은 곡괭이는 더 단단한 광맥을
    /// 캐지만, 아무리 좋은 곡괭이라도 나무는 베지 못한다.
    ///
    /// <b>왜 여기 있는가.</b> 한 줄짜리 조건이지만 이 조건이 게임의 도구 체계
    /// 전체를 정한다. 실수로 <c>||</c> 하나가 되면 도구가 통째로 무의미해지는데,
    /// 그 순간을 잡아 줄 곳이 여기 말고는 없다.
    /// </summary>
    public static class ToolMatch
    {
        /// <param name="required">노드가 요구하는 도구 종류. None이면 맨손으로 된다.</param>
        /// <param name="requiredTier">그 종류에서 요구하는 최소 등급.</param>
        /// <param name="have">지금 손에 든 도구의 종류. 맨손이면 None.</param>
        /// <param name="haveTier">지금 손에 든 도구의 등급.</param>
        public static bool Satisfies(ToolType required, int requiredTier,
                                     ToolType have, int haveTier)
        {
            if (required == ToolType.None) return true;   // 맨손으로 되는 것은 도구가 있어도 된다
            if (have != required) return false;           // 종류가 다르면 등급을 볼 것도 없다
            return haveTier >= requiredTier;
        }

        /// <summary>
        /// 화면에 보일 도구 이름. 채집 프롬프트가 "무엇이 필요한가"를 말할 때 쓴다.
        ///
        /// <b>상수가 아니라 매번 표를 뒤진다.</b> 상수로 두면 로케일을 바꿔도
        /// 그 자리만 옛 언어로 남는다 — 프롬프트는 매 프레임 다시 물으므로
        /// 여기서 꺼내면 저절로 따라온다.
        /// </summary>
        public static string Name(ToolType t) => t switch
        {
            ToolType.Pickaxe => Loc.T("World", "tool_pickaxe"),
            ToolType.Hammer => Loc.T("World", "tool_hammer"),
            ToolType.Axe => Loc.T("World", "tool_axe"),
            _ => Loc.T("World", "tool_generic")
        };
    }
}
