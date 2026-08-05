using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;
using Survive.Core;
using Survive.Items;
using Survive.Player;
using Survive.World;

namespace Survive.UI
{
    /// <summary>
    /// 숫자 키로 장비를 바로 꺼내는 퀵슬롯.
    ///
    /// Q로 순환하는 방식만 있으면 도구가 셋만 넘어가도 원하는 것을 꺼내기까지
    /// 몇 번을 눌러야 하는지 세게 된다. 슬롯이 눈에 보이고 번호가 붙어 있으면
    /// 무엇을 가졌는지와 어떻게 꺼내는지가 한 번에 읽힌다.
    ///
    /// 슬롯은 인벤토리의 도구로 자동으로 채운다. 따로 등록하는 절차를 두면
    /// 그 절차 자체를 또 설명해야 한다 — 챕터 1에 도구가 둘뿐이라 그럴 값이 없다.
    ///
    /// 도구 뒤에 <b>바로 쓰는 물건</b>이 붙는다. 지금은 발광 버섯 갓 하나뿐이고,
    /// 누르면 장착이 아니라 발밑에 표식으로 놓인다(<see cref="GlowMarker"/>).
    /// 도구를 앞에 두는 이유: 곡괭이 1·랜턴 2가 이미 손에 익었고,
    /// 물건이 늘 때마다 그 번호가 밀리면 안 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class QuickSlotBar : MonoBehaviour
    {
        [SerializeField] RectTransform slotParent;
        [SerializeField] PlayerInventory inventory;
        [SerializeField] PlayerToolUser toolUser;
        [SerializeField] PlayerToolHolder holder;

        [Header("모양")]
        [SerializeField] int slotCount = 5;
        [SerializeField] Vector2 slotSize = new Vector2(58f, 58f);
        [SerializeField] float spacing = 8f;

        [SerializeField] Color emptyColor = new Color(0.10f, 0.11f, 0.14f, 0.72f);
        [SerializeField] Color filledColor = new Color(0.16f, 0.18f, 0.24f, 0.88f);
        [SerializeField] Color selectedColor = new Color(0.95f, 0.82f, 0.42f, 0.95f);

        readonly List<Slot> _slots = new List<Slot>();

        /// <summary>슬롯에 올라간 것들. 도구가 앞, 바로 쓰는 물건이 뒤다.</summary>
        readonly List<ItemDataSO> _entries = new List<ItemDataSO>();

        /// <summary>
        /// 도구가 아니면서 퀵슬롯에 오르는 물건의 id. 순서가 곧 슬롯 순서다.
        ///
        /// 아이템 데이터에 "놓을 수 있음" 같은 칸을 새로 파지 않는다 —
        /// 그 칸을 아무도 안 쓰는 아이템 열두 개에까지 들려 보내는 값이 없고,
        /// 이 리포지토리는 이미 id 상수로 특별한 물건을 가려낸다
        /// (PlayerInventory.ScrapId, PlayerToolUser의 "lantern").
        /// </summary>
        static readonly string[] PlaceableIds = { GlowMarker.ItemId };

        /// <summary>지금 슬롯 순서. 무엇을 몇 번으로 꺼내는지 검증에서 집는다.</summary>
        public IReadOnlyList<ItemDataSO> Entries => _entries;

        class Slot
        {
            public RectTransform rect;
            public Image frame;
            public Image icon;
            public TMP_Text number;
        }

        void Awake()
        {
            if (inventory == null) inventory = Object.FindAnyObjectByType<PlayerInventory>(FindObjectsInactive.Exclude);
            if (toolUser == null) toolUser = Object.FindAnyObjectByType<PlayerToolUser>(FindObjectsInactive.Exclude);
            if (holder == null) holder = Object.FindAnyObjectByType<PlayerToolHolder>(FindObjectsInactive.Exclude);
            if (slotParent == null) slotParent = transform as RectTransform;
        }

        IEnumerator Start()
        {
            // 플레이어가 준비될 때까지 기다린다
            for (int i = 0; i < 180; i++)
            {
                if (inventory == null) inventory = Object.FindAnyObjectByType<PlayerInventory>(FindObjectsInactive.Exclude);
                if (toolUser == null) toolUser = Object.FindAnyObjectByType<PlayerToolUser>(FindObjectsInactive.Exclude);
                if (holder == null) holder = Object.FindAnyObjectByType<PlayerToolHolder>(FindObjectsInactive.Exclude);
                if (inventory?.Inventory != null && holder != null) break;
                yield return null;
            }

            Build();

            if (inventory?.Inventory != null) inventory.Inventory.Changed += Refresh;
            if (holder != null) holder.ToolChanged += OnToolChanged;
            Refresh();
        }

        void OnDestroy()
        {
            if (inventory?.Inventory != null) inventory.Inventory.Changed -= Refresh;
            if (holder != null) holder.ToolChanged -= OnToolChanged;
            foreach (var s in _slots) s.rect?.DOKill();
        }

        void OnToolChanged(ToolItemSO _) => Refresh();

        void Update()
        {
            // 숫자 키. InputActions에 슬롯마다 액션을 만드는 것보다
            // 여기서 한 번에 읽는 편이 슬롯 수를 바꿀 때 손이 덜 간다.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;

            for (int i = 0; i < _slots.Count && i < 9; i++)
            {
                var key = kb[(UnityEngine.InputSystem.Key)((int)UnityEngine.InputSystem.Key.Digit1 + i)];
                if (key != null && key.wasPressedThisFrame) Select(i);
            }
        }

        /// <summary>슬롯 하나를 누른다. 도구면 장착, 바로 쓰는 물건이면 쓴다.</summary>
        public void Select(int index)
        {
            if (index < 0 || index >= _entries.Count) return;
            var entry = _entries[index];
            if (entry == null) return;

            if (entry is ToolItemSO tool) toolUser?.EquipFirst(tool.id);
            else if (!Use(entry)) return;

            // 눌린 슬롯이 튀어오른다. 반응이 없으면 눌린 줄 모른다.
            var rect = _slots[index].rect;
            rect.DOKill();
            rect.localScale = Vector3.one;
            rect.DOPunchScale(Vector3.one * 0.18f, 0.22f, 8, 0.6f);
        }

        /// <summary>도구가 아닌 물건을 쓴다. 실제로 쓰였으면 true.</summary>
        bool Use(ItemDataSO item)
        {
            if (item.id != GlowMarker.ItemId) return false;

            var player = inventory != null ? inventory.GetComponentInParent<PlayerContext>() : null;
            if (player == null) return false;

            return GlowMarker.PlaceFrom(player) != null;
        }

        [Tooltip("복제할 기존 슬롯. 비우면 인벤토리 패널의 슬롯을 찾아 쓴다")]
        [SerializeField] InventorySlotView styleSource;

        /// <summary>
        /// 인벤토리 슬롯과 같은 모양을 쓴다.
        ///
        /// 퀵슬롯만 혼자 다르게 생기면 같은 게임의 UI로 보이지 않는다.
        /// 이미 있는 슬롯의 스프라이트와 색을 그대로 가져온다.
        /// </summary>
        Sprite BorrowSlotSprite()
        {
            var src = styleSource != null
                ? styleSource
                : Object.FindAnyObjectByType<InventorySlotView>(FindObjectsInactive.Include);

            var img = src != null ? src.GetComponent<Image>() : null;
            if (img != null && img.sprite != null) return img.sprite;

            return UISkin.Panel;
        }

        void Build()
        {
            foreach (var s in _slots) if (s.rect != null) Destroy(s.rect.gameObject);
            _slots.Clear();

            var sprite = BorrowSlotSprite();
            float total = slotCount * slotSize.x + (slotCount - 1) * spacing;
            float startX = -total * 0.5f + slotSize.x * 0.5f;

            for (int i = 0; i < slotCount; i++)
            {
                var go = new GameObject("Slot" + (i + 1), typeof(RectTransform));
                go.transform.SetParent(slotParent, false);
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = slotSize;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(startX + i * (slotSize.x + spacing), 0f);

                var frame = go.AddComponent<Image>();
                frame.sprite = sprite;
                frame.type = Image.Type.Sliced;
                frame.color = emptyColor;
                frame.raycastTarget = false;

                var iconGo = new GameObject("Icon", typeof(RectTransform));
                iconGo.transform.SetParent(go.transform, false);
                var irt = (RectTransform)iconGo.transform;
                irt.anchorMin = Vector2.zero;
                irt.anchorMax = Vector2.one;
                irt.offsetMin = new Vector2(7f, 7f);
                irt.offsetMax = new Vector2(-7f, -7f);
                var icon = iconGo.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.enabled = false;

                var numGo = new GameObject("Number", typeof(RectTransform));
                numGo.transform.SetParent(go.transform, false);
                var nrt = (RectTransform)numGo.transform;
                nrt.anchorMin = new Vector2(0f, 0f);
                nrt.anchorMax = new Vector2(0f, 0f);
                nrt.pivot = new Vector2(0f, 0f);
                nrt.anchoredPosition = new Vector2(5f, 3f);
                nrt.sizeDelta = new Vector2(20f, 20f);
                var num = numGo.AddComponent<TextMeshProUGUI>();
                num.text = (i + 1).ToString();
                num.fontSize = 14f;
                num.color = new Color(0.75f, 0.76f, 0.82f, 0.9f);
                num.alignment = TextAlignmentOptions.BottomLeft;
                num.raycastTarget = false;

                _slots.Add(new Slot { rect = rt, frame = frame, icon = icon, number = num });
            }
        }

        void Refresh()
        {
            _entries.Clear();
            var inv = inventory?.Inventory;
            if (inv != null)
            {
                foreach (var s in inv.Slots)
                {
                    if (s.IsEmpty) continue;
                    if (s.item is ToolItemSO tool && !_entries.Contains(tool)) _entries.Add(tool);
                }

                foreach (var id in PlaceableIds)
                {
                    foreach (var s in inv.Slots)
                    {
                        if (s.IsEmpty || s.item.id != id) continue;
                        if (!_entries.Contains(s.item)) _entries.Add(s.item);
                        break;
                    }
                }
            }

            var equipped = holder != null ? holder.EquippedTool : null;

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                bool filled = i < _entries.Count;
                var entry = filled ? _entries[i] : null;

                slot.icon.enabled = filled && entry.icon != null;
                if (slot.icon.enabled) slot.icon.sprite = entry.icon;

                bool selected = filled && entry is ToolItemSO &&
                                equipped != null && equipped.id == entry.id;
                slot.frame.color = selected ? selectedColor : (filled ? filledColor : emptyColor);
                slot.number.color = filled
                    ? new Color(0.95f, 0.94f, 0.88f, 0.95f)
                    : new Color(0.55f, 0.56f, 0.62f, 0.55f);
            }
        }
    }
}
