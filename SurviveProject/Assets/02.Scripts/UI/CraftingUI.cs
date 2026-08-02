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
        StationType _현재스테이션 = StationType.None;
        bool _열림;

        public bool IsOpen => _열림;

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
            _열림 = false;
        }

        void OnEnable()
        {
            // ESC 처리는 UIStateService가 전담한다. 여기서 따로 듣지 않는다 —
            // 패널마다 각자 들으면 닫히는 것과 안 닫히는 것이 생긴다.
            StartCoroutine(연결대기());
        }

        IEnumerator 연결대기()
        {
            yield return null;
            GameServices.TryGet<PlayerInventory>(out _inventory);
            _player = UnityEngine.Object.FindFirstObjectByType<Survive.Player.PlayerContext>(FindObjectsInactive.Exclude);
            행만들기();
        }

        void 행만들기()
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

                var 캡처 = r;
                btn.onClick.AddListener(() => 제작시도(캡처));

                _rows.Add((r, btn, txt));
            }
            목록갱신();
        }

        void 목록갱신()
        {
            var inv = _inventory?.Inventory;
            foreach (var (recipe, button, label) in _rows)
            {
                bool 가능 = inv != null && CraftingService.CanCraft(recipe, inv, _현재스테이션);
                if (button != null) button.interactable = 가능;
                if (label != null)
                {
                    label.text = 설명(recipe, inv);
                    label.color = 가능 ? Color.white : new Color(0.65f, 0.65f, 0.7f, 1f);
                }
            }
        }

        string 설명(RecipeSO r, Inventory inv)
        {
            var sb = new StringBuilder();
            sb.Append(string.IsNullOrEmpty(r.displayName) ? r.result?.item?.displayName ?? r.id : r.displayName);
            sb.Append("  —  ");

            if (r.ingredients == null || r.ingredients.Length == 0) sb.Append("재료 없음");
            else
            {
                bool 첫 = true;
                foreach (var need in r.ingredients)
                {
                    if (need?.item == null) continue;
                    if (!첫) sb.Append(", ");
                    int 보유 = inv != null ? inv.CountOf(need.item.id) : 0;
                    sb.Append($"{need.item.displayName} {보유}/{need.count}");
                    첫 = false;
                }
            }
            if (r.requiredStation != StationType.None) sb.Append("  (제작대 필요)");
            return sb.ToString();
        }

        void 제작시도(RecipeSO r)
        {
            var inv = _inventory?.Inventory;
            if (inv == null) return;

            if (CraftingService.Craft(r, inv, _현재스테이션))
                craftFeedback?.PlayFeedbacks();

            목록갱신();
        }

        public void Open(StationType station)
        {
            _현재스테이션 = station;
            if (_열림) { 목록갱신(); return; }

            _열림 = true;

            // 행이 아직 없으면 지금 만든다. 오브젝트가 꺼져 있어
            // 연결대기 코루틴이 돌지 못했을 수 있다.
            if (_rows.Count == 0) 행만들기();
            목록갱신();

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
                group.interactable = false;
                group.DOKill();
                group.DOFade(0f, tweenSeconds);
            }

            조작잠금(false);
            if (GameServices.TryGet<UIStateService>(out var ui)) ui.NotifyClosed(this);
        }

        void 조작잠금(bool 잠글까)
        {
            _player?.Locomotion?.SetMovementLocked(잠글까);
            _player?.CameraRig?.SetLookLocked(잠글까);
        }

        void 즉시닫기()
        {
            _열림 = false;
            if (group == null) return;
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }
    }
}
