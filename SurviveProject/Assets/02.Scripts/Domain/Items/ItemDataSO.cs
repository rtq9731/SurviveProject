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

        /// <summary>
        /// 비어 있지 않으면 소지품 칸이 아니라 장비 슬롯으로 간다.
        ///
        /// id 상수로 특별한 물건을 가려내는 길(예: PlayerToolUser의 "lantern")도
        /// 있지만, 여기는 데이터로 둔다. 어느 물건이 필수 장비인가는 기획이
        /// 바뀌면 같이 바뀌는 값이고, 그때마다 코드를 고쳐야 하면 그 자체가 마찰이다.
        /// </summary>
        [Tooltip("장비 슬롯에 걸리는 물건이면 종류를 고른다. None이면 보통의 소지품")]
        public EquipmentSlotKind equipSlot = EquipmentSlotKind.None;

        [Tooltip("바닥에 떨어질 때 생성할 프리팹")]
        public GameObject worldPrefab;
    }
}
