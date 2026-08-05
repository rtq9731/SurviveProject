using UnityEngine;
using Survive.Audio;
using Survive.Domain.Audio;

namespace Survive.Player
{
    /// <summary>
    /// 걸음에 소리를 붙인다.
    ///
    /// <b>발소리는 사건이 아니라 리듬이다.</b> 다른 소리는 "맞았다", "주웠다" 같은
    /// 순간에 붙지만 발소리는 그런 순간이 코드 어디에도 없다 — 걷는 동안 일정한
    /// 간격으로 스스로 나야 한다. 그래서 부를 자리를 만드는 대신 자기가 재는
    /// 컴포넌트를 하나 둔다. 간격 규칙은 <see cref="FootstepCadence"/>에 있고
    /// 여기서는 상태를 읽어 넘기기만 한다.
    ///
    /// <b>스스로 붙는다.</b> <see cref="PlayerLocomotion"/>이 깨어나면서 붙인다 —
    /// 플레이어 프리팹을 고치지 않기 위해서다. 그래서 인스펙터에서 꽂을 수 없고,
    /// 소리는 <see cref="AudioService.Book"/>에서 가져온다.
    ///
    /// <b>소리가 없으면 아무 일도 하지 않는다.</b> 지금이 그 상태다 —
    /// 표가 없으면 <see cref="AudioService.Play"/>가 첫 줄에서 돌아간다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerFootsteps : MonoBehaviour
    {
        [Tooltip("걷는 소리. 비우면 소리 표의 footstepWalk를 쓴다")]
        [SerializeField] AudioCueSO walkCue;

        [Tooltip("뛰는 소리. 비우면 소리 표의 footstepRun을 쓴다")]
        [SerializeField] AudioCueSO runCue;

        [Tooltip("착지 소리. 비우면 소리 표의 land를 쓴다")]
        [SerializeField] AudioCueSO landCue;

        [Tooltip("도약 소리. 비우면 소리 표의 jump를 쓴다")]
        [SerializeField] AudioCueSO jumpCue;

        [Tooltip("소리가 나는 자리. 비우면 발밑으로 잡는다")]
        [SerializeField] Transform feet;

        PlayerLocomotion _locomotion;
        PlayerSwimming _swimming;
        CharacterController _cc;

        float _sinceLastStep;
        int _lastLandingCount;

        /// <summary>지금까지 낸 발소리 수. 소리가 없어도 센다 — 리듬만 따로 검증하려고 둔다.</summary>
        public int StepCount { get; private set; }

        /// <summary>마지막 걸음이 뛰는 걸음이었는가.</summary>
        public bool LastStepWasRun { get; private set; }

        void Awake()
        {
            _locomotion = GetComponent<PlayerLocomotion>();
            _swimming = GetComponent<PlayerSwimming>();
            _cc = GetComponent<CharacterController>();
            if (feet == null) feet = transform;
            if (_locomotion != null) _lastLandingCount = _locomotion.LandingCount;
        }

        void Update()
        {
            if (_locomotion == null) return;

            // 물속에서는 발이 땅에 닿지 않는다. 헤엄 소리는 이 컴포넌트의 일이 아니다.
            bool swimming = _swimming != null && _swimming.IsSwimming;

            _sinceLastStep += Time.deltaTime;

            if (_locomotion.LandingCount != _lastLandingCount)
            {
                _lastLandingCount = _locomotion.LandingCount;
                if (!swimming) Land();
            }

            if (swimming) return;

            float speed = _locomotion.CurrentSpeed;
            if (!FootstepCadence.ShouldStep(_locomotion.IsGrounded, speed, _sinceLastStep)) return;

            Step(FootstepCadence.IsRunning(speed));
        }

        void Step(bool running)
        {
            _sinceLastStep = 0f;
            StepCount++;
            LastStepWasRun = running;

            var book = AudioService.Book;
            var cue = running
                ? AudioCueBookSO.Or(runCue, book != null ? book.footstepRun : null)
                : AudioCueBookSO.Or(walkCue, book != null ? book.footstepWalk : null);

            AudioService.Play(cue, FootPosition());
        }

        void Land()
        {
            // 착지한 자리에서 다시 리듬을 센다. 안 그러면 떨어진 직후 한 걸음이
            // 착지음과 겹쳐 두 소리가 한 덩어리로 들린다.
            _sinceLastStep = 0f;

            var book = AudioService.Book;
            AudioService.Play(AudioCueBookSO.Or(landCue, book != null ? book.land : null),
                              FootPosition());
        }

        /// <summary>
        /// 도약 순간. 지금 부르는 곳은 없다 — 점프 소리를 넣기로 하면
        /// <see cref="PlayerLocomotion"/>의 점프 성사 지점에서 이것을 부른다.
        /// </summary>
        public void Jump()
        {
            var book = AudioService.Book;
            AudioService.Play(AudioCueBookSO.Or(jumpCue, book != null ? book.jump : null),
                              FootPosition());
        }

        /// <summary>소리가 날 자리. 귀(카메라)가 아니라 발에서 나야 거리감이 산다.</summary>
        Vector3 FootPosition()
        {
            if (_cc == null) return feet.position;
            return transform.position + Vector3.down * (_cc.height * 0.5f - _cc.center.y);
        }
    }
}
