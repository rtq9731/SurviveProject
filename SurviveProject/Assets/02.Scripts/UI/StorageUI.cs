using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;
using Survive.Core;
using Survive.Interaction;
using Survive.Items;
using Survive.Player;

namespace Survive.UI
{
    /// <summary>
    /// 보관함을 열었을 때의 화면.
    ///
    /// 소지품 UI와 나란히 뜬다. 넣고 빼는 것은 클릭 한 번으로 한 칸씩 옮긴다 —
    /// 드래그 앤 드롭은 만들 것이 많고, 이 게임에서 옮길 물건은 대부분
    /// 한 종류를 통째로 넘기는 것이라 한 번 클릭이면 충분하다.
    /// </summary>
    [DisallowMultipleComponent]
    public class StorageUI : MonoBehaviour, IClosablePanel
    {
        [SerializeField] RectTransform panel;
        [SerializeField] Transform slotParent;
        [SerializeField] CanvasGroup group;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_FontAsset font;

        [SerializeField] int columns = 6;
        [SerializeField] Vector2 slotSize = new Vector2(58f, 58f);
        [SerializeField] float spacing = 6f;
        [SerializeField] float tweenSeconds = 0.18f;

        readonly List<(Button button, Image icon, TMP_Text count)> _slots =
            new List<(Button, Image, TMP_Text)>();

        StorageContainer _open;
        PlayerContext _player;
        bool _isOpen;

        public bool IsOpen => _isOpen;

        void Awake() => CloseImmediate();

        void OnEnable() => GameServices.Register(this);
        void OnDisable() => GameServices.Unregister<StorageUI>();

        public void Open(StorageContainer container, PlayerContext player)
        {
            _open = container;
            _player = player;
            if (_open == null) return;

            if (_slots.Count == 0) BuildSlots();
            if (title != null) title.text = _open.DisplayName;

            _isOpen = true;
            Refresh();

            if (group != null)
            {
                group.blocksRaycasts = true;
                group.interactable = true;
                group.DOKill();
                group.DOFade(1f, tweenSeconds);
            }
            if (panel != null)
            {
                panel.DOKill();
                panel.localScale = Vector3.one * 0.92f;
                panel.DOScale(1f, tweenSeconds).SetEase(Ease.OutBack);
            }

            // 소지품도 같이 연다. 옮길 대상이 양쪽에 보여야 옮길 수 있다.
            var inv = Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
            if (inv != null && !inv.IsOpen) inv.Open();

            // 손 제작 목록은 보관함과 같은 자리를 쓴다. 겹치면 둘 다 못 읽는다.
            // 상자 앞에 서 있는 사람이 지금 하려는 것은 제작이 아니라 정리다.
            var craft = Object.FindFirstObjectByType<CraftingUI>(FindObjectsInactive.Include);
            if (craft != null && craft.IsOpen && craft.CurrentStation == Survive.Crafting.StationType.None)
                craft.Close();

            if (GameServices.TryGet<UIStateService>(out var ui)) ui.NotifyOpened(this);
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            _open = null;

            if (group != null)
            {
                group.blocksRaycasts = false;
                group.interactable = false;
                group.DOKill();
                group.DOFade(0f, tweenSeconds);
            }

            if (GameServices.TryGet<UIStateService>(out var ui)) ui.NotifyClosed(this);
        }

        void CloseImmediate()
        {
            _isOpen = false;
            if (group == null) return;
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        void BuildSlots()
        {
            if (slotParent == null) return;

            var sprite = UISkin.Panel;
            int total = 18;

            for (int i = 0; i < total; i++)
            {
                var go = new GameObject("Slot" + i, typeof(RectTransform));
                go.transform.SetParent(slotParent, false);
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = slotSize;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                int col = i % columns, row = i / columns;
                rt.anchoredPosition = new Vector2(col * (slotSize.x + spacing),
                                                  -row * (slotSize.y + spacing));

                var frame = go.AddComponent<Image>();
                frame.sprite = sprite;
                frame.type = Image.Type.Sliced;
                frame.color = new Color(0.14f, 0.16f, 0.21f, 0.9f);

                var btn = go.AddComponent<Button>();

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

                var cntGo = new GameObject("Count", typeof(RectTransform));
                cntGo.transform.SetParent(go.transform, false);
                var crt = (RectTransform)cntGo.transform;
                crt.anchorMin = new Vector2(1f, 0f);
                crt.anchorMax = new Vector2(1f, 0f);
                crt.pivot = new Vector2(1f, 0f);
                crt.anchoredPosition = new Vector2(-4f, 3f);
                crt.sizeDelta = new Vector2(34f, 20f);
                var cnt = cntGo.AddComponent<TextMeshProUGUI>();
                if (font != null) cnt.font = font;
                cnt.fontSize = 15f;
                cnt.alignment = TextAlignmentOptions.BottomRight;
                cnt.color = Color.white;
                cnt.raycastTarget = false;

                int captured = i;
                btn.onClick.AddListener(() => TakeOut(captured));

                _slots.Add((btn, icon, cnt));
            }
        }

        /// <summary>보관함 → 소지품.</summary>
        void TakeOut(int index)
        {
            if (_open == null || _player?.Inventory == null) return;

            var slots = _open.Contents.Slots;
            if (index < 0 || index >= slots.Count) return;

            var s = slots[index];
            if (s.IsEmpty) return;

            int left = _player.Inventory.Add(s.item, s.count);
            int moved = s.count - left;
            if (moved > 0) _open.Contents.TryRemove(s.item.id, moved);

            Refresh();
        }

        /// <summary>소지품 → 보관함. 소지품 슬롯이 이걸 부른다.</summary>
        public bool PutIn(ItemDataSO item, int count)
        {
            if (_open == null || item == null || count <= 0) return false;

            int left = _open.Contents.TryAdd(item, count);
            int moved = count - left;
            if (moved <= 0) return false;

            _player?.Inventory?.Remove(item.id, moved);
            Refresh();
            return true;
        }

        void Refresh()
        {
            if (_open == null) return;
            var slots = _open.Contents.Slots;

            for (int i = 0; i < _slots.Count; i++)
            {
                var (btn, icon, cnt) = _slots[i];
                bool filled = i < slots.Count && !slots[i].IsEmpty;

                icon.enabled = filled && slots[i].item.icon != null;
                if (icon.enabled) icon.sprite = slots[i].item.icon;

                cnt.text = filled && slots[i].count > 1 ? slots[i].count.ToString() : "";
                btn.interactable = filled;
            }
        }
    }
}
