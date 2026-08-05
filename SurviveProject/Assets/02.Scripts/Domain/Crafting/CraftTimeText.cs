namespace Survive.Crafting
{
    /// <summary>
    /// 남은 시간을 시계 꼴(HH:MM:SS)로 적는다.
    ///
    /// 초를 소수점까지 보여 주면 숫자가 쉴 새 없이 흔들려 오히려 못 읽는다.
    ///
    /// 자릿수를 항상 채우는 이유는 <b>폭이 변하지 않게</b> 하기 위해서다.
    /// 대기열 칸에 붙어 매초 다시 그려지는 글자라, "9초"에서 "10초"로 넘어갈 때
    /// 길이가 달라지면 칸 안에서 글자가 들썩인다. 00:00:09 → 00:00:10 은
    /// 숫자만 바뀌고 자리는 그대로다.
    /// </summary>
    public static class CraftTimeText
    {
        const int Minute = 60;
        const int Hour = 60 * Minute;

        public static string Short(float seconds)
        {
            if (seconds < 0f) seconds = 0f;

            // 올림이다. 0.4초 남았는데 00:00:00이라고 적으면 이미 끝난 것처럼 보인다.
            int total = UnityEngine.Mathf.CeilToInt(seconds);

            int h = total / Hour;
            int m = (total % Hour) / Minute;
            int s = total % Minute;

            // 시가 100을 넘으면 자리가 하나 늘어난다. 그런 제작은 없지만,
            // 잘라 내서 거짓말을 하느니 칸을 넘기는 편이 낫다.
            return $"{h:00}:{m:00}:{s:00}";
        }
    }
}
