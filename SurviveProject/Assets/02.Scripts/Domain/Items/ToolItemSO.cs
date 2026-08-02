using UnityEngine;

namespace Survive.Items
{
    [CreateAssetMenu(menuName = "Survive/Items/Tool")]
    public class ToolItemSO : ItemDataSO
    {
        [Header("채집")]
        public ToolType toolType = ToolType.Pickaxe;

        [Tooltip("채집 노드의 requiredTier 이상이어야 캘 수 있다")]
        public int tier = 1;

        [Tooltip("채집 시간 = 노드 baseDuration / harvestPower")]
        [Min(0.01f)]
        public float harvestPower = 1f;

        [Header("전투")]
        public float damage = 10f;
        public float attackRange = 2f;
        public float attackCooldown = 0.6f;

        [Header("장착")]
        [Tooltip("손 소켓(jointItemR) 아래 자식 오브젝트 이름. 예: pickaxe01")]
        public string socketChildName;
    }
}
