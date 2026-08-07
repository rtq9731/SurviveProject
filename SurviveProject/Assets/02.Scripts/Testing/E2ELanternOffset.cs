using System.Collections;
using UnityEngine;
using Survive.Creatures;
using Survive.Combat;
using Survive.Player;
using Survive.Vitals;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// 랜턴 오프셋 — <b>등 뒤 사각</b> (기획서 §9).
    ///
    /// 직전 라운드에서 랜턴이 상시 점등이 됐다. 그런데 랜턴은 밝은 구역으로 등록되고
    /// 낫은 밝은 구역에 접근하지 않으므로, 그 조합은 플레이어를 <b>걸어다니는 안전지대</b>로
    /// 만들었다. 죽는 길이 배터리 소진 하나로 줄고, 그 순간 <b>조명탄이 존재할 이유도
    /// 사라진다.</b> 빛 웅덩이를 조금 앞으로 밀어 등 뒤를 내주는 것이 그 해법이다.
    ///
    /// 여기서 재는 것은 셋이다.
    /// <list type="number">
    /// <item><b>웅덩이가 정말로 앞으로 치우쳤는가</b> — 판정과 실제 광원이 함께 갔는가.
    ///       둘이 갈라지면 플레이어는 등 뒤가 어둡다는 것을 <b>맞고 나서야</b> 알게 되고,
    ///       그것이 곧 억울함이다</item>
    /// <item><b>등 뒤가 실제로 사각인가</b> — 세워 두고 좌표로 잰다. 정면은 안이고
    ///       등 뒤는 밖이어야 한다. <b>이 항목의 핵심 게이트다</b></item>
    /// <item><b>낫이 그리로 파고들 수 있는가</b> — 같은 자리, 같은 낫, <b>바라보는 쪽만</b>
    ///       바꿔서 가른다. 지도도 개체도 그대로이므로 달라진 것은 방향 하나뿐이다</item>
    /// </list>
    ///
    /// <b>수치는 확정이 아니다.</b> 오프셋은 기획서 §9 튜닝 5값의 둘째이고 사람의 몫이라,
    /// 여기서는 상수를 못 박지 않고 <see cref="LanternRule"/>이 말하는 값과
    /// 세계에서 실제로 잰 값이 <b>서로 맞는지</b>만 본다.
    /// </summary>
    public static class E2ELanternOffset
    {
        const string 낫프리팹 = "Assets/05.Prefabs/Creatures/Creature_낫.prefab";

        static LanternController _lantern;
        static CreatureDefinitionSO _def;
        static CreatureBrain _낫;
        static Vector3 _육지;
        static Vector3 _바다;

        static Vector3 사람자리 => E2EHarness.Player.transform.position;
        static Vector3 사람정면 => LanternRule.Facing(E2EHarness.Player.transform.forward);
        static PlayerVitals Vitals => E2EHarness.Player.Vitals;

        public static IEnumerator FullRun()
        {
            yield return 준비();
            if (_lantern == null) yield break;

            yield return 웅덩이가_앞으로_치우쳤다();
            yield return 등_뒤가_사각이다();
            yield return 실측표();
            yield return 낫이_등_뒤로만_파고든다();

            yield return 치운다();
            E2EHarness.Log("=== 랜턴 오프셋 완주 ===");
        }

        // ── 준비 ────────────────────────────────────────────────

        static IEnumerator 준비()
        {
            남은것을_치운다();

            _lantern = Object.FindAnyObjectByType<LanternController>(FindObjectsInactive.Include);
            E2EHarness.Assert(_lantern != null, "랜턴이 있다");
            if (_lantern == null) yield break;

            int 잠든생물 = E2EHarness.SleepWildCreatures();
            int 끈광원 = E2EHarness.MuteAmbientLitZones();
            E2EHarness.Log($"  무대 정리: 야생 생물 {잠든생물}마리, 주변 광원 {끈광원}곳 " +
                           "(재려는 것은 랜턴 하나가 만드는 웅덩이다)");

            var inv = E2EHarness.Player.Inventory.Inventory;
            if (inv.CountOf(LanternRule.ItemId) == 0)
            {
                var item = E2EHarness.Player.Inventory.Database.GetById(LanternRule.ItemId);
                if (item != null) inv.TryAdd(item, 1);
            }

            Vitals.Health.Modify(Vitals.Health.Max);
            yield return 랜턴(true);
        }

        // ── 1. 웅덩이가 앞으로 치우쳤다 ─────────────────────────

        static IEnumerator 웅덩이가_앞으로_치우쳤다()
        {
            E2EHarness.Log("— 빛 웅덩이가 사람보다 앞에 있다 —");

            int 티어 = _lantern.Tier;
            float 오프셋 = LanternRule.OffsetForTier(티어);
            E2EHarness.Assert(오프셋 > 0f, $"티어 {티어}의 오프셋이 0보다 크다 ({오프셋:F2}m)");

            var 자리 = _lantern.LitAnchor;
            var 중심 = _lantern.LitZoneCenter;
            var 앞 = _lantern.LitForward;

            E2EHarness.Assert(앞 != Vector3.zero, "미는 방향이 정해져 있다");
            E2EHarness.Assert(Mathf.Abs(앞.y) < 1e-4f,
                              $"미는 방향이 수평이다 (y {앞.y:F4}) — 고개를 들어도 웅덩이가 뜨지 않는다");

            var 밀린것 = 중심 - 자리;
            E2EHarness.Assert(Mathf.Abs(밀린것.magnitude - 오프셋) < 0.05f,
                              $"중심이 오프셋만큼 갔다 ({밀린것.magnitude:F2}m / {오프셋:F2}m)");
            E2EHarness.Assert(Vector3.Dot(밀린것.normalized, 앞) > 0.99f,
                              "중심이 바라보는 쪽으로 갔다");

            // 판정만 옮기고 보이는 빛을 두면 화면과 규칙이 다른 말을 하게 된다.
            var 램프 = _lantern.Lamp;
            E2EHarness.Assert(램프 != null, "실제 램프를 찾았다");
            if (램프 != null)
            {
                E2EHarness.Assert(Vector3.Distance(램프.transform.position, 중심) < 0.05f,
                                  $"실제 광원도 판정과 같은 자리에 있다 " +
                                  $"(광원 {램프.transform.position.ToString("F2")}, 판정 {중심.ToString("F2")})");
                E2EHarness.Assert(Mathf.Abs(램프.range - _lantern.LitZoneRadius) < 0.01f,
                                  $"반경도 여전히 같다 ({램프.range:F1}m)");
            }

            // 앞으로 밀었다고 제 발밑이 어두워지면 그것은 다른 게임이다.
            E2EHarness.Assert(LitZoneRegistry.IsLit(사람자리), "서 있는 자리는 여전히 밝다");
            E2EHarness.Assert(!LitZoneRegistry.IsBlindSide(사람자리), "제자리는 사각이 아니다");

            yield return null;
        }

        // ── 2. 등 뒤가 사각이다 (핵심 게이트) ───────────────────

        /// <summary>
        /// <b>세워 두고 좌표로 잰다.</b> 사람은 가만히 있고, 그 앞뒤 좌표가
        /// 밝은 구역 안인지 밖인지만 묻는다. 개체도 물리도 끼지 않으므로
        /// 실패하면 원인이 하나뿐이다.
        /// </summary>
        static IEnumerator 등_뒤가_사각이다()
        {
            E2EHarness.Log("— 세워 두고 앞뒤를 잰다 (핵심 게이트) —");

            int 티어 = _lantern.Tier;
            float 앞끝 = LanternRule.ForwardReachForTier(티어);
            float 뒤끝 = LanternRule.BackReachForTier(티어);

            var 자리 = _lantern.LitAnchor;
            var 앞 = _lantern.LitForward;
            var 뒤 = -앞;
            var 옆 = Vector3.Cross(Vector3.up, 앞);

            E2EHarness.Assert(LitZoneRegistry.IsLit(자리 + 앞 * (앞끝 - 0.3f)),
                              $"정면 {앞끝 - 0.3f:F1}m는 밝다");
            E2EHarness.Assert(!LitZoneRegistry.IsLit(자리 + 앞 * (앞끝 + 0.3f)),
                              $"정면 {앞끝 + 0.3f:F1}m는 어둡다");

            E2EHarness.Assert(LitZoneRegistry.IsLit(자리 + 뒤 * (뒤끝 - 0.3f)),
                              $"등 뒤 {뒤끝 - 0.3f:F1}m는 아직 밝다");
            E2EHarness.Assert(!LitZoneRegistry.IsLit(자리 + 뒤 * (뒤끝 + 0.3f)),
                              $"등 뒤 {뒤끝 + 0.3f:F1}m는 어둡다 — <b>여기가 사각이다</b>");

            // 그리고 그 어두움이 <b>사각으로 읽혀야</b> 한다. 낫이 물어볼 창구가 이쪽이다.
            E2EHarness.Assert(LitZoneRegistry.IsBlindSide(자리 + 뒤 * (뒤끝 + 0.3f)),
                              "등 뒤 어두운 자리가 사각으로 읽힌다");
            E2EHarness.Assert(!LitZoneRegistry.IsBlindSide(자리 + 앞 * (앞끝 + 5f)),
                              "정면은 아무리 멀어도 사각이 아니다");

            // 같은 거리인데 앞은 밝고 뒤는 어둡다 — 이 한 줄이 이 항목의 전부다.
            float 가른다 = (앞끝 + 뒤끝) * 0.5f;
            E2EHarness.Assert(LitZoneRegistry.IsLit(자리 + 앞 * 가른다) &&
                              !LitZoneRegistry.IsLit(자리 + 뒤 * 가른다),
                              $"같은 {가른다:F1}m인데 앞은 밝고 뒤는 어둡다");

            E2EHarness.Assert(!LitZoneRegistry.IsLit(자리 + 옆 * (앞끝 - 0.3f)),
                              "옆은 정면만큼 멀리 가지 못한다");

            yield return null;
        }

        // ── 3. 실측표 ───────────────────────────────────────────

        /// <summary>
        /// 세계를 실제로 훑어 잰 값. <b>수치는 사람의 몫이므로 판정하지 않고 적는다</b> —
        /// 다만 Domain이 말하는 값과 어긋나면 그것은 버그이므로 그것만 단언한다.
        /// </summary>
        static IEnumerator 실측표()
        {
            var 자리 = _lantern.LitAnchor;
            var 앞 = _lantern.LitForward;
            int 티어 = _lantern.Tier;

            float 잰앞 = 훑는다(자리, 앞);
            float 잰뒤 = 훑는다(자리, -앞);
            float 잰옆 = 훑는다(자리, Vector3.Cross(Vector3.up, 앞));

            E2EHarness.Log("— 실측 (손잡이는 Domain/World/LanternRule.cs) —");
            E2EHarness.Log($"  티어 {티어} · 반경 {LanternRule.RadiusForTier(티어):F2}m · " +
                           $"오프셋 {LanternRule.OffsetForTier(티어):F2}m");
            E2EHarness.Log($"  정면 유효 거리   실측 {잰앞:F2}m / 규칙 {LanternRule.ForwardReachForTier(티어):F2}m");
            E2EHarness.Log($"  등 뒤 사각 시작  실측 {잰뒤:F2}m / 규칙 {LanternRule.BackReachForTier(티어):F2}m");
            E2EHarness.Log($"  옆 유효 거리     실측 {잰옆:F2}m / 규칙 {LanternRule.SideReachForTier(티어):F2}m");
            E2EHarness.Log($"  낫의 공격 거리 {(_def != null ? _def.attackRange : 2.2f):F2}m — " +
                           "사각에서 여기까지 좁히는 것이 다음 라운드(이동속도 조건)의 몫이다");

            E2EHarness.Assert(Mathf.Abs(잰앞 - LanternRule.ForwardReachForTier(티어)) < 0.15f,
                              "실측 정면 거리가 규칙과 맞는다");
            E2EHarness.Assert(Mathf.Abs(잰뒤 - LanternRule.BackReachForTier(티어)) < 0.15f,
                              "실측 등 뒤 거리가 규칙과 맞는다");
            E2EHarness.Assert(잰앞 > 잰뒤 + 1f, $"앞이 뒤보다 확실히 멀다 ({잰앞:F2} > {잰뒤:F2})");

            yield return null;
        }

        /// <summary>이 방향으로 몇 미터까지 밝은가. 10cm 눈금으로 실제로 훑는다.</summary>
        static float 훑는다(Vector3 자리, Vector3 방향)
        {
            float 끝 = 0f;
            for (float d = 0f; d <= 40f; d += 0.1f)
            {
                if (!LitZoneRegistry.IsLit(자리 + 방향 * d)) break;
                끝 = d;
            }
            return 끝;
        }

        // ── 4. 낫이 등 뒤로만 파고든다 ──────────────────────────

        /// <summary>
        /// <b>같은 자리, 같은 낫, 바라보는 쪽만 바꾼다.</b> 지도도 개체도 그대로이므로
        /// 두 판의 차이는 방향 하나뿐이고, 그래서 결과의 원인을 오해할 자리가 없다.
        ///
        /// 재는 것은 <b>얼마나 붙었는가</b>와 <b>쫓을 마음을 먹었는가</b> 둘이다.
        /// 거리만 보면 배회하다 우연히 가까워진 것과 구별되지 않는다.
        /// </summary>
        static IEnumerator 낫이_등_뒤로만_파고든다()
        {
            E2EHarness.Log("— 같은 낫, 바라보는 쪽만 바꾼다 —");

            yield return 무대를_고른다();
            if (_바다 == Vector3.zero) yield break;

            yield return 낫을_세운다();
            if (_낫 == null) yield break;

            var 정면판 = new 관찰();
            yield return 지켜본다(정면판, 마주본다: true, 초: 16f);

            // 등 뒤 판은 길게 본다. 재려는 것이 "붙었는가"가 아니라 <b>때렸는가</b>라서,
            // 파고들고 밀려나기를 몇 번은 되풀이할 시간을 줘야 한다.
            var 등판 = new 관찰();
            yield return 지켜본다(등판, 마주본다: false, 초: 40f);

            float 뒤끝 = LanternRule.BackReachForTier(_lantern.Tier);
            float 앞끝 = LanternRule.ForwardReachForTier(_lantern.Tier);

            E2EHarness.Log($"  마주봤을 때: 가장 가까이 {정면판.가장가까이:F2}m, " +
                           $"쫓은 {정면판.쫓은프레임}/{정면판.전체프레임}, " +
                           $"사각 {정면판.사각프레임}, 빛 안 {정면판.빛속프레임}, " +
                           $"물러남 {정면판.물러난프레임}");
            E2EHarness.Log($"  등졌을 때:   가장 가까이 {등판.가장가까이:F2}m, " +
                           $"쫓은 {등판.쫓은프레임}/{등판.전체프레임}, " +
                           $"사각 {등판.사각프레임}, 빛 안 {등판.빛속프레임}, " +
                           $"물러남 {등판.물러난프레임}");

            // 정면 — 빛에 막혀 마음을 먹지 못한다.
            E2EHarness.Assert(정면판.사각프레임 == 0,
                              "마주보는 동안 낫이 사각에 든 적이 없다");
            E2EHarness.Assert(정면판.쫓은프레임 == 0,
                              $"마주보면 쫓을 마음조차 먹지 못한다 ({정면판.쫓은프레임}프레임)");
            E2EHarness.Assert(정면판.가장가까이 > 뒤끝 + 1.5f,
                              $"마주보면 등 뒤만큼 붙지 못한다 ({정면판.가장가까이:F2}m > {뒤끝 + 1.5f:F2}m)");

            // 등 뒤 — 파고든다.
            E2EHarness.Assert(등판.사각프레임 > 0, "등지면 낫이 사각으로 들어온다");
            E2EHarness.Assert(등판.쫓은프레임 > 정면판.쫓은프레임,
                              $"등졌을 때만 쫓을 마음을 먹는다 " +
                              $"({등판.쫓은프레임} > {정면판.쫓은프레임})");
            E2EHarness.Assert(등판.가장가까이 < 정면판.가장가까이 - 1.5f,
                              $"등졌을 때 확실히 더 붙었다 " +
                              $"({등판.가장가까이:F2}m < {정면판.가장가까이:F2}m)");
            E2EHarness.Assert(등판.가장가까이 <= 뒤끝 + 2.5f,
                              $"사각의 깊이만큼 붙었다 ({등판.가장가까이:F2}m <= {뒤끝 + 2.5f:F2}m)");

            // 빛은 여전히 민다. 마주봤을 때 낫이 빛에 닿은 프레임이 있긴 한데
            // (배회하다 11m 안으로 들어온다), 그 프레임이 곧 물러나는 프레임이다 —
            // 닿는 족족 밀려나므로 쫓을 마음까지는 가지 못한다.
            E2EHarness.Assert(정면판.물러난프레임 >= 정면판.빛속프레임 - 5,
                              $"마주봤을 때 낫은 빛에 닿는 족족 물러났다 " +
                              $"(빛 안 {정면판.빛속프레임}, 물러남 {정면판.물러난프레임})");

            // 그리고 등졌을 때는 <b>떼어내지 못한다.</b> 이 둘이 짝이다 —
            // 기획서 §5: "랜턴은 앞쪽만 지키므로 등 뒤를 내주고, 붙어 있는 개체를
            // 떼어내지 못한다." 떼어내는 일은 조명탄의 몫이고, 랜턴이 그것까지 하면
            // 조명탄이 존재할 이유가 사라진다.
            //
            // <b>2026-08-07: 「빛 안 0프레임」이 새 정상이다.</b> 사각이 반평면에서
            // 원으로 바뀌고 오프셋이 6.5m로 오르면서 등 뒤 덮개가 1.5m로 줄었다.
            // 그래서 등진 사람에게 파고드는 낫은 <b>빛에 닿지도 않은 채</b> 사거리
            // 안까지 들어온다 — 예전에는 때리는 시간의 96.4%를 빛 안에 서 있었다.
            //
            // 빛에 한 번도 닿지 않은 것은 "떼어내지 못한다"의 <b>더 센 형태</b>다.
            // 비율로만 재면 분모가 0이라 그 성공이 실패로 읽히므로, 두 경우를 갈라 본다.
            // <b>재는 방식이 2026-08-07에 바뀌었다 — 규칙이 바뀌었기 때문이다.</b>
            // 예전에는 "빛 안에 있으면서도 안 밀린 프레임"의 비율로 쟀다. 사각이
            // 반평면이던 시절에는 낫이 <b>빛 안에 선 채로</b> 때렸고(실측: 때리는
            // 시간의 96.4%가 빛 안), 그래서 그 비율이 뜻을 가졌다.
            //
            // 사각이 원이 된 뒤로는 낫이 빛에 닿으면 <b>제대로 밀리는 것이 맞다</b> —
            // 등 뒤라도 원 안이면 더 이상 내준 쪽이 아니기 때문이다. 실제로 밀린
            // 프레임이 빛 안 프레임과 거의 같게 나온다(실측 112/112, 148/149).
            // 옛 비율로 재면 <b>규칙이 제대로 도는 것이 실패로 읽힌다.</b>
            //
            // 지금 §5가 말하는 "떼어내지 못한다"는 이것이다: 밀어내 봤자
            // <b>사거리 안 어두운 띠</b>(등 뒤 덮개 1.5m ~ 사거리 2.2m)로 밀려날 뿐이고,
            // 낫은 거기서 계속 때린다. 그 타격은 아래 「등 뒤에서 맞는다」 절이
            // 맞은 횟수로 직접 단언한다. 여기서는 <b>밀어내는 것이 붙어 있는 것을
            // 이기지 못한다</b>는 것만 본다.
            E2EHarness.Log($"  등졌을 때 빛 안 {등판.빛속프레임}프레임 · " +
                           $"물러남 {등판.물러난프레임}프레임 · 사각 {등판.사각프레임}프레임");
            E2EHarness.Assert(등판.사각프레임 > 등판.물러난프레임,
                              $"밀리는 것보다 내준 쪽에 있는 시간이 길다 " +
                              $"(사각 {등판.사각프레임} > 물러남 {등판.물러난프레임})");

            // ── 이 항목의 진짜 완료 조건 (스펙 §19 게이트 5) ────────
            // <b>랜턴이 켜진 채로 낫이 사람을 때린다.</b> 이것이 없으면 사각은
            // 화면에만 있고 규칙에는 없다.
            E2EHarness.Assert(정면판.내내켜져있었다 && 등판.내내켜져있었다,
                              "두 판 내내 랜턴이 켜져 있었다 — 빛을 끈 것이 아니다");
            E2EHarness.Assert(정면판.맞은횟수 == 0,
                              $"마주보는 동안에는 한 대도 맞지 않았다 ({정면판.맞은횟수}회)");
            E2EHarness.Assert(등판.맞은횟수 > 0,
                              $"랜턴이 켜진 채로 등 뒤에서 맞았다 ({등판.맞은횟수}회)");

            if (등판.맞은횟수 > 0)
            {
                // ── 방향 신호 (스펙 §19 게이트 6) ──────────────────
                E2EHarness.Log($"  맞았을 때: 방향 {등판.맞았을때방향:F0}도, " +
                               $"비네트 쏠림 {등판.맞았을때비네트쏠림:F3}, " +
                               $"소리 요청 {등판.소리요청}건");

                E2EHarness.Assert(Mathf.Abs(등판.맞았을때방향) > 90f,
                                  $"화면이 '등 뒤에서 왔다'를 안다 ({등판.맞았을때방향:F0}도)");
                E2EHarness.Assert(Mathf.Abs(등판.맞았을때비네트쏠림) > 0.01f,
                                  $"비네트가 돌아설 쪽으로 쏠렸다 ({등판.맞았을때비네트쏠림:F3})");
                E2EHarness.Assert(등판.소리요청 > 0,
                                  $"소리 요청이 실제로 발행됐다 ({등판.소리요청}건, 클립은 사람의 몫)");
            }

            E2EHarness.Log($"  정면 유효 {앞끝:F1}m · 등 뒤 사각 시작 {뒤끝:F1}m · " +
                           $"사각 크기 {LanternRule.BlindSpotDepthForTier(_lantern.Tier):F1}m — " +
                           "같은 개체가 방향 하나로 갈렸다");
        }

        class 관찰
        {
            public float 가장가까이 = float.MaxValue;
            public int 쫓은프레임;
            public int 사각프레임;
            public int 빛속프레임;
            public int 물러난프레임;
            public int 전체프레임;

            public int 맞은횟수;
            public float 맞았을때방향;      // 도. 0이 정면, ±180이 등 뒤
            public float 맞았을때비네트쏠림;
            public int 소리요청;
            public bool 내내켜져있었다 = true;
        }

        static IEnumerator 지켜본다(관찰 표, bool 마주본다, float 초)
        {
            // 앞판이 남긴 어그로·상태를 끌고 가면 두 판의 차이가 방향 하나가 아니게 된다.
            _낫.transform.position = _바다;
            E2EHarness.SyncPhysics();
            for (int i = 0; i < 30; i++) yield return null;

            Vitals.Health.Modify(Vitals.Health.Max);
            float 앞체력 = Vitals.Health.Current;
            int 앞소리 = Survive.Audio.AudioService.Requests;

            float t = 0f, 다음정렬 = 0f;
            while (t < 초)
            {
                if (t >= 다음정렬)
                {
                    다음정렬 = t + 0.4f;
                    yield return 자리를_잡는다(마주본다);
                }

                var p = _낫.transform.position;
                표.전체프레임++;
                표.가장가까이 = Mathf.Min(표.가장가까이, Vector3.Distance(p, 사람자리));
                if (_낫.State == CreatureState.Chase || _낫.State == CreatureState.Attack)
                    표.쫓은프레임++;
                if (_낫.State == CreatureState.Flee) 표.물러난프레임++;
                if (LitZoneRegistry.IsBlindSide(p)) 표.사각프레임++;
                if (LitZoneRegistry.IsLit(p)) 표.빛속프레임++;

                // <b>랜턴이 켜진 채였는지가 이 항목의 전제다.</b> 중간에 배터리가
                // 다해 꺼졌다면 "사각을 만들었다"가 아니라 "빛을 껐다"가 된다.
                if (!_lantern.IsOn) 표.내내켜져있었다 = false;

                if (Vitals.Health.Current < 앞체력 - 0.001f)
                {
                    표.맞은횟수++;
                    표.맞았을때방향 = Survive.Art.PostFxService.LastHurtBearing;
                    표.소리요청 = Survive.Audio.AudioService.Requests - 앞소리;

                    // 화면 값은 <b>한 프레임 뒤에</b> 읽는다. 후처리는 LateUpdate에서
                    // 볼륨에 넣는데 코루틴은 그 앞에서 깨어나므로, 지금 읽으면
                    // 맞기 <b>전</b> 프레임의 값을 보게 된다.
                    yield return null;
                    표.맞았을때비네트쏠림 = Survive.Art.PostFxService.LastLook.VignetteCenterOffset.x;

                    // 죽으면 부활이 끼어들어 무대가 사라진다. 살려 두고 계속 잰다.
                    Vitals.Health.Modify(Vitals.Health.Max);
                    앞체력 = Vitals.Health.Current;
                }

                t += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>
        /// 제자리에 다시 세우고 낫 쪽을 보거나 등진다. 사람이 흘러내리면
        /// 재는 것이 방향이 아니라 지형의 경사가 된다.
        /// </summary>
        static IEnumerator 자리를_잡는다(bool 마주본다)
        {
            E2EHarness.Teleport(_육지 + Vector3.up * 0.4f);
            yield return null;

            var 낫쪽 = _낫.transform.position;
            E2EHarness.LookAt(마주본다 ? 낫쪽 : 사람자리 + (사람자리 - 낫쪽));
            yield return null;
        }

        static IEnumerator 무대를_고른다()
        {
            _육지 = Vector3.zero;
            _바다 = Vector3.zero;

            // 무대를 고르는 동안은 꺼 둔다. 켜 두면 제 랜턴 빛이 "어두운 자리"를
            // 전부 지워 버려 설 곳도 세울 곳도 찾지 못한다.
            yield return 랜턴(false);

            // <b>사람은 물가에 세운다.</b> 처음에는 내륙에 세웠는데, 그러면 낫이
            // 멈춰 서는 자리를 정하는 것이 빛이 아니라 <b>물가까지의 거리</b>가 된다 —
            // 실측으로 등지고 서 있어도 8.31m에서 더 오지 못했고, 그것은 사각의
            // 깊이(5m)가 아니라 육지 가장자리였다. 재려는 것이 빛인데 지형을 쟀다.
            E2EHarness.Assert(물가를_찾는다(사람자리, 4f, 80f, out _육지, out _바다),
                              "낫이 다가올 통로가 있는 어두운 물가를 찾았다");
            if (_육지 == Vector3.zero || _바다 == Vector3.zero) yield break;

            E2EHarness.Log($"  무대: 물가 {_육지.ToString("F1")} · 바다 {_바다.ToString("F1")} " +
                           $"({Vector3.Distance(_육지, _바다):F1}m)");

            yield return 랜턴(true);
            E2EHarness.Teleport(_육지 + Vector3.up * 0.4f);
            yield return null;
            E2EHarness.Assert(LitZoneRegistry.IsLit(사람자리), "선 자리가 랜턴 빛 안이다");
        }

        /// <summary>낫을 세울 거리. 감지 반경(14m) 안이되 정면 유효 거리보다는 멀다 —
        /// 처음부터 빛 안에 있으면 "다가왔다"와 "거기 있었다"가 구별되지 않는다.</summary>
        static float 액면까지 => LanternRule.ForwardReachForTier(1) + 1f;

        /// <summary>
        /// 사람이 설 수 있는 <b>어두운 물가</b>와, 그 사람을 등지고 세울 <b>바다</b>.
        ///
        /// <see cref="E2EScytheHabitat.육지를_찾는다"/>는 일부러 <b>띠보다 확실히 위</b>를
        /// 고른다 — 거기가 "낫이 못 오는 자리"를 재는 무대이기 때문이다. 여기서는
        /// 정반대로 <b>올 수 있는 자리</b>가 필요하다.
        ///
        /// <b>발밑이 띠 안인 것만으로는 모자라다.</b> 낫은 제 서식지 밖으로 발을
        /// 내밀지 않으므로(<see cref="HoverDrifter"/>), 사람이 선 자리가 육지가
        /// 파고든 <b>홈</b>이면 낫은 몇 미터 앞에서 벽을 만난다 — 실측으로 40초 내내
        /// 2.77m에서 더 오지 못했고, 그것은 빛이 아니라 지형이었다. 그래서
        /// <b>사람과 바다 사이에 낫이 지나갈 수 있는 통로가 있는지</b>까지 본다.
        /// </summary>
        static bool 물가를_찾는다(Vector3 중심, float 최소, float 최대,
                                  out Vector3 자리, out Vector3 바다)
        {
            자리 = Vector3.zero;
            바다 = Vector3.zero;
            if (!WaterBody.TryGetSurfaceAt(new Vector3(중심.x, 0f, 중심.z), out float 액면))
                return false;

            float 천장 = 액면 + ScytheHabitat.ShoreRise;

            for (float r = 최소; r <= 최대; r += 2f)
            {
                for (int a = 0; a < 24; a++)
                {
                    var p = 중심 + Quaternion.Euler(0f, a * 15f, 0f) * Vector3.forward * r;
                    if (!UnityEngine.AI.NavMesh.SamplePosition(p, out var hit, 2f,
                                                               UnityEngine.AI.NavMesh.AllAreas))
                        continue;

                    // 물에 잠기면 헤엄이 되고, 띠를 넘으면 낫이 못 온다.
                    if (hit.position.y < 액면 || hit.position.y > 천장) continue;
                    if (LitZoneRegistry.IsLit(hit.position)) continue;

                    // 등질 바다가 실제로 있어야 무대가 성립한다.
                    if (!E2EScytheHabitat.서식지를_찾는다(hit.position, 액면까지, 액면까지 + 6f,
                                                          true, out var 후보바다)) continue;

                    // 그리고 그 바다에서 사람에게 오는 길이 뚫려 있어야 한다.
                    if (!통로가_뚫렸는가(hit.position, 후보바다)) continue;

                    자리 = hit.position;
                    바다 = 후보바다;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 사람과 바다 사이 첫 몇 미터가 낫의 서식지인가. <b>사거리 안까지 들어올
        /// 자리가 있어야</b> "랜턴이 켜진 채로 맞는다"를 잴 수 있다.
        /// </summary>
        static bool 통로가_뚫렸는가(Vector3 사람, Vector3 바다쪽)
        {
            var d = 바다쪽 - 사람;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return false;
            d.Normalize();

            for (float m = 1.5f; m <= 4f; m += 0.5f)
                if (구역(사람 + d * m) == HabitatZone.Inland) return false;

            return true;
        }

        /// <summary>그 자리가 낫에게 무엇인가. 낫이 보는 것과 같은 것을 본다.</summary>
        static HabitatZone 구역(Vector3 p)
        {
            if (!WaterBody.TryGetSurfaceAt(new Vector3(p.x, 0f, p.z), out float 수면))
                return HabitatZone.Inland;

            bool 지형 = Physics.Raycast(
                new Vector3(p.x, 수면 + ScytheHabitat.ShoreRise + 1f, p.z), Vector3.down,
                out var hit, 60f, ~0, QueryTriggerInteraction.Ignore);

            return ScytheHabitat.Classify(true, 수면, 지형, 지형 ? hit.point.y : 0f);
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
            go.name = "E2E_낫사각";
            yield return null;
            yield return null;

            _낫 = go.GetComponent<CreatureBrain>();
            _def = go.GetComponent<CreatureHealth>()?.Definition;
            E2EHarness.Assert(_낫 != null, "낫이 섰다");

            for (int i = 0; i < 30; i++) yield return null;
            if (_낫 != null)
                E2EHarness.Log($"  낫 {_낫.transform.position.ToString("F1")} " +
                               $"(사람에게서 {Vector3.Distance(_낫.transform.position, 사람자리):F1}m)");
        }

        // ── 뒷정리 ──────────────────────────────────────────────

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

        static void 남은것을_치운다()
        {
            _낫 = null;
            foreach (var b in Object.FindObjectsByType<CreatureBrain>(FindObjectsInactive.Include))
                if (b != null && b.name.StartsWith("E2E_낫사각"))
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
    }
}
