using System.Collections.Generic;
using UnityEngine;
using Survive.Input;
using Survive.Items;
using Survive.World;

namespace Survive.Player
{
    /// <summary>
    /// 인벤토리에 있는 도구를 순환 장착하고, 랜턴 불을 켜고 끈다.
    /// Q = 다음 도구, F = 랜턴 토글.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerToolUser : MonoBehaviour
    {
        [SerializeField] InputReaderSO input;
        [SerializeField] PlayerToolHolder holder;
        [SerializeField] PlayerInventory inventory;
        [SerializeField] LanternController lantern;

        readonly List<ToolItemSO> _도구목록 = new List<ToolItemSO>();
        int _현재 = -1;

        void Awake()
        {
            if (holder == null) holder = GetComponent<PlayerToolHolder>();
            if (inventory == null) inventory = GetComponent<PlayerInventory>();
            if (lantern == null) lantern = GetComponentInChildren<LanternController>(true);
        }

        void OnEnable()
        {
            if (input == null) return;
            input.NextToolEvent += 다음도구;
            input.ToggleLanternEvent += 랜턴토글;
        }

        void OnDisable()
        {
            if (input == null) return;
            input.NextToolEvent -= 다음도구;
            input.ToggleLanternEvent -= 랜턴토글;
        }

        void 도구목록갱신()
        {
            _도구목록.Clear();
            var inv = inventory?.Inventory;
            if (inv == null) return;

            foreach (var s in inv.Slots)
            {
                if (s.IsEmpty) continue;
                if (s.item is ToolItemSO tool && !_도구목록.Contains(tool)) _도구목록.Add(tool);
            }
        }

        void 다음도구()
        {
            도구목록갱신();

            if (_도구목록.Count == 0)
            {
                holder?.Unequip();
                lantern?.SetOn(false);
                _현재 = -1;
                return;
            }

            _현재 = (_현재 + 1) % _도구목록.Count;
            장착(_도구목록[_현재]);
        }

        void 장착(ToolItemSO tool)
        {
            holder?.Equip(tool);
            // 랜턴을 손에 들면 켜고, 다른 도구로 바꾸면 끈다.
            lantern?.SetOn(tool != null && tool.id == "lantern");
        }

        void 랜턴토글()
        {
            if (lantern == null) return;

            // 랜턴을 가지고 있지 않으면 아무것도 하지 않는다.
            // 제작 전에는 어둠을 그대로 견뎌야 한다 — 그것이 챕터 1의 압박이다.
            var inv = inventory?.Inventory;
            if (inv == null || !inv.Has("lantern", 1)) return;

            // 손에 들고 있지 않으면 먼저 꺼낸다. 꺼내면서 켜지므로 여기서 끝난다.
            if (holder != null && (holder.EquippedTool == null || holder.EquippedTool.id != "lantern"))
            {
                if (EquipFirst("lantern")) return;
            }
            lantern.Toggle();
        }

        /// <summary>인벤토리에서 지정 도구를 찾아 장착한다. 제작 직후 편의용.</summary>
        public bool EquipFirst(string itemId)
        {
            도구목록갱신();
            for (int i = 0; i < _도구목록.Count; i++)
            {
                if (_도구목록[i].id != itemId) continue;
                _현재 = i;
                장착(_도구목록[i]);
                return true;
            }
            return false;
        }
    }
}
