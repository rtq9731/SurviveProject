using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;
using Survive.Items;

namespace Survive.UI
{
    /// <summary>인벤토리 한 칸의 표시. 아이콘과 수량만 담당한다.</summary>
    [DisallowMultipleComponent]
    public class InventorySlotView : MonoBehaviour,
                                     IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] Image icon;
        [SerializeField] TMP_Text countLabel;

        ItemStack _shown;

        /// <summary>이 칸에 올라와 있는 것. 비었으면 null이거나 IsEmpty다. 검증 하네스가 본다.</summary>
        public ItemStack Shown => _shown;

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

        /// <summary>
        /// 커서를 올리면 그 물건이 무엇인지 읽어 준다.
        ///
        /// 아이템 에셋에는 진작부터 설명문이 적혀 있었는데 화면에 닿는 길이 없었다.
        /// 칸 자체가 이미 포인터를 받고 있으므로(클릭으로 보관함에 넣는다) 여기서
        /// 두 줄만 더 받으면 된다 — 따로 껍데기를 붙일 이유가 없다.
        /// </summary>
        public void OnPointerEnter(PointerEventData e)
        {
            if (_shown == null || _shown.IsEmpty) { ItemTooltipView.Hide(this); return; }
            ItemTooltipView.Show(_shown.item, e != null ? e.position : Vector2.zero, this);
        }

        public void OnPointerExit(PointerEventData e) => ItemTooltipView.Hide(this);

        void OnDisable() => ItemTooltipView.Hide(this);

        public void Render(ItemStack stack)
        {
            _shown = stack;
            bool empty = stack == null || stack.IsEmpty;

            // 커서를 올려 둔 사이에 그 물건이 빠져나갈 수 있다(제작에 재료로 들어가거나
            // 상자로 넘어가거나). 설명이 그대로 남아 있으면 없는 물건을 읽게 되므로 걷는다.
            // 이 칸이 띄운 것이 아니면 아무 일도 일어나지 않는다.
            var tooltip = ItemTooltipView.Instance;
            if (tooltip != null && tooltip.Item != (empty ? null : stack.item))
                ItemTooltipView.Hide(this);

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
