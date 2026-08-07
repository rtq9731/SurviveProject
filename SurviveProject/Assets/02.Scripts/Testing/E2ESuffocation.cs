using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Survive.Items;
using Survive.Player;
using Survive.Vitals;

namespace Survive.Testing
{
    /// <summary>
    /// 물속에서 숨이 다해 죽고, 벌어 온 것을 그 자리에 남긴다.
    ///
    /// <see cref="E2EDeathDrop"/>은 체력을 코드로 0으로 만들어 죽인다. 사망 이후의
    /// 규칙(가방이 남는가, 되찾아지는가)을 보기에는 그걸로 충분하지만, 챕터 1이
    /// 실제로 사람을 죽이는 경로 — <b>산소가 떨어져서</b> 죽는 길 — 은 한 번도 지나지 않는다.
    ///
    /// 그 길은 고리 세 개로 이어져 있고 어느 하나가 조용히 끊겨도 압박만 사라진다:
    /// <b>잠기면 산소가 줄고</b>, <b>산소가 0이면 체력이 깎이고</b>, <b>체력이 0이면 죽는다.</b>
    /// 산소 소모만 확인하면 "산소는 0인데 아무 일도 없다"를 놓치고,
    /// 사망만 확인하면 "죽기는 하는데 물과 무관하다"를 놓친다.
    ///
    /// 그래서 여기서는 셋을 한 줄로 이어서 본다 — 실제로 잠긴 채 기다려서 죽는다.
    ///
    /// <b>바다가 살을 깎게 된 뒤(백로그 32)의 개정.</b> 이제 물속에서는 숨이 붙어 있는
    /// 동안에도 체력이 준다 — 섬 사이 액체가 물이 아니라 옅은 매크로늄이 됐기 때문이다.
    /// 그래서 "산소가 0이면 체력이 깎인다"만 보면 부족하다: 그 말은 바다가 깎는 것만으로도
    /// 참이 되어, 질식 피해가 통째로 죽어도 이 검사는 통과한다.
    ///
    /// 개정한 것은 <b>재는 방식</b>이지 기준이 아니다. 숨이 붙어 있는 동안의 감소 속도를
    /// 먼저 재 두고, 산소가 0이 된 뒤의 속도와 견준다 — 질식은 바다 <i>위에 더해져야</i>
    /// 하고, 그 차이가 곧 질식이 살아 있다는 증거다.
    /// </summary>
    public static class E2ESuffocation
    {
        static Inventory Inv => E2EHarness.Player.Inventory.Inventory;
        static PlayerVitals Vitals => E2EHarness.Player.Vitals;

        /// <summary>죽을 때 빠져나가야 하는 것들.</summary>
        static readonly string[] 벌어온것 = { "scrap", "alien_alloy" };

        public static IEnumerator FullRun()
        {
            var swim = UnityEngine.Object.FindAnyObjectByType<PlayerSwimming>(FindObjectsInactive.Exclude);
            E2EHarness.Assert(swim != null, "PlayerSwimming이 있다");
            E2EHarness.Assert(Vitals != null, "PlayerVitals가 있다");
            E2EHarness.Assert(DeathDropService.Instance != null, "사망 드롭 서비스가 서 있다");
            if (swim == null || Vitals == null) yield break;

            yield return Prepare();
            yield return Submerge(swim);
            yield return 숨이_붙어_있는_동안의_속도를_잰다();
            yield return OxygenRunsOut(swim);
            yield return HealthDrainsWhileEmpty(swim);
            yield return DiesAndLeavesABag();

            E2EHarness.Log("=== 산소 고갈 사망 완주 ===");
        }

        // ── 준비 ────────────────────────────────────────────────

        static IEnumerator Prepare()
        {
            var dir = UnityEngine.Object.FindAnyObjectByType<Survive.Progression.ChapterDirector>(
                FindObjectsInactive.Exclude);
            yield return E2EHarness.WaitUntil(() => dir != null && dir.Current != null,
                                              "챕터가 시작된다", 8f);

            var db = E2EHarness.Player.Inventory.Database;
            E2EHarness.Assert(db != null, "아이템 데이터베이스가 연결돼 있다");
            if (db == null) yield break;

            // 죽어서 잃을 것이 있어야 가방이 무엇을 담는지 볼 수 있다.
            // 모으는 것은 다른 시나리오가 보므로 여기서는 채워 넣는다.
            foreach (var id in 벌어온것)
            {
                var item = db.GetById(id);
                E2EHarness.Assert(item != null, $"아이템 정의를 찾았다: {id}");
                if (item != null) Inv.TryAdd(item, 6);
            }

            // 체력·산소가 가득한 상태에서 시작해야 걸리는 시간이 예측 가능하다
            Vitals.Health.Modify(Vitals.Health.Max);
            yield return null;

            E2EHarness.Assert(!Vitals.Health.IsEmpty, "산 채로 시작한다");
            E2EHarness.Log($"  체력 {Vitals.Health.Current:F0}, 산소 {Vitals.Oxygen.Current:F0}, " +
                           $"scrap {Inv.CountOf("scrap")}");
        }

        // ── 잠긴다 ──────────────────────────────────────────────

        /// <summary>
        /// 가장 깊은 곳으로 옮겨 머리를 담근다.
        ///
        /// 순간이동을 쓰는 이유: 여기서 보려는 것은 "물가를 찾아 걸어 들어갈 수 있는가"가
        /// 아니라 "잠긴 채 두면 죽는가"다. 걸어 들어가는 쪽은
        /// <see cref="E2ESwimRoundTrip"/>이 이미 실제 조작으로 지난다.
        /// </summary>
        static IEnumerator Submerge(PlayerSwimming swim)
        {
            E2EHarness.Assert(E2EWaterProbe.TryFindSurface(out float surface, out var water),
                              "물이 씬에 있다");
            if (water == null) yield break;

            bool found = E2EWaterProbe.TryFindDeepest(E2EHarness.Player.transform.position, surface,
                                                      60f, out var bed, out float depth);
            E2EHarness.Assert(found && depth > 2.5f,
                              $"머리까지 잠길 만큼 깊은 곳이 있다 (수심 {depth:F1}m)");
            if (!found || depth <= 2.5f) yield break;

            E2EHarness.Log($"  가장 깊은 곳 {bed.ToString("F1")}, 수심 {depth:F1}m");
            E2EHarness.Teleport(bed + Vector3.up * 0.6f);
            yield return null;
            yield return null;

            // 바닥에 서면 부력으로 떠오르므로 Ctrl로 눌러 앉힌다
            yield return E2EHarness.PressKey(Key.LeftCtrl);

            float t = 0f;
            while (t < 5f && !swim.IsHeadSubmerged)
            {
                E2EHarness.QueueKeys();
                t += Time.deltaTime;
                yield return null;
            }
            E2EHarness.Assert(swim.IsHeadSubmerged, $"머리까지 잠겼다 ({t:F1}초)");
        }

        // ── 숨이 붙어 있는 동안 ─────────────────────────────────

        /// <summary>
        /// 산소가 남아 있는 동안의 체력 감소 속도. 이것이 바다만 깎는 값이다.
        ///
        /// 뒤에서 질식이 더해졌는지 보려면 더해지기 전의 값이 있어야 한다.
        /// 상수를 적어 두지 않고 실제로 재는 이유는, 규칙이 바뀌면 기준도 같이
        /// 따라가야 하기 때문이다 — 적어 둔 숫자는 반드시 낡는다.
        /// </summary>
        static float _바다만깎는속도;

        static IEnumerator 숨이_붙어_있는_동안의_속도를_잰다()
        {
            E2EHarness.Assert(!Vitals.Oxygen.IsEmpty, "아직 숨이 붙어 있다");

            float 전 = Vitals.Health.Current;
            float t = 0f;
            while (t < 3f && !Vitals.Oxygen.IsEmpty)
            {
                E2EHarness.QueueKeys();
                t += Time.deltaTime;
                yield return null;
            }

            _바다만깎는속도 = (전 - Vitals.Health.Current) / Mathf.Max(0.01f, t);
            E2EHarness.Log($"  숨이 붙은 채 {t:F1}초 — 체력 {전:F1} → {Vitals.Health.Current:F1} " +
                           $"(초당 {_바다만깎는속도:F1})");

            E2EHarness.Assert(_바다만깎는속도 > 0f,
                              "산소가 남아 있어도 잠겨 있으면 체력이 준다 — 바다가 매크로늄이다");
        }

        // ── 산소가 다한다 ───────────────────────────────────────

        /// <summary>
        /// 잠긴 채 버틴다. 산소가 실제로 0에 닿아야 한다 —
        /// 줄기만 하고 바닥에 닿지 않으면 익사는 영원히 일어나지 않는다.
        /// </summary>
        static IEnumerator OxygenRunsOut(PlayerSwimming swim)
        {
            float start = Vitals.Oxygen.Current;
            float t = 0f;

            while (t < 60f && !Vitals.Oxygen.IsEmpty)
            {
                E2EHarness.QueueKeys();          // Ctrl을 계속 눌러 가라앉은 채로 둔다
                t += Time.deltaTime;
                yield return null;
            }

            E2EHarness.Log($"  {start:F0}에서 시작해 {t:F1}초 만에 산소가 바닥났다");
            E2EHarness.Assert(Vitals.Oxygen.IsEmpty, "잠긴 채로 두면 산소가 0이 된다");
            E2EHarness.Assert(swim.IsHeadSubmerged, "산소가 다할 때까지 잠겨 있었다");
        }

        // ── 체력이 깎인다 ───────────────────────────────────────

        static IEnumerator HealthDrainsWhileEmpty(PlayerSwimming swim)
        {
            float before = Vitals.Health.Current;

            float t = 0f;
            while (t < 2f) { E2EHarness.QueueKeys(); t += Time.deltaTime; yield return null; }

            float after = Vitals.Health.Current;
            float 속도 = (before - after) / t;
            E2EHarness.Log($"  산소 0으로 {t:F1}초 — 체력 {before:F1} → {after:F1} " +
                           $"(초당 {속도:F1}, 숨 붙었을 때는 {_바다만깎는속도:F1})");

            E2EHarness.Assert(after < before - 1f, "산소가 0이면 체력이 깎인다");

            // 질식은 바다 위에 <b>더해진다</b>. 둘 중 하나가 다른 하나를 대신하면
            // 여기서 걸린다 — 대신하는 순간 두 속도가 같아진다.
            E2EHarness.Assert(속도 > _바다만깎는속도 + 1f,
                              $"질식 피해가 바다 위에 더해진다 " +
                              $"(숨 있을 때 {_바다만깎는속도:F1} → 숨 없을 때 {속도:F1})");
        }

        // ── 죽고, 남긴다 ────────────────────────────────────────

        static IEnumerator DiesAndLeavesABag()
        {
            var 벌어온수량 = new int[벌어온것.Length];
            for (int i = 0; i < 벌어온것.Length; i++) 벌어온수량[i] = Inv.CountOf(벌어온것[i]);

            int 이전가방수 = DeathDropBag.Active.Count;

            float t = 0f;
            while (t < 40f && !Vitals.Health.IsEmpty)
            {
                E2EHarness.QueueKeys();
                t += Time.deltaTime;
                yield return null;
            }

            var 죽은자리 = E2EHarness.Player.transform.position;
            yield return E2EHarness.ReleaseAllKeys();

            E2EHarness.Log($"  숨이 다한 지 {t:F1}초 만에 체력이 0이 됐다");
            E2EHarness.Assert(Vitals.Health.IsEmpty, "질식으로 체력이 0이 된다");

            yield return E2EHarness.WaitUntil(() => DeathDropBag.Active.Count > 이전가방수,
                                              "익사한 자리에 가방이 남는다", 5f);

            var bag = DeathDropService.LastBag;
            E2EHarness.Assert(bag != null, "남긴 가방을 집을 수 있다");
            if (bag == null) yield break;

            for (int i = 0; i < 벌어온것.Length; i++)
            {
                E2EHarness.AssertEqual(Inv.CountOf(벌어온것[i]), 0,
                                       $"익사하면서 {벌어온것[i]}를 전부 잃었다");
                E2EHarness.AssertEqual(bag.Contents.CountOf(벌어온것[i]), 벌어온수량[i],
                                       $"가방에 {벌어온것[i]}가 그대로 들어 있다");
            }

            float 거리 = Vector3.Distance(bag.transform.position, 죽은자리);
            E2EHarness.Log($"  가방 {bag.transform.position.ToString("F1")}, " +
                           $"익사한 자리에서 {거리:F1}m");
            E2EHarness.Assert(거리 < 3f, $"가방은 죽은 자리에 남는다 ({거리:F1}m)");

            // 되살려 두지 않으면 뒤에 도는 시나리오가 시체에서 시작한다
            Vitals.Health.Modify(Vitals.Health.Max);
            yield return null;
            E2EHarness.Assert(!Vitals.Health.IsEmpty, "체력을 되돌려 다음 검사에 넘긴다");
        }
    }
}
