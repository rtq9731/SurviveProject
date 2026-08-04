using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEditor.Formats.Fbx.Exporter;

// 동굴 천장. 빛기둥이 나오는 구멍만 뚫려 있고 나머지는 바위다.
//
// 레거시의 BackGround는 중심 y=580, 지름 2843m짜리 원경 덩어리라
// 천장으로 쓸 물건이 아니었다. 고리 모양 메시로 직접 만든다.
//
// 법선은 아래를 향한다 — 밑에서만 보이는 면이다.

// 구멍은 빛기둥 메시가 딱 막도록 맞춘다. 빔은 y=92에서 반경 9m다 —
// 조금이라도 크면 천장과 빔 사이로 검은 틈이 보인다.
const float HoleR   = 8.6f;
const float OuterR  = 420f;
const float BaseY   = 92f;    // 빔 꼭대기와 같은 높이
const int   Rings   = 16;
const int   Seg     = 56;

var rnd = new System.Random(1204);
var v = new List<Vector3>();
var uv = new List<Vector2>();
var tri = new List<int>();

for (int r = 0; r <= Rings; r++)
{
    float t = (float)r / Rings;
    // 반경은 지수로 벌린다 — 구멍 근처를 촘촘하게 해야 테두리가 곱다
    float radius = Mathf.Lerp(HoleR, OuterR, Mathf.Pow(t, 2.2f));

    // 높이: 멀어질수록 완만히 올라가고, 큰 덩어리들이 얹힌다
    float rise = Mathf.Pow(t, 1.4f) * 44f;
    for (int i = 0; i < Seg; i++)
    {
        float a = (float)i / Seg * Mathf.PI * 2f;
        // 구멍 언저리는 흔들지 않는다. 흔들면 빛기둥과 어긋나 틈이 보인다
        float lump = t < 0.06f ? 0f
                   : (Mathf.PerlinNoise(Mathf.Cos(a) * radius * 0.012f + 3.1f,
                                        Mathf.Sin(a) * radius * 0.012f + 7.7f) - 0.5f) * (10f + t * 34f);
        float jitter = t < 0.06f ? 0f : (float)(rnd.NextDouble() - 0.5) * (1.5f + t * 6f);
        float rr = radius * (1f + (t < 0.06f ? 0f : (float)(rnd.NextDouble() - 0.5) * 0.05f));

        var p = new Vector3(Mathf.Cos(a) * rr, BaseY + rise + lump + jitter, Mathf.Sin(a) * rr);
        v.Add(p);
        uv.Add(new Vector2(p.x, p.z) * 0.02f + new Vector2(0.5f, 0.5f));
    }
}

// 감기: 아래에서 보이도록
for (int r = 0; r < Rings; r++)
    for (int i = 0; i < Seg; i++)
    {
        int a0 = r * Seg + i, a1 = r * Seg + (i + 1) % Seg;
        int b0 = (r + 1) * Seg + i, b1 = (r + 1) * Seg + (i + 1) % Seg;
        tri.Add(a0); tri.Add(b0); tri.Add(a1);
        tri.Add(a1); tri.Add(b0); tri.Add(b1);
    }

var mesh = new Mesh { name = "CaveCeiling" };
mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
mesh.RecalculateNormals(); mesh.RecalculateBounds();

// 아래를 향하는지 확인 — 감기를 틀리면 위에서만 보이고 밑에서는 뚫려 보인다(전에 당했다)
var n = mesh.normals;
int down = 0;
for (int i = 0; i < n.Length; i++) if (n[i].y < 0f) down++;
Debug.Log($"[천장] 정점 {v.Count} 삼각형 {tri.Count / 3} / 법선이 아래를 향하는 정점 {down}/{n.Length}");
if (down < n.Length * 0.8f) Debug.LogWarning("[천장] 법선이 위를 향한다 — 감기를 뒤집어야 한다");

var tmp = new GameObject("CaveCeiling");
tmp.AddComponent<MeshFilter>().sharedMesh = mesh;
tmp.AddComponent<MeshRenderer>().sharedMaterials = new Material[0];

const string fbx = "Assets/10.Generated/Islands/CaveCeiling.fbx";
ModelExporter.ExportObject(fbx, tmp, new ExportModelOptions { ExportFormat = ExportFormat.Binary });
Object.DestroyImmediate(tmp);
Object.DestroyImmediate(mesh);
AssetDatabase.Refresh();

var imp = AssetImporter.GetAtPath(fbx) as ModelImporter;
if (imp != null)
{
    imp.materialImportMode = ModelImporterMaterialImportMode.None;
    imp.importNormals = ModelImporterNormals.Import;
    imp.importCameras = false; imp.importLights = false; imp.importAnimation = false;
    imp.SaveAndReimport();
}

var imported = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().FirstOrDefault();
Debug.Log($"[천장] FBX 정점 {imported.vertexCount} 크기={imported.bounds.size:0.#} y {imported.bounds.min.y:0.#}~{imported.bounds.max.y:0.#}");

var old = GameObject.Find("CaveCeiling");
if (old != null) Object.DestroyImmediate(old);

var go = new GameObject("CaveCeiling");
go.transform.position = Vector3.zero;
go.AddComponent<MeshFilter>().sharedMesh = imported;
go.AddComponent<MeshRenderer>().sharedMaterial =
    AssetDatabase.LoadAssetAtPath<Material>("Assets/03.Materials/IslandRock.mat");
// 콜라이더는 두지 않는다 — 닿을 일이 없고 42만 삼각형을 물리에 올릴 이유가 없다

EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
Debug.Log("[천장] 배치 완료");
