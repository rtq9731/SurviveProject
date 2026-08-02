using UnityEngine;

namespace Survive.Harvesting
{
    /// <summary>
    /// 식물 채집물의 정의. 채집 노드와 달리 <b>자란다</b>.
    /// 생산자 기계가 이것을 먹어 스크랩을 만들고, 그 사이 노드는 단계가 내려간다.
    /// </summary>
    [CreateAssetMenu(menuName = "Survive/World/Plant Node")]
    public class PlantNodeSO : ScriptableObject
    {
        public string displayName = "식물";

        [Header("성장")]
        [Tooltip("최대 성장 단계. 0은 밑동만 남은 상태")]
        [Min(1)] public int maxStage = 2;

        [Tooltip("한 단계 자라는 데 걸리는 시간(초)")]
        [Min(1f)] public float growSeconds = 45f;

        [Tooltip("단계별 크기 배율. 0단계는 이 값의 최솟값을 쓴다")]
        public float minScale = 0.35f;
        public float maxScale = 1f;

        [Header("채집")]
        [Tooltip("한 단계당 떨어지는 전리품")]
        public LootTableSO dropsPerStage;

        [Tooltip("채집에 걸리는 시간")]
        [Min(0.1f)] public float harvestSeconds = 1.2f;

        [Header("남획")]
        [Tooltip("0단계로 떨어진 뒤 이 시간이 지나면 소멸한다. 0이면 소멸하지 않는다")]
        [Min(0f)] public float witherSeconds = 90f;

        [Header("섭취")]
        [Tooltip("생산자가 한 번 먹을 때 얻는 스크랩 가치")]
        [Min(0f)] public float nutritionPerStage = 2f;
    }
}
