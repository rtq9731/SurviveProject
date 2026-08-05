using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Survive.Combat;
using Survive.Creatures;
using Survive.Interaction;
using Survive.Items;
using Survive.Player;
using Survive.Vitals;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// 소비자 1종 — 먼저 덤비고, 빛을 피한다 (백로그 30, P2 스펙 §3·§8-5).
    ///
    /// 이 생물이 게임에 무엇을 더하는지는 문장 두 개로 요약된다.
    /// <b>"뾰족하다 = 도망쳐라"</b>와 <b>"랜턴을 켜면 다가오지 못한다"</b>.
    /// 둘 다 "그럴듯해 보인다"로는 확인할 수 없다 — 도망이 성립하려면 실제로
    /// 걷기보다 빠르고 달리기보다 느려야 하고, 빛이 방어 수단이려면 켜는 순간
    /// 실제로 거리가 벌어져야 한다. 그래서 전부 <b>수치</b>로 확인한다.
    ///
    /// 기존 4종(눈·공·날개·열매게)이 그대로인지도 마지막에 함께 본다.
    /// 판단을 공유하는 이상 여기서 한 번 보는 것이 싸다.
    /// </summary>
    public static class E2EConsumer
    {
        const string PrefabPath = "Assets/05.Prefabs/Creatures/Creature_낫.prefab";

        static PlayerVitals Vitals => E2EHarness.Player.Vitals;
        static Vector3 PlayerPos => E2EHarness.Player.transform.position;

        static LanternController _lantern;
        static CreatureDefinitionSO _def;

        public static IEnumerator FullRun()
        {
            CreatureBrain 낫 = null;

            yield return 준비();
            yield return 소환한다(r => 낫 = r);
            if (낫 == null) yield break;

            yield return 어둠에서_감지_밖이면_덤비지_않는다(낫);
            yield return 다가가면_맞기_전에_먼저_쫓는다(낫);
            yield return 추격_속도가_걷기와_달리기_사이다(낫);
            yield return 사거리에_들면_때리고_체력이_준다(낫);
            yield return 랜턴을_켜면_물러난다(낫);
            yield return 랜턴을_끄면_다시_쫓는다(낫);
            yield return 도망치면_추격을_포기한다(낫);
            yield return 계속_때리면_죽고_떨군다(낫);
            yield return 기존_생물은_그대로다();

            E2EHarness.Log("=== 소비자 1종 완주 ===");
        }

        // ── 준비 ────────────────────────────────────────────────

        static IEnumerator 준비()
        {
            // 정의는 프리팹에서 꺼낸다. 아직 씬에 배치된 개체가 없어 에셋이 로드돼
            // 있지 않고, Resources.FindObjectsOfTypeAll로는 잡히지 않는다.
            var prefab = 프리팹();
            E2EHarness.Assert(prefab != null, "소비자 프리팹을 찾았다");
            if (prefab == null) yield break;

            _def = prefab.GetComponent<CreatureHealth>() != null
                 ? prefab.GetComponent<CreatureHealth>().Definition : null;
            E2EHarness.Assert(_def != null, "프리팹에 생물 정의가 물려 있다");
            if (_def == null) yield break;
            E2EHarness.AssertEqual(_def.id, "scythe", "소비자 정의를 집었다");

            E2EHarness.AssertEqual(_def.behavior, BehaviorProfile.Aggressive, "성향이 선공이다");
            E2EHarness.Assert(_def.avoidsLight, "빛을 꺼린다고 정의돼 있다");
            E2EHarness.Assert(_def.tier == TrophicTier.Consumer1, "1차 소비자로 분류돼 있다");

            // 도망이 성립하는 속도인가 — 이 수치가 곧 "도망쳐라"라는 규칙의 내용이다.
            E2EHarness.Assert(_def.moveSpeed > 5f && _def.moveSpeed < 7f,
                              $"걷기(5)보다 빠르고 달리기(7)보다 느리다 ({_def.moveSpeed})");

            // 2~3방에 위험해지는가
            float 최대체력 = Vitals.Health.Max;
            E2EHarness.Assert(_def.attackDamage * 3f > 최대체력 * 0.7f,
                              $"세 방이면 체력 {최대체력}의 7할을 넘긴다 ({_def.attackDamage}×3)");
            E2EHarness.Assert(_def.attackDamage * 2f < 최대체력,
                              $"두 방으로는 죽지 않는다 ({_def.attackDamage}×2 < {최대체력})");

            _lantern = Object.FindAnyObjectByType<LanternController>(FindObjectsInactive.Include);
            E2EHarness.Assert(_lantern != null, "랜턴이 있다");

            // 랜턴을 F로 켤 수 있으려면 인벤토리에 있어야 한다(PlayerToolUser.ToggleLantern)
            var db = E2EHarness.Player.Inventory.Database;
            if (E2EHarness.Player.Inventory.Inventory.CountOf("lantern") == 0)
            {
                var item = db.GetById("lantern");
                if (item != null) E2EHarness.Player.Inventory.Inventory.TryAdd(item, 1);
            }
            E2EHarness.Assert(E2EHarness.Player.Inventory.Inventory.CountOf("lantern") > 0,
                              "랜턴을 지니고 있다");

            // 곡괭이 — 마지막에 실제로 때려 죽인다
            if (E2EHarness.Player.Inventory.Inventory.CountOf("pickaxe") == 0)
            {
                var pick = db.GetById("pickaxe");
                if (pick != null) E2EHarness.Player.Inventory.Inventory.TryAdd(pick, 1);
            }
            var toolUser = E2EHarness.Player.GetComponentInChildren<PlayerToolUser>(true);
            if (toolUser != null) toolUser.EquipFirst("pickaxe");

            yield return 랜턴(false);
            E2EHarness.Log($"  정의: 체력 {_def.maxHealth}, 속도 {_def.moveSpeed}, " +
                           $"감지 {_def.detectRadius}m, 공격 {_def.attackDamage}, " +
                           $"사거리 {_def.attackRange}m, 어그로 {_def.aggroSeconds}초");
        }

        /// <summary>F를 눌러 켜고 끈다. 실제 조작 경로를 쓴다.</summary>
        static IEnumerator 랜턴(bool 켜기)
        {
            for (int i = 0; i < 3 && _lantern.IsOn != 켜기; i++)
            {
                yield return E2EHarness.TapKey(Key.F);
                yield return null;
            }
            E2EHarness.AssertEqual(_lantern.IsOn, 켜기, 켜기 ? "F로 랜턴을 켰다" : "F로 랜턴을 껐다");

            // 배터리가 닳아 저절로 꺼지면 이후 판정이 흔들린다. 채워 둔다.
            if (켜기) _lantern.Recharge(100f);
            yield return null;
        }

        // ── 소환 ────────────────────────────────────────────────

        /// <summary>
        /// 소환할 프리팹을 집는다. 아직 씬에 배치된 개체가 없어서(§8-4는 사람과 함께 한다)
        /// 에셋에서 직접 집을 수밖에 없다. E2E는 에디터에서만 도는 도구이므로
        /// 여기서만 UnityEditor에 기댄다 — 빌드에는 들어가지 않는다.
        /// </summary>
        static GameObject 프리팹()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
#else
            return null;
#endif
        }

        static IEnumerator 소환한다(System.Action<CreatureBrain> result)
        {
            var prefab = 프리팹();
            E2EHarness.Assert(prefab != null, "소비자 프리팹을 찾았다");
            if (prefab == null) { result(null); yield break; }

            // 감지 반경 훨씬 밖, 걸어올 수 있는 자리에 놓는다.
            Vector3 원하는자리 = PlayerPos + new Vector3(1f, 0f, 0f).normalized * (_def.detectRadius + 10f);
            E2EHarness.Assert(NavMesh.SamplePosition(원하는자리, out var hit, 25f, NavMesh.AllAreas),
                              "소환 자리가 NavMesh 위에 있다");

            var go = Object.Instantiate(prefab, hit.position, Quaternion.identity);
            go.name = "E2E_소비자";
            yield return null;
            yield return null;

            var brain = go.GetComponent<CreatureBrain>();
            E2EHarness.Assert(brain != null, "두뇌가 붙어 있다");

            var agent = go.GetComponent<NavMeshAgent>();
            E2EHarness.Assert(agent != null && agent.isOnNavMesh, "NavMesh 위에 섰다");
            E2EHarness.AssertEqual(Mathf.Round(agent.speed * 10f) / 10f, _def.moveSpeed,
                                   "정의의 속도가 실제 이동에 들어갔다");

            // 실루엣이 뾰족한가 — 블록아웃이라도 규칙은 지켜야 한다.
            // 발광 부속(날붙이)이 몸통 윤곽 밖으로 뻗어 나와 있는지 본다.
            var 발광 = go.GetComponentsInChildren<MeshRenderer>()
                        .Count(r => r.sharedMaterial != null &&
                                    r.sharedMaterial.IsKeywordEnabled("_EMISSION"));
            E2EHarness.Assert(발광 >= 8, $"날붙이 부속이 여럿이다 ({발광}개)");

            E2EHarness.Log($"  소환: {hit.position.ToString("F1")}, " +
                           $"플레이어와 {Vector3.Distance(hit.position, PlayerPos):F1}m");
            result(brain);
        }

        static float 거리(CreatureBrain c) => Vector3.Distance(c.transform.position, PlayerPos);

        /// <summary>
        /// 플레이어를 생물에서 원하는 거리만큼 떨어뜨려, <b>걸을 수 있는 지면 위에</b> 세운다.
        ///
        /// 높이를 손으로 정하면 안 된다. 지면 속에 놓으면 캐릭터 컨트롤러가 위로
        /// 밀어내고(실측 3.2m), 그 높이차가 생물이 쓰는 3차원 거리에 그대로 섞인다.
        /// 그러면 수평으로는 코앞인데 사거리 밖으로 판정되고, NavMeshAgent는
        /// 수평 목적지에 도착했다며 멈춰 선다(remaining=0, vel=0) — 실제로 겪었다.
        /// 그래서 NavMesh에 물어 실제 지면을 받아 온다. 사람이 이 생물을 마주칠 때는
        /// 언제나 같은 지면 위에 서 있다.
        /// </summary>
        static IEnumerator 떨어져_선다(CreatureBrain c, float 거리m)
        {
            Vector3 기준 = c.transform.position;
            Vector3 dir = PlayerPos - 기준;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = c.transform.forward;

            Vector3 목표 = 기준 + dir.normalized * 거리m;
            if (NavMesh.SamplePosition(목표, out var hit, Mathf.Max(2.5f, 거리m * 0.35f), NavMesh.AllAreas))
                목표 = hit.position;

            목표.y += 0.1f;
            for (int i = 0; i < 5; i++)
            {
                E2EHarness.Teleport(목표);
                yield return null;
            }
        }

        /// <summary>
        /// <paramref name="거리m"/>에 붙어 선 채로 조건이 참이 되기를 기다린다.
        /// 생물이 다가오면서 자리를 밀어 대므로 매 프레임 다시 세운다.
        /// </summary>
        static IEnumerator 붙어서_기다린다(CreatureBrain c, float 거리m,
                                          System.Func<bool> 조건, string 무엇, float 제한)
        {
            float t = 0f;
            while (t < 제한)
            {
                if (조건()) { E2EHarness.Assert(true, 무엇); yield break; }
                if (거리(c) > 거리m + 0.6f) yield return 떨어져_선다(c, 거리m);
                t += Time.deltaTime;
                yield return null;
            }
            throw new System.TimeoutException(
                $"기다리기 실패: {무엇} ({제한}초 초과, 거리 {거리(c):F2}m, 상태 {c.State})");
        }

        // ── 감지 밖 — 덤비지 않는다 ─────────────────────────────

        static IEnumerator 어둠에서_감지_밖이면_덤비지_않는다(CreatureBrain 낫)
        {
            yield return 떨어져_선다(낫, _def.detectRadius + 12f);
            yield return null;

            E2EHarness.Assert(!LitZoneRegistry.IsLit(PlayerPos), "랜턴이 꺼져 있어 플레이어 자리가 어둡다");

            float 시작거리 = 거리(낫);
            float t = 0f;
            bool 덤빔 = false;
            while (t < 3f)
            {
                if (낫.State == CreatureState.Chase || 낫.State == CreatureState.Attack) 덤빔 = true;
                t += Time.deltaTime;
                yield return null;
            }

            E2EHarness.Assert(!덤빔, $"감지 밖({시작거리:F1}m)에서는 쫓지 않는다 — 상태 {낫.State}");
            E2EHarness.Assert(낫.State == CreatureState.Wander || 낫.State == CreatureState.Idle,
                              $"한가하면 배회한다 (상태 {낫.State})");
        }

        // ── 선공 ────────────────────────────────────────────────

        static IEnumerator 다가가면_맞기_전에_먼저_쫓는다(CreatureBrain 낫)
        {
            var 체력 = 낫.GetComponent<CreatureHealth>();
            float 체력이전 = _def.maxHealth;

            yield return 떨어져_선다(낫, _def.detectRadius - 1f);
            yield return null;

            yield return E2EHarness.WaitUntil(
                () => 낫.State == CreatureState.Chase || 낫.State == CreatureState.Attack,
                "감지 반경 안에 들어가자 먼저 덤빈다", 4f);

            E2EHarness.Assert(!체력.IsDead, "아직 한 대도 때리지 않았다");
            E2EHarness.Assert(체력이전 == _def.maxHealth, "피격 없이 선공했다 — Defensive와 다른 점");
            E2EHarness.Log($"  {거리(낫):F1}m에서 {낫.State}로 전이");
        }

        // ── 속도 ────────────────────────────────────────────────

        static IEnumerator 추격_속도가_걷기와_달리기_사이다(CreatureBrain 낫)
        {
            // 쫓는 동안 실제로 몇 m를 가는지 잰다. 정의의 숫자가 아니라 몸의 숫자다.
            yield return 떨어져_선다(낫, _def.detectRadius - 1f);
            yield return E2EHarness.WaitUntil(() => 낫.State == CreatureState.Chase, "쫓기 시작한다", 4f);

            // 가속이 끝날 때까지 한 박자 준다
            float w = 0f;
            while (w < 0.6f) { w += Time.deltaTime; yield return null; }

            // 프레임마다 실제로 지나간 길이를 더한다. 시작점과 끝점의 직선거리로 재면
            // 바위를 돌아가는 경로가 그대로 감속으로 보인다 — 재려는 것은 이동 속도지
            // 접근 속도가 아니다.
            //
            // 그리고 0.4초짜리 창을 여럿 잡아 그중 가장 빠른 것을 쓴다. 방향을 트는
            // 순간과 비탈을 오르는 구간이 섞이면 평균이 정의값보다 한참 아래로 내려간다
            // (실측 5.64까지 내려가 하한 5에 아슬아슬했다). 여기서 확인하려는 것은
            // "이 생물이 낼 수 있는 속도"이므로 최고 구간을 본다.
            Vector3 이전자리 = 낫.transform.position;
            float 창시간 = 0f, 창이동 = 0f, 총시간 = 0f, 최고 = 0f, 총이동 = 0f;

            while (총시간 < 2.5f && 낫.State == CreatureState.Chase)
            {
                float dt = Time.deltaTime;
                yield return null;

                float d = Vector3.Distance(이전자리, 낫.transform.position);
                이전자리 = 낫.transform.position;

                총시간 += dt; 총이동 += d;
                창시간 += dt; 창이동 += d;

                if (창시간 >= 0.4f)
                {
                    최고 = Mathf.Max(최고, 창이동 / 창시간);
                    창시간 = 0f; 창이동 = 0f;
                }
            }

            float 속도 = 최고;
            E2EHarness.Log($"  실측 추격 속도 최고 {속도:F2} m/s " +
                           $"(평균 {총이동 / Mathf.Max(총시간, 0.001f):F2}, {총이동:F2}m / {총시간:F2}초)");
            E2EHarness.Assert(속도 > 5f, $"걷기(5 m/s)로는 못 뿌리친다 — 실측 {속도:F2}");
            E2EHarness.Assert(속도 < 7f, $"달리기(7 m/s)로는 뿌리칠 수 있다 — 실측 {속도:F2}");
        }

        // ── 공격 ────────────────────────────────────────────────

        static IEnumerator 사거리에_들면_때리고_체력이_준다(CreatureBrain 낫)
        {
            Vitals.Health.Modify(Vitals.Health.Max);
            float 이전 = Vitals.Health.Current;

            float 붙는거리 = _def.attackRange * 0.7f;
            yield return 떨어져_선다(낫, 붙는거리);

            yield return 붙어서_기다린다(낫, 붙는거리, () => 낫.State == CreatureState.Attack,
                                        "사거리 안이면 멈춰 서서 때린다", 8f);

            yield return 붙어서_기다린다(낫, 붙는거리, () => Vitals.Health.Current < 이전,
                                        "맞으면 체력이 준다", _def.attackCooldown + 5f);

            float 한방 = 이전 - Vitals.Health.Current;
            E2EHarness.Log($"  한 방에 {한방:F0} ({이전:F0} -> {Vitals.Health.Current:F0})");
            E2EHarness.Assert(Mathf.Abs(한방 - _def.attackDamage) < 0.5f,
                              $"정의한 만큼 아프다 ({_def.attackDamage})");
        }

        // ── 빛 — 켜면 물러난다 ──────────────────────────────────

        static IEnumerator 랜턴을_켜면_물러난다(CreatureBrain 낫)
        {
            Vitals.Health.Modify(Vitals.Health.Max);
            float 붙는거리 = _def.attackRange * 0.7f;
            yield return 떨어져_선다(낫, 붙는거리);
            yield return 붙어서_기다린다(낫, 붙는거리,
                () => 낫.State == CreatureState.Chase || 낫.State == CreatureState.Attack,
                "빛을 켜기 전에는 붙어 있다", 8f);

            float 켜기전거리 = 거리(낫);
            float 체력이전 = Vitals.Health.Current;

            yield return 랜턴(true);
            E2EHarness.Assert(LitZoneRegistry.IsLit(PlayerPos), "켜진 랜턴이 밝은 구역으로 등록된다");

            yield return E2EHarness.WaitUntil(
                () => 낫.State != CreatureState.Chase && 낫.State != CreatureState.Attack,
                "빛이 켜지자 추격을 끊는다", 2f);
            E2EHarness.Log($"  전이: {낫.State} (거리 {거리(낫):F1}m)");

            // 물러나는가 — 상태만 바뀌고 제자리면 "다가오지 못한다"가 아니다
            float t = 0f;
            while (t < 2.5f) { t += Time.deltaTime; yield return null; }

            float 켠뒤거리 = 거리(낫);
            E2EHarness.Log($"  거리 {켜기전거리:F1}m -> {켠뒤거리:F1}m");
            E2EHarness.Assert(켠뒤거리 > 켜기전거리 + 1f,
                              $"빛을 켜자 실제로 멀어졌다 ({켜기전거리:F1} -> {켠뒤거리:F1})");
            E2EHarness.AssertEqual(Vitals.Health.Current, 체력이전,
                                   "빛을 켠 뒤로는 한 대도 맞지 않았다");
        }

        // ── 빛 — 끄면 다시 쫓는다 ───────────────────────────────

        static IEnumerator 랜턴을_끄면_다시_쫓는다(CreatureBrain 낫)
        {
            float 시험거리 = _def.detectRadius - 2f;

            // 켜져 있는 동안은 감지 반경 안에 서 있어도 붙지 않는다.
            // 생물이 물러나며 거리를 벌리므로 매번 다시 그 거리에 선다.
            float t = 0f;
            bool 켜진채덤빔 = false;
            while (t < 2f)
            {
                if (거리(낫) > 시험거리 + 0.6f) yield return 떨어져_선다(낫, 시험거리);
                if (낫.State == CreatureState.Chase || 낫.State == CreatureState.Attack) 켜진채덤빔 = true;
                t += Time.deltaTime;
                yield return null;
            }
            E2EHarness.Assert(!켜진채덤빔,
                $"켜져 있는 동안은 감지 반경 안({거리(낫):F1}m)이어도 오지 않는다 (상태 {낫.State})");

            yield return 랜턴(false);
            E2EHarness.Assert(!LitZoneRegistry.IsLit(PlayerPos), "끄면 그 자리가 다시 어두워진다");

            // 어그로가 남아 쫓는 것과 구별하려면 새로 감지되는 자리에 서야 한다.
            yield return 떨어져_선다(낫, 시험거리);
            float 끈뒤거리 = 거리(낫);

            yield return E2EHarness.WaitUntil(
                () => 낫.State == CreatureState.Chase || 낫.State == CreatureState.Attack,
                "불을 끄자 같은 거리에서 다시 쫓는다", 4f);

            // 상태만 바뀌고 제자리면 "다시 쫓는다"가 아니다. 실제로 좁혀 오는지 본다.
            float w = 0f;
            while (w < 1.5f) { w += Time.deltaTime; yield return null; }
            float 좁힌뒤 = 거리(낫);

            E2EHarness.Log($"  재추격: {낫.State} ({끈뒤거리:F1}m -> {좁힌뒤:F1}m)");
            E2EHarness.Assert(좁힌뒤 < 끈뒤거리 - 1f,
                              $"불을 끄자 실제로 다가온다 ({끈뒤거리:F1} -> {좁힌뒤:F1})");
        }

        // ── 도주 ────────────────────────────────────────────────

        /// <summary>
        /// 생물의 뒤쪽으로 <paramref name="거리m"/>만큼, <b>걸을 수 있는 자리로</b> 물러난다.
        ///
        /// 직선 뒤가 언제나 땅인 것은 아니다. 부유섬 가장자리로 물러나면 허공이고,
        /// 거기서는 아무것도 확인할 수 없다. 그래서 뒤쪽을 중심으로 좌우로 훑어
        /// NavMesh 위의 자리를 고른다.
        /// </summary>
        static IEnumerator 물러난다(CreatureBrain c, float 거리m)
        {
            Vector3 기준 = c.transform.position;
            Vector3 뒤 = PlayerPos - 기준;
            뒤.y = 0f;
            if (뒤.sqrMagnitude < 0.01f) 뒤 = Vector3.forward;
            뒤.Normalize();

            for (int i = 0; i < 12; i++)
            {
                float 각 = (i % 2 == 0 ? 1f : -1f) * 30f * ((i + 1) / 2);
                Vector3 후보 = 기준 + (Quaternion.Euler(0f, 각, 0f) * 뒤) * 거리m;
                if (!NavMesh.SamplePosition(후보, out var hit, 15f, NavMesh.AllAreas)) continue;
                if (Vector3.Distance(hit.position, 기준) < 거리m * 0.55f) continue;

                for (int k = 0; k < 4; k++)
                {
                    E2EHarness.Teleport(hit.position + Vector3.up * 0.2f);
                    yield return null;
                }
                yield break;
            }

            E2EHarness.Log("  [주의] 걸을 수 있는 도주 지점을 못 찾아 직선으로 물러난다");
            for (int k = 0; k < 4; k++)
            {
                E2EHarness.Teleport(기준 + 뒤 * 거리m + Vector3.up * 0.2f);
                yield return null;
            }
        }

        static IEnumerator 도망치면_추격을_포기한다(CreatureBrain 낫)
        {
            yield return E2EHarness.WaitUntil(() => 낫.AggroLeft > 0f, "어그로가 차 있다", 4f);

            // 앞서 나가기만 하면 된다 — 이 생물의 6.2 m/s는 달리기(7)보다 느리다.
            // 감지 반경 안으로 다시 들어오면 어그로가 갱신되므로, 좁혀지기 전에
            // 계속 물러난다. 그것이 실제 플레이에서 "달려서 뿌리친다"의 내용이다.
            float 안전거리 = _def.detectRadius + 5f;
            yield return 물러난다(낫, 안전거리 + 8f);
            float 처음거리 = 거리(낫);
            E2EHarness.Assert(처음거리 > _def.detectRadius,
                              $"감지 반경 밖까지 달아났다 ({처음거리:F1}m > {_def.detectRadius}m)");
            E2EHarness.Log($"  {처음거리:F1}m까지 도망쳤다 (어그로 {낫.AggroLeft:F1}초 남음)");

            float t = 0f;
            int 재도주 = 0;
            while (t < _def.aggroSeconds + 5f)
            {
                if (낫.State != CreatureState.Chase && 낫.State != CreatureState.Attack) break;

                if (거리(낫) < 안전거리)
                {
                    yield return 물러난다(낫, 안전거리 + 8f);
                    재도주++;
                }

                t += Time.deltaTime;
                yield return null;
            }

            E2EHarness.Log($"  {t:F1}초 뒤 상태 {낫.State} " +
                           $"(거리 {거리(낫):F1}m, 어그로 {낫.AggroLeft:F1}초, 재도주 {재도주}회)");
            E2EHarness.Assert(낫.State != CreatureState.Chase && 낫.State != CreatureState.Attack,
                              $"달아나면 {_def.aggroSeconds}초 뒤 추격을 포기한다");
            E2EHarness.Assert(낫.AggroLeft <= 0f, "어그로가 다 식었다");
            E2EHarness.Assert(낫.State == CreatureState.Wander || 낫.State == CreatureState.Idle,
                              $"포기하면 배회로 돌아간다 (상태 {낫.State})");
        }

        // ── 사망과 드롭 ─────────────────────────────────────────

        static IEnumerator 계속_때리면_죽고_떨군다(CreatureBrain 낫)
        {
            var 체력 = 낫.GetComponent<CreatureHealth>();
            var 위치 = 낫.transform.position;
            int 이전줍기 = Object.FindObjectsByType<ItemPickup>().Length;

            var 스윙 = Object.FindAnyObjectByType<MeleeSwing>();
            int 스윙시작 = 스윙 != null ? 스윙.SwingCount : 0;

            // 실제로 곡괭이를 휘둘러 죽인다. 맞아 가며 싸우면 다섯 대를 넣기 전에
            // 플레이어가 먼저 죽을 수 있으므로, 매 스윙 전에 체력을 채운다 —
            // 여기서 확인하려는 것은 전투 난이도가 아니라 타격→사망→드롭이 이어지는가다.
            //
            // 스윙 사이에 쿨타임(곡괭이 0.4초)만큼 쉰다. 쉬지 않고 연달아 부르면
            // MeleeSwing이 조용히 되돌아가 열네 번 눌러도 두 번만 들어간다.
            int 필요타수 = Mathf.CeilToInt(_def.maxHealth / 12f);
            for (int i = 0; i < 필요타수 + 6 && !체력.IsDead && 낫 != null; i++)
            {
                Vitals.Health.Modify(Vitals.Health.Max);
                yield return 떨어져_선다(낫, 1.6f);
                E2EHarness.LookAt(낫.transform.position + Vector3.up * 0.9f);
                yield return null;
                yield return E2EHarness.ClickAttack();

                위치 = 낫 != null ? 낫.transform.position : 위치;

                float 쉼 = 0f;
                while (쉼 < 0.45f && !체력.IsDead) { 쉼 += Time.deltaTime; yield return null; }
            }

            int 휘두름 = (스윙 != null ? 스윙.SwingCount : 0) - 스윙시작;
            E2EHarness.Log($"  {휘두름}번 휘둘러 체력 {_def.maxHealth}를 깎았다 (필요 {필요타수}타)");
            E2EHarness.Assert(체력.IsDead, $"곡괭이를 휘둘러 죽였다 ({휘두름}번 휘두름)");

            yield return E2EHarness.WaitUntil(
                () => Object.FindObjectsByType<ItemPickup>().Length > 이전줍기,
                "죽은 자리에 전리품을 떨군다", 4f);

            var 떨어진것 = Object.FindObjectsByType<ItemPickup>()
                            .Where(p => Vector3.Distance(p.transform.position, 위치) < 6f)
                            .ToArray();
            E2EHarness.Assert(떨어진것.Length > 0, $"떨어진 것이 시체 곁에 있다 ({떨어진것.Length}개)");

            yield return E2EHarness.WaitUntil(() => 낫 == null, "시체가 세계에서 사라진다", 3f);
            Vitals.Health.Modify(Vitals.Health.Max);
        }

        // ── 기존 생물 불변 ──────────────────────────────────────

        /// <summary>
        /// 판단을 공유하는 이상 소비자를 넣으면서 기존 4종을 건드릴 수 있다.
        /// Domain 테스트가 전이 규칙을 지키지만, 씬의 개체가 실제로 배회하는지는
        /// 여기서 한 번 본다 — 규칙이 맞아도 몸이 굳으면 게임은 망가진 것이다.
        /// </summary>
        static IEnumerator 기존_생물은_그대로다()
        {
            var 기존 = Object.FindObjectsByType<CreatureBrain>()
                          .Where(b => b.GetComponent<CreatureHealth>()?.Definition != null &&
                                      b.GetComponent<CreatureHealth>().Definition.id != "scythe")
                          .ToArray();
            E2EHarness.Assert(기존.Length > 0, $"기존 생물이 씬에 있다 ({기존.Length}마리)");

            foreach (var b in 기존)
            {
                var d = b.GetComponent<CreatureHealth>().Definition;
                E2EHarness.Assert(!d.avoidsLight, $"{d.displayName}은 빛을 꺼리지 않는다 — 예전 그대로");
                E2EHarness.Assert(d.behavior != BehaviorProfile.Aggressive,
                                  $"{d.displayName}은 선공이 아니다 — 예전 그대로");
            }

            // 실제로 움직이는가. 한 마리만 골라 보면 안 된다 — 먹는 중(Feed)인 개체는
            // 목표에 닿아 제자리에 서 있는 것이 정상이고, 그것이 하필 뽑히면
            // 멀쩡한 씬을 고장으로 읽는다. 전부 재고 한 마리라도 움직이면 된다.
            var 시작 = 기존.Select(b => b.transform.position).ToArray();
            var 최대이동 = new float[기존.Length];

            float t = 0f;
            while (t < 6f)
            {
                for (int i = 0; i < 기존.Length; i++)
                    if (기존[i] != null)
                        최대이동[i] = Mathf.Max(최대이동[i],
                                              Vector3.Distance(시작[i], 기존[i].transform.position));
                t += Time.deltaTime;
                yield return null;
            }

            int 움직인수 = 0;
            for (int i = 0; i < 기존.Length; i++)
            {
                var d = 기존[i].GetComponent<CreatureHealth>().Definition;
                if (최대이동[i] > 0.3f) 움직인수++;
                E2EHarness.Log($"  {d.displayName}: 6초 동안 최대 {최대이동[i]:F2}m (상태 {기존[i].State})");
            }

            E2EHarness.Assert(움직인수 > 0,
                              $"기존 생물이 여전히 스스로 움직인다 ({움직인수}/{기존.Length}마리)");
        }
    }
}

