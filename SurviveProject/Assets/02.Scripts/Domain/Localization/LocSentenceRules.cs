using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Survive.Localization
{
    /// <summary>깨진 규칙의 종류.</summary>
    public enum LocRule
    {
        /// <summary><c>Loc.F</c>의 인자에 문자열 리터럴이 있다. 말을 인자로 넘긴 것이다.</summary>
        LiteralArgument,

        /// <summary>화면에 나가는 코드에 한글 문자열 리터럴이 남아 있다.</summary>
        KoreanLiteral,

        /// <summary><c>Loc</c>이 낸 글을 한글 리터럴과 <c>+</c>나 <c>Append</c>로 이었다.</summary>
        Concatenation,
    }

    /// <summary>깨진 자리 하나.</summary>
    public readonly struct LocFinding
    {
        public readonly LocRule Rule;
        public readonly int Line;
        public readonly string Detail;

        public LocFinding(LocRule rule, int line, string detail)
        {
            Rule = rule;
            Line = line;
            Detail = detail;
        }

        public override string ToString() => $"{Line}행 [{Rule}] {Detail}";
    }

    /// <summary><c>Loc.T</c>/<c>Loc.F</c> 호출 한 자리.</summary>
    public sealed class LocCall
    {
        public string Category;
        public string Key;
        public int Line;

        /// <summary><c>Loc.F</c>인가(<c>Loc.T</c>가 아니라).</summary>
        public bool IsFormat;

        /// <summary>이름표 두 개가 리터럴인가. 아니면 표와 대조할 수 없다.</summary>
        public bool NameIsLiteral;

        /// <summary>이름표 두 개를 뺀 인자 개수.</summary>
        public int ArgCount;

        /// <summary>
        /// 인자 개수를 믿어도 되는가. 배열을 통째로 넘기는 자리는 셀 수 없다.
        /// </summary>
        public bool ArgCountKnown;

        public LocKey AsKey() => new LocKey(Category, Key);
    }

    /// <summary>
    /// <b>문장을 조각으로 짓지 못하게 막는 규칙</b>을 소스 글줄에서 판정한다.
    ///
    /// <b>규칙 한 줄.</b> 한 줄에 보이는 문장은 표의 한 칸에 통째로 들어가고,
    /// 그 안에서 <c>{0} {1} {2}</c>가 값이 들어갈 자리를 표시한다.
    /// <b>인자로 넘길 수 있는 것은 값뿐이다</b> — 숫자, 데이터에서 온 이름, 서식된 시간.
    /// 코드에 적은 문자열 리터럴("개", ", ", "  ")은 값이 아니라 <b>말</b>이고,
    /// 말은 번역 대상이므로 표 안에 있어야 한다.
    ///
    /// <b>왜인가.</b> 조각을 이어 붙이면 번역가가 어순을 바꿀 수 없다.
    /// 한국어 "가진 수/드는 수"가 영어에서 같은 순서일 이유가 없고, 세는 단위는
    /// 언어마다 아예 없거나(영어) 명사마다 다르다(한국어 개/마리/자루).
    ///
    /// <b>유일한 예외는 되풀이다.</b> 재료가 세 종이면 같은 꼴이 세 번 반복되는데,
    /// 이것만은 통짜로 만들 수 없다. 그래서 항목 하나의 틀(<c>ingredient_entry</c>)과
    /// 항목 사이 구분자(<c>list_separator</c>)를 <b>표의 키로</b> 두고, 이어 붙인 결과를
    /// 바깥 문장에 <c>{n}</c>으로 꽂는다. 구분자를 코드에 <c>", "</c>로 박지 않는 이유는
    /// 중국어·일본어가 <c>、</c>를 쓰기 때문이다.
    /// <b>이 예외를 넓혀 읽지 마라</b> — "구분자도 조각이니 이것도 되겠지"가 시작되면
    /// 규칙이 무너진다.
    ///
    /// <b>사각지대.</b> 정적 분석은 완벽할 수 없다. 변수로 만든 이름표
    /// (<c>Loc.T("Item", id + ".name")</c>), 배열로 넘긴 인자, 리플렉션은 판정할 수 없다.
    /// 게이트가 그 수를 세어 로그에 남긴다 — 모르는 것을 아는 게이트가 낫다.
    /// </summary>
    public static class LocSentenceRules
    {
        static readonly Regex CallStart =
            new Regex(@"\bLoc\s*\.\s*([TF])\s*\(", RegexOptions.Compiled);

        static readonly Regex LiteralOnly =
            new Regex(@"^\s*""((?:[^""\\]|\\.)*)""\s*$", RegexOptions.Compiled);

        /// <summary>인스펙터에만 뜨는 글. 화면에 나가지 않으므로 번역 대상이 아니다.</summary>
        static readonly Regex EditorOnlyAttributes = new Regex(
            @"\[\s*(Tooltip|Header|TextArea|Multiline|Space|InspectorName|MenuItem|" +
            @"CreateAssetMenu|AddComponentMenu|HelpURL|ContextMenu)\b[^\]\n]*\]",
            RegexOptions.Compiled);

        /// <summary>콘솔에만 나가는 글. 사람에게 보여 주는 화면이 아니다.</summary>
        static readonly Regex ConsoleCalls =
            new Regex(@"\bDebug\s*\.\s*(Log|LogWarning|LogError|LogFormat|LogAssertion)\s*\(",
                      RegexOptions.Compiled);

        /// <summary>이름표 자리의 리터럴은 키다. 인자 검사에서 세면 안 된다.</summary>
        static readonly Regex NestedLocName =
            new Regex(@"Loc\s*\.\s*[TF]\s*\(\s*""[^""]*""\s*,\s*""[^""]*""", RegexOptions.Compiled);

        // ── 창구 ─────────────────────────────────────────────────

        /// <summary>
        /// 소스에서 규칙을 어긴 자리를 뽑는다.
        /// </summary>
        /// <param name="banKoreanLiterals">
        /// 이 파일이 "화면에 나가는 코드"로 지정된 범위 안인가.
        /// 범위는 게이트가 목록으로 관리한다 — 넓힐 때마다 게이트가 강해진다.
        /// </param>
        public static List<LocFinding> Inspect(string source, bool banKoreanLiterals)
        {
            var findings = new List<LocFinding>();
            string code = Prepare(source);
            if (code.Length == 0) return findings;

            InspectCalls(code, findings);
            if (banKoreanLiterals) InspectKoreanLiterals(code, findings);
            return findings;
        }

        /// <summary>소스에 있는 <c>Loc.T</c>/<c>Loc.F</c> 호출 전부.</summary>
        public static List<LocCall> FindCalls(string source)
        {
            var calls = new List<LocCall>();
            string code = Prepare(source);

            foreach (Match m in CallStart.Matches(code))
            {
                var args = SplitArguments(code, m.Index + m.Length, out int _);
                calls.Add(Describe(code, m, args));
            }
            return calls;
        }

        public static bool HasHangul(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s)
            {
                if (c >= '가' && c <= '힣') return true;   // 음절
                if (c >= 'ᄀ' && c <= 'ᇿ') return true;   // 자모
                if (c >= '㄰' && c <= '㆏') return true;   // 호환 자모
            }
            return false;
        }

        /// <summary>
        /// 검사하기 전에 지우는 것들. 줄바꿈은 남긴다 — 줄 번호가 어긋나면
        /// 실패 메시지가 사람을 엉뚱한 줄로 보낸다.
        /// </summary>
        public static string Prepare(string source)
        {
            if (string.IsNullOrEmpty(source)) return "";

            string code = LocSourceScanner.StripComments(source);
            code = BlankMatches(code, EditorOnlyAttributes);
            code = BlankCalls(code, ConsoleCalls);
            return code;
        }

        // ── ① Loc.F의 인자, ③ 이어 붙이기 ────────────────────────

        static void InspectCalls(string code, List<LocFinding> findings)
        {
            foreach (Match m in CallStart.Matches(code))
            {
                var args = SplitArguments(code, m.Index + m.Length, out int close);
                int line = LineOf(code, m.Index);

                for (int i = 2; i < args.Count; i++)
                {
                    string arg = StripNestedNames(args[i]);
                    if (arg.IndexOf('"') < 0) continue;

                    findings.Add(new LocFinding(LocRule.LiteralArgument, line,
                        $"Loc.{m.Groups[1].Value}의 {i - 1}번째 값 자리에 문자열 리터럴이 있다: " +
                        $"{Trim(args[i])} — 말은 값이 아니다. 표의 한 칸에 통째로 적고 {{n}}으로 자리를 잡아라"));
                }

                if (close < 0) continue;
                InspectNeighbourhood(code, m.Index, close + 1, line, findings);
            }
        }

        /// <summary>
        /// 이 호출을 품은 문장(앞뒤 <c>;</c> <c>{</c> <c>}</c> 사이)에 한글 리터럴이
        /// 또 있으면 조각을 이어 붙인 것이다. <c>+</c>든 <c>Append</c>든 보간이든 마찬가지다.
        /// </summary>
        static void InspectNeighbourhood(string code, int callStart, int callEnd, int line,
                                         List<LocFinding> findings)
        {
            int from = callStart;
            while (from > 0 && code[from - 1] != ';' && code[from - 1] != '{' && code[from - 1] != '}')
                from--;

            int to = callEnd;
            while (to < code.Length && code[to] != ';' && code[to] != '{' && code[to] != '}')
                to++;

            // 호출 자체는 빼고 본다. 이름표(키)는 리터럴이지만 말이 아니다.
            string around = code.Substring(from, callStart - from) +
                            code.Substring(callEnd, to - callEnd);

            foreach (var literal in StringLiterals(around))
            {
                if (!HasHangul(literal.Text)) continue;
                findings.Add(new LocFinding(LocRule.Concatenation, line,
                    $"Loc이 낸 글에 한글 조각 \"{literal.Text}\"를 이어 붙였다 — " +
                    "이러면 번역가가 어순을 바꿀 수 없다. 문장을 통째로 표에 넣어라"));
            }
        }

        // ── ② 화면 코드의 한글 리터럴 ────────────────────────────

        static void InspectKoreanLiterals(string code, List<LocFinding> findings)
        {
            foreach (var literal in StringLiterals(code))
            {
                if (!HasHangul(literal.Text)) continue;
                findings.Add(new LocFinding(LocRule.KoreanLiteral, literal.Line,
                    $"화면 코드에 한글 리터럴이 남아 있다: \"{literal.Text}\""));
            }
        }

        // ── 글줄 다루기 ──────────────────────────────────────────

        static LocCall Describe(string code, Match m, List<string> args)
        {
            var call = new LocCall
            {
                Line = LineOf(code, m.Index),
                IsFormat = m.Groups[1].Value == "F",
                ArgCount = args.Count >= 2 ? args.Count - 2 : 0,
                ArgCountKnown = true,
            };

            if (args.Count >= 2)
            {
                var a0 = LiteralOnly.Match(args[0]);
                var a1 = LiteralOnly.Match(args[1]);
                if (a0.Success && a1.Success)
                {
                    call.NameIsLiteral = true;
                    call.Category = a0.Groups[1].Value;
                    call.Key = a1.Groups[1].Value;
                }
            }

            for (int i = 2; i < args.Count; i++)
                if (args[i].Contains("new object[") || args[i].Contains("new object ["))
                    call.ArgCountKnown = false;

            return call;
        }

        /// <summary>
        /// 여는 괄호 <b>다음</b> 자리에서 시작해 맨 바깥 쉼표로 인자를 가른다.
        /// </summary>
        /// <param name="close">닫는 괄호 자리. 못 찾으면 -1.</param>
        public static List<string> SplitArguments(string code, int start, out int close)
        {
            var args = new List<string>();
            var sb = new StringBuilder();
            int depth = 0;
            int i = start;
            close = -1;

            while (i < code.Length)
            {
                char c = code[i];

                if (c == '"')
                {
                    int end = SkipString(code, i);
                    sb.Append(code, i, end - i);
                    i = end;
                    continue;
                }
                if (c == '\'')
                {
                    int end = SkipChar(code, i);
                    sb.Append(code, i, end - i);
                    i = end;
                    continue;
                }

                if (c == '(' || c == '[' || c == '{') { depth++; sb.Append(c); i++; continue; }
                if (c == ']' || c == '}') { depth--; sb.Append(c); i++; continue; }

                if (c == ')')
                {
                    if (depth == 0)
                    {
                        if (sb.Length > 0 || args.Count > 0) args.Add(sb.ToString());
                        close = i;
                        return args;
                    }
                    depth--;
                    sb.Append(c);
                    i++;
                    continue;
                }

                if (c == ',' && depth == 0)
                {
                    args.Add(sb.ToString());
                    sb.Clear();
                    i++;
                    continue;
                }

                sb.Append(c);
                i++;
            }

            if (sb.Length > 0) args.Add(sb.ToString());
            return args;
        }

        /// <summary>글줄 안의 문자열 리터럴 전부. 값은 따옴표를 벗긴 알맹이다.</summary>
        public static List<(int Line, string Text)> StringLiterals(string code)
        {
            var found = new List<(int, string)>();
            if (string.IsNullOrEmpty(code)) return found;

            int i = 0;
            int line = 1;
            while (i < code.Length)
            {
                char c = code[i];
                if (c == '\n') { line++; i++; continue; }
                if (c == '\'') { i = SkipChar(code, i); continue; }
                if (c != '"') { i++; continue; }

                int end = SkipString(code, i);
                bool verbatim = i > 0 && code[i - 1] == '@';
                int from = i + 1;
                int to = end - 1 >= from ? end - 1 : from;
                string raw = code.Substring(from, to - from);
                found.Add((line, verbatim ? raw.Replace("\"\"", "\"") : raw));

                for (int k = i; k < end; k++) if (code[k] == '\n') line++;
                i = end;
            }
            return found;
        }

        /// <summary>여는 따옴표 자리에서 시작해 닫는 따옴표 <b>다음</b> 자리를 돌려준다.</summary>
        static int SkipString(string code, int at)
        {
            bool verbatim = at > 0 && code[at - 1] == '@';
            int i = at + 1;

            while (i < code.Length)
            {
                char c = code[i];
                if (verbatim)
                {
                    if (c == '"')
                    {
                        if (i + 1 < code.Length && code[i + 1] == '"') { i += 2; continue; }
                        return i + 1;
                    }
                    i++;
                    continue;
                }

                if (c == '\\' && i + 1 < code.Length) { i += 2; continue; }
                if (c == '"') return i + 1;
                if (c == '\n') return i;      // 닫히지 않은 문자열에서 빠져나온다
                i++;
            }
            return code.Length;
        }

        static int SkipChar(string code, int at)
        {
            int i = at + 1;
            while (i < code.Length)
            {
                if (code[i] == '\\' && i + 1 < code.Length) { i += 2; continue; }
                if (code[i] == '\'') return i + 1;
                if (code[i] == '\n') return i;
                i++;
            }
            return code.Length;
        }

        static string StripNestedNames(string arg)
        {
            string previous;
            do
            {
                previous = arg;
                arg = NestedLocName.Replace(arg, "LOCNAME(");
            } while (arg != previous);
            return arg;
        }

        static string BlankMatches(string code, Regex pattern)
        {
            return pattern.Replace(code, m => Blank(m.Value));
        }

        /// <summary>
        /// 여는 괄호부터 짝이 맞는 닫는 괄호까지를 지운다. 정규식으로는 짝을 못 센다.
        /// </summary>
        static string BlankCalls(string code, Regex opening)
        {
            var sb = new StringBuilder(code);
            foreach (Match m in opening.Matches(code))
            {
                SplitArguments(code, m.Index + m.Length, out int close);
                int end = close < 0 ? code.Length : close + 1;
                for (int i = m.Index; i < end; i++)
                    if (sb[i] != '\n') sb[i] = ' ';
            }
            return sb.ToString();
        }

        static string Blank(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s) sb.Append(c == '\n' ? '\n' : ' ');
            return sb.ToString();
        }

        static int LineOf(string text, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < text.Length; i++)
                if (text[i] == '\n') line++;
            return line;
        }

        static string Trim(string s)
        {
            s = s.Trim();
            return s.Length <= 40 ? s : s.Substring(0, 37) + "...";
        }
    }
}
