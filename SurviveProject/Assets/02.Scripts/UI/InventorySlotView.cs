using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;
using Survive.Items;

namespace Survive.UI
{
    /// <summary>인벤토리 한 칸의 표시. 아이콘과 수량만 담당한다.</summary>
    [DisallowMultipleComponent]
    public class InventorySlotView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] Image icon;
        [SerializeField] TMP_Text countLabel;

        ItemStack _shown;

        /// <summary>
        /// 보관함이 열려 있으면 클릭으로 넣는다.
        /// 드래그 앤 드롭은 만들 것이 많고, 이 게임에서 옮기는 것은 대개
        /// 한 종류를 통째로 넘기는 일이라 한 번 클릭이면 충분하다.
        /// </summary>
        public void OnPointerClick(PointerEventData e)
        {
            if (_shown == null || _shown.IsEmpty) return;
            if (!Survive.Core.GameServices.TryGet<StorageUI>(out var storage)) return;
            if (!storage.IsOpen) return;

            storage.PutIn(_shown.item, _shown.count);
        }

        public void Render(ItemStack stack)
        {
            _shown = stack;
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
