using System.Text;
using Survive.Building;
using Survive.Crafting;
using Survive.Items;
using Survive.Progression;

namespace Survive.UI
{
    /// <summary>
    /// 제작·건축 목록에 <b>무엇이 실리는가</b>, 그리고 실린 줄에 <b>무엇이 적히는가</b>.
    ///
    /// 모르는 것은 줄을 만들지 않는다. 회색으로 가라앉혀 남겨 두던 시절에는
    /// 잠긴 줄이 "잠항 설계 청사진이 필요하다: 낫의 핵을 연구대에서 분석하면 알게 된다"
    /// 까지 적어 주었다. 낫을 본 적도 없는 사람에게 아이템 이름과 후반 전개가
    /// 통째로 새어 나간 셈이다. 배우면 그때 줄이 생긴다 — 목록이 자라는 것 자체가
    /// 보상으로 읽히는 편이 낫다.
    ///
    /// 규칙을 여기 둔 이유는 <b>증명하기 위해서</b>다. 화면에 나갈 수 있는 문자열이
    /// 전부 이 파일에서 나오면, "그 어디에도 청사진 힌트가 없다"를 한 번에 단언할 수 있다.
    ///
    /// 화면에 나가는 문자열이므로 줄표(U+2014)를 쓰지 않는다 — 본문 글꼴(ChosunGu)에
    /// 없어서 네모(□)로 찍힌다. 가운뎃점을 쓴다.
    /// </summary>
    public static class MenuListing
    {
        /// <summary>이름과 뒷말을 잇는 기호. 쪽지(ItemTooltipContent)와 같은 것을 쓴다.</summary>
        public const string Separator = "  ·  ";

        /// <summary>
        /// 아는 제작법이 하나도 없을 때 그 자리에 남기는 한 줄.
        ///
        /// 몇 개가 잠겨 있는지는 세어 주지 않는다. 그 숫자도 "앞으로 이만큼 남았다"는
        /// 정보라, 감추기로 한 것을 옆문으로 흘리는 셈이 된다.
        /// </summary>
        public const string NothingKnownToCraft = "아직 아는 제작법이 없다";

        /// <summary>건축 목록의 같은 자리.</summary>
        public const string NothingKnownToBuild = "아직 아는 건축물이 없다";

        // ── 무엇이 실리는가 ──────────────────────────────────────

        /// <summary>
        /// 이 레시피가 지금 이 자리의 목록에 실리는가.
        ///
        /// 두 가지를 함께 본다. <b>자리</b>가 맞아야 하고(손 제작 목록에는 손으로
        /// 되는 것만, 제작대에는 손 것까지 함께), <b>알고 있어야</b> 한다.
        ///
        /// 원장이 아직 서지 않았으면 막지 않는다(<see cref="BlueprintGate.IsUnlocked"/>) —
        /// 실패는 개방 쪽으로. 잠금이 판을 통째로 얼어붙게 만드는 것보다는
        /// 잠깐 다 보이는 편이 낫다.
        /// </summary>
        public static bool ShouldList(RecipeSO r, StationType station, UnlockLedger ledger)
        {
            if (r == null) return false;
            if (r.requiredStation != StationType.None && r.requiredStation != station) return false;
            return BlueprintGate.IsUnlocked(r.requiredBlueprint, ledger);
        }

        /// <summary>이 건축물이 목록에 실리는가. 건축에는 자리 구분이 없다.</summary>
        public static bool ShouldList(BuildableSO b, UnlockLedger ledger) =>
            b != null && BlueprintGate.IsUnlocked(b.requiredBlueprint, ledger);

        /// <summary>
        /// 판때기 높이를 잴 때 세는 줄 수.
        ///
        /// 실린 것이 하나도 없어도 0이 아니다. 게임 초반에는 아는 것이 정말 0개일 수
        /// 있는데, 그때 0으로 재면 판때기가 위아래 여백만 남은 띠로 찌그러진다.
        /// 빈 자리에는 <see cref="NothingKnownToCraft"/> 한 줄이 대신 선다.
        /// </summary>
        public static int PanelRows(int listedRows) => listedRows <= 0 ? 1 : listedRows;

        // ── 무엇이 적히는가 ──────────────────────────────────────

        /// <summary>
        /// 제작 줄 한 줄. 이름, 재료 가진 수/드는 수, 걸리는 시간.
        /// 여기 오는 것은 이미 아는 레시피뿐이라 잠금에 관한 말은 하지 않는다.
        /// </summary>
        public static string RecipeLine(RecipeSO r, Inventory inv, int want, bool queueRoom)
        {
            if (r == null) return "";

            var sb = new StringBuilder();
            sb.Append(NameOf(r));
            sb.Append(Separator);

            if (r.ingredients == null || r.ingredients.Length == 0) sb.Append("재료 없음");
            else
            {
                bool first = true;
                foreach (var need in r.ingredients)
                {
                    if (need?.item == null) continue;
                    if (!first) sb.Append(", ");
                    int held = inv != null ? inv.CountOf(need.item.id) : 0;
                    sb.Append($"{need.item.displayName} {held}/{need.count * want}");
                    first = false;
                }
            }

            sb.Append(Separator).Append(CraftTimeText.Short(r.craftSeconds * want));
            if (!queueRoom) sb.Append("  (대기열이 가득 찼다)");
            return sb.ToString();
        }

        /// <summary>건축 줄 한 줄. 제작 줄과 같은 모양이되 걸리는 시간이 없다.</summary>
        public static string BuildableLine(BuildableSO b, Inventory inv)
        {
            if (b == null) return "";

            var sb = new StringBuilder();
            sb.Append(NameOf(b));
            sb.Append(Separator);

            if (b.cost == null || b.cost.Length == 0) { sb.Append("재료 없음"); return sb.ToString(); }

            bool first = true;
            foreach (var c in b.cost)
            {
                if (c?.item == null) continue;
                if (!first) sb.Append(", ");
                int held = inv != null ? inv.CountOf(c.item.id) : 0;
                sb.Append($"{c.item.displayName} {held}/{c.count}");
                first = false;
            }
            return first ? sb.Append("재료 없음").ToString() : sb.ToString();
        }

        /// <summary>
        /// 쪽지에 덧붙일 재료 한 줄. 줄에 적힌 것과 달리 가진 수량은 넣지 않는다 —
        /// 그 숫자는 이미 줄에 있고, 쪽지가 알려 줄 것은 "무엇이 드는가"다.
        /// </summary>
        public static string IngredientLine(RecipeSO r)
        {
            if (r?.ingredients == null || r.ingredients.Length == 0) return "재료 없음";

            var sb = new StringBuilder("재료").Append(Separator);
            bool first = true;
            foreach (var need in r.ingredients)
            {
                if (need?.item == null || need.count <= 0) continue;
                if (!first) sb.Append(", ");
                sb.Append(need.item.displayName).Append(' ').Append(need.count);
                first = false;
            }
            return first ? "재료 없음" : sb.ToString();
        }

        /// <summary>이름이 비면 id라도 보여 준다. 빈 줄이 뜨는 것보다는 낫다.</summary>
        public static string NameOf(RecipeSO r)
        {
            if (r == null) return "";
            if (!string.IsNullOrWhiteSpace(r.displayName)) return r.displayName;
            if (r.result?.item != null && !string.IsNullOrWhiteSpace(r.result.item.displayName))
                return r.result.item.displayName;
            return r.id ?? "";
        }

        public static string NameOf(BuildableSO b)
        {
            if (b == null) return "";
            return string.IsNullOrWhiteSpace(b.displayName) ? (b.id ?? "") : b.displayName;
        }
    }
}
