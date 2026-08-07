using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Survive.Core;
using Survive.Instruments;
using Survive.Items;
using Survive.Localization;
using Survive.Progression;
using Survive.UI;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// 레이더를 <b>진짜로 돌린다</b> (챕터 1 스펙 §8-2).
    ///
    /// EditMode(<c>RadarDetectionTests</c>)는 규칙만 본다 — 무엇이 잡히고 무엇이
    /// 안 잡히는가, 상태가 어떻게 옮겨 가는가. 규칙이 옳아도 껍데기가 그 규칙을
    /// 안 부르면 아무 일도 안 일어나고, 배터리가 실제로 줄지 않으면 "정보를 사는 값"이
    /// 화면에만 있는 이야기가 된다. 그래서 여기서는 <b>G를 누르고, 서서 기다리고,
    /// 통에 남은 배터리를 직접 센다.</b>
    ///
    /// 화면에 선 글자도 표가 내놓아야 할 문장과 글자 그대로 견준다 — 이름만 들어
    /// 있는지 보는 것으로는 껍데기가 코드에 박혀 있어도 초록불이 뜬다.
    /// </summary>
    public static class E2ERadar
    {
        const string 장소id = "a_islet";
        const string 청사진id = "bp_radar";

        static RadarItemSO _radar;
        static ItemDataSO _cell;

        static readonly List<GameObject> _planted = new List<GameObject>();
        static GameObject _trigger;

        static RadarController Radar => RadarController.Instance;
        static RadarScanView View => RadarScanView.Instance;

        public static IEnumerator FullRun()
        {
            yield return 무대를_세운다();
            yield return 표가_서_있다();
            yield return 걸면_배터리가_실제로_준다();
            yield return 움직이면_끊긴다();
            yield return 완주하면_결과가_화면에_뜬다();
            yield return 전원이_모자라면_걸리지도_않는다();
            yield return 셀을_끼우면_다시_걸린다();
            yield return 자리에_닿으면_청사진이_열린다();
            yield return 무대를_치운다();

            E2EHarness.Log("=== 레이더 완주 ===");
        }

        // ── 1. 무대 ──────────────────────────────────────────────

        static IEnumerator 무대를_세운다()
        {
            _planted.Clear();

            _radar = Resources.FindObjectsOfTypeAll<RadarItemSO>()
                              .FirstOrDefault(r => r != null && r.id == RadarController.ItemId);
            E2EHarness.Assert(_radar != null, "레이더 아이템 정의를 찾았다");

            yield return E2EHarness.WaitUntil(() => Radar != null,
                "레이더 조작부가 스스로 섰다 (RuntimeInitializeOnLoadMethod)", 5f);
            yield return E2EHarness.WaitUntil(() => View != null,
                "레이더 화면이 스스로 섰다", 5f);

            _cell = Resources.FindObjectsOfTypeAll<ItemDataSO>()
                             .FirstOrDefault(i => i != null && i.id == LanternController.BatteryCellId);
            E2EHarness.Assert(_cell != null, "배터리 셀 아이템 정의를 찾았다 (랜턴과 같은 물건이다)");

            var inv = E2EHarness.Player.Inventory.Inventory;
            if (!inv.Has(RadarController.ItemId, 1)) inv.TryAdd(_radar, 1);
            E2EHarness.Assert(Radar.HeldRadar() != null, "소지품에 레이더가 들어갔다");

            채운다();

            심는다();
            E2EHarness.Log($"  표식 {_planted.Count}개를 심었다 (섬·공동·낫·균열)");
            yield return null;
        }

        static void 채운다() => Radar.SetCharge(_radar.maxCharge);

        static int 셀수() => E2EHarness.Player.Inventory.Inventory.CountOf(LanternController.BatteryCellId);

        /// <summary>
        /// 잡힐 것 둘과 안 잡힐 것 둘을 나란히 놓는다.
        ///
        /// <b>낫과 균열도 똑같이 등록한다.</b> 안 잡히는 것을 아예 심지 않으면
        /// "안 잡힌다"를 증명한 것이 아니라 "물어보지 않았다"를 증명한 것이 된다.
        /// </summary>
        static void 심는다()
        {
            var 기준 = E2EHarness.Player.Transform.position;

            심기("E2E_Island", RadarContactKind.Island, 600f, 0f, 0f, 기준 + Vector3.forward * 400f);
            심기("E2E_Cavity", RadarContactKind.Cavity, 90f, 0f, 180f, 기준 + Vector3.right * 200f);
            심기("E2E_Scythe", RadarContactKind.Creature, 2.4f, 6f, 0f, 기준 + Vector3.forward * 15f);
            심기("E2E_Fissure", RadarContactKind.Fissure, 0.6f, 0f, 0f, 기준 + Vector3.forward * 3f);
        }

        static void 심기(string name, RadarContactKind kind, float size, float speed,
                         float depth, Vector3 at)
        {
            var go = new GameObject(name);
            go.transform.position = at;

            var mark = go.AddComponent<RadarLandmark>();
            mark.ContactId = name;
            mark.ContactKind = kind;
            mark.ContactSizeMeters = size;
            mark.ContactDepthMeters = depth;
            mark.FixedSpeedMps = speed;

            // OnEnable이 이미 돌았으므로 값을 세운 뒤 다시 등록시킨다.
            go.SetActive(false);
            go.SetActive(true);

            _planted.Add(go);
        }

        // ── 2. 표 ────────────────────────────────────────────────

        static IEnumerator 표가_서_있다()
        {
            yield return E2EHarness.WaitUntil(() => Loc.IsLoaded, "번역 표가 실렸다", 8f);
            E2EHarness.Assert(Loc.Catalog.Problems.Count == 0,
                $"표에 건전성 문제가 없다 ({Loc.Catalog.Problems.Count}건)");

            foreach (var key in new[] { "scanning", "result_title", "reading", "cancel_moved" })
            {
                var k = new LocKey(RadarText.Category, key);
                E2EHarness.Assert(Loc.Catalog.Contains(k),
                    $"표에 {k}가 있다 — 없으면 문장이 코드에 박혀 있다는 뜻이다");
                E2EHarness.Assert(Loc.Catalog.TryGet("en", k, out var 영문) &&
                                  !string.IsNullOrEmpty(영문),
                    $"{k}의 en 칸이 차 있다");
            }
        }

        // ── 3. 배터리가 실제로 준다 ──────────────────────────────

        static IEnumerator 걸면_배터리가_실제로_준다()
        {
            채운다();
            float 처음 = Radar.Charge;

            yield return E2EHarness.TapKey(Key.G);
            yield return null;

            E2EHarness.Assert(Radar.IsScanning, "G를 누르니 관측이 걸렸다");
            E2EHarness.Assert(View.IsVisible, "관측 중 화면이 떴다");
            E2EHarness.AssertEqual(View.TitleText, Loc.T(RadarText.Category, "scanning"),
                "관측 중 화면의 글자가 표에서 나왔다");

            yield return new WaitForSeconds(2f);

            float 준것 = 처음 - Radar.Charge;
            E2EHarness.Log($"  2초 동안 배터리 {처음:F1} -> {Radar.Charge:F1} (준 것 {준것:F1})");

            E2EHarness.Assert(준것 > 0f, "서 있는 동안 배터리가 실제로 줄었다");
            E2EHarness.Assert(Mathf.Abs(준것 - _radar.batteryPerSecond * 2f) < 2f,
                $"준 배터리가 초당 소모 × 흐른 시간과 맞는다 (기대 {_radar.batteryPerSecond * 2f:F1}, 실제 {준것:F1})");
            E2EHarness.Assert(Radar.Scan.Progress > 0.2f,
                $"진행률이 올라갔다 ({Radar.Scan.Progress:P0})");
        }

        // ── 4. 움직이면 끊긴다 ───────────────────────────────────

        static IEnumerator 움직이면_끊긴다()
        {
            E2EHarness.Assert(Radar.IsScanning, "아직 관측 중이다 (앞 단계에서 이어진다)");
            float 끊기기전 = Radar.Charge;

            yield return E2EHarness.PressKey(Key.W);
            yield return new WaitForSeconds(0.6f);
            yield return E2EHarness.ReleaseKey(Key.W);
            yield return null;

            E2EHarness.Assert(!Radar.IsScanning, "움직였더니 관측이 끊겼다");
            E2EHarness.AssertEqual(Radar.Scan.CancelReason, RadarCancelReason.Moved,
                "끊긴 이유가 '움직였다'다");
            E2EHarness.AssertEqual(View.TitleText, Loc.T(RadarText.Category, "cancel_moved"),
                "끊김을 알리는 글자가 표에서 나왔다");

            // 값은 돌아오지 않는다. 물러도 값을 돌려주면 서 있는 것에 무게가 없어진다.
            E2EHarness.Assert(Radar.Charge <= 끊기기전 + 0.01f,
                $"끊겼다고 배터리가 되돌아오지 않았다 ({끊기기전:F1} -> {Radar.Charge:F1})");
            E2EHarness.Assert(Radar.Scan.Drawn > 0f,
                $"끊길 때까지 쓴 배터리가 남아 있다 ({Radar.Scan.Drawn:F1})");

            Radar.Dismiss();
            yield return null;
        }

        // ── 5. 완주하면 결과가 뜬다 ──────────────────────────────

        static IEnumerator 완주하면_결과가_화면에_뜬다()
        {
            채운다();
            float 처음 = Radar.Charge;

            yield return E2EHarness.TapKey(Key.G);
            yield return null;
            E2EHarness.Assert(Radar.IsScanning, "관측이 다시 걸렸다");

            yield return E2EHarness.WaitUntil(
                () => Radar.Scan != null && Radar.Scan.State == RadarScanState.Complete,
                "서서 기다리니 관측이 끝났다", _radar.scanSeconds + 4f);
            yield return null;

            float 준것 = 처음 - Radar.Charge;
            E2EHarness.Log($"  완주에 배터리 {준것:F1}을 썼다 (에셋의 한 번 값 {_radar.ScanCost:F1})");
            E2EHarness.Assert(Mathf.Abs(준것 - _radar.ScanCost) < 2f,
                "한 번 완주하는 값이 에셋이 정한 값과 맞는다");

            // ── 무엇이 잡혔는가 ──
            var 잡힌것 = Radar.Readings.Select(c => c.kind).ToArray();
            E2EHarness.Log("  잡힌 것: " + string.Join(", ", 잡힌것));

            E2EHarness.Assert(잡힌것.Contains(RadarContactKind.Island), "다른 섬이 잡혔다");
            E2EHarness.Assert(잡힌것.Contains(RadarContactKind.Cavity), "바다 아래 공동이 잡혔다");
            E2EHarness.Assert(!잡힌것.Contains(RadarContactKind.Creature),
                "낫은 잡히지 않았다 — 작고 빨라서 뭉개지고 번진다");
            E2EHarness.Assert(!잡힌것.Contains(RadarContactKind.Fissure),
                "발밑 균열은 잡히지 않았다 — 해상도보다 작다");
            E2EHarness.AssertEqual(Radar.Readings.Count, 2, "잡힌 것은 둘뿐이다");

            // ── 화면에 그대로 적혔는가 ──
            E2EHarness.Assert(View.IsVisible, "결과 화면이 떴다");
            E2EHarness.AssertEqual(View.TitleText, Loc.T(RadarText.Category, "result_title"),
                "결과 화면의 제목이 표에서 나왔다");

            string 기대 = string.Join("\n", Radar.Readings.Select(RadarText.Reading));
            E2EHarness.AssertEqual(View.BodyText, 기대, "결과 줄이 표가 내놓는 문장과 글자 그대로 같다");
            E2EHarness.Log("  화면: " + View.BodyText.Replace("\n", " / "));

            // 종류 이름이 열쇠 그대로 찍히지 않았는지 본다.
            foreach (var c in Radar.Readings)
                E2EHarness.Assert(!View.BodyText.Contains("kind_"),
                    $"종류 이름이 열쇠 그대로 찍혔다 ({c.id})");
        }

        // ── 6. 전원이 모자라면 걸리지 않는다 ─────────────────────

        static IEnumerator 전원이_모자라면_걸리지도_않는다()
        {
            Radar.Dismiss();
            yield return null;

            // 셀도 없고 전하도 한 번 값에 못 미치게 만든다.
            var inv = E2EHarness.Player.Inventory.Inventory;
            int 있던셀 = 셀수();
            if (있던셀 > 0) inv.TryRemove(LanternController.BatteryCellId, 있던셀);

            Radar.SetCharge(_radar.ScanCost - 1f);
            E2EHarness.Log($"  전하를 {Radar.Charge:F1}로 낮추고 셀을 비웠다 (한 번 값 {_radar.ScanCost:F1})");

            float 처음 = Radar.Charge;
            yield return E2EHarness.TapKey(Key.G);
            yield return null;
            yield return null;

            E2EHarness.Assert(!Radar.IsScanning,
                "값을 치를 수 없으면 걸리지 않는다 — 반쯤 하다 꺼지면 배터리만 없어진다");
            E2EHarness.Assert(Mathf.Abs(처음 - Radar.Charge) < 0.01f,
                "걸리지 않았으니 전하도 안 줄었다");
            E2EHarness.Assert(!View.IsVisible, "걸리지 않았으니 화면도 안 떴다");
        }

        // ── 6-2. 셀을 끼우면 다시 걸린다 ─────────────────────────

        static IEnumerator 셀을_끼우면_다시_걸린다()
        {
            // 랜턴과 같은 물건을 먹는다. 관측 한 번은 곧 랜턴을 그만큼 못 켠다는 뜻이고,
            // 그것이 "정보를 사는 값"의 실체다.
            var inv = E2EHarness.Player.Inventory.Inventory;
            inv.TryAdd(_cell, 1);

            int 전 = 셀수();
            E2EHarness.Assert(전 >= 1, "배터리 셀을 하나 챙겼다");

            yield return E2EHarness.TapKey(Key.G);
            yield return null;

            E2EHarness.Assert(Radar.IsScanning, "셀을 챙기니 관측이 걸렸다");
            E2EHarness.AssertEqual(Radar.LastCellsInserted, 1, "셀 하나가 끼워졌다");
            E2EHarness.AssertEqual(셀수(), 전 - 1, "소지품에서 셀이 실제로 하나 사라졌다");
            E2EHarness.Log($"  셀 {전} -> {셀수()}, 전하 {Radar.Charge:F1}");

            Radar.Abort();
            yield return null;
            E2EHarness.Assert(!Radar.IsScanning, "껐다");
            E2EHarness.AssertEqual(셀수(), 전 - 1, "껐다고 셀이 돌아오지 않는다");
        }

        // ── 7. 자리에 닿으면 청사진이 열린다 ─────────────────────

        static IEnumerator 자리에_닿으면_청사진이_열린다()
        {
            var service = UnlockService.Instance;
            E2EHarness.Assert(service != null, "해금 서비스가 서 있다");
            E2EHarness.Assert(service.Book != null, "발견 목록이 실렸다");

            // A-3(내부 id는 여전히 a_islet)은 아직 세계에 없다(스펙 §16 — 배치는 사람의 몫).
            // 사람이 심을 그 물건을 여기서 임시로 세워 배선이 살아 있는지만 본다.
            _trigger = new GameObject("E2E_IsletTrigger");

            // Collider를 먼저 단다. [RequireComponent(typeof(Collider))]의 Collider는
            // 추상형이라 런타임 AddComponent가 대신 달아 주지 못하고 null을 돌려준다.
            var box = _trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = Vector3.one * 4f;

            var t = _trigger.AddComponent<LocationDiscoveryTrigger>();
            t.LocationId = 장소id;

            int 말한줄 = service.LinesSpoken;
            bool 이미겪음 = service.Ledger.IsUnlocked(청사진id);

            bool 처음 = t.Fire();
            yield return null;

            E2EHarness.Assert(service.Ledger.IsUnlocked(청사진id),
                "자리에 닿으니 레이더 청사진이 열렸다");

            if (이미겪음)
            {
                E2EHarness.Log("  (앞 판에서 이미 겪은 자리라 대사는 다시 울리지 않는다)");
                E2EHarness.Assert(!처음, "이미 겪은 자리는 두 번째부터 조용하다");
            }
            else
            {
                E2EHarness.Assert(처음, "첫 도달이 발견으로 잡혔다");
                yield return E2EHarness.WaitUntil(() => service.LinesSpoken > 말한줄,
                    "우주복 AI가 한 줄 말했다", 4f);
                E2EHarness.Log("  AI: " + service.LastLine);

                E2EHarness.Assert(service.LastLine.Contains("권장"),
                    "분석 보고에서 제안으로 넘어가는 대사다");
            }

            // 목록이 실제로 자란다 — 말만 하고 목록이 그대로면 그것은 거짓말이다.
            var book = Resources.FindObjectsOfTypeAll<Survive.Crafting.RecipeBookSO>().FirstOrDefault();
            E2EHarness.Assert(book != null, "제작법 목록을 찾았다");

            var 레시피 = book.recipes.FirstOrDefault(r => r != null && r.id == RadarController.ItemId);
            E2EHarness.Assert(레시피 != null, "레이더 제작법이 목록에 있다");
            E2EHarness.Assert(MenuListing.ShouldList(레시피, 레시피.requiredStation, service.Ledger),
                "청사진이 열리니 레이더가 제작 목록에 실린다");
            E2EHarness.Assert(!MenuListing.ShouldList(레시피, 레시피.requiredStation, new UnlockLedger()),
                "아무것도 모르는 판에서는 실리지 않는다");
        }

        // ── 8. 치운다 ────────────────────────────────────────────

        static IEnumerator 무대를_치운다()
        {
            Radar.Dismiss();

            foreach (var go in _planted)
                if (go != null) Object.Destroy(go);
            _planted.Clear();

            if (_trigger != null) Object.Destroy(_trigger);
            _trigger = null;

            // 다음 시나리오가 앞 판의 레이더를 물려받지 않게 한다.
            E2EHarness.Player.Inventory.Inventory.TryRemove(RadarController.ItemId, 1);
            Radar.SetCharge(0f);

            yield return null;
            E2EHarness.Assert(RadarContactRegistry.Count == 0,
                $"심은 표식이 전부 물러났다 (남은 것 {RadarContactRegistry.Count}개)");
        }
    }
}
