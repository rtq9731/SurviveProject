using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Audio;
using Survive.Domain.Audio;
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

        [Tooltip("주울 때 소리. 비우면 소리 표의 itemPickup")]
        [SerializeField] AudioCueSO pickupCue;

        public string InteractionPrompt =>
            item == null ? "" : $"[E] {item.displayName} 줍기" + (count > 1 ? $" ×{count}" : "");

        public bool CanInteract(PlayerContext player) => item != null && player?.Inventory != null;

        public void Interact(PlayerContext player)
        {
            int remaining = player.Inventory.Add(item, count);
            if (remaining <= 0)
            {
                pickupFeedback?.PlayFeedbacks();
                PlayPickupSound();
                Destroy(gameObject);
                return;
            }

            // 일부만 들어갔으면 남은 만큼만 남긴다
            if (remaining != count)
            {
                pickupFeedback?.PlayFeedbacks();
                PlayPickupSound();
                count = remaining;
            }
        }

        /// <summary>
        /// 이 오브젝트는 다음 줄에서 사라질 수 있다. 자기 몸에 붙은 AudioSource로 냈다면
        /// 소리가 시작하자마자 잘린다 — 그래서 창구(<see cref="AudioService"/>)에 맡긴다.
        /// </summary>
        void PlayPickupSound()
        {
            var book = AudioService.Book;
            AudioService.Play(AudioCueBookSO.Or(pickupCue, book != null ? book.itemPickup : null),
                              transform.position);
        }

        /// <summary>런타임 생성용 (전리품 드롭 등).</summary>
        public void Setup(ItemDataSO newItem, int newCount)
        {
            item = newItem;
            count = Mathf.Max(1, newCount);
        }
    }
}
