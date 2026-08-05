using System;
using UnityEngine;
using UnityEngine.EventSystems;
using Survive.Items;

namespace Survive.UI
{
    /// <summary>
    /// "이 칸 위에 커서가 오면 저 물건을 설명하라"만 하는 얇은 껍데기.
    ///
    /// 런타임에 줄을 만드는 화면(제작 목록·보관함·도감)이 각자 PointerEnter를
    /// 구현하면 같은 여섯 줄이 세 번 복사된다. 대신 줄 오브젝트에 이것을 하나 붙이고
    /// 무엇을 설명할지만 알려 준다.
    ///
    /// <b>물건을 붙들지 않고 물어본다.</b> 목록의 줄은 다시 그려도 오브젝트는 그대로라,
    /// 무엇이 올라와 있는지가 프레임마다 바뀔 수 있다(보관함 칸이 대표적이다).
    /// 값을 박아 두면 옛 물건을 설명하게 되므로 커서가 올라온 <b>그때</b> 묻는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ItemTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        Func<ItemDataSO> _item;
        Func<string> _detail;

        /// <summary>줄 오브젝트에 붙인다. 이미 붙어 있으면 그것을 준다.</summary>
        public static ItemTooltipTrigger Attach(GameObject host)
        {
            if (host == null) return null;
            var found = host.GetComponent<ItemTooltipTrigger>();
            return found != null ? found : host.AddComponent<ItemTooltipTrigger>();
        }

        /// <summary>바뀌지 않는 물건.</summary>
        public void Bind(ItemDataSO item) => Bind(() => item, null);

        /// <summary>
        /// 커서가 올라올 때마다 다시 묻는다.
        /// </summary>
        /// <param name="item">지금 이 자리에 올라와 있는 물건. null을 주면 쪽지가 뜨지 않는다.</param>
        /// <param name="detail">설명문 아래에 덧붙일 한 덩어리(예: 제작 재료). 없으면 null.</param>
        public void Bind(Func<ItemDataSO> item, Func<string> detail = null)
        {
            _item = item;
            _detail = detail;
        }

        /// <summary>지금 이 자리가 설명할 물건. 없으면 null.</summary>
        public ItemDataSO Resolve() => _item != null ? _item() : null;

        public void OnPointerEnter(PointerEventData eventData)
        {
            var item = Resolve();
            if (item == null) { ItemTooltipView.Hide(this); return; }

            ItemTooltipView.Show(item, eventData != null ? eventData.position : Vector2.zero,
                                 this, _detail != null ? _detail() : null);
        }

        public void OnPointerExit(PointerEventData eventData) => ItemTooltipView.Hide(this);

        void OnDisable() => ItemTooltipView.Hide(this);
    }
}
