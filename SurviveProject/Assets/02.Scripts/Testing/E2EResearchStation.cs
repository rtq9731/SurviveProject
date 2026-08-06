using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Survive.Building;
using Survive.Crafting;
using Survive.Items;
using Survive.Progression;
using Survive.UI;

namespace Survive.Testing
{
    /// <summary>
    /// 백로그 38 — 연구대. <b>알아내는 일에도 대가와 시간이 든다.</b>
    ///
    /// 채널 1(첫 습득)은 공짜였다. 손에 쥐는 순간 AI가 읽어 주고 문이 열렸다.
    /// 진행 장비는 그렇게 열리지 않는다 — 낫의 영역에서 유물을 주워 오고,
    /// 거점에 연구대를 세우고, 스크랩을 태우고, 기다려야 한다.
    ///
    /// 순수 규칙은 <c>ResearchServiceTests</c>가, 데이터의 사슬은
    /// <c>ResearchWiringTests</c>가 Unity 없이 본다. 여기서 보는 것은 <b>배선</b>이다:
    /// 실제로 지어지는가, UI 클릭이 정말 줄로 가는가, 시간이 실제로 흐르는가,
    /// 끝났을 때 <b>정말로 잠겨 있던 것이 열리는가</b>.
    /// </summary>
    public static class E2EResearchStation
    {
        const string 연구대 = "research_bench";
        const string 제작대 = "bench";
        const string 막 = "relic_membrane";
        const string 핵 = "relic_core";
        const string 막연구 = "res_surface_walker";
        const string 핵연구 = "res_breach_pod";
        const string 돌파설계 = "bp_breach_pod";
        const string 보행설계 = "bp_surface_walker";
        const string 돌파정 = "breach_pod";

        static Inventory Inv => E2EHarness.Player.Inventory.Inventory;
        static UnlockLedger Ledger => UnlockService.Instance.Ledger;

        static CraftingUI UI => Object.FindAnyObjectByType<CraftingUI>(FindObjectsInactive.Include);

        static BuildPlacer Placer =>
            Object.FindAnyObjectByType<BuildPlacer>(FindObjectsInactive.Include);

        static ResearchStation _station;
        static CraftingBench _bench;

        public static IEnumerator FullRun()
        {
            yield return Prepare();

            yield return 연구대를_짓는다();
            yield return 돌파정은_아직_잠겨_있다();
            yield return 가져_본_적_없는_것은_목록에_없다();
            yield return 소재가_없으면_분석할_수_없다();
            yield return 스크랩이_모자라면_거절한다();
            yield return 물리면_유물과_스크랩이_전부_돌아온다();
            yield return 시간이_지나야_알게_된다();
            yield return 잠겼던_돌파정을_만들_수_있게_된다();

            yield return 치운다();
            E2EHarness.Log("=== 연구대 완주 ===");
        }

        // ── 준비 ────────────────────────────────────────────────

        static IEnumerator Prepare()
        {
            var dir = Object.FindAnyObjectByType<ChapterDirector>(FindObjectsInactive.Exclude);
            yield return E2EHarness.WaitUntil(() => dir != null && dir.Current != null,
                                              "챕터가 시작된다", 8f);

            yield return E2EHarness.WaitUntil(() => UI != null, "제작 UI가 있다", 8f);
            yield return E2EHarness.WaitUntil(() => UnlockService.Instance != null,
                                              "해금 서비스가 스스로 서 있다", 8f);
            yield return E2EHarness.WaitUntil(() => Placer != null, "BuildPlacer가 준비된다", 8f);

            var book = Resources.Load<ResearchBookSO>("ResearchBook");
            E2EHarness.Assert(book != null, "Resources/ResearchBook 연구 목록을 읽었다");
            E2EHarness.Assert(book != null && book.energyItem != null,
                              "연구대가 태울 것(스크랩)이 지정돼 있다");

            // 앞선 시나리오가 남긴 앎을 지운다. "잠겨 있던 것이 열린다"를 보려면
            // 잠긴 상태에서 시작해야 한다.
            Ledger.Clear();
            앞선것을_치운다();
            yield return null;

            // 부품을 손에 넣는 것은 채널 1이다 — 연구대를 지을 줄 알게 되는 길목이
            // 바로 이것이고(요구 청사진 bp_salvaged_mechanism), 여기서 그 사슬도 함께 지난다.
            var db = E2EHarness.Player.Inventory.Database;
            준다("machine_part", 40);
            E2EHarness.Assert(Ledger.IsUnlocked("bp_salvaged_mechanism"),
                              "부품을 쥐자 '부품 재조립'이 열렸다 — 연구대는 그 뒤에 선다");

            // 연구대·제작대를 짓고 돌파정을 만들 재료를 채운다. 목록을 손으로 적으면
            // 비용이 바뀔 때마다 낡으므로 카탈로그가 요구하는 것을 그대로 채운다.
            var needed = new HashSet<string> { "scrap", "machine_part" };
            var placer = Placer;
            foreach (var id in new[] { 연구대, 제작대 })
            {
                placer.SelectById(id);
                var cost = placer.Selected?.cost;
                E2EHarness.Assert(cost != null, $"건축물 정의를 찾았다: {id}");
                if (cost == null) continue;
                foreach (var c in cost) if (c?.item != null) needed.Add(c.item.id);
            }
            placer.Cancel();

            var sub = 레시피(돌파정);
            E2EHarness.Assert(sub != null, "돌파정 레시피가 있다");
            if (sub?.ingredients != null)
                foreach (var need in sub.ingredients) if (need?.item != null) needed.Add(need.item.id);

            foreach (var id in needed)
            {
                E2EHarness.Assert(db.GetById(id) != null, $"아이템 정의를 찾았다: {id}");
                준다(id, 60 - Inv.CountOf(id));
            }

            E2EHarness.Log("  재료 주입: " +
                string.Join(", ", needed.Select(id => $"{id} {Inv.CountOf(id)}")));

            // 유물을 어떻게 손에 넣는가는 여기서 볼 것이 아니다(그쪽은
            // E2ERelicSupply가 낫을 세워 놓고 기다려 실제로 줍는다). 이 시나리오는
            // 연구대 자체를 보므로 유물을 직접 쥐여 주고 "쥔 다음"부터를 본다.
            E2EHarness.AssertEqual(Inv.CountOf(막), 0, "낫의 막을 아직 갖고 있지 않다");
            E2EHarness.AssertEqual(Inv.CountOf(핵), 0, "낫의 핵을 아직 갖고 있지 않다");
        }

        /// <summary>
        /// 앞선 시나리오가 손에 남긴 것을 턴다.
        ///
        /// 진행 장비만 털던 자리였는데 <b>연구 소재도 전부</b> 털게 됐다. 이제 연구
        /// 목록은 "가져 본 적 있는가"로 걸리고(<see cref="HeldRecord"/>), 손에 남은
        /// 것은 매 프레임 다시 기록으로 찍힌다. 앞선 시나리오가 낫의 부품을 남겨
        /// 두었으면 원장을 비워도 그 줄이 되살아나, "처음 열면 아무것도 없다"를
        /// 볼 수 없게 된다.
        /// </summary>
        static void 앞선것을_치운다()
        {
            var ids = new HashSet<string> { 막, 핵, 돌파정, "surface_walker" };

            var book = Resources.Load<ResearchBookSO>("ResearchBook");
            if (book?.entries != null)
            {
                foreach (var e in book.entries)
                {
                    if (e?.materials == null) continue;
                    foreach (var need in e.materials)
                        if (need?.item != null) ids.Add(need.item.id);
                }
            }

            foreach (var id in ids)
            {
                int n = Inv.CountOf(id);
                if (n > 0) Inv.TryRemove(id, n);
            }
        }

        // ── 1. 연구대를 짓는다 ──────────────────────────────────

        static IEnumerator 연구대를_짓는다()
        {
            E2EHarness.Log("— 거점에 연구대를 세운다 —");

            var placer = Placer;
            placer.SelectById(연구대);
            yield return null;
            E2EHarness.Assert(placer.Selected != null, "연구대 정의를 찾았다");
            E2EHarness.Assert(placer.Selected.requiredBlueprint != null &&
                              placer.Selected.requiredBlueprint.id == "bp_salvaged_mechanism",
                              "연구대는 부품 재조립 청사진을 요구한다");

            var go = new GameObject[1];
            yield return 세운다(연구대, g => go[0] = g);
            E2EHarness.Assert(go[0] != null, "좌클릭으로 연구대를 세웠다");
            if (go[0] == null) yield break;

            _station = go[0].GetComponentInChildren<ResearchStation>(true);
            E2EHarness.Assert(_station != null, "세운 것에 연구대 부품이 붙어 있다");
            E2EHarness.Assert(_station != null && _station.Book != null,
                              "연구대가 목록을 들고 있다");
            E2EHarness.Assert(_station != null && _station.IsPowered,
                              "연구대는 연료를 먹지 않는다 — 세우면 돈다");
            E2EHarness.Log("  프롬프트: " + _station.InteractionPrompt);
        }

        // ── 2. 아직 잠겨 있다 ───────────────────────────────────

        static IEnumerator 돌파정은_아직_잠겨_있다()
        {
            E2EHarness.Log("— 알기 전에는 만들 수 없다 —");

            var go = new GameObject[1];
            yield return 세운다(제작대, g => go[0] = g);
            E2EHarness.Assert(go[0] != null, "제작대를 세웠다");
            if (go[0] == null) yield break;

            _bench = go[0].GetComponentInChildren<CraftingBench>(true);
            E2EHarness.Assert(_bench != null, "제작대 부품이 붙어 있다");
            if (_bench == null) yield break;

            E2EHarness.Assert(!Ledger.IsUnlocked(돌파설계), "돌파 설계를 아직 모른다");

            UI.Open(_bench);
            yield return null;
            yield return null;

            // 모르는 것은 회색으로 남기지 않고 <b>목록에서 지운다</b>.
            // 잠긴 줄을 남겨 두던 시절에는 그 자리에 "돌파 설계 청사진이 필요하다:
            // 낫의 핵을 연구대에서 분석하면 알게 된다"가 통째로 적혔다.
            var row = 줄(돌파정);
            E2EHarness.Assert(row == null || !row.gameObject.activeInHierarchy,
                              "돌파정 줄이 제작대 목록에 아예 없다");
            E2EHarness.Assert(!보이는줄().Any(s => s.Contains("잠김") || s.Contains("청사진")),
                              "목록 어디에도 자물쇠도 청사진 이야기도 없다");
            E2EHarness.Assert(!CraftingService.CanCraft(레시피(돌파정), Inv, StationType.Bench, Ledger),
                              "재료가 가득해도 몰라서 만들 수 없다");

            UI.Close();
            yield return null;
        }

        // ── 2-b. 겪지 않은 것은 목록에 없다 ─────────────────────

        /// <summary>
        /// 연구대를 처음 열면 <b>아무 줄도 없다</b>. 부위 하나를 손에 넣으면
        /// <b>창을 닫지 않은 채</b> 그 줄만 생기고, 다 써 버려도 남는다.
        ///
        /// 감추기 전에는 연구대 앞에 서는 순간 일곱 줄이 펼쳐졌다 —
        /// "공 개체 분석 · 공의 껍질 0/3", "삼켜지지 않는 핵 분석 · 낫의 핵 0/1"...
        /// 생물 다섯 종의 존재와 한 번도 본 적 없는 부위 일곱 종의 이름이 통째로
        /// 새어 나갔다. 제작 목록에서 지운 정보가 옆문으로 돌아온 셈이다.
        /// </summary>
        static IEnumerator 가져_본_적_없는_것은_목록에_없다()
        {
            E2EHarness.Log("— 있는지도 모르는 물체를 가져오라 할 수는 없다 —");
            if (_station == null) yield break;

            var book = _station.Book;
            E2EHarness.Assert(book?.entries != null && book.entries.Length > 0,
                              "연구 목록에 항목이 있다 — 감출 것이 없으면 검사가 공회전한다");
            if (book?.entries == null) yield break;

            // 앞선 단계에서 유물을 쥔 적이 없어야 한다. Prepare가 원장을 비우고
            // 재료를 치웠으므로 여기까지는 아직 아무 부위도 겪지 않은 상태다.
            var 겪은것 = book.entries.Where(e => e?.materials != null)
                                     .SelectMany(e => e.materials)
                                     .Where(m => m?.item != null && HeldRecord.Has(Ledger, m.item.id))
                                     .Select(m => m.item.id)
                                     .Distinct().ToList();
            E2EHarness.Assert(겪은것.Count == 0,
                              "어떤 부위도 아직 가져 본 적이 없다" +
                              (겪은것.Count > 0 ? " (겪은 것: " + string.Join(", ", 겪은것) + ")" : ""));

            _station.Interact(E2EHarness.Player);
            yield return null;
            yield return null;

            E2EHarness.Assert(UI.CurrentResearchHost == (object)_station, "연구대 목록이 열렸다");

            var 켜진항목 = 켜진_연구줄(book);
            E2EHarness.AssertEqual(켜진항목.Count, 0,
                                   "연구대를 처음 열면 항목 줄이 하나도 없다" +
                                   (켜진항목.Count > 0 ? " (실제: " + string.Join(", ", 켜진항목) + ")" : ""));

            var 화면 = 보이는줄();
            화면.Add(안내문());

            var 샌것 = 샌_이름들(book, 화면);
            E2EHarness.Assert(샌것.Count == 0,
                              "화면 어디에도 항목 이름도 재료 이름도 없다" +
                              (샌것.Count > 0 ? " (샌 것: " + string.Join(", ", 샌것) + ")" : ""));

            E2EHarness.Assert(안내문() == MenuListing.NothingKnownToResearch,
                              $"빈 목록에도 할 말은 있다 — \"{안내문()}\"");
            E2EHarness.Assert(!안내문().Any(char.IsDigit),
                              "몇 개가 남았는지 세어 주지 않는다 — 그 숫자도 앞으로 무엇이 있는지를 말한다");

            // ── 창을 닫지 않은 채 부위 하나를 손에 넣는다 ──
            준다(핵, 1);
            yield return null;
            yield return null;

            var 이제켜진것 = 켜진_연구줄(book);
            E2EHarness.AssertEqual(이제켜진것.Count, 1,
                                   "창을 닫았다 열지 않았는데 줄 하나가 생겼다 " +
                                   "(실제: " + string.Join(", ", 이제켜진것) + ")");
            E2EHarness.Assert(이제켜진것.Contains(핵연구),
                              $"생긴 줄이 손에 쥔 것의 항목이다 — {핵연구}");

            var 줄글자 = 글자(줄(핵연구));
            E2EHarness.Assert(줄글자.Contains("1/1"), $"쥔 것이 줄에 세어진다 — \"{줄글자}\"");

            // ── 다 써 버려도 기록은 남는다 ──
            Inv.TryRemove(핵, Inv.CountOf(핵));
            yield return null;
            yield return null;

            E2EHarness.AssertEqual(Inv.CountOf(핵), 0, "손에서 다시 비웠다");
            E2EHarness.AssertEqual(켜진_연구줄(book).Count, 1,
                                   "다 써 버렸다고 줄이 사라지지는 않는다 — " +
                                   "겪은 것은 겪은 것이다");

            UI.Close();
            yield return null;
        }

        /// <summary>지금 켜져 있는 연구 항목 줄들의 id.</summary>
        static List<string> 켜진_연구줄(ResearchBookSO book) =>
            book.entries.Where(e => e != null)
                        .Select(e => e.id)
                        .Where(id => 줄(id) != null && 줄(id).gameObject.activeInHierarchy)
                        .ToList();

        /// <summary>목록이 비었을 때 대신 서는 한 줄. 이름이 Row_로 시작하지 않는다.</summary>
        static string 안내문()
        {
            var t = UI.GetComponentsInChildren<TMP_Text>(true)
                      .FirstOrDefault(x => x.transform.parent != null &&
                                           x.transform.parent.name == "EmptyNotice" &&
                                           x.gameObject.activeInHierarchy);
            return t != null ? t.text : "";
        }

        /// <summary>화면 문자열들에 섞여 나온 연구 항목 이름·소재 이름. 없으면 빈 목록.</summary>
        static List<string> 샌_이름들(ResearchBookSO book, List<string> 화면)
        {
            var 감출것 = new HashSet<string>();
            foreach (var e in book.entries)
            {
                if (e == null) continue;
                if (!string.IsNullOrWhiteSpace(e.displayName)) 감출것.Add(e.displayName.Trim());
                if (e.materials == null) continue;

                foreach (var need in e.materials)
                {
                    var name = need?.item != null ? need.item.displayName : null;
                    if (!string.IsNullOrWhiteSpace(name)) 감출것.Add(name.Trim());
                }
            }

            E2EHarness.Assert(감출것.Count > 5,
                              $"감출 이름이 실제로 에셋에 있다 ({감출것.Count}종)");

            return 감출것.Where(n => 화면.Any(s => !string.IsNullOrEmpty(s) && s.Contains(n)))
                         .Select(n => $"\"{n}\"")
                         .ToList();
        }

        /// <summary>지금 실제로 켜져 있는 줄들의 글자.</summary>
        static List<string> 보이는줄() =>
            UI.GetComponentsInChildren<TMP_Text>(true)
              .Where(t => t.gameObject.name == "Label" && t.gameObject.activeInHierarchy &&
                          t.transform.parent != null &&
                          t.transform.parent.name.StartsWith("Row_"))
              .Select(t => t.text)
              .ToList();

        // ── 3. 소재 부족 ────────────────────────────────────────

        static IEnumerator 소재가_없으면_분석할_수_없다()
        {
            E2EHarness.Log("— 들여다볼 물건이 없다 —");
            if (_station == null) yield break;

            var entry = 항목(핵연구);
            E2EHarness.Assert(entry != null, "핵 연구 항목을 찾았다");
            if (entry == null) yield break;

            // 두 스테이션을 세우느라 스크랩이 줄었다. 여기서 볼 것은 소재 부족이므로
            // 태울 것은 넉넉히 채워 둔다 — 두 부족이 겹치면 무엇에 막혔는지 못 가른다.
            준다("scrap", 60 - Inv.CountOf("scrap"));
            E2EHarness.AssertEqual(Inv.CountOf(핵), 0, "낫의 핵이 없다");
            E2EHarness.Assert(Inv.CountOf("scrap") >= entry.energyCost,
                              $"스크랩은 넉넉하다 ({Inv.CountOf("scrap")} ≥ {entry.energyCost})");

            _station.Interact(E2EHarness.Player);
            yield return null;
            yield return null;

            E2EHarness.Assert(UI.CurrentResearchHost == (object)_station,
                              "연구대에서 목록이 열렸다");

            var 대기열줄 = UI.GetComponentsInChildren<Button>(true)
                             .FirstOrDefault(b => b.gameObject.name == "Row_ResearchQueue");
            E2EHarness.Assert(대기열줄 != null && 대기열줄.gameObject.activeInHierarchy,
                              "연구 대기열 줄이 화면에 있다");

            // 제작 줄은 접혀 있다 — 만드는 자리와 알아내는 자리는 다른 자리다.
            var 제작줄 = 줄(돌파정);
            E2EHarness.Assert(제작줄 == null || !제작줄.gameObject.activeInHierarchy,
                              "연구대 앞에서는 제작 줄이 뜨지 않는다");

            var row = 줄(핵연구);
            E2EHarness.Assert(row != null, "연구 항목 줄이 있다");
            E2EHarness.Assert(row != null && !row.interactable, "소재가 없어 누를 수 없다");
            E2EHarness.Assert(글자(row).Contains("분석할 물건이 없다"),
                              $"이유를 말한다 — \"{글자(row)}\"");

            int 스크랩전 = Inv.CountOf("scrap");
            누른다(row);
            yield return null;
            E2EHarness.Assert(_station.Work.IsEmpty, "눌러도 걸리지 않는다");
            E2EHarness.AssertEqual(Inv.CountOf("scrap"), 스크랩전, "거절됐으니 스크랩도 그대로다");

            UI.Close();
            yield return null;
        }

        // ── 4. 스크랩 부족 ──────────────────────────────────────

        static IEnumerator 스크랩이_모자라면_거절한다()
        {
            E2EHarness.Log("— 태울 것이 모자란다 —");
            if (_station == null) yield break;

            준다(핵, 1);
            var entry = 항목(핵연구);
            E2EHarness.Assert(entry != null, "핵 연구 항목을 찾았다");
            if (entry == null) yield break;

            // 스크랩을 비용보다 하나 모자라게 남긴다. 소재는 손에 있는 상태다 —
            // 거절 이유가 소재가 아니라 에너지임을 갈라내는 자리다.
            int 남길것 = entry.energyCost - 1;
            int 지금 = Inv.CountOf("scrap");
            if (지금 > 남길것) Inv.TryRemove("scrap", 지금 - 남길것);
            E2EHarness.AssertEqual(Inv.CountOf("scrap"), 남길것,
                                   $"스크랩을 {남길것}개만 남겼다 (필요 {entry.energyCost})");

            _station.Interact(E2EHarness.Player);
            yield return null;
            yield return null;

            var row = 줄(핵연구);
            E2EHarness.Assert(row != null && !row.interactable, "태울 것이 없어 누를 수 없다");
            E2EHarness.Assert(글자(row).Contains("스크랩"),
                              $"모자란 것이 스크랩임을 말한다 — \"{글자(row)}\"");

            누른다(row);
            yield return null;
            E2EHarness.Assert(_station.Work.IsEmpty, "눌러도 걸리지 않는다");
            E2EHarness.AssertEqual(Inv.CountOf(핵), 1, "거절됐으니 유물도 그대로다");

            UI.Close();
            yield return null;

            준다("scrap", 60);
        }

        // ── 5. 취소 환급 ────────────────────────────────────────

        static IEnumerator 물리면_유물과_스크랩이_전부_돌아온다()
        {
            E2EHarness.Log("— 다 보지 않은 것을 물린다 —");
            if (_station == null) yield break;

            준다(막, 1);
            var entry = 항목(막연구);
            if (entry == null) yield break;

            int 막전 = Inv.CountOf(막);
            int 스크랩전 = Inv.CountOf("scrap");

            _station.Interact(E2EHarness.Player);
            yield return null;
            yield return null;

            var row = 줄(막연구);
            E2EHarness.Assert(row != null && row.interactable, "이제 걸 수 있다");
            누른다(row);
            yield return null;

            E2EHarness.AssertEqual(_station.Work.Count, 1, "분석이 연구대에 걸렸다");
            E2EHarness.AssertEqual(Inv.CountOf(막), 막전 - 1, "유물이 걸리는 순간 들어갔다");
            E2EHarness.AssertEqual(Inv.CountOf("scrap"), 스크랩전 - entry.energyCost,
                                   $"스크랩 {entry.energyCost}개가 탔다");
            E2EHarness.Assert(!Ledger.IsUnlocked(보행설계), "걸었다고 바로 알게 되지는 않는다");

            // 조금 진행시킨 뒤 물린다. 손도 안 댄 것을 물리는 것과 다르다.
            yield return 기다린다(1.0f);
            E2EHarness.Assert(_station.Work.Active.Elapsed > 0.5f,
                              $"분석이 진행 중이다 ({_station.Work.Active.Elapsed:F1}초)");
            E2EHarness.Assert(글자(row).Contains("분석 중"),
                              $"줄이 진행 상태를 보여 준다 — \"{글자(row)}\"");

            누른다(row);
            yield return null;

            E2EHarness.Assert(_station.Work.IsEmpty, "줄을 다시 눌러 물렸다");
            E2EHarness.AssertEqual(Inv.CountOf(막), 막전, "유물이 돌아왔다");
            E2EHarness.AssertEqual(Inv.CountOf("scrap"), 스크랩전, "태운 스크랩까지 전부 돌아왔다");
            E2EHarness.Assert(!Ledger.IsUnlocked(보행설계), "물렸으니 알아낸 것도 없다");

            UI.Close();
            yield return null;
        }

        // ── 6. 시간이 지나야 알게 된다 ──────────────────────────

        static IEnumerator 시간이_지나야_알게_된다()
        {
            E2EHarness.Log("— 유물과 스크랩을 넣고 기다린다 —");
            if (_station == null) yield break;

            var entry = 항목(핵연구);
            if (entry == null) yield break;

            준다(핵, 1 - Inv.CountOf(핵));
            E2EHarness.Assert(Inv.CountOf(핵) >= 1, "낫의 핵을 쥐었다");

            int 말한줄수 = UnlockService.Instance.LinesSpoken;
            int 끝낸것 = _station.CompletedCount;

            _station.Interact(E2EHarness.Player);
            yield return null;
            yield return null;

            var row = 줄(핵연구);
            E2EHarness.Assert(row != null && row.interactable, "이제 걸 수 있다");
            E2EHarness.Log($"  {entry.id}: 소재 {핵} 1 + 스크랩 {entry.energyCost}, " +
                           $"{entry.researchSeconds:F0}초");
            누른다(row);
            yield return null;

            E2EHarness.AssertEqual(_station.Work.Count, 1, "분석이 걸렸다");
            E2EHarness.AssertEqual(Inv.CountOf(핵), 0, "유물이 들어갔다");

            UI.Close();
            yield return null;

            // 자리를 뜬다. 제작대와 같다 — 걸어 두고 떠날 수 있어야 물건이 자리를
            // 차지할 이유가 생긴다.
            var 원래자리 = E2EHarness.Player.transform.position;
            E2EHarness.Teleport(원래자리 + Vector3.right * 14f + Vector3.up * 1f);
            yield return null;

            yield return 시간을_접고_기다린다(
                () => _station.CompletedCount > 끝낸것,
                "떠나 있는 동안 연구대가 다 보았다", entry.researchSeconds + 10f);

            E2EHarness.Teleport(원래자리);
            yield return null;

            E2EHarness.Assert(_station.Work.IsEmpty, "다 본 항목은 줄에서 빠진다");
            E2EHarness.Assert(_station.LastCompleted == entry, "끝난 것이 그 항목이다");
            E2EHarness.Assert(Ledger.IsUnlocked(돌파설계),
                              "돌파 설계가 원장에 적혔다 — 산출물은 물건이 아니라 앎이다");

            yield return E2EHarness.WaitUntil(
                () => UnlockService.Instance.LinesSpoken > 말한줄수,
                "우주복 AI가 알아낸 것을 말했다", 8f);
            E2EHarness.Log($"  AI: {UnlockService.Instance.LastLine}");
            E2EHarness.Assert(UnlockService.Instance.LastLine.EndsWith("제작법을 확보했습니다."),
                              "연구의 정형구로 닫는다 — 주운 것과 밝혀낸 것은 다른 문장이다");

            // 두 번 알아낼 것은 없다.
            _station.Interact(E2EHarness.Player);
            yield return null;
            yield return null;
            var row2 = 줄(핵연구);
            E2EHarness.Assert(row2 != null && !row2.interactable, "끝난 항목은 다시 걸리지 않는다");
            E2EHarness.Assert(글자(row2).Contains("이미 밝혀냈다"),
                              $"이미 안다고 적혀 있다 — \"{글자(row2)}\"");
            UI.Close();
            yield return null;
        }

        // ── 7. 잠겼던 것이 열린다 ───────────────────────────────

        static IEnumerator 잠겼던_돌파정을_만들_수_있게_된다()
        {
            E2EHarness.Log("— 알고 나니 돌파정을 만들 수 있다 —");
            if (_bench == null) yield break;

            var r = 레시피(돌파정);
            if (r == null) yield break;

            foreach (var need in r.ingredients)
                if (need?.item != null) 준다(need.item.id, need.count - Inv.CountOf(need.item.id));

            int 결과전 = Inv.CountOf(돌파정);

            UI.Open(_bench);
            yield return null;
            yield return null;

            var row = 줄(돌파정);
            E2EHarness.Assert(row != null && row.gameObject.activeInHierarchy,
                              "없던 줄이 목록에 새로 생겼다 — 알게 되자 목록이 자랐다");
            E2EHarness.Assert(row != null && row.interactable, "그 줄을 누를 수 있다");
            E2EHarness.Assert(!글자(row).Contains("잠김"),
                              $"재료가 적힌다 — \"{글자(row)}\"");

            누른다(row);
            yield return null;
            yield return null;
            E2EHarness.AssertEqual(_bench.Work.Queue.Count, 1, "돌파정이 제작대에 걸렸다");

            UI.Close();
            yield return null;

            yield return 시간을_접고_기다린다(() => _bench.Work.HasOutput,
                                              "제작대가 돌파정을 다 만들었다",
                                              r.craftSeconds + 10f);

            _bench.Interact(E2EHarness.Player);
            yield return null;
            E2EHarness.AssertEqual(Inv.CountOf(돌파정), 결과전 + 1,
                                   "연구 하나가 종막의 열쇠를 손에 쥐게 했다");
        }

        // ── 다른 시나리오가 쓰는 창구 ───────────────────────────

        /// <summary>
        /// 청사진을 원장에 직접 적는다.
        ///
        /// <b>왜 이런 것이 필요한가.</b> 진행 장비(액면 보행 장비·돌파정)는 연구대의
        /// 산출물 뒤에 서고, 그 연구의 소재인 유물은 낫이 순찰하다 흘린다(백로그 39).
        /// 배선은 이제 다 있다 — <c>E2EDescent</c>는 이 창구를 쓰지 않고
        /// <c>E2ERelicSupply</c>를 통해 실제로 주워 연구한다.
        ///
        /// 남은 사용처는 <b>동선을 걷는</b> 두 시나리오(<c>E2EChapter1</c>·
        /// <c>E2EWalkthrough</c>)뿐이다. 낫의 서식지는 아직 씬에 놓이지 않았고(§8-4는
        /// 사람과 함께 한다), 걸어서 닿을 낫의 영역이 없는 채로 그 자리에 낫을 소환해
        /// 세우면 그것은 이미 동선이 아니다. 매크로늄 광맥이 같은 이유로 같은 대접을
        /// 받는다. <b>§8-4에서 서식지가 놓이면 그쪽도 실제로 걸어가 줍도록 고친다.</b>
        /// </summary>
        public static bool 원장에_적는다(params string[] blueprintIds)
        {
            var ledger = BlueprintGate.Active;
            if (ledger == null) return false;

            foreach (var id in blueprintIds) ledger.Unlock(id);
            return true;
        }

        // ── 공통 동작 ───────────────────────────────────────────

        static void 준다(string id, int 개수)
        {
            if (개수 <= 0) return;
            var db = E2EHarness.Player.Inventory.Database;
            var item = db != null ? db.GetById(id) : null;
            if (item != null) Inv.TryAdd(item, 개수);
        }

        static RecipeSO 레시피(string id)
        {
            var book = Resources.FindObjectsOfTypeAll<RecipeBookSO>().FirstOrDefault();
            var pool = book?.recipes != null ? book.recipes : Resources.FindObjectsOfTypeAll<RecipeSO>();
            return pool.FirstOrDefault(r => r != null && r.id == id);
        }

        static ResearchEntrySO 항목(string id) =>
            _station != null && _station.Book != null ? _station.Book.Find(id) : null;

        static Button 줄(string id) =>
            UI.GetComponentsInChildren<Button>(true)
              .FirstOrDefault(b => b.gameObject.name == "Row_" + id);

        static string 글자(Component row)
        {
            if (row == null) return "";
            var t = row.GetComponentsInChildren<TMP_Text>(true)
                       .FirstOrDefault(x => x.gameObject.name == "Label");
            return t != null ? t.text : "";
        }

        static void 누른다(Button b)
        {
            if (b == null) return;
            ExecuteEvents.Execute(b.gameObject, new PointerEventData(EventSystem.current),
                                  ExecuteEvents.pointerClickHandler);
        }

        static IEnumerator 기다린다(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.deltaTime; yield return null; }
        }

        /// <summary>
        /// 시간을 8배로 접어 기다린다. 연구는 분 단위로 설계된 것이라 실시간으로
        /// 기다리면 검사 하나가 시나리오 전체보다 길어진다 — 시간 제작 시나리오가
        /// 화톳불 가공에서 쓰는 것과 같은 수단이다.
        /// </summary>
        static IEnumerator 시간을_접고_기다린다(System.Func<bool> 조건, string what, float 실시간상한)
        {
            float 이전 = Time.timeScale;
            Time.timeScale = 8f;
            yield return E2EHarness.WaitUntil(조건, what, 실시간상한);
            Time.timeScale = 이전;
        }

        // ── 짓기 ────────────────────────────────────────────────

        /// <summary>
        /// 놓을 자리를 스스로 찾아 실제 좌클릭으로 세운다.
        ///
        /// 좌표를 박아 두지 않는 이유: 지형이 바뀌면 그 좌표만 조용히 낡는다.
        /// 실제 판정 함수에게 물어보고 통과하는 곳을 쓰면 지형이 바뀌어도 따라간다
        /// (<c>E2EBaseBuilding</c>이 확립한 방식).
        /// </summary>
        public static IEnumerator 세운다(string id, System.Action<GameObject> result)
        {
            var placer = Placer;
            E2EHarness.Assert(placer != null, "BuildPlacer가 있다");
            if (placer == null) { result(null); yield break; }

            placer.SelectById(id);
            yield return null;

            bool found = false;
            yield return 놓을_자리를_찾는다(placer, r => found = r);

            if (!found)
            {
                E2EHarness.Log($"  [배치 문제] {id}를 놓을 자리를 찾지 못했다 " +
                               $"(마지막 판정 {placer.LastResult})");
                placer.Cancel();
                result(null);
                yield break;
            }

            yield return E2EHarness.ClickAttack();
            yield return null;
            yield return null;

            var built = Object.FindObjectsByType<BuiltStructure>(FindObjectsInactive.Exclude)
                              .OrderByDescending(b => b.GetEntityId())
                              .FirstOrDefault(b => b.Definition != null && b.Definition.id == id);

            placer.Cancel();
            yield return null;

            if (built != null)
                E2EHarness.Log($"  세웠다: {built.name} {built.transform.position.ToString("F0")}");
            result(built != null ? built.gameObject : null);
        }

        static IEnumerator 놓을_자리를_찾는다(BuildPlacer placer, System.Action<bool> result)
        {
            var player = E2EHarness.Player.transform;

            for (int ring = 0; ring < 3; ring++)
            {
                float dist = 1.8f + ring * 0.9f;

                for (int a = 0; a < 12; a++)
                {
                    var dir = Quaternion.Euler(0f, a * 30f, 0f) * Vector3.forward;
                    var probe = player.position + dir * dist + Vector3.up * 2f;

                    if (!Physics.Raycast(probe, Vector3.down, out var hit, 8f, ~0,
                                         QueryTriggerInteraction.Ignore))
                        continue;

                    E2EHarness.LookAt(hit.point);
                    yield return null;
                    yield return null;

                    if (placer.Evaluate(out _, out _) == PlacementResult.Ok)
                    {
                        result(true);
                        yield break;
                    }
                }
            }
            result(false);
        }

        static IEnumerator 치운다()
        {
            if (_station != null) Object.Destroy(_station.gameObject);
            if (_bench != null) Object.Destroy(_bench.gameObject);
            _station = null;
            _bench = null;

            yield return E2EHarness.ReleaseAllKeys();
        }
    }
}
