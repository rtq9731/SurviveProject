using System;
using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.World;

namespace Survive.Player
{
    /// <summary>
    /// 수영. 물에 들어가면 이동 규칙이 바뀐다.
    ///
    /// 상태는 셋이다.
    ///   Dry        물 밖. 평소 이동
    ///   Wading     발은 물에 잠겼지만 머리는 밖. 느려질 뿐 걷는다
    ///   Swimming   몸이 뜬다. 중력이 사라지고 위아래로 움직인다
    ///
    /// 머리까지 잠기면 산소가 닳는다 — 지하 대기는 호흡할 수 있지만 물속은 아니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerSwimming : MonoBehaviour
    {
        public enum State { Dry, Wading, Swimming }

        [Tooltip("이 높이 이상 잠기면 헤엄친다 (발바닥 기준)")]
        [SerializeField] float swimDepth = 1.15f;

        [Tooltip("이 높이 이상 잠기면 걷기가 느려진다")]
        [SerializeField] float wadeDepth = 0.35f;

        [Tooltip("머리 위치. 여기가 잠기면 산소가 닳는다")]
        [SerializeField] Transform headPoint;

        [Header("피드백")]
        [Tooltip("물에 들어갈 때")]
        [SerializeField] MMF_Player enterFeedback;

        [Tooltip("물에서 나올 때")]
        [SerializeField] MMF_Player exitFeedback;

        [Tooltip("머리까지 잠길 때")]
        [SerializeField] MMF_Player submergeFeedback;

        CharacterController _cc;
        State _상태 = State.Dry;
        float _수면 = float.MinValue;
        bool _물있음;
        bool _머리잠김;

        public State Current => _상태;
        public bool IsSwimming => _상태 == State.Swimming;
        public bool IsWading => _상태 == State.Wading;

        /// <summary>머리까지 잠겼는가. 산소 소모 판정에 쓴다.</summary>
        public bool IsHeadSubmerged => _머리잠김;

        /// <summary>발바닥 기준 잠긴 깊이. 물이 없으면 0.</summary>
        public float SubmergedDepth =>
            _물있음 ? Mathf.Max(0f, _수면 - 발바닥Y) : 0f;

        /// <summary>수면까지의 거리. 양수면 아직 물속이다.</summary>
        public float DepthBelowSurface =>
            _물있음 && headPoint != null ? _수면 - headPoint.position.y : 0f;

        public event Action<State> StateChanged;

        float 발바닥Y => transform.position.y - _cc.height * 0.5f + _cc.center.y;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (headPoint == null) headPoint = transform;
        }

        void Update()
        {
            _물있음 = WaterBody.TryGetSurfaceAt(transform.position, out _수면);

            var 이전 = _상태;
            var 이전머리 = _머리잠김;

            if (!_물있음)
            {
                _상태 = State.Dry;
                _머리잠김 = false;
            }
            else
            {
                float 깊이 = _수면 - 발바닥Y;
                if (깊이 >= swimDepth) _상태 = State.Swimming;
                else if (깊이 >= wadeDepth) _상태 = State.Wading;
                else _상태 = State.Dry;

                _머리잠김 = headPoint.position.y < _수면;
            }

            if (_상태 != 이전)
            {
                if (이전 == State.Dry && _상태 != State.Dry) enterFeedback?.PlayFeedbacks();
                else if (_상태 == State.Dry) exitFeedback?.PlayFeedbacks();
                StateChanged?.Invoke(_상태);
            }

            if (_머리잠김 && !이전머리) submergeFeedback?.PlayFeedbacks();
        }

        /// <summary>
        /// 수면 근처에서 부력을 죽여 물 밖으로 튀어나오지 않게 한다.
        ///
        /// 절대 막아서는 안 된다. 머리를 수면 위로 못 올라가게 하면
        /// 물가에 닿아도 뭍으로 기어오를 수 없어 물에 갇힌다.
        /// 부력만 줄이고, 지면을 밟았거나 스스로 올라가려 할 때는 그대로 둔다.
        /// </summary>
        public float DampBuoyancyNearSurface(float verticalVelocity, bool grounded, bool ascending)
        {
            if (_상태 != State.Swimming) return verticalVelocity;

            // 바닥(호수 바닥·물가 경사)을 밟았으면 손대지 않는다. 걸어 나가야 한다.
            if (grounded) return verticalVelocity;

            // 스스로 올라가려는 조작은 존중한다.
            if (ascending) return verticalVelocity;

            float 머리 = headPoint.position.y;
            if (verticalVelocity <= 0f) return verticalVelocity;

            // 수면에 가까울수록 부력을 줄인다. 딱 끊지 않고 서서히.
            float 여유 = 0.35f;
            float 남은거리 = _수면 - 머리;
            if (남은거리 >= 여유) return verticalVelocity;

            return verticalVelocity * Mathf.Clamp01(남은거리 / 여유);
        }
    }
}
