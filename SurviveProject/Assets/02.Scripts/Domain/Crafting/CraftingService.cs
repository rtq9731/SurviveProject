using Survive.Items;

namespace Survive.Crafting
{
    /// <summary>
    /// 제작 판정과 실행. 순수 정적이라 Unity 실행 없이 테스트한다.
    /// </summary>
    public static class CraftingService
    {
        public static bool CanCraft(RecipeSO recipe, Inventory inventory, StationType available)
        {
            if (recipe == null || inventory == null) return false;
            if (recipe.result == null || recipe.result.item == null) return false;
            if (!스테이션충족(recipe.requiredStation, available)) return false;

            if (recipe.ingredients != null)
            {
                foreach (var need in recipe.ingredients)
                {
                    if (need?.item == null) continue;
                    if (!inventory.Has(need.item.id, need.count)) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 재료를 먼저 빼고 결과를 넣는다. 결과가 다 안 들어가면
        /// 재료를 되돌리고 실패로 처리한다 — 재료만 사라지는 일이 없어야 한다.
        /// </summary>
        public static bool Craft(RecipeSO recipe, Inventory inventory, StationType available)
        {
            if (!CanCraft(recipe, inventory, available)) return false;

            if (recipe.ingredients != null)
            {
                foreach (var need in recipe.ingredients)
                {
                    if (need?.item == null) continue;
                    inventory.TryRemove(need.item.id, need.count);
                }
            }

            int 남은수 = inventory.TryAdd(recipe.result.item, recipe.result.count);
            if (남은수 > 0)
            {
                // 되돌린다
                inventory.TryRemove(recipe.result.item.id, recipe.result.count - 남은수);
                if (recipe.ingredients != null)
                {
                    foreach (var need in recipe.ingredients)
                    {
                        if (need?.item == null) continue;
                        inventory.TryAdd(need.item, need.count);
                    }
                }
                return false;
            }
            return true;
        }

        static bool 스테이션충족(StationType 필요, StationType 가능)
        {
            if (필요 == StationType.None) return true;   // 휴대 제작은 어디서든
            return 필요 == 가능;
        }
    }
}
