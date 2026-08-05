using UnityEngine;

namespace Survive.Domain.Audio
{
    /// <summary>
    /// 보이지 않는 것이 다가올 때의 기척.
    ///
    /// <b>이 게임에서 소리가 가장 크게 일하는 자리다.</b> 환경광이 0이라 랜턴이 닿지
    /// 않는 곳은 진짜로 검고, 낫은 그 검은 데서 온다. 눈으로 알 수 있는 것이 없을 때
    /// 남은 감각은 귀뿐이다 — 멀리서 한 번, 조금 가까워지면 더 자주, 코앞이면 쉼 없이.
    /// 그 <i>간격의 변화</i>가 거리 정보다. 볼륨만 키우면 방향은 알아도 얼마나
    /// 급한지는 모른다.
    ///
    /// 실제 재생은 <c>AudioService</c>가 3D로 하므로 감쇠는 거기서 또 걸린다.
    /// 여기서 내는 것은 그 위에 얹히는 <b>연출분</b>이다.
    /// </summary>
    public static class ApproachAudio
    {
        /// <summary>코앞일 때의 소리 사이 간격(초).</summary>
        public const float NearIntervalSeconds = 0.8f;

        /// <summary>들리기 시작하는 언저리에서의 간격(초).</summary>
        public const float FarIntervalSeconds = 3.4f;

        /// <summary>들리는 범위 안쪽이라도 이만큼은 깔린다. 0에서 시작하면 켜지는 순간이 티 난다.</summary>
        public const float FloorLoudness = 0.15f;

        /// <summary>이 거리 안이면 가장 가까운 것으로 본다(m). 그 안에서 더 커지지는 않는다.</summary>
        public const float PointBlank = 3f;

        /// <summary>들리는 범위 안인가. 범위가 0 이하면 아무것도 들리지 않는다.</summary>
        public static bool IsAudible(float distance, float audibleRange) =>
            audibleRange > 0f && distance < audibleRange;

        /// <summary>
        /// 얼마나 가까운가. 0(못 들음) ~ 1(코앞).
        /// 거리에 반비례하는 것이 아니라 <b>남은 거리에 비례</b>한다 — 멀리서부터
        /// 서서히 자라야 다가오고 있다는 것이 읽힌다.
        /// </summary>
        public static float Closeness(float distance, float audibleRange)
        {
            if (!IsAudible(distance, audibleRange)) return 0f;
            if (distance <= PointBlank) return 1f;
            return 1f - Mathf.Clamp01((distance - PointBlank) / Mathf.Max(0.01f, audibleRange - PointBlank));
        }

        /// <summary>연출분 볼륨 배율. 범위 밖이면 0.</summary>
        public static float Loudness(float distance, float audibleRange)
        {
            float close = Closeness(distance, audibleRange);
            if (close <= 0f) return 0f;
            return Mathf.Lerp(FloorLoudness, 1f, close);
        }

        /// <summary>다음 기척까지의 간격(초). 가까울수록 짧다.</summary>
        public static float IntervalSeconds(float distance, float audibleRange)
        {
            float close = Closeness(distance, audibleRange);
            return Mathf.Lerp(FarIntervalSeconds, NearIntervalSeconds, close);
        }
    }
}
