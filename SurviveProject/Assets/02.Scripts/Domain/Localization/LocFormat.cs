using System.Collections.Generic;
using System.Text;

namespace Survive.Localization
{
    /// <summary>
    /// 서식 인자 묶음. 값이 하나둘일 때 배열을 새로 만들지 않으려고 구조체로 둔다 —
    /// 제작 화면은 열려 있는 동안 매 프레임 목록을 다시 그린다.
    /// </summary>
    public readonly struct LocArgs
    {
        readonly object _a0, _a1, _a2;
        readonly object[] _more;
        readonly int _count;

        LocArgs(object a0, object a1, object a2, object[] more, int count)
        {
            _a0 = a0; _a1 = a1; _a2 = a2; _more = more; _count = count;
        }

        public static LocArgs Of(object a0) => new LocArgs(a0, null, null, null, 1);
        public static LocArgs Of(object a0, object a1) => new LocArgs(a0, a1, null, null, 2);
        public static LocArgs Of(object a0, object a1, object a2) => new LocArgs(a0, a1, a2, null, 3);

        public static LocArgs Of(object[] args) =>
            args == null || args.Length == 0
                ? new LocArgs(null, null, null, null, 0)
                : new LocArgs(null, null, null, args, args.Length);

        public int Count => _count;

        public object this[int i]
        {
            get
            {
                if (i < 0 || i >= _count) return null;
                if (_more != null) return _more[i];
                return i == 0 ? _a0 : i == 1 ? _a1 : _a2;
            }
        }
    }

    /// <summary>
    /// 자리표 <c>{0} {1} {2}</c>에 값을 끼운다. 번역 층의 서식 규약이 여기 전부 있다.
    ///
    /// <b>왜 <c>string.Format</c>이 아닌가.</b> 셋 때문이다.
    /// <list type="number">
    /// <item><b>절대 예외를 던지지 않는다.</b> 인자가 모자라도, 남아도, 중괄호가
    ///       짝이 안 맞아도 던지지 않는다. 번역가의 오타로 게임이 죽으면 안 된다 —
    ///       대신 EditMode 게이트가 잡는다.</item>
    /// <item><b>조사를 값과 함께 정한다.</b> 자리표를 채우는 그 순간 방금 넣은 값을
    ///       알고 있으므로, 바로 뒤에 붙은 조사를 <see cref="IParticleResolver"/>에
    ///       값과 함께 넘길 수 있다. <c>string.Format</c>으로는 그 자리가 없다.</item>
    /// <item><b>게이트가 자리표를 셀 수 있다.</b> <see cref="Indices"/>가 표의 값과
    ///       호출부 인자 개수를 대조하는 근거다.</item>
    /// </list>
    ///
    /// <b>규약.</b>
    /// <list type="bullet">
    /// <item>자리표는 <c>{0}</c>부터 <b>0에서 시작해 빠짐없이</b> 이어진다.</item>
    /// <item><b>순서를 마음대로 바꿀 수 있다.</b> <c>{1} {0}</c>도, 같은 자리를 두 번
    ///       쓰는 <c>{0}과 {0}</c>도 된다. 어순은 언어마다 다르므로 이것이 요점이다.</item>
    /// <item>중괄호 자체를 쓰려면 <c>{{</c> <c>}}</c>로 겹쳐 쓴다.</item>
    /// <item>인자가 모자라면 그 자리표를 <b>글자 그대로</b> 남긴다. 화면에 <c>{2}</c>가
    ///       보이면 어디가 고장 났는지 바로 보인다 — 빈칸으로 지우면 아무도 모른다.</item>
    /// </list>
    /// </summary>
    public static class LocFormat
    {
        /// <summary>
        /// 값을 끼운다. <paramref name="particles"/>가 null이 아니면 자리표 바로 뒤에
        /// 붙은 조사를 방금 넣은 값과 함께 해석한다(한국어에서만 넘어온다).
        /// </summary>
        public static string Apply(string format, LocArgs args, IParticleResolver particles = null)
        {
            if (string.IsNullOrEmpty(format)) return format ?? "";
            if (format.IndexOf('{') < 0 && format.IndexOf('}') < 0) return format;

            var sb = new StringBuilder(format.Length + 16);
            int i = 0;

            while (i < format.Length)
            {
                char c = format[i];

                if (c == '{')
                {
                    if (i + 1 < format.Length && format[i + 1] == '{') { sb.Append('{'); i += 2; continue; }

                    if (!TryReadIndex(format, i, out int index, out int after))
                    {
                        // {abc} 처럼 자리표가 아닌 것. 글자로 본다.
                        sb.Append('{');
                        i++;
                        continue;
                    }

                    if (index >= args.Count)
                    {
                        // 인자가 모자란다. 자리표를 그대로 남겨 눈에 띄게 둔다.
                        sb.Append(format, i, after - i);
                        i = after;
                        continue;
                    }

                    string value = Text(args[index]);
                    sb.Append(value);
                    i = after;

                    if (particles != null)
                    {
                        int len = KoreanParticles.MatchAt(format, i, out var pair);
                        if (len > 0)
                        {
                            // 값을 넘긴다. 오늘은 쓰이지 않지만 받침 판정으로 올리는 날
                            // 이 자리가 그대로 쓰인다 (IParticleResolver 참조).
                            sb.Append(particles.Resolve(value, pair));
                            i += len;
                        }
                    }
                    continue;
                }

                if (c == '}' && i + 1 < format.Length && format[i + 1] == '}')
                {
                    sb.Append('}');
                    i += 2;
                    continue;
                }

                sb.Append(c);
                i++;
            }

            return sb.ToString();
        }

        /// <summary>null은 빈 글자다. "null"이라고 적히면 그것이 더 나쁜 화면이다.</summary>
        static string Text(object value) => value?.ToString() ?? "";

        // ── 게이트가 보는 것 ─────────────────────────────────────

        /// <summary>이 틀에 나오는 자리표 번호. <b>나온 순서 그대로, 중복 포함.</b></summary>
        public static List<int> Indices(string format)
        {
            var list = new List<int>();
            if (string.IsNullOrEmpty(format)) return list;

            int i = 0;
            while (i < format.Length)
            {
                if (format[i] != '{') { i++; continue; }
                if (i + 1 < format.Length && format[i + 1] == '{') { i += 2; continue; }

                if (TryReadIndex(format, i, out int index, out int after)) { list.Add(index); i = after; }
                else i++;
            }
            return list;
        }

        /// <summary>이 틀이 요구하는 인자 개수. 자리표가 없으면 0.</summary>
        public static int RequiredArgCount(string format)
        {
            int max = -1;
            foreach (int n in Indices(format)) if (n > max) max = n;
            return max + 1;
        }

        /// <summary>
        /// 자리표 번호가 0부터 빠짐없이 이어지는가.
        /// <c>{0} {2}</c>는 <c>{1}</c>이 사라진 것이라 여기서 걸린다.
        /// </summary>
        public static bool IsContiguousFromZero(string format, out List<int> missing)
        {
            missing = new List<int>();
            var seen = new HashSet<int>(Indices(format));
            if (seen.Count == 0) return true;

            int max = 0;
            foreach (int n in seen) if (n > max) max = n;

            for (int n = 0; n <= max; n++) if (!seen.Contains(n)) missing.Add(n);
            return missing.Count == 0;
        }

        /// <summary>
        /// <paramref name="at"/>이 <c>{숫자}</c>의 시작인가.
        /// </summary>
        /// <param name="after">맞으면 닫는 괄호 <b>다음</b> 자리.</param>
        public static bool TryReadIndex(string s, int at, out int index, out int after)
        {
            index = 0;
            after = at;
            if (string.IsNullOrEmpty(s) || at < 0 || at >= s.Length || s[at] != '{') return false;

            int j = at + 1;
            int value = 0;
            int digits = 0;
            while (j < s.Length && s[j] >= '0' && s[j] <= '9')
            {
                value = value * 10 + (s[j] - '0');
                if (value > 999) return false;    // 자리표 번호가 세 자리를 넘을 일은 없다
                digits++;
                j++;
            }
            if (digits == 0 || j >= s.Length || s[j] != '}') return false;

            index = value;
            after = j + 1;
            return true;
        }
    }
}
