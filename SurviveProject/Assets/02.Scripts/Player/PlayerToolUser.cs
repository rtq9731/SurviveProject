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
    ///
    /// <b>랜턴이 여기 있는 이유는 입력뿐이다.</b> 랜턴은 손에 드는 도구가 아니라
    /// 장비 칸에 걸리는 착용물이고(§11), 그래서 퀵슬롯에도 없다(⑨). 다만 F를
    /// 받는 <see cref="InputReaderSO"/> 참조를 이미 들고 있는 것이 이 컴포넌트라,
    /// 배선만 여기를 지난다. 켜고 끄는 판단은 전부
    /// <see cref="Survive.World.LanternController"/>와 <see cref="LanternRule"/>에 있다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerToolUser : MonoBehaviour
    {
        [SerializeField] InputReaderSO input;
        [SerializeField] PlayerToolHolder holder;
        [SerializeField] PlayerInventory inventory;

        [Tooltip("비워 두면 자식에서 찾는다. 프리팹을 손대지 않고도 배선이 선다")]
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
                _currentIndex = -1;
                return;
            }

            _currentIndex = (_currentIndex + 1) % _tools.Count;
            EquipTool(_tools[_currentIndex]);
        }

        void EquipTool(ToolItemSO tool)
        {
            // 랜턴 점등은 손에 무엇을 들었는지와 무관하다. 곡괭이를 들 때마다
            // 불이 꺼지면 캐는 동안 앞이 안 보인다 — 랜턴은 손에 드는 도구가
            // 아니라 몸에 다는 조명이다.
            holder?.Equip(tool);
        }

        /// <summary>
        /// F. 판단은 <see cref="LanternController.Toggle"/>이 한다 —
        /// 랜턴을 가졌는지도 거기서 본다(<see cref="LanternRule.NextSwitchState"/>).
        /// 여기서 한 번 더 물으면 같은 규칙이 두 군데에 적힌다.
        /// </summary>
        void ToggleLantern()
        {
            if (lantern == null) lantern = GetComponentInChildren<LanternController>(true);
            lantern?.Toggle();
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
