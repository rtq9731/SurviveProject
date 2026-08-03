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

            return sb.ToString();
        }

        static List<MaterialFacts> Collect()
        {
            var roots = new List<string>(ScenePaths);
            roots.AddRange(AssetDatabase
                .FindAssets("t:Prefab", PrefabRoots)
                .Select(AssetDatabase.GUIDToAssetPath));

            var matPaths = new HashSet<string>();
            foreach (var dep in AssetDatabase.GetDependencies(roots.ToArray(), true))
                if (dep.EndsWith(".mat")) matPaths.Add(dep);

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
