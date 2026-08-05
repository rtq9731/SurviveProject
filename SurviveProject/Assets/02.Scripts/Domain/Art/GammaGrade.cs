using UnityEngine;

namespace Survive.Domain.Art
{
    /// <summary>
    /// 감마 조절 화면이 정한 값을 노출 보정으로 옮기는 규칙 (백로그 42).
    ///
    /// <b>왜 필요한가.</b> 화면이 거의 검정인 게임은 모니터 편차가 그대로
    /// 난이도가 된다. 같은 동굴이 어떤 화면에서는 형태가 읽히고 어떤
    /// 화면에서는 순수한 검정이다. 밝기를 사람이 한 번 맞춰 두지 않으면
    /// "어둠이 위협"이라는 설계가 사람마다 다른 게임이 된다.
    ///
    /// <b>표준 문법을 따른다.</b> 아주 어두운 무늬를 띄우고 "간신히 보일 때까지"
    /// 슬라이더를 옮기게 한다. 그 값(0~1)이 여기서 EV로 바뀌어 기본 볼륨의
    /// post-exposure에 상시 얹힌다.
    ///
    /// <b>기본값은 0.5이고 그때 보정은 정확히 0이다.</b> 검증(E2E·스크린샷 비교)이
    /// 사람의 설정에 흔들리면 안 되므로, 기본값에서는 아무 일도 일어나지 않아야 한다.
    /// </summary>
    public static class GammaGrade
    {
        /// <summary>손대지 않은 값. 이때 노출 보정은 0이다.</summary>
        public const float Neutral = 0.5f;

        /// <summary>슬라이더 최소. 화면이 너무 밝은 모니터를 눌러 준다.</summary>
        public const float MinExposure = -0.8f;

        /// <summary>슬라이더 최대. 어두운 모니터에서 형태가 겨우 읽히게 든다.</summary>
        public const float MaxExposure = 1.2f;

        /// <summary>
        /// 조절 무늬의 밝기(선형). 2%는 잘 맞춘 화면에서 "있는 것 같기도 하다"
        /// 정도로 보이는 고전적인 기준점이다. 이보다 밝으면 아무 화면에서나
        /// 보여서 조절이 되지 않고, 어두우면 아무 화면에서도 안 보인다.
        /// </summary>
        public const float PatchLuminance = 0.02f;

        /// <summary>슬라이더 값(0~1) → 노출 보정(EV).</summary>
        public static float Exposure(float value)
        {
            value = Mathf.Clamp01(value);
            return value <= Neutral
                ? Mathf.Lerp(MinExposure, 0f, Mathf.InverseLerp(0f, Neutral, value))
                : Mathf.Lerp(0f, MaxExposure, Mathf.InverseLerp(Neutral, 1f, value));
        }

        /// <summary>
        /// 노출 보정을 먹인 뒤의 밝기. EV는 2의 거듭제곱 배율이다.
        /// 검증이 "슬라이더 극단에서 암부가 실제로 달라지는가"를 값으로
        /// 확인하기 위한 것이다.
        /// </summary>
        public static float Apply(float linear, float exposureEv)
            => linear * Mathf.Pow(2f, exposureEv);

        /// <summary>사람이 아직 정하지 않았을 때 쓸 값인가.</summary>
        public static bool IsNeutral(float value) => Mathf.Approximately(value, Neutral);
    }
}
