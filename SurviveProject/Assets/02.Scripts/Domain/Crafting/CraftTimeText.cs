namespace Survive.Crafting
{
    /// <summary>
    /// 남은 시간을 사람이 읽는 길이로 적는다.
    ///
    /// 초를 소수점까지 보여 주면 숫자가 쉴 새 없이 흔들려 오히려 못 읽는다.
    /// 시간이 넘어가면 시·분, 분이 넘어가면 분·초, 아니면 초만.
    ///
    /// 붙는 칸이 좁다(대기열 슬롯 하나). 그래서 단위 사이를 띄우지 않는다 —
    /// "2분30초"는 다섯 글자지만 "2분 30초"는 여섯 글자다.
    /// </summary>
    public static class CraftTimeText
    {
        const int Minute = 60;
        const int Hour = 60 * Minute;

        public static string Short(float seconds)
        {
            if (seconds < 0f) seconds = 0f;

            // 올림이다. 0.4초 남았는데 "0초"라고 적으면 이미 끝난 것처럼 보인다.
            int total = UnityEngine.Mathf.CeilToInt(seconds);

            if (total < Minute) return total + "초";

            if (total < Hour)
            {
                int m = total / Minute;
                int s = total % Minute;
                return s == 0 ? m + "분" : $"{m}분{s}초";
            }

            // 시간 단위에서는 초를 버린다. 한 시간을 기다리는 사람에게
            // 초 자리는 정보가 아니라 소음이다.
            int h = total / Hour;
            int rm = (total % Hour) / Minute;
            return rm == 0 ? h + "시간" : $"{h}시간{rm}분";
        }
    }
}
