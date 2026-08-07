using UnityEngine;

namespace Survive.Harvesting
{
    /// <summary>
    /// 식물 정의 목록. <see cref="Survive.Creatures.CreatureBookSO"/>와 같은 자리다 —
    /// 흩어진 에셋을 런타임에 훑지 않고 한 곳에서 받는다.
    ///
    /// <b>왜 필요한가.</b> 도감에 식물 탭을 세우려면 "아직 못 본 것이 무엇인가"를
    /// 보여 줘야 하는데, 식물 정의는 프리팹이 물고 있어서 세계에 한 포기도 나오지
    /// 않았으면 런타임 탐색으로는 잡히지 않는다. <c>AssetDatabase</c>는 에디터
    /// 전용이라 게임에서 쓸 수 없다. 생물 도감이 같은 이유로 목록 에셋을 두었고,
    /// 여기서도 그 자리를 그대로 쓴다.
    ///
    /// <b>새 서사를 담지 않는다.</b> 여기 있는 것은 이미 있는 에셋을 가리키는
    /// 참조뿐이고, 이름과 수치는 전부 각 정의(<see cref="PlantNodeSO"/>)가 들고 있다.
    /// 그래서 이 에셋이 늘어난다고 번역 표에 손댈 것이 생기지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Survive/World/Plant Book")]
    public class PlantBookSO : ScriptableObject
    {
        [Tooltip("도감에 뜨는 차례대로. 미관측은 실루엣으로 남으므로 순서 자체가 정보다")]
        public PlantNodeSO[] plants = new PlantNodeSO[0];
    }
}
