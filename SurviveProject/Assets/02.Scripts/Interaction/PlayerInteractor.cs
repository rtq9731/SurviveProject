using System;
using UnityEngine;
using Survive.InputSystem;
using Survive.Player;

namespace Survive.Interaction
{
    /// <summary>
    /// 카메라 전방을 훑어 상호작용 대상을 찾고 실행한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] InputReaderSO input;
        [SerializeField] Transform rayOrigin;      // 보통 카메라
        [SerializeField] float detectDistance = 3f;
        [SerializeField] float detectRadius = 0.3f;
        [SerializeField] LayerMask interactableMask = ~0;

        PlayerContext _player;
        Transform _playerRoot;

        public IInteractable Current { get; private set; }

        public event Action<string> PromptChanged;       // null이면 숨김
        public event Action<float> HoldProgressChanged;  // 0~1

        IHoldInteractable _누르는중;
        float _누른시간;
        string _마지막문구;

        void Awake()
        {
            _player = GetComponentInParent<PlayerContext>();
            _playerRoot = _player != null ? _player.transform : transform.root;
            if (rayOrigin == null && Camera.main != null) rayOrigin = Camera.main.transform;
        }

        void OnEnable()
        {
            if (input == null) return;
            input.InteractEvent += 상호작용시작;
            input.InteractCancelledEvent += 상호작용취소;
        }

        void OnDisable()
        {
            if (input == null) return;
            input.InteractEvent -= 상호작용시작;
            input.InteractCancelledEvent -= 상호작용취소;
        }

        void Update()
        {
            대상갱신();
            누름진행();
        }

        void 대상갱신()
        {
            if (rayOrigin == null) return;

            // 카메라가 플레이어 콜라이더 안에 있어 SphereCast가 자기 자신을 거리 0에서
            // 명중시킨다. 단일 SphereCast로는 그 뒤가 보이지 않으므로 전부 받아
            // 자기 몸을 건너뛰고 가장 가까운 상호작용 대상을 고른다.
            var hits = Physics.SphereCastAll(rayOrigin.position, detectRadius, rayOrigin.forward,
                                             detectDistance, interactableMask,
                                             QueryTriggerInteraction.Collide);

            IInteractable 찾은것 = null;
            float 가장가까운 = float.MaxValue;

            foreach (var hit in hits)
            {
                if (자기몸인가(hit.collider)) continue;

                var 후보 = hit.collider.GetComponentInParent<IInteractable>();
                if (후보 == null) continue;

                if (hit.distance < 가장가까운)
                {
                    가장가까운 = hit.distance;
                    찾은것 = 후보;
                }
            }

            if (!ReferenceEquals(찾은것, Current))
            {
                if (_누르는중 != null) 상호작용취소();
                Current = 찾은것;
            }

            string 문구 = null;
            if (Current != null && Current.CanInteract(_player))
                문구 = Current.InteractionPrompt;

            if (문구 != _마지막문구)
            {
                _마지막문구 = 문구;
                PromptChanged?.Invoke(문구);
            }
        }

        /// <summary>플레이어 자신의 콜라이더인지. 카메라가 몸 안에 있어 반드시 걸러야 한다.</summary>
        bool 자기몸인가(Collider col)
        {
            if (_playerRoot == null) return false;
            return col.transform == _playerRoot || col.transform.IsChildOf(_playerRoot);
        }

        void 상호작용시작()
        {
            if (Current == null || !Current.CanInteract(_player)) return;

            if (Current is IHoldInteractable hold && hold.HoldDuration > 0f)
            {
                _누르는중 = hold;
                _누른시간 = 0f;
            }
            else
            {
                Current.Interact(_player);
            }
        }

        void 상호작용취소()
        {
            if (_누르는중 == null) return;
            _누르는중.OnHoldCancelled();
            _누르는중 = null;
            _누른시간 = 0f;
            HoldProgressChanged?.Invoke(0f);
        }

        void 누름진행()
        {
            if (_누르는중 == null) return;

            _누른시간 += Time.deltaTime;
            float 진행도 = Mathf.Clamp01(_누른시간 / _누르는중.HoldDuration);
            _누르는중.OnHoldProgress(진행도);
            HoldProgressChanged?.Invoke(진행도);

            if (진행도 >= 1f)
            {
                var 완료할것 = _누르는중;
                _누르는중 = null;
                _누른시간 = 0f;
                HoldProgressChanged?.Invoke(0f);
                완료할것.Interact(_player);
            }
        }
    }
}
