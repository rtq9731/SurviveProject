using UnityEngine;
using Survive.Audio;
using Survive.Domain.Audio;
using Survive.Player;

namespace Survive.Creatures
{
    /// <summary>
    /// 다가오는 것의 기척.
    ///
    /// <b>이 게임에서 소리가 가장 중요한 자리다.</b> 환경광이 0이라 랜턴이 닿지 않는
    /// 곳은 진짜로 검고, 낫은 그 검은 데서 온다. 화면으로는 알 수 없는 것을
    /// 귀로 알아채는 순간 — 이 컴포넌트가 만들 자리가 그것이다.
    ///
    /// 멀면 뜸하게, 가까우면 자주 낸다. <b>간격이 곧 거리 정보</b>이기 때문이다.
    /// 볼륨만으로는 "저쪽에 있다"까지고, 얼마나 급한지는 알 수 없다.
    /// 그 규칙은 <see cref="ApproachAudio"/>에 있다.
    ///
    /// <b>스스로 붙는다.</b> <see cref="CreatureBrain"/>이 깨어나면서, 그 종의
    /// 정의에 접근음이 꽂혀 있을 때만 붙인다 — 낫 프리팹을 고치지 않기 위해서고,
    /// 소리가 없는 종에는 아예 <c>Update</c>가 돌지 않게 하기 위해서다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CreatureApproachAudio : MonoBehaviour
    {
        /// <summary>플레이어를 다시 찾아보는 주기(초). 매 프레임 씬을 훑을 이유가 없다.</summary>
        const float PlayerScanSeconds = 1f;

        CreatureBrain _brain;
        CreatureDefinitionSO _definition;
        Transform _player;
        float _nextScan;
        float _nextCry;

        /// <summary>
        /// 기척을 들을 수 있는 것들. <b>간격은 「가장 가까운 귀」가 정한다</b> —
        /// 기척은 이 개체가 내는 하나의 소리이고, 그 급함은 제일 가까운 사람에게
        /// 맞춰져야 한다(스펙 §22). 지금은 언제나 하나뿐이라 예전과 답이 같다.
        /// 목록을 들고 있는 것은 매 프레임 할당을 만들지 않기 위해서다.
        /// </summary>
        readonly System.Collections.Generic.List<ThreatSighting> _listeners =
            new System.Collections.Generic.List<ThreatSighting>(1);

        /// <summary>지금까지 낸 기척의 수. 소리가 없어도 센다 — 리듬만 따로 볼 때 쓴다.</summary>
        public int CryCount { get; private set; }

        /// <summary>마지막으로 잰 거리. 검증에서 "정말 가까워졌는가"를 값으로 집는다.</summary>
        public float LastDistance { get; private set; } = float.MaxValue;

        /// <summary>이 종의 접근음과 들리는 범위를 물려받는다.</summary>
        public void Bind(CreatureDefinitionSO definition)
        {
            _definition = definition;
        }

        void Awake()
        {
            _brain = GetComponent<CreatureBrain>();
            if (_definition == null && TryGetComponent<Survive.Combat.CreatureHealth>(out var health))
                _definition = health.Definition;
        }

        void Update()
        {
            if (_definition == null || _definition.approachCue == null) return;

            // 죽은 것은 다가오지 않는다.
            if (_brain != null && _brain.State == CreatureState.Dead) return;

            if (_player == null && Time.time >= _nextScan)
            {
                _nextScan = Time.time + PlayerScanSeconds;
                var ctx = FindAnyObjectByType<PlayerContext>(FindObjectsInactive.Exclude);
                if (ctx != null) _player = ctx.transform;
            }

            if (_player == null) return;

            _listeners.Clear();
            _listeners.Add(new ThreatSighting(Vector3.Distance(transform.position, _player.position)));

            float distance = ThreatRoster.From(_listeners).NearestDistance;
            LastDistance = distance;

            float range = _definition.audibleRange;
            if (!ApproachAudio.IsAudible(distance, range))
            {
                // 범위를 벗어나면 다음에 들어오는 순간 바로 한 번 울리게 둔다.
                _nextCry = 0f;
                return;
            }

            if (Time.time < _nextCry) return;
            _nextCry = Time.time + ApproachAudio.IntervalSeconds(distance, range);

            CryCount++;

            // 3D 감쇠는 AudioService가 이미 건다. 여기서 얹는 것은 그 위의 연출분 —
            // 들리기 시작하는 언저리에서는 기척만 남고, 코앞이면 온전히 들린다.
            AudioService.Play(_definition.approachCue, transform.position,
                              ApproachAudio.Loudness(distance, range));
        }
    }
}
