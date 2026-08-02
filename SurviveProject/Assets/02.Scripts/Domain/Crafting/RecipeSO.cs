using UnityEngine;
using Survive.Items;

namespace Survive.Crafting
{
    public enum StationType
    {
        None,   // 휴대 제작
        Bench
    }

    [CreateAssetMenu(menuName = "Survive/Crafting/Recipe")]
    public class RecipeSO : ScriptableObject
    {
        public string id;
        public string displayName;

        public ItemStack[] ingredients = new ItemStack[0];
        public ItemStack result;

        [Min(0f)] public float craftSeconds = 1f;
        public StationType requiredStation = StationType.None;
    }
}
