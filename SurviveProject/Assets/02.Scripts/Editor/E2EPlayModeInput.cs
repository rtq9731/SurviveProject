using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Survive.EditorTools
{
    /// <summary>
    /// E2E를 돌리기 전에 게임 뷰가 앞에 없어도 입력이 전달되게 해 둔다.
    ///
    /// <b>왜 따로 있는가.</b> 이 설정은 <see cref="InputSettings"/> <b>에셋</b>에 적힌다.
    /// 재생 중에 쓰면 Unity가 에셋 변경으로 보고 도메인을 다시 올리면서
    /// <b>재생 모드가 그 자리에서 꺼진다</b>(실측 확인 — 시나리오가 첫 줄만 찍고
    /// 조용히 멈춘 원인이 이것이었다). 그래서 편집 모드에서만 만진다.
    ///
    /// <b>왜 자동으로 켜지 않는가.</b> 켜 두면 사람이 다른 창을 보고 있어도
    /// 키가 게임으로 들어간다. 검사를 돌리는 동안에만 필요한 것이라
    /// 부르는 쪽이 명시적으로 켠다 — 자동화(uloop 드라이버)는 재생 전에 이것을 부르고,
    /// 사람은 메뉴로 켠다.
    ///
    /// 얼어붙은 <b>진짜</b> 장치의 상태를 떼어 내는 나머지 절반은
    /// <see cref="Survive.Testing.E2EHarness.IsolateInput"/>이 재생 중에 맡는다.
    /// </summary>
    public static class E2EPlayModeInput
    {
        const string EnableMenu = "Tools/Survive/E2E 입력 포커스 우회 켜기";
        const string DisableMenu = "Tools/Survive/E2E 입력 포커스 우회 끄기";

        [MenuItem(EnableMenu)]
        public static void Enable()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[E2EPlayModeInput] 재생 중에는 켤 수 없습니다 — " +
                                 "설정 에셋이 바뀌면 재생 모드가 꺼집니다. 먼저 재생을 멈추십시오.");
                return;
            }

            InputSystem.settings.backgroundBehavior =
                InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

            Debug.Log("[E2EPlayModeInput] 게임 뷰가 앞에 없어도 입력이 전달됩니다.");
        }

        [MenuItem(DisableMenu)]
        public static void Disable()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[E2EPlayModeInput] 재생 중에는 끌 수 없습니다.");
                return;
            }

            InputSystem.settings.backgroundBehavior =
                InputSettings.BackgroundBehavior.ResetAndDisableNonBackgroundDevices;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.PointersAndKeyboardsRespectGameViewFocus;

            Debug.Log("[E2EPlayModeInput] 입력이 다시 게임 뷰 포커스를 따릅니다.");
        }

        /// <summary>지금 켜져 있는가. 드라이버가 물어볼 수 있게 열어 둔다.</summary>
        public static bool IsEnabled =>
            InputSystem.settings.backgroundBehavior == InputSettings.BackgroundBehavior.IgnoreFocus;
    }
}
