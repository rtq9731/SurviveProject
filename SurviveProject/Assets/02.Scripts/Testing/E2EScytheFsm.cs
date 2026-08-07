using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Survive.Combat;
using Survive.Creatures;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// 낫의 4상태가 <b>실제로 몸을 모는가</b> (스펙 §20 게이트 8 · 9).
    ///
    /// 규칙과 전수 테스트는 앞 라운드에 섰다. 여기서 재는 것은 그 규칙이 화면 위에서
    /// 성립하는가 하나다. 둘을 본다.
    /// <list type="number">
    /// <item><b>게이트 9 — 화톳불로 걸어 들어가면 따라붙기가 풀린다.</b> 그리고 <b>랜턴으로는
    ///       풀리지 않는다</b>는 대조를 같이 둔다. 대조가 없으면 "빛이면 다 풀린다"와
    ///       구별되지 않고, 그 구분이 이 상태 기계에서 가장 큰 갈림이다</item>
    /// <item><b>게이트 8 — 재등장할 때 방위각이 실제로 바뀐다.</b> 사람을 꼭짓점으로 잰
    ///       방위가 연속 세 번 이상, 매번 하한(90도)을 넘겨 바뀌어야 한다</item>
    /// </list>
    ///
    /// <b>왜 별도 시나리오인가.</b> 「낫 꼬리」는 자세가 상태를 말하는지를 재고, 여기서는
    /// 상태 자체가 규칙대로 움직이는지를 잰다. 한 시나리오에 묶으면 어느 쪽이 깨졌는지
    /// 실패 한 줄로 갈리지 않는다.
    /// </summary>
    public static class E2EScytheFsm
    {
        const string 낫프리팹 = "Assets/05.Prefabs/Creatures/Creature_낫.prefab";

        static CreatureDefinitionSO _def;
        static CreatureBrain _낫;
        static HoverDrifter _몸;
        static ScytheMind _마음;
        static float _액면;
        static Vector3 _바다;

        static Vector3 사람자리 => E2EHarness.Player.transform.position;

        /// <summary>재등장 절에서 사람이 계속 바라보는 곳. 무대를 흔들지 않으려고 고정한다.</summary>
        static Vector3 _바라볼곳;

        /// <summary>시나리오를 연 자리. 재등장 절이 여기로 되돌아온다.</summary>
        static Vector3 _사람시작;

        public static IEnumerator FullRun()
        {
            yield return 준비();
            yield return 무대를_고른다();
            yield return 낫을_세운다();

            yield return 감지하면_따라붙는다();
            yield return 랜턴으로는_풀리지_않는다();
            yield return 고정_조명에_들어가면_풀린다();
            yield return 재등장하면_방위가_바뀐다();

            yield return 치운다();
            E2EHarness.Log("=== 낫 4상태 배선 완주 ===");
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
            E2EHarness.DarkenLantern();
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
        /// 사람이 설 어두운 물가와 낫이 뜰 액면을 찾는다. 좌표를 코드에 적지 않는 것은
        /// 섬 메시를 사람이 다시 만들기 때문이다(스펙 §16) — 「낫 꼬리」와 같은 문법이다.
        /// </summary>
        static IEnumerator 무대를_고른다()
        {
            var water = Object.FindAnyObjectByType<WaterBody>(FindObjectsInactive.Exclude);
            E2EHarness.Assert(water != null, "세계에 액면이 있다");
            if (water == null) yield break;

            _액면 = water.SurfaceY;
            _바다 = new Vector3(0f, _액면 + 0.6f, 8f);

            E2EHarness.Assert(!LitZoneRegistry.IsLit(_바다), "낫이 뜰 자리가 어둡다");
            E2EHarness.Log($"  무대: 바다 {_바다.ToString("F1")} (액면 {_액면:F2})");
            yield return null;
        }

        static IEnumerator 낫을_세운다()
        {
            _사람시작 = _바다 + new Vector3(0f, 0.6f, -(_def.detectRadius * 3f));

            // 사람을 감지 밖으로 먼저 물린다. 세우자마자 사람이 반경 안에 있으면
            // 첫 프레임에 따라붙기로 올라가는데, 그것은 규칙이 맞게 도는 것이지
            // 순찰로 시작하지 않는 것이 아니다 — 시작점을 재려면 무대를 비워야 한다.
            E2EHarness.Teleport(_바다 + new Vector3(0f, 0.6f, -(_def.detectRadius * 3f)));
            E2EHarness.SyncPhysics();
            yield return null;

            var go = Object.Instantiate(프리팹(), _바다, Quaternion.identity);
            go.name = "E2E_낫FSM";

            _낫 = go.GetComponent<CreatureBrain>();
            yield return null;   // Awake가 부품을 붙일 한 프레임

            _몸 = go.GetComponent<HoverDrifter>();
            _마음 = go.GetComponent<ScytheMind>();

            E2EHarness.Assert(_몸 != null, "부유하는 몸이 스스로 붙었다");
            E2EHarness.Assert(_마음 != null, "4상태를 드는 부품이 스스로 붙었다 — 프리팹을 고치지 않았다");
            if (_마음 == null) yield break;

            E2EHarness.AssertEqual(_마음.State, ScytheState.Patrol, "순찰로 시작한다");
            yield return null;
        }

        // ── 게이트 9 — 무엇이 따라붙기를 푸는가 ──────────────────

        static IEnumerator 감지하면_따라붙는다()
        {
            E2EHarness.Log("— 감지: 순찰에서 따라붙기로 —");

            yield return 사람을_낫_곁에_세운다();

            yield return E2EHarness.WaitUntil(() => _마음.State != ScytheState.Patrol,
                                              "감지하면 순찰을 그만둔다", 8f);
            E2EHarness.Log($"  4상태 {_마음.State}, 거리 {_낫.ThreatDistance:F1} " +
                           $"(감지 반경 {_def.detectRadius})");
            E2EHarness.Assert(_마음.State == ScytheState.Beware || _마음.State == ScytheState.Attack,
                              $"따라붙거나 덤빈다 (4상태 {_마음.State})");
        }

        static IEnumerator 랜턴으로는_풀리지_않는다()
        {
            E2EHarness.Log("— 임시 조명: 랜턴은 따라붙기를 풀지 못한다 —");

            // <b>옆에 세운다.</b> 웅덩이 안에 두면 낫이 제가 빛에 잠겨 물러나고,
            // 물러나다 감지 밖으로 나가면 <b>랜턴이 아니라 거리가</b> 따라붙기를
            // 푼다 — 그러면 이 절이 재려던 것과 다른 것을 재게 된다. 그런데 오프셋이
            // 오른 뒤로 정면 도달(14.5m)이 감지 반경(14m)을 넘어, 정면에는 그런
            // 자리가 아예 없다. 옆 도달은 4.66m로 줄어 있으므로 옆 10m가 그 자리다.
            float 거리 = 10f;
            yield return 낫을_사람_옆에_세운다(거리);
            E2EHarness.LightLantern();
            yield return null;

            E2EHarness.Assert(LitZoneRegistry.IsLit(사람자리), "사람이 랜턴 웅덩이 안에 있다");
            E2EHarness.Assert(!LitZoneRegistry.IsLitByFixed(사람자리),
                              "그 빛은 고정 조명이 아니다 — 이 대조가 이 절의 전부다");

            // 랜턴을 켜고 <b>충분히</b> 버틴다. 시간으로 풀리는 규칙이었다면 여기서 풀린다.
            // 무대는 붙들고 있는다 — 감지 안에 있다는 것은 이 문장의 전제이지 주어가 아니다.
            float t = 0f;
            var 본상태 = new HashSet<ScytheState>();
            while (t < 5f)
            {
                if (Vector3.Distance(_낫.transform.position, 사람자리) > _def.detectRadius * 0.9f)
                    yield return 낫을_사람_옆에_세운다(거리);

                E2EHarness.LookAt(_바라볼곳);
                본상태.Add(_마음.State);
                t += Time.deltaTime;
                yield return null;
            }

            E2EHarness.Log($"  5초 동안 본 4상태: {string.Join("·", 본상태)}");
            E2EHarness.Assert(!본상태.Contains(ScytheState.Patrol),
                              "랜턴 아래에서 5초를 서 있어도 따라붙기가 풀리지 않는다 " +
                              "(시간으로 풀지 않는다는 규칙)");
        }

        static IEnumerator 고정_조명에_들어가면_풀린다()
        {
            E2EHarness.Log("— 고정 조명: 걸어 들어가면 풀린다 —");

            // 랜턴을 끄고 다시 붙게 만든 다음, 사람만 고정 조명 안으로 옮긴다.
            E2EHarness.DarkenLantern();
            yield return 사람을_낫_곁에_세운다();
            yield return E2EHarness.WaitUntil(() => _마음.State == ScytheState.Beware ||
                                                    _마음.State == ScytheState.Attack,
                                              "다시 따라붙는다", 8f);

            var 불자리 = 고정_조명_자리();
            E2EHarness.Assert(불자리.HasValue,
                              "세계에 고정 조명이 있다 (화톳불·빛기둥)");
            if (!불자리.HasValue) yield break;

            E2EHarness.Teleport(불자리.Value);
            E2EHarness.SyncPhysics();
            yield return null;

            E2EHarness.Assert(LitZoneRegistry.IsLitByFixed(사람자리),
                              $"사람이 고정 조명 안에 섰다 {사람자리.ToString("F1")}");

            yield return E2EHarness.WaitUntil(() => _마음.State == ScytheState.Patrol,
                                              "고정 조명에 들어가면 따라붙기가 풀린다", 5f);

            E2EHarness.Log($"  4상태 {_마음.State}, 어그로 {_낫.AggroLeft:F1}");
            E2EHarness.AssertEqual(_마음.State, ScytheState.Patrol, "순찰로 내려갔다");

            // <b>풀렸다는 것이 이름뿐이 아니어야 한다.</b> 어그로가 남아 있으면 몸은
            // 최대 6초를 더 쫓고, 그러면 규칙과 몸이 갈린 것이다.
            E2EHarness.Assert(_낫.AggroLeft <= 0f,
                              $"쫓던 것을 그 자리에서 놓았다 (어그로 {_낫.AggroLeft:F2})");
        }

        /// <summary>
        /// 등록된 고정 조명 하나의 중심. <b>앞뒤가 없는 광원만</b> 고른다 —
        /// 랜턴은 사람을 따라다니므로 걸어 들어갈 자리가 아니다.
        /// </summary>
        static Vector3? 고정_조명_자리()
        {
            // 무대 정리에서 꺼 둔 주변 광원을 이 절에서만 되살린다. 화톳불이 없는
            // 세계에서도 빛기둥이 같은 규칙(앞뒤 없는 광원)으로 답한다.
            E2EHarness.RestoreAmbientLitZones();

            foreach (var src in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
            {
                if (!(src is ILitZoneSource zone)) continue;
                if (src is IOffsetLitSource) continue;
                if (!zone.IsLit) continue;

                var c = zone.LitZoneCenter;
                if (LitZoneRegistry.IsLitByFixed(c)) return c;
            }

            return null;
        }

        // ── 게이트 8 — 재등장 방위 ───────────────────────────────

        static IEnumerator 재등장하면_방위가_바뀐다()
        {
            E2EHarness.Log("— 재등장: 같은 방향에 머무르지 않는다 —");

            // 고정 조명 안에서는 따라붙기 자체가 풀리므로 재등장이 돌지 않는다.
            // 어둠으로 돌아간다.
            E2EHarness.MuteAmbientLitZones();

            // <b>랜턴을 켠다.</b> 재등장은 따라붙기 안의 규칙인데, 캄캄한 곳에서 사람
            // 곁에 서면 낫은 그 다음 프레임에 교전으로 올라간다 — 붙은 개체가 순간이동해
            // 사라지면 그것은 재등장이 아니라 도주다. 기획서 §4.5가 재등장을 적은 자리도
            // 정확히 여기다: <b>"임시 조명(랜턴)일 때는 계속 따라붙는다. 단 같은 방향에
            // 머무르지 않는다."</b> 랜턴이 앞을 막는 동안에만 이 규칙이 돈다.
            // <b>낫을 옆에 세운다 — 정면이 아니다.</b> 오프셋이 6.5m로 오르면서
            // 웅덩이 정면 도달이 14.5m가 되어 <b>낫의 감지 반경 14m를 넘어섰다.</b>
            // 그래서 사람이 낫을 마주 보는 한 「웅덩이 밖이면서 감지 안」인 자리가
            // 정면에는 존재하지 않는다 — 마주 보면 낫이 제 감지 밖으로 밀린다.
            // 그것이 §19가 노린 것이므로 고칠 일이 아니라 무대를 옮길 일이다.
            //
            // 옆은 다르다. 원을 앞으로 밀면 옆 도달이 줄어들어(피타고라스, 지금 4.66m)
            // <b>옆쪽 10m는 웅덩이 밖이면서 감지 안</b>이다. 그리고 정확히 옆은 등 뒤도
            // 아니라 사각이 아니므로 빛이 앞을 막고, 낫은 따라붙기에 머문다.
            // 재등장이 실제로 도는 자리가 여기다.
            float 거리 = 10f;
            yield return 낫을_사람_옆에_세운다(거리);
            E2EHarness.LightLantern();
            yield return null;
            E2EHarness.Log($"  사람과의 거리 {거리:F1}m · 옆 도달 " +
                           $"{LanternRule.SideReachForTier(1):F2}m · 정면 도달 " +
                           $"{LanternRule.ForwardReachForTier(1):F1}m · 감지 반경 {_def.detectRadius}");

            yield return E2EHarness.WaitUntil(() => _마음.State == ScytheState.Beware,
                                              "랜턴이 앞을 막아 따라붙기에 머문다", 8f);

            int 시작횟수 = _마음.ReappearCount;
            int 마지막본횟수 = 시작횟수;

            // <b>재등장 「한 번」에서 방위가 얼마나 도는지를 잰다.</b> 관측을 목록에
            // 쌓아 두고 이웃끼리 빼면 안 된다 — 사이사이 무대를 다시 세우느라 사람이
            // 순간이동하고, 그러면 낫이 옮겨서 바뀐 각과 사람이 옮겨서 바뀐 각이 섞인다
            // (실측 0도·30도·60도가 그렇게 나왔다). 옮긴 그 프레임의 앞뒤만 본다.
            var 돈각들 = new List<float>();
            var 좌표들 = new List<Vector3>();
            float 직전방위 = 방위();

            // 간격은 개체의 기억 길이(aggroSeconds, 낫은 6초)다. 다만 사람이 몸을
            // 돌리는 동안 상태가 따라붙기와 교전 사이를 오가므로 실제 리듬은 그보다
            // 느리다 — <b>재는 것은 「세 번 옮기는가」이지 「몇 초 만인가」가 아니므로</b>
            // 지켜보는 창을 넉넉히 연다(실측 48초에 두 번인 판이 있었다).
            float 한계 = _def.aggroSeconds * 12f + 20f;
            float t = 0f;
            while (t < 한계 && 돈각들.Count < 3)
            {
                // <b>사람은 한 방향을 보고 서 있는다.</b> 낫을 눈으로 좇게 하면
                // 웅덩이가 그쪽으로 밀려 낫을 삼키고(정면 도달 14.5m), 그러면 낫이
                // 제 빛에 잠겨 물러나다 감지 밖으로 나간다. 사람이 한쪽을 보는 동안
                // 반대쪽이 비는 것이 §19가 그린 그림이고, 이 절은 그 상태에서
                // 낫이 자리를 바꾸는지를 잰다.
                E2EHarness.LookAt(_바라볼곳);

                // <b>사이 거리를 붙들어 둔다.</b> 낫이 다가와 웅덩이 안으로 들어오면
                // 후보 고리가 통째로 밝아져 고를 자리가 하나도 남지 않고(실측 0개),
                // 그때 낫은 규칙대로 <b>머문다</b>. 그것은 옳은 동작이지만 이 절이
                // 재려는 것은 아니다 — 여기서 재는 것은 「자리를 바꾸는가」다.
                // 멀어져 감지 밖으로 나가는 경우도 같은 이유로 되돌린다.
                // <b>따라붙기에 머무는 것이 이 절의 전제다.</b> 재등장은 Beware 안의
                // 규칙이므로, 낫이 사각으로 돌아 들어가 교전에 올라가거나(그러면
                // 규칙대로 자리를 안 바꾼다) 감지 밖으로 나가면 잴 것이 없어진다.
                // 거리도 붙든다 — 가까워지면 후보 고리가 통째로 웅덩이에 잠긴다.
                float 사이 = Vector3.Distance(_낫.transform.position, 사람자리);
                if (_마음.State != ScytheState.Beware || 사이 < 거리 - 3f ||
                    사이 > _def.detectRadius * 0.95f)
                {
                    yield return 낫을_사람_옆에_세운다(거리);
                    yield return E2EHarness.WaitUntil(() => _마음.State == ScytheState.Beware,
                                                      "다시 따라붙는다", 6f);

                    // 무대를 옮겼으니 기준을 다시 잡는다. 이 프레임의 각 변화는
                    // 재등장이 만든 것이 아니다.
                    마지막본횟수 = _마음.ReappearCount;
                    직전방위 = 방위();
                    t += Time.deltaTime;
                    yield return null;
                    continue;
                }

                if (_마음.ReappearCount > 마지막본횟수)
                {
                    마지막본횟수 = _마음.ReappearCount;
                    돈각들.Add(Mathf.Abs(Mathf.DeltaAngle(직전방위, 방위())));
                    좌표들.Add(_낫.transform.position);
                }

                직전방위 = 방위();
                t += Time.deltaTime;
                yield return null;
            }

            for (int i = 0; i < 돈각들.Count; i++)
                E2EHarness.Log($"  재등장 {i + 1}: {좌표들[i].ToString("F2")} · 돈 각 {돈각들[i]:F1}도");

            E2EHarness.Assert(돈각들.Count >= 3,
                              $"연속 세 번 이상 자리를 옮겼다 (잰 것 {돈각들.Count}회 / " +
                              $"부품이 센 것 {_마음.ReappearCount - 시작횟수}회, {t:F1}초, " +
                              $"통과한 자리 {_마음.LastAcceptableCount}개)");

            // <b>방위가 실제로 바뀌었는가.</b> 옮긴 횟수만 세면 제자리에서 떨어도 통과한다.
            for (int i = 0; i < 돈각들.Count; i++)
                E2EHarness.Assert(돈각들[i] >= ScytheReappearance.MinTurnDegrees - 1f,
                                  $"{i + 1}번째 재등장이 하한만큼 돌았다 ({돈각들[i]:F1}도 " +
                                  $">= {ScytheReappearance.MinTurnDegrees})");
        }

        /// <summary>사람을 꼭짓점으로 잰 낫의 방위(도). 높이는 버린다.</summary>
        static float 방위()
        {
            Vector3 d = _낫.transform.position - 사람자리;
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        // ── 무대 조작 ───────────────────────────────────────────

        /// <summary>
        /// 사람을 낫의 감지 반경 안쪽 <b>어두운 자리</b>에 세운다.
        /// 낫을 옮기지 않는 것은 이 시나리오가 재려는 것이 낫의 자리 선택이기 때문이다.
        /// </summary>
        /// <summary>
        /// <b>낫을 사람의 옆에 놓는다.</b> 사람은 한 방향(+z)을 보고 서 있고 낫은
        /// 그 90도 옆에 선다.
        ///
        /// 옆이어야 하는 이유는 기하다. 웅덩이가 앞으로 6.5m 밀려 있어 정면 도달이
        /// 14.5m인데 낫의 감지 반경은 14m다 — <b>정면에는 「빛 밖이면서 감지 안」인
        /// 자리가 없다.</b> 옆 도달은 4.66m로 줄어 있으므로 옆 10m가 그 자리다.
        /// 정확히 옆은 등 뒤가 아니라 사각도 아니므로, 빛이 앞을 막아 낫이 따라붙기에
        /// 머문다 — 재등장이 도는 유일한 상태다.
        /// </summary>
        static IEnumerator 낫을_사람_옆에_세운다(float 거리)
        {
            // <b>사람을 시나리오를 연 자리로 되돌린다.</b> 앞 절들이 사람을 화톳불
            // 속으로 옮겨 놓기 때문에, 그대로 두면 재등장 고리가 물이 아니라 육지
            // 위에 깔린다 — 후보가 전부 서식지 밖으로 걸러져 고를 자리가 0이 된다
            // (실측: 사람 y=53.4 물가에서 통과 0개). 재려는 것은 방위이지 지형이 아니다.
            if (_사람시작 != Vector3.zero)
            {
                E2EHarness.Teleport(_사람시작);
                E2EHarness.SyncPhysics();
                yield return null;
            }

            Vector3 사람 = 사람자리;
            _바라볼곳 = 사람 + Vector3.forward * 20f;

            var 옆 = 사람 + new Vector3(거리, 0f, 0f);
            옆.y = _낫.transform.position.y;

            _낫.transform.position = 옆;
            E2EHarness.LookAt(_바라볼곳);
            E2EHarness.SyncPhysics();
            yield return null;
            yield return null;
        }

        static IEnumerator 사람을_낫_곁에_세운다(float 거리 = -1f)
        {
            if (거리 <= 0f) 거리 = _def.detectRadius * 0.5f;

            Vector3 낫자리 = _낫.transform.position;
            Vector3 목표 = 낫자리 + new Vector3(0f, 0.6f, -거리);

            E2EHarness.Teleport(목표);
            E2EHarness.LookAt(낫자리);
            E2EHarness.SyncPhysics();
            yield return null;
            yield return null;
        }

        static IEnumerator 치운다()
        {
            E2EHarness.DarkenLantern();
            남은것을_치운다();
            E2EHarness.RestoreWorld();
            yield return null;
        }

        static void 남은것을_치운다()
        {
            foreach (var brain in Object.FindObjectsByType<CreatureBrain>(FindObjectsInactive.Include))
                if (brain != null && brain.gameObject.name.StartsWith("E2E_낫FSM"))
                    Object.DestroyImmediate(brain.gameObject);
        }
    }
}
