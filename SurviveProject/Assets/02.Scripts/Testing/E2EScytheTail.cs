using System.Collections;
using UnityEngine;
using Survive.Combat;
using Survive.Creatures;
using Survive.Player;
using Survive.Vitals;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// 낫의 꼬리가 상태를 말한다 (기획서 §4.5 "상태 표현 — 꼬리" · 스펙 §4).
    ///
    /// <b>여기서 재는 것은 화면이 아니라 규칙이 화면까지 도달했는가다.</b> 자세 규칙
    /// 자체는 <c>ScytheStanceTests</c>가 씬 없이 전수로 못 박았다. 이 시나리오가 답하는
    /// 물음은 하나 더 뒤에 있다 — <b>실제로 물 위에 세운 개체가 작업 자세를 잡는가,
    /// 실제로 육지에 올린 개체가 공격 태세가 되는가.</b> 두뇌가 재는 값과 꼬리가 읽는
    /// 값이 갈리면 규칙이 아무리 옳아도 화면은 거짓말을 한다.
    ///
    /// <b>왜 경계 자세를 빛으로 세우는가.</b> 낫은 선공(Aggressive)이라 감지한 그 프레임에
    /// 곧바로 추격으로 넘어간다 — 사람이 다가가는 것만으로는 경계 자세가 한 프레임도
    /// 머물지 않는다. 그런데 낫은 빛을 꺼리므로(은폐 프로토콜) 사람이 랜턴을 켜면
    /// 추격이 막힌 채 감지만 살아 있는 상태가 된다. 그것이 정확히
    /// <b>"작업을 멈췄지만 아직 덤비지 않았다"</b>이고, 게임 안에서 플레이어가 이 자세를
    /// 실제로 보게 되는 자리이기도 하다.
    ///
    /// 무대는 매번 찾는다. 섬 메시는 사람이 다시 만들므로(스펙 §16) 좌표를 적으면
    /// 검증이 지도를 붙잡는다 — <see cref="E2EScytheHabitat"/>와 같은 규율이다.
    /// </summary>
    public static class E2EScytheTail
    {
        const string 낫프리팹 = "Assets/05.Prefabs/Creatures/Creature_낫.prefab";

        static PlayerVitals Vitals => E2EHarness.Player.Vitals;
        static Vector3 사람자리 => E2EHarness.Player.transform.position;

        static LanternController _lantern;
        static CreatureDefinitionSO _def;
        static CreatureBrain _낫;
        static HoverDrifter _몸;
        static ScytheTail _꼬리;
        static float _액면;
        static Vector3 _바다;
        static Vector3 _내륙;
        static Vector3 _올려둘곳;

        public static IEnumerator FullRun()
        {
            yield return 준비();
            if (_def == null) yield break;

            yield return 무대를_고른다();
            if (_바다 == Vector3.zero) yield break;

            yield return 낫을_세운다();
            if (_꼬리 == null) yield break;

            yield return 평시_액면에서는_작업_중이다();
            yield return 빛에_막히면_작업을_멈춘다();
            yield return 육지에_오르면_공격_태세다();
            yield return 물로_돌아오면_작업으로_돌아간다();

            yield return 치운다();
            E2EHarness.Log("=== 낫 꼬리 상태 표현 완주 ===");
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

            _lantern = Object.FindAnyObjectByType<LanternController>(FindObjectsInactive.Include);
            E2EHarness.Assert(_lantern != null, "랜턴이 있다");

            남은것을_치운다();

            int 잠든생물 = E2EHarness.SleepWildCreatures();
            int 끈광원 = E2EHarness.MuteAmbientLitZones();
            E2EHarness.Log($"  무대 정리: 야생 생물 {잠든생물}마리, 주변 광원 {끈광원}곳");

            var inv = E2EHarness.Player.Inventory;
            if (inv.Inventory.CountOf("lantern") == 0)
            {
                var item = inv.Database.GetById("lantern");
                if (item != null) inv.Inventory.TryAdd(item, 1);
            }

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

        // ── 무대 ────────────────────────────────────────────────

        static IEnumerator 무대를_고른다()
        {
            _바다 = Vector3.zero;
            _내륙 = Vector3.zero;
            _올려둘곳 = Vector3.zero;

            E2EHarness.Assert(
                WaterBody.TryGetSurfaceAt(new Vector3(사람자리.x, 0f, 사람자리.z), out _액면),
                "세계에 액면이 있다");

            bool 육지 = E2EScytheHabitat.육지를_찾는다(사람자리, 3f, 90f, out _내륙);
            E2EHarness.Assert(육지, "사람이 설 어두운 육지를 찾았다");
            if (!육지) yield break;

            // 낫을 세울 바다는 그 육지의 감지 반경 안이어야 한다. 밖이면 "빛에 막혔다"와
            // "애초에 못 봤다"가 구별되지 않는다.
            bool 바다 = E2EScytheHabitat.서식지를_찾는다(_내륙, 4f, _def.detectRadius - 2f,
                                                        true, out _바다);
            E2EHarness.Assert(바다, "그 곁에 어두운 액면이 있다");
            if (!바다) yield break;

            // 육지에 올려 놓아 볼 자리. 사람 위에 겹치면 발밑을 재는 광선이 사람의 몸부터
            // 맞아 육지 한복판에서 구역이 Liquid로 읽힌다(E2EScytheHabitat 실측).
            bool 올릴곳 = E2EScytheHabitat.육지를_찾는다(_내륙, 2.5f, 8f, out _올려둘곳);
            E2EHarness.Assert(올릴곳, "사람과 겹치지 않는 육지 자리를 찾았다");
            if (!올릴곳) yield break;

            E2EHarness.Log($"  무대: 바다 {_바다.ToString("F1")} · 내륙 {_내륙.ToString("F1")} · " +
                           $"올려둘곳 {_올려둘곳.ToString("F1")} (액면 {_액면:F2})");
            yield return null;
        }

        static IEnumerator 낫을_세운다()
        {
            var prefab = 프리팹();
            if (prefab == null) yield break;

            var go = Object.Instantiate(prefab, _바다, Quaternion.identity);
            go.name = "E2E_낫꼬리";
            yield return null;
            yield return null;

            _낫 = go.GetComponent<CreatureBrain>();
            _몸 = _낫 != null ? _낫.Motor as HoverDrifter : null;
            _꼬리 = go.GetComponent<ScytheTail>();

            E2EHarness.Assert(_몸 != null, "부유하는 몸이 스스로 붙었다");
            E2EHarness.Assert(_꼬리 != null, "꼬리가 스스로 붙었다 — 프리팹을 고치지 않았다");
            if (_꼬리 == null) yield break;

            E2EHarness.Assert(_꼬리.HasTail,
                              "프리팹에서 꼬리 부품(Tail·TailBlade·TailSpike)을 찾았다");

            for (int i = 0; i < 30; i++) yield return null;
            E2EHarness.Log($"  낫 {go.transform.position.ToString("F1")} 구역 {_몸.Zone} " +
                           $"자세 {_꼬리.Posture}");
        }

        // ── 1. 평시 액면 = 작업 중 ──────────────────────────────

        static IEnumerator 평시_액면에서는_작업_중이다()
        {
            E2EHarness.Log("— 평시 액면: 꼬리를 늘어뜨려 훑는다 —");

            yield return 랜턴(false);
            yield return 작업으로_돌아갈_때까지_민다();

            // <b>관찰창은 전제가 선 자리에서 시작해야 한다.</b> 위의 밀어내기는
            // "평시 자세가 되었는가"를 보고 끝나는데, 4상태가 몸을 몰기 시작한 뒤로는
            // 그 순간이 훨씬 빨리 온다 — 감지에서 벗어난 두 프레임 뒤면 순찰이다.
            // 그래서 개체가 아직 관성으로 다가오는 중일 수 있고, 관찰 첫 1초 안에
            // 제 발로 감지 반경에 들어와 꼬리를 든다(실측 1069/1118).
            // 재는 것은 <b>평시의 자세</b>이므로, 재기 전에 평시를 만들어 둔다.
            //
            // <b>순서가 중요하다.</b> 사람은 「낫에서 32m」로 물러나므로, 낫을 먼저
            // 제자리에 놓고 나서 물러나야 한다. 거꾸로 하면 물러난 기준이 옛 자리라
            // 다시 붙은 낫이 코앞에 있다(실측 844/1211).
            _낫.transform.position = _바다;
            E2EHarness.SyncPhysics();
            yield return null;
            yield return 물러난다(_def.detectRadius + 18f);

            // 무대를 흔들어 놓았으니 가라앉기를 기다린다. 순간이동한 프레임에는 두뇌가
            // 아직 옛 거리를 들고 있어 한두 프레임 꼬리가 들렸다 내려온다(실측 33/1182).
            // 재려는 것은 <b>평시의 자세</b>이지 무대를 흔든 직후의 흔들림이 아니다.
            yield return E2EHarness.WaitUntil(() => _꼬리.Posture == ScythePosture.Trailing,
                                              "관찰을 시작하기 전에 평시로 가라앉는다", 6f);

            var 본자세 = new System.Collections.Generic.HashSet<ScythePosture>();
            int 작업프레임 = 0, 전체프레임 = 0, 훑은프레임 = 0;
            int 육지프레임 = 0, 설명안되는프레임 = 0;
            var 이전구역 = _몸.Zone;

            // 무대를 1초마다 다시 붙든다. 2초 간격으로는 6.2 m/s가 12m를 좁혀 와
            // 관찰 도중에 감지 반경 안으로 들어온다.
            float t = 0f, 다음정렬 = 1f;
            while (t < 6f)
            {
                if (t >= 다음정렬)
                {
                    다음정렬 = t + 1f;
                    if (Vector3.Distance(_낫.transform.position, 사람자리) < _def.detectRadius + 8f)
                        yield return 물러난다(_def.detectRadius + 18f);

                    // 배회하다 물가로 올라서면 훑을 액면이 발밑에서 사라진다. 자세는
                    // 여전히 작업 중이지만(그것이 규칙이다) 이 시나리오가 함께 재려는
                    // 빛줄기를 못 잰다 — 실측으로 6초 내내 Shore에 있던 판이 있었다.
                    // 바다 자리는 무대를 고를 때 액면임을 확인한 점이다.
                    if (_몸.Zone != HabitatZone.Liquid)
                    {
                        _낫.transform.position = _바다;
                        E2EHarness.SyncPhysics();
                    }
                }

                본자세.Add(_꼬리.Posture);
                전체프레임++;
                if (_꼬리.Posture == ScythePosture.Trailing) 작업프레임++;

                // <b>경계 프레임 하나를 함께 센다.</b> 자세를 정하는 것은 ScytheTail이고
                // 구역을 재는 것은 HoverDrifter인데, 둘의 Update 순서는 정해져 있지 않다.
                // 그래서 육지에서 액면으로 넘어오는 <b>그 한 프레임</b>은 "자세는 아직
                // 육지 값, 구역은 이미 액면"으로 읽힐 수 있다. 실측으로 나온 것이 정확히
                // 1프레임이었다(1219 중 1, 1227 중 1). 이것은 규칙이 뚫린 것이 아니라
                // 두 부품을 한 프레임에 함께 들여다본 탓이다.
                else if (_몸.Zone == HabitatZone.Inland || 이전구역 == HabitatZone.Inland) 육지프레임++;
                else 설명안되는프레임++;

                if (ScytheStance.Skims(_꼬리.Posture, _몸.Zone)) 훑은프레임++;
                이전구역 = _몸.Zone;

                t += Time.deltaTime;
                yield return null;
            }

            E2EHarness.Log($"  {t:F0}초: 작업 {작업프레임}/{전체프레임} 프레임, " +
                           $"액면을 훑은 {훑은프레임} 프레임, 육지로 읽힌 {육지프레임} 프레임, " +
                           $"본 자세 {string.Join("·", 본자세)}");

            // <b>"액면 위에서"가 이 문장의 조건이다.</b> 마지막 한 프레임이 마침 물가를
            // 스치는 프레임이면 규칙이 Furled를 내라고 한 자리에서 Trailing을 요구하게
            // 된다(실측 10회 중 1회). 발밑이 액면인 프레임에서 재는 것이 문장 그대로다.
            yield return E2EHarness.WaitUntil(() => _몸.Zone == HabitatZone.Liquid,
                                              "발밑이 액면인 프레임에서 잰다", 4f);
            E2EHarness.AssertEqual(_꼬리.Posture, ScythePosture.Trailing,
                                   "평시 액면 위에서는 작업 자세다");

            // <b>육지로 읽힌 프레임에 공격 태세인 것은 규칙이 시킨 것이다.</b>
            // ScytheStance는 "육지에 있다는 것 자체가 은폐 프로토콜을 버렸다는 뜻"이라
            // 자리를 상태보다 앞세운다. 그런데 발밑 지형은 광선으로 재므로 물가를 스칠 때
            // 한두 프레임 육지로 읽힌다 — 바로 아래 접촉 단언이 같은 이유로 프레임 단위
            // 단언을 이미 포기하고 있다(실측 1228 중 93).
            //
            // 그래서 <b>자세만 프레임 단위로 100%를 요구하면 규칙과 어긋난다</b> —
            // 규칙이 Furled를 내라고 한 프레임에 Trailing을 요구하는 꼴이다.
            // 기대를 낮추는 것이 아니라, 재는 것을 규칙에 맞춘다: 육지로 읽히지 않았는데도
            // 작업 자세가 아닌 프레임이 <b>하나라도</b> 있으면 실패다.
            E2EHarness.Assert(설명안되는프레임 == 0,
                              $"평시에 작업 자세가 아닌 프레임은 전부 물가를 스친 것이다 " +
                              $"(설명 안 되는 {설명안되는프레임} / 육지 {육지프레임} / 전체 {전체프레임})");
            // <b>접촉은 프레임 단위로 단언하지 않는다.</b> 배회하는 개체는 물가를 스치므로
            // 발밑이 Shore로 읽히는 프레임이 섞인다 — 실측 1228 중 93. 그것은 규칙이
            // 뚫린 것이 아니라 지도의 굴곡이고, 그때도 자세는 여전히 작업 중이다.
            // 재야 할 것은 "훑고 있는가"이지 "한 프레임도 빠짐없이 닿았는가"가 아니다.
            E2EHarness.Assert(훑은프레임 > 전체프레임 / 2,
                              $"꼬리가 액면을 훑고 있다 ({훑은프레임}/{전체프레임}) — " +
                              "멀리서 보이는 빛줄기가 여기서 난다");
            E2EHarness.Assert(_꼬리.Glow.Blade < _꼬리.Glow.Arc,
                              "빛이 날에 뭉치지 않고 호를 따라 퍼져 있다");
        }

        // ── 2. 빛에 막히면 경계 ─────────────────────────────────

        static IEnumerator 빛에_막히면_작업을_멈춘다()
        {
            E2EHarness.Log("— 감지 반경 안 + 랜턴: 작업을 멈추고 든다 —");

            yield return 선다(_내륙);
            yield return 랜턴(true);

            // 사람이 감지 반경 안에 있어야 한다. 육지에 서 있으므로 낫은 물가에서 멈춘다.
            //
            // <b>거리는 꼬리가 올라간 그 순간에 잰다.</b> 반복문을 빠져나온 뒤에 재면
            // 마지막 자리 정렬(최대 1.5초 전)과 측정 사이에 6.2 m/s로 미끄러지는 개체가
            // 반경을 넘어설 수 있고, 그러면 <b>규칙이 다 지켜졌는데도</b> 전제가 거짓으로
            // 읽힌다(실측 14.2 > 14). 이 단언이 말하려는 것은 "든 그때 감지 안이었는가"이지
            // "다 끝나고도 감지 안인가"가 아니다.
            bool 들었다 = false;
            float 거리 = float.MaxValue;
            float t = 0f;
            while (t < 8f && !들었다)
            {
                if (t % 1.5f < Time.deltaTime) yield return 선다(_내륙);

                // <b>사람이 낫을 마주 본다 — 그것이 "빛에 막힌"의 전제다.</b>
                // 랜턴 웅덩이는 앞으로 밀려 있어 앞쪽만 지키므로(§19), 등지고 서면
                // 낫이 사각으로 돌아 들어와 <b>규칙대로</b> 교전에 올라간다.
                // 4상태가 몸을 몰기 시작한 뒤로 그 규칙이 실제로 물어서, 이 절이
                // 재려는 경계 자세 대신 공격 태세가 찍혔다(실측 10회 중 1회).
                // 몸을 돌리는 것은 플레이어의 방어이지 검사의 편의가 아니다.
                E2EHarness.LookAt(_낫.transform.position);

                들었다 = _꼬리.Posture == ScythePosture.Raised;
                if (들었다) 거리 = Vector3.Distance(_낫.transform.position, 사람자리);

                t += Time.deltaTime;
                yield return null;
            }

            if (!들었다) 거리 = Vector3.Distance(_낫.transform.position, 사람자리);
            E2EHarness.Log($"  {t:F1}초: 자세 {_꼬리.Posture}, 상태 {_낫.State}, " +
                           $"거리 {거리:F1} (감지 반경 {_def.detectRadius})");

            E2EHarness.Assert(거리 <= _def.detectRadius,
                              $"사람이 감지 반경 안에 있다 ({거리:F1} <= {_def.detectRadius})");
            E2EHarness.Assert(들었다,
                              $"빛에 막힌 채 감지만 살아 있으면 꼬리를 든다 (자세 {_꼬리.Posture})");
            E2EHarness.Assert(_꼬리.Glow.Contact == 0f,
                              "든 꼬리는 액면에 닿지 않는다 — 빛줄기가 끊긴다");

            yield return 랜턴(false);
        }

        // ── 3. 육지 = 공격 태세 ─────────────────────────────────

        /// <summary>
        /// <b>이 항목의 본문이다.</b> 발령 태세로 세우고 육지 좌표에 올려 놓는다.
        ///
        /// 쫓아 올라오게 하지 않는 이유는 <see cref="E2EScytheHabitat.육지에_둘_수_있는가"/>에
        /// 적어 둔 것과 같다 — 이 섬의 물가는 대개 벼랑이라 그 무대에서는 지형의 경사를
        /// 재게 되지 규칙을 재지 못한다. 무엇이 발령을 거는가는 스펙 §5의 몫이고
        /// 여기서는 태세만 세운다.
        /// </summary>
        static IEnumerator 육지에_오르면_공격_태세다()
        {
            E2EHarness.Log("— 육지 진입: 몸에 바짝 붙인다 —");

            yield return 랜턴(false);
            yield return 선다(_내륙);

            ScytheWatch.Set(ScytheAlert.Alarmed);
            _낫.transform.position = _올려둘곳 + Vector3.up * 0.6f;
            E2EHarness.SyncPhysics();
            yield return null;
            yield return null;

            E2EHarness.AssertEqual(_몸.Zone, HabitatZone.Inland, "육지 좌표에 올려 놓았다");

            int 공격프레임 = 0, 전체프레임 = 0;
            float t = 0f;
            while (t < 4f)
            {
                if (_꼬리.Posture == ScythePosture.Furled) 공격프레임++;
                전체프레임++;
                t += Time.deltaTime;
                yield return null;
            }

            E2EHarness.Log($"  {t:F0}초: 공격 태세 {공격프레임}/{전체프레임} 프레임, " +
                           $"구역 {_몸.Zone}, 상태 {_낫.State}");

            E2EHarness.AssertEqual(_꼬리.Posture, ScythePosture.Furled, "육지에서는 공격 태세다");
            E2EHarness.Assert(공격프레임 == 전체프레임,
                              $"올려 둔 내내 공격 태세였다 ({공격프레임}/{전체프레임})");
            E2EHarness.Assert(_꼬리.Glow.Contact == 0f, "육지에서는 빛줄기가 사라진다");
            E2EHarness.Assert(_꼬리.Glow.Blade > _꼬리.Glow.Arc,
                              "대신 날에 응축된다 — 넓게 퍼진 빛에서 뭉친 빛으로");
        }

        // ── 4. 되돌아온다 ───────────────────────────────────────

        static IEnumerator 물로_돌아오면_작업으로_돌아간다()
        {
            E2EHarness.Log("— 되돌아오는 길 —");

            ScytheWatch.Set(ScytheAlert.Calm);
            _낫.transform.position = _바다;
            E2EHarness.SyncPhysics();
            yield return 랜턴(false);
            yield return 작업으로_돌아갈_때까지_민다();

            // <b>물 위에 있다는 것은 이 문장의 주어가 아니라 전제다.</b>
            // "물로 돌아오면 작업으로 돌아간다"에서 재려는 것은 <b>작업으로 돌아가는가</b>이고,
            // 물 위에 있는 것은 그 물음이 성립하기 위한 조건이다. 그런데 위의 밀어내기가
            // 도는 동안 개체는 제 배회 반경(6m) 안에서 자유롭게 미끄러지고, 그 반경에는
            // 해안선이 들어 있다. 해안선은 <b>서식 범위 안</b>이므로(기획서 §4.5) 거기 있는
            // 것은 규칙을 어긴 것이 아니지만, 발밑에 훑을 액면이 없어 Skims가 false다
            // (ScytheStance.Skims가 스스로 그렇게 적어 두었다 — "해안선에서 액면이 없는 것은
            // 경계에 들어간 것이 아니다").
            //
            // 그래서 관찰창이 해안선에서 시작하면 <b>규칙이 다 지켜졌는데도</b> 이 절이
            // 실패한다. 실측: main에서 8회 중 3회, 이 브랜치에서 14회 중 5회.
            // 기대를 낮추는 것이 아니라 <b>전제를 다시 세운다</b> — 절의 첫 줄이 이미 하던
            // 것과 같은 일이고, 밀어내기를 지나며 흐트러진 것을 되돌리는 것뿐이다.
            _낫.transform.position = _바다;
            E2EHarness.SyncPhysics();
            yield return null;

            // <b>여기서 재는 것은 "언제 돌아오는가"이지 "몇 프레임 만인가"가 아니다.</b>
            // 물에 내려놓은 개체는 곧바로 배회를 시작하고, 6.2 m/s면 4초에 25m를 간다 —
            // 관찰하는 동안 제 발로 감지 반경에 다시 들어와 꼬리를 드는 것은 규칙대로
            // 움직인 것이지 되돌아오지 못한 것이 아니다(실측 553/966). 그래서 사람을
            // 계속 밀어내면서 <b>작업 자세와 접촉이 실제로 돌아오는지</b>만 본다.
            bool 돌아왔다 = false, 다시_훑는다 = false;
            int 액면프레임 = 0, 전체프레임 = 0;
            float t = 0f, 다음정렬 = 1.5f;
            while (t < 12f && !(돌아왔다 && 다시_훑는다))
            {
                if (t >= 다음정렬)
                {
                    다음정렬 = t + 1.5f;
                    if (Vector3.Distance(_낫.transform.position, 사람자리) < _def.detectRadius + 8f)
                        yield return 물러난다(_def.detectRadius + 18f);
                }

                if (_꼬리.Posture == ScythePosture.Trailing) 돌아왔다 = true;
                if (ScytheStance.Skims(_꼬리.Posture, _몸.Zone)) 다시_훑는다 = true;

                전체프레임++;
                if (_몸.Zone == HabitatZone.Liquid) 액면프레임++;

                t += Time.deltaTime;
                yield return null;
            }

            // 실패했을 때 <b>왜</b>인지 로그만 보고 가를 수 있어야 한다 — 해안선으로
            // 흘러간 것인지, 교전에 들어간 것인지가 자세 하나로는 갈리지 않는다.
            E2EHarness.Log($"  {t:F1}초: 자세 {_꼬리.Posture}, 구역 {_몸.Zone}, " +
                           $"상태 {_낫.State}, 4상태 {상태이름()}, 어그로 {_낫.AggroLeft:F1}, " +
                           $"액면 {액면프레임}/{전체프레임} 프레임");
            E2EHarness.Assert(돌아왔다, "올리던 것이 전부 사라지면 작업으로 돌아간다");
            E2EHarness.Assert(다시_훑는다, "빛줄기가 다시 수면을 긋는다");
            E2EHarness.AssertEqual(_꼬리.Posture, ScythePosture.Trailing,
                                   "공격 태세가 남아 있지 않다");
        }

        /// <summary>
        /// 4상태를 로그에 적기 위한 이름. 부품이 없으면 물음표다 —
        /// 없다는 사실 자체가 진단에 필요한 정보다.
        /// </summary>
        static string 상태이름()
        {
            var mind = _낫 != null ? _낫.GetComponent<ScytheMind>() : null;
            return mind != null ? mind.State.ToString() : "?";
        }

        // ── 자리 잡기 ───────────────────────────────────────────

        /// <summary>
        /// 낫이 <b>제 일로 돌아갈 때까지</b> 사람을 밀어낸다.
        ///
        /// 한 번 물러나고 어그로만 기다리는 것으로는 부족하다 — 6.2 m/s로 미끄러지는
        /// 개체는 기다리는 동안 다시 감지 반경 안으로 들어오고, 선공 성향이라 보고
        /// 있는 내내 어그로가 다시 채워져 시계가 영영 0이 되지 않는다.
        /// 재려는 것은 <b>평시의 자세</b>이므로, 평시가 될 때까지 무대를 만든다.
        /// </summary>
        static IEnumerator 작업으로_돌아갈_때까지_민다()
        {
            float t = 0f, 다음정렬 = 0f;
            while (t < 25f && _꼬리.Posture != ScythePosture.Trailing)
            {
                if (t >= 다음정렬)
                {
                    다음정렬 = t + 1.5f;
                    if (Vector3.Distance(_낫.transform.position, 사람자리) < _def.detectRadius + 8f)
                        yield return 물러난다(_def.detectRadius + 18f);
                }

                t += Time.deltaTime;
                yield return null;
            }

            E2EHarness.Log($"  {t:F1}초 만에 평시로 (자세 {_꼬리.Posture}, " +
                           $"거리 {Vector3.Distance(_낫.transform.position, 사람자리):F1})");
        }

        static IEnumerator 어그로가_식기를_기다린다()
        {
            float t = 0f;
            while (t < _def.aggroSeconds + 3f && _낫.AggroLeft > 0f)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>
        /// 낫에게서 <b>걸어갈 수 있는 한 가장 멀리</b> 물러난다.
        ///
        /// <b>원하는 거리를 못 채우면 포기하지 않고 차선을 고른다.</b> 처음에는 거리를
        /// 못 채우면 원래 자리로 돌아가게 두었는데, 좁은 무대에서는 그 자리가 낫에게서
        /// 4m라 개체가 영원히 추격 상태로 굳었다(실측: 세 번 연속 "평시인데 공격 태세").
        /// 섬이 작아 28m를 못 벌 수는 있어도, <b>가장 먼 자리</b>는 언제나 있다.
        /// </summary>
        static IEnumerator 물러난다(float 거리m)
        {
            Vector3 기준 = _낫.transform.position;
            Vector3 뒤 = 사람자리 - 기준;
            뒤.y = 0f;
            if (뒤.sqrMagnitude < 0.01f) 뒤 = Vector3.forward;
            뒤.Normalize();

            Vector3 최선 = _내륙;
            float 최장 = Vector3.Distance(_내륙, 기준);

            foreach (float r in new[] { 거리m, 거리m * 0.7f, 거리m * 0.45f })
            {
                for (int i = 0; i < 16; i++)
                {
                    float 각 = (i % 2 == 0 ? 1f : -1f) * 22.5f * ((i + 1) / 2);
                    Vector3 후보 = 기준 + (Quaternion.Euler(0f, 각, 0f) * 뒤) * r;
                    if (!UnityEngine.AI.NavMesh.SamplePosition(후보, out var hit, 15f,
                                                               UnityEngine.AI.NavMesh.AllAreas))
                        continue;

                    float d = Vector3.Distance(hit.position, 기준);
                    if (d <= 최장) continue;

                    최선 = hit.position;
                    최장 = d;
                    if (d >= 거리m * 0.9f) { yield return 선다(최선); yield break; }
                }
            }

            yield return 선다(최선);
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
        /// 랜턴에 불이 들어오게 하거나 꺼지게 한다.
        ///
        /// <b>F는 더 이상 없다.</b> 랜턴은 상시 점등이 전제라(스펙 §12) 끄는 조작이
        /// 아예 없고, 불이 없는 상태에 이르는 유일한 길이 배터리를 다 쓰는 것이다.
        /// 여기서는 그 길이 시나리오의 절반을 지탱한다 — 어두워야 낫이 물러나지 않고,
        /// 밝아야 <b>추격이 막힌 채 감지만 살아 있는</b> 경계 자세가 성립한다.
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
            _꼬리 = null;

            foreach (var b in Object.FindObjectsByType<CreatureBrain>(FindObjectsInactive.Include))
                if (b != null && b.name.StartsWith("E2E_낫꼬리"))
                    Object.Destroy(b.gameObject);
        }

        static IEnumerator 치운다()
        {
            남은것을_치운다();
            yield return 랜턴(false);
            yield return E2EHarness.ReleaseAllKeys();
            E2EHarness.RestoreWorld();
            Vitals.Health.Modify(Vitals.Health.Max);
            yield return null;
        }

        // ── 사람의 눈을 위한 것 ─────────────────────────────────

        /// <summary>
        /// 세 자세의 실루엣을 <b>사람이 보라고</b> 세운다. 자동 판정은 여기까지 못 온다 —
        /// "사람이 구별할 수 있는가"가 이 항목의 최종 판정이므로(스펙 §4) 화면을 찍어 낸다.
        ///
        /// 마지막에 시간을 멈춘다. 바깥에서 스크린샷을 찍는 동안 개체가 움직여 버리면
        /// 같은 자세를 다시 잡아 줄 방법이 없다. 다음 실행의 첫 줄이 되돌린다.
        /// </summary>
        /// <param name="자세번호">0 작업 중 · 1 경계 · 2 공격 태세</param>
        public static IEnumerator 실루엣을_세운다(int 자세번호)
        {
            Time.timeScale = 1f;

            yield return 준비();
            if (_def == null) yield break;

            yield return 무대를_고른다();
            if (_바다 == Vector3.zero) yield break;

            yield return 낫을_세운다();
            if (_꼬리 == null) yield break;

            var 원하는것 = 자세번호 == 0 ? ScythePosture.Trailing
                         : 자세번호 == 1 ? ScythePosture.Raised
                                         : ScythePosture.Furled;

            switch (자세번호)
            {
                case 0:
                    yield return 랜턴(false);
                    _낫.transform.position = _바다;
                    E2EHarness.SyncPhysics();
                    yield return 작업으로_돌아갈_때까지_민다();
                    break;

                case 1:
                    yield return 선다(_내륙);
                    yield return 랜턴(true);
                    break;

                default:
                    yield return 랜턴(false);
                    yield return 선다(_내륙);
                    ScytheWatch.Set(ScytheAlert.Alarmed);
                    _낫.transform.position = _올려둘곳 + Vector3.up * 0.6f;
                    E2EHarness.SyncPhysics();
                    break;
            }

            // <b>판단이 원하는 답을 내는 그 프레임에 얼린다.</b> 두 가지가 이 순서를 강요한다.
            // <list type="number">
            // <item>옆모습을 잡으려면 4m까지 다가가야 하는데 감지 반경이 14m다. 살려 두면
            //       찍히는 것은 찍으려던 자세가 아니라 <b>카메라를 알아본 자세</b>다
            //       (실측: 작업 중을 찍으려다 공격 태세가 나왔다)</item>
            // <item>작업 중인 개체는 배회하다 물가로 올라선다. 놓아 두면 액면 접촉이
            //       끊긴 자리에서 굳어 이 자세의 절반인 <b>수면 빛줄기</b>가 화면에 없다
            //       (실측: 14초를 기다려도 Shore에서 멈췄다)</item>
            // </list>
            // 얼려도 꼬리는 살아 있다 — 얼린 값 위에서 같은 답을 계속 내고, 보간만 마저 돈다.
            float t = 0f;
            bool 섰다 = false;
            while (t < 14f && !섰다)
            {
                // <b>무대를 매 프레임 붙들고 있어야 한다.</b> 놓아 두면 6.2 m/s로
                // 미끄러지는 개체가 배회하다 물가로 올라서서 액면 접촉이 끊기고
                // (실측: 34m를 흘러가 Shore에서 굳었다), 사람 쪽으로 다가오면 아예
                // 공격 태세가 된다. 작업 자세는 바다 자리에 못 박아 두고 찍는다.
                if (자세번호 == 0)
                {
                    _낫.transform.position = _바다;
                    E2EHarness.SyncPhysics();
                }
                else if (자세번호 == 1) yield return 선다(_내륙);

                t += Time.deltaTime;
                yield return null;

                섰다 = _꼬리.Posture == 원하는것 &&
                       (자세번호 != 0 || ScytheStance.Skims(_꼬리.Posture, _몸.Zone));
            }

            얼린다();

            // 얼린 다음에 보간이 끝나기를 기다린다. 전환 중에 찍으면 셋이 다 비슷해 보인다.
            for (float s = 0f; s < 2.5f; s += Time.deltaTime) yield return null;

            yield return 눈을_맞춘다(자세번호);

            E2EHarness.Log($"  세운 자세 {_꼬리.Posture} (원한 것 {원하는것}), " +
                           $"호 {_꼬리.Glow.Arc:F2} 접점 {_꼬리.Glow.Contact:F2} " +
                           $"날 {_꼬리.Glow.Blade:F2}");
            E2EHarness.AssertEqual(_꼬리.Posture, 원하는것, "찍으려던 자세로 섰다");

            Time.timeScale = 0f;
        }

        /// <summary>
        /// 판단과 이동을 멈춘다. <b>꼬리만 살려 둔다</b> — 자세는 이미 잡혔고,
        /// 얼린 값 위에서 <see cref="ScytheTail"/>이 같은 답을 계속 낸다.
        /// </summary>
        static void 얼린다()
        {
            if (_낫 != null) _낫.enabled = false;
            if (_몸 != null) _몸.enabled = false;

            var guard = _꼬리.GetComponent<CreatureCollisionGuard>();
            if (guard != null) guard.enabled = false;

            var shedder = _꼬리.GetComponent<RelicShedder>();
            if (shedder != null) shedder.enabled = false;
        }

        /// <summary>
        /// 낫이 <b>옆모습</b>으로 보이게 무대를 다시 짠다.
        ///
        /// <b>사람이 낫에게 가는 것이 아니라 낫을 사람 앞에 옮긴다.</b> 카메라를 개체
        /// 옆으로 보내 보았더니 물 위였고, 사람은 헤엄쳐 가라앉아 화면이 통째로
        /// 물속이 됐다(실측). 얼린 개체는 어디에 놓아도 자세가 변하지 않으므로,
        /// 사람은 어두운 육지에 서고 낫이 그 앞으로 온다.
        ///
        /// <b>옆에서 본다.</b> 꼬리는 몸 뒤에 달려 있어 정면·후면에서는 몸통에 겹치고,
        /// 셋을 가르는 것은 <b>꼬리가 어느 높이에 있는가</b>라 옆모습이어야 한 눈에 갈린다.
        /// 공격 태세만 육지 자리에 세운다 — 육지에 올라온 것 자체가 그 자세의 내용이다.
        /// </summary>
        static IEnumerator 눈을_맞춘다(int 자세번호)
        {
            yield return 선다(_내륙);
            yield return null;

            // <b>사람이 낫에게 가는 것이 아니라 낫이 사람 앞으로 온다.</b> 이 물가는
            // 바위와 거대 버섯으로 빽빽해서, 개체 둘레 13m를 360도로 훑어도 시야가
            // 트인 자리가 한 곳도 없었다(실측). 낫은 이미 얼어 있어 어디에 놓아도
            // 자세가 변하지 않으므로, 트인 방향을 찾아 그쪽 허공에 세우는 편이 확실하다.
            // <b>먼바다로 나가서 찍는다.</b> 물가에서는 어느 방향을 보아도 무언가가
            // 앞을 막았다. 물리로 트인 방향을 고르려 해 보았지만, 이 세계의 바위와
            // 버섯 상당수는 <b>콜라이더가 없어 광선이 그냥 통과</b>한다 — "트였다"고
            // 판정한 방향이 화면에서는 통째로 바윗덩이였다(실측 두 번).
            // 액면 위에는 가릴 것이 없고, 셋을 <b>같은 배경·같은 거리</b>에서 찍으면
            // 사람이 자세만 비교할 수 있다.
            Vector3 바깥 = _바다 - _내륙;
            바깥.y = 0f;
            if (바깥.sqrMagnitude < 1e-4f) 바깥 = Vector3.forward;
            바깥.Normalize();

            Vector3 캠자리 = _바다;
            foreach (float 나감 in new[] { 18f, 12f, 8f, 26f })
            {
                Vector3 c = _바다 + 바깥 * 나감;
                Vector3 앞 = c + 바깥 * 3.2f;
                if (!WaterBody.TryGetSurfaceAt(new Vector3(c.x, 0f, c.z), out _)) continue;
                if (!WaterBody.TryGetSurfaceAt(new Vector3(앞.x, 0f, 앞.z), out _)) continue;

                캠자리 = c;
                break;
            }

            // <b>눈높이를 낫에 맞춘다.</b> 위에서 내려다보면 늘어뜨린 꼬리가 몸통에
            // 가려 작업 자세와 공격 태세가 비슷해 보인다. 액면에 눈을 붙이면 셋이
            // 옆모습으로 서고, 어두운 먼 수면이 배경이 되어 실루엣이 뜬다.
            캠자리.y = _액면 + 0.8f;

            // 3.2m — 몸 길이가 2m쯤이다. 5m에서는 화면에서 손톱만 해 꼬리 각도가 안 읽혔다(실측).
            Vector3 놓을곳 = 캠자리 + 바깥 * 3.2f;
            놓을곳.y = _액면 + 0.6f;
            _낫.transform.position = 놓을곳;

            // 몸을 카메라에 대해 옆으로 돌린다. 꼬리 각도는 옆에서만 읽힌다.
            _낫.transform.rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, 바깥));
            E2EHarness.SyncPhysics();

            E2EHarness.Log($"  카메라 {캠자리.ToString("F1")} → 낫 {놓을곳.ToString("F1")}");

            // 사람은 액면 위 허공에 뜬다. 매 프레임 다시 세워 가라앉지 않게 한다.
            for (int i = 0; i < 10; i++)
            {
                E2EHarness.Teleport(캠자리);
                E2EHarness.LookAt(놓을곳 + Vector3.up * 0.9f);
                yield return null;
            }
        }

    }
}
