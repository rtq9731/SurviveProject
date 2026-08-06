using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Survive.Items;
using Survive.Localization;
using Survive.Progression;
using Survive.UI;

namespace Survive.Testing
{
    /// <summary>
    /// 아이템 설명 쪽지 — 에셋에 적혀만 있던 문장이 <b>화면에 닿는가</b>.
    ///
    /// 아이템 23종 전부에 <c>description</c>이 채워져 있었는데 그 필드를 읽는 코드가
    /// 한 줄도 없었다. 자리를 잡는 규칙 자체는 EditMode(TooltipPlacementTests)가
    /// 지키므로, 여기서 보는 것은 그 규칙이 진짜 캔버스·진짜 포인터에 닿는지다:
    /// 커서 아래에 정말 그 칸이 있는가(레이캐스트로 확인한다), 뜬 쪽지에 적힌 문장이
    /// <b>그 아이템의 description 문자열과 같은가</b>, 커서를 떼면 사라지는가,
    /// 그리고 빈 칸에서는 아예 뜨지 않는가.
    /// </summary>
    public static class E2ETooltip
    {
        /// <summary>쪽지를 확인할 물건. 설명문이 있고 발견 기록도 걸려 있다.</summary>
        const string ProbeItem = "machine_part";

        static Inventory Inv => E2EHarness.Player.Inventory.Inventory;
        static ItemTooltipView Tip => ItemTooltipView.Instance;

        public static IEnumerator FullRun()
        {
            E2EHarness.ClearLog();
            E2EHarness.Log("=== 아이템 설명 쪽지 ===");

            E2EHarness.Assert(EventSystem.current != null, "EventSystem이 있다 (포인터가 흐른다)");
            E2EHarness.Assert(Tip != null, "쪽지가 스스로 서 있다 (씬 배선 없음)");

            yield return 소지품_칸에서_설명문이_읽힌다();
            yield return 커서를_떼면_사라진다();
            yield return 빈_칸에서는_뜨지_않는다();
            yield return 제작_목록은_결과물과_재료를_읽어_준다();
            yield return 도감의_물질_줄도_같은_쪽지를_쓴다();

            E2EHarness.Log("=== 아이템 설명 쪽지 통과 ===");
        }

        // ── 1. 소지품 ────────────────────────────────────────────

        static IEnumerator 소지품_칸에서_설명문이_읽힌다()
        {
            E2EHarness.Log("[1] 소지품을 열고 물건이 든 칸에 커서를 올린다");

            var probe = Give(ProbeItem);

            yield return OpenInventory();

            // 커서가 보이지 않으면 사람은 애초에 칸을 짚을 수 없다.
            E2EHarness.Assert(Cursor.visible && Cursor.lockState == CursorLockMode.None,
                              "소지품이 열린 동안 커서가 풀려 있다");

            var slot = SlotOf(probe);
            E2EHarness.Assert(slot != null, $"{probe.displayName}이 든 칸을 찾았다");

            yield return Hover(slot.gameObject);

            E2EHarness.Assert(Tip.IsVisible, "쪽지가 떴다");
            E2EHarness.AssertEqual(Tip.Item, probe, "쪽지가 설명하는 물건이 그 칸의 물건이다");

            E2EHarness.AssertEqual(Text("Title"), probe.displayName, "이름이 적힌다");
            E2EHarness.AssertEqual(Text("Body"), probe.description.Trim(),
                                   "에셋의 description이 <b>그대로</b> 화면에 떴다");
            E2EHarness.Log($"  쪽지 본문: {Text("Body")}");
            E2EHarness.Log($"  쪽지 아랫줄: {Text("Meta")}");

            E2EHarness.Assert(!Text("Body").Contains("—") && !Text("Meta").Contains("—"),
                              "본문 글꼴에 없는 줄표가 화면에 나가지 않는다");

            var r = Tip.ScreenRect;
            E2EHarness.Assert(r.xMin >= 0f && r.yMin >= 0f &&
                              r.xMax <= Screen.width && r.yMax <= Screen.height,
                              $"쪽지가 화면 안에 있다 ({r})");
            E2EHarness.Assert(!r.Contains(_cursor),
                              $"쪽지가 커서를 덮지 않는다 (커서 {_cursor}, 쪽지 {r})");
        }

        // ── 2. 커서를 뗀다 ───────────────────────────────────────

        static IEnumerator 커서를_떼면_사라진다()
        {
            E2EHarness.Log("[2] 커서를 뗀다");

            E2EHarness.Assert(Tip.IsVisible, "아직 떠 있다");
            yield return Unhover();

            E2EHarness.Assert(!Tip.IsVisible, "커서를 떼자 사라졌다 (ESC로 닫는 물건이 아니다)");
            E2EHarness.Assert(Tip.Item == null, "무엇을 설명하던 중이었는지도 지운다");
        }

        // ── 3. 빈 칸 ─────────────────────────────────────────────

        static IEnumerator 빈_칸에서는_뜨지_않는다()
        {
            E2EHarness.Log("[3] 빈 칸에 커서를 올린다");

            var empty = Object.FindObjectsByType<InventorySlotView>(FindObjectsInactive.Exclude)
                              .FirstOrDefault(s => s.Shown == null || s.Shown.IsEmpty);
            E2EHarness.Assert(empty != null, "빈 칸이 있다");

            yield return Hover(empty.gameObject);

            E2EHarness.Assert(!Tip.IsVisible, "빈 칸에는 쪽지가 뜨지 않는다");
            yield return Unhover();
        }

        // ── 4. 제작 목록 ─────────────────────────────────────────

        static IEnumerator 제작_목록은_결과물과_재료를_읽어_준다()
        {
            E2EHarness.Log("[4] 손 제작 목록의 줄에 커서를 올린다");

            var crafting = Object.FindObjectsByType<CraftingUI>(FindObjectsInactive.Include)
                                 .FirstOrDefault();
            E2EHarness.Assert(crafting != null, "제작 목록 화면이 있다");
            E2EHarness.Assert(crafting.IsOpen, "소지품과 함께 손 제작 목록이 열려 있다");

            // 열려 있는 줄 가운데 설명할 결과물이 붙은 것을 고른다.
            // 모르는 레시피는 줄 자체가 꺼져 있어 여기 잡히지 않는다.
            var row = Object.FindObjectsByType<ItemTooltipTrigger>(FindObjectsInactive.Exclude)
                            .FirstOrDefault(t => t.gameObject.name.StartsWith("Row_") &&
                                                 t.Resolve() != null);
            E2EHarness.Assert(row != null, "결과물을 설명할 수 있는 제작 줄이 있다");

            var item = row.Resolve();
            E2EHarness.Log($"  줄: {row.gameObject.name} -> {item.displayName}");

            yield return Hover(row.gameObject);

            E2EHarness.Assert(Tip.IsVisible, "제작 줄에서도 쪽지가 뜬다");
            E2EHarness.AssertEqual(Tip.Item, item, "결과물을 설명한다");
            E2EHarness.AssertEqual(Text("Body"), item.description.Trim(),
                                   "제작 목록에서도 같은 설명문이 읽힌다");

            string detail = Text("Detail");
            E2EHarness.Log($"  재료 줄: {detail}");

            // 예전에는 이 줄이 "재료 …"로 시작하는지만 봤다. 지금 이 문장은 표의
            // 한 칸(UI/ingredients_line)에서 통째로 나오고 <b>어순은 언어마다 다르다</b> —
            // 첫 글자로 판정하면 번역가가 순서를 바꾸는 순간 검증이 무너진다.
            // 물어야 할 것은 "무엇이 몇 개 드는지가 적혀 있는가" 하나다.
            E2EHarness.Assert(!string.IsNullOrWhiteSpace(detail) &&
                              detail != Loc.T("UI", "no_ingredients") &&
                              detail.Any(char.IsDigit),
                              $"재료가 드는 수와 함께 적힌다 — \"{detail}\"");

            yield return Unhover();
            E2EHarness.Assert(!Tip.IsVisible, "제작 줄에서도 커서를 떼면 사라진다");
        }

        // ── 5. 도감 ──────────────────────────────────────────────

        static IEnumerator 도감의_물질_줄도_같은_쪽지를_쓴다()
        {
            E2EHarness.Log("[5] 분석 기록의 물질 줄에 커서를 올린다");

            var codex = CodexUI.Instance;
            E2EHarness.Assert(codex != null, "분석 기록 화면이 있다");

            var probe = Give(ProbeItem);   // 발견 원장이 열려야 이름이 뜬다
            yield return E2EHarness.TapKey(Key.J);
            yield return E2EHarness.WaitUntil(() => codex.IsOpen, "J로 분석 기록이 열렸다", 3f);

            codex.ShowSection(CodexSection.Discovery);
            yield return null;
            yield return null;

            var row = codex.GetComponentsInChildren<ItemTooltipTrigger>(true)
                           .FirstOrDefault(t => t.Resolve() == probe);
            E2EHarness.Assert(row != null, $"{probe.displayName} 줄에 쪽지가 붙어 있다");

            yield return Hover(row.gameObject);

            E2EHarness.Assert(Tip.IsVisible, "도감에서도 쪽지가 뜬다");
            E2EHarness.AssertEqual(Text("Body"), probe.description.Trim(),
                                   "도감의 오른쪽 설명(AI의 말)과 별개로 물질 자신의 설명문이 읽힌다");

            // 오른쪽 설명 칸은 여전히 AI가 한 말이다. 쪽지를 붙이면서 그 계약을 깨지 않았다.
            var detail = codex.GetComponentsInChildren<TMP_Text>(true)
                              .FirstOrDefault(t => t.gameObject.name == "DetailBody");
            E2EHarness.Assert(detail != null, "도감 설명 칸이 있다");
            E2EHarness.Assert(detail.text != probe.description.Trim(),
                              "도감 설명 칸을 아이템 설명문으로 덮어쓰지 않았다");

            yield return Unhover();
            E2EHarness.Assert(!Tip.IsVisible, "도감에서도 커서를 떼면 사라진다");

            yield return E2EHarness.TapKey(Key.J);
            yield return E2EHarness.WaitUntil(() => !codex.IsOpen, "J로 다시 닫는다", 3f);
        }

        // ── 도우미 ───────────────────────────────────────────────

        static ItemDataSO Give(string id)
        {
            var db = E2EHarness.Player.Inventory.Database;
            var item = db != null ? db.GetById(id) : null;
            E2EHarness.Assert(item != null, $"{id} 정의가 있다");
            E2EHarness.Assert(!string.IsNullOrWhiteSpace(item.description),
                              $"{id}에 설명문이 적혀 있다");

            if (Inv.CountOf(id) <= 0) Inv.TryAdd(item, 1);
            return item;
        }

        static IEnumerator OpenInventory()
        {
            var inventory = Object.FindObjectsByType<InventoryUI>(FindObjectsInactive.Include)
                                  .FirstOrDefault();
            E2EHarness.Assert(inventory != null, "소지품 화면이 있다");

            if (!inventory.IsOpen) yield return E2EHarness.TapKey(Key.Tab);
            yield return E2EHarness.WaitUntil(() => inventory.IsOpen, "Tab으로 소지품이 열렸다", 3f);

            // 열리는 트윈이 끝나야 칸이 제자리에 온다. 자리를 재서 커서를 올릴 것이라
            // 움직이는 도중에 재면 엉뚱한 칸을 짚는다.
            float t = 0f;
            while (t < 0.4f) { t += Time.deltaTime; yield return null; }
        }

        static InventorySlotView SlotOf(ItemDataSO item) =>
            Object.FindObjectsByType<InventorySlotView>(FindObjectsInactive.Exclude)
                  .FirstOrDefault(s => s.Shown != null && !s.Shown.IsEmpty && s.Shown.item == item);

        /// <summary>지금 쪽지에 실제로 찍혀 있는 글자. 캐시한 문자열이 아니라 TMP에서 읽는다.</summary>
        static string Text(string name)
        {
            if (Tip == null) return "";

            var label = Tip.GetComponentsInChildren<TMP_Text>(true)
                           .FirstOrDefault(t => t.gameObject.name == name);
            if (label == null || !label.gameObject.activeInHierarchy) return "";
            return label.text;
        }

        static Vector2 _cursor;
        static GameObject _hovered;

        /// <summary>
        /// 그 칸 한가운데에 커서를 올린다.
        ///
        /// 손으로 이벤트를 쏘기 전에 <b>레이캐스트로 확인한다</b> — 그 자리에 정말
        /// 그 칸이 있고 포인터가 닿는지(raycastTarget이 켜져 있고 CanvasGroup이
        /// 막지 않는지)를 건너뛰면, 실제로는 아무도 올릴 수 없는 칸을 통과시키게 된다.
        /// </summary>
        static IEnumerator Hover(GameObject target)
        {
            var rect = (RectTransform)target.transform;
            var canvas = target.GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            _cursor = RectTransformUtility.WorldToScreenPoint(
                cam, rect.TransformPoint(rect.rect.center));

            var pointer = new PointerEventData(EventSystem.current) { position = _cursor };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);

            E2EHarness.Assert(hits.Count > 0, $"커서 자리 {_cursor}에 UI가 있다");

            var top = hits[0].gameObject;
            E2EHarness.Assert(top.transform == target.transform ||
                              top.transform.IsChildOf(target.transform),
                              $"커서 아래에 있는 것이 그 칸이다 (실제로는 {top.name})");

            ExecuteEvents.ExecuteHierarchy(top, pointer, ExecuteEvents.pointerEnterHandler);
            _hovered = top;

            yield return null;
        }

        static IEnumerator Unhover()
        {
            if (_hovered != null)
            {
                var pointer = new PointerEventData(EventSystem.current) { position = _cursor };
                ExecuteEvents.ExecuteHierarchy(_hovered, pointer, ExecuteEvents.pointerExitHandler);
                _hovered = null;
            }
            yield return null;
        }
    }
}
