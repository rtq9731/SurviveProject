using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 부순 조각을 바닥에 앉힌다.
// 모델마다 피벗이 달라서 "회전 후 h*0.15 내리기"로는 맞지 않는다 —
// 렌더러 바운즈 밑면을 섬 표면에 맞추는 것이 유일하게 옳은 기준이다.

var bucket = GameObject.Find("Chapter1_Flora").transform.Find("Crushed_LandingSite");
Physics.SyncTransforms();

int settled = 0;
float worst = 0f;
foreach (var r in bucket.GetComponentsInChildren<Renderer>(false).ToArray())
{
    var t = r.transform;
    var b = r.bounds;

    // 위에서 쏴서 '섬'만 잡는다 — 다른 잔해에 얹히면 계속 떠오른다
    var hits = Physics.RaycastAll(new Vector3(b.center.x, 200f, b.center.z), Vector3.down, 300f, ~0, QueryTriggerInteraction.Ignore)
        .Where(h => h.collider.name.StartsWith("Island"))
        .OrderByDescending(h => h.point.y).ToArray();
    if (hits.Length == 0) { Debug.LogWarning($"[앉히기] {r.name} 아래에 섬이 없다"); continue; }

    float ground = hits[0].point.y;
    float target = ground - 0.35f;               // 조금 파묻히게 — 짓눌린 느낌
    float dy = target - b.min.y;
    if (Mathf.Abs(dy) < 0.05f) continue;

    t.position += new Vector3(0f, dy, 0f);
    EditorUtility.SetDirty(t.gameObject);
    settled++;
    worst = Mathf.Max(worst, Mathf.Abs(dy));
}
Physics.SyncTransforms();
Debug.Log($"[앉히기] {settled}개 이동, 최대 보정 {worst:0.#}m");

// 다시 검사
int floating = 0, sunk = 0;
foreach (var r in bucket.GetComponentsInChildren<Renderer>(false))
{
    var b = r.bounds;
    var hits = Physics.RaycastAll(new Vector3(b.center.x, 200f, b.center.z), Vector3.down, 300f, ~0, QueryTriggerInteraction.Ignore)
        .Where(h => h.collider.name.StartsWith("Island")).OrderByDescending(h => h.point.y).ToArray();
    if (hits.Length == 0) continue;
    float gap = b.min.y - hits[0].point.y;
    if (gap > 1.0f) floating++;
    if (gap < -2.5f) sunk++;
}
Debug.Log($"[검사] 뜬 것 {floating}개, 깊이 파묻힌 것 {sunk}개");

// 플레이어 스폰이 여전히 깨끗한지
var player = GameObject.FindWithTag("Player")
    ?? Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None).First().gameObject;
var cc = player.GetComponent<CharacterController>();
var sp = player.transform.position;
var over = Physics.OverlapCapsule(sp + Vector3.up * cc.radius, sp + Vector3.up * (cc.height - cc.radius),
                                  cc.radius + 0.3f, ~0, QueryTriggerInteraction.Ignore)
    .Where(c => c.transform.root != player.transform.root).ToArray();
Debug.Log($"[스폰] 캡슐 겹침 {over.Length}개" + (over.Length == 0 ? " — 깨끗함" : ": " + string.Join(", ", over.Select(c => c.name))));

EditorSceneManager.MarkSceneDirty(player.scene);
EditorSceneManager.SaveScene(player.scene);
