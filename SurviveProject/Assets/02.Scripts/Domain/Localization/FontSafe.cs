using System.Text;

namespace Survive.Localization
{
    /// <summary>
    /// 본문 글꼴에 없어 화면에서 두부(□)로 뜨는 글자들, 그리고 그 대신 쓸 글자.
    ///
    /// 본문 글꼴은 <c>Assets/07.Fonts/TMP/ChosunGu SDF.asset</c>이고 아틀라스가 Dynamic이다.
    /// 즉 원본 TTF에 있는 글자만 찍히고, 없으면 자리에 네모가 남는다. 목록은
    /// <c>TMP_FontAsset.HasCharacter(c, false, true)</c>로 실측한 것이다(2026-08-06).
    ///
    /// <b>왜 대치표까지 두는가.</b> 데이터 에셋에 이미 줄표(—)가 들어간 자리가 있다.
    /// 에셋의 한국어 원문은 손대지 않는 것이 이번 라운드의 규칙이라, 대신
    /// <b>표가 바로잡는다</b> — 번역 표는 덮어쓰는 층이므로 이런 고침에 딱 맞는 자리다.
    /// 에셋 쪽을 고칠 수 있게 되면 그때 표의 값과 원문이 저절로 맞아떨어진다.
    /// </summary>
    public static class FontSafe
    {
        /// <summary>ChosunGu에 없는 글자들. 게이트가 이 목록으로 표를 검사한다.</summary>
        public const string Missing = "—«»‹›⟦⟧ÀÉÜŽĐ−";

        /// <summary>
        /// 대신 쓸 글자. 짝이 맞지 않는 <see cref="Missing"/> 글자는 그대로 남고,
        /// 그러면 게이트가 실패해 사람이 판단하게 된다 — 아무 글자나 밀어 넣는 것보다 낫다.
        ///
        /// 줄표를 가운뎃점으로 바꾸는 것은 이 저장소가 이미 쓰던 방식이다
        /// (<c>HarvestNode</c>의 프롬프트, <c>MenuListing</c>의 주석).
        /// </summary>
        const string MissingWithSubstitute = "—«»‹›−";
        const string Substitutes /*        */ = "·\"\"\"\"-";

        public static bool HasMissing(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;

            for (int i = 0; i < s.Length; i++)
                if (Missing.IndexOf(s[i]) >= 0) return true;
            return false;
        }

        /// <summary>
        /// 글꼴에 없는 글자를 대신할 것으로 바꾼다. 바꿀 것이 없으면 <b>같은 참조</b>를
        /// 그대로 돌려준다 — 부르는 쪽이 "바뀌었는가"를 참조 비교로 알 수 있다.
        /// </summary>
        public static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;

            StringBuilder sb = null;
            for (int i = 0; i < s.Length; i++)
            {
                int at = MissingWithSubstitute.IndexOf(s[i]);
                if (at < 0)
                {
                    sb?.Append(s[i]);
                    continue;
                }

                if (sb == null) sb = new StringBuilder(s, 0, i, s.Length);
                sb.Append(Substitutes[at]);
            }

            return sb == null ? s : sb.ToString();
        }
    }
}
