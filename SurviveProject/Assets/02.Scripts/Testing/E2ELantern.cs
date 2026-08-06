using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Survive.Items;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// 챕터 1 재건 스펙 §12 - 랜턴은 상시 점등이고 반경은 작다.
    ///
    /// EditMode(<c>LanternRuleTests</c>)가 규칙과 "끄는 배선이 없다"를 지킨다.
    /// 규칙이 옳아도 실제 게임이 그 규칙을 지나지 않으면 랜턴은 그대로 꺼져 있고,
    /// 반경도 그대로 26m다. 그래서 여기서는 <b>진짜 씬에서 진짜 램프를</b> 본다.
    ///
    /// 보는 것은 다섯이다.
    /// 1. 랜턴이 없으면 어둡고, 지니는 순간 <b>아무것도 누르지 않았는데</b> 켜진다.
    /// 2. <b>끌 수 없다.</b> 옛 토글 키(F)도, 도구를 비우는 Q도 불을 끄지 못한다.
    /// 3. 반경 밖은 캄캄하다 - 판정 반경과 <b>실제 Light.range</b>가 같은 값이다.
    /// 4. 배터리가 실제로 준다. 초당 소모가 규칙과 맞는다.
    /// 5. 티어를 올리면 반경이 넓어지고, 배터리가 다하면 꺼진다.
    ///
    /// 수치는 확정이 아니다(기획서 §9 튜닝 3값). 그래서 단언은 전부
    /// <see cref="LanternRule"/>을 참조하고, 실측값은 마지막에 표로 남긴다.
    /// </summary>
    public static class E2ELantern
    {
        static PlayerInventory PI => E2EHarness.Player.Inventory;
        static Inventory Inv => PI.Inventory;

        static LanternController _lantern;
        static Light _lamp;
        static ToolItemSO _티어2;

        public static IEnumerator FullRun()
        {
            yield return 무대를_비운다();
            yield return 랜턴이_없으면_어둡다();
            yield return 지니면_저절로_켜진다();
            yield return 끌_수_없다();
            yield return 반경_밖은_캄캄하다();
            yield return 배터리가_준다();
            yield return 티어를_올리면_반경이_넓어진다();
            yield return 배터리가_다하면_꺼진다();
            yield return 실측표();
            yield return 되돌린다();

            E2EHarness.Log("=== 랜턴 상시 점등 완주 ===");
        }

        // ── 0. 무대 비우기 ───────────────────────────────────────

        /// <summary>
        /// 재려는 것은 <b>랜턴이 만드는 밝은 구역</b>뿐이다. 씬에는 반경 11m짜리
        /// 발광 버섯 군락이 시작 지점 둘레를 거의 덮고 있어서, 그대로 두면
        /// "반경 밖이 캄캄하다"를 어디서도 확인할 수 없다.
        /// </summary>
        static IEnumerator 무대를_비운다()
        {
            _lantern = E2EHarness.Lantern;
            E2EHarness.Assert(_lantern != null, "랜턴 장치가 씬에 있다");

            _lamp = 램프();
            E2EHarness.Assert(_lamp != null, "실제 Light 컴포넌트를 찾았다");

            int 끈광원 = E2EHarness.MuteAmbientLitZones();
            E2EHarness.Log($"  무대 정리: 주변 광원 {끈광원}곳을 밝은 구역에서 뺐다");

            // 앞 시나리오가 남긴 랜턴이 있으면 "없을 때"를 잴 수 없다.
            for (int 회 = 0; 회 < 4 && Inv.CountOf(LanternRule.ItemId) > 0; 회++)
                Inv.TryRemove(LanternRule.ItemId, Inv.CountOf(LanternRule.ItemId));
            yield return null;
        }

        /// <summary>램프는 LanternController의 자식이 아니다(백로그 41에서 몸 바깥으로 옮겼다).</summary>
        static Light 램프()
        {
            if (_lantern == null) return null;
            foreach (var t in _lantern.transform.root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "LampLight") continue;
                var l = t.GetComponent<Light>();
                if (l != null) return l;
            }
            return _lantern.GetComponentInChildren<Light>(true);
        }

        // ── 1. 없으면 어둡다 ─────────────────────────────────────

        static IEnumerator 랜턴이_없으면_어둡다()
        {
            E2EHarness.Log("— 랜턴이 없으면 어둡다 —");
            yield return null;

            E2EHarness.AssertEqual(Inv.CountOf(LanternRule.ItemId), 0, "지닌 랜턴 수");
            E2EHarness.AssertEqual(_lantern.Tier, 0, "랜턴 티어");
            E2EHarness.Assert(!_lantern.IsOn, "제작 전에는 어둠을 그대로 견딘다");
            E2EHarness.Assert(!_lamp.enabled, "실제 램프도 꺼져 있다");

            // 만들기도 전에 시계가 도는 것은 압박이 아니라 버그다.
            float before = _lantern.Battery;
            for (int i = 0; i < 30; i++) yield return null;
            E2EHarness.AssertEqual(_lantern.Battery, before, "랜턴이 없는 동안의 배터리");
        }

        // ── 2. 지니면 저절로 켜진다 ──────────────────────────────

        static IEnumerator 지니면_저절로_켜진다()
        {
            E2EHarness.Log("— 지니면 저절로 켜진다 —");

            var 정의 = PI.Database != null ? PI.Database.GetById(LanternRule.ItemId) : null;
            E2EHarness.Assert(정의 != null, "랜턴 아이템 정의를 찾았다");
            Inv.TryAdd(정의, 1);
            yield return null;

            _lantern.Recharge(LanternRule.MaxBattery);

            yield return E2EHarness.WaitUntil(() => _lantern.IsOn,
                                              "누른 것 없이 불이 들어왔다", 3f);
            E2EHarness.AssertEqual(_lantern.Tier, 1, "랜턴 티어");
            E2EHarness.Assert(_lamp.enabled, "실제 램프에도 불이 들어왔다");
        }

        // ── 3. 끌 수 없다 ────────────────────────────────────────

        /// <summary>
        /// <b>이것이 이 시나리오의 핵심이다.</b> 규칙에서 스위치를 지워도 어딘가에
        /// 배선이 한 가닥 남아 있으면 랜턴은 그대로 꺼진다. 실제로 있었던 두 길을
        /// 눌러 본다 - 옛 토글 키 F, 그리고 도구 목록을 비울 때 함께 불을 껐던 Q.
        /// </summary>
        static IEnumerator 끌_수_없다()
        {
            E2EHarness.Log("— 끌 수 없다 —");

            for (int i = 0; i < 3; i++)
            {
                yield return E2EHarness.TapKey(Key.F);
                yield return null;
                E2EHarness.Assert(_lantern.IsOn, $"F를 {i + 1}번 눌러도 켜져 있다");
            }

            for (int i = 0; i < 3; i++)
            {
                yield return E2EHarness.TapKey(Key.Q);
                yield return null;
            }
            E2EHarness.Assert(_lantern.IsOn, "Q로 도구를 돌려도 켜져 있다");
            E2EHarness.Assert(_lamp.enabled, "실제 램프도 그대로다");
        }

        // ── 4. 반경 밖은 캄캄하다 ────────────────────────────────

        static IEnumerator 반경_밖은_캄캄하다()
        {
            E2EHarness.Log("— 반경 밖은 캄캄하다 —");

            _lantern.Recharge(LanternRule.MaxBattery);
            yield return null;

            float r = _lantern.LitZoneRadius;
            E2EHarness.AssertEqual(r, LanternRule.RadiusForTier(1), "판정 반경(m)");

            // 판정 반경과 눈에 보이는 빛이 따로 놀면 플레이어가 판단할 수 없다.
            E2EHarness.Assert(Mathf.Abs(_lamp.range - r) < 0.01f,
                              $"실제 Light.range가 판정 반경과 같다 ({_lamp.range:F1}m)");

            Vector3 중심 = _lantern.LitZoneCenter;
            E2EHarness.Assert(LitZoneRegistry.IsLit(중심), "램프 자리는 밝다");
            E2EHarness.Assert(LitZoneRegistry.IsLit(중심 + Vector3.forward * (r * 0.9f)),
                              $"반경 안({r * 0.9f:F1}m)은 밝다");

            foreach (var 방향 in new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left })
                E2EHarness.Assert(!LitZoneRegistry.IsLit(중심 + 방향 * (r + 1f)),
                                  $"반경 밖({r + 1f:F1}m, {방향}) 은 캄캄하다");

            // 반경이 관대하면 어둠은 배터리가 다했을 때만 나타나는 처벌이 된다.
            E2EHarness.Assert(r < 12f, $"반경이 작다 ({r:F1}m)");
        }

        // ── 5. 배터리가 준다 ─────────────────────────────────────

        static IEnumerator 배터리가_준다()
        {
            E2EHarness.Log("— 배터리가 준다 —");

            _lantern.Recharge(LanternRule.MaxBattery);
            yield return null;

            float 시작 = _lantern.Battery;
            float t0 = Time.time;

            float 잰시간 = 0f;
            while (잰시간 < 2f) { 잰시간 = Time.time - t0; yield return null; }

            float 준양 = 시작 - _lantern.Battery;
            float 기대 = LanternRule.DrainForTier(1) * 잰시간;

            E2EHarness.Assert(준양 > 0f, $"{잰시간:F1}초 동안 배터리가 {준양:F2} 줄었다");
            E2EHarness.Assert(Mathf.Abs(준양 - 기대) < 기대 * 0.25f + 0.5f,
                              $"규칙이 말하는 만큼 줄었다 (실측 {준양:F2} / 기대 {기대:F2})");
        }

        // ── 6. 티어를 올리면 반경이 넓어진다 ─────────────────────

        /// <summary>
        /// 스위치에서 뺏은 선택을 돌려준 자리다. 상위 티어 에셋은 아직 없으므로
        /// (배치·데이터는 사람과 함께) 여기서 만들어 걸어 본다 - 재는 것은
        /// "티어가 실제 램프까지 도달하는가"이지 에셋의 존재가 아니다.
        /// </summary>
        static IEnumerator 티어를_올리면_반경이_넓어진다()
        {
            E2EHarness.Log("— 티어를 올리면 반경이 넓어진다 —");

            float 티어1반경 = _lantern.LitZoneRadius;

            _티어2 = ScriptableObject.CreateInstance<ToolItemSO>();
            _티어2.id = "lantern_t2_e2e";
            _티어2.displayName = _티어2.id;
            _티어2.maxStack = 1;
            _티어2.category = ItemCategory.Tool;
            _티어2.equipSlot = EquipmentSlotKind.Light;
            _티어2.tier = 2;

            Inv.Equipment.Unequip(EquipmentSlotKind.Light);
            Inv.TryRemove(LanternRule.ItemId, Inv.CountOf(LanternRule.ItemId));
            Inv.TryAdd(_티어2, 1);
            _lantern.Recharge(LanternRule.MaxBattery);

            yield return E2EHarness.WaitUntil(() => _lantern.Tier == 2, "티어 2 랜턴이 걸렸다", 3f);
            yield return null;

            float 티어2반경 = _lantern.LitZoneRadius;
            E2EHarness.AssertEqual(티어2반경, LanternRule.RadiusForTier(2), "티어 2 판정 반경(m)");
            E2EHarness.Assert(티어2반경 > 티어1반경,
                              $"반경이 넓어졌다 ({티어1반경:F1}m → {티어2반경:F1}m)");
            E2EHarness.Assert(Mathf.Abs(_lamp.range - 티어2반경) < 0.01f,
                              $"실제 Light.range도 따라왔다 ({_lamp.range:F1}m)");

            // 넓게 보면 더 낸다. 그래야 티어가 판단이 된다.
            E2EHarness.Assert(LanternRule.DrainForTier(2) > LanternRule.DrainForTier(1),
                              "티어 2는 초당 더 먹는다");

            // 티어 1로 되돌린다. 뒤의 실측표는 티어 1 기준이다.
            Inv.Equipment.Unequip(EquipmentSlotKind.Light);
            Inv.TryRemove(_티어2.id, Inv.CountOf(_티어2.id));
            var 정의 = PI.Database.GetById(LanternRule.ItemId);
            if (정의 != null) Inv.TryAdd(정의, 1);
            yield return E2EHarness.WaitUntil(() => _lantern.Tier == 1, "티어 1로 되돌렸다", 3f);
        }

        // ── 7. 배터리가 다하면 꺼진다 ────────────────────────────

        /// <summary>
        /// 이것이 이 게임의 유일한 "꺼짐"이다. 플레이어가 고른 것이 아니라
        /// 비용을 다 쓴 결과다. 그리고 그 순간 자리가 <b>실제로 어두워져야</b> 한다.
        /// </summary>
        static IEnumerator 배터리가_다하면_꺼진다()
        {
            E2EHarness.Log("— 배터리가 다하면 꺼진다 —");

            E2EHarness.DarkenLantern();
            yield return null;

            E2EHarness.AssertEqual(_lantern.Battery, 0f, "남은 배터리");
            E2EHarness.Assert(!_lantern.IsOn, "불이 꺼졌다");
            E2EHarness.Assert(!_lamp.enabled, "실제 램프도 꺼졌다");
            E2EHarness.Assert(!LitZoneRegistry.IsLit(_lantern.LitZoneCenter),
                              "서 있던 자리가 캄캄해졌다");
            E2EHarness.Assert(_lantern.HasLantern, "랜턴은 여전히 지니고 있다 (눈금은 보여야 한다)");

            // 되살리는 길은 있다. 셀·화톳불·군락 셋 다 Recharge를 지난다.
            _lantern.Recharge(LanternRule.BatteryPerCell);
            yield return null;
            E2EHarness.Assert(_lantern.IsOn, "채우면 곧바로 다시 켜진다");
        }

        // ── 8. 실측표 ────────────────────────────────────────────

        static IEnumerator 실측표()
        {
            _lantern.Recharge(LanternRule.MaxBattery);
            yield return null;

            E2EHarness.Log("— 실측 (수치는 사람의 몫, 손잡이는 Domain/World/LanternRule.cs) —");
            for (int t = 1; t <= LanternRule.MaxTier; t++)
                E2EHarness.Log($"  티어 {t}: 반경 {LanternRule.RadiusForTier(t):F1}m, " +
                               $"초당 {LanternRule.DrainForTier(t):F2}, " +
                               $"가득 한 번에 {LanternRule.SecondsOfLight(LanternRule.MaxBattery, t):F1}초");
            E2EHarness.Log($"  배터리 최대 {LanternRule.MaxBattery:F0}, " +
                           $"셀 하나 {LanternRule.BatteryPerCell:F0} (= 가득 한 번)");
            E2EHarness.Log($"  지금 걸린 것: 티어 {_lantern.Tier}, " +
                           $"판정 반경 {_lantern.LitZoneRadius:F1}m, 램프 range {_lamp.range:F1}m, " +
                           $"배터리 {_lantern.Battery:F1}");
        }

        // ── 9. 되돌린다 ──────────────────────────────────────────

        static IEnumerator 되돌린다()
        {
            if (_티어2 != null)
            {
                Inv.TryRemove(_티어2.id, Inv.CountOf(_티어2.id));
                Object.Destroy(_티어2);
                _티어2 = null;
            }

            _lantern.Recharge(LanternRule.MaxBattery);
            E2EHarness.RestoreWorld();
            yield return null;
        }
    }
}
