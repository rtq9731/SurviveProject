using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 착지 지점 = 한 점에서 터져나간 자국. 부호를 고친 판.
//
// 앞판의 버그: fallAxis를 (-o.z, 0, o.x)로 잡았다. Unity에서 축 a로 돌리면
// up은 a × up = (-a.z, 0, a.x) 방향으로 간다. 그 축을 넣으면 (-o.x, 0, -o.z),
// 즉 안쪽으로 눕는다. 갓이 웅덩이 위로 더 밀려들었다.
// 바깥으로 눕히려면 a = (o.z, 0, -o.x)여야 한다.
//
// 또 하나: 대상을 밑동 거리로 고르면 '갓만 뻗친' 거대버섯이 전부 샌다.
// 바운즈가 웅덩이 위로 겹치는지로 고른다.

const float PoolRadius  = 10f;
const float GroundZero  = 3.2f;
const float MinHeight   = 3f;
const float OverheadY   = 56f;      // 이보다 높은 것만이 빛을 막는다
var rnd = new System.Random(90125);

var spot = GameObject.Find("LightShaft").GetComponentInChildren<Light>(true);
var o = spot.transform.position;
var impact = new Vector2(o.x, o.z);

float GroundAt(float x, float z)
{
    var hits = Physics.RaycastAll(new Vector3(x, 200f, z), Vector3.down, 300f, ~0, QueryTriggerInteraction.Ignore)
        .Where(h => h.collider.name.StartsWith("Island")).OrderByDescending(h => h.point.y).ToArray();
    return hits.Length > 0 ? hits[0].point.y : float.NaN;
}

bool Overhangs(Bounds b, float radius)
{
    float dx = Mathf.Max(0f, Mathf.Abs(b.center.x - impact.x) - b.extents.x);
    float dz = Mathf.Max(0f, Mathf.Abs(b.center.z - impact.y) - b.extents.z);
    return dx * dx + dz * dz < radius * radius;
}

var flora = GameObject.Find("Chapter1_Flora").transform;
var bucket = flora.Find("Crushed_LandingSite");
if (bucket == null)
{
    var g = new GameObject("Crushed_LandingSite");
    g.transform.SetParent(flora, false);
    bucket = g.transform;
}

// ── 떠 있는 판: floe.011 / 013 / 014. 콜라이더가 없어 레이로는 안 잡힌다 ──
int floes = 0;
foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).ToArray())
{
    if (!mr.gameObject.name.StartsWith("floe")) continue;
    if (mr.bounds.min.y < OverheadY) continue;
    if (!Overhangs(mr.bounds, PoolRadius + 4f)) continue;
    Debug.Log($"[판] {mr.gameObject.name} (부모 {mr.transform.parent?.name}) 밑면 y={mr.bounds.min.y:0.#} → 끔");
    mr.gameObject.SetActive(false);
    EditorUtility.SetDirty(mr.gameObject);
    floes++;
}
Debug.Log($"[판] {floes}개");

// ── 폭발 ──
var done = new HashSet<Transform>();
int vaporized = 0, felled = 0;

void Fell(Transform t, Bounds b)
{
    float d = Vector2.Distance(new Vector2(t.position.x, t.position.z), impact);
    float k = Mathf.Clamp01(Mathf.InverseLerp(GroundZero, 22f, d));

    var outward = new Vector3(t.position.x - impact.x, 0f, t.position.z - impact.y);
    if (outward.sqrMagnitude < 0.0001f) outward = Vector3.forward;
    outward.Normalize();

    // 바깥으로 눕는 축. a × up = outward 가 되도록 a = (o.z, 0, -o.x)
    var fallAxis = new Vector3(outward.z, 0f, -outward.x);

    // 멀리 선 것도 갓이 웅덩이 위에 있으면 충분히 눕혀야 걷힌다 — 최소 48도
    float angle = Mathf.Lerp(86f, 48f, k) + ((float)rnd.NextDouble() - 0.5f) * 12f;

    float g = GroundAt(t.position.x, t.position.z);
    if (float.IsNaN(g)) g = b.min.y;
    var pivot = new Vector3(t.position.x, g, t.position.z);
    var rot = Quaternion.AngleAxis(angle, fallAxis);
    t.position = pivot + rot * (t.position - pivot);
    t.rotation = rot * t.rotation;
    t.localScale *= Mathf.Lerp(0.72f, 1f, k);
    t.SetParent(bucket, true);
    EditorUtility.SetDirty(t.gameObject);
    done.Add(t);
    felled++;
}

for (int pass = 1; pass <= 5; pass++)
{
    Physics.SyncTransforms();
    var targets = flora.GetComponentsInChildren<MeshRenderer>(false)
        .Where(mr => mr.gameObject.name.Contains("Mushroom"))
        .Where(mr => !done.Contains(mr.transform))
        .Select(mr => new { t = mr.transform, b = mr.bounds })
        .Where(x => x.b.size.y >= MinHeight)
        .Where(x => x.b.max.y > OverheadY)
        .Where(x => Overhangs(x.b, PoolRadius))
        .ToArray();

    Debug.Log($"[{pass}차] 갓이 웅덩이 위로 뻗은 것 {targets.Length}개");
    if (targets.Length == 0) break;

    foreach (var x in targets)
    {
        float d = Vector2.Distance(new Vector2(x.t.position.x, x.t.position.z), impact);
        if (d < GroundZero)
        {
            x.t.gameObject.SetActive(false);
            x.t.SetParent(bucket, true);
            EditorUtility.SetDirty(x.t.gameObject);
            done.Add(x.t); vaporized++;
        }
        else Fell(x.t, x.b);
    }
}

// 폭심 안에 남은 것은 형체가 남지 않는다
Physics.SyncTransforms();
foreach (var mr in flora.GetComponentsInChildren<MeshRenderer>(false).ToArray())
{
    if (!mr.gameObject.name.Contains("Mushroom")) continue;
    if (mr.transform.parent == bucket) continue;
    if (Vector2.Distance(new Vector2(mr.transform.position.x, mr.transform.position.z), impact) >= GroundZero) continue;
    mr.gameObject.SetActive(false);
    mr.transform.SetParent(bucket, true);
    EditorUtility.SetDirty(mr.gameObject);
    vaporized++;
}
Debug.Log($"[충격] 폭심 {vaporized}개 소멸, 바깥으로 {felled}개 쓰러뜨림");

// ── 안착 ──
Physics.SyncTransforms();
int settled = 0;
foreach (var mr in bucket.GetComponentsInChildren<MeshRenderer>(false))
{
    var b = mr.bounds;
    float g = GroundAt(b.center.x, b.center.z);
    if (float.IsNaN(g)) continue;
    float dy = (g - 0.25f) - b.min.y;
    if (Mathf.Abs(dy) < 0.05f) continue;
    mr.transform.position += new Vector3(0f, dy, 0f);
    EditorUtility.SetDirty(mr.gameObject);
    settled++;
}
Physics.SyncTransforms();
Debug.Log($"[안착] {settled}개");

// ── 나머지 복원 ──
spot.intensity = 1200f;
EditorUtility.SetDirty(spot);

var mac = AssetDatabase.LoadAssetAtPath<Material>("Assets/03.Materials/MacroniumSurface.mat");
foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
    if (mr.sharedMaterial != null && mr.sharedMaterial.name == "Wavy")
    { mr.sharedMaterial = mac; EditorUtility.SetDirty(mr); Debug.Log($"[매크로늄] {mr.gameObject.name}"); }

foreach (var b in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
{
    if (b == null || b.GetType().Name != "UnderwaterView") continue;
    var so = new SerializedObject(b);
    var p = so.FindProperty("underwaterFog");
    if (p != null) { p.colorValue = new Color32(0x2C, 0x12, 0x40, 255); so.ApplyModifiedProperties(); EditorUtility.SetDirty(b); }
}

int raised = 0;
foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
{
    if (!l.gameObject.name.StartsWith("GroveLight")) continue;
    float g = GroundAt(l.transform.position.x, l.transform.position.z);
    if (float.IsNaN(g) || l.transform.position.y >= g + 1.4f) continue;
    l.transform.position = new Vector3(l.transform.position.x, g + 1.5f, l.transform.position.z);
    EditorUtility.SetDirty(l.gameObject); raised++;
}
Debug.Log($"[군락등] {raised}개 지면 위로");

// ── 스폰 ──
var player = GameObject.FindWithTag("Player")
    ?? Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None).First().gameObject;
var cc = player.GetComponent<CharacterController>();
var sp = player.transform.position;
Physics.SyncTransforms();
foreach (var c in Physics.OverlapCapsule(sp + Vector3.up * cc.radius, sp + Vector3.up * (cc.height - cc.radius),
                                         cc.radius + 0.3f, ~0, QueryTriggerInteraction.Ignore)
         .Where(c => c.transform.root != player.transform.root))
{
    var root = c.transform;
    while (root.parent != null && root.parent != flora && root.parent != bucket) root = root.parent;
    if (root.name.Contains("Mushroom")) { root.gameObject.SetActive(false); Debug.Log($"[스폰] {root.name} 제거"); }
    else Debug.LogWarning($"[스폰] 버섯 아닌 것이 겹침: {c.name}");
}
Physics.SyncTransforms();
var still = Physics.OverlapCapsule(sp + Vector3.up * cc.radius, sp + Vector3.up * (cc.height - cc.radius),
                                   cc.radius + 0.3f, ~0, QueryTriggerInteraction.Ignore)
    .Where(c => c.transform.root != player.transform.root).ToArray();
Debug.Log($"[스폰] 캡슐 겹침 {still.Length}개");

// ── 결과 ──
int floor = 0, overhead = 0, total = 0;
var left = new Dictionary<string, int>();
for (int ix = -9; ix <= 9; ix++)
for (int iz = -9; iz <= 9; iz++)
{
    if (ix * ix + iz * iz > 81) continue;
    total++;
    if (!Physics.Raycast(new Vector3(o.x + ix, o.y, o.z + iz), Vector3.down, out var h, 300f, ~0, QueryTriggerInteraction.Ignore)) continue;
    if (h.collider.name.StartsWith("Island")) floor++;
    else if (h.point.y > 55f) { overhead++; left.TryGetValue(h.collider.name, out int c); left[h.collider.name] = c + 1; }
}
Debug.Log($"[결과] {total}점 — 섬 바닥 {floor} / 머리 위 차폐 {overhead}");
foreach (var kv in left.OrderByDescending(k => k.Value).Take(6)) Debug.Log($"   {kv.Value,3}점 {kv.Key}");

EditorSceneManager.MarkSceneDirty(player.scene);
EditorSceneManager.SaveScene(player.scene);
