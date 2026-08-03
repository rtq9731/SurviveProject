using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Survive.Domain.Art;

namespace Survive.Tests.EditMode
{
    /// <summary>
    /// 스펙 §11 [자동]의 "씬 설정 단언 테스트" — 포그 활성, 환경광 상한, 볼륨
    /// 프로파일 구성 요소 존재를 코드로 단언한다. 이것이 씬에 박힌 리터럴
    /// 색상값(예: MainScene의 {0.047, 0.059, 0.082})을 ArtPalette로 다시
    /// 묶어 주는 유일한 테스트다 — 사람이 인스펙터에서 색을 건드려도
    /// 이 테스트가 없으면 아무도 모른다.
    ///
    /// RenderSettings(안개·환경광)는 씬 애셋 자체가 아니라 "현재 활성 씬"의
    /// 데이터다. 여러 씬을 동시에 로드해도 활성 씬이 아니면 RenderSettings에
    /// 반영되지 않는다(실측 확인 완료). 그래서 검사 대상 씬을 Additive로
    /// 열고 반드시 SetActiveScene으로 활성화한 뒤 읽는다.
    ///
    /// 이미 에디터에 열려 있던 씬(예: 사람이 저장하지 않은 편집을 가진
    /// StartScene)은 절대 Single 모드로 다시 열거나 닫지 않는다 — 이
    /// 테스트가 스스로 연 씬만 닫는다.
    /// </summary>
    public class SceneArtSettingsTests
    {
        const string MainScenePath = "Assets/01.Scenes/MainScene.unity";
        const string StartScenePath = "Assets/01.Scenes/StartScene.unity";
        const float ColorTolerance = 0.002f;

        [Test]
        public void MainScene_fog_and_ambient_match_FogIslands()
            => AssertSceneArtSettings(MainScenePath, ArtPalette.FogIslands);

        [Test]
        public void StartScene_fog_and_ambient_match_FogSurface()
            => AssertSceneArtSettings(StartScenePath, ArtPalette.FogSurface);

        /// <summary>
        /// 환경광이 구역 포그와 같은 색조인지, 그리고 포그보다 밝지 않은지 본다.
        /// 채널 비율을 비교하므로 명도를 어떻게 낮추든 통과하고,
        /// 색조가 틀어지면(예: 청록 구역인데 환경광만 주황) 잡힌다.
        /// </summary>
        static void AssertSameHueButDarker(Color region, Color ambient, string scenePath)
        {
            float regionMax = Mathf.Max(region.r, Mathf.Max(region.g, region.b));
            float ambientMax = Mathf.Max(ambient.r, Mathf.Max(ambient.g, ambient.b));

            Assert.LessOrEqual(ambientMax, regionMax + ColorTolerance,
                $"{scenePath}: 환경광이 구역 포그보다 밝다 (스펙 §5 — 지하에 태양은 없다)");

            // 완전한 검정은 허용한다.
            //
            // 처음에는 "형태만 겨우 읽히는 하한"을 남기라고 스펙에 썼고 여기서도
            // 0보다 커야 한다고 단언했다. 플레이해 보니 그 전제가 틀렸다 —
            // 환경광이 조금이라도 있으면 광원이 없는 곳까지 지면과 풀이 읽혀서
            // 따라다니는 밝기 거품이 생기고 암흑이 성립하지 않는다.
            // 빛기둥·발광 버섯·랜턴이 있으니 환경광이 대신 밝혀줄 이유가 없다.
            if (ambientMax <= 0f) return;

            var normRegion = region / regionMax;
            var normAmbient = ambient / ambientMax;
            AssertColorApprox(normRegion, normAmbient,
                $"{scenePath}: 환경광의 색조가 구역 포그와 다르다 (밝기가 아니라 색이 틀어졌다)");
        }

        static void AssertSceneArtSettings(string scenePath, Color expectedRegionColor)
        {
            var previousActive = SceneManager.GetActiveScene();
            var scene = OpenForInspection(scenePath, out var wasAlreadyOpen);
            try
            {
                Assert.IsTrue(scene.IsValid() && scene.isLoaded, $"씬을 열지 못했다: {scenePath}");
                SceneManager.SetActiveScene(scene);

                Assert.IsTrue(RenderSettings.fog, $"{scenePath}: 포그가 꺼져 있다");
                AssertColorApprox(expectedRegionColor, RenderSettings.fogColor,
                    $"{scenePath}: 포그 색이 ArtPalette와 다르다");
                // 환경광은 구역 포그와 **같은 색조**이되 더 어둡다.
                //
                // 처음에는 두 값이 같아야 한다고 단언했는데, 실제로 플레이해 보니
                // 광원이 하나도 없는 곳에서도 지면과 풀이 읽혔다. 지하에 태양이 없으면
                // 안 보이는 게 맞다. 그래서 환경광을 포그 색보다 낮춰 잡는다.
                //
                // 세기(ambientIntensity)로는 못 낮춘다 — Flat 모드에서 Unity가 그 배율을
                // 무시한다. 실측으로 확인했다(0.15 → 5로 33배 올려도 화면 밝기 8% 변화).
                // 그래서 색의 명도로 조절하고, 색조가 어긋나지 않았는지를 여기서 지킨다.
                AssertSameHueButDarker(expectedRegionColor, RenderSettings.ambientLight, scenePath);

                var volume = scene.GetRootGameObjects()
                    .SelectMany(go => go.GetComponentsInChildren<Volume>(true))
                    .FirstOrDefault();
                Assert.IsNotNull(volume, $"{scenePath}: 전역 Volume이 없다");
                Assert.IsNotNull(volume.sharedProfile, $"{scenePath}: Volume의 sharedProfile이 비어 있다");

                var profile = volume.sharedProfile;
                AssertHasResolvedComponent<Tonemapping>(profile, scenePath);
                AssertHasResolvedComponent<Bloom>(profile, scenePath);
                AssertHasResolvedComponent<ColorAdjustments>(profile, scenePath);
                AssertHasResolvedComponent<Vignette>(profile, scenePath);
            }
            finally
            {
                SceneManager.SetActiveScene(previousActive);
                if (!wasAlreadyOpen)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>
        /// 이미 로드되어 있는 씬은 그대로 재사용한다(재로드하지 않는다) —
        /// 사람의 미저장 편집을 가진 씬을 건드리지 않기 위해서다.
        /// </summary>
        static Scene OpenForInspection(string path, out bool wasAlreadyOpen)
        {
            var existing = SceneManager.GetSceneByPath(path);
            if (existing.IsValid() && existing.isLoaded)
            {
                wasAlreadyOpen = true;
                return existing;
            }
            wasAlreadyOpen = false;
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        }

        static void AssertColorApprox(Color expected, Color actual, string message)
        {
            Assert.AreEqual(expected.r, actual.r, ColorTolerance, message + " (R)");
            Assert.AreEqual(expected.g, actual.g, ColorTolerance, message + " (G)");
            Assert.AreEqual(expected.b, actual.b, ColorTolerance, message + " (B)");
        }

        /// <summary>
        /// components.Count만 세면 통과하는 버그가 이 단계에서 실제로 있었다
        /// (널 4개가 들어가도 Count는 4). 타입 T로 실제로 리졸브되는 컴포넌트가
        /// 있는지, 그리고 그것이 널이 아닌지까지 확인한다.
        /// </summary>
        static void AssertHasResolvedComponent<T>(VolumeProfile profile, string scenePath)
            where T : VolumeComponent
        {
            Assert.IsTrue(profile.TryGet<T>(out var component) && component != null,
                $"{scenePath}: Volume 프로파일에 {typeof(T).Name}이 없다(또는 널이다)");
        }
    }
}
