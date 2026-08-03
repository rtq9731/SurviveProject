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
                AssertColorApprox(expectedRegionColor, RenderSettings.ambientLight,
                    $"{scenePath}: 환경광 색이 포그 색과 다르다 (스펙 §5)");

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
