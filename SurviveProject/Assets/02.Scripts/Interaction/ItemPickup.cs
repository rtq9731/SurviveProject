using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Items;
using Survive.Player;

namespace Survive.Interaction
{
    /// <summary>바닥에 떨어져 있는 아이템.</summary>
    public class ItemPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] ItemDataSO item;
        [Min(1)] [SerializeField] int count = 1;

        [Tooltip("획득 성공 시 재생. 획득음·파티클")]
        [SerializeField] MMF_Player pickupFeedback;

        public string InteractionPrompt =>
            item == null ? "" : $"[E] {item.displayName} 줍기" + (count > 1 ? $" ×{count}" : "");

        public bool CanInteract(PlayerContext player) => item != null && player?.Inventory != null;

        public void Interact(PlayerContext player)
        {
            int 남은수 = player.Inventory.Add(item, count);
            if (남은수 <= 0)
            {
                pickupFeedback?.PlayFeedbacks();
                Destroy(gameObject);
                return;
            }

            // 일부만 들어갔으면 남은 만큼만 남긴다
            if (남은수 != count)
            {
                pickupFeedback?.PlayFeedbacks();
                count = 남은수;
            }
        }

        /// <summary>런타임 생성용 (전리품 드롭 등).</summary>
        public void Setup(ItemDataSO newItem, int newCount)
        {
            item = newItem;
            count = Mathf.Max(1, newCount);
        }
    }
}
