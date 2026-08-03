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

        readonly List<ToolItemSO> _tools = new List<ToolItemSO>();
        int _currentIndex = -1;

        void Awake()
        {
            if (holder == null) holder = GetComponent<PlayerToolHolder>();
            if (inventory == null) inventory = GetComponent<PlayerInventory>();
            if (lantern == null) lantern = GetComponentInChildren<LanternController>(true);
        }

        void OnEnable()
        {
            if (input == null) return;
            input.NextToolEvent += NextTool;
            input.ToggleLanternEvent += ToggleLantern;
        }

        void OnDisable()
        {
            if (input == null) return;
            input.NextToolEvent -= NextTool;
            input.ToggleLanternEvent -= ToggleLantern;
        }

        void RefreshToolList()
        {
            _tools.Clear();
            var inv = inventory?.Inventory;
            if (inv == null) return;

            foreach (var s in inv.Slots)
            {
                if (s.IsEmpty) continue;
                if (s.item is ToolItemSO tool && !_tools.Contains(tool)) _tools.Add(tool);
            }
        }

        void NextTool()
        {
            RefreshToolList();

            if (_tools.Count == 0)
            {
                holder?.Unequip();
                lantern?.SetOn(false);
                _currentIndex = -1;
                return;
            }

            _currentIndex = (_currentIndex + 1) % _tools.Count;
            EquipTool(_tools[_currentIndex]);
        }

        void EquipTool(ToolItemSO tool)
        {
            holder?.Equip(tool);
            // 랜턴 점등은 손에 무엇을 들었는지와 무관하다. 아래 ToggleLantern 참고.
        }

        void ToggleLantern()
        {
            if (lantern == null) return;

            // 랜턴을 가지고 있지 않으면 아무것도 하지 않는다.
            // 제작 전에는 어둠을 그대로 견뎌야 한다 — 그것이 챕터 1의 압박이다.
            var inv = inventory?.Inventory;
            if (inv == null || !inv.Has("lantern", 1)) return;

            // 손에 든 것과 무관하게 켜고 끈다.
            // 곡괭이를 들 때마다 불이 꺼지면 캐는 동안 앞이 안 보인다 —
            // 랜턴은 손에 드는 도구가 아니라 몸에 다는 조명으로 다룬다.
            lantern.Toggle();
        }

        /// <summary>인벤토리에서 지정 도구를 찾아 장착한다. 제작 직후 편의용.</summary>
        public bool EquipFirst(string itemId)
        {
            RefreshToolList();
            for (int i = 0; i < _tools.Count; i++)
            {
                if (_tools[i].id != itemId) continue;
                _currentIndex = i;
                EquipTool(_tools[i]);
                return true;
            }
            return false;
        }
    }
}
