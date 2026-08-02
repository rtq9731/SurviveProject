using UnityEngine;

namespace Survive.Items
{
    [CreateAssetMenu(menuName = "Survive/Items/Item")]
    public class ItemDataSO : ScriptableObject
    {
        [Tooltip("소문자 스네이크 케이스. 예: scrap, oxygen_filter")]
        public string id;

        public string displayName;

        [TextArea]
        public string description;

        public Sprite icon;

        [Min(1)]
        public int maxStack = 1;

        public ItemCategory category = ItemCategory.Resource;

        [Tooltip("바닥에 떨어질 때 생성할 프리팹")]
        public GameObject worldPrefab;
    }
}
