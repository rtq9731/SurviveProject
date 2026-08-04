using UnityEngine;

namespace Survive.World
{
    /// <summary>
    /// 지정한 레이어를 카메라에서 일정 거리 밖이면 그리지 않는다.
    ///
    /// 왜 필요한가: 건너 섬의 버섯과 발광 광원이 안개를 뚫고 실루엣으로 남는다.
    /// 안개 밀도로는 해결되지 않는다 — 0.045에서 0.1로 올려(20m 가시성이 44%에서 2%로
    /// 무너지는 값) 대비가 0.084에서 0.057까지밖에 안 떨어졌다. HDR로 밝게 렌더된 광원은
    /// 안개가 0.4%만 남겨도 톤매핑 뒤에 눈에 띈다. 거리에서 끊는 것이 유일하게 확실하다.
    ///
    /// 설계와도 맞는다: 섬 하나가 이동 수단 하나에 1:1로 대응하므로(P2 스펙 §3),
    /// 다음 섬은 뚫기 전까지 보이지 않는 편이 티어 명료성에 맞다.
    ///
    /// Camera.layerCullDistances는 직렬화되지 않는다. 매 실행 코드로 넣어야 한다 —
    /// 인스펙터에서 아무리 맞춰도 저장되지 않는다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class LayerCullDistance : MonoBehaviour
    {
        [SerializeField] string layerName = "IslandProps";

        /// <summary>이 거리 밖의 해당 레이어는 그리지 않는다.</summary>
        [SerializeField, Min(1f)] float distance = 70f;

        void OnEnable() => Apply();
        void OnValidate() => Apply();

        void Apply()
        {
            var cam = GetComponent<Camera>();
            if (cam == null) return;

            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
            {
                Debug.LogWarning($"[컬링] 레이어 '{layerName}'가 없다. 거리 컬링을 걸지 못했다.");
                return;
            }

            // 길이 32짜리 사본이 돌아온다. 0은 '파 클립까지'라는 뜻이므로 건드리지 않는다.
            var distances = cam.layerCullDistances;
            distances[layer] = distance;
            cam.layerCullDistances = distances;

            // layerCullSpherical은 건드리지 않는다 — SRP에서는 지원하지 않고
            // 설정하면 매 프레임 경고만 남는다("only with the built-in renderer").
        }
    }
}
