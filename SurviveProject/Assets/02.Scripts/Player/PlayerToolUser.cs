using System.Collections.Generic;
using UnityEngine;
using Survive.Input;
using Survive.Items;

namespace Survive.Player
{
    /// <summary>
    /// 인벤토리에 있는 도구를 순환 장착한다. Q = 다음 도구.
    ///
    /// <b>랜턴은 여기 없다.</b> 예전에는 F로 켜고 껐지만 랜턴은 상시 점등이
    /// 전제가 되었고(스펙 §12), 끄는 선택지를 두지 않는다. 켜짐 여부는 조작이
    /// 아니라 상태이므로 <see cref="Survive.World.LanternController"/>가 스스로
    /// 인벤토리를 보고 정한다 — 손에 든 것과는 애초에 무관했다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerToolUser : MonoBehaviour
    {
        [SerializeField] InputReaderSO input;
        [SerializeField] PlayerToolHolder holder;
        [SerializeField] PlayerInventory inventory;

        readonly List<ToolItemSO> _tools = new List<ToolItemSO>();
        int _currentIndex = -1;

        void Awake()
        {
            if (holder == null) holder = GetComponent<PlayerToolHolder>();
            if (inventory == null) inventory = GetComponent<PlayerInventory>();
        }

        void OnEnable()
        {
            if (input == null) return;
            input.NextToolEvent += NextTool;
        }

        void OnDisable()
        {
            if (input == null) return;
            input.NextToolEvent -= NextTool;
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
                _currentIndex = -1;
                return;
            }

            _currentIndex = (_currentIndex + 1) % _tools.Count;
            EquipTool(_tools[_currentIndex]);
        }

        void EquipTool(ToolItemSO tool)
        {
            holder?.Equip(tool);
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
