using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survive.Items
{
    /// <summary>
    /// 가방 한 칸이 저장본에 남기는 것. 슬롯 번호를 같이 적는 이유는
    /// <see cref="DeathDrop.Extract"/>가 슬롯 단위로 떼어내기 때문이다 —
    /// 번호를 버리면 되살린 가방의 배치가 죽을 때와 달라지고,
    /// 회수 후 소지품에 돌아오는 모양도 같이 어긋난다.
    /// </summary>
    [Serializable]
    public class DeathDropSlotRecord
    {
        public int slot;
        public string itemId;
        public int count;
    }

    /// <summary>
    /// 저장본에 남는 가방 하나. 어디에 있었고 무엇이 들어 있었는가.
    /// </summary>
    [Serializable]
    public class DeathDropBagRecord
    {
        public Vector3 position;
        public int slotCount;
        public List<DeathDropSlotRecord> slots = new List<DeathDropSlotRecord>();
    }

    /// <summary>
    /// 세계에 남아 있는 가방 전부. 이것이 저장본의 한 칸에 들어간다.
    /// </summary>
    [Serializable]
    public class DeathDropBagsState
    {
        public List<DeathDropBagRecord> bags = new List<DeathDropBagRecord>();
    }

    /// <summary>
    /// 가방 ↔ 저장본. Unity 씬도 프리팹도 모르는 순수 변환이다.
    ///
    /// <b>왜 이것이 따로 필요한가.</b> 보관함은 씬에 놓여 있으니 불러오기가
    /// 자기 상태를 되받으면 끝난다. 가방은 플레이 중에 생겨나므로
    /// 되받을 주체가 아예 없다 — 위치까지 저장본이 들고 있다가
    /// 불러올 때 그 자리에 다시 세워야 한다. 그래서 보관함의
    /// <c>SaveState</c>와 달리 좌표와 칸 수가 스키마에 들어간다.
    ///
    /// 변환을 여기 둔 이유는 <see cref="Survive.Core.SaveSerializer"/>와 같다 —
    /// 저장본이 왕복하는지는 씬을 띄우지 않고 확인할 수 있어야 한다.
    /// </summary>
    public static class DeathDropBagSave
    {
        /// <summary>
        /// 가방 하나를 적는다. 빈 가방은 적지 않는다(null) —
        /// 회수가 끝난 가방은 세계에서도 사라지므로 되살릴 것이 없다.
        /// </summary>
        public static DeathDropBagRecord Capture(Vector3 position, Inventory contents)
        {
            if (contents == null) return null;

            var record = new DeathDropBagRecord
            {
                position = position,
                slotCount = contents.SlotCount
            };

            var slots = contents.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || slot.IsEmpty) continue;

                record.slots.Add(new DeathDropSlotRecord
                {
                    slot = i,
                    itemId = slot.item.id,
                    count = slot.count
                });
            }

            return record.slots.Count > 0 ? record : null;
        }

        /// <summary>
        /// 적어 둔 가방을 되살린다. 정의를 찾지 못한 아이템은 조용히 버린다 —
        /// 데이터베이스에서 빠진 아이템 하나 때문에 나머지 회수분까지
        /// 통째로 잃는 것이 더 나쁘다.
        /// </summary>
        /// <returns>가방 속. 되살릴 것이 하나도 없으면 null.</returns>
        public static Inventory Restore(DeathDropBagRecord record, ItemDatabaseSO database)
        {
            if (record == null || database == null) return null;

            var contents = new Inventory(Mathf.Max(1, record.slotCount));
            if (record.slots == null) return null;

            bool any = false;
            var slots = contents.Slots;

            for (int i = 0; i < record.slots.Count; i++)
            {
                var entry = record.slots[i];
                if (entry == null || entry.count <= 0) continue;
                if (!database.TryGetById(entry.itemId, out var item) || item == null) continue;

                // 슬롯에 직접 쓴다. TryAdd로 넣으면 기존 스택부터 채우느라
                // 적어 둔 번호와 다른 자리에 들어간다.
                if (entry.slot < 0 || entry.slot >= slots.Count) continue;

                slots[entry.slot].item = item;
                slots[entry.slot].count = entry.count;
                any = true;
            }

            return any ? contents : null;
        }
    }
}
