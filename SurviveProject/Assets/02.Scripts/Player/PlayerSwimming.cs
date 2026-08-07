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
    /// 머리까지 잠기면 산소가 닳는다 - 대기는 호흡할 수 있지만 액체 속은 아니다.
    ///
    /// <b>액체의 종류는 이동을 바꾸지 않는다.</b> 호수든 매크로늄 바다든 똑같이
    /// 헤엄치고 똑같이 숨이 막힌다(<see cref="LiquidKind"/>). 종류가 답을 바꾸는 것은
    /// 부식 하나뿐이므로, 여기서는 <b>지금 잠긴 액체가 무엇인지 알려 주기만</b> 하고
    /// 그것으로 무엇을 할지는 <see cref="Survive.World.LiquidContactService"/>가 정한다.
    /// 종류를 여기서 판정에 쓰기 시작하면 임계가 둘로 갈린다.
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
        State _state = State.Dry;
        float _surfaceY = float.MinValue;
        bool _inWater;
        LiquidKind _kind;
        bool _headSubmerged;

        public State Current => _state;
        public bool IsSwimming => _state == State.Swimming;
        public bool IsWading => _state == State.Wading;

        /// <summary>
        /// 머리까지 잠겼는가. 산소 소모 판정에 쓴다.
        ///
        /// <b>종류를 보지 않는다.</b> 호수에서 잠수해도 산소는 똑같이 준다 - 숨은
        /// 액체의 성분이 아니라 머리가 잠겼는가의 문제이고, 여기서 종류를 보기
        /// 시작하면 "호수에서는 숨을 쉴 수 있다"가 되어 버린다(기획서 §5.1).
        /// </summary>
        public bool IsHeadSubmerged => _headSubmerged;

        /// <summary>
        /// 지금 몸이 든 액체가 무엇인가. 물 밖이면 false.
        /// <b>부식을 매기는 쪽이 종류를 묻는 유일한 창구다.</b>
        /// </summary>
        public bool TryGetLiquid(out LiquidKind kind)
        {
            kind = _kind;
            return _inWater;
        }

        /// <summary>발바닥 기준 잠긴 깊이. 물이 없으면 0.</summary>
        public float SubmergedDepth =>
            _inWater ? Mathf.Max(0f, _surfaceY - FeetY) : 0f;

        /// <summary>수면까지의 거리. 양수면 아직 물속이다.</summary>
        public float DepthBelowSurface =>
            _inWater && headPoint != null ? _surfaceY - headPoint.position.y : 0f;

        public event Action<State> StateChanged;

        float FeetY => transform.position.y - _cc.height * 0.5f + _cc.center.y;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (headPoint == null) headPoint = transform;
        }

        void Update()
        {
            _inWater = WaterBody.TryGetAt(transform.position, out _surfaceY, out _kind);

            var prev = _state;
            var prevHead = _headSubmerged;

            if (!_inWater)
            {
                _state = State.Dry;
                _headSubmerged = false;
            }
            else
            {
                float depth = _surfaceY - FeetY;
                if (depth >= swimDepth) _state = State.Swimming;
                else if (depth >= wadeDepth) _state = State.Wading;
                else _state = State.Dry;

                _headSubmerged = headPoint.position.y < _surfaceY;
            }

            if (_state != prev)
            {
                if (prev == State.Dry && _state != State.Dry) enterFeedback?.PlayFeedbacks();
                else if (_state == State.Dry) exitFeedback?.PlayFeedbacks();
                StateChanged?.Invoke(_state);
            }

            if (_headSubmerged && !prevHead) submergeFeedback?.PlayFeedbacks();
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
            if (_state != State.Swimming) return verticalVelocity;

            // 바닥(호수 바닥·물가 경사)을 밟았으면 손대지 않는다. 걸어 나가야 한다.
            if (grounded) return verticalVelocity;

            // 스스로 올라가려는 조작은 존중한다.
            if (ascending) return verticalVelocity;

            float headY = headPoint.position.y;
            if (verticalVelocity <= 0f) return verticalVelocity;

            // 수면에 가까울수록 부력을 줄인다. 딱 끊지 않고 서서히.
            float margin = 0.35f;
            float toSurface = _surfaceY - headY;
            if (toSurface >= margin) return verticalVelocity;

            return verticalVelocity * Mathf.Clamp01(toSurface / margin);
        }
    }
}
