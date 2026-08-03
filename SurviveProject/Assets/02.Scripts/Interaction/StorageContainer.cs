using System;
using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Core;
using Survive.Items;
using Survive.Player;

namespace Survive.Interaction
{
    /// <summary>
    /// 세워 두고 물건을 넣어 두는 보관함.
    ///
    /// <see cref="LootContainer"/>와 다르다. 저쪽은 미리 채워 둔 것을 한 번 털어가는
    /// 세계의 배치물이고, 이쪽은 플레이어가 넣고 빼는 그릇이다.
    /// 소지품 15칸이 금방 차는데 버릴 수도 없어서, 거점을 만들 이유가 필요했다.
    /// </summary>
    [DisallowMultipleComponent]
    public class StorageContainer : MonoBehaviour, IInteractable, ISaveable
    {
        [SerializeField] string displayName = "보관함";
        [SerializeField] int slotCount = 18;

        [Tooltip("열 때 재생")]
        [SerializeField] MMF_Player openFeedback;

        [Tooltip("세계에 하나뿐이 아니므로 저장 키에 붙일 꼬리표. 비우면 위치로 만든다")]
        [SerializeField] string saveIdOverride;

        Inventory _contents;

        public Inventory Contents => _contents ??= new Inventory(slotCount);
        public string DisplayName => displayName;

        public event Action<StorageContainer> Opened;

        public string InteractionPrompt => $"[E] {displayName} 열기";

        public bool CanInteract(PlayerContext player) => player?.Inventory != null;

        public void Interact(PlayerContext player)
        {
            openFeedback?.PlayFeedbacks();
            Opened?.Invoke(this);

            if (GameServices.TryGet<Survive.UI.StorageUI>(out var ui))
                ui.Open(this, player);
        }

        void OnEnable()
        {
            if (GameServices.TryGet<SaveCoordinator>(out var coord))
                coord.Service?.Register(this);
        }

        // 등록만 하고 빠지지 않으면, 부순 보관함이 저장 목록에 죽은 채로 남는다.
        // 다음 저장에서 그 참조를 건드리는 순간 MissingReferenceException이 난다 —
        // 게임은 계속 돌아가서 눈에 안 띄고, 콘솔에만 쌓인다.
        void OnDisable()
        {
            if (GameServices.TryGet<SaveCoordinator>(out var coord))
                coord.Service?.Unregister(this);
        }

        // ── 저장 ─────────────────────────────────────────────────
        //
        // 보관함은 여러 개가 생긴다. SaveKey가 겹치면 나중 것이 앞의 것을 덮어쓴다.
        // 위치를 키에 넣어 구분한다 — 옮길 수 없는 물건이라 위치가 곧 신원이다.

        [Serializable]
        public class SaveState
        {
            public List<string> itemIds = new List<string>();
            public List<int> counts = new List<int>();
        }

        public string SaveKey =>
            string.IsNullOrEmpty(saveIdOverride)
                ? $"storage_{Mathf.RoundToInt(transform.position.x)}_" +
                  $"{Mathf.RoundToInt(transform.position.y)}_" +
                  $"{Mathf.RoundToInt(transform.position.z)}"
                : "storage_" + saveIdOverride;

        public object CaptureState()
        {
            var s = new SaveState();
            foreach (var slot in Contents.Slots)
            {
                if (slot.IsEmpty) continue;
                s.itemIds.Add(slot.item.id);
                s.counts.Add(slot.count);
            }
            return s;
        }

        public void RestoreState(object state)
        {
            if (state is not SaveState s) return;

            _contents = new Inventory(slotCount);

            GameServices.TryGet<PlayerInventory>(out var inv);
            var db = inv != null ? inv.Database : null;
            if (db == null) return;

            for (int i = 0; i < s.itemIds.Count && i < s.counts.Count; i++)
            {
                var item = db.GetById(s.itemIds[i]);
                if (item != null) _contents.TryAdd(item, s.counts[i]);
            }
        }
    }
}
