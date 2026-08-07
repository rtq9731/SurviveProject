using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using DG.Tweening;
using Survive.Combat;
using Survive.Creatures;
using Survive.Interaction;
using Survive.Items;
using Survive.Player;
using Survive.Progression;
using Survive.Vitals;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// 낫의 서식 범위 — <b>바다 전역과 양쪽 섬의 해안선까지</b> (기획서 §4.5 · 스펙 §3).
    ///
    /// 이 규정 하나가 챕터 1의 매듭 셋을 동시에 푼다. 그래서 여기서 재는 것도 셋이다.
    /// <list type="number">
    /// <item><b>평시에 육지로 올라오지 않는다</b> — 내륙과 지하가 안전한 이유.
    ///       육지에 선 사람을 감지해 쫓으면서도 물가에서 멈춰야 한다. 멈추는 것과
    ///       애초에 굳어 있는 것은 다르므로 <b>움직였는데도 안 올라왔다</b>까지 본다</item>
    /// <item><b>해안선에서 유물을 줍는다</b> — 액면 보행 장비 없이. 이것이 성립하지
    ///       않으면 "막을 얻으려면 낫에게 가야 하고, 낫에게 가려면 막이 필요하다"는
    ///       순환이 되살아나 챕터가 그 자리에서 막힌다</item>
    /// <item><b>발령이면 육지에 있을 수 있다</b> — 종막의 압력. 능력이 없어서 안 오는
    ///       것이 아니라 은폐 프로토콜 때문이라는 구분이 값을 하는 자리다. 같은 육지
    ///       좌표에 올려 놓고 <b>태세만 바꿔</b> 가른다.
    ///       <b>무엇이 발령을 거는가는 스펙 §5의 몫이고 여기서는 태세만 세워 본다</b></item>
    /// </list>
    ///
    /// <b>왜 좌표를 코드에 적지 않는가.</b> 섬 메시는 사람이 다시 만든다(스펙 §16).
    /// 지도를 고칠 때마다 이 시나리오가 깨지면 검증이 지도를 붙잡게 된다. 그래서
    /// 무대는 매번 <b>찾는다</b> — 액면 높이를 기준으로 물가와 바다를 실제로 재서 고른다.
    /// </summary>
    public static class E2EScytheHabitat
    {
        const string 낫프리팹 = "Assets/05.Prefabs/Creatures/Creature_낫.prefab";
        const string 보행장비 = "surface_walker";
        const string 막 = "relic_membrane";
        const string 핵 = "relic_core";

        /// <summary>흘리는 간격을 줄인다. 확률과 pity는 그대로다 (E2ERelicSupply와 같은 이유).</summary>
        const float 굴림간격 = 1f;

        /// <summary>액면 위 지형이 이만큼까지면 해안선이다. Domain의 값을 그대로 쓴다.</summary>
        static float 해안선폭 => ScytheHabitat.ShoreRise;

        static Inventory Inv => E2EHarness.Player.Inventory.Inventory;
        static PlayerVitals Vitals => E2EHarness.Player.Vitals;
        static Vector3 사람자리 => E2EHarness.Player.transform.position;

        static LanternController _lantern;
        static CreatureDefinitionSO _def;
        static CreatureBrain _낫;
        static HoverDrifter _몸;
        static float _액면;
        static Vector3 _물가;      // 사람이 서는 자리 (해안선)
        static Vector3 _바다;      // 낫을 세우는 자리 (액면 위)
        static Vector3 _내륙;      // 사람이 물러나는 자리 (육지)
        static Vector3 _올려둘곳;  // 낫을 육지에 올려 놓아 보는 자리 (사람과 겹치지 않게 띄운다)

        public static IEnumerator FullRun()
        {
            yield return 준비();
            yield return 무대를_고른다();
            if (_바다 == Vector3.zero) yield break;

            yield return 낫을_세운다();
            if (_낫 == null) yield break;

            yield return 평시에는_육지로_올라오지_않는다();
            yield return 해안선에서_유물을_줍는다();
            yield return 육지에_둘_수_있는가();

            yield return 치운다();
            E2EHarness.Log("=== 낫 서식 범위 완주 ===");
        }

        // ── 준비 ────────────────────────────────────────────────

        static IEnumerator 준비()
        {
            var prefab = 프리팹();
            E2EHarness.Assert(prefab != null, "낫 프리팹을 찾았다");
            if (prefab == null) yield break;

            _def = prefab.GetComponent<CreatureHealth>()?.Definition;
            E2EHarness.Assert(_def != null && _def.id == "scythe", "낫 정의를 집었다");
            if (_def == null) yield break;

            E2EHarness.AssertEqual(_def.locomotion, LocomotionType.Hovering,
                                   "정의에 부유로 적혀 있다");

            _lantern = Object.FindAnyObjectByType<LanternController>(FindObjectsInactive.Include);
            E2EHarness.Assert(_lantern != null, "랜턴이 있다");

            남은것을_치운다();

            // 낫은 밤에만 나온다(스펙 §8). 밤은 이 시나리오의 전제이지 주어가 아니다.

            E2EScytheNight.밤에_세운다();


            int 잠든생물 = E2EHarness.SleepWildCreatures();
            int 끈광원 = E2EHarness.MuteAmbientLitZones();
            E2EHarness.Log($"  무대 정리: 야생 생물 {잠든생물}마리, 주변 광원 {끈광원}곳 " +
                           "(재려는 것은 낫 하나의 자리다)");

            var db = E2EHarness.Player.Inventory.Database;
            if (Inv.CountOf("lantern") == 0)
            {
                var item = db.GetById("lantern");
                if (item != null) Inv.TryAdd(item, 1);
            }

            // 이 시나리오의 핵심 조건 — 액면 보행 장비 없이 유물에 닿아야 한다.
            Inv.TryRemove(보행장비, Inv.CountOf(보행장비));
            E2EHarness.AssertEqual(Inv.CountOf(보행장비), 0,
                                   "액면 보행 장비를 지니고 있지 않다 — 순환이 풀리는지 보는 것이다");

            RelicShedder.ResetCounters();
            RelicShedder.IntervalOverrideSeconds = 굴림간격;

            yield return 랜턴(false);
            Vitals.Health.Modify(Vitals.Health.Max);
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

        // ── 무대 고르기 ─────────────────────────────────────────

        /// <summary>
        /// 시작 지점 둘레를 훑어 <b>바다 · 물가 · 내륙</b> 셋을 찾는다.
        ///
        /// 셋이 서로 가까워야 한 시나리오 안에서 걸어 오갈 수 있고, 물가와 내륙이
        /// 갈려야 "여기까지는 오고 저기부터는 안 온다"를 잴 수 있다.
        /// </summary>
        static IEnumerator 무대를_고른다()
        {
            _바다 = Vector3.zero;
            _물가 = Vector3.zero;
            _내륙 = Vector3.zero;

            Vector3 원점 = 사람자리;
            E2EHarness.Assert(WaterBody.TryGetSurfaceAt(new Vector3(원점.x, 0f, 원점.z), out _액면),
                              "세계에 액면이 있다");
            E2EHarness.Log($"  액면 높이 {_액면:F2} (해안선 띠 {_액면:F2}~{_액면 + 해안선폭:F2})");

            // <b>내륙부터 고른다.</b> 셋 중 가장 까다로운 조건이 여기 붙기 때문이다 —
            // 걸을 수 있고, 어둡고, 발밑 기둥이 통째로 육지여야 한다. 물가와 바다는
            // 어느 해안선에나 널려 있으니 내륙이 잡힌 다음에 그 곁에서 찾으면 된다.
            var 내륙후보 = new List<Vector3>();
            for (float r = 3f; r <= 90f && 내륙후보.Count < 40; r += 3f)
            {
                for (int a = 0; a < 24; a++)
                {
                    Vector3 p = 원점 + Quaternion.Euler(0f, a * 15f, 0f) * Vector3.forward * r;
                    if (내륙인가(p, out var 자리)) 내륙후보.Add(자리);
                }
            }

            E2EHarness.Assert(내륙후보.Count > 0, "어두운 육지를 찾았다");
            if (내륙후보.Count == 0) yield break;

            foreach (var 내륙 in 내륙후보.OrderBy(p => Vector3.Distance(p, 원점)))
            {
                // 낫이 쫓아올 수 있는 거리 안에 제 영역이 있어야 한다. 감지 반경 밖이면
                // "못 올라온 것"과 "부르지 않은 것"이 구별되지 않는다.
                if (!가까운_구역을_찾는다(내륙, 바다인가, 4f, _def.detectRadius - 2f, out var 바다)) continue;
                if (!가까운_구역을_찾는다(내륙, 물가인가, 3f, _def.detectRadius, out var 물가)) continue;

                // 낫을 올려 놓아 볼 자리. 사람 위에 겹쳐 놓으면 발밑을 재는
                // 광선이 사람의 몸부터 맞고 지형을 놓친다(실측: 구역이 Liquid로 읽혔다).
                if (!가까운_구역을_찾는다(내륙, 내륙인가, 2.5f, 8f, out var 올려둘곳)) continue;

                _물가 = 물가;
                _바다 = 바다;
                _내륙 = 내륙;
                _올려둘곳 = 올려둘곳;
                break;
            }

            E2EHarness.Assert(_바다 != Vector3.zero,
                              "바다 · 물가 · 내륙이 나란히 있는 무대를 찾았다");
            if (_바다 == Vector3.zero) yield break;

            E2EHarness.Log($"  무대: 바다 {_바다.ToString("F1")} · 물가 {_물가.ToString("F1")} · " +
                           $"내륙 {_내륙.ToString("F1")} · 올려둘곳 {_올려둘곳.ToString("F1")}");
            yield return null;
        }

        static bool 바다인가(Vector3 p, out Vector3 자리)
        {
            자리 = new Vector3(p.x, _액면, p.z);
            if (!지형높이(p, out float gy) || gy > _액면) return false;

            // 빛 안에 세우면 빛을 꺼리는 개체가 영원히 물러나기만 한다.
            return 둘레까지_어둡다(자리);
        }

        static bool 물가인가(Vector3 p, out Vector3 자리)
        {
            자리 = Vector3.zero;
            if (!지형높이(p, out float gy)) return false;
            if (gy <= _액면 || gy > _액면 + 해안선폭) return false;

            자리 = new Vector3(p.x, gy, p.z);
            return !LitZoneRegistry.IsLit(자리);
        }

        /// <summary>
        /// 사람이 실제로 서 있을 수 있는 <b>육지</b>인가.
        ///
        /// 세 가지를 함께 묻는다.
        /// <list type="number">
        /// <item><b>걸을 수 있는가</b> — 구워 둔 NavMesh가 답한다. 아래를 내려다보는
        ///       광선만으로는 안 된다. 이 섬의 내륙은 거대 버섯의 줄기와 갓에 걸려
        ///       (실측 y 55.8~70) 지면이 아닌 것을 지면으로 읽는다</item>
        /// <item><b>발밑에 실제로 무언가 있는가</b> — <b>바로 위에서 짧게</b> 내려다본다.
        ///       NavMesh가 있다고 광선이 맞는 것은 아니다. 실측으로 NavMesh 56.5인
        ///       자리에서 아래로 40m를 쏘아도 아무것도 없었고, 낫을 거기 올려 놓으면
        ///       발밑을 못 찾아 <b>육지 한복판에서 구역이 Liquid</b>로 읽혔다.
        ///       낫이 보는 것과 같은 것을 보아야 무대가 성립한다</item>
        /// <item><b>해안선 띠보다 확실히 위인가</b> — 경계에 바짝 붙은 자리를 고르면
        ///       "올라오지 않았다"를 좌표로 확인할 수 없다</item>
        /// </list>
        /// </summary>
        static bool 내륙인가(Vector3 p, out Vector3 자리)
        {
            자리 = Vector3.zero;
            if (!NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas)) return false;

            // <b>발밑이 섬 지형이어야 한다.</b> 이 섬의 NavMesh는 거대 버섯의 갓 위에도
            // 깔려 있고, 거기에 낫을 올려 놓으면 갓이 곧 발밑이라 내려올 길이 없다 —
            // 실측으로 25초 동안 55.11에 얹힌 채 액면으로 돌아가지 못했다.
            // 섬 메시는 폭이 100m를 넘고 소품은 몇 미터다. 이름이 아니라 크기로 가른다.
            // 첫 명중만 보면 지면에 놓인 돌멩이가 잡히므로 아래로 지나가는 것을 다 본다.
            float 지면 = float.MinValue;
            foreach (var 것 in Physics.RaycastAll(hit.position + Vector3.up * 1f, Vector3.down, 4f,
                                                  ~0, QueryTriggerInteraction.Ignore))
            {
                var 크기 = 것.collider.bounds.size;
                if (크기.x < 40f || 크기.z < 40f) continue;
                지면 = Mathf.Max(지면, 것.point.y);
            }

            if (지면 == float.MinValue) return false;
            if (Mathf.Abs(지면 - hit.position.y) > 2.5f) return false;   // 딛는 것이 그 지형이다
            if (지면 <= _액면 + 해안선폭 + 0.8f) return false;
            if (LitZoneRegistry.IsLit(hit.position)) return false;

            자리 = hit.position;
            return true;
        }

        delegate bool 구역판정(Vector3 p, out Vector3 자리);

        static bool 가까운_구역을_찾는다(Vector3 기준, 구역판정 맞는가,
                                        float 최소, float 최대, out Vector3 찾은것)
        {
            for (float r = 최소; r <= 최대; r += 2f)
            {
                for (int a = 0; a < 24; a++)
                {
                    Vector3 p = 기준 + Quaternion.Euler(0f, a * 15f, 0f) * Vector3.forward * r;
                    if (맞는가(p, out 찾은것)) return true;
                }
            }

            찾은것 = Vector3.zero;
            return false;
        }

        /// <summary>
        /// 이 기둥에서 <b>해안선 띠 아래</b>의 지형 높이. 그 위의 것은 보지 않는다.
        ///
        /// 하늘에서 쏘면 거대 버섯의 줄기와 갓(실측 y 55.8~70)이 먼저 잡혀 물가가
        /// 통째로 육지로 읽힌다 — 실제로 그렇게 한 번 깨졌다. 띠보다 한 뼘 위에서
        /// 시작하면 머리 위의 것은 애초에 광선에 들어오지 않는다
        /// (<see cref="HoverDrifter"/>가 같은 이유로 같은 방식이다).
        /// 못 찾았다는 것은 <b>여기 지형이 띠보다 높다</b>는 뜻이다.
        /// </summary>
        static bool 지형높이(Vector3 p, out float y) =>
            지형높이(p, _액면 + 해안선폭 + 1f, out y);

        static bool 지형높이(Vector3 p, float top, out float y)
        {
            y = 0f;
            var origin = new Vector3(p.x, top, p.z);

            if (!Physics.Raycast(origin, Vector3.down, out var hit, 60f, ~0,
                                 QueryTriggerInteraction.Ignore))
                return false;

            if (hit.collider.GetComponentInParent<CreatureHealth>() != null) return false;
            if (hit.collider.GetComponentInParent<PlayerContext>() != null) return false;

            y = hit.point.y;
            return true;
        }

        /// <summary>
        /// <b>다른 시나리오가 쓰는 창구</b> — 낫을 세울 수 있는 자리를 찾는다.
        ///
        /// 낫은 이제 아무 NavMesh 위에나 서지 못한다. 예전처럼 걸을 수 있는 자리에
        /// 세우면 서식지 밖이라 곧바로 물가로 돌아가 버리고, 그 사이 관측 반경을
        /// 벗어나 시나리오가 재려던 것을 놓친다.
        /// </summary>
        /// <param name="중심">여기서 <paramref name="최소"/>~<paramref name="최대"/>m 떨어진 곳을 훑는다.</param>
        /// <param name="어두워야">밝은 구역을 피할 것인가. 빛을 꺼리는 개체를 빛 안에 세우면 영원히 물러난다.</param>
        public static bool 서식지를_찾는다(Vector3 중심, float 최소, float 최대,
                                           bool 어두워야, out Vector3 자리)
        {
            자리 = Vector3.zero;
            if (!WaterBody.TryGetSurfaceAt(new Vector3(중심.x, 0f, 중심.z), out float 수면))
                return false;

            for (float r = 최소; r <= 최대; r += 2f)
            {
                for (int a = 0; a < 24; a++)
                {
                    Vector3 p = 중심 + Quaternion.Euler(0f, a * 15f, 0f) * Vector3.forward * r;
                    var 후보 = new Vector3(p.x, 수면 + 0.6f, p.z);

                    if (!WaterBody.TryGetSurfaceAt(new Vector3(p.x, 0f, p.z), out float s)) continue;

                    bool 지형있다 = Physics.Raycast(
                        new Vector3(p.x, s + ScytheHabitat.ShoreRise + 1f, p.z), Vector3.down,
                        out var hit, 60f, ~0, QueryTriggerInteraction.Ignore);

                    var zone = ScytheHabitat.Classify(true, s, 지형있다, 지형있다 ? hit.point.y : 0f);
                    if (zone == HabitatZone.Inland) continue;
                    if (어두워야 && !둘레까지_어둡다(후보)) continue;

                    자리 = 후보;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// <b>다른 시나리오가 쓰는 창구</b> — 사람이 설 수 있는 <b>어두운 육지</b>를 찾는다.
        ///
        /// <see cref="내륙인가"/>에는 이 섬을 실제로 훑어 가며 알아낸 것이 들어 있다
        /// (거대 버섯의 갓이 NavMesh를 이고 있다는 것, 하늘에서 쏜 광선이 그 갓부터
        /// 맞는다는 것). 그 지식을 시나리오마다 다시 적으면 한쪽만 고쳐지고 갈라진다.
        /// </summary>
        public static bool 육지를_찾는다(Vector3 중심, float 최소, float 최대, out Vector3 자리)
        {
            자리 = Vector3.zero;

            // 판정이 액면 높이를 기준으로 도므로 먼저 재어 둔다. FullRun도 제 무대를
            // 고를 때 이 값을 다시 잰다 — 남의 실행에 끼어들 자리가 없다.
            if (!WaterBody.TryGetSurfaceAt(new Vector3(중심.x, 0f, 중심.z), out _액면)) return false;

            return 가까운_구역을_찾는다(중심, 내륙인가, 최소, 최대, out 자리);
        }

        /// <summary>
        /// 이 자리도, 배회하다 닿을 만한 둘레도 어두운가.
        ///
        /// 중심만 보면 발광 군락 가장자리에 세워 놓고 곧 빛 속으로 들어가게 된다.
        /// 빛을 꺼리는 개체는 거기서 영원히 물러나기만 하므로 재려는 것을 못 잰다
        /// (<c>E2ERelicSupply</c>가 실측으로 확립한 조건이다: 40초 내내 Flee, 흘린 것 0개).
        /// </summary>
        static bool 둘레까지_어둡다(Vector3 자리)
        {
            if (LitZoneRegistry.IsLit(자리)) return false;

            for (int i = 0; i < 4; i++)
            {
                var 옆 = 자리 + Quaternion.Euler(0f, i * 90f, 0f) * Vector3.forward * 7f;
                if (LitZoneRegistry.IsLit(옆)) return false;
            }
            return true;
        }

        // ── 낫을 세운다 ─────────────────────────────────────────

        static IEnumerator 낫을_세운다()
        {
            var prefab = 프리팹();
            if (prefab == null) yield break;

            var go = Object.Instantiate(prefab, _바다 + Vector3.up * 0.6f, Quaternion.identity);
            go.name = "E2E_낫서식";
            yield return null;
            yield return null;

            _낫 = go.GetComponent<CreatureBrain>();
            _몸 = _낫 != null ? _낫.Motor as HoverDrifter : null;

            E2EHarness.Assert(_몸 != null, "부유하는 몸이 스스로 붙었다");
            if (_몸 == null) yield break;

            E2EHarness.AssertEqual(_몸.Alert, ScytheAlert.Calm, "평시로 시작한다");

            // 몇 프레임 두어 제 높이를 잡게 한다.
            for (int i = 0; i < 30; i++) yield return null;

            E2EHarness.Assert(_몸.Zone != HabitatZone.Inland,
                              $"세운 자리가 제 서식지다 (구역 {_몸.Zone})");
            E2EHarness.Assert(Mathf.Abs(_낫.transform.position.y - _액면) < 1.5f,
                              $"액면에 붙어 떠 있다 (y {_낫.transform.position.y:F2}, 액면 {_액면:F2})");
            E2EHarness.Log($"  낫 {_낫.transform.position.ToString("F1")} 구역 {_몸.Zone}");
        }

        // ── 1. 평시에는 육지로 올라오지 않는다 ──────────────────

        static IEnumerator 평시에는_육지로_올라오지_않는다()
        {
            E2EHarness.Log("— 평시: 쫓아와도 물가에서 멈춘다 —");

            // 감지 반경 안, 그러나 육지에 선다. 어두워야 빛에 막힌 것과 섞이지 않는다.
            yield return 선다(_내륙);
            E2EHarness.Assert(!LitZoneRegistry.IsLit(사람자리), "선 자리가 어둡다");
            E2EHarness.Assert(사람자리.y > _액면 + 해안선폭 + 0.8f,
                              $"사람이 선 곳은 해안선 띠보다 확실히 위다 " +
                              $"(사람 y {사람자리.y:F2}, 띠 윗선 {_액면 + 해안선폭:F2})");

            Vector3 시작 = _낫.transform.position;
            float 최대이동 = 0f, 최고높이 = float.MinValue, 가장가까이 = float.MaxValue;
            var 밟은구역 = new HashSet<HabitatZone>();
            bool 쫓았다 = false;
            int 육지프레임 = 0, 전체프레임 = 0;
            float 육지연속 = 0f, 최장육지 = 0f;

            float t = 0f, 다음정렬 = 1.5f;
            while (t < 14f)
            {
                // 감지 반경 안, 그리고 육지 위에 머문다. 비탈에서 미끄러져 내려가면
                // 그것은 "육지에 선 사람을 쫓았다"가 아니게 된다 (실측: 14초 동안
                // 54.5에서 52.1까지 흘러내려 낫이 0.9m까지 붙었다).
                if (t >= 다음정렬)
                {
                    다음정렬 = t + 1.5f;
                    yield return 선다(_내륙);
                }

                밟은구역.Add(_몸.Zone);
                전체프레임++;
                if (_몸.Zone == HabitatZone.Inland)
                {
                    육지프레임++;
                    육지연속 += Time.deltaTime;
                    최장육지 = Mathf.Max(최장육지, 육지연속);
                }
                else 육지연속 = 0f;

                if (_낫.State == CreatureState.Chase || _낫.State == CreatureState.Attack)
                    쫓았다 = true;

                최대이동 = Mathf.Max(최대이동, Vector3.Distance(시작, _낫.transform.position));
                가장가까이 = Mathf.Min(가장가까이, Vector3.Distance(_낫.transform.position, 사람자리));
                최고높이 = Mathf.Max(최고높이, _낫.transform.position.y);

                t += Time.deltaTime;
                yield return null;
            }

            E2EHarness.Log($"  {t:F0}초 관찰: 최대 이동 {최대이동:F1}m, 가장 가까이 {가장가까이:F1}m, " +
                           $"밟은 구역 {string.Join("·", 밟은구역)}, 최고 높이 {최고높이:F2}, " +
                           $"육지 프레임 {육지프레임}/{전체프레임} (최장 {최장육지 * 1000f:F0}ms)");

            E2EHarness.Assert(쫓았다, "사람을 알아보고 쫓기는 했다 — 굳어 있던 것이 아니다");
            E2EHarness.Assert(최대이동 > 1.5f, $"실제로 움직였다 ({최대이동:F1}m)");

            // <b>좌표가 본 판정이다.</b> 구역은 낫이 스스로 보고하는 값이라 그것만 믿으면
            // 재는 쪽과 재이는 쪽이 같아지고, 무엇보다 <b>경계는 칼날</b>이다 — 발밑
            // 지형이 띠 윗선과 1cm 차이로 오르내리는 프레임이 섞인다. 액면에 붙어 사는
            // 것이 정말로 섬에 올라섰다면 그런 오차가 아니라 <b>높이</b>가 달라진다.
            // 실측한 내륙은 53.5 위이고 사람은 그 위에 서 있다.
            float 천장 = _액면 + 해안선폭 + 0.9f;
            E2EHarness.Assert(최고높이 <= 천장,
                              $"높이가 해안선 띠 위로 올라가지 않았다 " +
                              $"(최고 {최고높이:F2} <= {천장:F2}, 사람은 {사람자리.y:F2}에 서 있었다)");

            // 그리고 <b>머무르지 않았다.</b> 스치는 것과 올라서는 것은 시간으로 갈린다 —
            // 6.2 m/s로 반 초면 3m를 간다. 경계를 한두 프레임 스친 것까지 실패로 치면
            // 지형 굴곡을 재게 되지만, 반 초를 넘겨 머물렀다면 그것은 규칙이 뚫린 것이다.
            E2EHarness.Assert(최장육지 < 0.5f,
                              $"육지에 머문 적이 없다 — 가장 길게 걸친 것이 {최장육지 * 1000f:F0}ms다");
            // 프레임 <b>수</b>로는 단언하지 않는다. 물가 지형이 띠 윗선과 몇 cm 차이로
            // 오르내리면 스치는 프레임이 수십 개씩 섞이는데, 그 수는 지도의 굴곡이
            // 정하지 낫의 행동이 정하지 않는다(실측 3.7%). 위에서 이미 두 가지로
            // 못 박았다 — <b>높이가 띠를 넘지 않았고, 반 초를 넘겨 머문 적이 없다.</b>
        }

        // ── 2. 해안선에서 유물을 줍는다 ─────────────────────────

        static IEnumerator 해안선에서_유물을_줍는다()
        {
            E2EHarness.Log("— 해안선: 액면 보행 장비 없이 유물을 줍는다 —");

            E2EHarness.AssertEqual(Inv.CountOf(보행장비), 0, "여전히 액면 보행 장비가 없다");

            // 감지 반경 밖 · 관측 반경 안. 쫓기면 흘리지 않고, 너무 멀면 굴리지도 않는다.
            // 그 띠 안에 머무는 것이 유물을 사는 값이다 (E2ERelicSupply와 같은 규칙).
            yield return 랜턴(false);
            yield return 물러난다(_def.detectRadius + 12f);

            int 이전 = RelicShedder.ShedCount;
            float t = 0f, 다음정렬 = 1.5f;
            while (t < 75f && RelicShedder.ShedCount == 이전)
            {
                // 매 프레임 자리를 다시 잡으면 재는 시간이 여섯 배로 늘어난다.
                // 실제로 그렇게 해 보고 60초짜리 기다림이 6분이 됐다.
                if (t >= 다음정렬)
                {
                    다음정렬 = t + 1.5f;
                    float d = Vector3.Distance(_낫.transform.position, 사람자리);
                    if (d < _def.detectRadius + 4f || d > _def.relicShed.witnessRadius - 6f)
                        yield return 물러난다(_def.detectRadius + 12f);
                }

                t += Time.deltaTime;
                yield return null;
            }

            E2EHarness.Assert(RelicShedder.ShedCount > 이전,
                              $"낫이 제 영역에서 유물을 흘렸다 ({t:F1}초, 상태 {_낫.State})");
            if (RelicShedder.ShedCount == 이전) yield break;

            string id = RelicShedder.LastShedItemId;
            E2EHarness.Assert(id == 막 || id == 핵, $"흘린 것이 유물이다 ({id})");

            var pickup = Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Exclude)
                               .Where(p => p.name == "Drop_" + id)
                               .OrderBy(p => Vector3.Distance(p.transform.position,
                                                              _낫.transform.position))
                               .FirstOrDefault();
            E2EHarness.Assert(pickup != null, "바닥에 실제 줍기 대상이 놓였다");
            if (pickup == null) yield break;

            // <b>여기가 이 항목의 내용이다.</b> 떨어진 자리가 육지도 심해도 아닌,
            // 육지에 선 사람이 팔을 뻗을 수 있는 가장자리여야 순환이 풀린다.
            Vector3 떨어진곳 = pickup.transform.position;
            bool 액면있다 = WaterBody.TryGetSurfaceAt(new Vector3(떨어진곳.x, 0f, 떨어진곳.z),
                                                     out float 수면);
            bool 지형있다 = 지형높이(떨어진곳, out float 지형);
            var 구역 = ScytheHabitat.Classify(액면있다, 수면, 지형있다, 지형);

            E2EHarness.Log($"  떨어진 곳 {떨어진곳.ToString("F1")} 구역 {구역} " +
                           $"(지형 {(지형있다 ? 지형.ToString("F2") : "없음")})");
            E2EHarness.Assert(구역 != HabitatZone.Inland,
                              "유물이 낫의 영역에 떨어졌다 — 육지에 흘린 것이 아니다");

            // 육지에 선 채로 닿는가. 물에 들어가지 않고 손이 닿는 거리에 선다.
            yield return 물가에_선다(떨어진곳);
            E2EHarness.Assert(사람자리.y >= _액면,
                              $"사람은 액면 위에 서 있다 (발 {사람자리.y:F2} >= 액면 {_액면:F2})");
            E2EHarness.Assert(LiquidContactService.LastImmersion != SeaImmersion.Swimming,
                              "헤엄쳐 간 것이 아니다");

            // <b>2026-08-07: 여기서 랜턴을 켜던 것을 걷어냈다 — 설계가 뒤집혔다.</b>
            //
            // 예전 이 줄은 <b>랜턴을 켠 채로 유물을 줍는 것을 정상으로</b> 못 박고
            // 있었다. 세계관 §10이 그 반대를 정했다: <b>"낫은 밤에 다니고 빛을
            // 피한다. 유물이 있어야 진행이 되므로, 유물을 얻으려면 칠흑 속에서
            // 랜턴을 꺼야 한다. 이 게임에서 가장 무서운 행동이 곧 진행 조건이다."</b>
            // 규칙도 그렇게 섰다(<see cref="Survive.Progression.RelicDropRule.CanShed"/>) —
            // 사람이 빛 안이면 낫이 애초에 흘리지 않는다.
            //
            // 그래서 이 절은 처음부터 끝까지 어둠에서 돈다. 이 항목이 재려는 것은
            // <b>액면 보행 장비 없이 손이 닿는가</b>이지 랜턴 상태가 아니므로,
            // 켜는 줄 하나를 지우는 것으로 족하다.
            E2EHarness.Assert(!_lantern.IsOn, "어둠에서 줍는다 — 켜면 애초에 흘리지 않는다");

            int 전 = Inv.CountOf(id);
            yield return 줍는다(pickup);

            E2EHarness.AssertEqual(Inv.CountOf(id), 전 + 1,
                                   $"액면 보행 장비 없이 {id}를 손에 넣었다 — 순환이 풀린다");
            E2EHarness.AssertEqual(Inv.CountOf(보행장비), 0, "끝까지 액면 보행 장비 없이 해냈다");
        }

        // ── 3. 육지에 있을 수 있는가 ────────────────────────────

        /// <summary>
        /// 같은 육지 좌표에 올려 놓고 <b>태세만 바꾼다.</b> 평시면 스스로 물가로
        /// 돌아가고, 발령이면 그 자리에 머문다.
        ///
        /// <b>왜 쫓아 올라오게 하지 않는가.</b> 처음에는 사람을 육지에 세우고 낫이
        /// 걸어 올라오는지를 보려 했다. 그런데 이 섬의 물가는 대부분 벼랑이거나
        /// 액면이 파고든 처마라, 낫이 사람의 <b>발밑 수면</b>까지 1.1m로 붙고도 끝내
        /// 육지가 아니었다(실측). 호버링이라고 4m 벼랑을 타지는 못하며 그것은 결함이
        /// 아니다 — 다만 그 무대에서는 <b>지형의 경사</b>를 재게 되지 서식 범위 규칙을
        /// 재지 못한다. 올려 놓고 보면 지도가 어떻게 생겼든 규칙만 남는다.
        ///
        /// 덤으로 밀려난 개체가 <b>스스로 돌아가는</b> 길(<see cref="HoverDrifter.Returning"/>)도
        /// 여기서만 지나간다. 은폐 프로토콜은 "가지 않는다"가 아니라 "있으면 안 된다"이므로
        /// 밀어 넣어진 개체도 제자리로 돌아가야 규칙이 상태와 무관하게 성립한다.
        /// </summary>
        static IEnumerator 육지에_둘_수_있는가()
        {
            E2EHarness.Log("— 같은 육지 좌표, 태세만 바꾼다 —");
            if (_몸 == null) yield break;

            yield return 랜턴(false);
            yield return 선다(_내륙);

            // 1) 평시 — 올려 놓아도 남아 있지 못한다
            ScytheWatch.Set(ScytheAlert.Calm);
            올려놓는다();
            yield return null;
            yield return null;
            발밑을_적는다();
            E2EHarness.AssertEqual(_몸.Zone, HabitatZone.Inland, "육지 좌표에 올려 놓았다");

            bool 돌아갔다 = false;
            bool 돌아가려했다 = false;
            float t = 0f;
            while (t < 25f && !돌아갔다)
            {
                if (_몸.Returning) 돌아가려했다 = true;

                // 구역만 보면 갓 가장자리에서 한 발 비켜선 것도 "돌아갔다"가 된다.
                // 액면에 붙는 높이까지 내려와야 제자리로 돌아온 것이다.
                돌아갔다 = _몸.Zone != HabitatZone.Inland &&
                          _낫.transform.position.y <= _액면 + 해안선폭 + 0.9f;
                t += Time.deltaTime;
                yield return null;
            }

            E2EHarness.Log($"  평시: {t:F1}초 만에 {_낫.transform.position.ToString("F1")} " +
                           $"구역 {_몸.Zone}");
            E2EHarness.Assert(돌아가려했다, "평시에는 육지를 제자리로 여기지 않는다 — 돌아가려 했다");
            E2EHarness.Assert(돌아갔다,
                              $"평시에는 육지에 남아 있지 못한다 — 스스로 액면까지 돌아갔다 " +
                              $"({t:F1}초, 구역 {_몸.Zone}, 높이 {_낫.transform.position.y:F2})");

            // 2) 발령 — 같은 자리에 그대로 머문다
            ScytheWatch.Set(ScytheAlert.Alarmed);
            E2EHarness.Log("  태세를 발령으로 세운다 (무엇이 이것을 거는가는 스펙 §5)");
            올려놓는다();
            yield return null;
            yield return null;
            발밑을_적는다();
            E2EHarness.AssertEqual(_몸.Zone, HabitatZone.Inland, "같은 육지 좌표에 다시 올려 놓았다");

            int 육지프레임 = 0, 전체프레임 = 0;
            t = 0f;
            while (t < 10f)
            {
                if (_몸.Returning) E2EHarness.Assert(false, "발령인데도 돌아가려 한다");
                if (_몸.Zone == HabitatZone.Inland) 육지프레임++;
                전체프레임++;
                t += Time.deltaTime;
                yield return null;
            }

            E2EHarness.Log($"  발령: {t:F0}초 중 {육지프레임}/{전체프레임} 프레임을 육지에서 보냈다 " +
                           $"({_낫.transform.position.ToString("F1")})");
            E2EHarness.Assert(육지프레임 > 전체프레임 / 2,
                              $"발령이면 육지에 머문다 — 못 오는 것이 아니었다 " +
                              $"({육지프레임}/{전체프레임} 프레임)");

            ScytheWatch.Set(ScytheAlert.Calm);
        }

        /// <summary>
        /// 낫을 육지 좌표 위에 올려 놓는다.
        ///
        /// <b>사람과 겹치지 않게 띄운 자리를 미리 골라 둔다</b>(<see cref="_올려둘곳"/>).
        /// 사람 위에 겹쳐 놓으면 발밑을 재는 광선이 사람의 몸부터 맞고, 몸은 지형이
        /// 아니므로 걸러진 다음 섬 안쪽을 지나 아무것도 못 찾는다 — 육지 한복판인데
        /// 구역이 Liquid로 읽혔다(실측). 아무 데나 옆으로 미는 것도 안 된다.
        /// 섬 가장자리에서는 한 뼘 옆이 이미 처마 밖이라 정말로 액면 위다.
        /// </summary>
        /// <summary>지금 발밑에 무엇이 있는지 그대로 적는다. 실패했을 때 원인을 짚으라고 둔다.</summary>
        static void 발밑을_적는다()
        {
            var p = _낫.transform.position;
            bool hit = Physics.Raycast(p + Vector3.up * 0.75f, Vector3.down, out var h, 40f, ~0,
                                       QueryTriggerInteraction.Ignore);
            E2EHarness.Log($"  [발밑] 올려둘곳 {_올려둘곳.ToString("F2")} 낫 {p.ToString("F2")} " +
                           $"구역 {_몸.Zone} 광선 {(hit ? $"{h.point.y:F2} {h.collider.name}" : "없음")}");
        }

        static void 올려놓는다()
        {
            _낫.transform.position = _올려둘곳 + Vector3.up * 0.6f;
            E2EHarness.SyncPhysics();
        }

        // ── 자리 잡기 ───────────────────────────────────────────

        /// <summary>
        /// 낫에게서 <paramref name="거리m"/>만큼, <b>걸을 수 있는 육지로</b> 물러난다.
        /// 뒤가 늘 땅인 것은 아니다 — 물가에서 곧장 물러나면 바다다.
        /// </summary>
        static IEnumerator 물러난다(float 거리m)
        {
            Vector3 기준 = _낫.transform.position;
            Vector3 뒤 = 사람자리 - 기준;
            뒤.y = 0f;
            if (뒤.sqrMagnitude < 0.01f) 뒤 = Vector3.forward;
            뒤.Normalize();

            for (int i = 0; i < 12; i++)
            {
                float 각 = (i % 2 == 0 ? 1f : -1f) * 30f * ((i + 1) / 2);
                Vector3 후보 = 기준 + (Quaternion.Euler(0f, 각, 0f) * 뒤) * 거리m;
                if (!NavMesh.SamplePosition(후보, out var hit, 15f, NavMesh.AllAreas)) continue;
                if (Vector3.Distance(hit.position, 기준) < 거리m * 0.7f) continue;

                yield return 선다(hit.position);
                yield break;
            }

            yield return 선다(_내륙);
        }

        static IEnumerator 선다(Vector3 자리)
        {
            var 목표 = 자리 + Vector3.up * 0.4f;
            for (int i = 0; i < 5; i++)
            {
                E2EHarness.Teleport(목표);
                yield return null;
            }
        }

        /// <summary>
        /// 떨어진 것 곁의 <b>액면 위 지형</b>에 선다. 물에 들어가면 그것은
        /// "해안선에서 주웠다"가 아니다.
        /// </summary>
        static IEnumerator 물가에_선다(Vector3 대상)
        {
            for (float r = 1.4f; r <= 6f; r += 0.8f)
            {
                for (int a = 0; a < 16; a++)
                {
                    Vector3 p = 대상 + Quaternion.Euler(0f, a * 22.5f, 0f) * Vector3.forward * r;
                    if (!지형높이(p, out float gy)) continue;
                    if (gy < _액면) continue;

                    yield return 선다(new Vector3(p.x, gy, p.z));
                    yield break;
                }
            }

            // 못 찾으면 앞의 단언이 "액면 위 지형을 딛고 있다"에서 걸린다.
            yield return 선다(_물가);
        }

        /// <summary>
        /// 눈앞으로 옮겨 조준하고 E를 눌러 줍는다 (<c>E2ERelicSupply.줍는다</c>와 같은 방식).
        /// 떨구는 자리를 시나리오가 고를 수 없어서 확립된 방식이고, 줍는 절차 자체는
        /// 건너뛰지 않는다 — 조준도 프롬프트도 E도 그대로 지난다.
        /// </summary>
        /// <summary>
        /// 줍는 동작. <b>「낫은 밤에 다닌다」가 같은 절차를 다시 쓰므로 연다</b> —
        /// 줍는 방식이 시나리오마다 다르면 "얻었다"가 서로 다른 뜻이 된다.
        /// </summary>
        public static IEnumerator 줍는다(ItemPickup pickup)
        {
            Vitals.Health.Modify(Vitals.Health.Max);

            var it = E2EHarness.Player.Interactor;
            var cam = E2EHarness.Eye;
            pickup.transform.DOKill();

            foreach (float 거리 in new[] { 1.8f, 1.2f, 0.8f })
            {
                for (int a = 0; a < 8; a++)
                {
                    var d = Quaternion.Euler(0f, a * 45f, 0f) * Vector3.forward;
                    if (Physics.Raycast(cam.transform.position, d, 거리 + 0.4f, ~0,
                                        QueryTriggerInteraction.Ignore))
                        continue;

                    bool 잡혔다 = false;
                    for (int f = 0; f < 4 && !잡혔다; f++)
                    {
                        pickup.transform.position = cam.transform.position + d * 거리;
                        E2EHarness.SyncPhysics();
                        E2EHarness.LookAt(pickup.transform.position);
                        yield return null;
                        잡혔다 = ReferenceEquals(it.Current, pickup);
                    }

                    if (!잡혔다) continue;

                    E2EHarness.Log("  프롬프트: " + it.Current.InteractionPrompt);
                    yield return E2EHarness.TapKey(Key.E);
                    yield return null;
                    yield return null;
                    yield break;
                }
            }

            E2EHarness.Assert(false, $"떨어진 것이 조준에 잡힌다 ({pickup.name})");
        }

        /// <summary>
        /// 랜턴에 불이 들어오게 하거나 꺼지게 한다.
        ///
        /// <b>F는 더 이상 없다.</b> 랜턴은 상시 점등이 전제라(스펙 §12) 켜는 조작도
        /// 끄는 조작도 없다. 불이 없는 상태에 이르는 유일한 길이 배터리를 다 쓰는
        /// 것이므로 검사도 그 길을 지난다 — <c>E2EConsumer</c>가 같은 이유로 같은 모양이다.
        ///
        /// 이 시나리오는 <b>어둠이 전제</b>다. 빛을 꺼리는 개체를 빛 안에 세우면
        /// 영원히 물러나기만 해 재려던 것을 하나도 재지 못한다.
        /// </summary>
        static IEnumerator 랜턴(bool 켜기)
        {
            if (_lantern == null) yield break;

            if (켜기) E2EHarness.LightLantern();
            else E2EHarness.DarkenLantern();

            yield return null;
            E2EHarness.AssertEqual(_lantern.IsOn, 켜기,
                                   켜기 ? "배터리를 채우니 저절로 불이 들어왔다"
                                        : "배터리가 다하자 불이 꺼졌다");
            yield return null;
        }

        // ── 치우기 ──────────────────────────────────────────────

        static void 남은것을_치운다()
        {
            _낫 = null;
            _몸 = null;

            foreach (var b in Object.FindObjectsByType<CreatureBrain>(FindObjectsInactive.Include))
                if (b != null && b.name.StartsWith("E2E_낫서식"))
                    Object.Destroy(b.gameObject);

            foreach (var p in Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Exclude))
                if (p.name == "Drop_" + 막 || p.name == "Drop_" + 핵)
                    Object.Destroy(p.gameObject);
        }

        static IEnumerator 치운다()
        {
            RelicShedder.IntervalOverrideSeconds = 0f;
            남은것을_치운다();

            yield return 랜턴(false);
            yield return E2EHarness.ReleaseAllKeys();
            E2EHarness.RestoreWorld();
            Vitals.Health.Modify(Vitals.Health.Max);
            yield return null;
        }
    }
}
