using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Survive.Localization;

namespace Survive.EditorTools
{
    /// <summary>
    /// 데이터 에셋의 한국어를 번역 표 초안으로 뽑아내는 도구.
    ///
    /// <b>왜 도구인가.</b> 대상이 100줄이 넘는다. 손으로 옮기면 반드시 어딘가에서
    /// 한 글자가 틀리고, 틀린 것은 그 화면을 그 로케일로 열어 보기 전까지 아무
    /// 신호도 내지 않는다. 옮기는 일은 기계가 하고 사람은 번역만 한다.
    ///
    /// <b>사람이 고친 것을 덮지 않는다.</b> 이미 표에 있는 키는 칸 내용을 그대로 두고,
    /// 새 키만 넣는다. 어느 에셋도 만들지 않게 된 키는 지우지 않고 콘솔에 적기만
    /// 한다 — 지울지 말지는 사람이 판단할 일이다. 규칙 전부는
    /// <see cref="DataTextDraft"/>에 있고 EditMode가 그것을 지킨다.
    /// </summary>
    public static class LocDataDraftTool
    {
        const string CsvPath = "Assets/Resources/Localization/strings.csv";

        [MenuItem("Tools/Survive/번역/데이터 에셋 초안 갱신", priority = 100)]
        public static void Update() => Run(write: true);

        [MenuItem("Tools/Survive/번역/데이터 에셋 초안 미리보기 (쓰지 않는다)", priority = 101)]
        public static void Preview() => Run(write: false);

        static void Run(bool write)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), CsvPath);
            if (!File.Exists(full))
            {
                Debug.LogError($"[번역 초안] 표가 없다: {CsvPath}");
                return;
            }

            var entries = DataTextAssets.CollectAll();
            string before = File.ReadAllText(full);
            var result = DataTextDraft.Merge(before, entries);

            if (result.Abort != null)
            {
                Debug.LogError("[번역 초안] " + result.Abort);
                return;
            }

            var report = new StringBuilder();
            report.Append($"[번역 초안] 에셋에서 {entries.Count}칸을 읽었다 — ")
                  .Append($"새로 {result.Added.Count}개, 그대로 둔 것 {result.Kept}개");
            if (result.Orphans.Count > 0) report.Append($", 에셋이 없어진 키 {result.Orphans.Count}개");
            if (result.Drifted.Count > 0) report.Append($", 원문이 어긋난 키 {result.Drifted.Count}개");

            var fontFixed = new List<string>();
            foreach (var e in entries)
                if (e.FontFixed) fontFixed.Add($"{e} ({AssetDatabase.GetAssetPath(e.Owner)}): " +
                                               $"\"{e.AssetKorean}\" -> \"{e.Korean}\"");

            Append(report, "새로 넣은 키", result.Added);
            Append(report, "글꼴에 없는 글자를 표가 대치했다 (에셋을 고치면 더 낫다)", fontFixed);
            Append(report, "표에는 있는데 어느 에셋도 만들지 않는 키 (지우지 않았다 — 사람이 판단해라)",
                   result.Orphans);
            Append(report, "표의 ko와 에셋 원문이 다르다 (덮지 않았다 — 어느 쪽이 맞는지 사람이 정해라)",
                   result.Drifted);

            bool changed = !string.Equals(before, result.Csv, System.StringComparison.Ordinal);

            if (!write)
            {
                report.Append(changed ? "\n\n미리보기다. 파일은 그대로 두었다." : "\n\n바뀔 것이 없다.");
                Debug.Log(report.ToString());
                return;
            }

            if (!changed)
            {
                Debug.Log(report.Append("\n\n바뀐 것이 없어 파일을 쓰지 않았다.").ToString());
                return;
            }

            // BOM 없는 UTF-8. 파서는 BOM을 견디지만 git diff에 잡음이 남는다.
            File.WriteAllText(full, result.Csv, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(CsvPath);

            Debug.Log(report.Append($"\n\n{CsvPath}를 다시 썼다.").ToString());
        }

        static void Append(StringBuilder sb, string title, List<string> items)
        {
            if (items.Count == 0) return;
            sb.Append("\n\n").Append(title).Append(':');
            foreach (var item in items) sb.Append("\n  ").Append(item);
        }
    }
}
