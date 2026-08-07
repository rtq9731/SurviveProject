using System.Collections;
using UnityEngine;
using Survive.Combat;
using Survive.Creatures;
using Survive.Items;
using Survive.Progression;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// <b>칠흑 속에서 랜턴을 꺼야 유물에 닿는다</b> (세계관 §10, 스펙 §8).
    ///
    /// 이 시나리오가 재는 것은 하나다 — <b>이 게임에서 가장 무서운 행동이 곧 진행
    /// 조건인가.</b> 그것이 참이려면 둘이 동시에 성립해야 한다.
    /// <list type="number">
    /// <item><b>켠 채로는 얻지 못한다.</b> 안전한 거리에서 불을 켜고 기다리는 길이
    ///       열려 있으면 무서울 일이 없다</item>
    /// <item><b>끄면 얻는다.</b> 끄는 것이 벌이 아니라 <b>길</b>이어야 한다</item>
    /// </list>
    ///
    /// <b>대조가 본문이다.</b> 둘을 같은 무대·같은 자리·같은 시각에서 재고 <b>랜턴
    /// 하나만</b> 바꾼다. 그래야 갈린 것이 랜턴이라고 말할 수 있다.
    ///
    /// <b>밤에 돈다.</b> 낫은 밤에만 나오므로(<see cref="ScytheHabitat.IsAbroad(float)"/>)
    /// 낮에는 애초에 물러나 있어 물가에서 만날 수 없다. 시계를 세워 두고 잰다.
    /// </summary>
    public static class E2EScytheNight
    {
        const string 낫프리팹 = "Assets/05.Prefabs/Creatures/Creature_낫.prefab";

        /// <summary>굴림 간격을 줄인다. 확률과 pity는 그대로다(E2ERelicSupply와 같은 이유).</summary>
        const float 굴림간격 = 1f;

        /// <summary>한 판을 기다리는 한계(초). 평균 간격의 넉넉한 배수다.</summary>
        const float 한판 = 40f;

        static CreatureDefinitionSO _def;
        static CreatureBrain _낫;
        static LanternController _lantern;
        static DayNightService _시계;
        static float _액면;
        static Vector3 _바다;

        static Inventory Inv => E2EHarness.Player.Inventory.Inventory;
        static Vector3 사람자리 => E2EHarness.Player.transform.position;

        public static IEnumerator FullRun()
        {
            yield return 준비();
            if (_def == null) yield break;

            yield return 밤으로_돌린다();
            yield return 낫을_세운다();
            if (_낫 == null) yield break;

            yield return 켠_채로는_얻지_못한다();
            yield return 끄면_얻는다();
            yield return 낮에는_물러나_있다();

            yield return 치운다();
            E2EHarness.Log("=== 낫은 밤에 다닌다 완주 ===");
        }

        // ── 다른 낫 시나리오가 함께 쓰는 창구 ──────────────────

        /// <summary>
        /// 시계를 <b>한밤중에 세운다.</b> 낫 시나리오들이 준비 단계에서 부른다.
        ///
        /// <b>왜 필요해졌는가.</b> 2026-08-07에 낫이 밤에만 나오게 됐다
        /// (<see cref="ScytheHabitat.IsAbroad(float)"/>). 시계를 흐르게 두면 판마다
        /// 낮일 수도 밤일 수도 있고, 낮에 걸린 판은 낫이 물러나 있어
        /// <b>규칙이 지켜졌는데도</b> 시나리오가 실패한다 — 실측으로 「낫 서식」이
        /// 셋 중 둘, 「랜턴 오프셋」이 셋 중 하나에서 그렇게 깨졌다.
        /// 밤은 그 시나리오들이 재려는 것의 <b>전제</b>이지 주어가 아니다.
        /// </summary>
        public static void 밤에_세운다()
        {
            var 시계 = DayNightService.Instance;
            if (시계 == null) return;

            // <b>시계를 멈추지 않는다 — 시각만 옮긴다 (2026-08-07 정정).</b>
            //
            // 예전에는 여기서 <c>Frozen = true</c>를 켰다. 그런데 그 스위치는
            // 하루의 시각만 붙드는 것이 아니라 <see cref="Survive.World.WorldClock"/>을
            // 통째로 멈춘다(<c>WorldClock.Paused = Frozen || ...</c>). 그러면 그 시계로
            // 재는 것들이 전부 얼어붙는다 — 채집물 재생이 그것이라, 「발광 군락」이
            // <b>갓이 영영 다시 자라지 않아</b> 깨졌다. 낫 절이 먼저 실패하는 바람에
            // 한동안 가려져 있었다.
            //
            // 붙들 이유도 없다. 밤은 8분 48초이고 시나리오는 1분을 넘지 않으므로,
            // 0.95에 놓으면 해뜰녘까지 324초가 남는다. <b>필요한 것은 "지금이 밤"이지
            // "시간이 멈춘 것"이 아니다.</b> 낮과 밤을 나란히 재는 시나리오만
            // 제 손으로 얼린다(<see cref="밤으로_돌린다"/>).
            시계.SetTimeOfDay(0.95f);

            // <b>제 낫만 무대에 둔다.</b> 이 창구를 쓰는 시나리오들은 낫을 손수
            // 세워 재는 쪽이고, 스포너가 함께 돌면 세계에 낫이 여럿이 된다 —
            // 유물 굴림 수처럼 온 세계가 함께 쓰는 값이 남의 것에도 올라간다.
            // 스포너 자체를 재는 시나리오는 이 뒤에 다시 켠다.
            ScytheSpawner.Suspended = true;
            if (ScytheSpawner.Instance != null) ScytheSpawner.Instance.전부_치운다();
        }

        /// <summary>세워 둔 시계를 되돌린다. 다음 시나리오가 남은 밤을 물려받지 않게.</summary>
        public static void 시계를_되돌린다()
        {
            var 시계 = DayNightService.Instance;
            if (시계 == null) return;

            시계.Frozen = false;
            시계.SetTimeOfDay(DayNightService.StartTimeOfDay);
            ScytheSpawner.Suspended = false;
            Survive.World.WorldClock.Paused = false;
        }

        // ── 준비 ────────────────────────────────────────────────

        static IEnumerator 준비()
        {
            남은것을_치운다();

            var prefab = 프리팹();
            E2EHarness.Assert(prefab != null, "낫 프리팹을 찾았다");
            if (prefab == null) yield break;

            _def = prefab.GetComponent<CreatureHealth>()?.Definition;
            E2EHarness.Assert(_def != null && _def.id == "scythe", "낫 정의를 집었다");
            if (_def == null) yield break;

            _lantern = Object.FindAnyObjectByType<LanternController>(FindObjectsInactive.Include);
            E2EHarness.Assert(_lantern != null, "랜턴이 있다");

            _시계 = DayNightService.Instance;
            E2EHarness.Assert(_시계 != null, "밤낮 시계가 있다");
            if (_시계 == null || _lantern == null) yield break;

            int 잠든생물 = E2EHarness.SleepWildCreatures();
            int 끈광원 = E2EHarness.MuteAmbientLitZones();
            E2EHarness.Log($"  무대 정리: 야생 생물 {잠든생물}마리, 주변 광원 {끈광원}곳");

            var inv = E2EHarness.Player.Inventory;
            if (inv.Inventory.CountOf("lantern") == 0)
            {
                var item = inv.Database.GetById("lantern");
                if (item != null) inv.Inventory.TryAdd(item, 1);
            }

            ScytheWatch.Reset();
            RelicShedder.IntervalOverrideSeconds = 굴림간격;
            RelicShedder.ResetCounters();
            E2EHarness.Player.Vitals.Health.Modify(E2EHarness.Player.Vitals.Health.Max);
            yield return null;
        }

        static GameObject 프리팹()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(낫프리팹);
#else
            return null;
#endif
        }

        /// <summary>
        /// 시계를 <b>한밤중에 세운다.</b> 흐르게 두면 관찰 도중에 해가 떠서
        /// 낫이 물러나고, 그때 실패한 것이 랜턴 때문인지 시각 때문인지 갈리지 않는다.
        /// </summary>
        static IEnumerator 밤으로_돌린다()
        {
            _시계.Frozen = true;
            _시계.SetTimeOfDay(0.95f);
            yield return null;

            E2EHarness.Assert(_시계.IsNight, $"한밤중이다 (시각 {_시계.TimeOfDay:F3})");
            E2EHarness.Assert(ScytheHabitat.IsAbroad(_시계.TimeOfDay), "낫이 나와 있는 시각이다");
        }

        static IEnumerator 낫을_세운다()
        {
            _액면 = 0f;
            var water = Object.FindAnyObjectByType<WaterBody>(FindObjectsInactive.Exclude);
            E2EHarness.Assert(water != null, "세계에 액면이 있다");
            if (water == null) yield break;
            _액면 = water.SurfaceY;

            // 「낫 서식」이 쓰는 것과 같은 찾기다. 어두운 액면 자리를 고른다.
            E2EHarness.Assert(
                E2EScytheHabitat.서식지를_찾는다(사람자리, 6f, 40f, true, out _바다),
                "어두운 액면 자리를 찾았다");
            if (_바다 == Vector3.zero) yield break;

            var go = Object.Instantiate(프리팹(), _바다 + Vector3.up * 0.6f, Quaternion.identity);
            go.name = "E2E_낫밤";
            _낫 = go.GetComponent<CreatureBrain>();
            yield return null;

            E2EHarness.Log($"  낫 {_낫.transform.position.ToString("F1")} (액면 {_액면:F2})");
        }

        // ── 대조 ────────────────────────────────────────────────

        /// <summary>
        /// <b>켠 채로는 얻지 못한다.</b> 감지 밖 · 목격 안이라 예전에는 여기서
        /// 안전하게 유물이 나왔다. 그 길을 막은 것이 이 라운드다.
        /// </summary>
        static IEnumerator 켠_채로는_얻지_못한다()
        {
            E2EHarness.Log("— 랜턴을 켠 채로: 흘리지 않는다 —");

            yield return 자리를_잡는다();
            E2EHarness.LightLantern();
            yield return null;

            E2EHarness.Assert(_lantern.IsOn, "랜턴이 켜져 있다");
            E2EHarness.Assert(LitZoneRegistry.IsLit(사람자리), "사람이 빛 안에 서 있다");
            E2EHarness.Assert(!RelicDropRule.CanShed(true, true), "규칙이 흘리지 말라고 한다");

            var 흘리개 = _낫.GetComponent<RelicShedder>();
            int 이전 = 흘리개 != null ? 흘리개.Shed : 0;
            float t = 0f;
            while (t < 한판)
            {
                if (t % 1.5f < Time.deltaTime) yield return 자리를_잡는다();
                t += Time.deltaTime;
                yield return null;
            }

            int 지금 = 흘리개 != null ? 흘리개.Shed : 0;
            E2EHarness.Log($"  {t:F0}초 동안 이 낫이 흘린 것 {지금 - 이전}개 " +
                           $"(굴림 간격 {굴림간격}초)");
            E2EHarness.AssertEqual(지금, 이전, "랜턴을 켠 채로는 한 개도 얻지 못한다");
        }

        /// <summary>
        /// <b>끄면 얻는다.</b> 같은 무대·같은 자리·같은 시각, 랜턴 하나만 바꾼다.
        /// </summary>
        static IEnumerator 끄면_얻는다()
        {
            E2EHarness.Log("— 랜턴을 끄면: 흘린다 —");

            yield return 자리를_잡는다();
            E2EHarness.DarkenLantern();
            yield return null;

            E2EHarness.Assert(!_lantern.IsOn, "랜턴이 꺼졌다");
            E2EHarness.Assert(!LitZoneRegistry.IsLit(사람자리),
                              "사람이 칠흑 속에 서 있다 — 이 게임에서 가장 무서운 자리다");

            // <b>이 낫이 흘린 것만 센다.</b> 정적 셈은 온 세계가 함께 쓰는 값이라
            // 남의 것에도 올라간다 — 게다가 SleepWildCreatures는 두뇌만 재우므로
            // 잠든 개체도 계속 흘린다. 실측으로 28.1m 떨어진 남의 유물을 세다
            // 셋 중 하나꼴로 깨졌다.
            var 흘리개 = _낫.GetComponent<RelicShedder>();
            E2EHarness.Assert(흘리개 != null, "이 낫에 유물을 흘리는 부품이 붙어 있다");
            if (흘리개 == null) yield break;

            int 이전 = 흘리개.Shed;
            float t = 0f;

            while (t < 한판 && 흘리개.Shed == 이전)
            {
                if (t % 1.5f < Time.deltaTime) yield return 자리를_잡는다();
                t += Time.deltaTime;
                yield return null;
            }

            Vector3 흘린자리 = 흘리개.LastShedPoint;
            E2EHarness.Log($"  {t:F1}초 만에 흘렸다 ({흘리개.LastShedId}) " +
                           $"자리 {흘린자리.ToString("F1")}");
            E2EHarness.Assert(흘리개.Shed > 이전,
                              $"랜턴을 끄니 유물이 나왔다 ({t:F1}초)");

            // 손에 넣는 데까지 간다. 흘린 것과 얻은 것은 다른 일이다.
            var pickup = 떨어진것을_찾는다(흘린자리);

            if (pickup == null)
            {
                int 세계에있는것 = Object.FindObjectsByType<Survive.Interaction.ItemPickup>(
                    FindObjectsInactive.Exclude).Length;
                float 가장가까운 = float.MaxValue;
                foreach (var q in Object.FindObjectsByType<Survive.Interaction.ItemPickup>(
                             FindObjectsInactive.Exclude))
                    가장가까운 = Mathf.Min(가장가까운, Vector3.Distance(q.transform.position, 흘린자리));

                E2EHarness.Log($"  못 찾았다: 세계의 낙하물 {세계에있는것}개, " +
                               $"흘린 자리에서 가장 가까운 것 {가장가까운:F1}m");
            }

            E2EHarness.Assert(pickup != null, "떨어진 유물을 찾았다");
            if (pickup == null) yield break;

            string id = 흘리개.LastShedId;
            int 전 = Inv.CountOf(id);
            yield return E2EScytheHabitat.줍는다(pickup);

            E2EHarness.AssertEqual(Inv.CountOf(id), 전 + 1,
                                   $"칠흑 속에서 {id}를 손에 넣었다 — 가장 무서운 행동이 곧 진행이다");
        }

        /// <summary>
        /// <b>낮에는 물러나 있다.</b> 시계만 돌리고 나머지는 그대로 둔다 —
        /// 갈린 것이 시각이라고 말할 수 있어야 한다.
        /// </summary>
        static IEnumerator 낮에는_물러나_있다()
        {
            E2EHarness.Log("— 낮으로 돌린다: 물러난다 —");

            _시계.SetTimeOfDay(0.5f);
            yield return null;
            yield return null;

            E2EHarness.Assert(!_시계.IsNight, $"한낮이다 (시각 {_시계.TimeOfDay:F3})");
            E2EHarness.Assert(!ScytheHabitat.IsAbroad(_시계.TimeOfDay), "낫이 물러나는 시각이다");

            yield return 자리를_잡는다();
            E2EHarness.DarkenLantern();
            yield return null;

            var 마음 = _낫.GetComponent<ScytheMind>();
            E2EHarness.Assert(마음 != null, "4상태 부품이 붙어 있다");

            // <b>사라지지 않았다는 것부터 본다.</b> 낮에 개체를 지웠다가 밤에 다시
            // 만들면 도감과 관측이 세는 「본 적 있다」가 흔들린다.
            E2EHarness.Assert(_낫 != null && _낫.gameObject.activeInHierarchy,
                              "낮에도 낫은 세계에 남아 있다 — 지운 것이 아니라 물러난 것이다");

            var 흘리개2 = _낫.GetComponent<RelicShedder>();
            int 이전 = 흘리개2 != null ? 흘리개2.Shed : 0;
            float t = 0f;
            var 본상태 = new System.Collections.Generic.HashSet<ScytheState>();
            while (t < 12f)
            {
                if (t % 1.5f < Time.deltaTime) yield return 자리를_잡는다();
                if (마음 != null) 본상태.Add(마음.State);
                t += Time.deltaTime;
                yield return null;
            }

            E2EHarness.Log($"  낮 12초 동안 본 4상태: {string.Join("·", 본상태)} · " +
                           $"흘린 것 {(흘리개2 != null ? 흘리개2.Shed : 0) - 이전}개 · " +
                           $"구역 {_낫.GetComponent<HoverDrifter>()?.Zone}");

            E2EHarness.Assert(!본상태.Contains(ScytheState.Beware) &&
                              !본상태.Contains(ScytheState.Attack),
                              $"낮에는 쫓지도 덤비지도 않는다 (본 것 {string.Join("·", 본상태)})");
        }

        // ── 무대 조작 ───────────────────────────────────────────

        /// <summary>
        /// 감지 밖 · 목격 안에 선다. <b>쫓기면 흘리지 않고 너무 멀면 굴리지도 않는다</b> —
        /// 「낫 서식」·「유물 공급」이 쓰는 것과 같은 띠다. 이 자리에서 랜턴만 바꾸는 것이
        /// 이 시나리오의 대조다.
        /// </summary>
        static IEnumerator 자리를_잡는다()
        {
            Vector3 낫쪽 = _낫.transform.position;
            Vector3 뒤로 = (사람자리 - 낫쪽);
            뒤로.y = 0f;
            if (뒤로.sqrMagnitude < 0.01f) 뒤로 = Vector3.back;

            Vector3 목표 = 낫쪽 + 뒤로.normalized * (_def.detectRadius + 12f);
            목표.y = 사람자리.y;

            E2EHarness.Teleport(목표);
            E2EHarness.LookAt(낫쪽);
            E2EHarness.SyncPhysics();
            yield return null;
        }

        static Survive.Interaction.ItemPickup 떨어진것을_찾는다(Vector3 흘린자리)
        {
            Survive.Interaction.ItemPickup 가장가까운 = null;
            float best = float.MaxValue;

            foreach (var p in Object.FindObjectsByType<Survive.Interaction.ItemPickup>(
                         FindObjectsInactive.Exclude))
            {
                if (p == null) continue;
                float d = Vector3.Distance(p.transform.position, 흘린자리);
                if (d >= best) continue;
                best = d;
                가장가까운 = p;
            }

            return best <= 25f ? 가장가까운 : null;
        }

        static IEnumerator 치운다()
        {
            RelicShedder.IntervalOverrideSeconds = 0f;
            E2EHarness.DarkenLantern();

            if (_시계 != null)
            {
                _시계.Frozen = false;
                _시계.SetTimeOfDay(DayNightService.StartTimeOfDay);
            }

            남은것을_치운다();
            E2EHarness.RestoreWorld();
            yield return null;
        }

        static void 남은것을_치운다()
        {
            foreach (var brain in Object.FindObjectsByType<CreatureBrain>(FindObjectsInactive.Include))
                if (brain != null && brain.gameObject.name.StartsWith("E2E_낫밤"))
                    Object.DestroyImmediate(brain.gameObject);
        }
    }
}
