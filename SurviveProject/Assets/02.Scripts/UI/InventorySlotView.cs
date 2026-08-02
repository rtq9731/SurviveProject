using UnityEngine;
using UnityEngine.UI;
using Survive.Items;

namespace Survive.UI
{
    /// <summary>인벤토리 한 칸의 표시. 아이콘과 수량만 담당한다.</summary>
    [DisallowMultipleComponent]
    public class InventorySlotView : MonoBehaviour
    {
        [SerializeField] Image icon;
        [SerializeField] Text countLabel;

        public void Render(ItemStack stack)
        {
            bool 비었나 = stack == null || stack.IsEmpty;

            if (icon != null)
            {
                icon.enabled = !비었나 && stack.item.icon != null;
                if (!비었나) icon.sprite = stack.item.icon;
            }

            if (countLabel != null)
            {
                bool 표시 = !비었나 && stack.count > 1;
                countLabel.enabled = 표시;
                if (표시) countLabel.text = stack.count.ToString();
            }
        }
    }
}
