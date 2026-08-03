using System.Collections.Generic;
using System.Linq;
using System.Text;
using Survive.Domain.Art;
using UnityEditor;
using UnityEngine;

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
    }
}
