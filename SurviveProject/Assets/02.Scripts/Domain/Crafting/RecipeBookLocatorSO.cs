using UnityEngine;

namespace Survive.Crafting
{
    /// <summary>
    /// <b>런타임이 레시피 목록을 찾는 창구.</b>
    ///
    /// <b>왜 필요한가.</b> <see cref="RecipeBookSO"/> 에셋은
    /// <c>08.Data/Recipes/</c>에 있어서 <see cref="Resources.Load"/>로는 닿지 않는다.
    /// 그래서 <c>RecipeIndex</c>는 <c>Resources.FindObjectsOfTypeAll</c>로 <b>이미
    /// 메모리에 올라와 있는</b> 목록을 주웠는데, 에디터에서는 프로젝트의 에셋이
    /// 대개 올라와 있어서 <b>언제나 찾아진다</b> — 빌드에서는 씬이 그 에셋을
    /// 참조하고 있을 때만 찾아진다. 즉 <b>에디터에서는 영영 안 보이는 사고</b>가
    /// 될 수 있는 자리였다. 못 찾으면 걸어 둔 제작 하나가 경고와 함께 사라지고,
    /// 재료는 걸 때 이미 빠진 뒤다.
    ///
    /// 목록 에셋을 <c>Resources/</c>로 옮기지 않은 이유는
    /// <see cref="Survive.Creatures.CreatureSpawnBookSO"/>와 같다 — 그 경로를 물고 있는
    /// 검사와 에디터 도구가 여럿이고, 여러 갈래가 도는 동안 데이터 에셋의 자리를
    /// 옮기는 것은 이 저장소의 규율이 아니다. <b>가리키는 종이만 Resources에 둔다.</b>
    /// <c>AudioCueBookSO</c>·<c>ResearchBookSO</c>·<c>DiscoveryBookSO</c>·
    /// <c>CreatureSpawnBookSO</c>가 이미 같은 모양이다 — 새 관례가 아니라 있는 관례다.
    ///
    /// <b>덤으로 목록이 빌드에 확실히 들어간다.</b> <c>Resources/</c>에 있는 것은
    /// 참조를 따라 통째로 실리므로, 제작 화면이 씬에서 빠지는 날에도 저장이
    /// 걸어 둔 제작을 되찾을 수 있다.
    /// </summary>
    [CreateAssetMenu(menuName = "Survive/Crafting/Recipe Book Locator",
                     fileName = "RecipeBookLocator")]
    public class RecipeBookLocatorSO : ScriptableObject
    {
        /// <summary><see cref="Resources.Load"/>가 찾는 이름.</summary>
        public const string ResourceName = "RecipeBookLocator";

        /// <summary>이 종이가 놓여야 하는 자리. 게이트가 이 경로를 본다.</summary>
        public const string AssetPath = "Assets/Resources/RecipeBookLocator.asset";

        [Tooltip("실제 레시피 목록. 08.Data/Recipes/RecipeBook.asset")]
        [SerializeField] RecipeBookSO book;

        /// <summary>레시피 목록. 안 꽂혀 있으면 null이고, 부르는 쪽이 사유를 적는다.</summary>
        public RecipeBookSO Book => book;
    }
}
