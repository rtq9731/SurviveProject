using UnityEngine;

namespace Survive.Interaction
{
    /// <summary>
    /// 아이콘 판이 언제나 카메라를 마주 보게 돌린다.
    ///
    /// 판은 두께가 없어서 옆에서 보면 사라진다. 낙하물 뿌리는 여전히 천천히 도는데
    /// (그 회전은 프리팹 낙하물의 연출이다), 판은 그 회전을 무시하고 화면을 향해야 한다.
    /// 그래서 회전을 덮어쓰는 자식으로 붙인다 — 뿌리의 트윈은 그대로 두고
    /// 판만 매 프레임 다시 세우면 두 연출이 싸우지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class DropBillboard : MonoBehaviour
    {
        Camera _camera;

        void LateUpdate()
        {
            // 카메라는 씬 전환·리스폰으로 갈릴 수 있다. 죽은 참조면 다시 찾는다.
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            // 카메라 위치를 바라보게 하면 화면 가장자리의 판이 안쪽으로 기운다.
            // 시선축과 나란히 두면 화면 어디에 있든 정면으로 보인다.
            transform.rotation = _camera.transform.rotation;
        }
    }
}
