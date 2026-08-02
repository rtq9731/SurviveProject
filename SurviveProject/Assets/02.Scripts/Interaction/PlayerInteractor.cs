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

        public IInteractable Current { get; private set; }

        public event Action<string> PromptChanged;       // null이면 숨김
        public event Action<float> HoldProgressChanged;  // 0~1

        IHoldInteractable _누르는중;
        float _누른시간;
        string _마지막문구;

        void Awake()
        {
            _player = GetComponentInParent<PlayerContext>();
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

            IInteractable 찾은것 = null;
            if (Physics.SphereCast(rayOrigin.position, detectRadius, rayOrigin.forward,
                                   out var hit, detectDistance, interactableMask,
                                   QueryTriggerInteraction.Collide))
            {
                찾은것 = hit.collider.GetComponentInParent<IInteractable>();
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
