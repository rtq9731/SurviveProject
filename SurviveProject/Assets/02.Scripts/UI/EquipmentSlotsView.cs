using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Survive.Items;
using Survive.Localization;

namespace Survive.UI
{
    /// <summary>
    /// 소지품 화면 위쪽에 붙는 장비 자리들.
    ///
    /// <b>왜 런타임에 짓는가.</b> PanelInven 프리팹은 15칸 격자를
    /// <c>GridLayoutGroup</c>으로 깔고 있어서, 거기에 칸을 하나 더 붙이면
    /// 16번째 격자칸이 되어 버린다. 장비 자리는 격자 밖에 있어야 "일반 칸이
    /// 아니다"가 보인다. 프리팹을 다시 짜는 대신 패널 위쪽 빈 곳에 세운다
    /// (QuickSlotBar가 쓰는 것과 같은 방식이다).
    ///
    /// 표시만 한다. 끌어다 옮기는 조작은 두지 않는다 - 지금 여기 걸리는 것은
    /// 랜턴 하나고, 습득 길목(<see cref="Inventory.TryAdd"/>)이 알아서 앉힌다.
    /// </summary>
    [DisallowMultipleComponent]
    public class EquipmentSlotsView : MonoBehaviour
    {
        class Cell
        {
            public EquipmentSlotKind kind;
            public Image frame;
            public Image icon;
            public TMP_Text label;
        }

        static readonly Color EmptyColor = new Color(0.10f, 0.11f, 0.14f, 0.72f);
        static readonly Color FilledColor = new Color(0.20f, 0.19f, 0.14f, 0.92f);
        static readonly Color LabelColor = new Color(0.78f, 0.79f, 0.84f, 0.92f);

        readonly List<Cell> _cells = new List<Cell>();
        TMP_Text _title;

        /// <summary>패널 위쪽에 세운다. 이미 있으면 그것을 돌려준다.</summary>
        public static EquipmentSlotsView Build(RectTransform panel, Sprite frameSprite)
        {
            if (panel == null) return null;

            var existing = panel.GetComponentInChildren<EquipmentSlotsView>(true);
            if (existing != null) return existing;

            var go = new GameObject("EquipmentSlots", typeof(RectTransform));
            go.transform.SetParent(panel, false);
            var root = (RectTransform)go.transform;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0f, -18f);
            root.sizeDelta = new Vector2(panel.sizeDelta.x, 96f);

            var view = go.AddComponent<EquipmentSlotsView>();
            view.Compose(root, frameSprite);
            return view;
        }

        void Compose(RectTransform root, Sprite frameSprite)
        {
            const float cellSize = 62f;
            const float spacing = 10f;

            _title = MakeText(root, "Title", 17f, TextAlignmentOptions.Center);
            var trt = (RectTransform)_title.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = Vector2.zero;
            trt.sizeDelta = new Vector2(200f, 22f);

            var kinds = EquipmentSlots.AllKinds;
            float total = kinds.Length * cellSize + (kinds.Length - 1) * spacing;
            float startX = -total * 0.5f + cellSize * 0.5f;

            for (int i = 0; i < kinds.Length; i++)
            {
                var cellGo = new GameObject("Equip_" + kinds[i], typeof(RectTransform));
                cellGo.transform.SetParent(root, false);
                var crt = (RectTransform)cellGo.transform;
                crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 1f);
                crt.pivot = new Vector2(0.5f, 1f);
                crt.anchoredPosition = new Vector2(startX + i * (cellSize + spacing), -26f);
                crt.sizeDelta = new Vector2(cellSize, cellSize);

                var frame = cellGo.AddComponent<Image>();
                frame.sprite = frameSprite;
                frame.type = Image.Type.Sliced;
                frame.color = EmptyColor;
                frame.raycastTarget = false;

                var iconGo = new GameObject("Icon", typeof(RectTransform));
                iconGo.transform.SetParent(cellGo.transform, false);
                var irt = (RectTransform)iconGo.transform;
                irt.anchorMin = Vector2.zero;
                irt.anchorMax = Vector2.one;
                irt.offsetMin = new Vector2(8f, 8f);
                irt.offsetMax = new Vector2(-8f, -8f);
                var icon = iconGo.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.enabled = false;

                var label = MakeText(cellGo.transform, "Label", 12f, TextAlignmentOptions.Center);
                var lrt = (RectTransform)label.transform;
                lrt.anchorMin = new Vector2(0f, 0f);
                lrt.anchorMax = new Vector2(1f, 0f);
                lrt.pivot = new Vector2(0.5f, 1f);
                lrt.anchoredPosition = new Vector2(0f, -2f);
                lrt.sizeDelta = new Vector2(0f, 16f);

                _cells.Add(new Cell { kind = kinds[i], frame = frame, icon = icon, label = label });
            }

            ApplyStaticText();
        }

        static TMP_Text MakeText(Transform parent, string name, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.alignment = align;
            t.color = LabelColor;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>
        /// 자리 이름은 표에서 온다. 화면에 나가는 글자를 코드에 적을 수 없다.
        /// </summary>
        void ApplyStaticText()
        {
            if (_title != null) _title.text = Loc.T("Inv", "equip_title");
            foreach (var cell in _cells)
                cell.label.text = LabelKeyFor(cell.kind);
        }

        static string LabelKeyFor(EquipmentSlotKind kind)
        {
            switch (kind)
            {
                case EquipmentSlotKind.Light: return Loc.T("Inv", "equip_light");
                default: return string.Empty;
            }
        }

        public void Render(EquipmentSlots equipment)
        {
            foreach (var cell in _cells)
            {
                var item = equipment != null ? equipment.Get(cell.kind) : null;
                bool filled = item != null;

                cell.icon.enabled = filled && item.icon != null;
                if (cell.icon.enabled) cell.icon.sprite = item.icon;

                cell.frame.color = filled ? FilledColor : EmptyColor;

                // 이름은 걸린 물건이 있으면 그 물건 이름으로 바뀐다. 아이템 이름은
                // 반드시 DataText를 지난다 (SO의 displayName을 직접 읽으면 게이트가 잡는다).
                cell.label.text = filled ? DataText.Name(item) : LabelKeyFor(cell.kind);
            }
        }
    }
}
