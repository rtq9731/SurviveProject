using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using TMPro;
using Survive.Building;
using Survive.Items;

namespace Survive.UI
{
    /// <summary>
    /// 지을 수 있는 것 목록.
    ///
    /// 제작 목록(CraftingUI)과 같은 모양으로 만든다 — 만드는 것과 짓는 것은
    /// 플레이어에게 같은 종류의 행동이고, 화면이 달라 보일 이유가 없다.
    /// 행 생성 방식도 그쪽과 같은 구조를 따른다.
    /// </summary>
    [DisallowMultipleComponent]
    public class BuildMenuUI : MonoBehaviour, IClosablePanel
    {
        [SerializeField] BuildCatalogSO catalog;
        [SerializeField] BuildPlacer placer;
        [SerializeField] PlayerInventory inventory;

        [SerializeField] RectTransform panel;
        [SerializeField] Transform rowParent;
        [SerializeField] CanvasGroup group;
        [SerializeField] TMP_FontAsset font;

        [SerializeField] float tweenSeconds = 0.18f;

        readonly List<(BuildableSO item, Button button, TMP_Text label)> _rows =
            new List<(BuildableSO, Button, TMP_Text)>();

        bool _isOpen;

        public bool IsOpen => _isOpen;

        void Awake()
        {
            if (placer == null) placer = Object.FindFirstObjectByType<BuildPlacer>(FindObjectsInactive.Include);
            if (inventory == null) inventory = Object.FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            if (rowParent == null) rowParent = transform;

            // 여기서 gameObject.SetActive(false)를 하면 안 된다.
            // 이 스크립트가 붙은 오브젝트를 끄면 Open()의 SetActive(true)가
            // Awake를 다시 깨우고, 그 Awake가 또 끈다. 제작창에서 두 번 겪었다.
            CloseImmediate();
        }

        void OnEnable()
        {
            if (inventory?.Inventory != null) inventory.Inventory.Changed += Refresh;
        }

        void OnDisable()
        {
            if (inventory?.Inventory != null) inventory.Inventory.Changed -= Refresh;
        }

        void BuildRows()
        {
            if (rowParent == null || catalog == null) return;

            foreach (var (_, b, _) in _rows) if (b != null) Destroy(b.gameObject);
            _rows.Clear();

            var sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");

            foreach (var e in catalog.entries)
            {
                if (e == null) continue;

                var go = new GameObject("Row_" + e.id, typeof(RectTransform));
                go.transform.SetParent(rowParent, false);
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = new Vector2(520f, 44f);

                var img = go.AddComponent<Image>();
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
                img.color = new Color(0.12f, 0.14f, 0.18f, 0.9f);

                var btn = go.AddComponent<Button>();

                var labelGo = new GameObject("Label", typeof(RectTransform));
                labelGo.transform.SetParent(go.transform, false);
                var lrt = (RectTransform)labelGo.transform;
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = new Vector2(12f, 0f);
                lrt.offsetMax = new Vector2(-12f, 0f);

                var txt = labelGo.AddComponent<TextMeshProUGUI>();
                if (font != null) txt.font = font;
                txt.fontSize = 20f;
                txt.alignment = TextAlignmentOptions.Left;
                txt.color = Color.white;
                txt.raycastTarget = false;
                txt.enableAutoSizing = true;
                txt.fontSizeMin = 13f;
                txt.fontSizeMax = 20f;

                var captured = e;
                btn.onClick.AddListener(() => Choose(captured));

                _rows.Add((e, btn, txt));
            }
            Refresh();
        }

        void Choose(BuildableSO b)
        {
            placer?.Select(b);
            // 고르면 목록은 닫는다. 미리보기를 봐야 놓을 자리를 정할 수 있다.
            Close();
        }

        void Refresh()
        {
            var inv = inventory?.Inventory;

            foreach (var (item, button, label) in _rows)
            {
                bool affordable = true;
                if (inv != null && item.cost != null)
                {
                    foreach (var c in item.cost)
                    {
                        if (c?.item == null) continue;
                        if (!inv.Has(c.item.id, c.count)) { affordable = false; break; }
                    }
                }

                if (button != null) button.interactable = affordable;
                if (label != null)
                {
                    label.text = Describe(item, inv);
                    label.color = affordable ? Color.white : new Color(0.65f, 0.65f, 0.7f, 1f);
                }
            }
        }

        string Describe(BuildableSO b, Inventory inv)
        {
            var sb = new StringBuilder();
            sb.Append(string.IsNullOrEmpty(b.displayName) ? b.id : b.displayName);
            sb.Append("  ·  ");

            if (b.cost == null || b.cost.Length == 0) { sb.Append("재료 없음"); return sb.ToString(); }

            bool first = true;
            foreach (var c in b.cost)
            {
                if (c?.item == null) continue;
                if (!first) sb.Append(", ");
                int held = inv != null ? inv.CountOf(c.item.id) : 0;
                sb.Append($"{c.item.displayName} {held}/{c.count}");
                first = false;
            }
            return sb.ToString();
        }

        // ── 열고 닫기 ────────────────────────────────────────────

        public void Open()
        {
            if (_isOpen) { Refresh(); return; }
            _isOpen = true;

            if (_rows.Count == 0) BuildRows();
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

            if (Survive.Core.GameServices.TryGet<UIStateService>(out var ui)) ui.NotifyOpened(this);
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;

            if (group != null)
            {
                group.blocksRaycasts = false;
                group.interactable = false;
                group.DOKill();
                group.DOFade(0f, tweenSeconds);
            }

            if (Survive.Core.GameServices.TryGet<UIStateService>(out var ui)) ui.NotifyClosed(this);
        }

        void CloseImmediate()
        {
            _isOpen = false;
            if (group == null) return;
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }
    }
}
