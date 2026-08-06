using System.Text;
using Survive.Building;
using Survive.Core;
using Survive.Crafting;
using Survive.Creatures;
using Survive.Harvesting;
using Survive.Items;
using Survive.Narrative;
using Survive.Progression;
using Survive.Vitals;

namespace Survive.Localization
{
    /// <summary>
    /// 데이터 에셋(ScriptableObject)에 적힌 한국어를 번역 표로 갈아 끼우는 <b>단 하나의 문</b>.
    ///
    /// <b>왜 필드를 지우지 않았는가.</b> <c>ItemDataSO.displayName</c>은 그대로 남는다.
    /// 셋을 한꺼번에 얻기 때문이다 — ① 이관 도중 아무것도 깨지지 않고,
    /// ② 표가 없거나 망가져도 게임은 한국어로 돌아가고,
    /// ③ 인스펙터에서 그 에셋이 무엇인지 여전히 읽힌다.
    /// 표는 <b>덮어쓰는 층</b>이지 옮겨 담는 그릇이 아니다.
    ///
    /// <b>조회 순서.</b> <c>Item/{id}.name</c>이 표에 있으면 그것, 없으면 에셋의 원문.
    /// 마지막에 키를 내주는 <see cref="Loc.T(LocKey)"/>와 다르다 — 여기서는
    /// 원문이 언제나 키보다 나은 답이다.
    ///
    /// <b>왜 한 문으로 모으는가.</b> 한 군데라도 SO를 직접 읽으면 그 화면만
    /// 번역이 안 된다. 그런 구멍은 그 화면을 그 로케일로 열어 보기 전까지
    /// 아무 신호도 내지 않는다. 그래서 읽는 자리를 여기 하나로 모으고,
    /// 우회하는 자리가 생기면 EditMode 게이트(<c>DataTextGateTests</c>)가 잡는다.
    ///
    /// 키 규약과 카테고리 목록은 <c>docs/번역-체계.md</c> 8절.
    /// </summary>
    public static class DataText
    {
        /// <summary>
        /// 에셋 종류별 카테고리. 나중에 Unity Localization으로 갈아탈 때
        /// String Table Collection 하나씩이 된다.
        /// </summary>
        public static class Category
        {
            public const string Item = "Item";
            public const string Buildable = "Buildable";
            public const string Recipe = "Recipe";
            public const string Blueprint = "Blueprint";
            public const string Research = "Research";
            public const string Discovery = "Discovery";
            public const string Creature = "Creature";
            public const string Harvest = "Harvest";
            public const string Plant = "Plant";
            public const string Vital = "Vital";
            public const string Scene = "Scene";
            public const string Chapter = "Chapter";
            public const string Objective = "Objective";
            public const string Sequence = "Sequence";
        }

        /// <summary>필드 접미어. 키는 언제나 <c>{id}{접미어}</c>다.</summary>
        public static class Suffix
        {
            public const string Name = ".name";
            public const string Desc = ".desc";
            public const string Hint = ".hint";
            public const string Codex = ".codex";
            public const string Title = ".title";
            public const string Text = ".text";
            public const string Speaker = ".speaker";

            /// <summary>대사가 하나뿐인 에셋(발견·연구)의 화자.</summary>
            public const string LineSpeaker = ".line" + Speaker;

            /// <summary>대사가 하나뿐인 에셋(발견·연구)의 대사.</summary>
            public const string LineText = ".line" + Text;
        }

        // ── 키 ──────────────────────────────────────────────────
        //
        // 초안 생성 도구와 게이트가 런타임 조회와 <b>똑같은 함수</b>로 키를 만든다.
        // 키를 두 군데서 지으면 언젠가 한 글자가 어긋나고, 그때 게이트는
        // "없는 키"를 못 잡는 대신 있지도 않은 키를 요구하게 된다.

        public static LocKey NameKey(ItemDataSO a) => Key(Category.Item, IdOf(a), Suffix.Name);
        public static LocKey DescKey(ItemDataSO a) => Key(Category.Item, IdOf(a), Suffix.Desc);

        public static LocKey NameKey(BuildableSO a) => Key(Category.Buildable, IdOf(a), Suffix.Name);

        public static LocKey NameKey(RecipeSO a) => Key(Category.Recipe, IdOf(a), Suffix.Name);

        public static LocKey NameKey(BlueprintSO a) => Key(Category.Blueprint, IdOf(a), Suffix.Name);
        public static LocKey HintKey(BlueprintSO a) => Key(Category.Blueprint, IdOf(a), Suffix.Hint);

        public static LocKey NameKey(ResearchEntrySO a) => Key(Category.Research, IdOf(a), Suffix.Name);
        public static LocKey SpeakerKey(ResearchEntrySO a) => Key(Category.Research, IdOf(a), Suffix.LineSpeaker);
        public static LocKey LineKey(ResearchEntrySO a) => Key(Category.Research, IdOf(a), Suffix.LineText);

        public static LocKey SpeakerKey(DiscoverySO a) => Key(Category.Discovery, IdOf(a), Suffix.LineSpeaker);
        public static LocKey LineKey(DiscoverySO a) => Key(Category.Discovery, IdOf(a), Suffix.LineText);

        public static LocKey NameKey(CreatureDefinitionSO a) => Key(Category.Creature, IdOf(a), Suffix.Name);
        public static LocKey CodexKey(CreatureDefinitionSO a) => Key(Category.Creature, IdOf(a), Suffix.Codex);

        public static LocKey NameKey(HarvestNodeSO a) => Key(Category.Harvest, IdOf(a), Suffix.Name);

        public static LocKey NameKey(PlantNodeSO a) => Key(Category.Plant, IdOf(a), Suffix.Name);

        public static LocKey NameKey(VitalDefinitionSO a) => Key(Category.Vital, IdOf(a), Suffix.Name);

        public static LocKey NameKey(SceneReferenceSO a) => Key(Category.Scene, IdOf(a), Suffix.Name);

        public static LocKey TitleKey(ChapterSO a) => Key(Category.Chapter, IdOf(a), Suffix.Title);

        public static LocKey TextKey(ObjectiveSO a) => Key(Category.Objective, IdOf(a), Suffix.Text);

        /// <summary>
        /// 시퀀스는 대사가 여럿이라 줄 번호가 키에 들어간다. <b>1부터 센다</b> —
        /// 번역가가 보는 것은 배열 첨자가 아니라 대사의 순서다.
        /// </summary>
        public static LocKey SpeakerKey(SequenceSO a, int index) =>
            Key(Category.Sequence, IdOf(a), ".line" + (index + 1) + Suffix.Speaker);

        public static LocKey LineKey(SequenceSO a, int index) =>
            Key(Category.Sequence, IdOf(a), ".line" + (index + 1) + Suffix.Text);

        // ── 조회 ────────────────────────────────────────────────

        public static string Name(ItemDataSO a) => Resolve(NameKey(a), a == null ? null : a.displayName);
        public static string Desc(ItemDataSO a) => Resolve(DescKey(a), a == null ? null : a.description);

        public static string Name(BuildableSO a) => Resolve(NameKey(a), a == null ? null : a.displayName);

        public static string Name(RecipeSO a) => Resolve(NameKey(a), a == null ? null : a.displayName);

        public static string Name(BlueprintSO a) => Resolve(NameKey(a), a == null ? null : a.displayName);
        public static string Hint(BlueprintSO a) => Resolve(HintKey(a), a == null ? null : a.hint);

        public static string Name(ResearchEntrySO a) => Resolve(NameKey(a), a == null ? null : a.displayName);
        public static string Speaker(ResearchEntrySO a) => Resolve(SpeakerKey(a), a?.line?.speaker);
        public static string Line(ResearchEntrySO a) => Resolve(LineKey(a), a?.line?.text);

        public static string Speaker(DiscoverySO a) => Resolve(SpeakerKey(a), a?.line?.speaker);
        public static string Line(DiscoverySO a) => Resolve(LineKey(a), a?.line?.text);

        public static string Name(CreatureDefinitionSO a) => Resolve(NameKey(a), a == null ? null : a.displayName);
        public static string Codex(CreatureDefinitionSO a) => Resolve(CodexKey(a), a == null ? null : a.codexDescription);

        public static string Name(HarvestNodeSO a) => Resolve(NameKey(a), a == null ? null : a.displayName);

        public static string Name(PlantNodeSO a) => Resolve(NameKey(a), a == null ? null : a.displayName);

        public static string Name(VitalDefinitionSO a) => Resolve(NameKey(a), a == null ? null : a.displayName);

        public static string Name(SceneReferenceSO a) => Resolve(NameKey(a), a == null ? null : a.displayName);

        public static string Title(ChapterSO a) => Resolve(TitleKey(a), a == null ? null : a.title);

        public static string Text(ObjectiveSO a) => Resolve(TextKey(a), a == null ? null : a.displayText);

        public static string Speaker(SequenceSO a, int index) =>
            Resolve(SpeakerKey(a, index), LineAt(a, index)?.speaker);

        public static string Line(SequenceSO a, int index) =>
            Resolve(LineKey(a, index), LineAt(a, index)?.text);

        // ── 안쪽 ────────────────────────────────────────────────

        /// <summary>
        /// 표가 이기고, 없으면 원문이 이긴다.
        ///
        /// 원문의 앞뒤 공백은 뗀다. 인스펙터의 <c>TextArea</c>에 줄바꿈이 딸려 들어가는
        /// 일이 흔하고, 표를 거친 값에는 그런 것이 붙지 않으므로 두 경로의 답이
        /// 달라지면 안 된다.
        /// </summary>
        static string Resolve(LocKey key, string fallback)
        {
            if (!key.IsEmpty && Loc.TryT(key, out var translated)) return translated;
            return fallback == null ? "" : fallback.Trim();
        }

        /// <summary>
        /// id가 없으면 키를 지을 수 없다. <c>default(LocKey)</c>가 아니라 빈 이름표를
        /// 돌려주는 이유는 <c>default</c>의 두 필드가 <b>null</b>이라서다 —
        /// 그것을 사전에 넣으면 <c>GetHashCode</c>에서 터진다.
        /// </summary>
        static LocKey Key(string category, string id, string suffix) =>
            string.IsNullOrEmpty(id) ? new LocKey(null, null) : new LocKey(category, id + suffix);

        static SequenceSO.Line LineAt(SequenceSO a, int index) =>
            a?.lines == null || index < 0 || index >= a.lines.Length ? null : a.lines[index];

        // ── id ──────────────────────────────────────────────────
        //
        // 대부분의 에셋에는 id 필드가 있고 그것이 저장본에도 박히는 계약이라 제일 안정적이다.
        // 없는 셋(채집물·식물·씬 참조)만 딴 자리에서 가져온다.

        public static string IdOf(ItemDataSO a) => a == null ? null : a.id;
        public static string IdOf(BuildableSO a) => a == null ? null : a.id;
        public static string IdOf(RecipeSO a) => a == null ? null : a.id;
        public static string IdOf(BlueprintSO a) => a == null ? null : a.id;
        public static string IdOf(ResearchEntrySO a) => a == null ? null : a.id;
        public static string IdOf(DiscoverySO a) => a == null ? null : a.id;
        public static string IdOf(CreatureDefinitionSO a) => a == null ? null : a.id;
        public static string IdOf(VitalDefinitionSO a) => a == null ? null : a.id;
        public static string IdOf(ChapterSO a) => a == null ? null : a.id;
        public static string IdOf(ObjectiveSO a) => a == null ? null : a.id;
        public static string IdOf(SequenceSO a) => a == null ? null : a.id;

        /// <summary>채집물에는 id 필드가 없다. 에셋 이름을 쓴다 — 이름을 바꾸면 키가 바뀐다.</summary>
        public static string IdOf(HarvestNodeSO a) => a == null ? null : Slug(a.name);

        /// <summary>식물도 마찬가지다.</summary>
        public static string IdOf(PlantNodeSO a) => a == null ? null : Slug(a.name);

        /// <summary>
        /// 씬 참조는 에셋 이름 대신 <c>sceneName</c>을 쓴다.
        /// 그쪽이 Build Settings에 등록된 이름이라 함부로 바뀌지 않는다.
        /// </summary>
        public static string IdOf(SceneReferenceSO a) => a == null ? null : Slug(a.sceneName);

        /// <summary>
        /// 파스칼케이스 이름을 키 규약(<c>^[a-z0-9]+([._][a-z0-9]+)*$</c>)에 맞는
        /// 소문자 스네이크로 바꾼다. <c>LooseScrap</c> → <c>loose_scrap</c>.
        /// </summary>
        public static string Slug(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";

            var sb = new StringBuilder(raw.Length + 4);
            bool prevWasLowerOrDigit = false;

            foreach (char c in raw)
            {
                if (char.IsUpper(c))
                {
                    if (prevWasLowerOrDigit) sb.Append('_');
                    sb.Append(char.ToLowerInvariant(c));
                    prevWasLowerOrDigit = false;
                }
                else if (char.IsLower(c) || char.IsDigit(c))
                {
                    sb.Append(c);
                    prevWasLowerOrDigit = true;
                }
                else
                {
                    sb.Append('_');
                    prevWasLowerOrDigit = false;
                }
            }

            // 밑줄이 겹치거나 앞뒤에 붙은 것을 걷어낸다. 키 규약이 둘 다 막는다.
            var outSb = new StringBuilder(sb.Length);
            bool lastUnderscore = true;   // 맨 앞의 밑줄부터 버린다
            for (int i = 0; i < sb.Length; i++)
            {
                char c = sb[i];
                if (c == '_')
                {
                    if (lastUnderscore) continue;
                    lastUnderscore = true;
                }
                else lastUnderscore = false;
                outSb.Append(c);
            }
            while (outSb.Length > 0 && outSb[outSb.Length - 1] == '_') outSb.Length--;

            return outSb.ToString();
        }
    }
}
