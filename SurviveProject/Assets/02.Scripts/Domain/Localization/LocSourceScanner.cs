using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Survive.Localization
{
    /// <summary>
    /// C# 글줄에서 <c>Loc.T("Cat","key")</c> 호출을 긁어낸다.
    ///
    /// <b>왜 있는가.</b> 번역 층에서 제일 무서운 고장은 "키를 오타 냈다"이고,
    /// 그것은 실행해서 그 화면을 열어 보기 전까지 아무 신호도 내지 않는다.
    /// 코드가 부르는 키와 표에 있는 키를 EditMode에서 대조하면 컴파일 직후에 잡힌다.
    ///
    /// <b>한계.</b> 리터럴 두 개로 부른 자리만 검사할 수 있다. 변수로 만든 키
    /// (<c>Loc.T("Item", id + ".name")</c>)는 여기서 판정할 수 없고,
    /// <see cref="Result.DynamicLines"/>에 세어 두기만 한다. 그런 자리가 늘어나면
    /// 게이트의 사각지대가 넓어진다는 뜻이라, 세는 것 자체가 신호다.
    /// </summary>
    public static class LocSourceScanner
    {
        public readonly struct Call
        {
            public readonly string Category;
            public readonly string Key;
            public readonly int Line;

            public Call(string category, string key, int line)
            {
                Category = category;
                Key = key;
                Line = line;
            }

            public LocKey AsKey() => new LocKey(Category, Key);
        }

        public sealed class Result
        {
            /// <summary>리터럴 두 개로 부른 자리.</summary>
            public readonly List<Call> Literals = new List<Call>();

            /// <summary>리터럴이 아닌 인자로 부른 자리의 줄 번호. 검사할 수 없는 자리다.</summary>
            public readonly List<int> DynamicLines = new List<int>();
        }

        // 주석을 지운 뒤의 글줄에서 찾는다. Loc.T / Loc.F 어느 쪽이든 앞 두 인자가 이름표다.
        static readonly Regex CallSite =
            new Regex(@"\bLoc\s*\.\s*[TF]\s*\(", RegexOptions.Compiled);

        static readonly Regex LiteralCall =
            new Regex(@"\bLoc\s*\.\s*[TF]\s*\(\s*""([^""\\]*)""\s*,\s*""([^""\\]*)""\s*[,)]",
                      RegexOptions.Compiled);

        public static Result Scan(string source)
        {
            var result = new Result();
            if (string.IsNullOrEmpty(source)) return result;

            string code = StripComments(source);

            var literalAt = new Dictionary<int, Match>();
            foreach (Match m in LiteralCall.Matches(code)) literalAt[m.Index] = m;

            foreach (Match site in CallSite.Matches(code))
            {
                int line = LineOf(code, site.Index);
                if (literalAt.TryGetValue(site.Index, out var lit))
                    result.Literals.Add(new Call(lit.Groups[1].Value, lit.Groups[2].Value, line));
                else
                    result.DynamicLines.Add(line);
            }

            return result;
        }

        static int LineOf(string text, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < text.Length; i++)
                if (text[i] == '\n') line++;
            return line;
        }

        /// <summary>
        /// 주석만 공백으로 지운다. 문자열 리터럴은 그대로 둔다(거기 키가 들어 있다).
        /// 줄바꿈은 남겨야 줄 번호가 어긋나지 않는다.
        ///
        /// 주석을 지우지 않으면 <c>/// Loc.T("UI","예시")</c> 같은 문서 주석이
        /// 진짜 호출로 세어져, 있지도 않은 키를 표에 넣으라고 요구하게 된다.
        /// </summary>
        public static string StripComments(string source)
        {
            if (string.IsNullOrEmpty(source)) return source ?? "";

            var sb = new StringBuilder(source.Length);
            int i = 0;

            while (i < source.Length)
            {
                char c = source[i];

                // 줄 주석
                if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
                {
                    while (i < source.Length && source[i] != '\n') { sb.Append(' '); i++; }
                    continue;
                }

                // 블록 주석
                if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
                {
                    sb.Append("  ");
                    i += 2;
                    while (i < source.Length && !(source[i] == '*' && i + 1 < source.Length && source[i + 1] == '/'))
                    {
                        sb.Append(source[i] == '\n' ? '\n' : ' ');
                        i++;
                    }
                    if (i < source.Length) { sb.Append("  "); i += 2; }
                    continue;
                }

                // 축자 문자열 @"..." — 안에서 ""는 따옴표 한 개다
                if (c == '@' && i + 1 < source.Length && source[i + 1] == '"')
                {
                    sb.Append(c).Append('"');
                    i += 2;
                    while (i < source.Length)
                    {
                        if (source[i] == '"')
                        {
                            if (i + 1 < source.Length && source[i + 1] == '"') { sb.Append("\"\""); i += 2; continue; }
                            sb.Append('"'); i++; break;
                        }
                        sb.Append(source[i]); i++;
                    }
                    continue;
                }

                // 보통 문자열 "..." (보간 문자열 $"..."도 여기로 들어온다)
                if (c == '"')
                {
                    sb.Append(c); i++;
                    while (i < source.Length)
                    {
                        if (source[i] == '\\' && i + 1 < source.Length)
                        {
                            sb.Append(source[i]).Append(source[i + 1]); i += 2; continue;
                        }
                        sb.Append(source[i]);
                        if (source[i] == '"') { i++; break; }
                        if (source[i] == '\n') { i++; break; }   // 닫히지 않은 문자열에서 빠져나온다
                        i++;
                    }
                    continue;
                }

                // 문자 리터럴 '/' 처럼 슬래시를 담은 것이 주석으로 오인되면 안 된다
                if (c == '\'')
                {
                    sb.Append(c); i++;
                    while (i < source.Length)
                    {
                        if (source[i] == '\\' && i + 1 < source.Length)
                        {
                            sb.Append(source[i]).Append(source[i + 1]); i += 2; continue;
                        }
                        sb.Append(source[i]);
                        if (source[i] == '\'') { i++; break; }
                        if (source[i] == '\n') { i++; break; }
                        i++;
                    }
                    continue;
                }

                sb.Append(c);
                i++;
            }

            return sb.ToString();
        }
    }
}
