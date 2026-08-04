using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 착지 지점을 '한 점에서 터져나간 자국'으로 만든다.
//
// 앞선 시도가 별로였던 이유: 넘어지는 방향을 랜덤 yaw로 굴린 뒤,
// 스폰을 비운다고 또 바깥으로 밀었다. 두 번 흩어서 충격파가 아니라 난장판이 됐다.
//
// 이번 규칙은 하나다 — 모든 것이 충돌점에서 '바깥으로' 눕는다.
//   · 중심에 가까울수록 더 납작하게 눕고 더 눌린다
//   · 밑동은 제자리에 둔다(밑동을 축으로 회전). 나무가 넘어지듯이
//   · 정중앙은 아예 박살난 것으로 본다 — 그래서 스폰이 저절로 비워진다
//   · 무작위는 각도에 ±7도만. 방향은 절대 굴리지 않는다

const float BlastRadius = 12f;      // 이 밖은 건드리지 않는다
const float GroundZero  = 3.2f;     // 이 안은 형체가 남지 않는다
const float MinHeight   = 3f;       // 이보다 낮은 것은 원래 빛을 안 막는다
var rnd = new System.Random(90125);

var spot = GameObject.Find("LightShaft").GetComponentInChildren<Light>(true);
var impact = new Vector2(spot.transform.position.x, spot.transform.position.z);

float GroundAt(float x, float z)
{
    var hits = Physics.RaycastAll(new Vector3(x, 200f, z), Vector3.down, 300f, ~0, QueryTriggerInteraction.Ignore)
        .Where(h => h.collider.name.StartsWith("Island")).OrderByDescending(h => h.point.y).ToArray();
    return hits.Length > 0 ? hits[0].point.y : float.NaN;
}

var flora = GameObject.Find("Chapter1_Flora").transform;
var bucket = flora.Find("Crushed_LandingSite");
if (bucket == null)
{
    var g = new GameObject("Crushed_LandingSite");
    g.transform.SetParent(flora, false);
    bucket = g.transform;
}

Physics.SyncTransforms();

var targets = flora.GetComponentsInChildren<MeshRenderer>(false)
    .Where(mr => mr.gameObject.name.Contains("Mushroom"))
    .Where(mr => mr.transform.parent != bucket)
    .Select(mr => new { t = mr.transform, mr, b = mr.bounds })
    .Where(x => x.b.size.y >= MinHeight)
    .Select(x => new { x.t, x.mr, x.b, d = Vector2.Distance(new Vector2(x.t.position.x, x.t.position.z), impact) })
    .Where(x => x.d <= BlastRadius)
    .OrderBy(x => x.d)
    .ToList();

int vaporized = 0, felled = 0;

foreach (var x in targets)
{
    // 폭심 — 형체가 남지 않는다
    if (x.d < GroundZero)
    {
        x.t.gameObject.SetActive(false);
        x.t.SetParent(bucket, true);
        EditorUtility.SetDirty(x.t.gameObject);
        vaporized++;
        continue;
    }

    float k = Mathf.InverseLerp(GroundZero, BlastRadius, x.d);   // 0=가깝다, 1=가장자리

    // 바깥 방향. 이것만이 방향을 정한다
    var outward = new Vector3(x.t.position.x - impact.x, 0f, x.t.position.z - impact.y);
    if (outward.sqrMagnitude < 0.0001f) outward = Vector3.forward;
    outward.Normalize();
    var fallAxis = new Vector3(-outward.z, 0f, outward.x);       // 바깥으로 눕는 회전축

    // 가까울수록 납작하게. 무작위는 각도에만 ±7도
    float angle = Mathf.Lerp(86f, 26f, k) + ((float)rnd.NextDouble() - 0.5f) * 14f;

    // 밑동을 축으로 회전한다 — 밑동은 제자리에 남는다
    float g = GroundAt(x.t.position.x, x.t.position.z);
    if (float.IsNaN(g)) g = x.b.min.y;
    var pivot = new Vector3(x.t.position.x, g, x.t.position.z);
    var rot = Quaternion.AngleAxis(angle, fallAxis);
    x.t.position = pivot + rot * (x.t.position - pivot);
    x.t.rotation = rot * x.t.rotation;

    // 가까울수록 더 눌린다
    x.t.localScale *= Mathf.Lerp(0.72f, 1f, k);

    x.t.SetParent(bucket, true);
    EditorUtility.SetDirty(x.t.gameObject);
    felled++;
}

Physics.SyncTransforms();
Debug.Log($"[충격] 폭심 {GroundZero}m 안 {vaporized}개 소멸, 바깥으로 {felled}개 쓰러뜨림 (반경 {BlastRadius}m)");

// 쓰러진 것을 바닥에 앉힌다 — 피벗이 모델마다 달라 회전만으로는 안 붙는다
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
Debug.Log($"[안착] {settled}개를 바닥에 앉힘");

// 스폰 확인 — 폭심을 비웠으니 깨끗해야 한다
var player = GameObject.FindWithTag("Player")
    ?? Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None).First().gameObject;
var cc = player.GetComponent<CharacterController>();
var sp = player.transform.position;
var over = Physics.OverlapCapsule(sp + Vector3.up * cc.radius, sp + Vector3.up * (cc.height - cc.radius),
                                  cc.radius + 0.3f, ~0, QueryTriggerInteraction.Ignore)
    .Where(c => c.transform.root != player.transform.root).ToArray();
foreach (var c in over)
{
    // 그래도 남은 것이 있으면 그것도 박살난 것으로 친다
    var root = c.transform;
    while (root.parent != null && root.parent != flora && root.parent != bucket) root = root.parent;
    if (root.name.Contains("Mushroom")) { root.gameObject.SetActive(false); Debug.Log($"[스폰] {root.name} 추가 제거"); }
    else Debug.LogWarning($"[스폰] 버섯이 아닌 것이 겹친다: {c.name}");
}
Physics.SyncTransforms();
var still = Physics.OverlapCapsule(sp + Vector3.up * cc.radius, sp + Vector3.up * (cc.height - cc.radius),
                                   cc.radius + 0.3f, ~0, QueryTriggerInteraction.Ignore)
    .Where(c => c.transform.root != player.transform.root).ToArray();
Debug.Log($"[스폰] 캡슐 겹침 {still.Length}개" + (still.Length == 0 ? " — 깨끗함" : ": " + string.Join(", ", still.Select(c => c.name))));

// 빛기둥 세기 — 40m 거리에서 34는 감쇠로 0.017밖에 안 남는다
spot.intensity = 1200f;
EditorUtility.SetDirty(spot);

// 착지 지점 14m 위에 떠서 빛을 가로채던 판
var floe = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
    .FirstOrDefault(t => t.name == "floe.014");
if (floe != null) { floe.gameObject.SetActive(false); EditorUtility.SetDirty(floe.gameObject); Debug.Log("[floe] floe.014 끔"); }

// 섬 안에 묻혀 있던 군락등
int raised = 0;
foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
{
    if (!l.gameObject.name.StartsWith("GroveLight")) continue;
    float g = GroundAt(l.transform.position.x, l.transform.position.z);
    if (float.IsNaN(g) || l.transform.position.y >= g + 1.4f) continue;
    l.transform.position = new Vector3(l.transform.position.x, g + 1.5f, l.transform.position.z);
    EditorUtility.SetDirty(l.gameObject);
    raised++;
}
Debug.Log($"[군락등] {raised}개 지면 위로");

EditorSceneManager.MarkSceneDirty(player.scene);
EditorSceneManager.SaveScene(player.scene);
