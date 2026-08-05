using System.Collections.Generic;
using System.Text;

namespace Survive.Localization
{
    /// <summary>
    /// RFC 4180 표를 읽는다. 번역 표가 CSV인 이유는 <b>사람이 고치고 git이 본다</b>는 것 하나다 —
    /// 에디터 창에서 편집해 내보내는 방식이면 진실이 두 군데(에셋과 CSV)에 생기고,
    /// 번역가에게 보낼 때마다 누가 최신인지를 따져야 한다.
    ///
    /// 직접 쓴 이유: 이 프로젝트에 CSV 의존성이 없고, 필요한 것은 아래 다섯 가지뿐이다.
    /// ① 쉼표가 든 값(한국어 문장에 쉼표는 흔하다) ② 따옴표가 든 값(<c>""</c>로 겹쳐 쓴다)
    /// ③ 줄바꿈이 든 값 ④ CRLF/LF 섞임 ⑤ UTF-8 BOM.
    ///
    /// RFC에서 벗어난 것이 둘 있다. <b>빈 줄은 레코드로 세지 않고</b>,
    /// <b><c>#</c>로 시작하는 줄은 주석</b>이다. 둘 다 사람이 표를 구역별로 나눠
    /// 읽기 좋게 두려고 허용했다. 값 안의 <c>#</c>는 영향이 없다 — 줄 첫 글자만 본다.
    /// </summary>
    public static class Csv
    {
        /// <summary>줄 첫 글자가 이것이면 그 줄 전체가 주석이다.</summary>
        public const char CommentMark = '#';

        /// <summary>UTF-8 BOM(U+FEFF). 엑셀이 저장하면 붙는다.</summary>
        public const char ByteOrderMark = (char)0xFEFF;

        /// <summary>
        /// 표를 레코드 목록으로 읽는다. 칸 수는 줄마다 다를 수 있으니 부르는 쪽이 검사한다.
        /// </summary>
        /// <param name="lineNumbers">
        /// 채워 주면 각 레코드가 파일 몇 번째 줄에서 시작했는지 담는다(1부터).
        /// 주석·빈 줄을 건너뛰기 때문에 레코드 번호와 줄 번호가 어긋나는데,
        /// 오류 보고에 레코드 번호를 적으면 사람이 파일에서 그 줄을 못 찾는다.
        /// </param>
        public static List<string[]> Parse(string text, List<int> lineNumbers = null)
        {
            var rows = new List<string[]>();
            if (string.IsNullOrEmpty(text)) return rows;

            // BOM은 첫 칸 이름에 눌어붙어 "Category"를 못 알아보게 만든다.
            int i = text[0] == ByteOrderMark ? 1 : 0;

            var field = new StringBuilder();
            var row = new List<string>();
            bool quoted = false;
            bool atRecordStart = true;
            int line = 1;
            int recordLine = 1;

            while (i < text.Length)
            {
                if (atRecordStart)
                {
                    char s = text[i];
                    if (s == '\r')
                    {
                        i++;
                        if (i < text.Length && text[i] == '\n') i++;
                        line++;
                        continue;
                    }
                    if (s == '\n') { i++; line++; continue; }
                    if (s == CommentMark)
                    {
                        while (i < text.Length && text[i] != '\n') i++;
                        continue;   // 다음 바퀴에서 '\n'을 만나 줄을 센다
                    }
                    atRecordStart = false;
                    recordLine = line;
                }

                char c = text[i];

                if (quoted)
                {
                    if (c == '"')
                    {
                        // 따옴표 두 개는 따옴표 한 개다. 닫는 것과 구별되는 유일한 신호.
                        if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i += 2; continue; }
                        quoted = false;
                        i++;
                        continue;
                    }
                    if (c == '\n') line++;
                    field.Append(c);
                    i++;
                    continue;
                }

                // 여는 따옴표는 칸 맨 앞에서만 인정한다. 값 중간의 따옴표는 글자다.
                if (c == '"' && field.Length == 0) { quoted = true; i++; continue; }

                if (c == ',') { row.Add(field.ToString()); field.Clear(); i++; continue; }

                if (c == '\r' || c == '\n')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    i++;
                    line++;
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row.ToArray());
                    lineNumbers?.Add(recordLine);
                    row.Clear();
                    atRecordStart = true;
                    continue;
                }

                field.Append(c);
                i++;
            }

            // 파일이 줄바꿈으로 끝나면 atRecordStart가 참이라 빈 레코드가 생기지 않는다.
            if (!atRecordStart)
            {
                row.Add(field.ToString());
                rows.Add(row.ToArray());
                lineNumbers?.Add(recordLine);
            }

            return rows;
        }

        /// <summary>
        /// 값 하나를 CSV 칸으로 감싼다. 표를 코드로 다시 쓸 일은 아직 없지만,
        /// 파서가 무엇을 되돌려 읽을 수 있어야 하는지를 한 곳에 적어 둔다.
        /// </summary>
        public static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            bool needs = value.IndexOf(',') >= 0 || value.IndexOf('"') >= 0 ||
                         value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0 ||
                         value[0] == ' ' || value[value.Length - 1] == ' ';
            if (!needs) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
