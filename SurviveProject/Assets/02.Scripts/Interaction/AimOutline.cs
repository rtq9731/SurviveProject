using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Survive.Domain.Art;

namespace Survive.Interaction
{
    /// <summary>
    /// 지금 조준한 것에 옅은 윤곽을 씌운다.
    ///
    /// <b>왜 필요한가.</b> 프롬프트는 "무엇을" 할 수 있는지는 말해 주지만
    /// "어느 것을" 겨눴는지는 말해 주지 않는다. 채집물이 몰려 있는 자리에서는
    /// 자막에 뜬 이름과 눈이 보고 있는 물체가 어긋나 보이고, 그것이 판정이 틀린
    /// 것인지 표시가 없는 것인지 구별할 방법이 없다. 윤곽이 그 구별을 준다.
    ///
    /// <b>어떻게 그리는가.</b> 씬에도 프리팹에도 손대지 않는다. 대상의 메시를
    /// 그 자리 그대로 한 번 더 그리되, 실루엣 가장자리에서 짙어지는 반투명
    /// 셰이더(<c>Survive/AimOutline</c>)를 물린다. 오브젝트를 만들지 않으므로
    /// 대상이 파괴돼도 치울 것이 없고, 머티리얼은 하나를 계속 쓴다 —
    /// 프레임마다 <c>new Material</c>을 만드는 실수는 이 저장소가 낙하물에서
    /// 한 번 저질렀다(<see cref="ItemDropper"/>의 캐시 참조).
    ///
    /// <b>어둠은 지켜진다.</b> 광원이 아니라 대상 표면에 덧그리는 반투명이라
    /// 주변을 밝히지 않고, ZTest가 LEqual이라 벽 너머로 비치지 않는다.
    /// 그려지는 것은 지금 조준한 하나뿐이고 사거리는 3m다 — 어두운 방의
    /// 지형을 읽어 낼 수단이 되지 않는다.
    /// </summary>
    public static class AimOutline
    {
        // ── 취향을 타는 값. 끄고 바꾸는 자리는 여기 하나다 ──────────
        //
        // 세기가 과하거나 색이 거슬리면 아래만 고치면 된다.
        // 에디터 메뉴 `Tools/Survive/조준 윤곽 켜기·끄기`도 같은 스위치를 건드린다.

        /// <summary>윤곽을 그릴 것인가. false면 아무 일도 하지 않는다.</summary>
        public static bool Enabled = true;

        /// <summary>
        /// 윤곽 색. 광원 4색 중 빛기둥 색(#E8D5A8)을 쓴다 —
        /// 어둠 속에 다섯 번째 색을 들이지 않기 위해서다.
        /// </summary>
        public static Color Tint = ArtPalette.LightShaft;

        /// <summary>실루엣 가장자리의 진하기(0~1).</summary>
        public static float RimStrength = 0.42f;

        /// <summary>정면을 보는 면에 깔리는 물빛(0~1). 판때기 하나짜리 대상 때문에 0이 아니다.</summary>
        public static float FillStrength = 0.05f;

        /// <summary>가장자리로 갈수록 얼마나 급히 진해지는가. 클수록 테두리가 얇다.</summary>
        public static float RimPower = 2.4f;

        // ── 그리기 ───────────────────────────────────────────────

        const string ShaderResourcePath = "Shaders/AimOutline";
        const string ShaderName = "Survive/AimOutline";

        static Material _material;
        static Transform _cachedRoot;
        // 나란한 두 목록이다. 같은 자리의 필터와 렌더러가 한 쌍이다 —
        // 필터는 메시를, 렌더러는 월드 경계를 준다.
        static readonly List<MeshFilter> Filters = new List<MeshFilter>();
        static readonly List<MeshRenderer> Renderers = new List<MeshRenderer>();
        static readonly List<MeshFilter> FilterBuffer = new List<MeshFilter>();

        /// <summary>
        /// 플레이 세션이 바뀌면 캐시가 가리키던 것은 이미 파괴돼 있다.
        /// 도메인 리로드를 꺼 둔 설정에서도 확실히 비우려고 진입점에서 초기화한다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()
        {
            _material = null;
            _cachedRoot = null;
            Filters.Clear();
            Renderers.Clear();
        }

        /// <summary>
        /// 이번 프레임의 윤곽을 낸다. 매 프레임 불러야 한다 —
        /// <see cref="Graphics.RenderMesh(in RenderParams, Mesh, int, Matrix4x4)"/>는
        /// 한 프레임짜리 요청이다.
        /// </summary>
        /// <param name="root">조준한 것의 계층. null이면 아무것도 그리지 않는다.</param>
        /// <param name="eye">보는 눈(보통 카메라).</param>
        public static void Draw(Transform root, Transform eye)
        {
            if (!Enabled || root == null || eye == null)
            {
                _cachedRoot = null;
                return;
            }

            var material = Material();
            if (material == null) return;

            if (!ReferenceEquals(root, _cachedRoot))
            {
                _cachedRoot = root;
                Collect(root);
            }

            ApplyStyle(material);

            for (int i = 0; i < Filters.Count; i++)
            {
                var filter = Filters[i];
                var renderer = Renderers[i];
                if (filter == null || renderer == null || !renderer.enabled) continue;

                var mesh = filter.sharedMesh;
                if (mesh == null) continue;

                var rp = new RenderParams(material)
                {
                    layer = filter.gameObject.layer,
                    receiveShadows = false,
                    shadowCastingMode = ShadowCastingMode.Off,
                    lightProbeUsage = LightProbeUsage.Off,
                    reflectionProbeUsage = ReflectionProbeUsage.Off,
                    // 비워 두면 원점 크기 0으로 잡혀 통째로 컬링된다.
                    worldBounds = renderer.bounds,
                };

                Matrix4x4 matrix = filter.transform.localToWorldMatrix;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    Graphics.RenderMesh(rp, mesh, sub, matrix);
            }
        }

        /// <summary>대상이 바뀔 때만 훑는다. 매 프레임 계층을 뒤지면 그것대로 비싸다.</summary>
        static void Collect(Transform root)
        {
            Filters.Clear();
            Renderers.Clear();
            root.GetComponentsInChildren(false, FilterBuffer);

            for (int i = 0; i < FilterBuffer.Count; i++)
            {
                var filter = FilterBuffer[i];
                if (filter == null || filter.sharedMesh == null) continue;

                // 꺼져 있거나 아예 없는 렌더러는 화면에 없는 것이다. 윤곽만 뜨면 유령이다.
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null) continue;

                Filters.Add(filter);
                Renderers.Add(renderer);
            }
        }

        static void ApplyStyle(Material material)
        {
            material.SetColor(BaseColorId, Tint);
            material.SetFloat(RimStrengthId, Mathf.Clamp01(RimStrength));
            material.SetFloat(FillStrengthId, Mathf.Clamp01(FillStrength));
            material.SetFloat(RimPowerId, Mathf.Max(0.01f, RimPower));
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int RimStrengthId = Shader.PropertyToID("_RimStrength");
        static readonly int FillStrengthId = Shader.PropertyToID("_FillStrength");
        static readonly int RimPowerId = Shader.PropertyToID("_RimPower");

        static Material Material()
        {
            if (_material != null) return _material;

            // Resources 우선. 에셋 참조가 없는 셰이더라 빌드에 남는 길이 그쪽뿐이다.
            var shader = Resources.Load<Shader>(ShaderResourcePath);
            if (shader == null) shader = Shader.Find(ShaderName);
            if (shader == null) return null;

            _material = new Material(shader) { name = "AimOutline", hideFlags = HideFlags.DontSave };
            return _material;
        }
    }
}
