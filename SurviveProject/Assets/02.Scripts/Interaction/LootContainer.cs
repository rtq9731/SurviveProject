using System;
using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Items;
using Survive.Player;

namespace Survive.Interaction
{
    /// <summary>우주선 잔해 상자처럼 한 번 열면 내용물을 전부 주는 대상.</summary>
    public class LootContainer : MonoBehaviour, IInteractable
    {
        [Serializable]
        public class 내용물
        {
            public ItemDataSO item;
            [Min(1)] public int count = 1;
        }

        [SerializeField] string displayName = "잔해";
        [SerializeField] 내용물[] contents = new 내용물[0];

        [Tooltip("열 때 재생")]
        [SerializeField] MMF_Player openFeedback;

        bool _열림;

        public string InteractionPrompt => _열림 ? "" : $"[E] {displayName} 뒤지기";

        public bool CanInteract(PlayerContext player) => !_열림 && player?.Inventory != null;

        public void Interact(PlayerContext player)
        {
            if (_열림) return;
            _열림 = true;
            openFeedback?.PlayFeedbacks();

            foreach (var c in contents)
            {
                if (c?.item == null) continue;
                int 남은수 = player.Inventory.Add(c.item, c.count);
                if (남은수 > 0)
                    Debug.LogWarning($"[LootContainer] 인벤토리가 가득 차 {c.item.displayName} {남은수}개를 넣지 못했습니다.", this);
            }
        }
    }
}
