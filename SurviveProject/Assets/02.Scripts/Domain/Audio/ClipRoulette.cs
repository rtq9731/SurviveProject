namespace Survive.Domain.Audio
{
    /// <summary>
    /// 여러 클립 중 하나를 고른다.
    ///
    /// <b>직전에 쓴 것을 피하는 이유.</b> 발소리가 똑같은 파일로 두 번 연달아 나면
    /// 귀가 즉시 "녹음된 것"으로 알아챈다. 흙을 밟는 소리 넷을 준비해 놓고
    /// 순수 난수로 뽑으면 넷 중 하나꼴로 같은 것이 붙어 나오는데, 그 한 번이
    /// 나머지 셋을 준비한 값을 통째로 지운다. 그래서 남은 것들 중에서만 고른다.
    ///
    /// 난수는 여기서 만들지 않는다 — 0~1 값을 받아 쓴다. 이 규칙을
    /// Unity 없이 시험하려면 뽑는 값이 바깥에서 정해져야 한다.
    /// </summary>
    public static class ClipRoulette
    {
        /// <summary>고를 것이 없음을 뜻하는 값.</summary>
        public const int None = -1;

        /// <summary>
        /// 다음에 쓸 인덱스. 고를 것이 없으면 <see cref="None"/>.
        /// </summary>
        /// <param name="count">고를 수 있는 클립 수. 0이면 아무것도 고르지 않는다.</param>
        /// <param name="previous">직전에 고른 인덱스. 없었으면 <see cref="None"/>.</param>
        /// <param name="roll01">0 이상 1 미만의 난수. 범위를 벗어나면 죈다.</param>
        public static int Next(int count, int previous, float roll01)
        {
            if (count <= 0) return None;
            if (count == 1) return 0;

            // 1은 배열 끝을 넘긴다. 1 미만으로 죄어 둔다.
            float r = roll01 < 0f ? 0f : (roll01 < 1f ? roll01 : 0.9999999f);

            // 직전 것이 없거나 범위 밖이면 전부가 후보다.
            if (previous < 0 || previous >= count)
            {
                int all = (int)(r * count);
                return all >= count ? count - 1 : all;
            }

            // 직전 것을 뺀 나머지에서 고르고, 빠진 자리만큼 뒤로 민다.
            int pick = (int)(r * (count - 1));
            if (pick >= count - 1) pick = count - 2;
            return pick >= previous ? pick + 1 : pick;
        }
    }
}
