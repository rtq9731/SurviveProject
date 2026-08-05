using UnityEngine;
using UnityEngine.Audio;
using Survive.Domain.Audio;

namespace Survive.Audio
{
    /// <summary>
    /// 소리를 내는 <b>단 하나의 창구</b>.
    ///
    /// <b>왜 창구가 하나여야 하는가.</b> 소리를 내는 자리마다 <c>AudioSource</c>를
    /// 붙이면 세 가지가 따라온다 — 부수는 순간 사라지는 물건(주운 아이템, 죽은 생물)의
    /// 소리가 잘려 나가고, 같은 소리가 몇 개나 겹쳐 나는지 아무도 모르며,
    /// <c>AddComponent</c>가 프레임마다 쓰레기를 만든다. 창구를 하나 두고
    /// 고정 크기 풀을 돌리면 셋 다 사라진다.
    ///
    /// <b>지금 이 저장소에는 소리 파일이 하나도 없다.</b> 그래서 이 창구가 실제로 하는
    /// 일은 대부분 "아무것도 하지 않고 돌아가는 것"이다 — cue가 없거나 클립이 없으면
    /// 즉시 반환한다. 경고도 내지 않는다. 소리를 꽂기 전과 꽂은 뒤의 게임이
    /// 타이밍까지 같아야 하기 때문이다.
    ///
    /// <b>겹침 판단은 여기에 없다.</b> 최소 간격·동시 발성 상한은
    /// <see cref="AudioThrottle"/>이 정한다. 소리는 들어서 검증할 수 없으므로,
    /// 그 규칙만은 Unity 없이 세어 볼 수 있는 자리에 있어야 한다.
    ///
    /// <b>Feel과의 관계.</b> 이 저장소는 이미 <c>MMF_Player</c>로 타격감을 조립한다.
    /// 소리도 그쪽에 얹을 수 있지만 그러려면 프리팹을 열어야 하고(이 작업에서 금지),
    /// 발소리·기척처럼 사건이 아닌 소리는 얹을 자리 자체가 없다. 그래서 재생 경로는
    /// 여기 하나로 두고, Feel에서 소리를 내고 싶을 때는 <see cref="MMF_AudioCue"/>가
    /// 이 창구를 부른다 — 두 체계가 따로 놀지 않게 하는 다리다.
    /// </summary>
    [DisallowMultipleComponent]
    public class AudioService : MonoBehaviour
    {
        /// <summary>동시에 울릴 수 있는 소리의 수. 풀 크기이자 전체 상한이다.</summary>
        public const int PoolSize = 24;

        /// <summary>소리 표를 찾는 이름. 없으면 모든 자리가 비어 있는 것과 같다.</summary>
        public const string BookResourceName = "AudioCueBook";

        /// <summary>믹서를 찾는 이름. 없으면 갈래별 배율만으로 돈다.</summary>
        public const string MixerResourceName = "SurviveAudioMixer";

        static AudioService _instance;

        /// <summary>세워져 있으면 그것. 재생 전이면 null.</summary>
        public static AudioService Instance => _instance;

        // ── 실측용 계수기 ────────────────────────────────────────
        // 소리를 들을 수 없으니 숫자로 본다. 풀이 새지 않는지, 겹침 막이가
        // 실제로 막고 있는지는 이 값들로만 확인할 수 있다.

        /// <summary>재생을 부탁받은 횟수. cue가 null이어도 센다.</summary>
        public static int Requests { get; private set; }

        /// <summary>실제로 소리가 나간 횟수.</summary>
        public static int Played { get; private set; }

        /// <summary>겹침 막이에 걸려 나가지 않은 횟수.</summary>
        public static int Throttled { get; private set; }

        /// <summary>낼 것이 없어(cue 없음·클립 없음) 조용히 돌아간 횟수. 지금은 이게 전부다.</summary>
        public static int Silent { get; private set; }

        /// <summary>동시에 울린 최대 개수. 풀 크기를 넘으면 안 된다.</summary>
        public static int PeakBusy { get; private set; }

        /// <summary>만들어 둔 AudioSource 수. 언제나 <see cref="PoolSize"/>여야 한다.</summary>
        public static int SourceCount => _instance != null ? _instance._pool.Length : 0;

        /// <summary>지금 울리고 있는 개수.</summary>
        public static int BusyCount
        {
            get
            {
                if (_instance == null) return 0;
                int n = 0;
                for (int i = 0; i < _instance._pool.Length; i++)
                    if (_instance._pool[i].InUse) n++;
                return n;
            }
        }

        /// <summary>계수기를 0으로. 구간별로 재려고 둔 것이다.</summary>
        public static void ResetCounters()
        {
            Requests = Played = Throttled = Silent = PeakBusy = 0;
        }

        // ── 자기 설치 ────────────────────────────────────────────

        /// <summary>
        /// 재생을 처음 부탁받는 순간에 세운다. <b>미리 세우지 않는다.</b>
        ///
        /// 소리가 하나도 없는 지금 이 저장소에서는 모든 재생 요청이 "낼 것이 없다"로
        /// 끝나므로, 이 함수가 아예 불리지 않는다 — 즉 <c>AudioService</c> 오브젝트도
        /// AudioSource 스물넷도 만들어지지 않는다. 소리 배선이 프레임을 바꾸지
        /// 않았다는 것을 증명해야 하는 이번 작업에서 이것이 가장 강한 보장이다.
        ///
        /// 편집 중(재생 아님)에는 아무것도 만들지 않는다.
        /// </summary>
        static AudioService Ensure()
        {
            if (_instance != null) return _instance;
            if (!Application.isPlaying) return null;

            var go = new GameObject("AudioService");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<AudioService>();
            return _instance;
        }

        /// <summary>
        /// 도메인 리로드를 끄고 재생하면 static이 지난 판에서 살아남는다.
        /// 씬이 올라오기 전에 한 번 비워 둔다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
            _book = null;
            _bookLoaded = false;
            ResetCounters();
        }

        // ── 상태 ─────────────────────────────────────────────────

        struct Voice
        {
            public AudioSource Source;
            public int CueId;
            public bool Loop;
            public bool InUse;
            public float StartedAt;
            public int Serial;
            public Transform Follow;
        }

        Voice[] _pool;
        AudioThrottle _throttle;
        AudioMixerGroup[] _groups;
        readonly float[] _busVolume = { 1f, 1f, 1f, 1f };
        int _nextSerial = 1;
        int _cursor;

        static AudioCueBookSO _book;
        static bool _bookLoaded;

        /// <summary>
        /// 소리 표. 없으면 null이고, <b>그것이 지금의 정상 상태다.</b>
        ///
        /// 창구와 떼어 두었다. 표를 읽는 것만으로 서비스가 세워지면, 소리가 하나도
        /// 없는데도 AudioSource 스물넷이 생긴다 — 부르는 쪽은 거의 언제나
        /// "표를 보고 → 비어 있어서 아무것도 안 함" 순서라 그 낭비가 상시가 된다.
        /// </summary>
        public static AudioCueBookSO Book
        {
            get
            {
                if (_bookLoaded) return _book;
                _bookLoaded = true;
                _book = Resources.Load<AudioCueBookSO>(BookResourceName);
                return _book;
            }
        }

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;

            _throttle = new AudioThrottle(PoolSize);
            BuildPool();
            BindMixer();
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// 소리 낼 자리를 미리 다 만들어 둔다.
        ///
        /// 필요할 때 만들면 매번 <c>AddComponent</c>가 힙을 건드리고, 그 쓰레기는
        /// 하필 소리가 가장 많이 나는 순간(무리와의 교전)에 쌓인다. 스물넷은
        /// 사람 귀가 한 번에 구분할 수 있는 수보다 이미 훨씬 많다.
        /// </summary>
        void BuildPool()
        {
            _pool = new Voice[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Voice" + i.ToString("00"));
                go.transform.SetParent(transform, false);

                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.spatialBlend = 0f;

                _pool[i] = new Voice { Source = src };
            }
        }

        /// <summary>
        /// 믹서가 있으면 갈래를 그룹에 묶는다. <b>없어도 된다.</b>
        ///
        /// 믹서는 병합할 수 없는 이진 에셋이라 이 작업에서 만들지 않았다.
        /// 대신 갈래별 배율(<see cref="SetBusVolume"/>)이 코드 쪽에 있어서,
        /// 믹서가 없어도 "효과음만 줄이기"가 성립한다. 나중에
        /// <c>Resources/SurviveAudioMixer.mixer</c>를 넣고 그룹 이름을
        /// Master·SFX·Ambient·UI로 두면 저절로 붙는다.
        /// </summary>
        void BindMixer()
        {
            _groups = new AudioMixerGroup[4];

            var mixer = Resources.Load<AudioMixer>(MixerResourceName);
            if (mixer == null) return;

            _groups[(int)AudioBus.Master] = FindGroup(mixer, "Master");
            _groups[(int)AudioBus.Sfx] = FindGroup(mixer, "SFX");
            _groups[(int)AudioBus.Ambient] = FindGroup(mixer, "Ambient");
            _groups[(int)AudioBus.UI] = FindGroup(mixer, "UI");
        }

        static AudioMixerGroup FindGroup(AudioMixer mixer, string name)
        {
            var found = mixer.FindMatchingGroups(name);
            return (found != null && found.Length > 0) ? found[0] : null;
        }

        /// <summary>갈래별 배율(0~1). 믹서가 없어도 듣는다. UI는 이 창구에서 만들지 않는다.</summary>
        public static void SetBusVolume(AudioBus bus, float value)
        {
            var svc = Ensure();
            if (svc == null) return;
            svc._busVolume[(int)bus] = Mathf.Clamp01(value);
        }

        /// <summary>지금 갈래 배율.</summary>
        public static float GetBusVolume(AudioBus bus)
        {
            var svc = Ensure();
            return svc != null ? svc._busVolume[(int)bus] : 1f;
        }

        // ── 재생 ─────────────────────────────────────────────────

        /// <summary>
        /// 세계의 한 자리에서 소리를 낸다. cue가 없으면 아무 일도 없다.
        /// </summary>
        /// <param name="volumeScale">
        /// cue가 정한 볼륨 위에 곱하는 연출분(0~1). 거리에 따라 기척을 키우는 것처럼
        /// <b>같은 소리를 상황에 따라 다르게 낼 때만</b> 쓴다. 기본값 1이면 cue 그대로다.
        /// </param>
        public static void Play(AudioCueSO cue, Vector3 position, float volumeScale = 1f) =>
            PlayInternal(cue, position, null, false, volumeScale);

        /// <summary>이 물건이 있는 자리에서. transform이 없으면 아무 일도 없다.</summary>
        public static void PlayAt(AudioCueSO cue, Transform where, float volumeScale = 1f)
        {
            if (where == null) { Requests++; Silent++; return; }
            PlayInternal(cue, where.position, null, false, volumeScale);
        }

        /// <summary>귀 바로 앞에서. 화면 소리·주관적인 소리.</summary>
        public static void Play2D(AudioCueSO cue, float volumeScale = 1f) =>
            PlayInternal(cue, Vector3.zero, null, true, volumeScale);

        /// <summary>
        /// 멈추라고 할 때까지 도는 소리. 돌려받은 손잡이로 멈춘다.
        /// 걸지 못했으면 무효한 손잡이가 돌아온다 — 그대로 들고 있어도 안전하다.
        /// </summary>
        public static AudioHandle PlayLoop(AudioCueSO cue, Transform follow)
        {
            Requests++;

            if (cue == null || !cue.HasClips) { Silent++; return AudioHandle.None; }

            var svc = Ensure();
            if (svc == null) { Silent++; return AudioHandle.None; }

            return svc.Begin(cue, follow != null ? follow.position : Vector3.zero,
                             follow, true, force2D: false, volumeScale: 1f);
        }

        /// <summary>루프를 멈춘다. 이미 멈췄거나 무효한 손잡이면 아무 일도 없다.</summary>
        public static void StopLoop(AudioHandle handle)
        {
            var svc = _instance;
            if (svc == null || !handle.IsValid) return;
            svc.End(handle);
        }

        /// <summary>이 손잡이가 아직 울리고 있는가.</summary>
        public static bool IsPlaying(AudioHandle handle)
        {
            var svc = _instance;
            if (svc == null || !handle.IsValid) return false;

            ref var v = ref svc._pool[handle.Index];
            return v.InUse && v.Serial == handle.Serial;
        }

        static void PlayInternal(AudioCueSO cue, Vector3 position, Transform follow,
                                 bool force2D, float volumeScale)
        {
            Requests++;

            // 여기가 이 클래스에서 가장 자주 지나가는 줄이다. 소리가 하나도 없는
            // 지금은 모든 호출이 여기서 끝난다 — 그래서 이 앞에 아무것도 두지 않는다.
            if (cue == null || !cue.HasClips) { Silent++; return; }

            var svc = Ensure();
            if (svc == null) { Silent++; return; }

            svc.Begin(cue, position, follow, cue.loop, force2D, volumeScale);
        }

        AudioHandle Begin(AudioCueSO cue, Vector3 position, Transform follow,
                          bool loop, bool force2D, float volumeScale)
        {
            float now = Time.unscaledTime;
            var clip = cue.PickClip(Random.value);
            if (clip == null) { Silent++; return AudioHandle.None; }

            float duration = loop ? AudioThrottle.Endless : clip.length / Mathf.Max(0.01f, cue.pitchMax);

            if (!_throttle.TryClaim(cue.CueId, now, cue.minIntervalSeconds, duration, cue.maxConcurrent))
            {
                Throttled++;
                return AudioHandle.None;
            }

            int index = TakeSlot(now);
            ref var v = ref _pool[index];

            var src = v.Source;
            src.clip = clip;
            src.loop = loop;
            src.pitch = cue.PickPitch(Random.value);
            src.spatialBlend = force2D ? 0f : cue.SpatialBlend;
            src.minDistance = cue.minDistance;
            src.maxDistance = Mathf.Max(cue.minDistance + 0.01f, cue.maxDistance);
            src.rolloffMode = cue.rolloff;
            src.outputAudioMixerGroup = _groups[(int)cue.bus];
            src.volume = cue.PickVolume(Random.value) * Mathf.Clamp01(volumeScale)
                         * _busVolume[(int)cue.bus] * _busVolume[(int)AudioBus.Master];

            src.transform.position = position;
            src.Play();

            v.CueId = cue.CueId;
            v.Loop = loop;
            v.InUse = true;
            v.StartedAt = now;
            v.Follow = follow;
            v.Serial = _nextSerial++;

            Played++;

            int busy = BusyCount;
            if (busy > PeakBusy) PeakBusy = busy;

            return new AudioHandle(index, v.Serial);
        }

        /// <summary>
        /// 쓸 자리를 하나 고른다. 비어 있는 것이 먼저고, 없으면 <b>가장 오래된 것</b>을
        /// 빼앗는다. 여기까지 오는 일은 겹침 막이가 이미 전체 상한에서 걸러 주므로
        /// 거의 없지만, 루프가 자리를 오래 붙들면 생길 수 있다.
        /// </summary>
        int TakeSlot(float now)
        {
            for (int n = 0; n < _pool.Length; n++)
            {
                int i = _cursor;
                _cursor = (_cursor + 1) % _pool.Length;

                ref var v = ref _pool[i];
                if (!v.InUse) return i;
                if (!v.Loop && !v.Source.isPlaying) { Retire(i); return i; }
            }

            int oldest = 0;
            float oldestAt = float.MaxValue;
            for (int i = 0; i < _pool.Length; i++)
            {
                if (_pool[i].StartedAt >= oldestAt) continue;
                oldestAt = _pool[i].StartedAt;
                oldest = i;
            }

            Retire(oldest);
            return oldest;
        }

        void End(AudioHandle handle)
        {
            ref var v = ref _pool[handle.Index];
            if (!v.InUse || v.Serial != handle.Serial) return;
            Retire(handle.Index);
        }

        void Retire(int index)
        {
            ref var v = ref _pool[index];
            if (v.Loop) _throttle.Release(v.CueId);

            v.Source.Stop();
            v.Source.clip = null;
            v.InUse = false;
            v.Loop = false;
            v.Follow = null;
            v.CueId = 0;
        }

        /// <summary>
        /// 다 운 자리를 걷고, 따라다니라고 한 소리를 따라 옮긴다.
        ///
        /// <c>LateUpdate</c>에 두는 이유는 따라갈 대상이 이미 그 프레임의 자리로
        /// 옮겨간 뒤여야 소리가 한 프레임 뒤처지지 않기 때문이다.
        /// </summary>
        void LateUpdate()
        {
            for (int i = 0; i < _pool.Length; i++)
            {
                ref var v = ref _pool[i];
                if (!v.InUse) continue;

                if (v.Follow != null) { v.Source.transform.position = v.Follow.position; continue; }

                // 따라가라고 한 대상이 사라졌으면 그 자리에서 마저 울리게 둔다.
                if (!v.Loop && !v.Source.isPlaying) Retire(i);
            }
        }
    }

    /// <summary>
    /// 돌고 있는 소리 하나를 가리키는 손잡이.
    ///
    /// 인덱스만으로는 부족하다 — 그 자리를 다른 소리가 이미 빼앗아 갔을 수 있고,
    /// 그때 옛 손잡이로 <c>StopLoop</c>을 부르면 남의 소리를 끈다.
    /// 일련번호를 함께 들고 다니며 같은 소리인지 확인한다.
    /// </summary>
    public readonly struct AudioHandle
    {
        public static readonly AudioHandle None = default;

        internal readonly int Index;
        internal readonly int Serial;

        internal AudioHandle(int index, int serial)
        {
            Index = index;
            Serial = serial;
        }

        public bool IsValid => Serial != 0;
    }
}
