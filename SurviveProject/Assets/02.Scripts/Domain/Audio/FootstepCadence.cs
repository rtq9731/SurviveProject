using UnityEngine;

namespace Survive.Domain.Audio
{
    /// <summary>
    /// 발소리를 언제 낼 것인가.
    ///
    /// 발소리는 다른 소리와 다르게 <b>사건이 아니라 리듬</b>이다. 부를 자리가
    /// 코드 어딘가에 있는 것이 아니라, 걷고 있는 동안 일정한 간격으로 스스로 나야 한다.
    /// 그 간격을 여기서 정한다 — 걸음의 보폭은 대체로 일정하므로 빨리 갈수록 간격이
    /// 짧아진다.
    ///
    /// <b>걷기와 뛰기를 가르는 이유.</b> 소리로 알 수 있는 것이 하나 늘기 때문이다.
    /// 어둠 속에서 뛰면 시끄럽고, 시끄러우면 무언가 온다 — 그 인과를 나중에 붙이려면
    /// 지금부터 두 소리가 갈라져 있어야 한다.
    /// </summary>
    public static class FootstepCadence
    {
        /// <summary>이보다 느리면 걷는 것으로 치지 않는다(m/s). 벽에 붙어 비비는 정도.</summary>
        public const float MinAudibleSpeed = 0.8f;

        /// <summary>이 속도부터 뛰는 소리를 쓴다. 걷기 5, 달리기 7 사이에 둔다.</summary>
        public const float RunSpeedThreshold = 5.8f;

        /// <summary>걸을 때의 발 사이 간격(초).</summary>
        public const float WalkStrideSeconds = 0.52f;

        /// <summary>가장 빨리 달릴 때의 간격(초). 이보다 촘촘해지지는 않는다.</summary>
        public const float RunStrideSeconds = 0.31f;

        /// <summary>간격이 최소가 되는 속도(m/s). 수영 가속까지 감안해 조금 넉넉히 잡는다.</summary>
        public const float TopSpeed = 8.5f;

        /// <summary>지금 소리를 낼 만큼 움직이고 있는가.</summary>
        public static bool IsMoving(float speed) => speed >= MinAudibleSpeed;

        /// <summary>뛰는 소리를 써야 하는가.</summary>
        public static bool IsRunning(float speed) => speed >= RunSpeedThreshold;

        /// <summary>이 속도에서 발과 발 사이의 간격(초).</summary>
        public static float StrideSeconds(float speed)
        {
            if (speed <= MinAudibleSpeed) return WalkStrideSeconds;

            float t = Mathf.InverseLerp(MinAudibleSpeed, TopSpeed, speed);
            return Mathf.Lerp(WalkStrideSeconds, RunStrideSeconds, Mathf.Clamp01(t));
        }

        /// <summary>
        /// 지금 한 발 딛는 소리를 낼 때인가.
        ///
        /// 공중에서는 내지 않는다 — 점프 중에 발소리가 나면 발이 어디 있는지가
        /// 화면과 어긋난다. 착지음은 이것과 별개로 착지 순간에 한 번 난다.
        /// </summary>
        public static bool ShouldStep(bool grounded, float speed, float secondsSinceLastStep)
        {
            if (!grounded) return false;
            if (!IsMoving(speed)) return false;
            return secondsSinceLastStep >= StrideSeconds(speed);
        }
    }
}
