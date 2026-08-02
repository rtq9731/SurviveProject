using System;
using System.Collections.Generic;
using UnityEngine;
using Survive.Core;

namespace Survive.Items
{
    [DisallowMultipleComponent]
    public class PlayerInventory : MonoBehaviour, ISaveable
    {
        [Tooltip("CvsUI/PanelInven의 슬롯 개수와 맞춘다")]
        [SerializeField] int slotCount = 15;

        [SerializeField] ItemDatabaseSO database;

        public Inventory Inventory { get; private set; }

        /// <summary>아이템 정의를 id로 찾을 때 쓴다 (저장 복원·검증 하네스).</summary>
        public ItemDatabaseSO Database => database;

        public const string ScrapId = "scrap";
        public int ScrapCount => Inventory?.CountOf(ScrapId) ?? 0;

        void Awake() => Inventory = new Inventory(slotCount);

        void OnEnable() => GameServices.Register(this);
        void OnDisable() => GameServices.Unregister<PlayerInventory>();

        public int Add(ItemDataSO item, int count) => Inventory.TryAdd(item, count);
        public bool Remove(string itemId, int count) => Inventory.TryRemove(itemId, count);

        // ── 저장 ─────────────────────────────────────────────────

        [Serializable]
        public class SaveState
        {
            public List<string> itemIds = new List<string>();
            public List<int> counts = new List<int>();
        }

        public string SaveKey => "player_inventory";

        public object CaptureState()
        {
            var s = new SaveState();
            foreach (var slot in Inventory.Slots)
            {
                s.itemIds.Add(slot.IsEmpty ? "" : slot.item.id);
                s.counts.Add(slot.IsEmpty ? 0 : slot.count);
            }
            return s;
        }

        public void RestoreState(object state)
        {
            if (!(state is SaveState s)) return;
            if (database == null)
            {
                Debug.LogError("[PlayerInventory] database가 비어 있어 복원할 수 없습니다.", this);
                return;
            }

            Inventory = new Inventory(slotCount);
            int count = Mathf.Min(s.itemIds.Count, Inventory.SlotCount);
            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrEmpty(s.itemIds[i])) continue;
                if (!database.TryGetById(s.itemIds[i], out var item)) continue;
                Inventory.Slots[i].item = item;
                Inventory.Slots[i].count = s.counts[i];
            }
        }
    }
}
