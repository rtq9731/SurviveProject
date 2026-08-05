using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Domain.Audio;

namespace Survive.Audio
{
    /// <summary>
    /// Feel 피드백에서 <see cref="AudioCueSO"/>를 낸다.
    ///
    /// <b>왜 이것이 필요한가.</b> 이 저장소는 타격감을 Feel로 조립한다 —
    /// 열여덟 파일에 <c>MMF_Player</c> 칸이 이미 있다. 소리만 다른 길로 가면
    /// 같은 사건에 두 개의 재생 경로가 생기고, 겹침 막이는 그중 하나만 본다.
    /// 그러면 "한 프레임에 쉰 번 불러도 괜찮다"는 보장이 조용히 깨진다.
    ///
    /// Feel이 기본으로 주는 <c>MMF_Sound</c>는 자기 <c>AudioSource</c>를 즉석에서
    /// 만들고 자기 규칙으로 재생한다. 그것을 쓰는 대신 이 피드백을 쓰면,
    /// Feel에서 조립한 소리도 <see cref="AudioService"/>의 풀과 상한을 그대로 지난다.
    ///
    /// 코드에서 직접 부르는 자리(발소리·기척)는 이 피드백을 거치지 않고
    /// 서비스를 바로 부른다. 어느 쪽이든 <b>나가는 문은 하나</b>다.
    /// </summary>
    [AddComponentMenu("")]
    [System.Serializable]
    [FeedbackPath("Audio/Survive Audio Cue")]
    [FeedbackHelp("Survive의 AudioCueSO를 AudioService를 통해 재생합니다. " +
                  "클립이 비어 있으면 아무 일도 일어나지 않습니다.")]
    public class MMF_AudioCue : MMF_Feedback
    {
#if UNITY_EDITOR
        public override Color FeedbackColor => new Color(0.55f, 0.78f, 0.95f);
#endif

        [MMFInspectorGroup("소리", true, 68)]
        [Tooltip("낼 소리. 비우면 아무 일도 없다")]
        public AudioCueSO Cue;

        [Tooltip("켜면 세계의 그 자리에서, 끄면 귀 바로 앞에서 난다")]
        public bool AtPosition = true;

        protected override void CustomPlayFeedback(Vector3 position, float feedbacksIntensity = 1f)
        {
            if (!Active || Cue == null) return;

            if (AtPosition) AudioService.Play(Cue, position);
            else AudioService.Play2D(Cue);
        }
    }
}
