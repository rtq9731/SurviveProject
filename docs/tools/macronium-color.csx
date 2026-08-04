using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 섬을 떠받치는 액체층은 물이 아니라 매크로늄이다(로드맵 §4.3).
// 지금은 팩 기본값 그대로 청록·파랑이라 세계관과 어긋난다.
//
// 팩 머티리얼(Wavy.mat)을 직접 고치지 않는다 — 사본을 우리 폴더에 두고 거기서 칠한다.
// IslandRock을 다룬 방식과 같다.

// ArtPalette의 값 (Assets/02.Scripts/Domain/Art/ArtPalette.cs)
var Macronium     = new Color32(0xA1, 0x2E, 0xE0, 255);   // MARSO의 인공물
var MacroniumHigh = new Color32(0xE7, 0x7B, 0xFF, 255);   // 표면 하이라이트
var FogCliffs     = new Color32(0x2C, 0x12, 0x40, 255);   // 매크로늄이 고인 깊은 곳

const string src = "Assets/Stylized Water For URP/Materials/Wavy.mat";
const string dst = "Assets/03.Materials/MacroniumSurface.mat";

if (AssetDatabase.LoadAssetAtPath<Material>(dst) == null)
{
    if (!AssetDatabase.CopyAsset(src, dst)) { Debug.LogError("[매크로늄] 머티리얼 복제 실패"); return null; }
    Debug.Log($"[매크로늄] {dst} 생성");
}
var m = AssetDatabase.LoadAssetAtPath<Material>(dst);

Color WithA(Color32 c, float a) => new Color(c.r / 255f, c.g / 255f, c.b / 255f, a);

// 표면: 얕은 곳은 매크로늄 본색, 깊을수록 고인 색으로
m.SetColor("_WaterColorShallow",    WithA(Macronium,     0.50f));
m.SetColor("_WaterColorDeep",       WithA(FogCliffs,     1f));
m.SetColor("_WaterColorHorizon",    new Color(0.29f, 0.06f, 0.55f, 1f));   // 먼 수평선 — 더 짙은 보라
m.SetColor("_WaterColorUnderwater", new Color(0.22f, 0.10f, 0.35f, 0f));

// 거품·경계: 흰색이 아니라 매크로늄 하이라이트. 표면장력이 빛나는 것이다
m.SetColor("_SurfaceFoamColor1",    WithA(MacroniumHigh, 0.65f));
m.SetColor("_SurfaceFoamColor2",    WithA(MacroniumHigh, 0f));
m.SetColor("_IntersectionFoamColor", WithA(MacroniumHigh, 1f));
m.SetColor("_ShoreColor",           WithA(MacroniumHigh, 0f));

EditorUtility.SetDirty(m);
AssetDatabase.SaveAssets();

var sb = new System.Text.StringBuilder("[매크로늄] 표면 색\n");
foreach (var p in new[] { "_WaterColorShallow", "_WaterColorDeep", "_WaterColorHorizon", "_WaterColorUnderwater",
                          "_SurfaceFoamColor1", "_IntersectionFoamColor" })
    sb.AppendLine($"   {p,-24} #{ColorUtility.ToHtmlStringRGBA(m.GetColor(p))}");
Debug.Log(sb.ToString());

// 씬의 액체층에 물린다
int swapped = 0;
foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
{
    if (mr.sharedMaterial == null || AssetDatabase.GetAssetPath(mr.sharedMaterial) != src) continue;
    mr.sharedMaterial = m;
    EditorUtility.SetDirty(mr);
    Debug.Log($"[매크로늄] {mr.gameObject.name} → MacroniumSurface");
    swapped++;
}
Debug.Log($"[매크로늄] 렌더러 {swapped}개 교체");

// 수중 시야도 매크로늄 아래다. 파란 물색이면 어긋난다
foreach (var b in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
{
    if (b == null || b.GetType().Name != "UnderwaterView") continue;
    var so = new SerializedObject(b);
    var p = so.FindProperty("underwaterFog");
    if (p == null) { Debug.LogWarning("[매크로늄] underwaterFog 필드를 못 찾았다"); continue; }
    Debug.Log($"[매크로늄] 수중 포그 #{ColorUtility.ToHtmlStringRGB(p.colorValue)} → #{ColorUtility.ToHtmlStringRGB(WithA(FogCliffs, 1f))}");
    p.colorValue = WithA(FogCliffs, 1f);
    so.ApplyModifiedProperties();
    EditorUtility.SetDirty(b);
}

EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
AssetDatabase.SaveAssets();
