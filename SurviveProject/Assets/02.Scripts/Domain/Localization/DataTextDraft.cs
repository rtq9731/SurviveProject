using System;
using System.Collections.Generic;
using System.Text;

namespace Survive.Localization
{
    /// <summary>초안 병합 결과. 도구가 이것을 콘솔에 그대로 적는다.</summary>
    public sealed class DataTextDraftResult
    {
        /// <summary>새로 쓸 CSV 전문. 바뀐 것이 없으면 원문과 글자까지 같다.</summary>
        public string Csv;

        /// <summary>표에 없어서 새로 넣은 키.</summary>
        public readonly List<string> Added = new List<string>();

        /// <summary>이미 있어서 손대지 않은 키의 수.</summary>
        public int Kept;

        /// <summary>표에는 있는데 이제 어느 에셋도 만들지 않는 키. <b>지우지 않고 알리기만</b> 한다.</summary>
        public readonly List<string> Orphans = new List<string>();

        /// <summary>표의 <c>ko</c>와 에셋의 원문이 어긋난 키. 사람이 판단할 일이라 고치지 않는다.</summary>
        public readonly List<string> Drifted = new List<string>();

        /// <summary>표 자체가 이상해서 병합을 포기한 이유. 있으면 <see cref="Csv"/>는 null이다.</summary>
        public string Abort;
    }

    /// <summary>
    /// 데이터 에셋에서 뽑은 줄들을 기존 번역 표에 <b>합치는</b> 규칙.
    ///
    /// 순수 함수다. 파일을 읽지도 쓰지도 않고 AssetDatabase도 모른다 —
    /// 그래야 "사람이 고친 번역을 도구가 덮지 않는가"를 EditMode에서 확인할 수 있다.
    ///
    /// <b>표를 통째로 다시 쓰지 않는다.</b> <see cref="Marker"/> 위쪽은 글자 하나
    /// 건드리지 않는다. 손으로 짓는 <c>UI</c> 계열 줄들이 거기 있고, 그쪽을 고치는
    /// 사람이 따로 있기 때문이다. 아래쪽만 도구의 구역이다.
    ///
    /// <b>아래쪽도 값은 보존한다.</b> 다시 쓰는 것은 순서와 주석뿐이고,
    /// 이미 있던 줄의 칸 내용은 읽은 그대로 되돌려 놓는다. 번역가가 채운
    /// <c>en</c> 칸을 도구가 지우면 그 도구는 두 번 다시 쓰이지 않는다.
    ///
    /// <b>왜 정렬해서 다시 쓰는가.</b> 새 키를 끝에 덧붙이기만 하면 몇 번 돌린 뒤에는
    /// 순서가 에셋을 만든 순서가 되어 git diff가 읽히지 않는다. 카테고리·키로
    /// 정렬해 두면 어느 줄이 늘었는지가 diff에 한 덩어리로 보인다.
    /// </summary>
    public static class DataTextDraft
    {
        /// <summary>
        /// 이 줄부터 아래가 도구의 구역이다. 줄 첫 글자가 <c>#</c>라 파서는 주석으로 넘긴다.
        /// <b>이 문자열을 바꾸면 기존 구역을 못 찾아 표 끝에 구역이 하나 더 생긴다.</b>
        /// </summary>
        public const string Marker =
            "# ======== 여기부터 데이터 에셋 자동 생성 구역 (손으로 줄을 더하지 마라) ========";

        static readonly string[] MarkerHelp =
        {
            "이 아래는 Tools/Survive/번역/데이터 에셋 초안 갱신 이 관리한다.",
            "  - 줄을 더하거나 지우지 마라. 에셋을 고치고 메뉴를 다시 돌려라.",
            "  - ko 이외의 칸은 마음껏 채워라. 도구는 그 칸을 절대 덮지 않는다.",
            "  - 이 구역 안의 주석도 도구가 다시 쓴다. 사람이 남길 말은 이 줄 위쪽에.",
        };

        /// <summary>
        /// 표와 에셋 목록을 합친다. <paramref name="existingCsv"/>는 파일 전문이고,
        /// 돌려주는 것도 파일 전문이다.
        /// </summary>
        public static DataTextDraftResult Merge(string existingCsv, IReadOnlyList<DataTextEntry> entries)
        {
            var result = new DataTextDraftResult();
            existingCsv = existingCsv ?? "";

            var lineNumbers = new List<int>();
            var rows = Csv.Parse(existingCsv, lineNumbers);

            if (rows.Count == 0)
            {
                result.Abort = "표에 헤더 줄조차 없다. 표를 먼저 세워라.";
                return result;
            }

            var header = rows[0];
            if (header.Length < 3 || header[0].Trim() != StringCatalog.CategoryColumn ||
                header[1].Trim() != StringCatalog.KeyColumn)
            {
                result.Abort = "헤더가 Category,Key,<로케일…> 꼴이 아니다. 표를 먼저 고쳐라.";
                return result;
            }

            int koColumn = -1;
            for (int c = 2; c < header.Length; c++)
                if (header[c].Trim() == StringCatalog.DefaultLocale) { koColumn = c; break; }

            if (koColumn < 0)
            {
                result.Abort = $"헤더에 기본 로케일 \"{StringCatalog.DefaultLocale}\" 열이 없다.";
                return result;
            }

            string newline = existingCsv.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";

            // ① 구역 경계를 찾는다. 못 찾으면 표 전체가 사람의 구역이고 뒤에 새로 붙인다.
            int markerOffset = FindMarkerOffset(existingCsv, out int markerLine);
            string prefix = markerOffset >= 0 ? existingCsv.Substring(0, markerOffset) : existingCsv;
            if (prefix.Length > 0 && !prefix.EndsWith("\n", StringComparison.Ordinal)) prefix += newline;
            if (markerOffset < 0) markerLine = int.MaxValue;

            // ② 구역 안에 이미 있던 줄. 칸 내용을 그대로 붙들어 둔다.
            var kept = new Dictionary<LocKey, string[]>();
            var keptOrder = new List<LocKey>();
            for (int r = 1; r < rows.Count; r++)
            {
                if (lineNumbers[r] <= markerLine) continue;   // 사람의 구역이면 건드리지 않는다

                var row = rows[r];
                if (row.Length < 2) continue;

                var key = new LocKey(row[0], row[1]);
                if (key.IsEmpty || kept.ContainsKey(key)) continue;

                kept[key] = Fit(row, header.Length);
                keptOrder.Add(key);
            }

            // ③ 합친다.
            var merged = new Dictionary<LocKey, string[]>();
            var seenFromAssets = new HashSet<LocKey>();

            foreach (var e in entries ?? Array.Empty<DataTextEntry>())
            {
                var key = e.AsKey();
                if (key.IsEmpty) continue;
                if (!seenFromAssets.Add(key)) continue;   // 같은 키를 두 에셋이 낸다면 게이트가 따로 잡는다

                if (kept.TryGetValue(key, out var existing))
                {
                    merged[key] = existing;
                    result.Kept++;

                    // 에셋의 한국어가 바뀌었는데 표가 옛말을 들고 있으면 화면에 옛말이 나온다.
                    // 사람이 고친 번역일 수도 있으니 도구가 덮지는 않고, 알리기만 한다.
                    string tableKo = existing.Length > koColumn ? existing[koColumn] : "";
                    if (!string.Equals(tableKo.Trim(), e.Korean, StringComparison.Ordinal))
                        result.Drifted.Add($"{key}: 표 \"{tableKo.Trim()}\" / 에셋 \"{e.Korean}\"");
                }
                else
                {
                    var row = new string[header.Length];
                    row[0] = key.Category;
                    row[1] = key.Key;
                    for (int c = 2; c < header.Length; c++) row[c] = "";
                    row[koColumn] = e.Korean;

                    merged[key] = row;
                    result.Added.Add(key.ToString());
                }
            }

            foreach (var key in keptOrder)
            {
                if (merged.ContainsKey(key)) continue;
                merged[key] = kept[key];
                result.Orphans.Add(key.ToString());
            }

            // ④ 다시 쓴다.
            var notes = new Dictionary<LocKey, string>();
            foreach (var e in entries ?? Array.Empty<DataTextEntry>())
                if (!string.IsNullOrEmpty(e.Note)) notes[e.AsKey()] = e.Note;

            result.Csv = prefix + Render(merged, notes, newline);
            return result;
        }

        // ── 다시 쓰기 ────────────────────────────────────────────

        static string Render(Dictionary<LocKey, string[]> rows, Dictionary<LocKey, string> notes,
                             string newline)
        {
            var keys = new List<LocKey>(rows.Keys);
            keys.Sort(Compare);

            var sb = new StringBuilder();
            sb.Append(Marker).Append(newline);
            foreach (var help in MarkerHelp) sb.Append("# ").Append(help).Append(newline);

            string category = null;
            foreach (var key in keys)
            {
                if (!string.Equals(category, key.Category, StringComparison.Ordinal))
                {
                    category = key.Category;
                    sb.Append('#').Append(newline);
                    sb.Append("# ---- ").Append(category).Append(" ----").Append(newline);
                    AppendComment(sb, DataTextCatalog.BlockNote(category), newline);
                }

                if (notes.TryGetValue(key, out var note)) AppendComment(sb, note, newline);

                var row = rows[key];
                for (int c = 0; c < row.Length; c++)
                {
                    if (c > 0) sb.Append(',');
                    sb.Append(Csv.Escape(row[c]));
                }
                sb.Append(newline);
            }

            return sb.ToString();
        }

        static void AppendComment(StringBuilder sb, string text, string newline)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (var line in text.Split('\n'))
                sb.Append("#   ").Append(line.TrimEnd('\r')).Append(newline);
        }

        /// <summary>카테고리 먼저, 그 안에서 키. 순서가 흔들리면 diff를 읽을 수 없다.</summary>
        static int Compare(LocKey a, LocKey b)
        {
            int c = string.CompareOrdinal(a.Category, b.Category);
            return c != 0 ? c : string.CompareOrdinal(a.Key, b.Key);
        }

        static string[] Fit(string[] row, int width)
        {
            if (row.Length == width) return row;

            var fitted = new string[width];
            for (int c = 0; c < width; c++) fitted[c] = c < row.Length ? row[c] : "";
            return fitted;
        }

        // ── 경계 찾기 ────────────────────────────────────────────

        /// <summary>
        /// 표시줄이 시작하는 글자 위치. 없으면 -1.
        ///
        /// 단순 문자열 검색이 아닌 이유: 따옴표로 감싼 값 안에 우연히 같은 글이 들어 있어도
        /// 그것은 표시줄이 아니다. 그래서 <b>줄 첫머리이면서 따옴표 밖</b>인 자리만 본다.
        /// </summary>
        static int FindMarkerOffset(string text, out int markerLine)
        {
            markerLine = -1;
            if (string.IsNullOrEmpty(text)) return -1;

            bool quoted = false;
            bool atLineStart = true;
            int line = 1;

            for (int i = 0; i < text.Length; i++)
            {
                if (atLineStart && !quoted &&
                    string.CompareOrdinal(text, i, Marker, 0, Marker.Length) == 0)
                {
                    markerLine = line;
                    return i;
                }

                char c = text[i];
                atLineStart = false;

                if (c == '"') quoted = !quoted;
                else if (c == '\n') { line++; atLineStart = true; }
            }

            return -1;
        }
    }
}
