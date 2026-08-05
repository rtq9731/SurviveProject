using System.Text;
using Survive.Building;
using Survive.Crafting;
using Survive.Items;
using Survive.Localization;
using Survive.Progression;

namespace Survive.UI
{
    /// <summary>
    /// 제작·건축·연구 목록에 <b>무엇이 실리는가</b>, 그리고 실린 줄에 <b>무엇이 적히는가</b>.
    ///
    /// 모르는 것은 줄을 만들지 않는다. 회색으로 가라앉혀 남겨 두던 시절에는
    /// 잠긴 줄이 "잠항 설계 청사진이 필요하다: 낫의 핵을 연구대에서 분석하면 알게 된다"
    /// 까지 적어 주었다. 낫을 본 적도 없는 사람에게 아이템 이름과 후반 전개가
    /// 통째로 새어 나간 셈이다. 배우면 그때 줄이 생긴다 — 목록이 자라는 것 자체가
    /// 보상으로 읽히는 편이 낫다.
    ///
    /// 연구 목록도 같은 규칙 아래 있다. 다만 그쪽 열쇠는 청사진이 아니라
    /// <b>가져 본 적이 있는가</b>다(<see cref="Survive.Progression.HeldRecord"/>) —
    /// 있는지도 모르는 물체를 가져오라고 할 수는 없기 때문이다.
    ///
    /// 규칙을 여기 둔 이유는 <b>증명하기 위해서</b>다. 화면에 나갈 수 있는 문자열이
    /// 전부 이 파일에서 나오면, "그 어디에도 청사진 힌트가 없다"와 "그 어디에도
    /// 겪지 않은 재료의 이름이 없다"를 한 번에 단언할 수 있다.
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
        ///
        /// 상수가 아니라 속성인 이유는 번역 표에서 꺼내기 때문이다. 로케일이 바뀌면
        /// 다음에 읽는 쪽부터 바뀐 글자를 본다.
        /// </summary>
        public static string NothingKnownToCraft => Loc.T("UI", "craft_empty");

        /// <summary>건축 목록의 같은 자리.</summary>
        public static string NothingKnownToBuild => Loc.T("UI", "build_empty");

        /// <summary>
        /// 연구 목록의 같은 자리. 여기서도 <b>몇 개가 남았는지 세어 주지 않는다</b> —
        /// 그 숫자는 앞으로 몇 종의 생물을 더 만나게 되는지를 말한다.
        /// </summary>
        public const string NothingKnownToResearch = "아직 들여다볼 것이 없다";

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
        /// 이 연구 항목이 연구대 목록에 실리는가.
        ///
        /// <b>요구 재료 중 하나라도 가져 본 적이 있으면 실린다.</b> 전부 가져야 하는
        /// 것이 아니다 — 하나를 쥔 순간 그 생물의 존재는 이미 알려졌고, 그때부터
        /// "몇 개가 더 필요한가"는 감출 것이 아니라 알려 줄 것이 된다.
        ///
        /// 예외가 셋이다.
        /// <list type="number">
        /// <item><b>이미 밝혀낸 것은 계속 보인다.</b> 아는 것을 감출 이유는 없고,
        ///       소재를 다 태워 없앤 뒤에 도감의 답과 연구대의 답이 어긋나면 안 된다</item>
        /// <item><b>원장이 서기 전에는 막지 않는다.</b> 제작 목록과 같다
        ///       (<see cref="BlueprintGate.IsUnlocked"/>) — 실패는 개방 쪽으로</item>
        /// <item><b>요구 재료를 하나도 적지 않은 항목은 실린다.</b> 감출 이름이
        ///       없는데 감추면 영영 뜨지 않는 유령 항목이 된다</item>
        /// </list>
        /// </summary>
        public static bool ShouldList(ResearchEntrySO e, UnlockLedger ledger)
        {
            if (e == null) return false;
            if (ledger == null) return true;
            if (ResearchService.IsKnown(e, ledger)) return true;

            bool declaresAny = false;
            if (e.materials != null)
            {
                foreach (var need in e.materials)
                {
                    if (need?.item == null || need.count <= 0) continue;
                    declaresAny = true;
                    if (HeldRecord.Has(ledger, need.item.id)) return true;
                }
            }
            return !declaresAny;
        }

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

            if (r.ingredients == null || r.ingredients.Length == 0) sb.Append(Loc.T("UI", "no_ingredients"));
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
            // 앞의 두 칸은 배치이지 번역할 것이 아니라 코드에 남긴다.
            if (!queueRoom) sb.Append("  ").Append(Loc.T("UI", "queue_full"));
            return sb.ToString();
        }

        /// <summary>건축 줄 한 줄. 제작 줄과 같은 모양이되 걸리는 시간이 없다.</summary>
        public static string BuildableLine(BuildableSO b, Inventory inv)
        {
            if (b == null) return "";

            var sb = new StringBuilder();
            sb.Append(NameOf(b));
            sb.Append(Separator);

            if (b.cost == null || b.cost.Length == 0)
            {
                sb.Append(Loc.T("UI", "no_ingredients"));
                return sb.ToString();
            }

            bool first = true;
            foreach (var c in b.cost)
            {
                if (c?.item == null) continue;
                if (!first) sb.Append(", ");
                int held = inv != null ? inv.CountOf(c.item.id) : 0;
                sb.Append($"{c.item.displayName} {held}/{c.count}");
                first = false;
            }
            return first ? sb.Append(Loc.T("UI", "no_ingredients")).ToString() : sb.ToString();
        }

        /// <summary>
        /// 쪽지에 덧붙일 재료 한 줄. 줄에 적힌 것과 달리 가진 수량은 넣지 않는다 —
        /// 그 숫자는 이미 줄에 있고, 쪽지가 알려 줄 것은 "무엇이 드는가"다.
        /// </summary>
        public static string IngredientLine(RecipeSO r)
        {
            if (r?.ingredients == null || r.ingredients.Length == 0) return Loc.T("UI", "no_ingredients");

            var sb = new StringBuilder(Loc.T("UI", "ingredients_label")).Append(Separator);
            bool first = true;
            foreach (var need in r.ingredients)
            {
                if (need?.item == null || need.count <= 0) continue;
                if (!first) sb.Append(", ");
                sb.Append(need.item.displayName).Append(' ').Append(need.count);
                first = false;
            }
            return first ? Loc.T("UI", "no_ingredients") : sb.ToString();
        }

        // ── 연구대에 적히는 것 ───────────────────────────────────
        //
        // 이 셋이 연구대 화면이 낼 수 있는 문자열 전부다. 화면(CraftingUI)이 직접
        // 문장을 짓던 것을 여기로 옮긴 이유는 제작 줄과 같다 — 한 파일에 모여 있어야
        // "초기 화면 어디에도 겪지 않은 재료의 이름이 없다"를 실제 에셋으로 단언할 수 있다.

        /// <summary>
        /// 아직 걸지 않은 연구 줄 한 줄. 이름, 소재 가진 수/드는 수, 태울 것, 걸리는 시간.
        /// 여기 오는 것은 <see cref="ShouldList(ResearchEntrySO, UnlockLedger)"/>를
        /// 통과한 항목뿐이라, 겪어 본 적 없는 재료의 이름이 적힐 일이 없다.
        /// </summary>
        public static string ResearchLine(ResearchEntrySO e, Inventory inv, ItemDataSO energy,
                                          ResearchReadiness state)
        {
            if (e == null) return "";

            var sb = new StringBuilder(NameOf(e));

            // 이미 밝혀낸 줄에는 소재를 적지 않는다. 다시 걸 수 없는 항목에
            // "0/3"을 붙여 두면 아직 모을 것이 남은 것처럼 읽힌다.
            if (state == ResearchReadiness.AlreadyKnown)
                return sb.Append(Separator).Append(ResearchService.Describe(state)).ToString();

            sb.Append(Separator);
            if (e.materials != null)
            {
                foreach (var need in e.materials)
                {
                    if (need?.item == null || need.count <= 0) continue;
                    int held = inv != null ? inv.CountOf(need.item.id) : 0;
                    sb.Append($"{need.item.displayName} {held}/{need.count}, ");
                }
            }

            string energyName = energy != null ? energy.displayName : DefaultEnergyName;
            int heldEnergy = inv != null ? inv.CountOf(ResearchService.EnergyIdOf(energy)) : 0;
            sb.Append($"{energyName} {heldEnergy}/{e.energyCost}");

            sb.Append(Separator).Append(CraftTimeText.Short(e.researchSeconds));
            if (state != ResearchReadiness.Ready)
                sb.Append($"  ({ResearchService.Describe(state)})");
            return sb.ToString();
        }

        /// <summary>
        /// 태울 것을 에셋이 지정하지 않았을 때 줄에 적는 이름.
        /// 첫 일 분에 손에 들어오는 물건이라 감출 것이 없다.
        /// </summary>
        const string DefaultEnergyName = "스크랩";

        /// <summary>줄에 서 있는 연구 항목 — 몇 번째인지와 얼마나 남았는지.</summary>
        public static string QueuedResearchLine(ResearchEntrySO e, ResearchQueue queue, int index)
        {
            var job = queue?.At(index);
            if (e == null || job == null) return "";

            string name = NameOf(e);
            if (index == 0)
                return $"{name}{Separator}분석 중 {job.Progress:P0}{Separator}남은 " +
                       $"{CraftTimeText.Short(job.SecondsLeft)}  (누르면 물린다)";

            return $"{name}{Separator}대기 {index + 1}번째{Separator}" +
                   $"{CraftTimeText.Short(job.SecondsLeft)}  (누르면 물린다)";
        }

        /// <summary>연구대 목록 맨 위의 띠. 무엇이 몇 개 걸려 있고 얼마나 남았는지.</summary>
        public static string ResearchHeaderLine(string stationName, ResearchQueue queue)
        {
            string name = string.IsNullOrWhiteSpace(stationName) ? "연구대" : stationName;
            if (queue == null || queue.IsEmpty) return $"{name}{Separator}분석 대기열 비어 있음";

            return $"{name}{Separator}분석 대기열 {queue.Count}/{queue.Capacity}" +
                   $"{Separator}남은 {CraftTimeText.Short(ResearchService.TotalSecondsLeft(queue))}";
        }

        /// <summary>이름이 비면 id라도 보여 준다. 빈 줄이 뜨는 것보다는 낫다.</summary>
        public static string NameOf(ResearchEntrySO e)
        {
            if (e == null) return "";
            return string.IsNullOrWhiteSpace(e.displayName) ? (e.id ?? "") : e.displayName;
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
