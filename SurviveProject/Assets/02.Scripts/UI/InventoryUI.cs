using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Survive.Core;
using Survive.Input;
using Survive.Items;

namespace Survive.UI
{
    /// <summary>
    /// 기존 CvsUI/PanelInven 그리드(15칸)에 인벤토리를 바인딩한다.
    /// 슬롯 오브젝트는 이미 씬에 있으므로 생성하지 않고 찾아 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    public class InventoryUI : MonoBehaviour, IClosablePanel
    {
        [SerializeField] InputReaderSO input;
        [SerializeField] RectTransform panel;
        [SerializeField] Transform slotParent;
        [SerializeField] CanvasGroup group;
        [SerializeField] float tweenSeconds = 0.18f;

        [Tooltip("소지품을 열 때 손 제작 목록도 같이 연다. 비우면 씬에서 찾는다")]
        [SerializeField] CraftingUI handCrafting;

        [Tooltip("손 제작 목록을 같이 열지")]
        [SerializeField] bool openHandCrafting = true;

        readonly List<InventorySlotView> _slots = new List<InventorySlotView>();
        EquipmentSlotsView _equipmentView;
        PlayerInventory _inventory;
        Survive.Player.PlayerContext _player;
        bool _isOpen;

        void Awake()
        {
            if (slotParent != null)
            {
                foreach (Transform c in slotParent)
                {
                    var v = c.GetComponent<InventorySlotView>();
                    if (v != null) _slots.Add(v);
                }
            }

            // 장비 자리는 격자 밖에 세운다. 격자 안에 넣으면 16번째 칸이 되어
            // "일반 칸을 먹지 않는다"가 화면에서 거짓말이 된다.
            _equipmentView = EquipmentSlotsView.Build(panel, BorrowSlotSprite());

            CloseImmediate();
        }

        /// <summary>장비 자리도 소지품 칸과 같은 테를 쓴다. 혼자 다르게 생기면 남의 UI로 보인다.</summary>
        Sprite BorrowSlotSprite()
        {
            foreach (var s in _slots)
            {
                var img = s != null ? s.GetComponent<UnityEngine.UI.Image>() : null;
                if (img != null && img.sprite != null) return img.sprite;
            }
            return UISkin.Panel;
        }

        void OnEnable()
        {
            if (input != null) input.ToggleInventoryEvent += Toggle;
            if (GameServices.TryGet<UIStateService>(out var ui)) ui.RegisterPanel(this);
            StartCoroutine(BindWhenReady());
        }

        void OnDisable()
        {
            if (input != null) input.ToggleInventoryEvent -= Toggle;
            if (GameServices.TryGet<UIStateService>(out var ui)) ui.UnregisterPanel(this);
            if (_inventory?.Inventory != null) _inventory.Inventory.Changed -= Refresh;
        }

        IEnumerator BindWhenReady()
        {
            yield return null;
            if (GameServices.TryGet<PlayerInventory>(out var inv))
            {
                _inventory = inv;
                _inventory.Inventory.Changed += Refresh;
                Refresh();
            }
            _player = UnityEngine.Object.FindAnyObjectByType<Survive.Player.PlayerContext>(FindObjectsInactive.Exclude);
        }

        /// <summary>
        /// 입력 맵을 UI로 전환하지 않는다. Tab은 Gameplay 맵에만 있어서
        /// 전환하면 인벤토리를 닫을 수 없게 된다. 대신 이동·시점만 잠근다.
        /// </summary>
        void LockControls(bool locked)
        {
            _player?.Locomotion?.SetMovementLocked(locked);
            _player?.CameraRig?.SetLookLocked(locked);
        }

        void Refresh()
        {
            if (_inventory?.Inventory == null) return;
            var slots = _inventory.Inventory.Slots;
            for (int i = 0; i < _slots.Count; i++)
                _slots[i].Render(i < slots.Count ? slots[i] : null);

            _equipmentView?.Render(_inventory.Equipment);
        }

        public bool IsOpen => _isOpen;
        public UIPanelKind PanelKind => UIPanelKind.Inventory;

        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        public void Open()
        {
            // 러스트·아크처럼 소지품 화면에서 바로 만든다.
            // 제작대가 필요한 것은 제작대에서만 뜨고, 손으로 되는 것만 여기 보인다 —
            // 두 목록을 나누는 기준은 이미 레시피의 requiredStation에 있다.
            if (openHandCrafting)
            {
                if (handCrafting == null)
                    handCrafting = Object.FindAnyObjectByType<CraftingUI>(FindObjectsInactive.Include);
                handCrafting?.Open(Survive.Crafting.StationType.None);
            }

            if (_isOpen) return;
            _isOpen = true;
            Refresh();

            if (panel != null) panel.gameObject.SetActive(true);
            if (group != null)
            {
                group.blocksRaycasts = true;
                group.DOKill();
                group.DOFade(1f, tweenSeconds);
            }
            if (panel != null)
            {
                panel.DOKill();
                panel.localScale = Vector3.one * 0.92f;
                panel.DOScale(1f, tweenSeconds).SetEase(Ease.OutBack);
            }

            LockControls(true);
            if (GameServices.TryGet<UIStateService>(out var ui)) ui.NotifyOpened(this);
        }

        public void Close()
        {
            // 제작대에서 연 목록까지 닫으면 안 되므로, 손 제작으로 연 것만 닫는다.
            // 어느 쪽이 딸려 닫히는지는 배타 규칙 표가 안다.
            if (openHandCrafting && handCrafting != null &&
                UIStateService.ActiveRules.ClosesTogether(PanelKind, handCrafting.PanelKind))
                handCrafting.Close();

            if (!_isOpen) return;
            _isOpen = false;

            if (group != null)
            {
                group.blocksRaycasts = false;
                group.DOKill();
                group.DOFade(0f, tweenSeconds).OnComplete(() =>
                {
                    if (panel != null) panel.gameObject.SetActive(false);
                });
            }
            else CloseImmediate();

            LockControls(false);
            if (GameServices.TryGet<UIStateService>(out var ui)) ui.NotifyClosed(this);
        }

        void CloseImmediate()
        {
            _isOpen = false;
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
            }
            if (panel != null) panel.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            group?.DOKill();
            panel?.DOKill();
        }
    }
}
