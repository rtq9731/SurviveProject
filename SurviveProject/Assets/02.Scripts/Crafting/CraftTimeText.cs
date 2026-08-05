namespace Survive.Crafting
{
    /// <summary>
    /// 남은 시간을 사람이 읽는 길이로 적는다.
    ///
    /// 초를 소수점까지 보여 주면 숫자가 쉴 새 없이 흔들려 오히려 못 읽는다.
    /// 분이 넘어가면 분·초로, 아니면 초만.
    /// </summary>
    public static class CraftTimeText
    {
        public static string Short(float seconds)
        {
            if (seconds < 0f) seconds = 0f;

            int total = UnityEngine.Mathf.CeilToInt(seconds);
            if (total < 60) return total + "s";

            int m = total / 60;
            int s = total % 60;
            return s == 0 ? m + "m" : $"{m}m{s}s";
        }
    }
}
