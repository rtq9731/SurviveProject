using UnityEngine;

namespace Survive.Progression
{
    /// <summary>
    /// 청사진 하나 — "만드는 법을 안다"는 사실의 이름표.
    ///
    /// 레시피·건축물이 이것을 <b>참조</b>하고, 열렸는지 여부는
    /// <see cref="UnlockLedger"/>가 혼자 들고 있다. 상태를 여기에 두면
    /// 에셋이 저장본이 되어 버린다.
    /// </summary>
    [CreateAssetMenu(menuName = "Survive/Progression/Blueprint")]
    public class BlueprintSO : ScriptableObject
    {
        [Tooltip("원장에 들어가는 열쇠. 저장본에 박히므로 한 번 정하면 바꾸지 않는다")]
        public string id;

        public string displayName;

        // 한 줄로 적어 둔다. 문장 게이트가 한 줄짜리 [Tooltip]만 인스펙터 전용으로
        // 알아보고 지우기 때문이다 — 게이트가 여러 줄을 세게 되면 되돌려도 된다.
        [Tooltip("어떻게 하면 열리는가. 도감(CodexCatalog)에서만 읽는다 — 제작·건축 목록은 모르는 항목을 아예 띄우지 않으므로 여기를 보지 않는다")]
        [TextArea(1, 3)] public string hint;
    }
}
