using UnityEngine;
using UnityEngine.InputSystem;
using Survive.Core;
using Survive.Player;

namespace Survive.Building
{
    /// <summary>
    /// 건설 모드의 입력.
    ///
    /// InputActions 에셋을 건드리지 않고 여기서 직접 읽는다. 퀵슬롯과 같은 판단이다 —
    /// 건설은 임시로 켜지는 모드라 액션맵을 하나 더 만들 만한 무게가 아니고,
    /// 키를 바꿀 때 손이 한 군데만 가면 된다.
    ///
    ///   B        건설 목록 열기/닫기
    ///   좌클릭    세운다
    ///   휠        회전
    ///   우클릭/ESC 취소
    /// </summary>
    [DisallowMultipleComponent]
    public class BuildModeController : MonoBehaviour
    {
        [SerializeField] BuildPlacer placer;
        [SerializeField] Survive.UI.BuildMenuUI menu;

        [Tooltip("건설 중에는 도구를 휘두르지 않는다")]
        [SerializeField] Survive.Combat.MeleeSwing melee;

        [SerializeField] float rotateStepPerNotch = 15f;

        void Awake()
        {
            if (placer == null) placer = GetComponentInParent<BuildPlacer>();
            if (melee == null) melee = GetComponentInParent<Survive.Combat.MeleeSwing>();
            if (menu == null) menu = Object.FindFirstObjectByType<Survive.UI.BuildMenuUI>(FindObjectsInactive.Include);
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null) return;

            if (kb.bKey.wasPressedThisFrame) ToggleMenu();

            if (placer == null || !placer.IsActive) return;

            // 건설 중에는 곡괭이가 나가면 안 된다
            if (melee != null && melee.enabled) melee.enabled = false;

            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    placer.Rotate(Mathf.Sign(scroll) * rotateStepPerNotch);

                if (mouse.leftButton.wasPressedThisFrame) placer.TryBuild();
                if (mouse.rightButton.wasPressedThisFrame) Exit();
            }

            if (kb.escapeKey.wasPressedThisFrame) Exit();
        }

        void ToggleMenu()
        {
            if (menu == null) return;

            if (menu.IsOpen) { menu.Close(); Exit(); }
            else menu.Open();
        }

        /// <summary>건설 모드에서 빠져나온다.</summary>
        public void Exit()
        {
            placer?.Cancel();
            if (melee != null) melee.enabled = true;
        }
    }
}
