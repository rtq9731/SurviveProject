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

        readonly List<InventorySlotView> _slots = new List<InventorySlotView>();
        PlayerInventory _inventory;
        Survive.Player.PlayerContext _player;
        bool _열림;

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
            즉시닫기();
        }

        void OnEnable()
        {
            if (input != null) input.ToggleInventoryEvent += Toggle;
            StartCoroutine(연결대기());
        }

        void OnDisable()
        {
            if (input != null) input.ToggleInventoryEvent -= Toggle;
            if (_inventory?.Inventory != null) _inventory.Inventory.Changed -= 갱신;
        }

        IEnumerator 연결대기()
        {
            yield return null;
            if (GameServices.TryGet<PlayerInventory>(out var inv))
            {
                _inventory = inv;
                _inventory.Inventory.Changed += 갱신;
                갱신();
            }
            _player = UnityEngine.Object.FindFirstObjectByType<Survive.Player.PlayerContext>(FindObjectsInactive.Exclude);
        }

        /// <summary>
        /// 입력 맵을 UI로 전환하지 않는다. Tab은 Gameplay 맵에만 있어서
        /// 전환하면 인벤토리를 닫을 수 없게 된다. 대신 이동·시점만 잠근다.
        /// </summary>
        void 조작잠금(bool 잠글까)
        {
            _player?.Locomotion?.SetMovementLocked(잠글까);
            _player?.CameraRig?.SetLookLocked(잠글까);
        }

        void 갱신()
        {
            if (_inventory?.Inventory == null) return;
            var slots = _inventory.Inventory.Slots;
            for (int i = 0; i < _slots.Count; i++)
                _slots[i].Render(i < slots.Count ? slots[i] : null);
        }

        public bool IsOpen => _열림;

        public void Toggle()
        {
            if (_열림) Close();
            else Open();
        }

        public void Open()
        {
            if (_열림) return;
            _열림 = true;
            갱신();

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

            조작잠금(true);
            if (GameServices.TryGet<UIStateService>(out var ui)) ui.NotifyOpened(this);
        }

        public void Close()
        {
            if (!_열림) return;
            _열림 = false;

            if (group != null)
            {
                group.blocksRaycasts = false;
                group.DOKill();
                group.DOFade(0f, tweenSeconds).OnComplete(() =>
                {
                    if (panel != null) panel.gameObject.SetActive(false);
                });
            }
            else 즉시닫기();

            조작잠금(false);
            if (GameServices.TryGet<UIStateService>(out var ui)) ui.NotifyClosed(this);
        }

        void 즉시닫기()
        {
            _열림 = false;
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
            }
            if (panel != null) panel.gameObject.SetActive(false);
        }
    }
}
