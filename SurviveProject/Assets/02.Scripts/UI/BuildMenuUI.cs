using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using TMPro;
using Survive.Building;
using Survive.Items;
using Survive.Progression;

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

        readonly List<(BuildableSO item, Button button, TMP_Text label, Image frame)> _rows =
            new List<(BuildableSO, Button, TMP_Text, Image)>();

        /// <summary>제작 목록과 같은 색을 쓴다. 잠긴 줄은 실루엣만 남는다.</summary>
        static readonly Color LockedFrame = new Color(0.09f, 0.10f, 0.13f, 0.75f);
        static readonly Color LockedLabel = new Color(0.46f, 0.48f, 0.56f, 1f);
        static readonly Color NormalFrame = new Color(0.12f, 0.14f, 0.18f, 0.9f);

        bool _isOpen;
        Survive.Player.PlayerContext _player;

        public bool IsOpen => _isOpen;
        public UIPanelKind PanelKind => UIPanelKind.BuildMenu;

        void Awake()
        {
            if (placer == null) placer = Object.FindAnyObjectByType<BuildPlacer>(FindObjectsInactive.Include);
            if (inventory == null) inventory = Object.FindAnyObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            if (rowParent == null) rowParent = transform;

            // 여기서 gameObject.SetActive(false)를 하면 안 된다.
            // 이 스크립트가 붙은 오브젝트를 끄면 Open()의 SetActive(true)가
            // Awake를 다시 깨우고, 그 Awake가 또 끈다. 제작창에서 두 번 겪었다.
            CloseImmediate();
        }

        void OnEnable()
        {
            if (inventory?.Inventory != null) inventory.Inventory.Changed += Refresh;
            if (Survive.Core.GameServices.TryGet<UIStateService>(out var ui)) ui.RegisterPanel(this);
        }

        void OnDisable()
        {
            if (inventory?.Inventory != null) inventory.Inventory.Changed -= Refresh;
            if (Survive.Core.GameServices.TryGet<UIStateService>(out var ui)) ui.UnregisterPanel(this);
        }

        void BuildRows()
        {
            if (rowParent == null || catalog == null) return;

            foreach (var (_, b, _, _) in _rows) if (b != null) Destroy(b.gameObject);
            _rows.Clear();

            var sprite = UISkin.Panel;

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
                img.color = NormalFrame;

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

                _rows.Add((e, btn, txt, img));
            }

            FitPanel();
            Refresh();
        }

        /// <summary>목록이 길어진 만큼 판때기도 길어져야 한다.</summary>
        void FitPanel()
        {
            var rowsRect = rowParent as RectTransform;
            if (panel == null || rowsRect == null || _rows.Count == 0) return;

            var first = _rows[0].button != null ? (RectTransform)_rows[0].button.transform : null;
            float rowHeight = first != null ? first.sizeDelta.y : 44f;

            var layout = rowsRect.GetComponent<VerticalLayoutGroup>();
            float spacing = layout != null ? layout.spacing : 6f;

            UISkin.FitPanelHeight(panel, rowsRect, _rows.Count, rowHeight, spacing);
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
            var ledger = BlueprintGate.Active;

            foreach (var (item, button, label, frame) in _rows)
            {
                // 재료와 청사진은 독립된 두 잠금이다. 제작 목록과 같은 규칙으로,
                // 모르는 것도 줄은 남기고 실루엣으로 가라앉힌다.
                bool known = BlueprintGate.IsUnlocked(item.requiredBlueprint, ledger);

                bool affordable = true;
                if (inv != null && item.cost != null)
                {
                    foreach (var c in item.cost)
                    {
                        if (c?.item == null) continue;
                        if (!inv.Has(c.item.id, c.count)) { affordable = false; break; }
                    }
                }

                if (button != null) button.interactable = known && affordable;
                if (frame != null) frame.color = known ? NormalFrame : LockedFrame;
                if (label != null)
                {
                    label.text = known ? Describe(item, inv) : DescribeLocked(item);
                    label.color = !known ? LockedLabel
                                : affordable ? Color.white
                                             : new Color(0.65f, 0.65f, 0.7f, 1f);
                }
            }
        }

        /// <summary>잠긴 줄 — 이름과 여는 방법만.</summary>
        static string DescribeLocked(BuildableSO b)
        {
            string name = string.IsNullOrEmpty(b.displayName) ? b.id : b.displayName;
            return $"{name}  ·  {BlueprintGate.LockText(b.requiredBlueprint)}";
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

            LockControls(true);
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

            LockControls(false);
            if (Survive.Core.GameServices.TryGet<UIStateService>(out var ui)) ui.NotifyClosed(this);
        }

        /// <summary>
        /// 목록이 떠 있는 동안은 커서를 풀어 준다. 제작창과 같은 처리다 —
        /// 이게 빠져 있어서 B를 눌러도 마우스가 시야에 묶인 채였고,
        /// 목록은 떴는데 아무것도 고를 수 없었다.
        ///
        /// 닫을 때 되돌린다. 고른 직후에는 유령을 조준해야 하니
        /// 커서가 다시 잠기는 것이 맞다.
        /// </summary>
        void LockControls(bool locked)
        {
            if (_player == null)
                _player = Object.FindAnyObjectByType<Survive.Player.PlayerContext>(
                    FindObjectsInactive.Exclude);

            _player?.Locomotion?.SetMovementLocked(locked);
            _player?.CameraRig?.SetLookLocked(locked);
        }

        void CloseImmediate()
        {
            _isOpen = false;
            if (group == null) return;
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        void OnDestroy()
        {
            group?.DOKill();
            panel?.DOKill();
        }
    }
}
