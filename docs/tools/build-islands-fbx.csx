using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.Formats.Fbx.Exporter;

// P2 섬 블록아웃 — FBX 판.
//
// 형태는 그대로 두고 출력 형식만 .asset → .fbx 로 옮긴다.
// .asset은 유니티 밖에서 열 수 없어서 블렌더로 넘길 수가 없다.

const float UvScale = 0.03f;

Mesh MakeIsland(string name, float radius, float topY, float bellyDepth, float spikeMax, int seed)
{
    // 링 7개 중 앞 5개가 대지, 뒤 2개가 해안 경사다.
    const int Plateau = 5, Rings = 7, Seg = 28;
    const float WaterY = 50.1f;
    var rnd = new System.Random(seed);
    var v = new List<Vector3>();
    var uv = new List<Vector2>();
    var tri = new List<int>();

    System.Func<Vector3, Vector2> PlanarUv = p => new Vector2(p.x, p.z) * UvScale + new Vector2(0.5f, 0.5f);

    // ── 윗면 ──
    v.Add(new Vector3(0f, topY, 0f)); uv.Add(new Vector2(0.5f, 0.5f));
    for (int r = 1; r <= Rings; r++)
    {
        bool shore = r > Plateau;
        float t = Mathf.Min(r, Plateau) / (float)Plateau;
        float st = shore ? (r - Plateau) / (float)(Rings - Plateau) : 0f;

        float ringR = shore ? radius * Mathf.Lerp(1f, 1.22f, st) : radius * t;
        float domeY = topY - (1f - Mathf.Cos(t * Mathf.PI * 0.5f)) * 3.2f;
        float ringY = shore ? Mathf.Lerp(domeY, WaterY - 3.5f, Mathf.Pow(st, 0.75f)) : domeY;
        float wobAmp = shore ? 0.5f : (1.2f + t * 3f);

        for (int i = 0; i < Seg; i++)
        {
            float a = (float)i / Seg * Mathf.PI * 2f;
            float wob = (float)(rnd.NextDouble() - 0.5) * wobAmp;
            float rr = ringR * (1f + (float)(rnd.NextDouble() - 0.5) * (shore ? 0.06f : 0.14f));
            var p = new Vector3(Mathf.Cos(a) * rr, ringY + wob, Mathf.Sin(a) * rr);
            v.Add(p); uv.Add(PlanarUv(p));
        }
    }
    for (int i = 0; i < Seg; i++) { tri.Add(0); tri.Add(1 + (i + 1) % Seg); tri.Add(1 + i); }
    for (int r = 0; r < Rings - 1; r++)
        for (int i = 0; i < Seg; i++)
        {
            int a0 = 1 + r * Seg + i, a1 = 1 + r * Seg + (i + 1) % Seg;
            int b0 = 1 + (r + 1) * Seg + i, b1 = 1 + (r + 1) * Seg + (i + 1) % Seg;
            tri.Add(a0); tri.Add(a1); tri.Add(b0);
            tri.Add(a1); tri.Add(b1); tri.Add(b0);
        }

    // ── 밑면(천장) ──
    // 정점을 윗면과 공유하지 않는다. 공유하면 RecalculateNormals가
    // 두 면의 법선을 평균내 밑면이 자기 방향을 잃는다.
    int outerTop = 1 + (Plateau - 1) * Seg;
    const int BellyRings = 3;
    int bellyStart = v.Count;
    for (int r = 0; r <= BellyRings; r++)
    {
        float t = (float)r / BellyRings;
        for (int i = 0; i < Seg; i++)
        {
            var top = v[outerTop + i];
            float shrink = Mathf.Lerp(1f, 0.15f, t);
            var p = new Vector3(top.x * shrink,
                                topY - Mathf.Lerp(2f, bellyDepth, Mathf.Pow(t, 0.8f)),
                                top.z * shrink);
            v.Add(p); uv.Add(PlanarUv(p));
        }
    }
    int bellyApex = v.Count;
    v.Add(new Vector3(0f, topY - bellyDepth - 3f, 0f)); uv.Add(new Vector2(0.5f, 0.5f));

    for (int r = 0; r < BellyRings; r++)
        for (int i = 0; i < Seg; i++)
        {
            int a0 = bellyStart + r * Seg + i, a1 = bellyStart + r * Seg + (i + 1) % Seg;
            int b0 = bellyStart + (r + 1) * Seg + i, b1 = bellyStart + (r + 1) * Seg + (i + 1) % Seg;
            tri.Add(a0); tri.Add(b0); tri.Add(a1);
            tri.Add(a1); tri.Add(b0); tri.Add(b1);
        }
    int lastBelly = bellyStart + BellyRings * Seg;
    for (int i = 0; i < Seg; i++)
    { tri.Add(lastBelly + i); tri.Add(bellyApex); tri.Add(lastBelly + (i + 1) % Seg); }

    // ── 종유석 ──
    int spikes = Mathf.RoundToInt(radius * 0.22f);
    for (int s = 0; s < spikes; s++)
    {
        float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
        float dist = radius * Mathf.Sqrt((float)rnd.NextDouble()) * 0.92f;
        float cx = Mathf.Cos(ang) * dist, cz = Mathf.Sin(ang) * dist;

        float k = dist / radius;
        float ceil = topY - Mathf.Lerp(bellyDepth, 2.5f, k) - 0.5f;

        float u = (float)rnd.NextDouble();
        float len = Mathf.Lerp(spikeMax * 0.25f, spikeMax, Mathf.Pow(u, 1.3f)) * (1f - k * 0.25f);
        float rad0 = Mathf.Lerp(2.0f, 6.5f, len / spikeMax);

        const int SSeg = 6;
        int b = v.Count;
        for (int i = 0; i < SSeg; i++)
        {
            float a = (float)i / SSeg * Mathf.PI * 2f;
            var p = new Vector3(cx + Mathf.Cos(a) * rad0, ceil, cz + Mathf.Sin(a) * rad0);
            v.Add(p); uv.Add(PlanarUv(p));
        }
        int tip = v.Count;
        v.Add(new Vector3(cx, ceil - len, cz)); uv.Add(new Vector2(0.5f, 0.5f));
        for (int i = 0; i < SSeg; i++)
        { tri.Add(b + i); tri.Add(tip); tri.Add(b + (i + 1) % SSeg); }
    }

    var m = new Mesh { name = name };
    m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
    m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
    m.RecalculateNormals(); m.RecalculateBounds();
    return m;
}

var specs = new[]
{
    new { name = "Island1_Landing", pos = new Vector3(0f,   0f, 0f),  radius = 50f, topY = 52.0f, belly = 7f, spike = 78f, seed = 11 },
    new { name = "Island2_Shoal",   pos = new Vector3(116f, 0f, 0f),  radius = 44f, topY = 51.4f, belly = 6f, spike = 64f, seed = 22 },
    new { name = "Island3_Far",     pos = new Vector3(211f, 0f, 37f), radius = 40f, topY = 58.0f, belly = 8f, spike = 88f, seed = 33 },
    new { name = "Island4_Surface", pos = new Vector3(309f, 0f, 77f), radius = 36f, topY = 51.0f, belly = 5f, spike = 56f, seed = 44 },
};

var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Unvik_3D/Cross_Plains/FBX/Materials/Cross_Plains.mat");
var report = new List<string>();

// ── 1) 생성 후 FBX로 내보낸다 ──
foreach (var s in specs)
{
    var mesh = MakeIsland(s.name, s.radius, s.topY, s.belly, s.spike, s.seed);
    var tmp = new GameObject(s.name);
    tmp.AddComponent<MeshFilter>().sharedMesh = mesh;
    // 머티리얼은 붙이지 않는다. 붙이면 FBX 안에 텍스처의 절대경로가 박혀
    // 다른 사람 기계에서 깨지고, 내 디스크 구조가 저장소에 들어간다.
    tmp.AddComponent<MeshRenderer>().sharedMaterials = new Material[0];

    int srcVerts = mesh.vertexCount;
    var srcSize = mesh.bounds.size;

    string fbx = "Assets/10.Generated/Islands/" + s.name + ".fbx";
    ModelExporter.ExportObject(fbx, tmp);
    Object.DestroyImmediate(tmp);
    Object.DestroyImmediate(mesh);

    // 왕복 후 크기가 달라지면(cm/m 환산 사고) 여기 값과 3)의 값이 어긋난다
    report.Add($"[원본] {s.name} 정점={srcVerts} 크기={srcSize.x:0.#}×{srcSize.y:0.#}×{srcSize.z:0.#}");
}
AssetDatabase.Refresh();

// ── 2) 임포트 설정: 머티리얼은 우리가 붙인다 ──
foreach (var s in specs)
{
    string fbx = "Assets/10.Generated/Islands/" + s.name + ".fbx";
    var imp = AssetImporter.GetAtPath(fbx) as ModelImporter;
    if (imp == null) { report.Add($"[오류] {s.name} 임포터 없음 — 내보내기 실패"); continue; }
    imp.materialImportMode = ModelImporterMaterialImportMode.None;
    imp.importNormals = ModelImporterNormals.Import;
    imp.importTangents = ModelImporterTangents.CalculateMikk;
    imp.importCameras = false;
    imp.importLights = false;
    imp.importAnimation = false;
    imp.SaveAndReimport();
}

// ── 3) 프리팹을 FBX 메시로 다시 짓는다 ──
var root = new GameObject("Archipelago");
foreach (var s in specs)
{
    string fbx = "Assets/10.Generated/Islands/" + s.name + ".fbx";
    var mesh = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().FirstOrDefault();
    if (mesh == null) { report.Add($"[오류] {s.name} FBX 안에 메시가 없다"); continue; }

    var go = new GameObject(s.name);
    go.transform.SetParent(root.transform, false);
    go.transform.localPosition = s.pos;
    go.AddComponent<MeshFilter>().sharedMesh = mesh;
    go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    go.AddComponent<MeshCollider>().sharedMesh = mesh;

    report.Add($"[섬] {s.name} 메시={mesh.name} 정점={mesh.vertexCount} 삼각형={mesh.triangles.Length / 3} 크기={mesh.bounds.size.x:0.#}×{mesh.bounds.size.y:0.#}×{mesh.bounds.size.z:0.#}");
}
PrefabUtility.SaveAsPrefabAsset(root, "Assets/05.Prefabs/Environment/Archipelago.prefab");
Object.DestroyImmediate(root);

// 낡은 .asset 메시는 이 스크립트 밖에서 지운다(DeleteAsset이 막혀 있다).

AssetDatabase.SaveAssets();
AssetDatabase.Refresh();
foreach (var line in report) Debug.Log(line);
