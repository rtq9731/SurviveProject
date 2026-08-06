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

        [Tooltip("비우면 번역 표의 World/loot_container_default를 쓴다")]
        [SerializeField] string displayName = "";
        [SerializeField] Entry[] contents = new Entry[0];

        [Tooltip("열 때 재생")]
        [SerializeField] MMF_Player openFeedback;

        bool _isOpen;

        /// <summary>
        /// 인스펙터에 적힌 이름이 이긴다. 그것은 배치한 사람이 이 하나에만 붙인
        /// 이름이라 표가 대신 정할 수 없다. 비어 있을 때만 표의 기본 이름을 쓴다.
        /// </summary>
        string Name => string.IsNullOrEmpty(displayName)
            ? Loc.T("World", "loot_container_default")
            : displayName;

        public string InteractionPrompt => _isOpen ? "" : Loc.F("Prompt", "loot_search", Name);

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
