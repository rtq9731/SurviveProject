using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Survive.Creatures;
using Survive.Localization;

namespace Survive.Progression
{
    /// <summary>도감의 갈래. 무엇을 알아낸 기록인가로 나눈다.</summary>
    public enum CodexSection
    {
        /// <summary>손에 쥐고 그 자리에서 읽어낸 것 (해금 채널 1).</summary>
        Discovery = 0,

        /// <summary>알아낸 결과로 열린 만드는 법.</summary>
        Blueprint = 1,

        /// <summary>연구대에서 시간과 전력을 들여 밝혀낸 것 (해금 채널 2).</summary>
        Research = 2,

        /// <summary>마주친 것들.</summary>
        Creature = 3,
    }

    /// <summary>도감 한 줄. 화면은 이것만 받아 그린다.</summary>
    public sealed class CodexEntry
    {
        public CodexSection Section;

        /// <summary>원장 열쇠(또는 항목 id). 줄 오브젝트 이름에도 쓰인다.</summary>
        public string Key;

        /// <summary>잠겨 있으면 <see cref="CodexCatalog.UnknownTitle"/>.</summary>
        public string Title;

        /// <summary>열려 있으면 AI가 그때 한 말, 잠겨 있으면 여는 방법 또는 미확인 안내.</summary>
        public string Body;

        public bool Unlocked;
    }

    /// <summary>
    /// 도감 목록을 짓는 규칙 (백로그 43).
    ///
    /// <b>새 저장 항목을 만들지 않는다.</b> 무엇을 아는가는 이미 <see cref="UnlockLedger"/>
    /// 하나에 전부 적혀 있다. 도감은 그 원장을 다르게 읽는 화면일 뿐이고, 여기 있는 것은
    /// "원장 + 에셋 목록 → 화면에 뜰 줄들"이라는 순수 변환이다. 세는 자리가 둘이면
    /// 언젠가 둘이 어긋난다는 <see cref="FieldDiscovery"/>·<see cref="ResearchService"/>의
    /// 규율을 그대로 따른다.
    ///
    /// <b>모르는 것을 목록에서 지우지 않는다.</b> 제작 목록이 잠긴 줄을 실루엣으로
    /// 남겨 두는 것과 같은 이유다 — 무엇이 있는지는 보여야 찾아 나설 수 있다.
    /// 다만 도감은 이름조차 알려주지 않는다. 이름을 아는 것이 곧 아는 것이기 때문이다.
    ///
    /// Unity 실행 없이 테스트한다.
    /// </summary>
    public static class CodexCatalog
    {
        /// <summary>
        /// 생물 관측 기록의 열쇠 접두. <c>codex_&lt;CreatureDefinition.id&gt;</c>가 계약이며,
        /// 이 열쇠를 <b>여는</b> 쪽(관측·처치 경로)과 <b>읽는</b> 쪽(이 화면)이 서로를
        /// 모른 채 같은 원장으로만 만난다.
        /// </summary>
        public const string CreatureKeyPrefix = "codex_";

        /// <summary>
        /// 잠긴 항목의 이름 자리. 실루엣만 남긴다.
        /// 글자가 아니라 기호라서 번역할 것이 없다 — 그래서 여기만 <c>const</c>다.
        /// </summary>
        public const string UnknownTitle = "???";

        // 아래 넷은 화면에 그대로 나가는 글이라 표에서 꺼낸다.
        // const이면 컴파일할 때 값이 박혀 로케일을 영영 따라오지 못한다.
        public static string UnknownDiscoveryBody => Loc.T("Codex", "unknown_discovery");
        public static string UnknownBlueprintBody => Loc.T("Codex", "unknown_blueprint");
        public static string UnknownResearchBody => Loc.T("Codex", "unknown_research");
        public static string UnknownCreatureBody => Loc.T("Codex", "unknown_creature");

        /// <summary>생물 하나의 관측 기록 열쇠. id가 없으면 null.</summary>
        public static string CreatureKey(string creatureId) =>
            string.IsNullOrWhiteSpace(creatureId) ? null : CreatureKeyPrefix + creatureId;

        public static string CreatureKey(CreatureDefinitionSO definition) =>
            definition == null ? null : CreatureKey(definition.id);

        /// <summary>갈래 이름. 탭에 적힌다.</summary>
        public static string SectionTitle(CodexSection section)
        {
            switch (section)
            {
                case CodexSection.Discovery: return Loc.T("Codex", "section_discovery");
                case CodexSection.Blueprint: return Loc.T("Codex", "section_blueprint");
                case CodexSection.Research:  return Loc.T("Codex", "section_research");
                case CodexSection.Creature:  return Loc.T("Codex", "section_creature");
                default:                     return Loc.T("Codex", "section_other");
            }
        }

        /// <summary>열린 줄의 수. 탭에 "3/5"로 적는다.</summary>
        public static int CountUnlocked(IReadOnlyList<CodexEntry> entries)
        {
            if (entries == null) return 0;

            int n = 0;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && entries[i].Unlocked) n++;
            return n;
        }

        // ── 물질 분석 ────────────────────────────────────────────────

        /// <summary>
        /// 현장에서 읽어낸 것들. 설명문은 <b>그때 AI가 한 말 그대로</b>다 —
        /// 도감용 문장을 따로 쓰면 같은 물질을 두 목소리가 설명하게 된다.
        /// </summary>
        public static void BuildDiscoveries(DiscoveryBookSO book, UnlockLedger ledger,
                                            List<CodexEntry> into)
        {
            if (into == null) return;
            into.Clear();
            if (book?.discoveries == null) return;

            foreach (var d in book.discoveries)
            {
                var key = FieldDiscovery.KeyOf(d);
                if (key == null) continue;

                bool known = ledger != null && ledger.IsUnlocked(key);
                into.Add(new CodexEntry
                {
                    Section = CodexSection.Discovery,
                    Key = key,
                    Unlocked = known,
                    Title = known ? NameOf(d) : UnknownTitle,
                    Body = known ? TextOf(d) : UnknownDiscoveryBody,
                });
            }
        }

        // 아래 이름·문장은 전부 DataText를 거친다. 도감은 아는 것을 <b>말로</b> 보여 주는
        // 화면이라, 여기서 SO를 직접 읽으면 게임의 절반이 로케일을 안 따라온다.

        static string NameOf(DiscoverySO d)
        {
            if (d.item == null) return d.id;
            var name = DataText.Name(d.item);
            return string.IsNullOrWhiteSpace(name) ? d.item.id : name;
        }

        static string TextOf(DiscoverySO d) => d.line == null ? "" : DataText.Line(d);

        // ── 확보 제작법 ──────────────────────────────────────────────

        /// <summary>
        /// 청사진에는 목록 에셋이 없다. 있을 필요도 없다 — 청사진은 언제나 무언가가
        /// <b>열어 주는</b> 것이라, 두 해금 채널을 훑으면 세상의 청사진이 전부 나온다.
        /// 목록을 따로 두면 어느 채널도 열어 주지 않는 유령 청사진이 도감에만 뜰 수 있다.
        ///
        /// 열린 줄에는 <b>어느 경로로 알게 됐는지</b>를 적는다. 주운 것과 밝혀낸 것은
        /// 같은 문장으로 말할 수 없다는 규칙(<see cref="ResearchEntrySO"/>)의 연장이다.
        /// </summary>
        public static void BuildBlueprints(DiscoveryBookSO discoveries, ResearchBookSO research,
                                           UnlockLedger ledger, List<CodexEntry> into)
        {
            if (into == null) return;
            into.Clear();

            var seen = new HashSet<string>();

            if (discoveries?.discoveries != null)
            {
                foreach (var d in discoveries.discoveries)
                {
                    if (d?.unlocks == null) continue;
                    foreach (var bp in d.unlocks)
                        Add(bp, Loc.F("Codex", "source_field", NameOf(d)));
                }
            }

            if (research?.entries != null)
            {
                foreach (var e in research.entries)
                {
                    if (e?.unlocks == null) continue;
                    foreach (var bp in e.unlocks)
                        Add(bp, Loc.F("Codex", "source_research", NameOf(e)));
                }
            }

            void Add(BlueprintSO bp, string source)
            {
                if (bp == null || string.IsNullOrWhiteSpace(bp.id)) return;
                if (!seen.Add(bp.id)) return;

                bool known = ledger != null && ledger.IsUnlocked(bp.id);
                into.Add(new CodexEntry
                {
                    Section = CodexSection.Blueprint,
                    Key = bp.id,
                    Unlocked = known,
                    Title = known ? NameOf(bp) : UnknownTitle,
                    Body = known
                        ? Loc.F("Codex", "acquired", source)
                        : HintOf(bp),
                });
            }
        }

        // ── 정밀 분석 ────────────────────────────────────────────────

        /// <summary>
        /// 연구 항목에는 자기 열쇠가 없다. 끝냈는지는 그것이 연 청사진이 원장에
        /// 있는지로만 판단한다(<see cref="ResearchService.IsKnown"/>) — 도감이
        /// 자기 기준을 새로 세우면 화면과 연구대가 서로 다른 답을 하게 된다.
        /// </summary>
        public static void BuildResearch(ResearchBookSO book, UnlockLedger ledger,
                                         List<CodexEntry> into)
        {
            if (into == null) return;
            into.Clear();
            if (book?.entries == null) return;

            foreach (var e in book.entries)
            {
                if (e == null || string.IsNullOrWhiteSpace(e.id)) continue;

                bool known = ResearchService.IsKnown(e, ledger);
                into.Add(new CodexEntry
                {
                    Section = CodexSection.Research,
                    Key = e.id,
                    Unlocked = known,
                    Title = known ? NameOf(e) : UnknownTitle,
                    Body = known ? TextOf(e) : UnknownResearchBody,
                });
            }
        }

        static string NameOf(BlueprintSO bp)
        {
            var name = DataText.Name(bp);
            return string.IsNullOrWhiteSpace(name) ? bp.id : name;
        }

        static string HintOf(BlueprintSO bp)
        {
            var hint = DataText.Hint(bp);
            return string.IsNullOrWhiteSpace(hint) ? UnknownBlueprintBody : hint;
        }

        static string NameOf(ResearchEntrySO e)
        {
            var name = DataText.Name(e);
            return string.IsNullOrWhiteSpace(name) ? e.id : name;
        }

        static string TextOf(ResearchEntrySO e) => e.line == null ? "" : DataText.Line(e);

        // ── 개체 관측 ────────────────────────────────────────────────

        /// <summary>
        /// 마주친 것들. 설명문도 수치도 <see cref="CreatureDefinitionSO"/>가 이미
        /// 들고 있던 것을 그대로 읽는다 — 도감을 위해 서사를 새로 쓰지 않는다.
        /// </summary>
        public static void BuildCreatures(CreatureBookSO book, UnlockLedger ledger,
                                          List<CodexEntry> into)
        {
            if (into == null) return;
            into.Clear();
            if (book?.creatures == null) return;

            foreach (var c in book.creatures)
            {
                var key = CreatureKey(c);
                if (key == null) continue;

                bool known = ledger != null && ledger.IsUnlocked(key);
                into.Add(new CodexEntry
                {
                    Section = CodexSection.Creature,
                    Key = key,
                    Unlocked = known,
                    Title = known ? NameOf(c) : UnknownTitle,
                    Body = known ? DescribeCreature(c) : UnknownCreatureBody,
                });
            }
        }

        /// <summary>수치를 적는 꼴. 인자 자리에 두면 게이트가 이것을 말로 오해한다.</summary>
        const string NumberFormat = "0.#";

        /// <summary>
        /// 생물 한 마리를 관측 보고 한 편으로 적는다.
        /// 관측된 성질(설명문)이 먼저 오고, 잰 값이 뒤따른다.
        ///
        /// <b>계층을 적지 않는다 (기획서 §4.7).</b> 예전에는 이 보고의 첫 칸이
        /// 영양 단계(분해자·생산자·1차 소비자…)였다. 그것을 뺀 이유는 셋이다.
        /// <list type="number">
        /// <item>계층을 도감이 알려 주지 않아야 <b>"다리 개수 = 포식 차수"</b> 같은
        ///       규칙을 플레이어가 관찰로 발견한다. 생태계를 읽으면 이득을 본다는
        ///       차별화 축이 필드에서만이 아니라 열람 화면에서도 성립한다.</item>
        /// <item>낫이 규칙 밖 존재라는 사실이 <b>분류표의 예외가 아니라 그냥 알 수
        ///       없는 것</b>으로 처리된다. 분류표를 두고 거기에 "생태계 밖" 칸을
        ///       새로 만들면 그것 자체가 답을 알려 주는 것이 된다.</item>
        /// <item>「모르는 것은 보여 주지 않는다」(§5.12)와 같은 문법이다. 라벨을
        ///       모르는 분석기가 계층표를 갖고 있을 리 없다.</item>
        /// </list>
        /// 그래서 이 화면에 실릴 수 있는 것은 <b>눈으로 본 것</b>까지다 —
        /// 어떻게 움직였는가(이동 방식), 어떻게 굴었는가(성향), 그리고 잰 값.
        /// 무엇인지는 판정하지 않는다.
        ///
        /// <b><see cref="CreatureDefinitionSO.tier"/>는 그대로 둔다.</b> 보여 주지
        /// 않는 것과 없는 것은 다르다 — 그 값은 여전히 포식 차수 규칙과 연구·드롭이
        /// 읽는다. 화면에서만 사라진다.
        ///
        /// <b>줄바꿈은 코드가 넣는다.</b> 보고는 눈에 보이는 줄마다 표의 한 칸이고,
        /// 줄과 줄을 잇는 것은 말이 아니라 배치다. 표의 한 칸 안에 줄바꿈을 넣지
        /// 않는 이유는 <c>strings.csv</c>가 CRLF라서 그 값에 <c>\r</c>가 딸려
        /// 들어가고, TMP가 그것을 빈 줄 하나로 더 세기 때문이다.
        /// </summary>
        public static string DescribeCreature(CreatureDefinitionSO c)
        {
            if (c == null) return "";

            var culture = CultureInfo.InvariantCulture;
            string health = c.maxHealth.ToString(NumberFormat, culture);
            string speed = c.moveSpeed.ToString(NumberFormat, culture);
            string detect = c.detectRadius.ToString(NumberFormat, culture);
            string damage = c.attackDamage.ToString(NumberFormat, culture);

            var report = new StringBuilder();

            var described = DataText.Codex(c);
            if (!string.IsNullOrWhiteSpace(described))
                report.Append(described).Append('\n').Append('\n');

            // 자리가 둘뿐인 새 열쇠를 쓴다. 옛 열쇠(세 칸)를 그대로 두고 첫 칸에
            // 빈 문자열을 넘기는 길도 있었지만, 그러면 화면에 "· 지상 · 방어"처럼
            // 앞이 빈 줄이 서고 무엇보다 <b>첫 칸이 아직 거기 있다</b> — 다음 사람이
            // 계층을 도로 끼워 넣기까지 한 줄이면 된다. 자리를 없애야 못 되돌린다.
            report.Append(Loc.F("Codex", "creature_observed",
                                LocomotionName(c.locomotion), BehaviorName(c.behavior)));
            report.Append('\n');
            report.Append(Loc.F("Codex", "creature_stats", health, speed, detect, damage));

            if (c.avoidsLight) report.Append('\n').Append(Loc.T("Codex", "creature_shy"));

            return report.ToString();
        }

        static string NameOf(CreatureDefinitionSO c)
        {
            var name = DataText.Name(c);
            return string.IsNullOrWhiteSpace(name) ? c.id : name;
        }

        // 여기에 TierName은 없다. 일부러 없다 — 기획서 §4.7.
        // 영양 단계를 말로 옮기는 함수가 하나라도 있으면 그것을 부르는 화면이
        // 언젠가 다시 생긴다. 도감이 계층을 모르게 하려면 <b>옮길 방법 자체가
        // 없어야</b> 한다. 계층이 필요한 규칙(포식 차수·연구·드롭)은 말이 아니라
        // <see cref="TrophicTier"/> 값을 그대로 쓴다.

        /// <summary>
        /// 어떻게 움직였는가. <b>계층이 아니라 관측이다</b> — 걷는 것과 미끄러지는
        /// 것은 실루엣이 보이지 않는 거리에서도 눈으로 갈린다. 그래서 계층을 걷어낸
        /// 뒤에도 이 줄은 남는다.
        /// </summary>
        public static string LocomotionName(LocomotionType type)
        {
            switch (type)
            {
                case LocomotionType.Flying:   return Loc.T("Codex", "locomotion_flying");
                case LocomotionType.Hovering: return Loc.T("Codex", "locomotion_hovering");
                default:                      return Loc.T("Codex", "locomotion_ground");
            }
        }

        public static string BehaviorName(BehaviorProfile profile)
        {
            switch (profile)
            {
                case BehaviorProfile.Passive:   return Loc.T("Codex", "behavior_passive");
                case BehaviorProfile.Skittish:  return Loc.T("Codex", "behavior_skittish");
                case BehaviorProfile.Defensive: return Loc.T("Codex", "behavior_defensive");
                case BehaviorProfile.Aggressive: return Loc.T("Codex", "behavior_aggressive");
                default:                        return Loc.T("Codex", "behavior_unknown");
            }
        }
    }
}
