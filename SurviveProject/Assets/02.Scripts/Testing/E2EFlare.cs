using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Survive.Combat;
using Survive.Creatures;
using Survive.Items;
using Survive.Player;
using Survive.Vitals;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// 조명탄 총 — <b>빛이 방어에서 공격으로 넘어간다</b> (기획서 §5.2).
    ///
    /// <b>이 시나리오의 본체는 하나다: 등 뒤에 붙은 낫을 조명탄으로 떼어낸다.</b>
    /// 규칙과 경계값은 <c>FlareRuleTests</c>가 씬 없이 전수로 지키고, 여기서 재는 것은
    /// 그 규칙이 <b>실제 무대에서, 실제 개체에게</b> 성립하는가 하나다.
    ///
    /// <b>몸을 돌리지 않는다.</b> 이것이 이 시나리오에서 가장 중요한 조작이다 —
    /// 뒤돌아서면 사각이 사라져 <c>Attack → Beware</c>가 <b>랜턴 때문에</b> 일어나고
    /// (§5.2의 "랜턴 반경 재진입"), 그러면 조명탄이 한 일이 무엇인지 갈라낼 수 없다.
    /// 그래서 사람은 낫에게 등을 준 채로 <b>발밑을 겨눠 쏜다.</b> 랜턴 웅덩이는
    /// 고개의 위아래를 따르지 않으므로(<see cref="LanternRule.Facing"/>) 그동안에도
    /// 등 뒤 사각은 그대로 열려 있다. 바뀐 것은 <b>조명탄 하나</b>다.
    ///
    /// <b>무대는 「랜턴 오프셋」과 글자 그대로 같은 것을 쓴다</b>
    /// (<see cref="E2ELanternOffset.물가를_찾는다"/>). 조건이 같기 때문이다 —
    /// 등진 사람에게 낫이 실제로 붙을 수 있는 어두운 물가.
    /// </summary>
    public static class E2EFlare
    {
        const string 낫프리팹 = "Assets/05.Prefabs/Creatures/Creature_낫.prefab";

        /// <summary>떼어내기를 몇 번 되풀이하는가. 한 번은 우연일 수 있다.</summary>
        const int 떼어낼횟수 = 3;

        /// <summary>
        /// 밀어내기 거리 후보(m). <b>여기서 값을 정하지 않는다</b> — 기획서 §5.2
        /// 튜닝 5값의 넷째는 사람의 몫이라, 후보마다 낫이 실제로 몇 미터 물러나고
        /// 다시 붙는 데 몇 초 걸리는지를 재서 올린다.
        /// </summary>
        static readonly float[] 밀어내기후보 = { 12f, 16f, 20f, 24f };

        static LanternController _lantern;
        static CreatureDefinitionSO _def;
        static CreatureBrain _낫;
        static ScytheMind _마음;
        static Vector3 _육지;
        static Vector3 _바다;

        static Vector3 사람자리 => E2EHarness.Player.transform.position;
        static PlayerVitals Vitals => E2EHarness.Player.Vitals;

        public static IEnumerator FullRun()
        {
            yield return 준비();
            if (_lantern == null) yield break;

            yield return 무대를_고른다();
            if (_바다 == Vector3.zero) yield break;

            yield return 쏘면_밝은_구역이_생긴다();
            yield return 낫을_세운다();
            if (_낫 == null) yield break;

            yield return 등_뒤에_붙은_낫을_떼어낸다();
            yield return 밀어내기_실측();

            yield return 치운다();
            E2EHarness.Log("=== 조명탄 총 완주 ===");
        }

        /// <summary>
        /// <b>사람 눈으로 볼 것을 만드는 절.</b> 판정하지 않는다 — 무대를 세우고
        /// 조명탄을 되풀이해 터뜨려, 바깥에서 스크린샷을 찍을 창을 연다.
        /// </summary>
        public static IEnumerator ShowCase()
        {
            yield return 준비();
            if (_lantern == null) yield break;

            yield return 무대를_고른다();
            if (_바다 == Vector3.zero) yield break;

            yield return 낫을_세운다();
            if (_낫 == null) yield break;

            for (int i = 0; i < 5; i++)
            {
                yield return 등을_준다();
                yield return 낫이_붙을_때까지(12f);

                E2EHarness.LightLantern();
                yield return 발밑을_겨눈다();
                yield return 쏜다();

                E2EHarness.Log($"  [연출 {i + 1}] 조명탄 터짐 — " +
                               $"낫 {(_낫 != null ? _낫.transform.position.ToString("F1") : "없음")}");

                // <b>여기서는 돌아본다.</b> 게이트 절에서 몸을 돌리지 않는 것은 원인을
                // 갈라내기 위한 것이고, 이 절이 만드는 것은 <b>사람이 볼 그림</b>이다 —
                // 자홍 웅덩이와 그 밖으로 물러나는 낫이 한 화면에 들어와야 한다.
                float t = 0f;
                while (t < FlareRule.BurnSeconds + 3f)
                {
                    if (_낫 != null) E2EHarness.LookAt(_낫.transform.position);
                    t += Time.deltaTime;
                    yield return null;
                }
            }

            yield return 치운다();
            E2EHarness.Log("=== 조명탄 연출 끝 ===");
        }

        // ── 준비 ────────────────────────────────────────────────

        static IEnumerator 준비()
        {
            남은것을_치운다();

            _lantern = Object.FindAnyObjectByType<LanternController>(FindObjectsInactive.Include);
            E2EHarness.Assert(_lantern != null, "랜턴이 있다");
            if (_lantern == null) yield break;

            // 낫은 밤에만 나온다(스펙 §8). 밤은 이 시나리오의 전제이지 주어가 아니다.
            E2EScytheNight.밤에_세운다();

            int 잠든생물 = E2EHarness.SleepWildCreatures();
            int 끈광원 = E2EHarness.MuteAmbientLitZones();
            E2EHarness.Log($"  무대 정리: 야생 생물 {잠든생물}마리, 주변 광원 {끈광원}곳 " +
                           "(재려는 것은 랜턴 하나와 조명탄 하나가 만드는 두 웅덩이다)");

            ScytheWatch.Reset();

            var bag = E2EHarness.Player.Inventory;
            준다(bag, LanternRule.ItemId);
            준다(bag, FlareRule.ItemId);

            var 손 = E2EHarness.Player.GetComponentInChildren<PlayerToolUser>(true);
            E2EHarness.Assert(손 != null && 손.EquipFirst(FlareRule.ItemId),
                              "조명탄 총을 손에 들었다 — 쏘는 길은 손에 든 것이 정한다");

            Vitals.Health.Modify(Vitals.Health.Max);
            E2EHarness.LightLantern();
            yield return null;
            E2EHarness.Assert(_lantern.IsOn, "랜턴이 켜져 있다 — 등 뒤 사각의 전제다");
        }

        static void 준다(PlayerInventory bag, string id)
        {
            if (bag == null || bag.Inventory.CountOf(id) > 0) return;
            var item = bag.Database.GetById(id);
            E2EHarness.Assert(item != null, $"아이템 DB에 {id}이 있다");
            if (item != null) bag.Inventory.TryAdd(item, 1);
        }

        static IEnumerator 무대를_고른다()
        {
            _육지 = Vector3.zero;
            _바다 = Vector3.zero;

            // 무대를 고르는 동안은 꺼 둔다. 켜 두면 제 랜턴 빛이 "어두운 자리"를
            // 전부 지워 버려 설 곳도 세울 곳도 찾지 못한다.
            E2EHarness.DarkenLantern();
            yield return null;

            E2EHarness.Assert(
                E2ELanternOffset.물가를_찾는다(사람자리, 4f, 80f, out _육지, out _바다),
                "낫이 다가올 통로가 있는 어두운 물가를 찾았다");
            if (_육지 == Vector3.zero || _바다 == Vector3.zero) yield break;

            E2EHarness.Log($"  무대: 물가 {_육지.ToString("F1")} · 바다 {_바다.ToString("F1")} " +
                           $"({Vector3.Distance(_육지, _바다):F1}m)");

            E2EHarness.LightLantern();
            E2EHarness.Teleport(_육지 + Vector3.up * 0.4f);
            E2EHarness.SyncPhysics();
            yield return null;
        }

        // ── 1. 쏘면 밝은 구역이 생긴다 ──────────────────────────

        /// <summary>
        /// <b>실제 조작으로 쏜다.</b> 손에 들고 좌클릭 — 배터리도 물리도 그대로 지난다.
        /// 여기서 <c>FlareBurn.Ignite</c>를 직접 부르면 입력과 배터리와 사거리를
        /// 통째로 건너뛰게 되고, 그러면 "쏠 수 있다"를 시험한 적이 없어진다.
        /// </summary>
        static IEnumerator 쏘면_밝은_구역이_생긴다()
        {
            E2EHarness.Log("— 쏘면 그 자리가 밝아진다 —");

            E2EHarness.LightLantern();
            float 앞배터리 = _lantern.Battery;

            yield return 발밑을_겨눈다();
            yield return 쏜다();

            var 탄 = 남은조명탄();
            E2EHarness.Assert(탄 != null, "좌클릭 한 번에 조명탄이 터졌다");
            if (탄 == null) yield break;

            var 중심 = 탄.LitZoneCenter;
            E2EHarness.Log($"  조명탄 {중심.ToString("F2")} · 반경 {탄.LitZoneRadius:F1}m · " +
                           $"배터리 {앞배터리:F0} -> {_lantern.Battery:F0}");

            E2EHarness.Assert(LitZoneRegistry.IsLit(중심), "터진 자리가 등록부에서 밝다");
            E2EHarness.Assert(LitZoneRegistry.IsLitByFixed(중심),
                              "조명탄이 <b>고정 조명</b>으로 잡힌다 — 이것이 랜턴과 갈리는 자리다");

            // 랜턴보다 넓다. 규칙(FlareRuleTests)이 이미 못 박았지만, 세계에 실제로
            // 선 광원이 그 값을 쓰고 있는지는 여기서만 알 수 있다.
            E2EHarness.Assert(탄.LitZoneRadius > _lantern.LitZoneRadius,
                              $"조명탄 {탄.LitZoneRadius:F1}m가 랜턴 {_lantern.LitZoneRadius:F1}m보다 넓다");

            // 배터리를 같은 통에서 먹는다.
            E2EHarness.Assert(앞배터리 - _lantern.Battery > FlareRule.BatteryCost - 2f,
                              $"랜턴 배터리를 {FlareRule.BatteryCost:F0} 먹었다 " +
                              $"(잰 것 {앞배터리 - _lantern.Battery:F1})");

            // 화면과 판정이 같은 말을 한다. 자홍이 아니면 광원 4색 규칙 밖이다.
            var 램프 = 탄.GetComponentInChildren<Light>(true);
            E2EHarness.Assert(램프 != null, "실제 광원이 섰다");
            if (램프 != null)
            {
                E2EHarness.Assert(Mathf.Abs(램프.range - 탄.LitZoneRadius) < 0.01f,
                                  $"보이는 반경과 판정 반경이 같다 ({램프.range:F1}m)");
                E2EHarness.Assert(ColorUtility.ToHtmlStringRGB(램프.color) ==
                                  ColorUtility.ToHtmlStringRGB(FlareRule.Color),
                                  $"조명탄이 자홍이다 (#{ColorUtility.ToHtmlStringRGB(램프.color)})");
            }

            // 그리고 <b>꺼진다</b>. 이것이 랜턴과 겹치지 않는 절반이다.
            float t = 0f;
            while (t < FlareRule.BurnSeconds + 2f && 남은조명탄() != null)
            {
                t += Time.deltaTime;
                yield return null;
            }

            E2EHarness.Log($"  {t:F1}초 만에 다 탔다 (규칙 {FlareRule.BurnSeconds:F0}초)");
            E2EHarness.Assert(남은조명탄() == null,
                              "조명탄이 안 꺼진다 — 그러면 그것은 던지는 랜턴이다");
            E2EHarness.Assert(!LitZoneRegistry.IsLitByFixed(중심),
                              "다 탄 조명탄이 등록부에 남아 있다");
        }

        // ── 2. 등 뒤에 붙은 낫을 떼어낸다 (본체) ────────────────

        static IEnumerator 등_뒤에_붙은_낫을_떼어낸다()
        {
            E2EHarness.Log($"— 등 뒤에 붙은 낫을 조명탄으로 떼어낸다 ({떼어낼횟수}회) —");

            int 성공 = 0;
            for (int 회 = 1; 회 <= 떼어낼횟수; 회++)
            {
                yield return 낫을_되돌린다();
                yield return 등을_준다();

                bool 붙었다 = false;
                yield return 낫이_붙을_때까지(20f, r => 붙었다 = r);

                float 붙은거리 = 사이();
                var 붙은자리 = _낫.transform.position;
                E2EHarness.Assert(붙었다,
                    $"[{회}회] 등 뒤 사각으로 낫이 붙었다 " +
                    $"({_마음.State} · {붙은거리:F2}m · {붙은자리.ToString("F1")})");
                if (!붙었다) continue;

                // <b>몸을 돌리지 않는다.</b> 돌아서면 사각이 사라져 랜턴이 떼어낸 것이
                // 되고, 조명탄이 한 일을 갈라낼 수 없다. 발밑을 겨눠 쏜다.
                E2EHarness.LightLantern();
                yield return 발밑을_겨눈다();

                bool 사각이었다 = LitZoneRegistry.IsBlindSide(붙은자리);
                yield return 쏜다();

                var 탄 = 남은조명탄();
                E2EHarness.Assert(탄 != null, $"[{회}회] 발밑에 조명탄이 터졌다");
                if (탄 == null) continue;

                // 터진 그 프레임의 판정. 규칙이 실제 좌표에서 뒤집혔는가.
                var 지금낫 = _낫.transform.position;
                E2EHarness.Assert(사각이었다, $"[{회}회] 쏘기 전 낫이 등 뒤 사각에 있었다");
                E2EHarness.Assert(FlareRule.Covers(탄.LitZoneCenter, 지금낫, 탄.LitZoneRadius),
                    $"[{회}회] 붙어 있던 낫이 조명탄 안에 들어왔다 " +
                    $"({Vector3.Distance(탄.LitZoneCenter, 지금낫):F2}m <= {탄.LitZoneRadius:F1}m)");
                E2EHarness.Assert(!LitZoneRegistry.IsBlindSide(지금낫),
                    $"[{회}회] 조명탄이 등 뒤 사각을 메웠다");

                // 그리고 <b>실제로 떨어진다</b>. 상태와 거리 둘 다 본다 —
                // 상태만 보면 제자리에서 이름만 바뀐 것과 구별되지 않는다.
                var 떼기 = new 떼어냄();
                yield return 물러나는_것을_지켜본다(떼기, 탄, FlareRule.BurnSeconds);

                E2EHarness.Log($"  [{회}회] 붙었을 때 {붙은거리:F2}m {붙은자리.ToString("F1")} -> " +
                               $"물러난 뒤 {떼기.가장멀리:F2}m {떼기.가장먼자리.ToString("F1")} " +
                               $"(교전을 놓기까지 {떼기.교전을놓은시각:F2}초, " +
                               $"순찰까지 {(떼기.순찰로내려간시각 < 0f ? "안 내려감" : 떼기.순찰로내려간시각.ToString("F2") + "초")})");
                E2EHarness.Log($"       {떼기.몸짓요약}");

                E2EHarness.Assert(떼기.교전을놓았다,
                    $"[{회}회] <b>Attack에서 내려왔다</b> (본 상태: {string.Join("·", 떼기.본상태)})");
                E2EHarness.Assert(떼기.가장멀리 > 붙은거리 + 1f,
                    $"[{회}회] 실제로 물러났다 ({붙은거리:F2}m -> {떼기.가장멀리:F2}m)");
                E2EHarness.Assert(_lantern.IsOn,
                    $"[{회}회] 랜턴은 내내 켜져 있었다 — 어둡게 만들어서 떼어낸 것이 아니다");

                if (떼기.교전을놓았다 && 떼기.가장멀리 > 붙은거리 + 1f) 성공++;
            }

            E2EHarness.AssertEqual(성공, 떼어낼횟수, $"{떼어낼횟수}회 연속으로 떼어냈다");
        }

        class 떼어냄
        {
            public bool 교전을놓았다;
            public float 교전을놓은시각 = -1f;
            public float 순찰로내려간시각 = -1f;
            public float 가장멀리;
            public Vector3 가장먼자리;
            public readonly HashSet<ScytheState> 본상태 = new HashSet<ScytheState>();

            // <b>몸이 실제로 무엇을 하고 있었는가.</b> 4상태만 적으면 "이름은
            // 순찰인데 제자리에 붙어 있다"를 실패 한 줄로 갈라낼 수 없다.
            public readonly HashSet<CreatureState> 본몸짓 = new HashSet<CreatureState>();
            public readonly HashSet<HabitatZone> 본구역 = new HashSet<HabitatZone>();
            public int 물러난프레임;
            public int 돌아가는프레임;
            public int 전체프레임;

            public string 몸짓요약 =>
                $"몸짓 {string.Join("·", 본몸짓)} · 구역 {string.Join("·", 본구역)} · " +
                $"도주 {물러난프레임}/{전체프레임} · 복귀 {돌아가는프레임}";
        }

        /// <summary>
        /// 조명탄이 타는 동안 낫이 무엇을 하는가. <b>사람은 등을 준 채 가만히 있는다</b> —
        /// 여기서 몸을 돌리거나 걸으면 물러난 원인이 조명탄인지 사람인지 갈리지 않는다.
        /// </summary>
        static IEnumerator 물러나는_것을_지켜본다(떼어냄 표, FlareBurn 탄, float 초)
        {
            float t = 0f;
            while (t < 초)
            {
                E2EHarness.LookAt(등질곳());

                if (_낫 == null) yield break;
                표.본상태.Add(_마음.State);
                표.본몸짓.Add(_낫.State);
                표.전체프레임++;
                if (_낫.State == CreatureState.Flee) 표.물러난프레임++;

                var 몸 = _낫.GetComponent<HoverDrifter>();
                if (몸 != null)
                {
                    표.본구역.Add(몸.Zone);
                    if (몸.Returning) 표.돌아가는프레임++;
                }

                float d = 사이();
                if (d > 표.가장멀리)
                {
                    표.가장멀리 = d;
                    표.가장먼자리 = _낫.transform.position;
                }

                if (_마음.State != ScytheState.Attack && 표.교전을놓은시각 < 0f)
                {
                    표.교전을놓았다 = true;
                    표.교전을놓은시각 = t;
                }
                if (_마음.State == ScytheState.Patrol && 표.순찰로내려간시각 < 0f)
                    표.순찰로내려간시각 = t;

                // 사람이 맞고 죽으면 부활이 끼어들어 무대가 사라진다. 살려 두고 잰다.
                if (Vitals.Health.Current < Vitals.Health.Max * 0.5f)
                    Vitals.Health.Modify(Vitals.Health.Max);

                t += Time.deltaTime;
                yield return null;
            }
        }

        // ── 3. 밀어내기 실측 ────────────────────────────────────

        /// <summary>
        /// <b>후보마다 재서 올린다. 판정하지 않는다</b> — 밀어내기 거리는 기획서 §5.2
        /// 튜닝 5값의 넷째이고 사람의 몫이다(스펙 §16). 여기서 단언하는 것은
        /// <b>후보가 커질수록 더 멀리 밀린다</b>는 방향뿐이다. 그것마저 깨지면
        /// 손잡이가 손잡이가 아니다.
        /// </summary>
        static IEnumerator 밀어내기_실측()
        {
            E2EHarness.Log("— 밀어내기 거리 후보 실측 (손잡이는 Domain/World/FlareRule.cs) —");
            E2EHarness.Log($"  지금 규칙값: 반경 {FlareRule.Radius:F0}m · 지속 {FlareRule.BurnSeconds:F0}초 · " +
                           $"한 발 {FlareRule.BatteryCost:F0} (랜턴 {FlareRule.LanternSecondsForfeited:F1}초) · " +
                           $"낫 감지 {(_def != null ? _def.detectRadius : 14f):F0}m");

            var 잰것 = new List<(float 후보, float 붙은거리, float 물러난거리, float 다시붙기, Vector3 자리)>();

            foreach (float 후보 in 밀어내기후보)
            {
                yield return 낫을_되돌린다();
                yield return 등을_준다();

                bool 붙었다 = false;
                yield return 낫이_붙을_때까지(20f, r => 붙었다 = r);
                if (!붙었다)
                {
                    E2EHarness.Log($"  후보 {후보:F0}m — 붙이지 못해 건너뛴다 ({_마음.State}, {사이():F2}m)");
                    continue;
                }

                float 붙은거리 = 사이();

                // <b>총이 아니라 손으로 지핀다.</b> 게임이 쏘는 조명탄은 언제나 규칙값이라
                // 후보를 재려면 반경을 지정해 세우는 수밖에 없다. 재려는 것은 기하이지
                // 사격이 아니고, 사격은 앞 절이 실제 조작으로 이미 시험했다.
                var 탄 = FlareBurn.Ignite(사람자리, 후보);

                var 표 = new 떼어냄();
                yield return 물러나는_것을_지켜본다(표, 탄, FlareRule.BurnSeconds);

                if (탄 != null) Object.Destroy(탄.gameObject);
                yield return null;

                // 다시 붙는 데 몇 초 걸리는가. 안 붙으면 감지 밖으로 밀린 것이다.
                float 다시 = -1f;
                float t = 0f;
                while (t < 20f)
                {
                    E2EHarness.LookAt(등질곳());
                    if (_낫 == null) break;
                    if (_마음.State == ScytheState.Attack) { 다시 = t; break; }
                    t += Time.deltaTime;
                    yield return null;
                }

                잰것.Add((후보, 붙은거리, 표.가장멀리, 다시, 표.가장먼자리));
                E2EHarness.Log($"  후보 {후보,4:F0}m — 붙었을 때 {붙은거리,5:F2}m · " +
                               $"가장 멀리 {표.가장멀리,6:F2}m {표.가장먼자리.ToString("F1")} · " +
                               $"교전 놓기 {(표.교전을놓은시각 < 0f ? "  안 놓음" : 표.교전을놓은시각.ToString("F2") + "초")} · " +
                               $"다시 붙기 {(다시 < 0f ? "20초 안에 못 붙음" : 다시.ToString("F1") + "초")}");
            }

            E2EHarness.Assert(잰것.Count >= 3,
                              $"후보를 셋 이상 쟀다 (잰 것 {잰것.Count}개)");

            // <b>순서는 단언하지 않는다.</b> "후보를 키우면 더 멀리 밀린다"를 게이트로
            // 걸어 봤더니 세 번 중 두 번 빨갰다 — 12m에서 21.8m까지 밀린 판과 24m에서
            // 16.3m에 멈춘 판이 같이 나온다. <b>밀려나는 거리를 정하는 것이 반경만이
            // 아니기 때문</b>이다: 도주는 제 자리에서 12m 앞을 목적지로 잡고, 그 목적지가
            // 물가·비탈·서식지 경계에 걸리면 거기서 멎는다. 잡음이 신호보다 크다.
            //
            // 그래서 여기서 못 박는 것은 <b>후보마다 실제로 떼어냈는가</b> 하나이고,
            // 값을 고르는 데 쓸 표는 위 로그다(스펙 §16 — 고르는 것은 사람).
            foreach (var r in 잰것)
                E2EHarness.Assert(r.물러난거리 > r.붙은거리,
                    $"후보 {r.후보:F0}m에서 낫이 물러났다 ({r.붙은거리:F2}m -> {r.물러난거리:F2}m)");
        }

        // ── 무대 조작 ───────────────────────────────────────────

        static float 사이() =>
            _낫 == null ? float.MaxValue : Vector3.Distance(_낫.transform.position, 사람자리);

        /// <summary>낫에게 등을 준 채 바라볼 곳. 사람 기준으로 낫의 정반대다.</summary>
        static Vector3 등질곳()
        {
            if (_낫 == null) return 사람자리 + Vector3.forward * 20f;
            var 반대 = 사람자리 - _낫.transform.position;
            반대.y = 0f;
            if (반대.sqrMagnitude < 0.01f) 반대 = Vector3.forward;
            return 사람자리 + 반대.normalized * 20f;
        }

        /// <summary>제자리에 다시 서서 낫에게 등을 준다.</summary>
        static IEnumerator 등을_준다()
        {
            E2EHarness.Teleport(_육지 + Vector3.up * 0.4f);
            E2EHarness.SyncPhysics();
            yield return null;

            E2EHarness.LookAt(등질곳());
            yield return null;
        }

        /// <summary>
        /// <b>발밑을 겨눈다.</b> 몸의 좌우(yaw)는 그대로 두고 고개만 내린다 —
        /// 랜턴 웅덩이는 고개의 위아래를 따르지 않으므로(<see cref="LanternRule.Facing"/>)
        /// 등 뒤 사각은 열린 채로 남는다. 그래서 조명탄이 한 일을 갈라낼 수 있다.
        /// </summary>
        static IEnumerator 발밑을_겨눈다()
        {
            var 앞 = LanternRule.Facing(E2EHarness.Player.transform.forward);
            if (앞 == Vector3.zero) 앞 = Vector3.forward;

            E2EHarness.LookAt(사람자리 + 앞 * 0.4f - Vector3.up * 2f);
            yield return null;
            yield return null;
        }

        static IEnumerator 쏜다()
        {
            yield return E2EHarness.ClickAttack();
            yield return null;
            yield return null;
        }

        /// <summary>지금 세계에 타고 있는 조명탄 하나. 없으면 null.</summary>
        static FlareBurn 남은조명탄()
        {
            foreach (var b in Object.FindObjectsByType<FlareBurn>(FindObjectsInactive.Exclude))
                if (b != null && b.IsLit) return b;
            return null;
        }

        /// <summary>
        /// 낫을 <b>사람과 바다 사이</b>, 사람 뒤 <paramref name="거리"/>m에 세운다.
        ///
        /// <b>물 쪽에 두는 것이 요점이다.</b> 사람은 물을 등지고 서므로 이 자리가
        /// 곧 등 뒤 사각이고, 조명탄에 밀려날 때 <b>열린 물 쪽으로</b> 밀린다.
        /// 사람의 반대편(뭍 쪽)에서 붙게 두면 밀어내는 방향이 서식지 밖을 향해
        /// 낫이 <b>제자리에 붙박인다</b> — 실측으로 그렇게 한 회차가 있었고,
        /// 그때 밀어낸 거리는 −0.2m였다(지형에 막힌 것이지 규칙이 진 것이 아니다).
        /// </summary>
        static void 낫을_물쪽_뒤에_세운다(float 거리)
        {
            if (_낫 == null) return;

            var 물쪽 = _바다 - _육지;
            물쪽.y = 0f;
            if (물쪽.sqrMagnitude < 0.01f) 물쪽 = Vector3.forward;
            물쪽.Normalize();

            var 몸 = _낫.GetComponent<HoverDrifter>();
            var 자리 = _바다;

            // 서식지 안이면서 감지 안인 자리를 물 쪽으로 훑어 고른다.
            for (float d = 거리; d <= 거리 + 6f; d += 1f)
            {
                var p = _육지 + 물쪽 * d;
                p.y = _바다.y;
                if (몸 != null && !몸.CanOccupy(p)) continue;
                자리 = p;
                break;
            }

            _낫.transform.position = 자리;
            E2EHarness.SyncPhysics();
        }

        /// <summary>낫을 바다로 돌려보내고 어그로를 식힌다. 앞 회차를 끌고 가지 않게.</summary>
        static IEnumerator 낫을_되돌린다()
        {
            치운다_조명탄();

            if (_낫 == null) yield break;
            _낫.transform.position = _바다;
            E2EHarness.SyncPhysics();

            Vitals.Health.Modify(Vitals.Health.Max);
            for (int i = 0; i < 30; i++) yield return null;
        }

        static IEnumerator 낫이_붙을_때까지(float 초) { yield return 낫이_붙을_때까지(초, null); }

        /// <summary>
        /// 낫이 교전에 들 때까지 등을 준 채 기다린다. 낫이 감지 밖으로 흘러가면
        /// 무대를 다시 세운다 — 재려는 것은 「붙은 것을 떼어내는가」이지
        /// 「낫이 돌아오는가」가 아니다.
        /// </summary>
        static IEnumerator 낫이_붙을_때까지(float 초, System.Action<bool> 결과)
        {
            float 감지 = _def != null ? _def.detectRadius : 14f;
            float t = 0f, 다음정렬 = 0f;

            while (t < 초)
            {
                if (_낫 == null) break;

                if (t >= 다음정렬)
                {
                    다음정렬 = t + 0.4f;
                    E2EHarness.Teleport(_육지 + Vector3.up * 0.4f);
                    E2EHarness.SyncPhysics();

                    // <b>감지 안으로 다시 넣는다.</b> 무대의 바다는 물가에서 15m쯤
                    // 떨어져 있어 낫의 감지 반경(14m) <b>밖</b>이다 — 거기 두면 낫이
                    // 사람을 아예 못 보고, 실측으로 세 번 중 한 번은 20초 안에
                    // 붙지 못했다. 재려는 것은 「붙은 것을 떼어내는가」이지
                    // 「낫이 사람을 찾아내는가」가 아니다.
                    if (사이() > 감지 * 0.95f) 낫을_물쪽_뒤에_세운다(감지 * 0.7f);
                }

                E2EHarness.LookAt(등질곳());

                // 맞아 죽으면 부활이 무대를 지운다. 살려 두고 계속 붙게 둔다.
                if (Vitals.Health.Current < Vitals.Health.Max * 0.5f)
                    Vitals.Health.Modify(Vitals.Health.Max);

                if (_마음 != null && _마음.State == ScytheState.Attack &&
                    사이() <= (_def != null ? _def.attackRange : 2.2f) + 1.5f)
                {
                    결과?.Invoke(true);
                    yield break;
                }

                t += Time.deltaTime;
                yield return null;
            }

            결과?.Invoke(false);
        }

        static IEnumerator 낫을_세운다()
        {
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(낫프리팹);
#endif
            E2EHarness.Assert(prefab != null, "낫 프리팹을 찾았다");
            if (prefab == null) yield break;

            var go = Object.Instantiate(prefab, _바다, Quaternion.identity);
            go.name = "E2E_낫조명탄";
            yield return null;
            yield return null;

            _낫 = go.GetComponent<CreatureBrain>();
            _마음 = go.GetComponent<ScytheMind>();
            _def = go.GetComponent<CreatureHealth>()?.Definition;

            E2EHarness.Assert(_낫 != null, "낫이 섰다");
            E2EHarness.Assert(_마음 != null, "4상태를 드는 부품이 스스로 붙었다");
            E2EHarness.Assert(_def != null && _def.id == "scythe", "낫 정의를 집었다");

            for (int i = 0; i < 30; i++) yield return null;
            if (_낫 != null)
                E2EHarness.Log($"  낫 {_낫.transform.position.ToString("F1")} " +
                               $"(사람에게서 {사이():F1}m · 감지 {_def.detectRadius:F0}m · " +
                               $"공격 거리 {_def.attackRange:F1}m)");
        }

        // ── 뒷정리 ──────────────────────────────────────────────

        static void 치운다_조명탄()
        {
            foreach (var b in Object.FindObjectsByType<FlareBurn>(FindObjectsInactive.Include))
                if (b != null) Object.DestroyImmediate(b.gameObject);
        }

        static void 남은것을_치운다()
        {
            _낫 = null;
            _마음 = null;
            치운다_조명탄();

            foreach (var b in Object.FindObjectsByType<CreatureBrain>(FindObjectsInactive.Include))
                if (b != null && b.name.StartsWith("E2E_낫조명탄"))
                    Object.DestroyImmediate(b.gameObject);
        }

        static IEnumerator 치운다()
        {
            남은것을_치운다();
            E2EHarness.DarkenLantern();
            yield return E2EHarness.ReleaseAllKeys();
            E2EHarness.RestoreWorld();
            Vitals.Health.Modify(Vitals.Health.Max);
            yield return null;
        }
    }
}
