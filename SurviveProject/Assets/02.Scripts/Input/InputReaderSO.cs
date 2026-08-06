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
        public event Action PauseEvent;
        public event Action CancelEvent;

        public Vector2 MoveValue { get; private set; }
        public Vector2 LookValue { get; private set; }
        public bool IsSprinting { get; private set; }

        /// <summary>점프 키가 눌려 있는가. 물속 상승처럼 계속 미는 조작에 쓴다.</summary>
        public bool IsJumpHeld { get; private set; }

        /// <summary>하강 키(Ctrl)가 눌려 있는가. 물속에서 가라앉는 데 쓴다.</summary>
        public bool IsDescending { get; private set; }

        /// <summary>공격 버튼이 눌려 있는가. 꾹 누른 채로 계속 휘두르는 데 쓴다.</summary>
        public bool IsAttackHeld { get; private set; }
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
            IsJumpHeld = false;
            IsDescending = false;
            IsAttackHeld = false;
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
            // 땅에서는 한 번 누르는 것이지만 물속에서는 누르고 있는 것이다.
            // 눌린 상태를 함께 내보낸다 — SprintEvent와 같은 방식이다.
            if (ctx.performed) { IsJumpHeld = true; JumpEvent?.Invoke(); }
            else if (ctx.canceled) IsJumpHeld = false;
        }

        public void OnSprint(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) { IsSprinting = true; SprintEvent?.Invoke(true); }
            else if (ctx.canceled) { IsSprinting = false; SprintEvent?.Invoke(false); }
        }

        /// <summary>
        /// 하강. 전에는 Shift가 물속에서 하강이었는데, 땅에서는 달리기라
        /// 물에 들어가는 순간 같은 손가락이 다른 뜻이 됐다.
        /// Shift는 어디서나 빨라지는 것으로 두고 하강은 Ctrl로 뺀다.
        /// </summary>
        public void OnDescend(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) IsDescending = true;
            else if (ctx.canceled) IsDescending = false;
        }

        public void OnInteract(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) { IsInteractHeld = true; InteractEvent?.Invoke(); }
            else if (ctx.canceled) { IsInteractHeld = false; InteractCancelledEvent?.Invoke(); }
        }

        public void OnAttack(InputAction.CallbackContext ctx)
        {
            // 한 번 누른 순간과 누르고 있는 상태를 둘 다 내보낸다.
            // 채집은 같은 바위를 여러 번 때려야 하는데, 그때마다 클릭하게 두면
            // 손가락만 아프고 얻는 것은 같다.
            if (ctx.performed) { IsAttackHeld = true; AttackEvent?.Invoke(); }
            else if (ctx.canceled) IsAttackHeld = false;
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

        // 랜턴 토글(F)은 없앴다. 랜턴은 상시 점등이 전제이고 끄는 선택지를 두지
        // 않는다(스펙 §12) — 스위치가 있으면 최적해가 "어두운 데서는 꺼 둔다"가
        // 되어 어둠이 매 순간의 비용이 아니라 가끔 내는 요금이 된다.
        // 규칙은 Survive.World.LanternRule에 있다.

        // ── UI ───────────────────────────────────────────────────

        public void OnPoint(InputAction.CallbackContext ctx) { }
        public void OnClick(InputAction.CallbackContext ctx) { }

        public void OnCancel(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) CancelEvent?.Invoke();
        }
    }
}
