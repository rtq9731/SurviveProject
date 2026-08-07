using UnityEngine;

namespace Survive.Creatures
{
    /// <summary>
    /// <b>관찰의 보상 — 배부른 개체를 고르면 얼마나 더 나오는가</b> (기획서 §3.4).
    ///
    /// 「생태계를 읽으면 이득을 본다」가 이 게임의 차별화 축이고, 그 축이 서느냐는
    /// <b>크기가 정한다.</b> 배부른 개체를 골라 두세 개 더 나오는 정도라면 아무도
    /// 고르지 않고 가까운 것을 잡는 것이 최적해가 된다 — 구현되어 있으되 없는 것과
    /// 같아진다. 그래서 이 축에서는 배율이 곧 성립 여부다.
    ///
    /// <b>왜 규칙으로 꺼냈는가.</b> 이 셈은 오래
    /// <c>CreatureHealth.DropLoot</c> 안에 <c>RoundToInt(stored)</c> 한 줄로 묻혀
    /// 있었다. 묻혀 있으면 지금 배율이 몇인지도, 무엇을 얼마로 바꾸면 몇이 되는지도
    /// 잴 수 없다. <b>고르라고 내놓을 값은 먼저 규칙으로 서 있어야 한다.</b>
    ///
    /// 값 자체는 여기서 정하지 않는다. 지금 서 있는 셈을 그대로 옮겨 놓고,
    /// 무엇을 재야 사람이 고를 수 있는지를 함수 이름으로 적어 둔다.
    /// </summary>
    public static class FeedingPayoff
    {
        /// <summary>
        /// 축적 1당 스크랩 몇 개로 붙는가.
        ///
        /// <b>1인 것은 결정이 아니라 실측 대상이다.</b> 축적량이 그대로 개수가 되도록
        /// 처음부터 그렇게 짜여 있었고, 여기서는 그 사실에 이름을 붙였을 뿐이다.
        /// 이 값을 올리는 것이 배율을 키우는 가장 짧은 손잡이다.
        /// </summary>
        public const float ScrapPerNutrition = 1f;

        /// <summary>먹어도 차지 않을 때의 답. 큰 수로 답하면 셈이 조용히 흘러간다.</summary>
        public const int NeverFull = -1;

        /// <summary>
        /// 축적량이 스크랩 몇 개로 붙는가.
        ///
        /// <b>반올림은 짝수로 간다</b>(<see cref="Mathf.RoundToInt"/>). 0.5는 0이 되고
        /// 1.5는 2가 된다. 이 결이 마음에 들지 않아도 여기서 바꾸면 안 된다 —
        /// 지금 게임이 실제로 내놓는 개수가 이것이라, 바꾸는 순간 실측이 실측이 아니게 된다.
        /// </summary>
        public static int Bonus(float stored)
        {
            if (stored <= 0f) return 0;
            return Mathf.Max(0, Mathf.RoundToInt(stored * ScrapPerNutrition));
        }

        /// <summary>
        /// 굶은 개체 대비 몇 배가 나오는가.
        ///
        /// <paramref name="baseDrop"/>이 0 이하면 <b>0을 돌려준다.</b> 「무한 배」는
        /// 답이 아니라 잴 수 없다는 뜻이고, 잴 수 없는 것을 큰 수로 답하면
        /// 표에서 그 칸만 유독 좋아 보인다.
        /// </summary>
        public static float Multiplier(float baseDrop, float stored)
        {
            if (baseDrop <= 0f) return 0f;
            return (baseDrop + Bonus(stored)) / baseDrop;
        }

        /// <summary>배부르려면 몇 입인가. 차지 않는 먹이면 <see cref="NeverFull"/>.</summary>
        public static int BitesToFull(float capacity, float nutritionPerBite)
        {
            if (capacity <= 0f) return 0;                  // 처음부터 배부르다
            if (nutritionPerBite <= 0f) return NeverFull;   // 아무리 먹어도 차지 않는다
            return Mathf.CeilToInt(capacity / nutritionPerBite);
        }

        /// <summary>
        /// 배부르기까지 <b>먹는 쪽으로만</b> 몇 초가 드는가.
        ///
        /// <b>첫 입은 기다리지 않는다.</b> 쿨다운은 입과 입 사이에만 들므로 n입이면
        /// 간격은 n−1개다. 여기에 <b>찾아가는 시간과 식물이 다시 자라는 시간은
        /// 들어 있지 않다</b> — 그 둘은 자리마다 다르므로 규칙이 답할 수 없고,
        /// 세계에서 재야 한다. 그래서 이 값은 언제나 <b>실제보다 짧은 쪽의 바닥</b>이다.
        /// </summary>
        public static float SecondsToFull(float capacity, float nutritionPerBite, float biteCooldown)
        {
            int bites = BitesToFull(capacity, nutritionPerBite);
            if (bites == NeverFull) return float.PositiveInfinity;
            if (bites <= 1) return 0f;
            return (bites - 1) * Mathf.Max(0f, biteCooldown);
        }

        /// <summary>
        /// <b>기다린 1분이 스크랩 몇 개를 만드는가.</b> 「그래서 고를 값어치가
        /// 있는가」에 답하는 값이다 — 배율만으로는 답이 안 나온다. 다섯 배라도
        /// 반나절을 기다려야 한다면 아무도 안 고른다.
        ///
        /// 기다림이 0초인데 이득이 있으면 <see cref="float.PositiveInfinity"/>다.
        /// 공짜라는 뜻이고, 그것은 실제로 표에 그렇게 적혀야 하는 사실이다.
        /// </summary>
        public static float BonusPerMinute(float capacity, float nutritionPerBite, float biteCooldown)
        {
            float seconds = SecondsToFull(capacity, nutritionPerBite, biteCooldown);
            if (float.IsPositiveInfinity(seconds)) return 0f;

            int bonus = Bonus(capacity);
            if (bonus <= 0) return 0f;
            if (seconds <= 0f) return float.PositiveInfinity;
            return bonus / seconds * 60f;
        }
    }
}
