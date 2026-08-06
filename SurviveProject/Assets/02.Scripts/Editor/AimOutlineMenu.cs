using UnityEditor;
using UnityEngine;
using Survive.Interaction;

namespace Survive.EditorTools
{
    /// <summary>
    /// 조준 윤곽을 껐다 켜는 스위치.
    ///
    /// 취향을 타는 연출이라 "일단 꺼 보고 싶다"가 반드시 나온다. 그때 코드를 고치고
    /// 다시 컴파일하게 만들면 아무도 안 끈다. 플레이 중에도 즉시 먹는다.
    /// 색·세기까지 바꾸려면 <see cref="AimOutline"/>의 맨 위 필드들을 본다.
    /// </summary>
    public static class AimOutlineMenu
    {
        const string Path = "Tools/Survive/조준 윤곽 표시";

        [MenuItem(Path)]
        static void Toggle() => AimOutline.Enabled = !AimOutline.Enabled;

        [MenuItem(Path, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(Path, AimOutline.Enabled);
            return true;
        }
    }
}
