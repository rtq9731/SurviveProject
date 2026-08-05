using UnityEngine;

namespace Survive.Progression
{
    /// <summary>
    /// 현장 발견 목록. 레시피에 <see cref="Survive.Crafting.RecipeBookSO"/>가 있는 것과
    /// 같은 자리다 — 흩어진 에셋을 런타임에 훑지 않고 한 곳에서 받는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Survive/Progression/Discovery Book")]
    public class DiscoveryBookSO : ScriptableObject
    {
        public DiscoverySO[] discoveries = new DiscoverySO[0];

        /// <summary>이 아이템에 걸린 발견. 없으면 null.</summary>
        public DiscoverySO Find(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || discoveries == null) return null;

            foreach (var d in discoveries)
            {
                if (d == null || d.item == null) continue;
                if (d.item.id == itemId) return d;
            }
            return null;
        }
    }
}
