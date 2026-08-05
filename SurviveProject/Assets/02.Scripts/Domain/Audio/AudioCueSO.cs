using UnityEngine;

namespace Survive.Domain.Audio
{
    /// <summary>
    /// 소리 하나. <b>클립이 아니라 "그 소리를 어떻게 낼 것인가"의 묶음이다.</b>
    ///
    /// <b>클립이 비어 있는 것은 고장이 아니다.</b> 이 저장소에는 아직 소리 파일이
    /// 하나도 없고, 무엇을 꽂을지는 사람이 정한다. 그래서 여기서는 경고도 로그도
    /// 내지 않는다 — 비어 있으면 <see cref="HasClips"/>가 false이고, 재생 창구는
    /// 조용히 아무것도 하지 않는다. 콘솔이 시끄러우면 진짜 문제가 묻힌다.
    ///
    /// <b>클립을 여러 개 두는 이유.</b> 같은 파형이 연달아 나오면 귀가 바로
    /// 녹음된 것임을 알아챈다. 특히 발소리와 타격음이 그렇다. 넷쯤 넣어 두면
    /// <see cref="ClipRoulette"/>이 직전 것을 피해 가며 고른다.
    /// </summary>
    [CreateAssetMenu(menuName = "Survive/Audio/Audio Cue", fileName = "Cue_")]
    public class AudioCueSO : ScriptableObject
    {
        [Tooltip("여러 개면 매번 하나를 무작위로 고른다. 직전에 쓴 것은 피한다. " +
                 "비워 두는 것이 기본이고, 비어 있으면 아무 소리도 나지 않는다")]
        public AudioClip[] clips = new AudioClip[0];

        [Header("세기")]
        [Range(0f, 1f)] public float volumeMin = 0.85f;
        [Range(0f, 1f)] public float volumeMax = 1f;

        [Tooltip("음정을 조금씩 흔들면 같은 클립도 다르게 들린다. 1이 원음")]
        [Range(0.25f, 3f)] public float pitchMin = 0.95f;
        [Range(0.25f, 3f)] public float pitchMax = 1.05f;

        [Header("겹침 막기")]
        [Tooltip("같은 소리를 이 시간 안에 두 번 내지 않는다(초). 0이면 검사하지 않는다")]
        [Min(0f)] public float minIntervalSeconds = 0.06f;

        [Tooltip("이 소리가 동시에 몇 개까지 살아 있어도 되는가. 무리가 한꺼번에 " +
                 "맞을 때 같은 파형이 겹쳐 진폭이 치솟는 것을 막는다")]
        [Min(1)] public int maxConcurrent = 3;

        [Header("자리")]
        [Tooltip("세계 안에서 나는 소리인가. 끄면 어디서 불러도 귀 바로 앞에서 난다")]
        public bool spatial = true;

        [Tooltip("이 거리까지는 줄지 않는다(m)")]
        [Min(0f)] public float minDistance = 3f;

        [Tooltip("이 거리에서 들리지 않는다(m)")]
        [Min(0f)] public float maxDistance = 35f;

        public AudioRolloffMode rolloff = AudioRolloffMode.Linear;

        [Header("갈래")]
        public AudioBus bus = AudioBus.Sfx;

        [Tooltip("멈추라고 할 때까지 계속 돈다. 불 타는 소리·기척처럼 상태에 붙는 소리")]
        public bool loop = false;

        int _lastPicked = ClipRoulette.None;

        /// <summary>
        /// 실제로 쓸 수 있는 클립의 수. 배열 중간의 빈 칸은 세지 않는다 —
        /// 인스펙터에서 크기만 늘려 놓고 아직 안 채운 칸이 재생을 막으면 안 된다.
        ///
        /// <b>추려 둔 배열을 캐시하지 않는다.</b> 한 번 그렇게 만들었다가
        /// 실측에서 걸렸다 — 캐시는 <c>OnEnable</c>에서 채워지는데, 코드로 만든
        /// cue는 그때 <see cref="clips"/>가 아직 비어 있어서 나중에 클립을 넣어도
        /// 영영 "소리 없음"이었다. 클립은 많아야 서넛이라 매번 세는 값이 싸다.
        /// </summary>
        public int ValidClipCount
        {
            get
            {
                if (clips == null) return 0;
                int n = 0;
                for (int i = 0; i < clips.Length; i++)
                    if (clips[i] != null) n++;
                return n;
            }
        }

        /// <summary>낼 소리가 있는가. 없는 것이 지금의 정상 상태다.</summary>
        public bool HasClips
        {
            get
            {
                if (clips == null) return false;
                for (int i = 0; i < clips.Length; i++)
                    if (clips[i] != null) return true;
                return false;
            }
        }

        /// <summary>
        /// 이 소리의 신원. 겹침 규칙이 같은 소리인지 가릴 때 쓴다.
        ///
        /// <c>GetInstanceID</c>는 Unity 6.5에서 폐기되었고, 대체인 <c>GetEntityId</c>는
        /// int가 아니다. 여기서 필요한 것은 "같은 에셋이면 같은 값"뿐이므로
        /// 참조 동일성 해시로 충분하다.
        /// </summary>
        public int CueId => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

        /// <summary>3D면 1, 2D면 0. UI 갈래는 세계에 자리가 없으므로 언제나 2D다.</summary>
        public float SpatialBlend => (spatial && bus != AudioBus.UI) ? 1f : 0f;

#if UNITY_EDITOR
        void OnValidate()
        {
            if (volumeMax < volumeMin) volumeMax = volumeMin;
            if (pitchMax < pitchMin) pitchMax = pitchMin;
            if (maxDistance < minDistance) maxDistance = minDistance;
        }
#endif

        /// <summary>
        /// 다음에 낼 클립. 없으면 null. 직전에 쓴 것은 스스로 기억해 피한다.
        /// </summary>
        /// <param name="roll01">0 이상 1 미만의 난수. 부르는 쪽이 뽑아 넣는다.</param>
        public AudioClip PickClip(float roll01)
        {
            int count = ValidClipCount;
            if (_lastPicked >= count) _lastPicked = ClipRoulette.None;

            int index = ClipRoulette.Next(count, _lastPicked, roll01);
            if (index == ClipRoulette.None) return null;

            _lastPicked = index;
            return ClipAt(index);
        }

        /// <summary>빈 칸을 건너뛴 <paramref name="index"/>번째 클립.</summary>
        public AudioClip ClipAt(int index)
        {
            if (clips == null || index < 0) return null;

            int seen = 0;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null) continue;
                if (seen++ == index) return clips[i];
            }
            return null;
        }

        /// <summary>이번에 쓸 볼륨.</summary>
        public float PickVolume(float roll01) => Mathf.Lerp(volumeMin, volumeMax, Mathf.Clamp01(roll01));

        /// <summary>이번에 쓸 음정.</summary>
        public float PickPitch(float roll01) => Mathf.Lerp(pitchMin, pitchMax, Mathf.Clamp01(roll01));
    }
}
