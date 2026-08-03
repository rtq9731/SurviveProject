using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;
using MoreMountains.Feedbacks;
using Survive.Core;
using Survive.Crafting;
using Survive.Input;
using Survive.Items;

namespace Survive.UI
{
    /// <summary>
    /// 레시피 목록과 제작 버튼. 행은 런타임에 만든다 —
    /// 기존 씬에 제작 UI 레이아웃이 없어서 재활용할 것이 없다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CraftingUI : MonoBehaviour, IClosablePanel
    {
        [SerializeField] InputReaderSO input;
        [SerializeField] RecipeBookSO book;
        [SerializeField] RectTransform panel;
        [SerializeField] CanvasGroup group;
        [SerializeField] Transform rowParent;
        [Tooltip("비우면 TMP 기본 폰트를 쓴다")]
        [SerializeField] TMP_FontAsset font;
        [SerializeField] MMF_Player craftFeedback;
        [SerializeField] float tweenSeconds = 0.18f;

        readonly List<(RecipeSO recipe, Button button, TMP_Text label)> _rows =
            new List<(RecipeSO, Button, TMP_Text)>();
        PlayerInventory _inventory;
        Survive.Player.PlayerContext _player;
        StationType _station = StationType.None;

        /// <summary>지금 열려 있는 목록이 어느 작업대 기준인지. 소지품 UI가 구분에 쓴다.</summary>
        public StationType CurrentStation => _station;
        bool _isOpen;

        public bool IsOpen => _isOpen;

        void Awake()
        {
            // 여기서 gameObject.SetActive(false)를 하면 안 된다.
            // 이 스크립트가 붙은 오브젝트를 끄면, Open()의 SetActive(true)가
            // 이 Awake를 처음 깨우고 곧바로 다시 닫아버린다.
            // 보이고 안 보이고는 CanvasGroup으로만 다룬다.
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
            _isOpen = false;
        }

        void OnEnable()
        {
            // ESC 처리는 UIStateService가 전담한다. 여기서 따로 듣지 않는다 —
            // 패널마다 각자 들으면 닫히는 것과 안 닫히는 것이 생긴다.
            StartCoroutine(BindWhenReady());
        }

        IEnumerator BindWhenReady()
        {
            yield return null;
            GameServices.TryGet<PlayerInventory>(out _inventory);
            _player = UnityEngine.Object.FindFirstObjectByType<Survive.Player.PlayerContext>(FindObjectsInactive.Exclude);
            BuildRows();
        }

        void BuildRows()
        {
            if (rowParent == null || book == null) return;
            foreach (var (_, b, _) in _rows) if (b != null) Destroy(b.gameObject);
            _rows.Clear();

            foreach (var r in book.recipes)
            {
                if (r == null) continue;

                var go = new GameObject("Row_" + r.id, typeof(RectTransform));
                go.transform.SetParent(rowParent, false);
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = new Vector2(520f, 44f);

                var img = go.AddComponent<Image>();
                UISkin.ApplyPanel(img, new Color(0.12f, 0.14f, 0.18f, 0.9f));

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
                // 재료가 많은 레시피는 한 줄을 넘긴다. 줄이 늘면 행이 밀리므로 줄인다.
                txt.enableAutoSizing = true;
                txt.fontSizeMin = 13f;
                txt.fontSizeMax = 20f;

                var captured = r;
                btn.onClick.AddListener(() => TryCraft(captured));

                _rows.Add((r, btn, txt));
            }
            RefreshList();
        }

        void RefreshList()
        {
            var inv = _inventory?.Inventory;
            int shown = 0;

            foreach (var (recipe, button, label) in _rows)
            {
                // 손 제작 목록에서는 제작대 전용을 아예 숨긴다.
                // 회색으로 남겨두면 "왜 안 되지"를 매번 확인하게 된다.
                bool visible = _station != StationType.None ||
                               recipe.requiredStation == StationType.None;
                if (button != null && button.gameObject.activeSelf != visible)
                    button.gameObject.SetActive(visible);
                if (!visible) continue;
                shown++;

                bool canMake = inv != null && CraftingService.CanCraft(recipe, inv, _station);
                if (button != null) button.interactable = canMake;
                if (label != null)
                {
                    label.text = Describe(recipe, inv);
                    label.color = canMake ? Color.white : new Color(0.65f, 0.65f, 0.7f, 1f);
                }
            }

            FitPanel(shown);
        }

        /// <summary>
        /// 보이는 줄 수에 맞춰 판때기를 늘린다.
        ///
        /// 손 제작과 제작대는 보이는 줄 수가 다르다. 높이를 하나로 고정해 두면
        /// 한쪽은 아래가 비고 다른 쪽은 흘러넘친다.
        /// </summary>
        void FitPanel(int visibleRows)
        {
            var rowsRect = rowParent as RectTransform;
            if (panel == null || rowsRect == null || _rows.Count == 0) return;

            var first = _rows[0].button != null ? (RectTransform)_rows[0].button.transform : null;
            float rowHeight = first != null ? first.sizeDelta.y : 44f;

            var layout = rowsRect.GetComponent<VerticalLayoutGroup>();
            float spacing = layout != null ? layout.spacing : 8f;

            UISkin.FitPanelHeight(panel, rowsRect, visibleRows, rowHeight, spacing);
        }

        string Describe(RecipeSO r, Inventory inv)
        {
            var sb = new StringBuilder();
            sb.Append(string.IsNullOrEmpty(r.displayName) ? r.result?.item?.displayName ?? r.id : r.displayName);
            sb.Append("  ·  ");

            if (r.ingredients == null || r.ingredients.Length == 0) sb.Append("재료 없음");
            else
            {
                bool first = true;
                foreach (var need in r.ingredients)
                {
                    if (need?.item == null) continue;
                    if (!first) sb.Append(", ");
                    int held = inv != null ? inv.CountOf(need.item.id) : 0;
                    sb.Append($"{need.item.displayName} {held}/{need.count}");
                    first = false;
                }
            }
            if (r.requiredStation != StationType.None) sb.Append("  (제작대 필요)");
            return sb.ToString();
        }

        void TryCraft(RecipeSO r)
        {
            var inv = _inventory?.Inventory;
            if (inv == null) return;

            if (CraftingService.Craft(r, inv, _station))
                craftFeedback?.PlayFeedbacks();

            RefreshList();
        }

        public void Open(StationType station)
        {
            _station = station;
            if (_isOpen) { RefreshList(); return; }

            _isOpen = true;

            // 행이 아직 없으면 지금 만든다. 오브젝트가 꺼져 있어
            // 연결대기 코루틴이 돌지 못했을 수 있다.
            if (_rows.Count == 0) BuildRows();
            RefreshList();

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
            if (GameServices.TryGet<UIStateService>(out var ui)) ui.NotifyOpened(this);
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
            if (GameServices.TryGet<UIStateService>(out var ui)) ui.NotifyClosed(this);
        }

        void LockControls(bool locked)
        {
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
    }
}
