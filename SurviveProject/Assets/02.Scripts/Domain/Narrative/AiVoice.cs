using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Survive.Narrative
{
    /// <summary>
    /// 우주복 AI의 말을 <b>기계가 셀 수 있는 부분</b>만 담는 자리 (스펙 §13).
    ///
    /// <b>왜 규격이 아니라 계산기인가.</b> 문체 규격 자체(무엇이 금지어인가, 어떤
    /// 정형구로 닫는가)는 <c>AiSpeechGateTests</c>에 있다. 규격은 <b>정책</b>이고
    /// 여기 있는 것은 그 정책이 쓰는 <b>자</b>다 — "이 글은 몇 문장인가",
    /// "그림글자가 섞였는가". 자를 정책과 같은 파일에 두면 자를 시험할 수 없다.
    ///
    /// 금지어를 여기 적지 않는 이유는 하나 더 있다. 이 파일은 <c>Assets/02.Scripts</c>
    /// 아래라 다른 훑개들이 지나다니는 길목이고, 금지어를 본문에 적어 두면
    /// 그 낱말이 저장소 안에 되살아나 다음 사람이 복사해 간다. 정책은 조각으로
    /// 지어 시험 쪽에 둔다.
    /// </summary>
    public static class AiVoice
    {
        /// <summary>
        /// 세 문장. <see cref="Survive.Progression.DiscoverySO"/>의 문서 주석이 원본이다 —
        /// "효율이 기조다. 수식어·접속사를 덜어내고 세 문장을 넘기지 않는다."
        /// </summary>
        public const int MaxSentences = 3;

        /// <summary>
        /// 문장을 끊는 것들. <b>말줄임표도 문장을 끝낸다.</b>
        ///
        /// 규격의 첫 문장은 <i>"[성질 묘사] 분석..."</i>이고 그 말줄임표는 아직 처리
        /// 중이라는 표시다 — 곧 <b>거기서 한 문장이 끝난다</b>. 마침표만 세면 그 줄은
        /// 두 문장으로 잡히고, 그러면 네 문장짜리 대사가 조용히 통과한다.
        ///
        /// 점 여러 개는 하나로 친다(<c>+</c>). 안 그러면 <c>...</c> 하나가 세 문장이 된다.
        /// </summary>
        static readonly Regex 문장끝 = new Regex(@"[.!?…]+");

        /// <summary>글을 문장으로 자른다. 빈 조각(끝의 마침표 뒤)은 버린다.</summary>
        public static IReadOnlyList<string> SplitSentences(string text)
        {
            var 문장들 = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return 문장들;

            foreach (var 조각 in 문장끝.Split(text))
            {
                var 다듬은것 = 조각.Trim();
                if (다듬은것.Length > 0) 문장들.Add(다듬은것);
            }
            return 문장들;
        }

        /// <summary>몇 문장인가.</summary>
        public static int SentenceCount(string text) => SplitSentences(text).Count;

        /// <summary>
        /// 그림글자(이모지)가 섞였는가. 규격의 금지 항목이다.
        ///
        /// <b>화살표는 여기서 보지 않는다.</b> <c>→</c>는 그림글자이기 이전에
        /// <b>방향 지시</b>라, 낫 경고가 방향을 흘리는지 보는 쪽(게이트의 방향
        /// 지시어 목록)에서 잡아야 실패 문구가 옳은 말을 한다.
        ///
        /// 가운뎃점(·)이나 <c>$#@%</c> 같은 문장 부호는 그림글자가 아니다 —
        /// 프롤로그의 "$#@%급 모래 폭풍"이 여기 걸리면 안 된다.
        /// </summary>
        public static bool HasEmoji(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                // 대리쌍으로만 적히는 구간(U+1F000~U+1FAFF)이 이모지의 본진이다.
                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    int cp = char.ConvertToUtf32(c, text[i + 1]);
                    if (cp >= 0x1F000 && cp <= 0x1FAFF) return true;
                    i++;
                    continue;
                }

                // 기본 다국어 평면에 흩어져 있는 것들. 잡동사니 기호·장식 기호와
                // 이모지 변형 선택자.
                if (c >= 0x2600 && c <= 0x27BF) return true;   // 잡기호·장식
                if (c >= 0x2B00 && c <= 0x2BFF) return true;   // 잡기호와 화살표
                if (c == 0xFE0F || c == 0x20E3) return true;   // 이모지 변형 선택자와 키캡
                if (c == 0x2122 || c == 0x00A9 || c == 0x00AE) return true;   // 상표·저작권 기호
            }

            return false;
        }
    }
}
