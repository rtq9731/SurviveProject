using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Survive.Items;
using Survive.Vitals;

namespace Survive.Testing
{
    /// <summary>
    /// 죽고, 떨구고, 돌아가서 되찾는다 (P2 스펙 §6-3).
    ///
    /// 왕복 압박은 "못 돌아가면 잃는 것이 있다"로 완성된다. 그 말이 참이려면
    /// 세 가지가 동시에 성립해야 하고, 하나라도 조용히 깨지면 압박은 사라진다:
    /// <b>죽으면 실제로 빠져나가고</b>, <b>그 자리에 남고</b>, <b>돌아가면 되돌아온다.</b>
    /// 셋을 따로 확인하면 "빠져나갔지만 아무 데도 없다" 같은 상태를 놓친다.
    ///
    /// 회수는 실제 조작으로 한다 — 걸어가서, 조준하고, E를 누른다.
    /// 인벤토리를 코드로 채워 넣는 것은 통과로 치지 않는다.
    /// </summary>
    public static class E2EDeathDrop
    {
        static Inventory Inv => E2EHarness.Player.Inventory.Inventory;
        static PlayerVitals Vitals => E2EHarness.Player.Vitals;

        // 떨궈야 하는 것 / 남아야 하는 것
        static readonly string[] 떨굴것 = { "scrap", "alien_alloy", "oxygen_filter" };
        static readonly string[] 남을것 = { "pickaxe", "lantern", "portal_key" };

        public static IEnumerator FullRun()
        {
            yield return Prepare();

            var 죽은자리 = E2EHarness.Player.transform.position;
            var 벌어온것 = 재고();

            DeathDropBag bag = null;
            yield return Die(r => bag = r);
            if (bag == null) yield break;

            DroppedWhatItShould(bag, 벌어온것, 죽은자리);

            yield return Revive();
            yield return WalkBackAndRetrieve(bag, 벌어온것);

            yield return SecondDeathDropsAgain();

            E2EHarness.Log("=== 사망 드롭과 회수 완주 ===");
        }

        // ── 준비 ────────────────────────────────────────────────

        static IEnumerator Prepare()
        {
            var dir = Object.FindAnyObjectByType<Survive.Progression.ChapterDirector>(
                FindObjectsInactive.Exclude);
            yield return E2EHarness.WaitUntil(() => dir != null && dir.Current != null,
                                              "챕터가 시작된다", 8f);

            E2EHarness.Assert(DeathDropService.Instance != null,
                              "사망 드롭 서비스가 스스로 서 있다");

            var db = E2EHarness.Player.Inventory.Database;
            E2EHarness.Assert(db != null, "아이템 데이터베이스가 연결돼 있다");
            if (db == null) yield break;

            // 벌어온 것 — 죽으면 전부 빠져나가야 한다
            Give(db, "scrap", 12);
            Give(db, "alien_alloy", 5);
            Give(db, "oxygen_filter", 2);

            // 몸에 지닌 것 — 죽어도 남아야 한다. 랜턴을 잃으면 어두운 데서
            // 가방을 찾아갈 수단까지 잃는다.
            foreach (var id in 남을것)
                if (Inv.CountOf(id) == 0) Give(db, id, 1);

            foreach (var id in 남을것)
                E2EHarness.Assert(Inv.CountOf(id) > 0, $"죽기 전에 {id}를 지니고 있다");

            E2EHarness.Log("  준비: " + 재고Text());
            yield return null;
        }

        static void Give(ItemDatabaseSO db, string id, int count)
        {
            var item = db.GetById(id);
            E2EHarness.Assert(item != null, $"아이템 정의를 찾았다: {id}");
            if (item != null) Inv.TryAdd(item, count);
        }

        static int[] 재고()
        {
            var counts = new int[떨굴것.Length];
            for (int i = 0; i < 떨굴것.Length; i++) counts[i] = Inv.CountOf(떨굴것[i]);
            return counts;
        }

        static string 재고Text()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var id in 떨굴것) sb.Append($"{id} {Inv.CountOf(id)}, ");
            foreach (var id in 남을것) sb.Append($"{id} {Inv.CountOf(id)}, ");
            return sb.ToString().TrimEnd(',', ' ');
        }

        // ── 죽는다 ──────────────────────────────────────────────

        static IEnumerator Die(System.Action<DeathDropBag> result)
        {
            int 이전가방수 = DeathDropBag.Active.Count;

            Vitals.Health.Modify(-(Vitals.Health.Max + 10f));
            E2EHarness.Assert(Vitals.Health.IsEmpty, "체력이 0이 됐다");

            yield return E2EHarness.WaitUntil(
                () => DeathDropBag.Active.Count > 이전가방수,
                "죽은 자리에 가방이 남는다", 5f);

            var bag = DeathDropService.LastBag;
            E2EHarness.Assert(bag != null, "남긴 가방을 집을 수 있다");
            result(bag);
        }

        static void DroppedWhatItShould(DeathDropBag bag, int[] 벌어온것, Vector3 죽은자리)
        {
            for (int i = 0; i < 떨굴것.Length; i++)
            {
                E2EHarness.AssertEqual(Inv.CountOf(떨굴것[i]), 0, $"죽으면서 {떨굴것[i]}를 전부 잃었다");
                E2EHarness.AssertEqual(bag.Contents.CountOf(떨굴것[i]), 벌어온것[i],
                                       $"가방에 {떨굴것[i]}가 그대로 들어 있다");
            }

            foreach (var id in 남을것)
                E2EHarness.Assert(Inv.CountOf(id) > 0, $"죽어도 {id}는 남는다");

            foreach (var id in 남을것)
                E2EHarness.AssertEqual(bag.Contents.CountOf(id), 0, $"{id}는 가방에 들어가지 않는다");

            float 거리 = Vector3.Distance(bag.transform.position, 죽은자리);
            E2EHarness.Log($"  가방 {bag.transform.position.ToString("F1")}, " +
                           $"죽은 자리에서 {거리:F1}m");
            E2EHarness.Assert(거리 < 3f, $"가방은 죽은 자리에 남는다 ({거리:F1}m)");
        }

        // ── 되살아난다 ──────────────────────────────────────────

        /// <summary>
        /// 부활 연출은 이 항목의 범위가 아니다. 여기서 확인하는 것은
        /// 체력이 돌아오면 다음 사망을 다시 보고할 수 있게 걸쇠가 풀린다는 것뿐이다.
        /// </summary>
        static IEnumerator Revive()
        {
            Vitals.Health.Modify(Vitals.Health.Max);
            yield return null;
            E2EHarness.Assert(!Vitals.Health.IsEmpty, "체력이 돌아왔다");
        }

        // ── 돌아가서 되찾는다 ───────────────────────────────────

        static IEnumerator WalkBackAndRetrieve(DeathDropBag bag, int[] 벌어온것)
        {
            var 가방자리 = bag.transform.position;

            // 죽은 자리에서 물러났다가 걸어서 돌아온다. 회수가 "그 자리에 서 있으면
            // 저절로 된다"가 아니라 실제로 가야 하는 일인지 보려는 것이다.
            yield return E2EHarness.StandInFrontOf(bag.transform, 7f);
            E2EHarness.Log($"  {Vector3.Distance(E2EHarness.Player.transform.position, 가방자리):F1}m 떨어져서 출발");

            yield return E2EHarness.TryWalkTo(가방자리, 2.0f, 25f);
            if (!E2EHarness.LastWalkArrived)
                E2EHarness.Log("  [주의] 걸어서 도착하지 못해 가방 앞으로 옮긴다 — 회수 자체는 그대로 검증한다");

            // 걸어왔든 옮겨졌든, 조준은 같은 자리에서 시작한다
            yield return E2EHarness.StandInFrontOf(bag.transform, 1.8f);
            E2EHarness.LookAt(가방자리);
            yield return null;
            yield return null;

            var it = E2EHarness.Player.Interactor;
            yield return E2EHarness.WaitUntil(() => it.Current is DeathDropBag,
                                              "가방이 조준된다", 5f);
            E2EHarness.Log("  프롬프트: " + it.Current.InteractionPrompt);

            yield return E2EHarness.TapKey(Key.E);
            yield return null;

            yield return E2EHarness.WaitUntil(() => Inv.CountOf("scrap") > 0,
                                              "E 한 번으로 되찾는다", 4f);

            for (int i = 0; i < 떨굴것.Length; i++)
                E2EHarness.AssertEqual(Inv.CountOf(떨굴것[i]), 벌어온것[i],
                                       $"{떨굴것[i]}가 죽기 전 수량으로 돌아왔다");

            E2EHarness.Assert(bag == null, "다 비운 가방은 세계에서 사라진다");
        }

        // ── 두 번째 죽음 ────────────────────────────────────────

        /// <summary>
        /// 사망 보고 걸쇠가 풀리지 않으면 한 판에 한 번만 죽을 수 있고,
        /// 두 번째부터는 아무것도 떨구지 않는다 — 조용히 압박이 사라지는 부류의 결함이다.
        /// </summary>
        static IEnumerator SecondDeathDropsAgain()
        {
            int 이전 = Inv.CountOf("scrap");
            E2EHarness.Assert(이전 > 0, "두 번째로 죽기 전에 떨굴 것이 있다");

            int 이전가방수 = DeathDropBag.Active.Count;
            Vitals.Health.Modify(-(Vitals.Health.Max + 10f));

            yield return E2EHarness.WaitUntil(() => DeathDropBag.Active.Count > 이전가방수,
                                              "두 번째 죽음도 가방을 남긴다", 5f);

            var bag = DeathDropService.LastBag;
            E2EHarness.AssertEqual(bag.Contents.CountOf("scrap"), 이전, "두 번째 가방에도 그대로 들어 있다");
            E2EHarness.AssertEqual(Inv.CountOf("scrap"), 0, "두 번째 죽음에서도 빠져나갔다");

            yield return Revive();

            yield return E2EHarness.StandInFrontOf(bag.transform, 1.8f);
            E2EHarness.LookAt(bag.transform.position);
            yield return null;
            yield return null;

            var it = E2EHarness.Player.Interactor;
            yield return E2EHarness.WaitUntil(() => it.Current is DeathDropBag,
                                              "두 번째 가방이 조준된다", 5f);

            yield return E2EHarness.TapKey(Key.E);
            yield return E2EHarness.WaitUntil(() => Inv.CountOf("scrap") == 이전,
                                              "두 번째 가방도 회수된다", 4f);
        }
    }
}
