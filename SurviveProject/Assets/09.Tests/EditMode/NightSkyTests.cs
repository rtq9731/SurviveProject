using NUnit.Framework;
using Survive.Domain.Art;
using Survive.World;
using UnityEditor;
using UnityEngine;

namespace Survive.Tests.EditMode
{
    /// <summary>
    /// <b>하늘을 세웠다</b> — 지평선은 뿌옇게 자홍으로 번지고 머리 위는 별이 쏟아진다
    /// (세계관 §2 · 상세기획서 §7.4).
    ///
    /// 여기서 지키는 것은 넷이다.
    /// <list type="number">
    /// <item><b>하늘은 고도와 햇빛 둘로만 정해진다.</b> 동쪽을 보나 서쪽을 보나
    ///       같은 높이면 같은 색이다 — 방위가 끼어드는 순간 「대기의 두께」가 아니라
    ///       「어느 쪽이 예쁜가」가 된다</item>
    /// <item><b>규칙은 한 곳에 있다.</b> 스카이박스가 쓰는 <c>색 × 두께</c> 분해가
    ///       <see cref="DepthFog.SkyColor"/>와 정확히 같은 값을 낸다. 이것이
    ///       깨지면 셰이더가 제 규칙을 갖기 시작한 것이다</item>
    /// <item><b>별은 낮에 사라지고 광원이 아니다.</b></item>
    /// <item><b>회귀선.</b> 하늘을 세우면서 지하와 물속의 깊이 축을 건드리지
    ///       않았는가</item>
    /// </list>
    ///
    /// 별의 수와 밝기는 여기서 판정하지 않는다. <b>사람이 화면을 보고 정할 값</b>이고
    /// 코드가 지킬 것은 「무엇에 매달려 움직이는가」다.
    /// </summary>
    public class NightSkyTests
    {
        const float Tolerance = 1e-5f;
        const string 셰이더경로 = "Assets/03.Materials/Sky.shader";
        const string 머티리얼경로 = "Assets/03.Materials/Sky.mat";

        // ── ① 방위는 하늘색에 끼어들지 않는다 ──────────────────────

        [Test]
        public void 같은_높이면_어느_쪽을_보든_같은_하늘이다()
        {
            // 스카이박스가 실제로 색인하는 값이 시선 벡터의 y 하나라는 것을
            // 여기서 못 박는다. 셰이더가 x나 z를 읽기 시작하면 이 자리가 빨개진다.
            foreach (float elevation in new[] { 0f, 5f, 20f, 45f, 70f, 89f })
            {
                float 기준 = float.NaN;

                for (int i = 0; i < 36; i++)
                {
                    var dir = Quaternion.Euler(-elevation, i * 10f, 0f) * Vector3.forward;
                    float cover = NightSky.CoverageAtSin(dir.y);

                    if (float.IsNaN(기준)) { 기준 = cover; continue; }
                    Assert.AreEqual(기준, cover, Tolerance,
                        $"고도 {elevation}도에서 방위 {i * 10}도만 하늘이 다르다");
                }

                Assert.AreEqual(DepthFog.SkyCoverage(elevation), 기준, 1e-4f,
                    $"고도 {elevation}도에서 사인 색인이 각도 색인과 다른 값을 낸다");
            }
        }

        [Test]
        public void 굴러도_하늘은_같다()
        {
            // 카메라가 옆으로 기울어도 <b>보는 방향</b>이 같으면 같은 하늘이다.
            var 곧게 = Quaternion.Euler(-30f, 17f, 0f) * Vector3.forward;
            var 기울여 = Quaternion.Euler(-30f, 17f, 40f) * Vector3.forward;

            Assert.AreEqual(NightSky.CoverageAtSin(곧게.y), NightSky.CoverageAtSin(기울여.y), Tolerance);
        }

        // ── ② 규칙은 DepthFog 한 곳에 있다 ────────────────────────

        [Test]
        public void 하늘색은_지평선색_곱하기_대기두께로_정확히_갈라진다()
        {
            // <b>스카이박스가 값싼 이유가 이 한 줄이다.</b> 뒤엣것에 햇빛이 들어가지
            // 않으므로 표를 한 번만 구우면 되고, 매 프레임 바뀌는 것은 색 하나다.
            // 이 분해가 깨지면 표를 매 프레임 다시 구워야 한다.
            foreach (float daylight in new[] { 0f, 0.2f, 0.5f, 0.8f, 1f })
            {
                var 지평선 = DepthFog.HorizonColor(daylight);

                foreach (float elevation in new[] { 0f, 10f, 30f, 60f, 90f })
                {
                    float 두께 = DepthFog.SkyCoverage(elevation);
                    var 기대 = new Color(지평선.r * 두께, 지평선.g * 두께, 지평선.b * 두께, 1f);
                    var 실제 = DepthFog.SkyColor(elevation, daylight);

                    Assert.AreEqual(기대.r, 실제.r, Tolerance, $"햇빛 {daylight} 고도 {elevation} R");
                    Assert.AreEqual(기대.g, 실제.g, Tolerance, $"햇빛 {daylight} 고도 {elevation} G");
                    Assert.AreEqual(기대.b, 실제.b, Tolerance, $"햇빛 {daylight} 고도 {elevation} B");
                }
            }
        }

        [Test]
        public void 대기_두께_표에는_시각이_들어가지_않는다()
        {
            // 표가 시각을 타면 매 프레임 다시 구워야 한다. 그것은 이 라운드가
            // 고른 「가장 값싼 방법」이 무너지는 순간이다.
            var 표 = NightSky.CoverageTable();
            Assert.AreEqual(NightSky.CoverageSteps, 표.Length);

            for (int i = 0; i < 표.Length; i++)
                Assert.AreEqual(NightSky.CoverageAtSin((i + 0.5f) / NightSky.CoverageSteps),
                                표[i], Tolerance, $"{i}번 칸");
        }

        [Test]
        public void 표는_위로_갈수록_단조롭게_얇아진다()
        {
            var 표 = NightSky.CoverageTable();
            for (int i = 1; i < 표.Length; i++)
                Assert.LessOrEqual(표[i], 표[i - 1] + Tolerance,
                    $"{i}번 칸에서 대기가 도로 두꺼워졌다 — 화면에 띠가 생긴다");

            Assert.Greater(표[0], 0.9f, "지평선 쪽이 통째로 덮이지 않는다");
            Assert.Less(표[표.Length - 1], 0.3f, "머리 위가 뿌옇다");
        }

        [Test]
        public void 천정과_지평선의_하늘_밝기_차이가_한_자릿수를_넘는다()
        {
            // <b>이 라운드의 핵심 숫자.</b> 값 자체는 튜닝값(척도고도)이 정하므로
            // 여기서는 "한 화면 안에서 확실히 갈린다"만 지킨다.
            foreach (float daylight in new[] { 0f, 1f })
            {
                float 지평선 = ArtPalette.Luminance(DepthFog.SkyColor(0f, daylight));
                float 천정 = ArtPalette.Luminance(DepthFog.SkyColor(90f, daylight));

                Assert.Greater(지평선 / 천정, 5f,
                    $"햇빛 {daylight}에서 지평선이 천정의 {지평선 / 천정:F2}배밖에 안 된다");
            }

            // 낮이든 밤이든 같은 배율이다 — 색만 옮기고 두께는 시각을 모르기 때문이다.
            float 밤배 = ArtPalette.Luminance(DepthFog.SkyColor(0f, 0f)) /
                         ArtPalette.Luminance(DepthFog.SkyColor(90f, 0f));
            float 낮배 = ArtPalette.Luminance(DepthFog.SkyColor(0f, 1f)) /
                         ArtPalette.Luminance(DepthFog.SkyColor(90f, 1f));
            Assert.AreEqual(밤배, 낮배, 1e-3f, "밤과 낮의 대비 배율이 다르다");
        }

        // ── ③ 별은 낮에 사라진다 ──────────────────────────────────

        [Test]
        public void 한밤에는_별이_다_보인다()
        {
            Assert.AreEqual(1f, NightSky.StarVisibility(0f), Tolerance);
            Assert.AreEqual(1f, NightSky.StarVisibility(DayNightCycle.Daylight(0f)), Tolerance,
                            "자정인데 별이 없다");
        }

        [Test]
        public void 한낮에는_별이_하나도_없다()
        {
            // 경계값. 씻김 문턱에서 정확히 0이어야 하고 그 위로는 계속 0이다.
            Assert.AreEqual(0f, NightSky.StarVisibility(NightSky.StarWashoutDaylight), Tolerance);
            Assert.AreEqual(0f, NightSky.StarVisibility(1f), Tolerance);
            Assert.AreEqual(0f, NightSky.StarVisibility(DayNightCycle.Daylight(0.5f)), Tolerance,
                            "정오인데 별이 남았다");
        }

        [Test]
        public void 별은_해가_다_뜨기_전에_이미_사라진다()
        {
            // 「낮이 시들시들하다」와 「낮이 밤 같다」의 경계가 여기다.
            // 하늘이 조금만 밝아져도 별보다 밝아진다.
            Assert.Less(NightSky.StarWashoutDaylight, 0.5f,
                        "해가 절반 넘게 뜬 뒤까지 별이 남는다");
            Assert.AreEqual(0f, NightSky.StarVisibility(0.5f), Tolerance);
        }

        [Test]
        public void 밤낮_곡선을_따라_단조롭게_사라진다()
        {
            // 별에 따로 시계를 달지 않았는지 본다 — 주인은 DayNightCycle 하나다.
            for (float t = 0f; t <= 1f; t += 0.005f)
            {
                float v = NightSky.StarVisibility(DayNightCycle.Daylight(t));
                Assert.GreaterOrEqual(v, 0f, $"시각 {t:F3}에서 별이 음수다");
                Assert.LessOrEqual(v, 1f, $"시각 {t:F3}에서 별이 1을 넘는다");
            }

            float 앞 = float.MaxValue;
            for (float d = 0f; d <= 1f; d += 0.01f)
            {
                float v = NightSky.StarVisibility(d);
                Assert.LessOrEqual(v, 앞 + Tolerance, $"햇빛 {d:F2}에서 별이 도로 밝아졌다");
                앞 = v;
            }
        }

        [Test]
        public void 해뜰녘_한복판에서는_이미_밤의_별이_아니다()
        {
            // 해뜰녘이 시작되는 순간에는 아직 온전하고, 끝나기 전에 이미 없다.
            Assert.AreEqual(1f, NightSky.StarVisibility(DayNightCycle.Daylight(DayNightCycle.DawnStart)),
                            Tolerance, "해뜰녘 첫 순간에 이미 별이 흐리다");
            Assert.AreEqual(0f, NightSky.StarVisibility(DayNightCycle.Daylight(DayNightCycle.DawnEnd)),
                            Tolerance, "해뜰녘이 끝났는데 별이 남았다");
        }

        // ── ④ 별은 광원이 아니다 ──────────────────────────────────

        [Test]
        public void 별빛은_새_색이_아니라_팔레트의_지표광이다()
        {
            Assert.AreEqual(ArtPalette.LightShaft, NightSky.StarColor);
            Assert.IsTrue(EmissionPaletteMatch.IsAllowed(NightSky.StarColor,
                                                         MaterialRule.EmissionChannelTolerance),
                          "팔레트 밖 색이다 — 다섯 번째 색이 생긴다");
        }

        [Test]
        public void 별은_블룸이_번지는_구간에_들어가지_않는다()
        {
            // HDR 세기 1 위에서만 번지게 잡혀 있다(§7.5 ③). 별이 번지는 순간
            // 그것은 배경의 점이 아니라 「빛나는 물건」으로 읽힌다.
            Assert.Less(NightSky.StarPeak, 1f);
            Assert.Less(NightSky.StarPeakLuminance, 1f);
            Assert.Greater(NightSky.StarDimmest, 0f, "가장 어두운 별이 아예 없다");
            Assert.Less(NightSky.StarDimmest, 1f, "별이 전부 같은 밝기다");
        }

        [Test]
        public void 별은_밤하늘보다는_확실히_밝다()
        {
            // 읽히지 않으면 세운 것이 아니다. 천정의 밤하늘과 견준다.
            float 밤천정 = ArtPalette.Luminance(DepthFog.SkyColor(90f, 0f));
            Assert.Greater(NightSky.StarPeakLuminance, 밤천정 * 10f,
                $"가장 밝은 별({NightSky.StarPeakLuminance:F4})이 밤하늘({밤천정:F4})에 묻힌다");
        }

        [Test]
        public void 별은_화면에서_점으로_읽힐_크기다()
        {
            // 1080p·수직 화각 60도면 한 화소가 약 0.056도다. 그보다 작으면
            // 화소 사이에서 깜빡이고, 너무 크면 별이 아니라 공이 된다.
            Assert.Greater(NightSky.StarAngularDiameterDegrees, 0.056f,
                $"별이 한 화소보다 작다({NightSky.StarAngularDiameterDegrees:F4}도)");
            Assert.Less(NightSky.StarAngularDiameterDegrees, 0.6f,
                $"별이 너무 크다({NightSky.StarAngularDiameterDegrees:F4}도)");
        }

        [Test]
        public void 별의_수를_사람이_읽을_수_있는_값으로_말한다()
        {
            // 확률과 칸 수를 머릿속에서 곱하게 두면 밀도를 조절할 때 감이 안 선다.
            Assert.AreEqual(Mathf.RoundToInt(6f * (2f * NightSky.StarCells) *
                                             (2f * NightSky.StarCells) * NightSky.StarChance),
                            NightSky.ApproximateStarCount);
            Assert.Greater(NightSky.ApproximateStarCount, 100, "「쏟아진다」고 하기엔 적다");
        }

        // ── ⑤ 셰이더와 머티리얼이 규칙과 어긋나지 않는가 ─────────────

        [Test]
        public void 셰이더가_허용_목록_안에_있다()
        {
            Assert.IsTrue(MaterialRule.IsAllowedShader("Survive/Sky"));
        }

        [Test]
        public void 셰이더_기본값이_규칙과_같다()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(셰이더경로);
            Assert.IsNotNull(shader, 셰이더경로 + " 를 찾지 못했다");
            Assert.AreEqual("Survive/Sky", shader.name);

            Assert.AreEqual(NightSky.StarPeak, DefaultFloat(shader, "_StarPeak"), 1e-4f);
            Assert.AreEqual(NightSky.StarDimmest, DefaultFloat(shader, "_StarDimmest"), 1e-4f);
            Assert.AreEqual(NightSky.StarCells, DefaultFloat(shader, "_StarCells"), 1e-4f);
            Assert.AreEqual(NightSky.StarChance, DefaultFloat(shader, "_StarChance"), 1e-4f);
            Assert.AreEqual(NightSky.StarRadius, DefaultFloat(shader, "_StarRadius"), 1e-4f);

            var 별색 = DefaultColor(shader, "_StarColor");
            Assert.AreEqual(NightSky.StarColor.r, 별색.r, 0.01f, "별 색 R");
            Assert.AreEqual(NightSky.StarColor.g, 별색.g, 0.01f, "별 색 G");
            Assert.AreEqual(NightSky.StarColor.b, 별색.b, 0.01f, "별 색 B");
        }

        [Test]
        public void 하늘_머티리얼이_아트_규칙을_통과한다()
        {
            // 씬이 이 머티리얼을 물고 있으므로 ArtRuleChecker가 실제로 검사한다.
            // artViolations 0의 절반이 여기서 정해진다.
            var m = AssetDatabase.LoadAssetAtPath<Material>(머티리얼경로);
            Assert.IsNotNull(m, 머티리얼경로 + " 를 찾지 못했다");
            Assert.AreEqual("Survive/Sky", m.shader.name);

            float smoothness = m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness")
                             : m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness")
                             : MaterialRule.SmoothnessMatte;
            float metallic = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
            bool hasEmission = m.IsKeywordEnabled("_EMISSION");
            var emission = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;

            var facts = new MaterialFacts(머티리얼경로, m.shader.name, smoothness, metallic,
                                          hasEmission, emission);
            Assert.IsEmpty(MaterialRule.Violations(facts));
        }

        [Test]
        public void 하늘은_발광하지_않는다()
        {
            // 별은 배경에 찍히는 점이지 발광체가 아니다. 에미션을 켜는 순간
            // 광원 4색 판정 대상이 되고, 그때부터 하늘이 물건을 밝히기 시작한다.
            var m = AssetDatabase.LoadAssetAtPath<Material>(머티리얼경로);
            Assert.IsFalse(m.IsKeywordEnabled("_EMISSION"));
            Assert.AreEqual(MaterialGlobalIlluminationFlags.EmissiveIsBlack, m.globalIlluminationFlags);
        }

        [Test]
        public void 씬이_이_하늘을_물고_있다()
        {
            var scene = AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(
                "Assets/01.Scenes/MainScene.unity");
            Assert.IsNotNull(scene);

            bool 물고있다 = false;
            foreach (var dep in AssetDatabase.GetDependencies("Assets/01.Scenes/MainScene.unity", false))
                if (dep == 머티리얼경로) 물고있다 = true;

            Assert.IsTrue(물고있다,
                "MainScene이 하늘 머티리얼을 물고 있지 않다 — 빌드에 들어가지 않고 " +
                "아트 규칙 검사기도 이 머티리얼을 보지 못한다");
        }

        // ── ⑥ 회귀선 — 하늘을 세워도 깊이 축은 시각을 모른다 ─────────

        [Test]
        public void 지하_안개는_여전히_시각을_모른다()
        {
            // 하늘은 밤낮에 매달려 있고 깊이 축은 아니다. 둘이 얽히는 순간
            // 「밤이 되면 지하가 밝아진다」가 조용히 들어온다.
            foreach (float y in new[] { DepthFog.SeaLevelY - 5f, DepthFog.SeaLevelY - 20f,
                                        DepthFog.SeaLevelY - 40f })
            {
                DepthFog.Sample(y, out var color, out float density);

                // 같은 높이를 두 번 물어 같은 답이 나오는지 — 시각을 읽는 경로가
                // 생겼다면 여기에 시각을 넣을 자리가 있었을 것이다.
                DepthFog.Sample(y, out var again, out float againDensity);
                Assert.AreEqual(density, againDensity, Tolerance);
                Assert.AreEqual(color, again);

                Assert.Greater(density, DepthFog.SurfaceBaseDensity,
                    $"y={y}의 깊이 안개가 지상보다 옅다");
            }
        }

        [Test]
        public void 물속_안개는_여전히_랜턴에서_나온다()
        {
            Assert.AreEqual(LanternRule.ForwardReachForTier(1) * UnderwaterFog.CloseAtReachMultiple,
                            UnderwaterFog.CloseDistance, 1e-4f,
                            "닫히는 거리가 랜턴 정면 도달의 배수가 아니게 되었다");
            Assert.Less(UnderwaterFog.Luminance(UnderwaterFog.Color), UnderwaterFog.SurfaceLuminance,
                        "물속이 수면 위보다 밝다");
        }

        // ── 도구 ────────────────────────────────────────────────

        static float DefaultFloat(Shader shader, string name)
        {
            int i = shader.FindPropertyIndex(name);
            Assert.GreaterOrEqual(i, 0, $"셰이더에 {name} 프로퍼티가 없다");
            return shader.GetPropertyDefaultFloatValue(i);
        }

        static Color DefaultColor(Shader shader, string name)
        {
            int i = shader.FindPropertyIndex(name);
            Assert.GreaterOrEqual(i, 0, $"셰이더에 {name} 프로퍼티가 없다");
            var v = shader.GetPropertyDefaultVectorValue(i);
            return new Color(v.x, v.y, v.z, v.w);
        }
    }
}
