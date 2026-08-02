using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
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
        [SerializeField] Font font;
        [SerializeField] MMF_Player craftFeedback;
        [SerializeField] float tweenSeconds = 0.18f;

        readonly List<(RecipeSO recipe, Button button, Text label)> _rows = new List<(RecipeSO, Button, Text)>();
        PlayerInventory _inventory;
        Survive.Player.PlayerContext _player;
        StationType _station = StationType.None;
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
                img.color = new Color(0.12f, 0.14f, 0.18f, 0.9f);

                var btn = go.AddComponent<Button>();

                var labelGo = new GameObject("Label", typeof(RectTransform));
                labelGo.transform.SetParent(go.transform, false);
                var lrt = (RectTransform)labelGo.transform;
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = new Vector2(12f, 0f);
                lrt.offsetMax = new Vector2(-12f, 0f);

                var txt = labelGo.AddComponent<Text>();
                txt.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.fontSize = 20;
                txt.alignment = TextAnchor.MiddleLeft;
                txt.color = Color.white;
                txt.raycastTarget = false;

                var captured = r;
                btn.onClick.AddListener(() => TryCraft(captured));

                _rows.Add((r, btn, txt));
            }
            RefreshList();
        }

        void RefreshList()
        {
            var inv = _inventory?.Inventory;
            foreach (var (recipe, button, label) in _rows)
            {
                bool canMake = inv != null && CraftingService.CanCraft(recipe, inv, _station);
                if (button != null) button.interactable = canMake;
                if (label != null)
                {
                    label.text = Describe(recipe, inv);
                    label.color = canMake ? Color.white : new Color(0.65f, 0.65f, 0.7f, 1f);
                }
            }
        }

        string Describe(RecipeSO r, Inventory inv)
        {
            var sb = new StringBuilder();
            sb.Append(string.IsNullOrEmpty(r.displayName) ? r.result?.item?.displayName ?? r.id : r.displayName);
            sb.Append("  —  ");

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
