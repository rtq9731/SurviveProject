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

        /// <summary>
        /// 필수 장비 자리. <b>인스턴스가 바뀌지 않는다</b> - 저장을 불러와도 같은
        /// 것을 계속 쓴다. <see cref="Inventory"/> 쪽은 복원 때 새로 만들어지는 바람에
        /// 구독이 끊기는 문제가 있는데, 여기까지 그러면 장비 화면이 조용히 죽는다.
        /// </summary>
        public EquipmentSlots Equipment { get; } = new EquipmentSlots();

        /// <summary>아이템 정의를 id로 찾을 때 쓴다 (저장 복원·검증 하네스).</summary>
        public ItemDatabaseSO Database => database;

        public const string ScrapId = "scrap";
        public int ScrapCount => Inventory?.CountOf(ScrapId) ?? 0;

        void Awake() => Inventory = new Inventory(slotCount, Equipment);

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

            /// <summary>
            /// 장비 자리에 걸린 것. <see cref="EquipmentSlots.AllKinds"/> 차례이고
            /// 빈 자리는 "". 예전 세이브에는 이 칸이 없으므로 빈 목록으로 복원되고,
            /// 그때는 소지품에 있던 랜턴을 <see cref="Inventory.RehomeEquipment"/>가
            /// 자리로 옮긴다. 그래서 판 버전을 올리지 않아도 된다.
            /// </summary>
            public List<string> equippedIds = new List<string>();
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
            for (int i = 0; i < Equipment.SlotCount; i++)
            {
                var equipped = Equipment.GetAt(i);
                s.equippedIds.Add(equipped != null ? equipped.id : "");
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

            Equipment.Clear();
            Inventory = new Inventory(slotCount, Equipment);

            int count = Mathf.Min(s.itemIds.Count, Inventory.SlotCount);
            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrEmpty(s.itemIds[i])) continue;
                if (!database.TryGetById(s.itemIds[i], out var item)) continue;
                Inventory.Slots[i].item = item;
                Inventory.Slots[i].count = s.counts[i];
            }

            if (s.equippedIds != null)
            {
                foreach (var id in s.equippedIds)
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    if (!database.TryGetById(id, out var item)) continue;
                    Equipment.TryEquipIntoEmpty(item);
                }
            }

            // 장비 칸이 없던 시절의 세이브는 랜턴이 소지품에 앉아 있다. 불러오는
            // 김에 자리로 옮긴다 - 안 그러면 예전에 시작한 판만 계속 14칸을 쓴다.
            Inventory.RehomeEquipment();
        }
    }
}
