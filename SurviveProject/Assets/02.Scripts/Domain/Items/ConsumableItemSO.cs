using UnityEngine;

namespace Survive.Items
{
    [CreateAssetMenu(menuName = "Survive/Items/Consumable")]
    public class ConsumableItemSO : ItemDataSO
    {
        [Tooltip("health 또는 oxygen")]
        public string targetVitalId = "health";

        [Tooltip("사용 즉시 회복량")]
        public float instantAmount;

        [Tooltip("0이면 즉시형, 0보다 크면 지속형")]
        public float durationSeconds;

        [Tooltip("지속형일 때 초당 변화량")]
        public float ratePerSecond;
    }
}
