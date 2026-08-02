using System;
using UnityEngine;
using Survive.Input;
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

        IHoldInteractable _holding;
        float _holdElapsed;
        string _lastPrompt;

        void Awake()
        {
            _player = GetComponentInParent<PlayerContext>();
            _playerRoot = _player != null ? _player.transform : transform.root;
            if (rayOrigin == null && Camera.main != null) rayOrigin = Camera.main.transform;
        }

        void OnEnable()
        {
            if (input == null) return;
            input.InteractEvent += BeginInteract;
            input.InteractCancelledEvent += CancelInteract;
        }

        void OnDisable()
        {
            if (input == null) return;
            input.InteractEvent -= BeginInteract;
            input.InteractCancelledEvent -= CancelInteract;
        }

        void Update()
        {
            RefreshTarget();
            AdvanceHold();
        }

        void RefreshTarget()
        {
            if (rayOrigin == null) return;

            // 카메라가 플레이어 콜라이더 안에 있어 SphereCast가 자기 자신을 거리 0에서
            // 명중시킨다. 단일 SphereCast로는 그 뒤가 보이지 않으므로 전부 받아
            // 자기 몸을 건너뛰고 가장 가까운 상호작용 대상을 고른다.
            var hits = Physics.SphereCastAll(rayOrigin.position, detectRadius, rayOrigin.forward,
                                             detectDistance, interactableMask,
                                             QueryTriggerInteraction.Collide);

            IInteractable found = null;
            float nearest = float.MaxValue;

            foreach (var hit in hits)
            {
                if (IsOwnBody(hit.collider)) continue;

                var candidates = hit.collider.GetComponentInParent<IInteractable>();
                if (candidates == null) continue;

                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                    found = candidates;
                }
            }

            if (!ReferenceEquals(found, Current))
            {
                if (_holding != null) CancelInteract();
                Current = found;
            }

            string prompt = null;
            if (Current != null && Current.CanInteract(_player))
                prompt = Current.InteractionPrompt;

            if (prompt != _lastPrompt)
            {
                _lastPrompt = prompt;
                PromptChanged?.Invoke(prompt);
            }
        }

        /// <summary>플레이어 자신의 콜라이더인지. 카메라가 몸 안에 있어 반드시 걸러야 한다.</summary>
        bool IsOwnBody(Collider col)
        {
            if (_playerRoot == null) return false;
            return col.transform == _playerRoot || col.transform.IsChildOf(_playerRoot);
        }

        void BeginInteract()
        {
            if (Current == null || !Current.CanInteract(_player)) return;

            if (Current is IHoldInteractable hold && hold.HoldDuration > 0f)
            {
                _holding = hold;
                _holdElapsed = 0f;
            }
            else
            {
                Current.Interact(_player);
            }
        }

        void CancelInteract()
        {
            if (_holding == null) return;
            _holding.OnHoldCancelled();
            _holding = null;
            _holdElapsed = 0f;
            HoldProgressChanged?.Invoke(0f);
        }

        void AdvanceHold()
        {
            if (_holding == null) return;

            _holdElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(_holdElapsed / _holding.HoldDuration);
            _holding.OnHoldProgress(progress);
            HoldProgressChanged?.Invoke(progress);

            if (progress >= 1f)
            {
                var toComplete = _holding;
                _holding = null;
                _holdElapsed = 0f;
                HoldProgressChanged?.Invoke(0f);
                toComplete.Interact(_player);
            }
        }
    }
}
