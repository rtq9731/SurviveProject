using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using Survive.Harvesting;
using Survive.Progression;
using Survive.UI;

namespace Survive.Testing
{
    /// <summary>
    /// 도감 식물 탭 — <b>식물만 갈래를 갖는 구조</b>가 화면에 서 있는가 (검토회신 ⑪).
    ///
    /// 목록을 짓는 규칙은 EditMode(CodexPlantTabTests)가 이미 지킨다. 여기서 보는 것은
    /// 그 규칙이 진짜 탭·진짜 원장·진짜 채집에 닿는지다: 탭 단추가 실제로 서 있는가,
    /// 오갈 때 목록이 갈래마다 갈리는가, 그리고 <b>식물을 한 포기 따고 나면</b>
    /// 열려 있던 화면이 스스로 그 줄을 채우는가.
    /// </summary>
    public static class E2ECodexPlant
    {
        static CodexUI Codex => CodexUI.Instance;
        static UnlockLedger Ledger => UnlockService.Instance.Ledger;

        static PlantBookSO _book;
        static GameObject _spawned;

        public static IEnumerator FullRun()
        {
            E2EHarness.ClearLog();
            E2EHarness.Log("=== 도감 식물 탭 ===");

            E2EHarness.Assert(Codex != null, "도감 화면이 스스로 서 있다 (씬 배선 없음)");
            E2EHarness.Assert(UnlockService.Instance != null, "해금 서비스가 서 있다");

            _book = Resources.Load<PlantBookSO>(CodexUI.PlantBookResourceName);
            E2EHarness.Assert(_book != null, "식물 목록 에셋을 Resources에서 읽었다");
            E2EHarness.Assert(_book != null && _book.plants.Length > 0, "식물 목록이 비어 있지 않다");

            yield return 탭이_서_있고_오갈_수_있다();
            yield return 못_본_식물은_실루엣으로만_뜬다();
            yield return 한_포기_따면_그_탭에_뜬다();
            yield return 식물은_다른_탭에_겹쳐_나오지_않는다();

            치운다();
            Codex.Close();

            E2EHarness.Log("=== 도감 식물 탭 통과 ===");
        }

        // ── 1. 탭이 서 있는가 ────────────────────────────────────

        static IEnumerator 탭이_서_있고_오갈_수_있다()
        {
            E2EHarness.Log("[1] 탭을 오간다");

            Codex.Close();
            Ledger.Clear();
            yield return null;

            yield return E2EHarness.TapKey(Key.J);
            yield return E2EHarness.WaitUntil(() => Codex.IsOpen, "J 키로 분석 기록이 열렸다", 3f);

            E2EHarness.AssertEqual(Codex.Section, CodexCatalog.DefaultSection,
                                   "열면 물질 분석부터 보인다 (식물은 예외라 첫 화면이 아니다)");

            ClickTab(CodexSection.Plant);
            yield return null;
            E2EHarness.AssertEqual(Codex.Section, CodexSection.Plant, "식물 탭으로 넘어갔다");
            E2EHarness.AssertEqual(Codex.Entries.Count, _book.plants.Length,
                                   "식물 탭에 목록의 모든 식물이 실렸다");

            E2EHarness.Assert(TabLabel(CodexSection.Plant)
                                  .Contains(CodexCatalog.SectionTitle(CodexSection.Plant)),
                              "탭에 갈래 이름이 표에서 나온 글자로 적혔다");

            ClickTab(CodexSection.Discovery);
            yield return null;
            E2EHarness.AssertEqual(Codex.Section, CodexSection.Discovery, "물질 분석으로 돌아왔다");
            E2EHarness.Assert(Codex.Entries.All(e => e.Section == CodexSection.Discovery),
                              "돌아온 목록에 식물 줄이 남아 있지 않다");

            ClickTab(CodexSection.Plant);
            yield return null;
            E2EHarness.Assert(Codex.Entries.All(e => e.Section == CodexSection.Plant),
                              "식물 탭에는 식물만 있다");
        }

        // ── 2. 모르는 것은 이름을 주지 않는다 ────────────────────

        static IEnumerator 못_본_식물은_실루엣으로만_뜬다()
        {
            E2EHarness.Log("[2] 아직 따 보지 않은 식물");

            Ledger.Clear();
            Codex.ShowSection(CodexSection.Plant);
            yield return null;

            var 첫식물 = _book.plants.First(p => p != null);
            string key = CodexCatalog.PlantKey(첫식물);

            var row = Row(key);
            E2EHarness.Assert(row != null, $"식물 줄이 목록에 남아 있다: {key}");
            E2EHarness.AssertEqual(Label(row), CodexCatalog.UnknownTitle,
                                   "따 보지 않은 식물은 이름조차 주지 않는다");

            Codex.Select(IndexOf(key));
            yield return null;
            E2EHarness.AssertEqual(Detail(), CodexCatalog.UnknownPlantBody,
                                   "잠긴 줄에는 미관찰 안내가 뜬다");
        }

        // ── 3. 한 포기 따면 그 자리에서 채워진다 ─────────────────

        static IEnumerator 한_포기_따면_그_탭에_뜬다()
        {
            E2EHarness.Log("[3] 식물을 한 포기 딴다");

            var 정의 = _book.plants.First(p => p != null);
            string key = CodexCatalog.PlantKey(정의);
            E2EHarness.Assert(!Ledger.IsUnlocked(key), "아직 잠겨 있다");

            // 창을 열어 둔 채로 딴다. 다시 열어야 채워지는 화면은 채워진 것이 아니다.
            Codex.ShowSection(CodexSection.Plant);
            yield return null;

            var node = 심는다(정의);
            E2EHarness.Assert(node != null, "식물을 한 포기 세웠다");
            if (node == null) yield break;

            yield return E2EHarness.StandInFrontOf(node.transform, 1.6f);
            yield return null;

            float 홀드 = node.HoldDuration;
            yield return E2EHarness.HoldKey(Key.E, 홀드 + 0.5f);
            yield return null;

            // 조준이 남의 것을 잡았을 수 있다. 홀드 경로 자체는 다른 시나리오가 지키므로
            // 여기서 볼 "따면 도감에 남는가"는 상호작용을 직접 넣어 끝낸다
            // (E2ENewMaterials가 같은 자리에서 쓰는 방법이다).
            if (!Ledger.IsUnlocked(key))
            {
                E2EHarness.Log("    조준이 빗나갔다 — 상호작용을 직접 넣어 배선만 본다");
                node.Interact(E2EHarness.Player);
                yield return null;
            }

            E2EHarness.Assert(Ledger.IsUnlocked(key), "딴 식물이 원장에 적혔다");

            yield return E2EHarness.WaitUntil(
                () => Codex.Entries.Any(e => e.Key == key && e.Unlocked),
                "열려 있던 화면이 스스로 그 줄을 채웠다", 3f);

            var row = Row(key);
            E2EHarness.Assert(row != null, "딴 식물의 줄이 있다");
            E2EHarness.AssertEqual(Label(row), Survive.Localization.DataText.Name(정의),
                                   "이제 이름이 적힌다");

            Codex.Select(IndexOf(key));
            yield return null;

            string body = Detail();
            E2EHarness.Log("  기록: " + body.Replace("\n", " / "));
            E2EHarness.AssertEqual(body, CodexCatalog.DescribePlant(정의),
                                   "잰 값이 그대로 적힌다");
            E2EHarness.Assert(계층을_말하지_않는다(body), "관찰 기록에 계층 낱말이 없다");
            E2EHarness.Assert(계층을_말하지_않는다(TabLabel(CodexSection.Plant)),
                              "탭 이름에 계층 낱말이 없다");

            치운다();
        }

        // ── 4. 갈래가 겹치지 않는다 ──────────────────────────────

        static IEnumerator 식물은_다른_탭에_겹쳐_나오지_않는다()
        {
            E2EHarness.Log("[4] 갈래가 겹치지 않는다");

            Codex.ShowSection(CodexSection.Plant);
            yield return null;
            var 식물열쇠 = Codex.Entries.Select(e => e.Key).ToArray();
            // 잠긴 줄은 어느 갈래든 이름이 ???라 이름 비교가 성립하지 않는다. 열린 것만 본다.
            var 식물이름 = Codex.Entries.Where(e => e.Unlocked).Select(e => e.Title).ToArray();

            foreach (var 갈래 in new[] { CodexSection.Discovery, CodexSection.Blueprint,
                                        CodexSection.Research, CodexSection.Creature })
            {
                Codex.ShowSection(갈래);
                yield return null;

                var 겹친열쇠 = Codex.Entries.Where(e => 식물열쇠.Contains(e.Key))
                                            .Select(e => e.Key).ToArray();
                var 겹친이름 = Codex.Entries.Where(e => 식물이름.Contains(e.Title))
                                            .Select(e => e.Title).ToArray();

                E2EHarness.Assert(겹친열쇠.Length == 0,
                    $"{갈래} 탭에 식물 열쇠가 없다" +
                    (겹친열쇠.Length == 0 ? "" : " — 겹친 것: " + string.Join(", ", 겹친열쇠)));
                E2EHarness.Assert(겹친이름.Length == 0,
                    $"{갈래} 탭에 식물 이름이 없다" +
                    (겹친이름.Length == 0 ? "" : " — 겹친 것: " + string.Join(", ", 겹친이름)));
                E2EHarness.Assert(Codex.Entries.All(e => e.Section == 갈래),
                                  $"{갈래} 탭의 모든 줄이 그 갈래다");
            }
            E2EHarness.Log("  네 갈래 어디에도 식물이 섞이지 않았다");
        }

        // ── 도우미 ───────────────────────────────────────────────

        /// <summary>
        /// 식물 프리팹은 아직 없다. 정의만 있으므로 그 자리에서 한 포기를 세운다.
        /// <c>definition</c>은 인스펙터 전용 칸이라 리플렉션으로 물려 준다 —
        /// 게임 코드에 검증만을 위한 공개 창구를 뚫지 않기 위해서다.
        /// </summary>
        static PlantNode 심는다(PlantNodeSO 정의)
        {
            치운다();

            var eye = E2EHarness.Eye;
            var 자리 = E2EHarness.Player.transform.position + eye.transform.forward * 1.6f;

            _spawned = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _spawned.name = "E2E_Plant_" + 정의.name;
            _spawned.transform.position = 자리;
            _spawned.transform.localScale = Vector3.one * 0.5f;

            var node = _spawned.AddComponent<PlantNode>();
            var field = typeof(PlantNode).GetField("definition",
                            BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) return null;
            field.SetValue(node, 정의);

            // Awake가 definition 없이 돌았으므로 단계를 손으로 세워 준다.
            var stage = typeof(PlantNode).GetField("_stage",
                            BindingFlags.NonPublic | BindingFlags.Instance);
            stage?.SetValue(node, 정의.maxStage);

            E2EHarness.SyncPhysics();
            return node;
        }

        static void 치운다()
        {
            if (_spawned == null) return;
            Object.Destroy(_spawned);
            _spawned = null;
        }

        /// <summary>
        /// ⑩이 걷어낸 낱말들. 조각을 이어 붙여 적는 이유는 이 파일 자체가
        /// 그 낱말을 통째로 담고 있으면 되돌리는 사람이 여기서 복사해 가기 때문이다.
        /// </summary>
        static bool 계층을_말하지_않는다(string 글)
        {
            string[] 계층어 =
            {
                "분해" + "자", "생산" + "자", "소비" + "자", "미분" + "류",
                "decompo" + "ser", "produ" + "cer", "consu" + "mer", "unclassi" + "fied",
            };
            foreach (var 말 in 계층어)
                if (글.IndexOf(말, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    E2EHarness.Log($"    계층 낱말이 있다: {말}");
                    return false;
                }
            return true;
        }

        static void ClickTab(CodexSection section)
        {
            var tab = Codex.GetComponentsInChildren<Button>(true)
                           .FirstOrDefault(b => b.gameObject.name == "Tab_" + section);
            E2EHarness.Assert(tab != null, $"{section} 탭 단추가 있다");
            tab?.onClick.Invoke();
        }

        static string TabLabel(CodexSection section)
        {
            var tab = Codex.GetComponentsInChildren<Button>(true)
                           .FirstOrDefault(b => b.gameObject.name == "Tab_" + section);
            if (tab == null) return "";

            var t = tab.GetComponentsInChildren<TMP_Text>(true)
                       .FirstOrDefault(x => x.gameObject.name == "Label");
            return t != null ? t.text : "";
        }

        static Button Row(string key) =>
            Codex.GetComponentsInChildren<Button>(true)
                 .FirstOrDefault(b => b.gameObject.name == "Codex_" + key);

        static int IndexOf(string key)
        {
            for (int i = 0; i < Codex.Entries.Count; i++)
                if (Codex.Entries[i].Key == key) return i;
            return -1;
        }

        static string Label(Component row)
        {
            if (row == null) return "";
            var t = row.GetComponentsInChildren<TMP_Text>(true)
                       .FirstOrDefault(x => x.gameObject.name == "Label");
            return t != null ? t.text : "";
        }

        static string Detail()
        {
            var t = Codex.GetComponentsInChildren<TMP_Text>(true)
                         .FirstOrDefault(x => x.gameObject.name == "DetailBody");
            return t != null ? t.text : "";
        }
    }
}
