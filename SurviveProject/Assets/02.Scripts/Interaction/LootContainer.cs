using System;
using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Items;
using Survive.Localization;
using Survive.Player;

namespace Survive.Interaction
{
    /// <summary>우주선 잔해 상자처럼 한 번 열면 내용물을 전부 주는 대상.</summary>
    public class LootContainer : MonoBehaviour, IInteractable
    {
        [Serializable]
        public class Entry
        {
            public ItemDataSO item;
            [Min(1)] public int count = 1;
        }

        [SerializeField] string displayName = "잔해";
        [SerializeField] Entry[] contents = new Entry[0];

        [Tooltip("열 때 재생")]
        [SerializeField] MMF_Player openFeedback;

        bool _isOpen;

        public string InteractionPrompt => _isOpen ? "" : $"[E] {displayName} 뒤지기";

        public bool CanInteract(PlayerContext player) => !_isOpen && player?.Inventory != null;

        public void Interact(PlayerContext player)
        {
            if (_isOpen) return;
            _isOpen = true;
            openFeedback?.PlayFeedbacks();

            foreach (var c in contents)
            {
                if (c?.item == null) continue;
                int remaining = player.Inventory.Add(c.item, c.count);
                if (remaining > 0)
                    Debug.LogWarning($"[LootContainer] 인벤토리가 가득 차 {DataText.Name(c.item)} {remaining}개를 넣지 못했습니다.", this);
            }
        }
    }
}
