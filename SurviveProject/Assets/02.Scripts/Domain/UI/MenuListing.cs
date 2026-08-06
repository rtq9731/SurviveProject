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
    /// 잠긴 줄이 "돌파 설계 청사진이 필요하다: 낫의 핵을 연구대에서 분석하면 알게 된다"
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
    /// <b>문장은 조각으로 짓지 않는다.</b> 한 줄에 보이는 문장은 표의 한 칸에 통째로
    /// 들어가고, 그 안의 <c>{0} {1} {2}</c>가 값이 들어갈 자리를 표시한다.
    /// 여기서 넘기는 것은 <b>값뿐</b>이다 — 이름, 개수, 서식된 시간.
    /// 가운뎃점 구분자도 "  ·  "라고 코드에 박지 않는다. 그것은 배치가 아니라
    /// <b>문장의 일부</b>이고, 언어에 따라 쉼표가 낫거나 줄바꿈이 나을 수 있다.
    ///
    /// 유일한 예외가 <b>되풀이</b>다. 재료가 세 종이면 같은 꼴이 세 번 반복되는데
    /// 그것만은 통짜로 만들 수 없다. 그래서 항목 하나의 틀(<c>ingredient_entry</c>)과
    /// 항목 사이 구분자(<c>list_separator</c>)를 표의 키로 두고, 이어 붙인 결과를
    /// 바깥 문장에 <c>{n}</c>으로 꽂는다. 자세한 것은 docs/번역-체계.md.
    ///
    /// 화면에 나가는 문자열이므로 줄표(U+2014)를 쓰지 않는다 — 본문 글꼴(ChosunGu)에
    /// 없어서 네모(□)로 찍힌다. 가운뎃점을 쓴다.
    /// </summary>
    public static class MenuListing
    {
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
        public static string NothingKnownToResearch => Loc.T("UI", "research_empty");

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
        ///
        /// 대기열이 찼을 때를 <b>다른 키</b>로 두는 이유는 두 가지다. 뒤에 조각을
        /// 덧붙이지 않아도 되고, 언어에 따라 그 말을 문장 앞에 놓을 수도 있다.
        /// 삼항 연산자로 키를 고르지 않는 것도 이유가 있다 — 키가 변수가 되면
        /// 누락 키 게이트가 그 자리를 검사할 수 없다.
        /// </summary>
        public static string RecipeLine(RecipeSO r, Inventory inv, int want, bool queueRoom)
        {
            if (r == null) return "";

            string ingredients = HeldList(r.ingredients, inv, want);
            string time = CraftTimeText.Short(r.craftSeconds * want);

            return queueRoom
                ? Loc.F("UI", "recipe_line", NameOf(r), ingredients, time)
                : Loc.F("UI", "recipe_line_queue_full", NameOf(r), ingredients, time);
        }

        /// <summary>건축 줄 한 줄. 제작 줄과 같은 모양이되 걸리는 시간이 없다.</summary>
        public static string BuildableLine(BuildableSO b, Inventory inv)
        {
            if (b == null) return "";
            return Loc.F("UI", "buildable_line", NameOf(b), HeldList(b.cost, inv, 1));
        }

        /// <summary>
        /// 쪽지에 덧붙일 재료 한 줄. 줄에 적힌 것과 달리 가진 수량은 넣지 않는다 —
        /// 그 숫자는 이미 줄에 있고, 쪽지가 알려 줄 것은 "무엇이 드는가"다.
        /// </summary>
        public static string IngredientLine(RecipeSO r)
        {
            if (r == null) return Loc.T("UI", "no_ingredients");

            string needs = NeedList(r.ingredients);
            if (needs == null) return Loc.T("UI", "no_ingredients");

            return Loc.F("UI", "ingredients_line", NameOf(r), needs);
        }

        // ── 되풀이 (규칙의 유일한 예외) ──────────────────────────
        //
        // 재료가 세 종이면 같은 꼴이 세 번 반복된다. 이것만은 통짜 문장으로 만들 수
        // 없어서 항목 틀과 구분자를 표에서 꺼내 이어 붙인다. 이 예외를 넓혀 읽지 마라 —
        // 여기서 이어 붙이는 것은 전부 표에서 나온 것이고, 코드에 적은 말은 하나도 없다.

        /// <summary>"이름 가진수/드는수"를 구분자로 이은 목록. 하나도 없으면 "재료 없음".</summary>
        static string HeldList(ItemStack[] stacks, Inventory inv, int want)
        {
            if (stacks == null || stacks.Length == 0) return Loc.T("UI", "no_ingredients");

            var sb = new StringBuilder();
            string separator = Loc.T("UI", "list_separator");
            bool first = true;

            foreach (var need in stacks)
            {
                if (need?.item == null) continue;
                if (!first) sb.Append(separator);

                int held = inv != null ? inv.CountOf(need.item.id) : 0;
                sb.Append(Loc.F("UI", "ingredient_entry",
                                DataText.Name(need.item), held, need.count * want));
                first = false;
            }

            return first ? Loc.T("UI", "no_ingredients") : sb.ToString();
        }

        /// <summary>"이름 드는수"만 이은 목록. 실을 것이 하나도 없으면 null.</summary>
        static string NeedList(ItemStack[] stacks)
        {
            if (stacks == null || stacks.Length == 0) return null;

            var sb = new StringBuilder();
            string separator = Loc.T("UI", "list_separator");
            bool first = true;

            foreach (var need in stacks)
            {
                if (need?.item == null || need.count <= 0) continue;
                if (!first) sb.Append(separator);
                sb.Append(Loc.F("UI", "ingredient_need", DataText.Name(need.item), need.count));
                first = false;
            }

            return first ? null : sb.ToString();
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

            // 이미 밝혀낸 줄에는 소재를 적지 않는다. 다시 걸 수 없는 항목에
            // "0/3"을 붙여 두면 아직 모을 것이 남은 것처럼 읽힌다.
            if (state == ResearchReadiness.AlreadyKnown)
                return Loc.F("UI", "research_line_known", NameOf(e), ReadinessText(state));

            string materials = ResearchCostList(e, inv, energy);
            string time = CraftTimeText.Short(e.researchSeconds);

            return state == ResearchReadiness.Ready
                ? Loc.F("UI", "research_line", NameOf(e), materials, time)
                : Loc.F("UI", "research_line_state", NameOf(e), materials, time, ReadinessText(state));
        }

        /// <summary>소재 목록 뒤에 태울 것을 한 항목으로 덧붙인다. 전부 같은 틀을 쓴다.</summary>
        static string ResearchCostList(ResearchEntrySO e, Inventory inv, ItemDataSO energy)
        {
            var sb = new StringBuilder();
            string separator = Loc.T("UI", "list_separator");

            if (e.materials != null)
            {
                foreach (var need in e.materials)
                {
                    if (need?.item == null || need.count <= 0) continue;
                    int held = inv != null ? inv.CountOf(need.item.id) : 0;
                    sb.Append(Loc.F("UI", "ingredient_entry",
                                    DataText.Name(need.item), held, need.count));
                    sb.Append(separator);
                }
            }

            string energyLabel = DataText.Name(energy);
            string energyName = string.IsNullOrWhiteSpace(energyLabel)
                ? Loc.T("UI", "research_energy_default")
                : energyLabel;
            int heldEnergy = inv != null ? inv.CountOf(ResearchService.EnergyIdOf(energy)) : 0;

            sb.Append(Loc.F("UI", "ingredient_entry", energyName, heldEnergy, e.energyCost));
            return sb.ToString();
        }

        /// <summary>
        /// 연구가 지금 어떤 상태인지 한 마디로. <see cref="ResearchService.Describe"/>와
        /// 같은 것을 말하되 <b>표에서 꺼낸다</b> — 그쪽은 규칙을 담은 자리라
        /// 화면 문구가 섞이면 번역이 규칙 파일까지 밀고 들어간다.
        /// </summary>
        public static string ReadinessText(ResearchReadiness state)
        {
            switch (state)
            {
                case ResearchReadiness.Ready:           return Loc.T("UI", "research_state_ready");
                case ResearchReadiness.AlreadyKnown:    return Loc.T("UI", "research_state_known");
                case ResearchReadiness.Queued:          return Loc.T("UI", "research_state_queued");
                case ResearchReadiness.QueueFull:       return Loc.T("UI", "research_state_queue_full");
                case ResearchReadiness.MissingMaterial: return Loc.T("UI", "research_state_no_material");
                case ResearchReadiness.MissingEnergy:   return Loc.T("UI", "research_state_no_energy");
                default:                                return Loc.T("UI", "research_state_invalid");
            }
        }

        /// <summary>줄에 서 있는 연구 항목 — 몇 번째인지와 얼마나 남았는지.</summary>
        public static string QueuedResearchLine(ResearchEntrySO e, ResearchQueue queue, int index)
        {
            var job = queue?.At(index);
            if (e == null || job == null) return "";

            string left = CraftTimeText.Short(job.SecondsLeft);

            // 진행률은 여기서 서식해 둔다. 서식된 값은 값이지 말이 아니다.
            // 지역 변수로 빼는 것은 게이트 때문이기도 하다 — 호출 인자 자리에
            // 문자열 리터럴("P0")이 보이면 그것이 서식 지정자인지 말인지
            // 정적 분석으로는 가릴 수 없고, 가리려 들면 규칙에 구멍이 난다.
            string progress = job.Progress.ToString("P0");

            return index == 0
                ? Loc.F("UI", "research_queued_active", NameOf(e), progress, left)
                : Loc.F("UI", "research_queued_waiting", NameOf(e), index + 1, left);
        }

        /// <summary>연구대 목록 맨 위의 띠. 무엇이 몇 개 걸려 있고 얼마나 남았는지.</summary>
        public static string ResearchHeaderLine(string stationName, ResearchQueue queue)
        {
            string name = string.IsNullOrWhiteSpace(stationName)
                ? Loc.T("UI", "research_station_default")
                : stationName;

            if (queue == null || queue.IsEmpty)
                return Loc.F("UI", "research_header_empty", name);

            return Loc.F("UI", "research_header", name, queue.Count, queue.Capacity,
                         CraftTimeText.Short(ResearchService.TotalSecondsLeft(queue)));
        }

        // 아래 이름들은 전부 <see cref="DataText"/>를 거친다. 에셋의 displayName을
        // 여기서 직접 읽으면 이 목록만 로케일을 안 따라오고, 그 구멍은 이 화면을
        // 그 로케일로 열어 보기 전까지 아무 신호도 내지 않는다.
        // 표에 키가 없으면 DataText가 에셋 원문을 그대로 돌려주므로 옛 동작 그대로다.

        /// <summary>이름이 비면 id라도 보여 준다. 빈 줄이 뜨는 것보다는 낫다.</summary>
        public static string NameOf(ResearchEntrySO e)
        {
            if (e == null) return "";
            string name = DataText.Name(e);
            return string.IsNullOrWhiteSpace(name) ? (e.id ?? "") : name;
        }

        /// <summary>이름이 비면 id라도 보여 준다. 빈 줄이 뜨는 것보다는 낫다.</summary>
        public static string NameOf(RecipeSO r)
        {
            if (r == null) return "";

            string name = DataText.Name(r);
            if (!string.IsNullOrWhiteSpace(name)) return name;

            string resultName = DataText.Name(r.result?.item);
            if (!string.IsNullOrWhiteSpace(resultName)) return resultName;

            return r.id ?? "";
        }

        public static string NameOf(BuildableSO b)
        {
            if (b == null) return "";
            string name = DataText.Name(b);
            return string.IsNullOrWhiteSpace(name) ? (b.id ?? "") : name;
        }
    }
}
