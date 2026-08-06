using System;

namespace Survive.Localization
{
    /// <summary>
    /// 조사 한 짝. 표에 어느 꼴로 적혀도 같은 짝으로 모인다 —
    /// <c>{0}을</c>이라고 적든 <c>{0}를</c>이라고 적든 이 짝 하나가 된다.
    /// </summary>
    public readonly struct ParticlePair
    {
        /// <summary>앞말에 받침이 있을 때 쓰는 꼴. 표에 이 꼴로 적을 수 있다.</summary>
        public readonly string WithFinal;

        /// <summary>앞말에 받침이 없을 때 쓰는 꼴. 표에 이 꼴로 적을 수 있다.</summary>
        public readonly string WithoutFinal;

        public ParticlePair(string withFinal, string withoutFinal)
        {
            WithFinal = withFinal;
            WithoutFinal = withoutFinal;
        }

        /// <summary>두 꼴을 나란히 — <c>을(를)</c>. 지금 화면에 나가는 꼴이다.</summary>
        public string BothForms => WithFinal + "(" + WithoutFinal + ")";

        public override string ToString() => BothForms;
    }

    /// <summary>
    /// 자리표에 값을 넣은 <b>직후</b>, 그 뒤에 붙은 조사를 무엇으로 쓸지 정한다.
    ///
    /// <b>이 인터페이스가 존재하는 이유가 하나뿐이다.</b> 지금은 두 꼴을 나란히
    /// 내보내지만(<c>을(를)</c>), 받침을 보고 하나만 고르는 것이 <b>정해진 다음 단계</b>다.
    /// 그때 고칠 곳이 <see cref="Resolve"/> 몸통 하나가 되도록, 오늘부터
    /// <b>앞에 들어간 값을 받아 둔다</b>. 값이 인자에 없으면 그날 호출부를 전부 고쳐야 한다.
    /// </summary>
    public interface IParticleResolver
    {
        /// <summary>
        /// 조사를 화면에 나갈 꼴로 정한다.
        /// </summary>
        /// <param name="precedingValue">
        /// 자리표에 방금 들어간 값. <b>오늘은 쓰지 않는다</b> —
        /// 받침으로 고르게 되는 날 이 값이 필요해서 미리 받아 둔 자리다.
        /// </param>
        /// <param name="pair">표에 적힌 조사가 속한 짝.</param>
        string Resolve(string precedingValue, ParticlePair pair);
    }

    /// <summary>
    /// 한국어 조사 하나만 맡는 자리. Unity를 모른다.
    ///
    /// <b>무엇을 하는가.</b> 표에 자연스럽게 적힌 조사를 화면에 나갈 꼴로 바꾼다.
    /// <code>
    /// 표   : {0}을 만들려면 {1}이 {2}개 필요합니다.
    /// 화면 : 스크랩을(를) 만들려면 도끼이(가) 3개 필요합니다.
    /// </code>
    /// 표를 쓰는 사람은 <b>표시자를 배울 필요가 없다</b>. <c>{0}을</c>이라고 적든
    /// <c>{0}를</c>이라고 적든 결과가 같다. 어느 쪽으로 적어도 되는 것이 요점이다.
    ///
    /// <b>지금 두 꼴을 나란히 내보내는 것은 사용자 결정이다</b>("보통 이(가) 을(를) 해야지").
    /// 한국어 소프트웨어의 오랜 통례이고, 무엇보다 절대 틀리지 않는다.
    /// <b>다만 받침으로 하나를 고르는 것이 확정된 다음 단계다</b> — 미정이 아니다.
    ///
    /// <b>그날 고칠 곳은 <see cref="BothFormsResolver.Resolve"/> 몸통 하나뿐이다.</b>
    /// 그때 필요한 규칙까지 미리 조사해 두었다(docs/번역-체계.md에도 적어 두었다):
    /// <list type="bullet">
    /// <item>종성 = <c>(코드 - 0xAC00) % 28</c>, 0이면 받침 없음</item>
    /// <item><c>으로/로</c>는 예외 — 받침이 <b>ㄹ</b>(종성 8)이면 "로"를 쓴다 ("칼로", "물로")</item>
    /// <item>숫자로 끝나면 읽는 소리를 따른다 — 1·3·6·7·8·0은 받침 있음, 2·4·5·9는 없음</item>
    /// <item>라틴 문자·기호로 끝나면 정답이 없다. 두 꼴 나란히로 물러서는 것이 안전하다</item>
    /// </list>
    ///
    /// <b>값을 넣는 그 자리에서 정한다.</b> 틀의 <c>{0}을</c>을 미리 <c>{0}을(를)</c>로
    /// 바꿔 두는 방식이 아니다 — 그러면 이 모듈이 값을 영영 못 보고, 받침 판정으로
    /// 올리는 날 구조를 통째로 뜯어야 한다. <see cref="LocFormat"/>이 자리표를 채우면서
    /// 방금 넣은 값과 함께 <see cref="IParticleResolver.Resolve"/>를 부른다.
    ///
    /// 반대로 <b>이미 채워진 결과 문자열</b>에 이 처리를 돌리는 것도 안 된다.
    /// 어디까지가 값이었는지 알 수 없고, 값 자체가 "이"로 끝나면(예: "고사리") 오작동한다.
    ///
    /// <b>한국어에서만 돈다.</b> <see cref="IsKoreanLocale"/>이 아닌 로케일에서는
    /// <see cref="LocFormat"/>이 해석기를 아예 넘기지 않아 표에 적힌 글자가 그대로 나간다.
    /// </summary>
    public static class KoreanParticles
    {
        /// <summary>
        /// 다루는 짝 여섯. <b>긴 것이 앞에 있어야 한다</b> — <c>으로</c>를 먼저 보지 않으면
        /// <c>{0}으로</c>에서 <c>으</c>를 남기고 뒤의 <c>로</c>만 잡는다.
        /// </summary>
        public static readonly ParticlePair[] Pairs =
        {
            new ParticlePair("으로", "로"),
            new ParticlePair("을", "를"),
            new ParticlePair("이", "가"),
            new ParticlePair("은", "는"),
            new ParticlePair("와", "과"),
            new ParticlePair("아", "야"),
        };

        /// <summary>
        /// 두 꼴을 나란히 내놓는 해석기. <b>오늘의 기본값이자, 언젠가 바뀔 한 곳.</b>
        /// </summary>
        public sealed class BothFormsResolver : IParticleResolver
        {
            /// <summary>
            /// <paramref name="precedingValue"/>는 <b>일부러 쓰지 않는다</b>.
            /// 받침으로 고르게 되는 날 여기서 쓰게 되고, 그날 호출부는 한 글자도
            /// 바뀌지 않아야 한다. 쓰지 않는 인자를 받는 이유가 그것뿐이다.
            /// </summary>
            public string Resolve(string precedingValue, ParticlePair pair) => pair.BothForms;
        }

        /// <summary>기본 해석기. 상태가 없어 하나를 돌려 쓴다.</summary>
        public static readonly IParticleResolver Standard = new BothFormsResolver();

        /// <summary>이 로케일에서 조사 처리를 돌리는가.</summary>
        public static bool IsKoreanLocale(string locale)
        {
            if (string.IsNullOrEmpty(locale)) return false;
            if (locale.Equals("ko", StringComparison.OrdinalIgnoreCase)) return true;
            return locale.StartsWith("ko-", StringComparison.OrdinalIgnoreCase) ||
                   locale.StartsWith("ko_", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// <paramref name="pos"/>에서 조사가 시작하는가.
        ///
        /// <b>부르는 쪽이 지켜야 할 것</b>: <paramref name="pos"/>는 <c>{n}</c>이 끝난
        /// 바로 다음이어야 한다. 자리표와 무관한 자리에서 부르면 평범한 글이 망가진다.
        ///
        /// 조사로 인정하려면 <b>그 조사 다음이 공백·문장부호·문자열 끝</b>이어야 한다.
        /// 이 조건이 없으면 <c>{0}이라고 부른다</c>가 <c>이(가)라고</c>가 되고,
        /// <c>{0}은하계</c>·<c>{0}로서</c>도 같이 무너진다.
        /// </summary>
        /// <returns>조사면 글자 수(1~2), 아니면 0.</returns>
        public static int MatchAt(string text, int pos, out ParticlePair pair)
        {
            pair = default;
            if (string.IsNullOrEmpty(text) || pos < 0 || pos >= text.Length) return 0;

            foreach (var p in Pairs)
            {
                int len = MatchOne(text, pos, p.WithFinal);
                if (len == 0) len = MatchOne(text, pos, p.WithoutFinal);
                if (len == 0) continue;

                pair = p;
                return len;
            }
            return 0;
        }

        static int MatchOne(string s, int pos, string particle)
        {
            if (pos + particle.Length > s.Length) return 0;
            for (int k = 0; k < particle.Length; k++)
                if (s[pos + k] != particle[k]) return 0;
            return IsBoundary(s, pos + particle.Length) ? particle.Length : 0;
        }

        /// <summary>
        /// 조사가 여기서 끝나도 되는가. 끝나도 되는 것은 문자열 끝·공백·문장부호뿐이다.
        /// 글자가 이어지면 그것은 조사가 아니라 낱말의 일부다.
        /// </summary>
        static bool IsBoundary(string s, int index)
        {
            if (index >= s.Length) return true;
            char c = s[index];
            return char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c);
        }
    }
}
