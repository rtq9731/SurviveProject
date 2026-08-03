using UnityEngine;
using Survive.Items;

namespace Survive.Building
{
    /// <summary>어디에 놓을 수 있는가.</summary>
    public enum PlacementMode
    {
        /// <summary>지면에 놓는다. 바닥·벽·작업대 대부분이 이것이다.</summary>
        Ground,

        /// <summary>이미 지은 구조물 위에 얹는다. 지붕·2층 바닥.</summary>
        OnStructure,

        /// <summary>지면이든 구조물이든 상관없다.</summary>
        Anywhere,
    }

    /// <summary>
    /// 지을 수 있는 것 하나의 정의.
    ///
    /// 제작(RecipeSO)과 나누는 이유: 제작은 아이템을 인벤토리에 넣고 끝나지만
    /// 건설은 세계에 물체를 놓는다. 놓을 자리를 고르는 절차, 겹침 판정, 지면 각도
    /// 같은 것들이 전부 여기에만 필요하다.
    /// </summary>
    [CreateAssetMenu(menuName = "Survive/Building/Buildable")]
    public class BuildableSO : ScriptableObject
    {
        public string id;
        public string displayName;

        [Tooltip("실제로 세워질 프리팹")]
        public GameObject prefab;

        [Tooltip("배치 미리보기에 쓸 프리팹. 비우면 prefab을 그대로 쓴다")]
        public GameObject ghostPrefab;

        public ItemStack[] cost = new ItemStack[0];

        public PlacementMode placement = PlacementMode.Ground;

        [Tooltip("이 각도보다 가파른 비탈에는 못 짓는다")]
        [Range(0f, 60f)] public float maxSlopeDegrees = 30f;

        [Tooltip("주변에 이만큼 안에 다른 건축물이 있으면 못 짓는다")]
        [Min(0f)] public float clearanceRadius = 1.2f;

        [Tooltip("놓을 때 격자에 맞출 간격. 0이면 자유 배치")]
        [Min(0f)] public float gridSnap = 0f;

        [Tooltip("Y축 회전 단위(도). 0이면 자유 회전")]
        [Min(0f)] public float rotationStep = 15f;
    }
}
