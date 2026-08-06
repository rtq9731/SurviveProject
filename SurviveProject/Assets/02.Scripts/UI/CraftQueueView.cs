using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Survive.Crafting;
using Survive.Localization;

namespace Survive.UI
{
    /// <summary>
    /// 걸어 놓은 제작이 줄지어 선 가로 띠.
    ///
    /// 러스트의 대기열처럼 칸마다 만들 물건의 아이콘과 수량이 있고,
    /// 맨 앞 칸에만 남은 시간이 붙는다 — 뒤는 아직 시작도 안 했으니
    /// 시간을 적어 봐야 거짓말이다. 칸을 누르면 그 항목이 물러나고 재료가 돌아온다.
    ///
    /// 화면에 붙박이로 남는다. 소지품을 닫아도 <b>진행 중인 한 칸</b>은 보여야
    /// "걸어 놓고 돌아다닌다"가 성립한다 — 보이지 않는 것은 잊힌다.
    ///
    /// 새 패널을 만들지 않는다. 제작 패널이 이미 있는 자리(캔버스)에 런타임으로
    /// 붙는 줄 하나다 — 프리팹을 늘리면 UI 작업이 또 한 파일에서 부딪친다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CraftQueueView : MonoBehaviour
    {
        const float SlotSize = 56f;
        const float Spacing = 6f;

        public delegate void CancelHandler(int index);

        CraftQueue _queue;
        CancelHandler _onCancel;
        bool _expanded;

        readonly List<Slot> _slots = new List<Slot>();
        RectTransform _rect;
        CanvasGroup _group;

        class Slot
        {
            public GameObject go;
            public RectTransform rect;
            public Image frame;
            public Image icon;
            public TMP_Text count;
            public TMP_Text time;
            public Image fill;
            public Button button;
        }

        /// <summary>
        /// 캔버스에 줄을 하나 세운다. 이미 있으면 그것을 쓴다.
        /// 퀵슬롯 바(y 18~88) 바로 위에 앉는다.
        /// </summary>
        public static CraftQueueView Ensure(Transform canvasParent, TMP_FontAsset font)
        {
            if (canvasParent == null) return null;

            var existing = canvasParent.GetComponentInChildren<CraftQueueView>(true);
            if (existing != null) return existing;

            var go = new GameObject("CraftQueueStrip", typeof(RectTransform));
            go.transform.SetParent(canvasParent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 100f);
            rt.sizeDelta = new Vector2(SlotSize, SlotSize);

            var view = go.AddComponent<CraftQueueView>();
            view._font = font;
            return view;
        }

        TMP_FontAsset _font;

        void Awake()
        {
            _rect = (RectTransform)transform;
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
        }

        /// <summary>어느 줄을 보여 줄지 갈아 끼운다. 제작 화면이 열릴 때마다 부른다.</summary>
        public void Bind(CraftQueue queue, CancelHandler onCancel)
        {
            _queue = queue;
            _onCancel = onCancel;
        }

        /// <summary>
        /// 펼침: 제작 화면이 열려 있어 줄 전체가 보인다.
        /// 접힘: 화면을 닫아 진행 중인 한 칸만 남는다.
        /// </summary>
        public void SetExpanded(bool expanded) => _expanded = expanded;

        void LateUpdate()
        {
            int shown = _queue == null ? 0 : (_expanded ? _queue.Count : Mathf.Min(1, _queue.Count));

            if (shown == 0)
            {
                if (_group.alpha > 0f) _group.alpha = 0f;
                foreach (var s in _slots) if (s.go.activeSelf) s.go.SetActive(false);
                return;
            }

            _group.alpha = 1f;
            EnsureSlots(shown);

            float width = shown * SlotSize + (shown - 1) * Spacing;
            _rect.sizeDelta = new Vector2(width, SlotSize);

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                bool live = i < shown;
                if (slot.go.activeSelf != live) slot.go.SetActive(live);
                if (!live) continue;

                slot.rect.anchoredPosition = new Vector2(i * (SlotSize + Spacing), 0f);
                Render(slot, _queue.At(i), i == 0);
            }
        }

        void Render(Slot slot, CraftJob job, bool isFront)
        {
            if (job == null) return;

            var item = job.Recipe?.result?.item;
            if (slot.icon != null)
            {
                bool hasIcon = item != null && item.icon != null;
                slot.icon.enabled = hasIcon;
                if (hasIcon) slot.icon.sprite = item.icon;
            }

            if (slot.count != null)
            {
                bool many = job.Remaining > 1;
                slot.count.enabled = many;
                if (many) slot.count.text = Loc.F("UI", "quantity_amount", job.Remaining);
            }

            // 남은 시간은 맨 앞에만. 뒤 칸은 언제 시작할지 모른다.
            if (slot.time != null)
            {
                slot.time.enabled = isFront;
                if (isFront)
                {
                    slot.time.text = job.Stalled
                        ? Loc.T("UI", "queue_slot_full")
                        : CraftTimeText.Short(job.SecondsLeft);
                    slot.time.color = job.Stalled
                        ? new Color(1f, 0.62f, 0.42f)
                        : new Color(0.95f, 0.9f, 0.72f);
                }
            }

            if (slot.fill != null)
            {
                slot.fill.enabled = isFront;
                if (isFront) slot.fill.fillAmount = job.UnitProgress;
            }

            // 아이콘 없는 아이템도 있다. 그럴 땐 이름 첫 글자라도 보여 준다.
            // 이름은 반드시 DataText를 거친다 — 여기서 SO를 직접 읽으면 이 칸만
            // 로케일을 안 따라온다.
            if (slot.icon != null && !slot.icon.enabled && slot.count != null && item != null)
            {
                string initial = Abbrev(DataText.Name(item));
                slot.count.enabled = true;
                slot.count.text = job.Remaining > 1
                    ? Loc.F("UI", "queue_slot_initial_count", initial, job.Remaining)
                    : initial;
            }
        }

        static string Abbrev(string name) =>
            string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1);

        void EnsureSlots(int needed)
        {
            while (_slots.Count < needed) _slots.Add(BuildSlot(_slots.Count));
        }

        Slot BuildSlot(int index)
        {
            var go = new GameObject("QueueSlot_" + index, typeof(RectTransform));
            go.transform.SetParent(transform, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(SlotSize, SlotSize);

            var frame = go.AddComponent<Image>();
            UISkin.ApplyPanel(frame, new Color(0.12f, 0.14f, 0.18f, 0.88f));

            var fill = MakeChild<Image>(go.transform, "Fill", new Vector2(0f, 0f), new Vector2(1f, 1f));
            UISkin.ApplyPanel(fill, new Color(0.42f, 0.72f, 0.95f, 0.30f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            fill.raycastTarget = false;

            var icon = MakeChild<Image>(go.transform, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            var irt = (RectTransform)icon.transform;
            irt.sizeDelta = new Vector2(SlotSize - 16f, SlotSize - 16f);
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var count = MakeText(go.transform, "Count", TextAlignmentOptions.BottomRight, 15f);
            var time = MakeText(go.transform, "Time", TextAlignmentOptions.Top, 14f);
            ((RectTransform)time.transform).anchoredPosition = new Vector2(0f, 2f);

            var button = go.AddComponent<Button>();
            int captured = index;
            button.onClick.AddListener(() => Cancel(captured));

            return new Slot
            {
                go = go, rect = rt, frame = frame, icon = icon,
                count = count, time = time, fill = fill, button = button
            };
        }

        void Cancel(int index)
        {
            if (_queue == null || _onCancel == null) return;
            if (index < 0 || index >= _queue.Count) return;

            _onCancel(index);

            // 눌린 티가 나야 한다. 조용히 사라지면 잘못 눌렀는지 알 수 없다.
            if (index < _slots.Count)
            {
                var rt = _slots[index].rect;
                rt.DOKill();
                rt.localScale = Vector3.one;
                rt.DOPunchScale(Vector3.one * 0.2f, 0.2f, 8, 0.6f);
            }
        }

        T MakeChild<T>(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
            where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go.AddComponent<T>();
        }

        TMP_Text MakeText(Transform parent, string name, TextAlignmentOptions align, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(3f, 2f);
            rt.offsetMax = new Vector2(-3f, -2f);

            var txt = go.AddComponent<TextMeshProUGUI>();
            if (_font != null) txt.font = _font;
            txt.fontSize = size;
            txt.alignment = align;
            txt.color = Color.white;
            txt.raycastTarget = false;
            return txt;
        }

        void OnDestroy()
        {
            foreach (var s in _slots) s.rect?.DOKill();
        }
    }
}
