using Survive.Items;

namespace Survive.UI
{
    /// <summary>
    /// 쪽지에 <b>무엇을</b> 적을 것인가.
    ///
    /// 아이템 에셋 23종에는 진작부터 <see cref="ItemDataSO.description"/>이 적혀 있었는데
    /// 그 필드를 읽는 코드가 한 줄도 없었다. 세계관 문장이 통째로 화면에 닿지 못한 셈이다.
    /// 여기서 하는 일은 그 문장을 화면에 쓸 수 있는 세 토막으로 나누는 것뿐이다.
    ///
    /// <b>과하게 채우지 않는다.</b> 이름·설명·분류 한 줄이면 된다. 무게·판매가 같은
    /// 있지도 않은 값을 자리만 잡아 두면, 정작 읽히기를 바라는 설명문이 묻힌다.
    ///
    /// 화면에 나가는 문자열이므로 줄표(U+2014)를 쓰지 않는다 — 본문 글꼴(ChosunGu)에
    /// 없어서 네모로 찍힌다. 가운뎃점을 쓴다.
    /// </summary>
    public static class ItemTooltipContent
    {
        /// <summary>분류와 묶음 수 사이. 제작 목록도 같은 기호로 잇는다.</summary>
        public const string Separator = "  ·  ";

        /// <summary>쪽지를 띄울 값어치가 있는가. 이름조차 없으면 띄우지 않는다.</summary>
        public static bool CanShow(ItemDataSO item) =>
            item != null && !string.IsNullOrWhiteSpace(Title(item));

        /// <summary>이름. 표시 이름이 비어 있으면 id라도 보여 준다.</summary>
        public static string Title(ItemDataSO item)
        {
            if (item == null) return "";
            return string.IsNullOrWhiteSpace(item.displayName)
                ? (item.id ?? "")
                : item.displayName.Trim();
        }

        /// <summary>본문. 이 한 덩어리를 읽히게 하려고 쪽지를 만든 것이다.</summary>
        public static string Body(ItemDataSO item) =>
            item == null || string.IsNullOrWhiteSpace(item.description)
                ? ""
                : item.description.Trim();

        /// <summary>
        /// 아래 한 줄. 분류는 늘 적고, 겹쳐지는 물건만 묶음 수를 덧붙인다 —
        /// 도구마다 "최대 1개"라고 적어 봐야 아무것도 알려 주지 않는다.
        /// </summary>
        public static string Meta(ItemDataSO item)
        {
            if (item == null) return "";

            string category = CategoryName(item.category);
            return item.maxStack > 1
                ? category + Separator + "한 칸에 " + item.maxStack + "개"
                : category;
        }

        /// <summary>분류 이름. 열거형 이름을 그대로 화면에 내보내지 않는다.</summary>
        public static string CategoryName(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Tool:       return "도구";
                case ItemCategory.Consumable: return "소모품";
                case ItemCategory.Quest:      return "진행 물품";
                case ItemCategory.Resource:   return "재료";
                default:                      return "물품";
            }
        }
    }
}
