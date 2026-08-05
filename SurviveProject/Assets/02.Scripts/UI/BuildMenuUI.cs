using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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

        /// <summary>제작 목록과 같은 색을 쓴다.</summary>
        static readonly Color NormalFrame = new Color(0.12f, 0.14f, 0.18f, 0.9f);

        /// <summary>
        /// 아는 건축물이 하나도 없을 때 대신 서는 한 줄. 제작 목록과 같은 이유다 —
        /// 판때기가 여백만 남은 띠로 찌그러지지 않게 한다.
        /// </summary>
        GameObject _emptyRow;
        TMP_Text _emptyLabel;

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

            // 이 화면은 제작 목록과 달리 매 프레임 다시 그리지 않는다. 로케일이 바뀌면
            // 아무도 다시 쓰지 않으므로 여기서 듣고 한 번 다시 그린다.
            Survive.Localization.Loc.LocaleChanged += Refresh;
            StartCoroutine(BindLedger());
        }

        void OnDisable()
        {
            if (inventory?.Inventory != null) inventory.Inventory.Changed -= Refresh;
            Survive.Localization.Loc.LocaleChanged -= Refresh;
            if (Survive.Core.GameServices.TryGet<UIStateService>(out var ui)) ui.UnregisterPanel(this);
            Unbind();
        }

        UnlockLedger _ledger;

        /// <summary>
        /// 원장이 열리는 것을 듣는다.
        ///
        /// 소지품이 바뀔 때만 다시 그리면 되던 시절이 끝났다. 연구대는 <b>아이템을
        /// 하나도 건드리지 않고</b> 청사진을 연다(백로그 38) — 목록을 띄워 놓고
        /// 분석이 끝나기를 기다리는 사람에게는 아무 일도 일어나지 않은 것처럼 보이고,
        /// 창을 닫았다 다시 열어야만 잠금이 풀린다.
        ///
        /// 원장은 씬이 아니라 판에 붙어 있어(<see cref="UnlockService"/>) 이 화면보다
        /// 늦게 설 수 있다. 그래서 한 번 물어보고 마는 것이 아니라 설 때까지 기다린다.
        /// </summary>
        IEnumerator BindLedger()
        {
            while (_ledger == null)
            {
                var ledger = BlueprintGate.Active;
                if (ledger != null)
                {
                    _ledger = ledger;
                    _ledger.KeyUnlocked += OnKeyUnlocked;
                    Refresh();
                    yield break;
                }
                yield return null;
            }
        }

        void OnKeyUnlocked(string key) => Refresh();

        void Unbind()
        {
            if (_ledger == null) return;
            _ledger.KeyUnlocked -= OnKeyUnlocked;
            _ledger = null;
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

            BuildEmptyRow();
            Refresh();
        }

        /// <summary>
        /// "아직 아는 건축물이 없다" 한 줄. 누르는 자리가 아니므로 Button을 달지 않고,
        /// 이름도 Row_로 시작하지 않게 둔다 — 검증이 실린 줄을 셀 때 섞이면 안 된다.
        /// </summary>
        void BuildEmptyRow()
        {
            if (_emptyRow != null || rowParent == null) return;

            var go = new GameObject("EmptyNotice", typeof(RectTransform));
            go.transform.SetParent(rowParent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(520f, 44f);

            var img = go.AddComponent<Image>();
            img.sprite = UISkin.Panel;
            img.type = Image.Type.Sliced;
            img.color = new Color(0.10f, 0.11f, 0.14f, 0.85f);
            img.raycastTarget = false;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var lrt = (RectTransform)labelGo.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(12f, 0f);
            lrt.offsetMax = new Vector2(-12f, 0f);

            _emptyLabel = labelGo.AddComponent<TextMeshProUGUI>();
            if (font != null) _emptyLabel.font = font;
            _emptyLabel.fontSize = 20f;
            _emptyLabel.alignment = TextAlignmentOptions.Center;
            _emptyLabel.color = new Color(0.62f, 0.64f, 0.70f, 1f);
            _emptyLabel.raycastTarget = false;
            _emptyLabel.text = MenuListing.NothingKnownToBuild;

            _emptyRow = go;
            go.SetActive(false);
        }

        /// <summary>실제로 켜진 줄만큼 판때기를 늘린다.</summary>
        void FitPanel(int visibleRows)
        {
            var rowsRect = rowParent as RectTransform;
            if (panel == null || rowsRect == null) return;

            var first = _rows.Count > 0 && _rows[0].button != null
                ? (RectTransform)_rows[0].button.transform
                : null;
            float rowHeight = first != null ? first.sizeDelta.y : 44f;

            var layout = rowsRect.GetComponent<VerticalLayoutGroup>();
            float spacing = layout != null ? layout.spacing : 6f;

            UISkin.FitPanelHeight(panel, rowsRect, visibleRows, rowHeight, spacing);
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
            int shown = 0;

            foreach (var (item, button, label, frame) in _rows)
            {
                // 모르는 것은 줄 자체를 끈다. 회색으로 남겨 두던 시절에는 잠긴 줄이
                // 이름과 여는 방법까지 적어 주었고, 그 한 줄로 후반 전개가 새어 나갔다.
                // 재료 부족은 다른 잠금이다 — 그건 줄을 남기고 회색으로만 죽인다.
                bool listed = MenuListing.ShouldList(item, ledger);

                var go = button != null ? button.gameObject : null;
                if (go != null && go.activeSelf != listed) go.SetActive(listed);
                if (!listed) continue;
                shown++;

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
                if (frame != null) frame.color = NormalFrame;
                if (label != null)
                {
                    label.text = MenuListing.BuildableLine(item, inv);
                    label.color = affordable ? Color.white : new Color(0.65f, 0.65f, 0.7f, 1f);
                }
            }

            if (_emptyRow != null)
            {
                bool empty = shown <= 0;
                if (_emptyRow.activeSelf != empty) _emptyRow.SetActive(empty);
                // 만들 때 한 번 쓰고 말면 로케일이 바뀌어도 옛 글자가 남는다.
                if (_emptyLabel != null) _emptyLabel.text = MenuListing.NothingKnownToBuild;
            }

            FitPanel(MenuListing.PanelRows(shown));
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
