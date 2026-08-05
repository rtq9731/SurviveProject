using System;
using System.Collections.Generic;
using System.Text;

namespace Survive.Localization
{
    /// <summary>
    /// 의사 번역. 켜면 모든 문자열이 <c>[!! 버섯 목재~~~ !!]</c> 꼴로 부풀어 나온다.
    ///
    /// 두 가지를 한꺼번에 본다.
    /// ① <b>번역 밖에 있는 글자가 드러난다.</b> 화면에 부풀지 않은 한국어가 보이면
    ///    그 자리는 <see cref="Loc"/>을 거치지 않는 하드코딩이다. 214곳을 옮기는 동안
    ///    "다 옮겼는가"를 눈으로 판정할 수 있는 유일한 방법이다.
    /// ② <b>길어졌을 때 판이 깨지는지 미리 본다.</b> 영어·독일어는 한국어보다 길다.
    ///
    /// <b>글자 고르기.</b> 본문 글꼴은 ChosunGu SDF이고 아틀라스가 Dynamic이라
    /// 원본 TTF에 있는 글자만 찍힌다. 실측해 보니 <c>À É Ü Ž Đ « » ‹ › ⟦ ⟧</c>는
    /// <b>없다</b>(두부 □로 뜬다). 흔한 의사 번역 관례인 악센트 라틴 문자를 쓸 수 없어
    /// ASCII <c>[ ] ! ~</c>만 쓴다 — 넷 다 TTF에 있다. 확인 방법은 docs/번역-체계.md에 적어 두었다.
    ///
    /// <b>부풀리는 양.</b> 통례대로 35%. 다만 한국어는 글자 하나가 넓어서 글자 수만
    /// 늘리면 실제 폭 증가를 낮춰 잡는다 — 이 눈금은 "깨지면 보인다"를 위한 것이지
    /// 정확한 예측이 아니다.
    /// </summary>
    public static class PseudoLocalizer
    {
        public const string Prefix = "[!! ";
        public const string Suffix = " !!]";
        public const char Padding = '~';

        /// <summary>글자 수를 이 비율만큼 늘린다.</summary>
        public const float Expansion = 0.35f;

        /// <summary>
        /// 한 줄을 부풀린다. 빈 값은 그대로 둔다 — 빈 자리에 괄호만 뜨면
        /// "번역이 없다"가 아니라 "글자가 깨졌다"로 읽힌다.
        ///
        /// 서식 자리(<c>{0}</c>)는 안쪽에 그대로 남으므로 <see cref="Loc.F"/>가 뒤에 붙여도 된다.
        /// </summary>
        public static string Transform(string source)
        {
            if (string.IsNullOrEmpty(source)) return source;
            if (IsTransformed(source)) return source;   // 두 번 감싸지 않는다

            int pad = Math.Max(1, (int)Math.Round(source.Length * Expansion));

            var sb = new StringBuilder(source.Length + pad + Prefix.Length + Suffix.Length);
            sb.Append(Prefix).Append(source).Append(Padding, pad).Append(Suffix);
            return sb.ToString();
        }

        public static bool IsTransformed(string s) =>
            !string.IsNullOrEmpty(s) &&
            s.StartsWith(Prefix, StringComparison.Ordinal) &&
            s.EndsWith(Suffix, StringComparison.Ordinal);

        /// <summary>
        /// 표 하나를 통째로 부풀려 둔다. 조회할 때마다 변형하면 매 프레임 문자열이
        /// 새로 생기므로, 로케일을 바꾸는 순간 한 번만 만들고 그 뒤로는 사전 조회다.
        /// </summary>
        public static Dictionary<LocKey, string> BuildTable(IReadOnlyDictionary<LocKey, string> source)
        {
            var table = new Dictionary<LocKey, string>(source?.Count ?? 0);
            if (source == null) return table;
            foreach (var pair in source) table[pair.Key] = Transform(pair.Value);
            return table;
        }
    }
}
