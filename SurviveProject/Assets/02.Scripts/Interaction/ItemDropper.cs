using UnityEngine;
using DG.Tweening;
using Survive.Items;

namespace Survive.Interaction
{
    /// <summary>
    /// 아이템을 세계에 떨군다.
    ///
    /// 부순 것이 곧바로 인벤토리로 빨려 들어가면 부쉈다는 느낌이 안 난다.
    /// 튀어나와 바닥에 구르고, 플레이어가 가서 줍는다 — 그 사이가 보상의 리듬이다.
    ///
    /// 생물 사망과 채집물 파괴가 같은 방식으로 떨궈야 해서 여기 모았다.
    /// </summary>
    public static class ItemDropper
    {
        /// <summary>
        /// <paramref name="origin"/>에서 아이템 하나를 튀어나오게 떨군다.
        /// </summary>
        /// <param name="prefab">
        /// 떨구는 쪽이 지정한 겉모습. 아이템 자신의 worldPrefab이 우선한다 —
        /// 한 번에 여러 종류를 떨구는 경우 떨구는 쪽의 프리팹 하나로는 맞출 수 없다.
        /// 둘 다 없으면 임시 큐브를 쓴다.
        /// </param>
        public static GameObject Drop(ItemDataSO item, int count, Vector3 origin,
                                      GameObject prefab = null, float spread = 0.9f)
        {
            if (item == null || count <= 0) return null;

            var visual = item.worldPrefab != null ? item.worldPrefab : prefab;

            GameObject go;
            if (visual != null)
            {
                go = Object.Instantiate(visual, origin, Random.rotation);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.localScale = Vector3.one * 0.28f;
                go.transform.position = origin;
                go.transform.rotation = Random.rotation;
                MakeVisible(go, item);
            }

            go.name = "Drop_" + item.id;

            // 조준용 트리거. 작은 물체는 이게 없으면 상호작용이 잡히지 않는다.
            var col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
            else go.AddComponent<SphereCollider>().isTrigger = true;

            var pickup = go.GetComponent<ItemPickup>();
            if (pickup == null) pickup = go.AddComponent<ItemPickup>();
            pickup.Setup(item, count);

            Scatter(go.transform, origin, spread);
            return go;
        }

        /// <summary>
        /// 임시 큐브를 아이템처럼 보이게 만든다.
        ///
        /// 지하는 어둡다. 회색 큐브는 바닥에 묻혀서 떨어진 줄도 모른다 —
        /// 스스로 빛나고 천천히 흔들려야 "저기 뭔가 떨어졌다"가 읽힌다.
        /// 제대로 된 프리팹이 생기면 이 경로는 안 쓰인다.
        /// </summary>
        static void MakeVisible(GameObject go, ItemDataSO item)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;

            var tint = TintFor(item);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return;

            // 발광을 세게 주면 흰 덩어리로 뭉개져 무엇인지 알 수 없다.
            // 어두운 데서 눈에 띌 만큼만 올리고 색은 남긴다.
            var mat = new Material(shader) { color = tint };
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", tint * 0.5f);
            rend.sharedMaterial = mat;
        }

        /// <summary>종류마다 색을 달리해 멀리서도 무엇인지 짐작할 수 있게 한다.</summary>
        static Color TintFor(ItemDataSO item) => item.id switch
        {
            "scrap" => new Color(0.95f, 0.72f, 0.35f),
            "machine_part" => new Color(0.60f, 0.80f, 0.95f),
            "alien_alloy" => new Color(0.55f, 0.95f, 0.85f),
            _ => new Color(0.85f, 0.85f, 0.70f),
        };

        /// <summary>
        /// 옆으로 튀어 바닥에 내려앉는 궤적. 물리를 붙이면 굴러가 버려서
        /// 어디로 갔는지 놓치기 쉽다. 착지 지점을 정해 두고 트윈으로 보낸다.
        /// </summary>
        static void Scatter(Transform t, Vector3 origin, float spread)
        {
            var dir = Random.insideUnitCircle.normalized * Random.Range(spread * 0.4f, spread);
            var target = origin + new Vector3(dir.x, 0f, dir.y);

            // 착지면을 찾는다. 못 찾으면 원래 높이에 둔다.
            if (Physics.Raycast(target + Vector3.up * 3f, Vector3.down, out var hit, 12f,
                                ~0, QueryTriggerInteraction.Ignore))
                target.y = hit.point.y + 0.18f;
            else
                target.y = origin.y;

            t.position = origin + Vector3.up * 0.35f;
            t.DOJump(target, 0.55f, 1, 0.5f).SetEase(Ease.OutQuad)
             .OnComplete(() => Idle(t, target));
            t.DORotate(new Vector3(0f, Random.Range(180f, 540f), 0f), 0.5f, RotateMode.LocalAxisAdd);
        }

        /// <summary>착지 후 계속 떠다니며 돈다. 움직이는 것에 눈이 간다.</summary>
        static void Idle(Transform t, Vector3 restAt)
        {
            if (t == null) return;

            t.DOMoveY(restAt.y + 0.14f, 1.1f)
             .SetLoops(-1, LoopType.Yoyo)
             .SetEase(Ease.InOutSine)
             .SetLink(t.gameObject);

            t.DORotate(new Vector3(0f, 360f, 0f), 3.5f, RotateMode.LocalAxisAdd)
             .SetLoops(-1, LoopType.Restart)
             .SetEase(Ease.Linear)
             .SetLink(t.gameObject);
        }
    }
}
