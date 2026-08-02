using UnityEngine;

namespace Survive.Progression
{
    /// <summary>아이템 N개 보유.</summary>
    [CreateAssetMenu(menuName = "Survive/Progression/Objective - Collect Item")]
    public class CollectItemObjective : ObjectiveSO
    {
        public string itemId;
        [Min(1)] public int amount = 1;

        public override float Evaluate(IObjectiveContext ctx)
        {
            var inv = ctx?.PlayerInventory;
            if (inv == null || string.IsNullOrEmpty(itemId)) return 0f;
            return Mathf.Clamp01(inv.CountOf(itemId) / (float)Mathf.Max(1, amount));
        }
    }
}
