using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 섬 사이를 스펙(§3)대로 벌린다.
//
// 왜 붙었나: 중심 거리를 '설정 반경'(50/44/40/36)으로 계산해 22/18/30m를 맞췄는데,
// 그 뒤 해안을 반경×1.22까지 늘리면서 간격을 다시 계산하지 않았다.
// 실제 가장자리가 61/54/49/44가 되어 틈이 0.8 / -1 / 12.7m로 먹혔다.
//
// 이번엔 '실측 반경'으로 계산한다.

var order = new[] { "Island1_Landing", "Island2_Shoal", "Island3_Far", "Island4_Surface" };
var targetGap = new[] { 22f, 18f, 30f };

const string prefabPath = "Assets/05.Prefabs/Environment/Archipelago.prefab";
var root = PrefabUtility.LoadPrefabContents(prefabPath);

var nodes = order.Select(n => root.transform.Find(n)).ToArray();
if (nodes.Any(n => n == null)) { Debug.LogError("[재배치] 프리팹에서 섬을 못 찾았다"); PrefabUtility.UnloadPrefabContents(root); return null; }

var oldPos = nodes.Select(n => new Vector2(n.localPosition.x, n.localPosition.z)).ToArray();
var radius = nodes.Select(n => {
    var b = n.GetComponent<MeshFilter>().sharedMesh.bounds;
    return Mathf.Max(b.extents.x, b.extents.z);
}).ToArray();

// 방향은 지금 것을 그대로 쓰고 거리만 다시 잡는다
var newPos = new Vector2[order.Length];
newPos[0] = oldPos[0];
for (int i = 0; i < order.Length - 1; i++)
{
    var dir = (oldPos[i + 1] - oldPos[i]).normalized;
    float need = radius[i] + radius[i + 1] + targetGap[i];
    newPos[i + 1] = newPos[i] + dir * need;
    Debug.Log($"[재배치] {order[i]} → {order[i + 1]}: 실반경 {radius[i]:0.#}+{radius[i + 1]:0.#}, " +
              $"목표 틈 {targetGap[i]}m → 중심거리 {Vector2.Distance(oldPos[i], oldPos[i + 1]):0.#} → {need:0.#}");
}

var delta = new Vector3[order.Length];
for (int i = 0; i < order.Length; i++)
{
    var d2 = newPos[i] - oldPos[i];
    delta[i] = new Vector3(d2.x, 0f, d2.y);
    nodes[i].localPosition = new Vector3(newPos[i].x, nodes[i].localPosition.y, newPos[i].y);
    Debug.Log($"[재배치] {order[i]} 이동 {delta[i]:0.#} → 새 위치 {nodes[i].localPosition:0.#}");
}

PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
PrefabUtility.UnloadPrefabContents(root);
Debug.Log("[재배치] 프리팹 저장");

// ── 콘텐츠를 함께 옮긴다 ──
// 옛 섬 중심을 기준으로 소속을 정한다. 어떤 덩어리 안의 렌더러가 전부 같은 섬이면
// 그 덩어리를 통째로 옮기고, 섞여 있으면 더 내려간다 — 그래야 그룹 구조가 안 깨진다.

int Owner(Vector3 p)
{
    int best = 0; float bestD = float.MaxValue;
    for (int i = 0; i < oldPos.Length; i++)
    {
        float d = Vector2.Distance(new Vector2(p.x, p.z), oldPos[i]);
        if (d < bestD) { bestD = d; best = i; }
    }
    return best;
}

int movedNodes = 0, movedRenderers = 0;

void Walk(Transform t)
{
    var rs = t.GetComponentsInChildren<Renderer>(true);
    if (rs.Length == 0) return;

    var owners = rs.Select(r => Owner(r.bounds.center)).Distinct().ToArray();
    if (owners.Length == 1)
    {
        int o = owners[0];
        if (delta[o] != Vector3.zero)
        {
            t.position += delta[o];
            EditorUtility.SetDirty(t.gameObject);
            movedNodes++; movedRenderers += rs.Length;
        }
        return;
    }
    // 섞여 있으면 자식으로 내려간다
    foreach (Transform c in t) Walk(c);
}

foreach (var groupName in new[] { "Chapter1_Flora", "Chapter1_Content", "CaveShell" })
{
    var g = GameObject.Find(groupName);
    if (g == null) { Debug.LogWarning($"[재배치] {groupName} 없음"); continue; }
    if (groupName == "CaveShell") continue;              // 원경이라 옮기지 않는다
    foreach (Transform c in g.transform) Walk(c);
}
Debug.Log($"[재배치] 콘텐츠 {movedNodes}덩어리 / 렌더러 {movedRenderers}개 이동");

// 광원도 같이 (렌더러 없는 라이트 단독 오브젝트)
int movedLights = 0;
foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
{
    if (l.type == LightType.Directional) continue;
    if (l.GetComponentInChildren<Renderer>(true) != null) continue;   // 이미 따라 옮겨졌다
    if (l.GetComponentInParent<Renderer>() != null) continue;
    int o = Owner(l.transform.position);
    if (delta[o] == Vector3.zero) continue;
    l.transform.position += delta[o];
    EditorUtility.SetDirty(l.gameObject);
    movedLights++;
}
Debug.Log($"[재배치] 단독 광원 {movedLights}개 이동");

Physics.SyncTransforms();

// ── 검증 ──
foreach (var n in order)
{
    var mc = GameObject.Find(n)?.GetComponent<MeshCollider>();
    if (mc != null) Debug.Log($"[섬] {n} 중심 XZ=({mc.bounds.center.x:0.#}, {mc.bounds.center.z:0.#}) 실반경 {Mathf.Max(mc.bounds.extents.x, mc.bounds.extents.z):0.#}");
}
for (int i = 0; i < order.Length - 1; i++)
{
    var a = GameObject.Find(order[i]).GetComponent<MeshCollider>().bounds;
    var b = GameObject.Find(order[i + 1]).GetComponent<MeshCollider>().bounds;
    float d = Vector2.Distance(new Vector2(a.center.x, a.center.z), new Vector2(b.center.x, b.center.z));
    float gap = d - Mathf.Max(a.extents.x, a.extents.z) - Mathf.Max(b.extents.x, b.extents.z);
    Debug.Log($"[간격] {order[i]} → {order[i + 1]}: {gap:0.#}m (목표 {targetGap[i]}m)");
}

// 걸어서 건널 수 있는지
const float WaterY = 50.1f;
for (int i = 0; i < order.Length - 1; i++)
{
    var a = GameObject.Find(order[i]).transform.position;
    var b = GameObject.Find(order[i + 1]).transform.position;
    int dry = 0, wet = 0, none = 0;
    for (int s = 0; s <= 160; s++)
    {
        var p = Vector3.Lerp(a, b, s / 160f);
        if (!Physics.Raycast(new Vector3(p.x, 200f, p.z), Vector3.down, out var h, 400f, ~0, QueryTriggerInteraction.Ignore)) { none++; continue; }
        if (h.point.y > WaterY) dry++; else wet++;
    }
    Debug.Log($"[건널 수 있나] {order[i]} → {order[i + 1]}: 물 위 {dry} / 물 아래 {wet} / 없음 {none}");
}

EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
