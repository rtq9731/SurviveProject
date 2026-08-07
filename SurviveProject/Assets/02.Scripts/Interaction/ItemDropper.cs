using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
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
        /// 둘 다 없으면 아이템 아이콘을 세운다 (<see cref="DropVisualRule"/>).
        /// </param>
        /// <param name="occasion">
        /// 같은 자리에서 <b>몇 번째</b> 떨굼인가. 착지 지점은 세계 시드에서
        /// 파생하므로(<see cref="Survive.World.WorldSeed"/>), 한 번에 여럿을
        /// 떨구면서 같은 번호를 주면 <b>전부 한 점에 겹쳐 떨어진다</b>.
        /// 떨구는 쪽이 자기 묶음 안의 순번을 준다.
        /// </param>
        public static GameObject Drop(ItemDataSO item, int count, Vector3 origin,
                                      GameObject prefab = null, float spread = 0.9f,
                                      int occasion = 0)
        {
            if (item == null || count <= 0) return null;

            GameObject go;
            switch (DropVisualRule.Choose(item, prefab != null))
            {
                case DropVisualKind.WorldPrefab:
                    go = Object.Instantiate(item.worldPrefab, origin, Random.rotation);
                    break;

                case DropVisualKind.SpawnerPrefab:
                    go = Object.Instantiate(prefab, origin, Random.rotation);
                    break;

                case DropVisualKind.IconBillboard:
                    go = BuildIconBillboard(item, origin);
                    break;

                default:
                    go = BuildFallbackBlock(origin);
                    break;
            }

            go.name = "Drop_" + item.id;

            // 조준용 트리거. 작은 물체는 이게 없으면 상호작용이 잡히지 않는다.
            var col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
            else go.AddComponent<SphereCollider>().isTrigger = true;

            var pickup = go.GetComponent<ItemPickup>();
            if (pickup == null) pickup = go.AddComponent<ItemPickup>();
            pickup.Setup(item, count);

            Scatter(go.transform, origin, spread, occasion);
            return go;
        }

        // ── 겉모습 ───────────────────────────────────────────────

        /// <summary>
        /// 아이콘을 세운 판. 인벤토리에서 보던 그림이 바닥에도 그대로 있으면
        /// 멀리서 한눈에 무엇인지 읽힌다 — 색만 다른 큐브로는 절대 안 되던 일이다.
        ///
        /// 판은 자식으로 둔다. 뿌리는 <see cref="Scatter"/>·<see cref="Idle"/>의
        /// 트윈이 돌리고, 판은 <see cref="DropBillboard"/>가 카메라로 되돌린다.
        /// </summary>
        static GameObject BuildIconBillboard(ItemDataSO item, Vector3 origin)
        {
            var root = new GameObject();
            root.transform.position = origin;

            // 조준 판정은 뿌리가 맡는다. 판에 콜라이더를 두면 옆에서 겨눌 때 놓친다.
            var sphere = root.AddComponent<SphereCollider>();
            sphere.radius = 0.22f;
            sphere.isTrigger = true;

            var plate = new GameObject("Icon");
            plate.transform.SetParent(root.transform, false);
            plate.transform.localScale = Vector3.one * DropVisualRule.IconSize;
            plate.AddComponent<DropBillboard>();

            plate.AddComponent<MeshFilter>().sharedMesh = PlateMesh();

            var rend = plate.AddComponent<MeshRenderer>();
            rend.sharedMaterial = IconMaterial(item.icon);
            // 손톱만 한 판이 그림자까지 드리우면 어두운 바닥이 지저분해지기만 한다.
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;

            return root;
        }

        /// <summary>
        /// 아이콘조차 없을 때. 무엇인지는 알 수 없어도 "뭔가 떨어졌다"는 보여야 한다.
        /// 데이터가 온전하면 여기로 오는 아이템은 하나도 없다 (DropVisualTests).
        /// </summary>
        static GameObject BuildFallbackBlock(Vector3 origin)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.localScale = Vector3.one * 0.28f;
            go.transform.position = origin;
            go.transform.rotation = Random.rotation;

            var rend = go.GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = FallbackMaterial();
            return go;
        }

        // ── 머티리얼·메시 캐시 ───────────────────────────────────
        //
        // 예전에는 드롭마다 new Material을 만들어 어디서도 해제하지 않았다.
        // 사망 드롭·채집·낫의 유물 드롭이 겹치는 긴 판에서는 그만큼 계속 쌓인다.
        // 같은 아이콘은 같은 머티리얼을 쓰므로 아이콘 수(스물몇)로 상한이 잡힌다.

        static readonly Dictionary<Sprite, Material> IconMaterials = new Dictionary<Sprite, Material>();
        static Material _fallbackMaterial;
        static Mesh _plateMesh;

        /// <summary>
        /// 플레이 세션이 바뀌면 캐시가 가리키던 것은 이미 파괴돼 있다.
        /// 도메인 리로드를 꺼 둔 설정에서도 확실히 비우려고 진입점에서 초기화한다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()
        {
            IconMaterials.Clear();
            _fallbackMaterial = null;
            _plateMesh = null;
        }

        static Shader DropShader()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            return shader;
        }

        static Material IconMaterial(Sprite icon)
        {
            if (icon == null) return FallbackMaterial();
            if (IconMaterials.TryGetValue(icon, out var cached) && cached != null) return cached;

            var shader = DropShader();
            if (shader == null) return null;

            var mat = new Material(shader) { name = "DropIcon_" + icon.name };
            mat.color = Color.white;

            var tex = icon.texture;
            if (tex != null)
            {
                mat.mainTexture = tex;
                // 아틀라스에 묶인 스프라이트는 텍스처 일부만 쓴다. 잘라 쓸 자리를 UV로 넘긴다.
                var r = icon.textureRect;
                mat.mainTextureScale = new Vector2(r.width / tex.width, r.height / tex.height);
                mat.mainTextureOffset = new Vector2(r.x / tex.width, r.y / tex.height);
            }

            // 아이콘 배경은 투명하다. 잘라내지 않으면 검은 판이 뜬다.
            mat.SetFloat("_Surface", 0f);          // Opaque
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)RenderQueue.AlphaTest;

            // 판이 뒤집혀도 사라지지 않게. 빌보드가 앞을 보게 하지만 한 프레임의 틈이 있다.
            mat.SetFloat("_Cull", 0f);

            mat.SetFloat("_Smoothness", 0.1f);
            mat.SetFloat("_Metallic", 0f);

            // 발광에도 같은 그림을 물린다. 이래야 어둠 속에서 흰 사각형이 아니라
            // 아이콘의 형태와 색이 떠오른다.
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", tex);
            mat.SetColor("_EmissionColor", Color.white * DropVisualRule.IconEmission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;

            IconMaterials[icon] = mat;
            return mat;
        }

        static Material FallbackMaterial()
        {
            if (_fallbackMaterial != null) return _fallbackMaterial;

            var shader = DropShader();
            if (shader == null) return null;

            var tint = DropVisualRule.FallbackTint;
            var mat = new Material(shader) { name = "DropFallback", color = tint };
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", tint * DropVisualRule.IconEmission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;

            _fallbackMaterial = mat;
            return mat;
        }

        /// <summary>
        /// 1×1 판. 내장 Quad 프리미티브를 쓰지 않는 이유는 딸려 오는 MeshCollider다 —
        /// 볼록이 아닌 메시 콜라이더는 트리거로 쓸 수 없어 매번 떼어 내야 한다.
        /// </summary>
        static Mesh PlateMesh()
        {
            if (_plateMesh != null) return _plateMesh;

            var m = new Mesh { name = "DropIconPlate" };
            m.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f),
            };
            m.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 1f), new Vector2(1f, 1f),
            };
            // 앞면은 -Z. 빌보드가 카메라 회전을 그대로 받으므로 이쪽이 화면을 향한다.
            m.normals = new[]
            {
                -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward,
            };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateBounds();

            _plateMesh = m;
            return m;
        }

        // ── 궤적 ─────────────────────────────────────────────────

        /// <summary>
        /// 옆으로 튀어 바닥에 내려앉는 궤적. 물리를 붙이면 굴러가 버려서
        /// 어디로 갔는지 놓치기 쉽다. 착지 지점을 정해 두고 트윈으로 보낸다.
        ///
        /// <b>착지 지점만 세계 시드에서 뽑는다.</b> 어디에 떨어지느냐가
        /// <b>주울 수 있느냐</b>를 정하므로 그것은 세계 상태에 닿는 난수다.
        /// 반면 구르는 각도(<see cref="Random"/>의 회전들)는 눈에만 보이는
        /// 것이라 그대로 둔다 — 파티클과 소리가 매번 같으면 그것이 더 나쁘다.
        /// </summary>
        static void Scatter(Transform t, Vector3 origin, float spread, int occasion)
        {
            var rng = Survive.World.WorldSeed.Rng(
                Survive.World.WorldSeedBranch.DropScatter, origin, occasion);

            float 각 = (float)rng.NextDouble() * Mathf.PI * 2f;
            float 거리 = Mathf.Lerp(spread * 0.4f, spread, (float)rng.NextDouble());
            var dir = new Vector2(Mathf.Cos(각), Mathf.Sin(각)) * 거리;
            var target = origin + new Vector3(dir.x, 0f, dir.y);

            if (TryFindGround(target, out float groundY)) target.y = groundY + RestHeight;
            else target.y = origin.y;

            // SetLink: 착지 애니메이션 도중 주워지면(Destroy) 트윈이 갈 곳을 잃는다.
            // static 유틸이라 OnDestroy 훅이 없으니 DOTween이 대상 파괴를 직접 감시하게 한다.
            t.position = origin + Vector3.up * 0.35f;
            t.DOJump(target, 0.55f, 1, 0.5f).SetEase(Ease.OutQuad)
             .SetLink(t.gameObject)
             .OnComplete(() => Idle(t, target));
            t.DORotate(new Vector3(0f, Random.Range(180f, 540f), 0f), 0.5f, RotateMode.LocalAxisAdd)
             .SetLink(t.gameObject);
        }

        /// <summary>땅에서 이만큼 띄워 놓는다. 반쯤 박혀 있으면 무엇인지 안 읽힌다.</summary>
        const float RestHeight = 0.18f;

        /// <summary>
        /// 떨어질 땅을 찾는다.
        ///
        /// <b>전에는 공중에 남았다.</b> 이유가 둘이었다.
        ///
        /// <b>하나, 너무 짧게 봤다.</b> 목표점 3m 위에서 12m만 내려다봤으니 그 아래
        /// 9m 안에 땅이 없으면 못 찾았다. 날아다니는 것(눈·날개)을 잡거나 거대 버섯
        /// 꼭대기의 갓을 따면 땅은 훨씬 아래에 있다. 그럴 때 원래 높이에 두었으니
        /// 죽은 자리에 그대로 걸려 있었다.
        ///
        /// <b>둘, 시체를 땅으로 쳤다.</b> 모든 레이어를 봤기 때문에 방금 죽은 생물의
        /// 몸(<see cref="Survive.Combat.CreatureHealth"/>는 0.1초 뒤에 사라진다)이나
        /// 플레이어의 몸이 먼저 맞았다. 그 위에 내려놓고 나면 몸이 사라지면서
        /// 아이템만 허공에 남는다.
        ///
        /// 그래서 멀리 보고, 맞은 것을 하나씩 살펴 <b>몸이 아닌 첫 번째</b>를 고른다.
        /// </summary>
        static bool TryFindGround(Vector3 target, out float groundY)
        {
            groundY = 0f;

            int count = Physics.RaycastNonAlloc(target + Vector3.up * CastAbove, Vector3.down,
                                                _hits, CastDistance, ~0, QueryTriggerInteraction.Ignore);
            if (count == 0) return false;

            // 가까운 것부터 본다. RaycastNonAlloc은 순서를 보장하지 않는다.
            float best = float.NegativeInfinity;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                if (IsBody(_hits[i].collider)) continue;
                if (_hits[i].point.y > best) { best = _hits[i].point.y; found = true; }
            }

            if (found) groundY = best;
            return found;
        }

        /// <summary>
        /// 땅으로 쳐서는 안 되는 것. 곧 사라지거나 돌아다니는 몸들이다.
        /// 떨어진 물건 자신은 트리거라 애초에 걸리지 않는다.
        /// </summary>
        static bool IsBody(Collider c) =>
            c == null
            || c.GetComponentInParent<Survive.Combat.CreatureHealth>() != null
            || c.GetComponentInParent<CharacterController>() != null;

        /// <summary>목표점보다 이만큼 위에서 내려다본다. 살짝 파묻힌 자리에서도 찾게.</summary>
        const float CastAbove = 2f;

        /// <summary>
        /// 내려다보는 거리. 뭍 하나의 높이와 그 위 구조물을 넉넉히 덮는다.
        /// 이보다 아래라면 뭍이 끝나고 액면이 시작되는 자리이고, 그때는 찾지 못하는
        /// 것이 맞다 — 액면 밑바닥까지 떨어뜨려 봐야 주우러 갈 수 없다.
        /// </summary>
        const float CastDistance = 60f;

        static readonly RaycastHit[] _hits = new RaycastHit[16];

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
