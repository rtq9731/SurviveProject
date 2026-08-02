using UnityEngine;
using Survive.Items;

namespace Survive.Harvesting
{
    [CreateAssetMenu(menuName = "Survive/World/Harvest Node")]
    public class HarvestNodeSO : ScriptableObject
    {
        public string displayName = "채집물";

        [Tooltip("None이면 맨손으로 캘 수 있다")]
        public ToolType requiredTool = ToolType.None;

        [Tooltip("도구의 tier가 이 값 이상이어야 한다")]
        public int requiredTier = 0;

        [Tooltip("맨손 기준 채집 시간. 실제 = base / 도구의 harvestPower")]
        [Min(0.1f)] public float baseDuration = 2f;

        public LootTableSO drops;

        [Tooltip("0이면 재생성하지 않는다")]
        [Min(0f)] public float respawnSeconds = 0f;
    }
}
