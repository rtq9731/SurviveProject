using UnityEngine;

namespace Survive.Crafting
{
    /// <summary>
    /// 제작 UI가 표시할 레시피 목록.
    /// RecipeSO와 같은 파일에 두면 Unity가 에셋의 m_Script를 연결하지 못한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Survive/Crafting/Recipe Book")]
    public class RecipeBookSO : ScriptableObject
    {
        public RecipeSO[] recipes = new RecipeSO[0];
    }
}
