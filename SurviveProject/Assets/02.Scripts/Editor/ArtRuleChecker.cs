using System.Collections.Generic;
using System.Linq;
using System.Text;
using Survive.Domain.Art;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Survive.EditorTools
{
    /// <summary>
    /// 씬이 실제로 도달하는 머티리얼만 검사한다.
    ///
    /// 프로젝트 전체 .mat은 337개지만 게임에 나오는 것은 26개뿐이다.
    /// Feel 데모나 안 쓰는 팩 에셋까지 고치는 것은 낭비이고,
    /// 보고서가 길어지면 아무도 안 읽는다.
    /// </summary>
    public static class ArtRuleChecker
    {
        static readonly string[] ScenePaths =
        {
            "Assets/01.Scenes/MainScene.unity",
            "Assets/01.Scenes/StartScene.unity",
        };

        static readonly string[] PrefabRoots = { "Assets/05.Prefabs" };

        [MenuItem("Tools/Survive/아트 규칙 점검")]
        public static void RunFromMenu() => Debug.Log(Run());

        public static int ViolationCount()
        {
            int n = 0;
            foreach (var f in Collect())
                n += MaterialRule.Violations(f).Count;
            foreach (var f in CollectLights())
                n += LightRule.Violations(f).Count;
            return n;
        }

        public static string Run()
        {
            var facts = Collect();
            var sb = new StringBuilder();
            sb.AppendLine($"[아트 규칙 점검] 씬 도달 머티리얼 {facts.Count}개");

            int violations = 0;
            foreach (var f in facts.OrderBy(x => x.AssetPath))
            {
                var v = MaterialRule.Violations(f);
                if (v.Count == 0) continue;
                violations += v.Count;
                sb.AppendLine($"  {f.AssetPath}");
                foreach (var line in v) sb.AppendLine($"      - {line}");
            }

            sb.AppendLine(violations == 0
                ? "위반 없음."
                : $"위반 {violations}건.");

            // 광원 4색 규칙은 머티리얼의 Emission뿐 아니라 Light 컴포넌트 자체에도 적용된다.
            // 이 둘을 같은 도구가 같이 봐야 한다 — 머티리얼만 보면 실제로 씬을 밝히는
            // Light는 전혀 검사하지 않은 채로 "위반 없음"을 낼 수 있다.
            var lights = CollectLights();
            sb.AppendLine();
            sb.AppendLine($"[아트 규칙 점검] 라이트 {lights.Count}개");

            int lightViolations = 0;
            foreach (var f in lights.OrderBy(x => x.AssetPath).ThenBy(x => x.GameObjectName))
            {
                var v = LightRule.Violations(f);
                if (v.Count == 0) continue;
                lightViolations += v.Count;
                sb.AppendLine($"  {f.AssetPath} :: {f.GameObjectName}");
                foreach (var line in v) sb.AppendLine($"      - {line}");
            }

            sb.AppendLine(lightViolations == 0
                ? "라이트 위반 없음."
                : $"라이트 위반 {lightViolations}건.");

            // Directional은 LightRule이 판정하지 않는다(태양·전역 조명이므로). 하지만
            // 지하 씬에 Directional이 존재하는 것 자체는 원칙 1("지하에 태양이 없다")과
            // 맞는지 사람이 볼 일이다. 프롤로그(StartScene)의 태양은 정당하다(§5).
            // 꺼둔 것은 보고하지 않는다. 실제로 꺼져 있는 Directional을 계속 보고하는 바람에
            // 사람이 "왜 자꾸 켜져 있냐"고 되묻는 일이 있었다 — 켜져 있지 않았다.
            var directional = lights.Where(f => f.Type == LightType.Directional)
                                     .OrderBy(f => f.AssetPath).ToList();
            var liveSuns = directional.Where(f => f.Active).ToList();
            if (liveSuns.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"[사람 확인] 켜져 있는 Directional 라이트 {liveSuns.Count}개 — " +
                              "색은 판정 대상이 아니다(전역/태양광). 지하 씬에 있다면 " +
                              "'지하에 태양 없음' 원칙에 맞는지 사람이 본다");
                foreach (var f in liveSuns)
                    sb.AppendLine($"  {f.AssetPath} :: {f.GameObjectName} " +
                                  $"({ColorUtility.ToHtmlStringRGB(f.Color)}, intensity {f.Intensity:0.##})");
            }
            int sleeping = directional.Count - liveSuns.Count;
            if (sleeping > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"[참고] 꺼둔 Directional 라이트 {sleeping}개 — 빛을 내지 않으므로 판정하지 않는다");
            }

            // Metallic은 자동 판정하지 않는다. 사람이 볼 목록만 낸다.
            var metallic = facts.Where(f => f.Metallic > 0.01f).OrderBy(f => f.AssetPath).ToList();
            if (metallic.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"[사람 확인] Metallic > 0 인 머티리얼 {metallic.Count}개 — 기계·인공 구조물만 허용된다");
                foreach (var f in metallic)
                    sb.AppendLine($"  {f.AssetPath}  (metallic {f.Metallic:0.##})");
            }

            // 패키지 기본 머티리얼을 그대로 쓰고 있는 것도 사람이 볼 일이다.
            // 규칙 위반은 아니지만(우리 에셋이 아니므로), 대개는 머티리얼을
            // 지정하는 것을 잊은 것이다 — 게임에서 회색 기본 재질로 보인다.
            if (PackageMaterials.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"[사람 확인] 패키지 기본 머티리얼을 참조하는 것 {PackageMaterials.Count}개 — 검사 대상이 아니지만 대개 지정을 잊은 것이다");
                foreach (var p in PackageMaterials.OrderBy(x => x))
                    sb.AppendLine($"  {p}");
            }

            return sb.ToString();
        }

        /// <summary>검사에서 제외한 패키지 머티리얼. 사람이 볼 목록으로만 쓴다.</summary>
        static readonly HashSet<string> PackageMaterials = new HashSet<string>();

        static List<MaterialFacts> Collect()
        {
            PackageMaterials.Clear();

            var roots = new List<string>(ScenePaths);
            roots.AddRange(AssetDatabase
                .FindAssets("t:Prefab", PrefabRoots)
                .Select(AssetDatabase.GUIDToAssetPath));

            var matPaths = new HashSet<string>();
            foreach (var dep in AssetDatabase.GetDependencies(roots.ToArray(), true))
            {
                if (!dep.EndsWith(".mat")) continue;

                // 패키지 안의 머티리얼은 검사하지 않는다.
                //
                // 이 규칙은 "우리가 만든 에셋을 한 세계로 묶는다"는 것이지
                // 써드파티 패키지의 내용을 우리 취향에 맞추라는 것이 아니다.
                // 그리고 패키지는 Library/PackageCache에 있어 git이 추적하지 않는다 —
                // 거기를 고치면 새로 클론하거나 Library를 지우는 순간 원복되고,
                // 커밋에도 남지 않아 아무도 그 변경을 볼 수 없다.
                //
                // 실제로 URP 기본 머티리얼(Packages/com.unity.render-pipelines.universal/
                // Runtime/Materials/Lit.mat)을 한 번 고쳤다가 이 문제를 확인했다.
                if (dep.StartsWith("Packages/")) { PackageMaterials.Add(dep); continue; }

                matPaths.Add(dep);
            }

            var result = new List<MaterialFacts>();
            foreach (var path in matPaths)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                string shaderName = mat.shader != null ? mat.shader.name : "";
                float smoothness = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness")
                                 : mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness")
                                 : MaterialRule.SmoothnessMatte;
                float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
                bool hasEmission = mat.IsKeywordEnabled("_EMISSION");
                Color emission = mat.HasProperty("_EmissionColor")
                    ? mat.GetColor("_EmissionColor") : Color.black;

                result.Add(new MaterialFacts(path, shaderName, smoothness, metallic, hasEmission, emission));
            }
            return result;
        }

        /// <summary>
        /// 씬 두 개 + Assets/05.Prefabs 아래 프리팹에서 Light 컴포넌트를 전부 모은다.
        ///
        /// 머티리얼과 달리 Light.color는 AssetDatabase.GetDependencies로 얻을 수 없다 —
        /// 실제 씬 콘텐츠를 열어야 하는 값이다. ReferenceIntegrityChecker가 이미 같은
        /// 이유로 씬을 Single 모드로 열었다 닫는 방식을 쓰고 있어 그 패턴을 그대로 따른다.
        /// </summary>
        static List<LightFacts> CollectLights()
        {
            var result = new List<LightFacts>();
            string activeBefore = SceneManager.GetActiveScene().path;

            foreach (var path in ScenePaths)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var light in root.GetComponentsInChildren<Light>(true))
                        result.Add(ToFacts(light, path));
            }

            // 마지막으로 열었던 씬 그대로 두지 않는다 — 사람이 에디터에서 보던 씬으로 되돌린다.
            if (!string.IsNullOrEmpty(activeBefore) && activeBefore != SceneManager.GetActiveScene().path)
                EditorSceneManager.OpenScene(activeBefore, OpenSceneMode.Single);

            foreach (var root in PrefabRoots)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { root }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go == null) continue;

                    foreach (var light in go.GetComponentsInChildren<Light>(true))
                        result.Add(ToFacts(light, path));
                }
            }

            return result;
        }

        static LightFacts ToFacts(Light light, string assetPath)
            => new LightFacts(assetPath, light.gameObject.name, light.color, light.type, light.intensity,
                              light.enabled && light.gameObject.activeInHierarchy);
    }
}
