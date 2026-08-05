using System;
using System.Collections.Generic;

namespace Survive.Localization
{
    /// <summary>
    /// CSV 한 장을 읽어 로케일별 표로 세워 둔 것. 여기까지가 순수부다 —
    /// 파일을 어디서 어떻게 읽어 왔는지는 이 클래스가 모른다.
    ///
    /// 표의 모양은 <c>Category,Key,ko,en,…</c>. 첫 두 칸은 이름표이고 나머지가 로케일이다.
    /// Unity Localization의 CSV(<c>Key</c> + 로케일 열)와 어긋나지 않게 두었다 —
    /// 그쪽으로 갈아탈 때 <c>Category</c>별로 줄을 갈라 테이블 파일로 나누면 끝이다.
    ///
    /// <b>빈 칸은 담지 않는다.</b> 담아 버리면 번역이 안 된 자리에 빈 문자열이 들어가
    /// 화면이 비고, 폴백이 도는 자리가 사라진다.
    /// </summary>
    public sealed class StringCatalog
    {
        /// <summary>폴백 사슬의 마지막 바로 앞. 이 칸은 항상 차 있어야 한다.</summary>
        public const string DefaultLocale = "ko";

        /// <summary>
        /// 표에 열로 존재하지 않는 특수 로케일. 기본 로케일 값을 눈에 띄게 부풀려 낸다
        /// (<see cref="PseudoLocalizer"/>).
        /// </summary>
        public const string PseudoLocale = "pseudo";

        public const string CategoryColumn = "Category";
        public const string KeyColumn = "Key";

        readonly List<string> _locales = new List<string>();
        readonly List<LocKey> _keys = new List<LocKey>();
        readonly List<string> _problems = new List<string>();
        readonly Dictionary<string, Dictionary<LocKey, string>> _tables =
            new Dictionary<string, Dictionary<LocKey, string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>아무것도 실리지 않은 표. 아직 읽지 못했을 때의 자리를 채운다.</summary>
        public static readonly StringCatalog Empty = new StringCatalog();

        /// <summary>CSV 헤더에 적혀 있던 로케일들. 나온 순서 그대로.</summary>
        public IReadOnlyList<string> Locales => _locales;

        /// <summary>표에 실린 이름표 전부. 나온 순서 그대로.</summary>
        public IReadOnlyList<LocKey> Keys => _keys;

        /// <summary>
        /// 표 자체의 건전성 문제. 게이트가 이 목록이 비어 있는지를 본다 —
        /// 중복 이름표, 헤더와 칸 수가 다른 줄, 기본 로케일 칸이 빈 줄.
        /// </summary>
        public IReadOnlyList<string> Problems => _problems;

        public bool Contains(LocKey key) =>
            _tables.TryGetValue(DefaultLocale, out var t) && t.ContainsKey(key);

        /// <summary>
        /// 로케일 하나의 표. 없으면 null. <see cref="Loc"/>이 이것을 붙들고 있다가
        /// 조회 때 사전 한 번만 뒤진다.
        /// </summary>
        public IReadOnlyDictionary<LocKey, string> TableFor(string locale)
        {
            if (string.IsNullOrEmpty(locale)) return null;
            return _tables.TryGetValue(locale, out var t) ? t : null;
        }

        public bool TryGet(string locale, LocKey key, out string value)
        {
            value = null;
            var t = TableFor(locale);
            return t != null && t.TryGetValue(key, out value);
        }

        /// <summary>
        /// CSV 글줄을 읽어 표를 세운다. <b>절대 예외를 던지지 않는다</b> —
        /// 표가 망가졌다고 게임이 안 뜨면 그게 더 나쁘다. 문제는
        /// <see cref="Problems"/>에 적어 두고, 읽을 수 있는 만큼 읽는다.
        /// </summary>
        public static StringCatalog Parse(string csvText)
        {
            var catalog = new StringCatalog();
            var lines = new List<int>();
            var rows = Csv.Parse(csvText, lines);

            if (rows.Count == 0)
            {
                catalog._problems.Add("표가 비어 있다: 헤더 줄조차 없다");
                return catalog;
            }

            var header = rows[0];
            if (header.Length < 3)
                catalog._problems.Add(
                    $"{lines[0]}행 헤더에 로케일 열이 없다: 최소 {CategoryColumn},{KeyColumn},{DefaultLocale}가 있어야 한다");

            if (header.Length > 0 && header[0].Trim() != CategoryColumn)
                catalog._problems.Add($"{lines[0]}행 헤더의 첫 칸이 \"{CategoryColumn}\"이 아니다: \"{header[0]}\"");
            if (header.Length > 1 && header[1].Trim() != KeyColumn)
                catalog._problems.Add($"{lines[0]}행 헤더의 둘째 칸이 \"{KeyColumn}\"이 아니다: \"{header[1]}\"");

            for (int c = 2; c < header.Length; c++)
            {
                string locale = header[c].Trim();
                if (locale.Length == 0)
                {
                    catalog._problems.Add($"{lines[0]}행 헤더 {c + 1}번째 칸에 로케일 이름이 없다");
                    continue;
                }
                if (locale.Equals(PseudoLocale, StringComparison.OrdinalIgnoreCase))
                {
                    // 의사 번역은 만들어 내는 것이라 표에 열로 두면 두 진실이 생긴다.
                    catalog._problems.Add($"\"{PseudoLocale}\"은 만들어 내는 로케일이라 표에 열로 둘 수 없다");
                    continue;
                }
                if (catalog._locales.Contains(locale))
                {
                    catalog._problems.Add($"{lines[0]}행 헤더에 로케일 \"{locale}\"이 두 번 있다");
                    continue;
                }
                catalog._locales.Add(locale);
                catalog._tables[locale] = new Dictionary<LocKey, string>();
            }

            // 기본 로케일 표는 없더라도 자리를 만들어 둔다. 폴백이 null을 만나지 않게.
            if (!catalog._tables.ContainsKey(DefaultLocale))
            {
                catalog._problems.Add($"헤더에 기본 로케일 \"{DefaultLocale}\" 열이 없다");
                catalog._tables[DefaultLocale] = new Dictionary<LocKey, string>();
            }

            var seen = new HashSet<LocKey>();

            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                int line = lines[r];

                if (row.Length != header.Length)
                    catalog._problems.Add(
                        $"{line}행: 칸이 {row.Length}개인데 헤더는 {header.Length}개다");

                string category = row.Length > 0 ? row[0].Trim() : "";
                string key = row.Length > 1 ? row[1].Trim() : "";

                if (category.Length == 0 || key.Length == 0)
                {
                    catalog._problems.Add($"{line}행: Category나 Key가 비었다 (\"{category}\"/\"{key}\")");
                    continue;
                }

                var lk = new LocKey(category, key);
                if (!seen.Add(lk))
                {
                    catalog._problems.Add($"{line}행: 이름표가 겹친다 — {lk}");
                    continue;
                }
                catalog._keys.Add(lk);

                for (int c = 2; c < header.Length && c < row.Length; c++)
                {
                    string locale = header[c].Trim();
                    if (!catalog._tables.TryGetValue(locale, out var table)) continue;
                    string value = row[c];
                    if (string.IsNullOrEmpty(value)) continue;   // 빈 칸은 담지 않는다
                    table[lk] = value;
                }

                if (!catalog._tables[DefaultLocale].ContainsKey(lk))
                    catalog._problems.Add($"{line}행: 기본 로케일({DefaultLocale}) 칸이 비었다 — {lk}");
            }

            return catalog;
        }
    }
}
