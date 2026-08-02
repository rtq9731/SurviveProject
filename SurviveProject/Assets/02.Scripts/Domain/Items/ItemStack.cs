using System;

namespace Survive.Items
{
    [Serializable]
    public class ItemStack
    {
        public ItemDataSO item;
        public int count;

        public ItemStack() { }

        public ItemStack(ItemDataSO item, int count)
        {
            this.item = item;
            this.count = count;
        }

        public bool IsEmpty => item == null || count <= 0;

        /// <summary>이 스택에 더 들어갈 수 있는 개수.</summary>
        public int RemainingSpace => item == null ? 0 : item.maxStack - count;

        public void Clear()
        {
            item = null;
            count = 0;
        }
    }
}
