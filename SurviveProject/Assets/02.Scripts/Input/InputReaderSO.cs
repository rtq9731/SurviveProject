using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Survive.Input
{
    /// <summary>
    /// 입력을 이벤트로 바꾸는 유일한 통로.
    /// 다른 시스템은 UnityEngine.Input이나 InputAction을 직접 만지지 않는다.
    /// ScriptableObject라서 씬이 달라도 같은 에셋 하나를 공유한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Survive/Input/Input Reader")]
    public class InputReaderSO : ScriptableObject,
        PlayerInputActions.IGameplayActions,
        PlayerInputActions.IUIActions
    {
        PlayerInputActions _actions;

        public event Action<Vector2> MoveEvent;
        public event Action<Vector2> LookEvent;
        public event Action JumpEvent;
        public event Action<bool> SprintEvent;
        public event Action InteractEvent;
        public event Action InteractCancelledEvent;
        public event Action AttackEvent;
        public event Action ToggleInventoryEvent;
        public event Action NextToolEvent;
        public event Action ToggleLanternEvent;
        public event Action PauseEvent;
        public event Action CancelEvent;

        public Vector2 MoveValue { get; private set; }
        public Vector2 LookValue { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsInteractHeld { get; private set; }

        void OnEnable()
        {
            if (_actions == null)
            {
                _actions = new PlayerInputActions();
                _actions.Gameplay.SetCallbacks(this);
                _actions.UI.SetCallbacks(this);
            }
            EnableGameplayInput();
        }

        void OnDisable() => DisableAllInput();

        public void EnableGameplayInput()
        {
            if (_actions == null) return;
            _actions.UI.Disable();
            _actions.Gameplay.Enable();
        }

        public void EnableUIInput()
        {
            if (_actions == null) return;
            _actions.Gameplay.Disable();
            _actions.UI.Enable();

            // 게임플레이 맵을 끄면 콜백이 오지 않으므로 이동이 눌린 채로 남는다.
            ResetValues();
            MoveEvent?.Invoke(Vector2.zero);
            LookEvent?.Invoke(Vector2.zero);
        }

        public void DisableAllInput()
        {
            _actions?.Gameplay.Disable();
            _actions?.UI.Disable();
            ResetValues();
        }

        void ResetValues()
        {
            MoveValue = Vector2.zero;
            LookValue = Vector2.zero;
            IsSprinting = false;
            IsInteractHeld = false;
        }

        // ── Gameplay ─────────────────────────────────────────────

        public void OnMove(InputAction.CallbackContext ctx)
        {
            MoveValue = ctx.ReadValue<Vector2>();
            MoveEvent?.Invoke(MoveValue);
        }

        public void OnLook(InputAction.CallbackContext ctx)
        {
            LookValue = ctx.ReadValue<Vector2>();
            LookEvent?.Invoke(LookValue);
        }

        public void OnJump(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) JumpEvent?.Invoke();
        }

        public void OnSprint(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) { IsSprinting = true; SprintEvent?.Invoke(true); }
            else if (ctx.canceled) { IsSprinting = false; SprintEvent?.Invoke(false); }
        }

        public void OnInteract(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) { IsInteractHeld = true; InteractEvent?.Invoke(); }
            else if (ctx.canceled) { IsInteractHeld = false; InteractCancelledEvent?.Invoke(); }
        }

        public void OnAttack(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) AttackEvent?.Invoke();
        }

        public void OnToggleInventory(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) ToggleInventoryEvent?.Invoke();
        }

        public void OnPause(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) PauseEvent?.Invoke();
        }

        public void OnNextTool(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) NextToolEvent?.Invoke();
        }

        public void OnToggleLantern(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) ToggleLanternEvent?.Invoke();
        }

        // ── UI ───────────────────────────────────────────────────

        public void OnPoint(InputAction.CallbackContext ctx) { }
        public void OnClick(InputAction.CallbackContext ctx) { }

        public void OnCancel(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) CancelEvent?.Invoke();
        }
    }
}
