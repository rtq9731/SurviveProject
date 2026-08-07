using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Survive.Domain.Art;
using Survive.Items;
using Survive.Player;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// 검토회신 2026-08-07 ②④ — <b>물속에서도 지상과 같은 규칙이다.</b>
    /// 켜면 보이지만 태우고, 끄면 아끼지만 못 본다.
    ///
    /// EditMode(<c>LanternSwitchTests</c>·<c>UnderwaterFogTests</c>)가 규칙을
    /// 지킨다. 규칙이 옳아도 물속에서 실제로 그렇게 되는지는 다른 이야기다 —
    /// 물속 안개는 <see cref="UnderwaterView"/>가 <see cref="RenderSettings"/>에
    /// 직접 쓰고, 그 위를 <c>DepthFogService</c>가 매 프레임 덮으려 든다. 둘이
    /// 어긋나면 규칙은 짙은 안개를 말하는데 화면은 옛 안개를 그린다.
    ///
    /// 보는 것은 넷이다.
    /// 1. 머리가 잠기면 <b>규칙이 정한</b> 안개가 실제로 깔린다.
    /// 2. 물속 먼 곳이 수면 위보다 <b>어둡다</b>.
    /// 3. 물속에서 F로 껐다 켜는 <b>왕복</b>이 된다 — 끄면 밝은 구역이 사라지고
    ///    배터리가 멈추고, 켜면 그대로 돌아온다.
    /// 4. 나오면 안개가 뭍의 것으로 되돌아온다.
    ///
    /// <b>몸은 붙들어야 한다.</b> 물속에 놓고 손을 떼면 떠올라서, 물속을 잰다고
    /// 믿은 값이 뭍의 값이 된다. 매 프레임 <see cref="DiveLightProbe.PinUnderwater"/>를
    /// 부른다.
    /// </summary>
    public static class E2EDiveLantern
    {
        static PlayerInventory PI => E2EHarness.Player.Inventory;
        static Inventory Inv => PI.Inventory;

        static LanternController _lantern;
        static Light _lamp;
        static PlayerSwimming _swim;

        static float _landDensity;
        static Color _landColor;

        public static IEnumerator FullRun()
        {
            yield return 무대를_비운다();
            yield return 물에_잠긴다();
            yield return 규칙이_정한_안개가_깔린다();
            yield return 물속이_수면_위보다_어둡다();
            yield return 물속에서_껐다_켠다();
            yield return 물_밖으로_나온다();
            yield return 실측표();
            yield return 되돌린다();

            E2EHarness.Log("=== 물속 랜턴 왕복 완주 ===");
        }

        // ── 0. 무대 비우기 ───────────────────────────────────────

        static IEnumerator 무대를_비운다()
        {
            DiveLightProbe.Reset();

            _lantern = E2EHarness.Lantern;
            E2EHarness.Assert(_lantern != null, "랜턴 장치가 씬에 있다");

            _swim = Object.FindAnyObjectByType<PlayerSwimming>(FindObjectsInactive.Exclude);
            E2EHarness.Assert(_swim != null, "PlayerSwimming이 있다");

            _lamp = 램프();
            E2EHarness.Assert(_lamp != null, "실제 Light 컴포넌트를 찾았다");

            // 발광 버섯 군락이 시작 지점을 덮고 있으면 "껐더니 어두워졌다"를
            // 어디서도 확인할 수 없다.
            int 끈광원 = E2EHarness.MuteAmbientLitZones();
            E2EHarness.Log($"  무대 정리: 주변 광원 {끈광원}곳을 밝은 구역에서 뺐다");

            if (Inv.CountOf(LanternRule.ItemId) == 0)
            {
                var 정의 = PI.Database != null ? PI.Database.GetById(LanternRule.ItemId) : null;
                E2EHarness.Assert(정의 != null, "랜턴 아이템 정의를 찾았다");
                Inv.TryAdd(정의, 1);
            }
            _lantern.Recharge(LanternRule.MaxBattery);
            yield return null;

            _landDensity = RenderSettings.fogDensity;
            _landColor = RenderSettings.fogColor;
            E2EHarness.Log($"  뭍 안개: 밀도 {_landDensity:F4}, 색 {_landColor}");

            E2EHarness.Assert(_lantern.IsOn, "출발선: 랜턴이 켜져 있다");
        }

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

        // ── 1. 물에 잠긴다 ───────────────────────────────────────

        static IEnumerator 물에_잠긴다()
        {
            E2EHarness.Log("— 물에 잠긴다 —");

            string 자리 = DiveLightProbe.Submerge();
            E2EHarness.Assert(!자리.Contains("error"), "잠길 만큼 깊은 자리를 찾았다: " + 자리);
            E2EHarness.Log("  " + 자리);

            yield return 붙든채_기다린다(() => _swim.IsHeadSubmerged, "머리가 물에 잠겼다", 5f);

            // 안개는 0.35초에 걸쳐 차오른다(UnderwaterView.blendSeconds).
            // <b>프레임 수로 기다리지 않는다</b> — 에디터가 빠르면 40프레임이
            // 0.3초라 다 차기 전에 재고, 실측 0.0905 대 규칙 0.0975로 깨진다.
            yield return 붙든채_기다린다(
                () => Mathf.Abs(RenderSettings.fogDensity - UnderwaterFog.Density) < 0.002f,
                "물속 안개가 다 찼다", 5f);
        }

        /// <summary>물속에 붙들어 둔 채로 조건을 기다린다.</summary>
        static IEnumerator 붙든채_기다린다(System.Func<bool> 조건, string 무엇, float 제한초)
        {
            float t0 = Time.time;
            while (!조건())
            {
                DiveLightProbe.PinUnderwater();
                if (Time.time - t0 > 제한초)
                    throw new System.TimeoutException($"{제한초}초 안에 이루어지지 않았다: {무엇}");
                yield return null;
            }
            E2EHarness.Assert(true, 무엇);
        }

        static IEnumerator 붙든채_프레임(int 수)
        {
            for (int i = 0; i < 수; i++)
            {
                DiveLightProbe.PinUnderwater();
                yield return null;
            }
        }

        // ── 2. 규칙이 정한 안개가 깔린다 ─────────────────────────

        static IEnumerator 규칙이_정한_안개가_깔린다()
        {
            E2EHarness.Log("— 규칙이 정한 안개가 깔린다 —");
            yield return 붙든채_프레임(2);

            E2EHarness.Assert(RenderSettings.fogMode == FogMode.ExponentialSquared,
                              "물속 안개는 ExponentialSquared다");

            E2EHarness.Assert(Mathf.Abs(RenderSettings.fogDensity - UnderwaterFog.Density) < 0.002f,
                              $"밀도가 규칙과 같다 (실측 {RenderSettings.fogDensity:F4} / " +
                              $"규칙 {UnderwaterFog.Density:F4})");

            var 색 = RenderSettings.fogColor;
            var 규칙색 = UnderwaterFog.Color;
            E2EHarness.Assert(Mathf.Abs(색.r - 규칙색.r) < 0.01f &&
                              Mathf.Abs(색.g - 규칙색.g) < 0.01f &&
                              Mathf.Abs(색.b - 규칙색.b) < 0.01f,
                              $"색이 규칙과 같다 (실측 {색} / 규칙 {규칙색})");

            E2EHarness.Assert(RenderSettings.fogDensity > _landDensity,
                              "물속이 뭍보다 짙다 — 그래야 반경 밖이 닫힌다");
        }

        // ── 3. 물속이 수면 위보다 어둡다 ─────────────────────────

        /// <summary>
        /// 깊이 들어갈수록 밝아지면 어둠 축이 정확히 뒤집힌다. 고치기 전에는
        /// 물속 안개색이 <c>FogCliffs</c>(휘도 0.105)라 수면 위 밴드(0.058)보다
        /// <b>밝았다</b>.
        /// </summary>
        static IEnumerator 물속이_수면_위보다_어둡다()
        {
            E2EHarness.Log("— 물속이 수면 위보다 어둡다 —");
            yield return 붙든채_프레임(2);

            float 물속 = UnderwaterFog.Luminance(RenderSettings.fogColor);
            float 뭍 = UnderwaterFog.Luminance(_landColor);
            E2EHarness.Assert(물속 < 뭍,
                              $"물속 먼 곳이 더 어둡다 (물속 {물속:F4} < 뭍 {뭍:F4})");

            // 반경 밖이 실제로 닫히는가. 랜턴 도달 거리의 두 배에서 1% 아래여야 한다.
            float 닫힘 = UnderwaterFog.Transmittance(UnderwaterFog.CloseDistance);
            E2EHarness.Assert(닫힘 < 0.02f,
                              $"{UnderwaterFog.CloseDistance:F1}m에서 세계가 닫힌다 (투과율 {닫힘:F4})");

            // 그런데 반경 안은 살아 있어야 한다. 둘 다여야 "반경 안만 보인다"다.
            float 반경끝 = UnderwaterFog.Transmittance(LanternRule.Tier1Radius);
            E2EHarness.Assert(반경끝 > UnderwaterFog.VisibleTransmittanceFloor,
                              $"반경 {LanternRule.Tier1Radius:F0}m는 아직 보인다 (투과율 {반경끝:F3})");
        }

        // ── 4. 물속에서 껐다 켠다 ────────────────────────────────

        /// <summary>
        /// <b>이것이 이 시나리오의 핵심이다.</b> 물속에서도 지상과 같은 규칙이라는
        /// 것은 곧 <b>같은 키가 같은 일을 한다</b>는 뜻이다. 왕복을 세 번 돈다 —
        /// 한 번은 우연히 맞을 수 있다.
        /// </summary>
        static IEnumerator 물속에서_껐다_켠다()
        {
            E2EHarness.Log("— 물속에서 F로 껐다 켠다 —");

            for (int 회 = 1; 회 <= 3; 회++)
            {
                DiveLightProbe.PinUnderwater();
                yield return E2EHarness.TapKey(Key.F);
                yield return 붙든채_기다린다(() => !_lantern.IsOn, $"{회}회차: 물속에서 껐다", 3f);

                E2EHarness.Assert(!_lamp.enabled, $"{회}회차: 실제 램프도 꺼졌다");
                E2EHarness.Assert(!LitZoneRegistry.IsLit(_lantern.LitZoneCenter),
                                  $"{회}회차: 밝은 구역이 사라졌다");
                E2EHarness.Assert(_lantern.HasLantern, $"{회}회차: 눈금은 남는다");
                E2EHarness.Assert(_swim.IsHeadSubmerged, $"{회}회차: 아직 물속이다");

                float 끈직후 = _lantern.Battery;
                yield return 붙든채_프레임(30);
                E2EHarness.AssertEqual(_lantern.Battery, 끈직후, $"{회}회차: 꺼진 동안의 배터리");

                DiveLightProbe.PinUnderwater();
                yield return E2EHarness.TapKey(Key.F);
                yield return 붙든채_기다린다(() => _lantern.IsOn, $"{회}회차: 물속에서 다시 켰다", 3f);

                E2EHarness.Assert(_lamp.enabled, $"{회}회차: 실제 램프도 돌아왔다");
                E2EHarness.Assert(LitZoneRegistry.IsLit(_lantern.LitZoneCenter),
                                  $"{회}회차: 밝은 구역이 돌아왔다");
                E2EHarness.Assert(Mathf.Abs(_lamp.range - LanternRule.RadiusForTier(_lantern.Tier)) < 0.01f,
                                  $"{회}회차: 반경도 그대로다 ({_lamp.range:F1}m)");

                // 켜 두면 다시 태운다. 그것이 거래의 다른 쪽이다.
                float 켠직후 = _lantern.Battery;
                yield return 붙든채_프레임(20);
                E2EHarness.Assert(_lantern.Battery < 켠직후,
                                  $"{회}회차: 켜 두면 다시 준다 ({켠직후:F1} → {_lantern.Battery:F1})");
                _lantern.Recharge(LanternRule.MaxBattery);
            }
        }

        // ── 5. 물 밖으로 나온다 ──────────────────────────────────

        static IEnumerator 물_밖으로_나온다()
        {
            E2EHarness.Log("— 물 밖으로 나온다 —");

            DiveLightProbe.Surface();
            float t0 = Time.time;
            while (_swim.IsHeadSubmerged && Time.time - t0 < 5f)
            {
                DiveLightProbe.Surface();
                yield return null;
            }
            E2EHarness.Assert(!_swim.IsHeadSubmerged, "머리가 물 밖으로 나왔다");

            t0 = Time.time;
            while (RenderSettings.fogDensity >= UnderwaterFog.Density * 0.9f && Time.time - t0 < 5f)
            {
                DiveLightProbe.Surface();
                yield return null;
            }

            E2EHarness.Assert(RenderSettings.fogDensity < UnderwaterFog.Density * 0.9f,
                              $"안개가 뭍의 것으로 돌아왔다 (밀도 {RenderSettings.fogDensity:F4})");
            E2EHarness.Assert(_lantern.IsOn, "나와도 랜턴은 켜진 채다");
        }

        // ── 6. 실측표 ────────────────────────────────────────────

        static IEnumerator 실측표()
        {
            E2EHarness.Log("— 실측 (손잡이는 Domain/Art/UnderwaterFog.cs) —");
            E2EHarness.Log($"  물속 밀도 {UnderwaterFog.Density:F4} " +
                           $"(세계가 닫히는 거리 {UnderwaterFog.CloseDistance:F1}m)");
            E2EHarness.Log($"  물속 안개색 휘도 {UnderwaterFog.Luminance(UnderwaterFog.Color):F4} " +
                           $"/ 수면 위 밴드 {UnderwaterFog.SurfaceLuminance:F4}");
            for (int t = 1; t <= LanternRule.MaxTier; t++)
                E2EHarness.Log($"  티어 {t}: 지상 유효 {UnderwaterFog.LanternRangeOnLand(t):F1}m, " +
                               $"물속 유효 {UnderwaterFog.LanternRangeUnderwater(t):F1}m");
            yield return null;
        }

        // ── 7. 되돌린다 ──────────────────────────────────────────

        static IEnumerator 되돌린다()
        {
            _lantern.SetSwitch(true);
            _lantern.Recharge(LanternRule.MaxBattery);
            DiveLightProbe.Reset();
            E2EHarness.RestoreWorld();
            yield return null;
        }
    }
}
