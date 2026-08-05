using System.Collections;
using UnityEngine;
using Survive.Items;
using Survive.Vitals;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// 매크로늄 액면에 닿으면 치르는 대가 (백로그 29).
    ///
    /// 규칙 자체는 <c>MacroniumContactTests</c>가 Unity 없이 본다. 여기서 보는 것은
    /// 그 규칙이 <b>실제로 사람을 죽이고, 그 죽음이 기존 고리를 타는가</b>다 —
    /// 체력 0 → 가방을 남기고 → 다시 일어선다. 대가는 죽는 것 하나가 아니라
    /// 이 왕복 전체이고, 중간에 하나만 끊겨도 액면은 그냥 아픈 바닥이 된다.
    ///
    /// 반대쪽도 같이 본다: <b>액면 보행 장비를 지니면 그 위에 선다.</b>
    /// 죽는 것만 확인하면 "장비를 만들어도 여전히 죽는다"를 놓치고, 그러면
    /// 만들 이유가 사라져 관문이 벽이 된다.
    ///
    /// 구역은 <b>런타임에 세운다</b>. 액면섬 배치는 스펙 §8-4의 일이라 아직 씬에 없고,
    /// 여기서 확인할 것은 배치가 아니라 닿았을 때의 규칙이다.
    /// </summary>
    public static class E2EMacroniumContact
    {
        static Inventory Inv => E2EHarness.Player.Inventory.Inventory;
        static PlayerVitals Vitals => E2EHarness.Player.Vitals;

        /// <summary>죽으면서 빠져나가야 하는 것. 가방이 무엇을 담는지 보려면 담길 것이 있어야 한다.</summary>
        static readonly string[] 벌어온것 = { "scrap", "alien_alloy" };

        const string 보행장비 = "surface_walker";

        static MacroniumSurfaceZone _zone;

        public static IEnumerator FullRun()
        {
            yield return Prepare();

            yield return 맨몸으로_닿으면_죽는다();
            yield return 장비를_지니면_그_위를_걷는다();

            치운다();
            E2EHarness.Log("=== 매크로늄 접촉 대가 완주 ===");
        }

        // ── 준비 ────────────────────────────────────────────────

        static IEnumerator Prepare()
        {
            var dir = Object.FindAnyObjectByType<Survive.Progression.ChapterDirector>(
                FindObjectsInactive.Exclude);
            yield return E2EHarness.WaitUntil(() => dir != null && dir.Current != null,
                                              "챕터가 시작된다", 8f);

            E2EHarness.Assert(DeathDropService.Instance != null, "사망 드롭 서비스가 스스로 서 있다");
            E2EHarness.Assert(RespawnService.Instance != null, "부활 서비스가 스스로 서 있다");
            E2EHarness.Assert(MacroniumContactService.Instance != null, "액면 접촉 서비스가 스스로 서 있다");

            // 씬에 이미 액면이 깔려 있으면 아래에서 세운 것과 구별할 수 없다.
            E2EHarness.Assert(
                Object.FindAnyObjectByType<MacroniumSurfaceZone>(FindObjectsInactive.Include) == null,
                "시작할 때 씬에 액면 구역이 없다");

            var db = E2EHarness.Player.Inventory.Database;
            E2EHarness.Assert(db != null, "아이템 데이터베이스가 연결돼 있다");
            if (db == null) yield break;

            E2EHarness.Assert(db.GetById(보행장비) != null, $"아이템 정의를 찾았다: {보행장비}");

            벗는다();
            Vitals.Health.Modify(Vitals.Health.Max);
            yield return null;

            E2EHarness.Assert(!Vitals.Health.IsEmpty, "산 채로 시작한다");
        }

        static void 채운다()
        {
            var db = E2EHarness.Player.Inventory.Database;
            if (db == null) return;

            foreach (var id in 벌어온것)
            {
                var item = db.GetById(id);
                if (item != null && Inv.CountOf(id) < 6) Inv.TryAdd(item, 6);
            }
        }

        static void 준다(string id, int 개수)
        {
            var db = E2EHarness.Player.Inventory.Database;
            var item = db != null ? db.GetById(id) : null;
            if (item != null) Inv.TryAdd(item, 개수);
        }

        /// <summary>액면 보행 장비를 내려놓는다. 보유가 곧 장착이므로 지니고 있으면 안 된다.</summary>
        static void 벗는다()
        {
            int 있는것 = Inv.CountOf(보행장비);
            if (있는것 > 0) Inv.TryRemove(보행장비, 있는것);
        }

        static int[] 재고()
        {
            var counts = new int[벌어온것.Length];
            for (int i = 0; i < 벌어온것.Length; i++) counts[i] = Inv.CountOf(벌어온것[i]);
            return counts;
        }

        // ── 액면 깔기 ───────────────────────────────────────────

        /// <summary>발바닥 높이. 액면과 견주는 값이 이것이다.</summary>
        static float 발바닥()
        {
            var p = E2EHarness.Player.transform;
            var cc = p.GetComponent<CharacterController>();
            return cc != null ? p.position.y - cc.height * 0.5f + cc.center.y : p.position.y;
        }

        /// <summary>
        /// 지금 서 있는 자리에 지정한 높이로 액면을 깐다.
        ///
        /// 액면섬을 찾아가지 않는 이유: 배치는 스펙 §8-4의 일이라 아직 씬에 없고,
        /// 여기서 볼 것은 닿은 뒤의 일이지 어디에 액면이 있느냐가 아니다.
        /// </summary>
        static MacroniumSurfaceZone 액면을_깐다(float 액면높이, float 반경 = 25f, float 폭 = 30f)
        {
            치운다();

            var p = E2EHarness.Player.transform.position;
            var go = new GameObject("E2E_MacroniumSurface");
            go.transform.position = new Vector3(p.x, 액면높이, p.z);

            var zone = go.AddComponent<MacroniumSurfaceZone>();
            zone.Setup(반경, 폭);
            return zone;
        }

        static void 치운다()
        {
            if (_zone != null) Object.Destroy(_zone.gameObject);
            _zone = null;
        }

        // ── 1. 맨몸으로 닿는다 ──────────────────────────────────

        static IEnumerator 맨몸으로_닿으면_죽는다()
        {
            E2EHarness.Log("— 맨몸으로 액면에 닿는다 —");

            벗는다();
            채운다();
            E2EHarness.AssertEqual(Inv.CountOf(보행장비), 0, "액면 보행 장비를 지니지 않았다");
            E2EHarness.Assert(Inv.CountOf(벌어온것[0]) > 0, "닿기 전에 떨굴 것이 있다");

            var 죽은자리 = E2EHarness.Player.transform.position;
            var 벌어온수량 = 재고();
            int 이전가방수 = DeathDropBag.Active.Count;
            int 이전치사 = MacroniumContactService.LethalContacts;

            // 발목까지 잠기게 깐다. 발바닥에 딱 맞추면 "닿았다"가 반올림에 걸린다.
            _zone = 액면을_깐다(발바닥() + 0.5f);
            E2EHarness.Log($"  액면 {_zone.SurfaceY:F2}, 발바닥 {발바닥():F2}, " +
                           $"건널 폭 {_zone.Magnitude:F0}m");

            yield return E2EHarness.WaitUntil(() => Vitals.Health.IsEmpty,
                                              "맨몸으로 액면에 닿자 체력이 0이 된다", 5f);

            E2EHarness.AssertEqual(MacroniumContactService.LethalContacts, 이전치사 + 1,
                                   "액면이 죽인 횟수");
            E2EHarness.AssertEqual(MacroniumContactService.LastOutcome,
                                   MacroniumContactOutcome.Lethal, "액면이 낸 판정");

            yield return E2EHarness.WaitUntil(() => DeathDropBag.Active.Count > 이전가방수,
                                              "죽은 자리에 가방이 남는다", 5f);

            var bag = DeathDropService.LastBag;
            E2EHarness.Assert(bag != null, "남긴 가방을 집을 수 있다");

            if (bag != null)
            {
                for (int i = 0; i < 벌어온것.Length; i++)
                {
                    E2EHarness.AssertEqual(Inv.CountOf(벌어온것[i]), 0,
                                           $"액면에 빠지면서 {벌어온것[i]}를 전부 잃었다");
                    E2EHarness.AssertEqual(bag.Contents.CountOf(벌어온것[i]), 벌어온수량[i],
                                           $"가방에 {벌어온것[i]}가 그대로 들어 있다");
                }

                float 거리 = 평면거리(bag.transform.position, 죽은자리);
                E2EHarness.Assert(거리 < 3f, $"가방은 빠진 자리에 남는다 ({거리:F1}m)");
            }

            // 부활 지점이 액면 안이면 일어서자마자 다시 빠진다. 그 고리는 배치(§8-4)의
            // 문제이지 규칙의 문제가 아니므로, 여기서는 액면을 걷어내고 부활만 본다.
            치운다();
            yield return null;

            yield return 부활을_기다린다();
        }

        static IEnumerator 부활을_기다린다()
        {
            int 이전 = RespawnService.RespawnCount;

            yield return E2EHarness.WaitUntil(() => RespawnService.RespawnCount > 이전,
                                              "잠깐 뒤 스스로 되살아난다", 6f);
            yield return null;

            E2EHarness.Assert(!Vitals.Health.IsEmpty, "체력이 돌아왔다");
            E2EHarness.Assert(Vitals.Health.IsFull, "체력이 가득 찼다");
        }

        // ── 2. 장비를 지니고 닿는다 ─────────────────────────────

        static IEnumerator 장비를_지니면_그_위를_걷는다()
        {
            E2EHarness.Log("— 액면 보행 장비를 지니고 닿는다 —");

            채운다();
            준다(보행장비, 1);
            E2EHarness.Assert(Inv.CountOf(보행장비) > 0, "액면 보행 장비를 지녔다");

            int 이전가방수 = DeathDropBag.Active.Count;
            int 이전치사 = MacroniumContactService.LethalContacts;
            float 이전체력 = Vitals.Health.Current;

            // 액면을 <b>땅보다 높이</b> 깔고 그 위로 떨어뜨린다.
            //
            // 발밑에 맞춰 깔면 "액면이 받쳤다"와 "그냥 땅을 밟고 서 있다"가 같은 그림이 되어,
            // 받침이 아예 동작하지 않아도 검사가 통과한다. 땅에서 띄워 놓으면
            // 서 있다는 사실 자체가 곧 받쳐졌다는 증거다.
            float 지면 = 발바닥();
            float 액면높이 = 지면 + 1.2f;

            _zone = 액면을_깐다(액면높이);
            E2EHarness.Teleport(E2EHarness.Player.transform.position + Vector3.up * 1.8f);
            yield return null;
            E2EHarness.Log($"  지면 {지면:F2}, 액면 {액면높이:F2}, 떨어뜨린 발 {발바닥():F2}");

            yield return E2EHarness.WaitUntil(
                () => MacroniumContactService.LastOutcome == MacroniumContactOutcome.Supported,
                "장비를 지니고 닿으면 액면이 몸을 받친다", 5f);

            E2EHarness.Assert(MacroniumContactService.Instance.IsSupported,
                              "발밑에 디딜 것이 깔린다");

            // 닿은 채로 시간이 흘러도 아무 일이 없어야 한다. 한 프레임만 보면
            // "처음엔 괜찮은데 조금 있다 죽는다"도, "잠깐 얹혔다 가라앉는다"도 놓친다.
            float t = 0f;
            while (t < 2.5f) { t += Time.deltaTime; yield return null; }

            E2EHarness.Assert(!Vitals.Health.IsEmpty, "액면 위에서 2.5초를 버텨도 살아 있다");
            E2EHarness.Assert(Vitals.Health.Current >= 이전체력 - 0.5f,
                              $"체력이 깎이지도 않았다 ({이전체력:F1} → {Vitals.Health.Current:F1})");
            E2EHarness.AssertEqual(MacroniumContactService.LethalContacts, 이전치사,
                                   "액면이 죽인 횟수가 늘지 않았다");
            E2EHarness.AssertEqual(DeathDropBag.Active.Count, 이전가방수,
                                   "가방이 새로 생기지 않았다");
            E2EHarness.AssertEqual(MacroniumContactService.LastOutcome,
                                   MacroniumContactOutcome.Supported, "2.5초 뒤에도 받쳐진 채다");

            float 발 = 발바닥();
            E2EHarness.Log($"  2.5초 뒤 발바닥 {발:F2} (액면 {액면높이:F2}, 지면 {지면:F2})");
            E2EHarness.Assert(Mathf.Abs(발 - 액면높이) < 0.4f,
                              $"액면 높이에 그대로 서 있다 (발 {발:F2}, 액면 {액면높이:F2})");
            E2EHarness.Assert(발 > 지면 + 0.8f,
                              $"땅이 아니라 액면이 받치고 있다 (발 {발:F2}, 지면 {지면:F2})");

            // 액면을 걷어내면 받칠 것이 없어져 땅으로 떨어져야 한다.
            // 이게 확인되지 않으면 위의 "떠 있다"가 액면 덕인지 알 수 없다.
            치운다();
            t = 0f;
            while (t < 1.5f) { t += Time.deltaTime; yield return null; }

            float 떨어진발 = 발바닥();
            E2EHarness.Log($"  액면을 걷어내자 발바닥 {발:F2} → {떨어진발:F2}");
            E2EHarness.Assert(떨어진발 < 액면높이 - 0.5f,
                              $"액면이 사라지면 떨어진다 ({떨어진발:F2})");
            E2EHarness.Assert(!MacroniumContactService.Instance.IsSupported,
                              "받침도 함께 걷힌다");
            E2EHarness.Assert(!Vitals.Health.IsEmpty, "떨어져도 살아 있다");

            벗는다();
            yield return null;
        }

        // ── 공통 ────────────────────────────────────────────────

        static float 평면거리(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
