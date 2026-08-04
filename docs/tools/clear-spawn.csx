using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 부순 버섯이 스폰을 덮어 플레이어가 낀다.
// 스폰 둘레를 비운다 — 눕힌 것들을 바깥으로 밀어낸다.
// Physics.SyncTransforms를 매번 부른다. 안 부르면 옛 위치를 본다(한 번 당했다).

const float ClearRadius = 4.0f;

var player = GameObject.FindWithTag("Player")
    ?? Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None).First().gameObject;
var cc = player.GetComponent<CharacterController>();
var sp = player.transform.position;
var axis = new Vector2(sp.x, sp.z);
Debug.Log($"[스폰] {sp}");

var flora = GameObject.Find("Chapter1_Flora").transform;

IEnumerable<Transform> Near()
{
    Physics.SyncTransforms();
    return flora.GetComponentsInChildren<Renderer>(false)
        .Select(r => r.transform)
        .Distinct()
        .Where(t => {
            var b = t.GetComponent<Renderer>().bounds;
            // 바운즈가 스폰 원기둥과 겹치는가
            float dx = Mathf.Max(0f, Mathf.Abs(b.center.x - sp.x) - b.extents.x);
            float dz = Mathf.Max(0f, Mathf.Abs(b.center.z - sp.z) - b.extents.z);
            return dx * dx + dz * dz < ClearRadius * ClearRadius && b.min.y < sp.y + 3f;
        });
}

int moved = 0;
foreach (var t in Near().ToArray())
{
    var b = t.GetComponent<Renderer>().bounds;
    var outward = new Vector3(b.center.x - sp.x, 0f, b.center.z - sp.z);
    if (outward.sqrMagnitude < 0.01f)
        outward = Quaternion.Euler(0f, moved * 47f, 0f) * Vector3.forward;   // 정확히 겹친 것은 흩어 보낸다
    outward.Normalize();

    // 바운즈 반지름 + 여유만큼 바깥으로
    float span = Mathf.Max(b.extents.x, b.extents.z);
    float cur = Vector2.Distance(new Vector2(b.center.x, b.center.z), axis);
    float need = ClearRadius + span + 0.5f - cur;
    if (need <= 0f) continue;

    t.position += outward * need;
    EditorUtility.SetDirty(t.gameObject);
    moved++;
}
Physics.SyncTransforms();
Debug.Log($"[정리] {moved}개를 스폰 밖으로 밀어냄 (확보 반경 {ClearRadius}m)");

// 확인: 플레이어 캡슐에 걸리는 것이 남았는가
float r = cc.radius, h = cc.height;
var hits = Physics.OverlapCapsule(sp + Vector3.up * r, sp + Vector3.up * (h - r), r + 0.3f, ~0, QueryTriggerInteraction.Ignore)
    .Where(c => c.transform.root != player.transform.root).ToArray();
Debug.Log($"[확인] 캡슐 겹침 {hits.Length}개" + (hits.Length == 0 ? " — 깨끗함" : ": " + string.Join(", ", hits.Select(c => c.name))));

// 스폰에서 여덟 방향으로 걸어나갈 수 있는가
int blocked = 0;
for (int i = 0; i < 8; i++)
{
    var dir = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
    if (Physics.Raycast(sp + Vector3.up * 1.0f, dir, out var hh, 3f, ~0, QueryTriggerInteraction.Ignore))
    { blocked++; Debug.Log($"   {i * 45}도 → {hh.collider.name} {hh.distance:0.#}m"); }
}
Debug.Log($"[통로] 여덟 방향 중 {8 - blocked}개가 3m까지 열려 있다");

EditorSceneManager.MarkSceneDirty(player.scene);
EditorSceneManager.SaveScene(player.scene);
