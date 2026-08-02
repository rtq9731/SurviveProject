using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Survive.EditorTools
{
    /// <summary>
    /// 씬·프리팹의 끊긴 참조를 전수로 찾는다.
    ///
    /// 왜 필요한가: 에셋을 다시 만들면 GUID가 바뀌는데, 그걸 가리키던 필드는
    /// 조용히 비워질 뿐 아무 오류도 내지 않는다. 실제로 이 프로젝트에서
    /// 제작대에 레시피가 안 뜨고(RecipeBook), 광맥을 캘 수 없던(OreVein definition)
    /// 두 사고가 모두 이 방식으로 일어났다. 컴파일도 통과하고 콘솔도 조용하다.
    ///
    /// 그래서 잡는 것은 셋이다.
    ///   1) MonoBehaviour의 스크립트 자체가 없어진 것 ("script can not be loaded")
    ///   2) [SerializeField] 오브젝트 참조가 null인 것
    ///   3) 참조가 있었는데 대상이 삭제돼 Missing으로 남은 것
    ///
    /// null이 전부 사고는 아니다. 선택적인 필드는 <see cref="IntentionalNulls"/>에
    /// 근거와 함께 적어 두고 보고에서 뺀다. 목록에 없는 null은 전부 보고한다.
    /// </summary>
    public static class ReferenceIntegrityChecker
    {
        static readonly string[] ScenePaths =
        {
            "Assets/01.Scenes/MainScene.unity",
            "Assets/01.Scenes/StartScene.unity",
        };

        static readonly string[] PrefabRoots = { "Assets/05.Prefabs" };

        /// <summary>
        /// 비어 있어도 되는 필드. "타입.필드" 형식이며 값은 그래도 되는 이유다.
        /// 근거 없이 여기 넣지 말 것 — 그러면 이 도구가 아무것도 막지 못한다.
        /// </summary>
        static readonly Dictionary<string, string> IntentionalNulls = new Dictionary<string, string>
        {
            // Feel 피드백은 C3 게이트에서 사람이 조립한다. 그때까지 비어 있는 게 정상이다.
            ["*.startFeedback"] = "C3에서 조립",
            ["*.completeFeedback"] = "C3에서 조립",
            ["*.craftFeedback"] = "C3에서 조립",
            ["*.openFeedback"] = "C3에서 조립",
            ["*.activateFeedback"] = "C3에서 조립",
            ["*.eatFeedback"] = "C3에서 조립",
            ["*.collectFeedback"] = "C3에서 조립",
            ["*.lowBatteryFeedback"] = "C3에서 조립",
            ["*.rechargeFeedback"] = "C3에서 조립",
            ["*.oxygenWarningFeedback"] = "C3에서 조립",
            ["*.oxygenRecoveredFeedback"] = "C3에서 조립",
            ["*.deathFeedback"] = "C3에서 조립",
            ["*.hitFeedback"] = "C3에서 조립",
            ["*.swingFeedback"] = "C3에서 조립",
            ["*.hurtFeedback"] = "C3에서 조립",
            ["*.enterFeedback"] = "C3에서 조립",
            ["*.exitFeedback"] = "C3에서 조립",
            ["*.submergeFeedback"] = "C3에서 조립",
            ["*.harvestFeedback"] = "C3에서 조립",
            ["*.witherFeedback"] = "C3에서 조립",

            // 아래는 코드에 대체 경로가 있다. 비어 있어도 동작이 정의된다.
            ["PlantNode.visual"] = "Awake에서 자기 transform으로 대체",
            ["HarvestNode.visual"] = "Awake에서 자기 transform으로 대체",
            ["CreatureBrain.agent"] = "Awake에서 GetComponent로 대체",
            ["CreatureBrain.flyer"] = "지상 생물에는 없는 것이 정상. Awake에서 GetComponent로 대체",
            // 떨어진 아이템의 겉모습은 ItemDataSO.worldPrefab이 갖는다.
            // 한 번에 여러 종류를 떨구는 경우 떨구는 쪽의 프리팹 하나로는 맞출 수 없다.
            // 아래 둘은 특정 대상만 다르게 보이게 하고 싶을 때 쓰는 덮어쓰기 슬롯이다.
            ["CreatureHealth.pickupPrefab"] = "비움이 기본. 아이템의 worldPrefab을 쓴다",
            ["HarvestNode.dropPrefab"] = "비움이 기본. 아이템의 worldPrefab을 쓴다",

            // 구간의 마지막 포탈은 다음 씬이 없다. PortalDevice가 이 경우를 정상 종료로 다룬다.
            ["PortalDevice.destination"] = "챕터 2 씬이 아직 없다. 빈 값이 구간 종료를 뜻한다",

            // 비우면 런타임에 Camera.main / 자기 컴포넌트로 대체된다.
            ["PlayerInteractor.rayOrigin"] = "비우면 Camera.main으로 대체",
            ["MeleeSwing.swingOrigin"] = "비우면 Camera.main으로 대체",
            ["UIStateService.panelBehaviours"] = "비우면 씬에서 자동 수집",
            ["CreatureFeeding.glowRenderer"] = "비우면 자기 렌더러 사용",
        };

        [MenuItem("Tools/Survive/참조 무결성 점검")]
        public static void RunFromMenu() => Debug.Log(Run());

        /// <summary>전수 점검하고 사람이 읽을 보고서를 돌려준다.</summary>
        public static string Run()
        {
            var report = new StringBuilder();
            var findings = new List<string>();
            _allowed.Clear();
            int scanned = 0;

            string activeBefore = SceneManager.GetActiveScene().path;

            foreach (var path in ScenePaths)
            {
                if (string.IsNullOrEmpty(path) || AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    findings.Add($"[씬 없음] {path}");
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (var root in scene.GetRootGameObjects())
                    scanned += Scan(root, path, findings);
            }

            foreach (var root in PrefabRoots)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { root }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go != null) scanned += Scan(go, path, findings);
                }
            }

            if (!string.IsNullOrEmpty(activeBefore) && activeBefore != SceneManager.GetActiveScene().path)
                EditorSceneManager.OpenScene(activeBefore, OpenSceneMode.Single);

            report.AppendLine($"컴포넌트 {scanned}개 점검");

            // 의도된 null도 함께 센다. 그래야 "왜 통과했는지"를 사람이 확인할 수 있다.
            if (_allowed.Count > 0)
            {
                report.AppendLine($"의도된 빈 참조 {_allowed.Values.Sum()}건 (근거 있음)");
                foreach (var kv in _allowed.OrderByDescending(k => k.Value))
                    report.AppendLine($"    {kv.Value,4}  {kv.Key}");
            }

            if (findings.Count == 0)
            {
                report.AppendLine("끊긴 참조 없음 — 남은 것은 전부 근거가 있다");
                return report.ToString();
            }

            report.AppendLine($"발견 {findings.Count}건");
            foreach (var f in findings) report.AppendLine("  " + f);
            return report.ToString();
        }

        static int Scan(GameObject root, string owner, List<string> findings)
        {
            int count = 0;

            // 스크립트가 통째로 날아간 경우. 한 파일에 MonoBehaviour를 여러 개
            // 넣었을 때 이렇게 된다 — 이미 한 번 겪었다.
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                int missingScripts = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if (missingScripts > 0)
                    findings.Add($"[스크립트 없음 x{missingScripts}] {owner} :: {Path(t)}");
            }

            foreach (var comp in root.GetComponentsInChildren<Component>(true))
            {
                count++;
                if (comp == null) continue;   // 위에서 이미 보고했다

                // 서드파티는 우리가 배선하지 않는다. 보고하면 진짜 문제가 묻힌다.
                var ns = comp.GetType().Namespace ?? "";
                if (!ns.StartsWith("Survive")) continue;

                var so = new SerializedObject(comp);
                var p = so.GetIterator();
                bool enterChildren = true;

                while (p.NextVisible(enterChildren))
                {
                    enterChildren = true;
                    if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (p.propertyPath == "m_Script") continue;

                    // Unity 6.5에서 objectReferenceInstanceIDValue가 EntityID로 바뀌었다.
                    // 값이 남아 있는데 objectReferenceValue가 null이면 대상이 삭제된 것이다.
                    bool hadTarget = p.objectReferenceEntityIdValue != default;
                    bool missing = p.objectReferenceValue == null && hadTarget;
                    bool empty = p.objectReferenceValue == null && !hadTarget;

                    if (!missing && !empty) continue;

                    string typeName = comp.GetType().Name;
                    string field = p.propertyPath;

                    // Missing은 근거와 무관하게 항상 사고다. 참조가 있었는데 대상이 사라진 것이다.
                    if (missing)
                    {
                        findings.Add($"[대상 삭제됨] {owner} :: {Path(comp.transform)} " +
                                     $":: {typeName}.{field}");
                        continue;
                    }

                    if (IsIntentional(typeName, field)) continue;

                    findings.Add($"[빈 참조] {owner} :: {Path(comp.transform)} :: {typeName}.{field}");
                }
            }

            return count;
        }

        /// <summary>이번 점검에서 근거로 넘어간 항목. 근거별 건수.</summary>
        static readonly Dictionary<string, int> _allowed = new Dictionary<string, int>();

        static bool IsIntentional(string typeName, string field)
        {
            // 배열 원소는 "arr.Array.data[0]" 형태로 온다. 필드 이름만 떼서 본다.
            string simple = field.Split('.')[0];

            foreach (var key in new[] { $"{typeName}.{simple}", $"*.{simple}" })
            {
                if (!IntentionalNulls.TryGetValue(key, out var reason)) continue;

                string line = $"{key} — {reason}";
                _allowed.TryGetValue(line, out int n);
                _allowed[line] = n + 1;
                return true;
            }
            return false;
        }

        static string Path(Transform t)
        {
            var parts = new List<string>();
            while (t != null) { parts.Add(t.name); t = t.parent; }
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
