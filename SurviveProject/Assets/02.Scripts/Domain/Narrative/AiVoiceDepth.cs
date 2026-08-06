using Survive.Domain.Art;

namespace Survive.Narrative
{
    /// <summary>
    /// 깊이 내려갈수록 AI의 말수가 준다 (기획서 §12 · 스펙 §13).
    ///
    /// <b>왜 말수가 주는가.</b> 침묵은 연출이 아니라 <b>정보의 결핍</b>이다. 우주복 AI가
    /// 아는 것은 MARSO가 아니라 지구의 자료이고, 아래로 갈수록 그 자료가 닿지 않는 것이
    /// 늘어난다. 초반의 수다와 심부의 침묵은 같은 인물의 기분 변화가 아니라
    /// <b>같은 규칙의 양 끝</b>이다 — 그래서 이것은 대사가 아니라 함수여야 한다.
    /// 종막에서 그 AI가 무엇이었는지 드러날 때, 말수 곡선이 대조군이 된다.
    ///
    /// <b>깊이 사다리는 새로 만들지 않았다.</b> 이 게임에서 "얼마나 내려왔는가"를 이미
    /// 답하고 있는 것은 <see cref="DepthFog.Bands"/>다("깊이가 곧 자홍의 농도다").
    /// 사다리를 하나 더 세우면 화면 색과 말수가 서로 다른 깊이를 가리키는 날이 오고,
    /// 그날 어느 쪽이 옳은지 아무도 모른다. 밴드가 바뀌면 말수도 따라 움직이는 것이 맞다.
    ///
    /// <b>아직 아무도 이 값을 읽지 않는다.</b> 말을 걸 채널(<c>UnlockService.Announce</c>)은
    /// 원장에 걸린 한 번짜리 알림이라 빈도라는 개념이 없다. 반복 발화 채널이 생기는
    /// 날 이 함수가 그 채널의 문지기가 된다. 그때까지 여기 있는 것은 <b>규칙과
    /// 그 규칙을 지키는 시험</b>뿐이고, 그것이 이 파일의 목적이다.
    /// </summary>
    public static class AiVoiceDepth
    {
        /// <summary>
        /// 단계별로 1분에 몇 번까지 말하는가. <see cref="DepthFog.Bands"/>와 같은 순서
        /// (위 → 아래)이고 <b>반드시 줄어들기만 한다.</b>
        ///
        /// <b>이 숫자들은 손잡이다.</b> 확정된 것은 규칙(단조 감소 · 맨 아래는 침묵)이지
        /// 값이 아니다 — 반복 발화 채널이 생겨 실제로 들어 볼 수 있게 되면 그때
        /// 다시 잰다. 다만 아무렇게나 고칠 수는 없다:
        /// <see cref="ValidateTable"/>가 규칙을 지키는지 보고, 어기면 시험이 무너진다.
        ///
        /// 지금 값의 뜻: 섬 위에서는 10초에 한 번까지 말을 걸 수 있고(초반 수다),
        /// 액면에서 20초, 하강 중에 1분, <b>심부에서는 아예 말하지 않는다.</b>
        /// </summary>
        public static readonly float[] UtterancesPerMinute = { 6f, 3f, 1f, 0f };

        /// <summary>깊이 단계 수. 안개 밴드 수와 같다 — 사다리가 하나이기 때문이다.</summary>
        public static int StageCount => DepthFog.Bands.Length;

        /// <summary>
        /// 이 높이는 몇 번째 단계인가. 0이 제일 얕고 <see cref="StageCount"/>-1이 심부다.
        /// 표 위(아주 높은 곳)는 0으로, 표 아래(더 깊은 곳)는 마지막 단계로 고정한다 —
        /// <see cref="DepthFog.Sample"/>가 밴드 바깥을 다루는 방식과 같다.
        /// </summary>
        public static int StageAt(float worldY)
        {
            var bands = DepthFog.Bands;
            for (int i = 0; i < bands.Length; i++)
                if (worldY >= bands[i].Y) return i;
            return bands.Length - 1;
        }

        /// <summary>이 단계에서 1분에 몇 번까지 말하는가.</summary>
        public static float RatePerMinute(int stage)
        {
            if (UtterancesPerMinute.Length == 0) return 0f;
            if (stage < 0) stage = 0;
            if (stage >= UtterancesPerMinute.Length) stage = UtterancesPerMinute.Length - 1;
            return UtterancesPerMinute[stage];
        }

        /// <summary>이 높이에서 1분에 몇 번까지 말하는가.</summary>
        public static float RateAt(float worldY) => RatePerMinute(StageAt(worldY));

        /// <summary>
        /// 두 마디 사이에 최소 몇 초를 두어야 하는가. 빈도가 0인 단계는
        /// <see cref="float.PositiveInfinity"/> — 어떤 기다림으로도 열리지 않는다는 뜻이고,
        /// 그것이 "심부 침묵"의 코드 쪽 표현이다.
        /// </summary>
        public static float MinIntervalSeconds(int stage)
        {
            float rate = RatePerMinute(stage);
            return rate <= 0f ? float.PositiveInfinity : 60f / rate;
        }

        /// <summary>
        /// 지금 말해도 되는가. 반복 발화 채널이 생기면 이 한 줄이 문지기다.
        /// </summary>
        public static bool MaySpeak(float worldY, float secondsSinceLastLine) =>
            secondsSinceLastLine >= MinIntervalSeconds(StageAt(worldY));

        /// <summary>
        /// <see cref="UtterancesPerMinute"/>가 규칙을 지키는가. 어기면 <c>why</c>에 이유가 담긴다.
        ///
        /// <b>왜 함수로 두는가.</b> 규칙을 시험 파일에만 적어 두면, 표를 고치는 사람이
        /// 시험을 열어 보기 전까지는 무엇이 규칙인지 알 수 없다. 규칙은 표 옆에 있어야 한다.
        /// </summary>
        public static bool ValidateTable(out string why) => ValidateTable(UtterancesPerMinute, out why);

        /// <summary>
        /// 같은 규칙을 아무 표에나. <b>규칙 자체를 시험할 수 있게</b> 열어 둔다 —
        /// 지금 표만 검사할 수 있으면 "규칙이 실제로 무엇을 걸러내는가"를 아무도 못 본다.
        /// </summary>
        public static bool ValidateTable(float[] 표, out string why)
        {
            if (표 == null)
            {
                why = "말수 표가 없다";
                return false;
            }

            if (표.Length != StageCount)
            {
                why = $"말수 표가 {표.Length}칸인데 깊이 사다리는 {StageCount}단이다. " +
                      "안개 밴드를 더했으면 말수도 함께 정해야 한다";
                return false;
            }

            if (표.Length < 2)
            {
                why = "단계가 둘도 안 되면 \"깊어질수록 준다\"가 성립하지 않는다";
                return false;
            }

            if (표[0] <= 0f)
            {
                why = "제일 얕은 곳에서도 말하지 않는다 — 초반 수다가 없으면 심부 침묵이 대조를 잃는다";
                return false;
            }

            if (표[표.Length - 1] != 0f)
            {
                why = $"심부의 말수가 {표[표.Length - 1]}다. 침묵은 \"드물게\"가 아니라 0이다";
                return false;
            }

            for (int i = 1; i < 표.Length; i++)
            {
                if (표[i] >= 표[i - 1])
                {
                    why = $"{i - 1}단({표[i - 1]})보다 {i}단({표[i]})이 줄지 않았다 — 단조 감소여야 한다";
                    return false;
                }
            }

            why = null;
            return true;
        }
    }
}
