using System;
using System.Collections.Generic;
using UnityEngine;
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
    /// <summary>번역 표에 있어야 할 데이터 에셋 글자 한 칸.</summary>
    public sealed class DataTextEntry
    {
        public string Category;

        /// <summary>접미어까지 붙인 완성된 키. <c>mushroom_wood.name</c>.</summary>
        public string Key;

        /// <summary>
        /// 표의 <c>ko</c> 칸에 들어갈 값. 에셋 원문에서 앞뒤 공백을 떼고,
        /// 글꼴에 없는 글자를 대치한 것(<see cref="FontSafe"/>).
        /// </summary>
        public string Korean;

        /// <summary>에셋에 적혀 있는 그대로. 폴백이 실제로 이것을 내놓는지 게이트가 본다.</summary>
        public string AssetKorean;

        /// <summary>글꼴에 없는 글자가 있어 <see cref="Korean"/>이 원문과 달라졌는가.</summary>
        public bool FontFixed;

        /// <summary>
        /// 번역가에게 남기는 맥락. 없으면 null. CSV에서는 그 줄 위의 <c>#</c> 주석이 된다.
        /// </summary>
        public string Note;

        /// <summary>어느 에셋에서 나왔는가. 오류 문구에 쓴다.</summary>
        public ScriptableObject Owner;

        /// <summary>
        /// <b>지금</b> 조회 경로가 내놓는 답. 폴백이 실제로 도는지를 게이트가
        /// 이것으로 확인한다 — 긁는 쪽과 읽는 쪽이 서로 다른 필드를 보고 있으면
        /// 표를 비웠을 때 이 값이 <see cref="Korean"/>과 달라진다.
        /// </summary>
        public Func<string> Resolve;

        public LocKey AsKey() => new LocKey(Category, Key);

        public override string ToString() => Category + "/" + Key;
    }

    /// <summary>
    /// 데이터 에셋을 훑어 "표에 있어야 할 줄"을 뽑아내는 자리.
    ///
    /// <b>왜 여기(Domain)인가.</b> 초안 생성 도구(에디터)와 게이트(EditMode 테스트)가
    /// 반드시 같은 목록을 봐야 한다. 둘이 각자 훑으면 도구가 안 만드는 키를 게이트가
    /// 요구하거나 그 반대가 되고, 어느 쪽도 사람이 눈으로 대조할 수 없다.
    ///
    /// 에셋을 <b>찾는</b> 일(AssetDatabase)은 여기 없다. 받은 것만 훑는다 —
    /// 그래서 이 클래스는 Unity 에디터 없이도 테스트할 수 있다.
    /// </summary>
    public static class DataTextCatalog
    {
        /// <summary>
        /// 카테고리별로 CSV 구역 머리에 적어 둘 말. 번역가가 이 줄들만 읽고도
        /// 그 구역이 무엇인지, 어떤 문체를 지켜야 하는지 알 수 있어야 한다.
        /// </summary>
        public static string BlockNote(string category)
        {
            switch (category)
            {
                case DataText.Category.Item:
                    return "아이템 이름(.name)과 설명(.desc). 설명은 소지품 쪽지에 뜬다.";
                case DataText.Category.Buildable:
                    return "지을 수 있는 것의 이름. 건축 메뉴와 철거 프롬프트에 뜬다.";
                case DataText.Category.Recipe:
                    return "제작법 이름. 비워 두면 화면이 결과물 아이템 이름을 대신 쓴다.";
                case DataText.Category.Blueprint:
                    return "청사진 이름(.name)과 여는 방법(.hint). hint는 도감의 잠긴 줄에만 뜬다.";
                case DataText.Category.Research:
                    return "연구 항목 이름(.name)과 끝냈을 때 AI가 하는 말(.line.text).\n" +
                           AiVoiceNote +
                           "\n연구는 마지막 문장이 다르다: \"해당 구조를 적용한 제작법을 확보했습니다.\"";
                case DataText.Category.Discovery:
                    return "재료를 처음 손에 넣었을 때 AI가 하는 말(.line.text).\n" +
                           AiVoiceNote +
                           "\n현장 발견의 마지막 문장: \"해당 물질을 사용한 제작법이 있습니다.\"";
                case DataText.Category.Creature:
                    return "생물 이름(.name)과 도감 관측 기록(.codex).";
                case DataText.Category.Harvest:
                    return "채집물 이름. 키는 에셋 이름에서 만든다(id 필드가 없다).";
                case DataText.Category.Plant:
                    return "식물 이름. 키는 에셋 이름에서 만든다(id 필드가 없다).";
                case DataText.Category.Vital:
                    return "생명 수치 이름. HUD 게이지에 붙는다.";
                case DataText.Category.Scene:
                    return "씬 표시 이름. 키는 Build Settings의 씬 이름에서 만든다.";
                case DataText.Category.Chapter:
                    return "챕터 제목.";
                case DataText.Category.Objective:
                    return "목표 한 줄. 화면에서는 앞에 기호가 붙고 뒤에 진행률이 붙는다.";
                case DataText.Category.Sequence:
                    return "연출 자막. .line1부터 <b>재생 순서대로</b>다. 순서가 곧 맥락이다.";
                default:
                    return null;
            }
        }

        /// <summary>
        /// 우주복 AI의 말투 규격. <c>DiscoverySO</c>의 문서 주석이 원본이고,
        /// 그 규격을 모르는 번역가가 문체를 무너뜨리지 않게 표에도 적어 둔다.
        /// </summary>
        public const string AiVoiceNote =
            "화자는 우주복 AI, 기계다. 세 문장 고정:\n" +
            "  [관찰된 성질] 분석... [용도 판정, \"~것으로 보입니다\"]. [해금 정형구].\n" +
            "  아이템 이름을 되뇌지 않는다. 감탄/추측/이모지/물음 금지. 규격 원본은 " +
            "DiscoverySO의 문서 주석.";

        /// <summary>
        /// 받은 에셋들에서 번역 대상 칸을 전부 뽑는다. 종류를 모르는 에셋은 조용히 넘긴다 —
        /// 데이터 폴더에는 목록 에셋(ItemDatabase 등)처럼 글자가 없는 것도 섞여 있다.
        /// </summary>
        public static void Collect(IEnumerable<ScriptableObject> assets, List<DataTextEntry> into)
        {
            if (into == null) return;
            if (assets == null) return;

            foreach (var asset in assets)
            {
                if (asset == null) continue;
                CollectOne(asset, into);
            }
        }

        static void CollectOne(ScriptableObject asset, List<DataTextEntry> into)
        {
            switch (asset)
            {
                case ItemDataSO a:
                    Add(into, a, DataText.NameKey(a), a.displayName, null, () => DataText.Name(a));
                    Add(into, a, DataText.DescKey(a), a.description, null, () => DataText.Desc(a));
                    break;

                case BuildableSO a:
                    Add(into, a, DataText.NameKey(a), a.displayName, null, () => DataText.Name(a));
                    break;

                case RecipeSO a:
                    Add(into, a, DataText.NameKey(a), a.displayName, null, () => DataText.Name(a));
                    break;

                case BlueprintSO a:
                    Add(into, a, DataText.NameKey(a), a.displayName, null, () => DataText.Name(a));
                    Add(into, a, DataText.HintKey(a), a.hint, null, () => DataText.Hint(a));
                    break;

                case ResearchEntrySO a:
                    Add(into, a, DataText.NameKey(a), a.displayName, null, () => DataText.Name(a));
                    Add(into, a, DataText.SpeakerKey(a), a.line?.speaker, null, () => DataText.Speaker(a));
                    Add(into, a, DataText.LineKey(a), a.line?.text,
                        "연구를 끝냈을 때의 대사. 대상: " + Describe(a.materials) +
                        " / 항목 이름: " + Blank(a.displayName),
                        () => DataText.Line(a));
                    break;

                case DiscoverySO a:
                    Add(into, a, DataText.SpeakerKey(a), a.line?.speaker, null, () => DataText.Speaker(a));
                    Add(into, a, DataText.LineKey(a), a.line?.text,
                        "재료를 처음 쥐었을 때의 대사. 대상 아이템: " +
                        (a.item != null ? Blank(a.item.displayName) + " (" + a.item.id + ")" : "(없음)"),
                        () => DataText.Line(a));
                    break;

                case CreatureDefinitionSO a:
                    Add(into, a, DataText.NameKey(a), a.displayName, null, () => DataText.Name(a));
                    Add(into, a, DataText.CodexKey(a), a.codexDescription, null, () => DataText.Codex(a));
                    break;

                case HarvestNodeSO a:
                    Add(into, a, DataText.NameKey(a), a.displayName, null, () => DataText.Name(a));
                    break;

                case PlantNodeSO a:
                    Add(into, a, DataText.NameKey(a), a.displayName, null, () => DataText.Name(a));
                    break;

                case VitalDefinitionSO a:
                    Add(into, a, DataText.NameKey(a), a.displayName, null, () => DataText.Name(a));
                    break;

                case SceneReferenceSO a:
                    Add(into, a, DataText.NameKey(a), a.displayName, null, () => DataText.Name(a));
                    break;

                case ChapterSO a:
                    Add(into, a, DataText.TitleKey(a), a.title, null, () => DataText.Title(a));
                    break;

                case ObjectiveSO a:
                    Add(into, a, DataText.TextKey(a), a.displayText, null, () => DataText.Text(a));
                    break;

                case SequenceSO a:
                    if (a.lines == null) break;
                    for (int i = 0; i < a.lines.Length; i++)
                    {
                        int n = i;   // 클로저가 루프 변수를 붙들지 않게
                        Add(into, a, DataText.SpeakerKey(a, n), a.lines[n]?.speaker, null,
                            () => DataText.Speaker(a, n));
                        Add(into, a, DataText.LineKey(a, n), a.lines[n]?.text,
                            (n + 1) + "번째 자막" + (n > 0 ? " (앞 줄: " + Snippet(a.lines[n - 1]?.text) + ")" : ""),
                            () => DataText.Line(a, n));
                    }
                    break;
            }
        }

        /// <summary>
        /// <b>비어 있는 칸은 담지 않는다.</b> 에셋이 그 필드를 안 쓰는 것과
        /// 번역이 빠진 것은 다른 일이고, 빈 줄을 표에 넣으면 <c>ko</c>가 빈 줄이 되어
        /// 표 건전성 검사가 실패한다.
        /// </summary>
        static void Add(List<DataTextEntry> into, ScriptableObject owner, LocKey key,
                        string korean, string note, Func<string> resolve)
        {
            if (key.IsEmpty) return;
            if (string.IsNullOrWhiteSpace(korean)) return;

            string raw = korean.Trim();
            string safe = FontSafe.Sanitize(raw);
            bool fixedUp = !ReferenceEquals(raw, safe);

            if (fixedUp)
            {
                // 표가 원문과 다른 이유는 파일 안에 적혀 있어야 한다. 안 그러면
                // 다음 사람이 "왜 에셋이랑 다르지" 하고 되돌려 놓는다.
                string why = "글꼴(ChosunGu)에 없는 글자를 대치했다. 에셋 원문: " + raw;
                note = string.IsNullOrEmpty(note) ? why : note + "\n" + why;
            }

            into.Add(new DataTextEntry
            {
                Category = key.Category,
                Key = key.Key,
                Korean = safe,
                AssetKorean = raw,
                FontFixed = fixedUp,
                Note = note,
                Owner = owner,
                Resolve = resolve,
            });
        }

        static string Blank(string s) => string.IsNullOrWhiteSpace(s) ? "(이름 없음)" : s.Trim();

        static string Snippet(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "(없음)";
            s = s.Trim().Replace("\n", " ").Replace("\r", "");
            return s.Length <= 24 ? s : s.Substring(0, 24) + "...";
        }

        static string Describe(ItemStack[] stacks)
        {
            if (stacks == null || stacks.Length == 0) return "(없음)";

            var sb = new System.Text.StringBuilder();
            foreach (var s in stacks)
            {
                if (s?.item == null) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(Blank(s.item.displayName));
            }
            return sb.Length == 0 ? "(없음)" : sb.ToString();
        }
    }
}
