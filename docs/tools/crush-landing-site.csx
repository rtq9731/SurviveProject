using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 착지 지점의 버섯을 부순다 — 빛기둥 웅덩이를 바닥에 떨구기 위해.
//
// 두 번 당했다.
//  1) 바운즈 '중심'으로 거리를 재면 갓만 뻗친 거대버섯이 전부 샌다 → 레이가 맞힌 것을 쓴다
//  2) 트랜스폼을 바꿔도 Physics.SyncTransforms 없이는 다음 레이가 옛 위치를 본다
//     → 같은 것을 8번 눌러 0.017배로 만들었다. 매 패스마다 반드시 동기화한다

const float PoolRadius = 9f;
const int MaxPasses = 10;
var rnd = new System.Random(4242);

var spot = GameObject.Find("LightShaft").GetComponentInChildren<Light>(true);
var o = spot.transform.position;

var flora = GameObject.Find("Chapter1_Flora");
var bucket = flora.transform.Find("Crushed_LandingSite");
if (bucket == null)
{
    var go = new GameObject("Crushed_LandingSite");
    go.transform.SetParent(flora.transform, false);
    bucket = go.transform;
}

var done = new HashSet<Transform>();     // 두 번 누르지 않는다

List<RaycastHit> Sample()
{
    Physics.SyncTransforms();            // ← 이것이 빠져서 8번 눌렀다
    var list = new List<RaycastHit>();
    for (int ix = -9; ix <= 9; ix++)
    for (int iz = -9; iz <= 9; iz++)
    {
        if (ix * ix + iz * iz > PoolRadius * PoolRadius) continue;
        if (Physics.Raycast(new Vector3(o.x + ix, o.y, o.z + iz), Vector3.down, out var h, 300f, ~0, QueryTriggerInteraction.Ignore))
            list.Add(h);
    }
    return list;
}

void Crush(Transform t)
{
    var r = t.GetComponentInChildren<Renderer>();
    float h = r != null ? r.bounds.size.y : 6f;
    var outward = new Vector3(t.position.x - o.x, 0f, t.position.z - o.z);
    if (outward.sqrMagnitude < 0.01f) outward = Vector3.forward;
    outward.Normalize();

    // 바깥으로 넘어뜨린다 — 무언가 떨어져 밀어낸 방향
    t.rotation = Quaternion.AngleAxis(76f + (float)rnd.NextDouble() * 26f,
                                      new Vector3(-outward.z, 0f, outward.x)) * t.rotation;
    t.localScale *= 0.55f + (float)rnd.NextDouble() * 0.25f;
    t.position -= new Vector3(0f, h * 0.15f, 0f);
    t.position += outward * (float)(rnd.NextDouble() * 2.0);
    t.SetParent(bucket, true);
    EditorUtility.SetDirty(t.gameObject);
}

var kept = new HashSet<string>();
int total = 0;

for (int pass = 1; pass <= MaxPasses; pass++)
{
    var s = Sample();
    int onFloor = s.Count(h => h.collider.name.StartsWith("Island"));
    var blocked = s.Where(h => h.point.y > 55f && !h.collider.name.StartsWith("Island")).ToList();

    var targets = blocked.Select(h => h.collider.transform)
        .Where(t => t.name.Contains("Mushroom") && !done.Contains(t))
        .Distinct().ToList();

    foreach (var h in blocked.Where(h => !h.collider.name.Contains("Mushroom")))
        kept.Add(h.collider.name);

    Debug.Log($"[{pass}차] {s.Count}점 중 섬 바닥 {onFloor} / 머리 위 차폐 {blocked.Count} → 새로 부술 것 {targets.Count}개");
    if (targets.Count == 0) break;

    foreach (var t in targets) { Crush(t); done.Add(t); }
    total += targets.Count;
}

var fin = Sample();
Debug.Log($"[결과] 버섯 {total}개를 부숨. 최종 {fin.Count}점 중 " +
          $"섬 바닥 {fin.Count(h => h.collider.name.StartsWith("Island"))} / " +
          $"머리 위 차폐 {fin.Count(h => h.point.y > 55f && !h.collider.name.StartsWith("Island"))}");
if (kept.Count > 0) Debug.Log("[버섯 아닌 차폐, 그대로 둠] " + string.Join(", ", kept));

// 40m 거리에서 세기 34는 감쇠로 0.017밖에 안 남는다
spot.intensity = 1200f;
EditorUtility.SetDirty(spot);
Debug.Log("[빛기둥] 스폿 세기 34 → 1200");

EditorSceneManager.MarkSceneDirty(spot.gameObject.scene);
EditorSceneManager.SaveScene(spot.gameObject.scene);
