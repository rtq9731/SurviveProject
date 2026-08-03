using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Survive.Items;

namespace Survive.UI
{
    /// <summary>인벤토리 한 칸의 표시. 아이콘과 수량만 담당한다.</summary>
    [DisallowMultipleComponent]
    public class InventorySlotView : MonoBehaviour
    {
        [SerializeField] Image icon;
        [SerializeField] TMP_Text countLabel;

        public void Render(ItemStack stack)
        {
            bool empty = stack == null || stack.IsEmpty;

            if (icon != null)
            {
                icon.enabled = !empty && stack.item.icon != null;
                if (!empty) icon.sprite = stack.item.icon;
            }

            if (countLabel != null)
            {
                bool show = !empty && stack.count > 1;
                countLabel.enabled = show;
                if (show) countLabel.text = stack.count.ToString();
            }
        }
    }
}
