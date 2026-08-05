using UnityEngine;

namespace Survive.Creatures
{
    /// <summary>
    /// 생물 정의 목록. <see cref="Survive.Progression.DiscoveryBookSO"/>·
    /// <see cref="Survive.Progression.ResearchBookSO"/>와 같은 자리다 —
    /// 흩어진 에셋을 런타임에 훑지 않고 한 곳에서 받는다.
    ///
    /// 도감이 이것을 필요로 한다. 생물 정의는 프리팹이 물고 있어서, 아직 한 마리도
    /// 세계에 나오지 않았으면 <c>FindObjectsOfTypeAll</c>로는 잡히지 않는다.
    /// 그러면 "아직 못 본 것이 무엇인가"를 보여 주는 화면이 정작 못 본 것을
    /// 빠뜨린다 — 도감에서 그것은 빈 화면과 같다.
    ///
    /// <b>새 서사를 담지 않는다.</b> 여기 있는 것은 이미 있는 에셋을 가리키는
    /// 참조뿐이고, 이름·성향·수치·설명문은 전부 각 정의가 들고 있다.
    /// </summary>
    [CreateAssetMenu(menuName = "Survive/Creatures/Creature Book")]
    public class CreatureBookSO : ScriptableObject
    {
        [Tooltip("도감에 뜨는 차례대로. 미해금은 실루엣으로 남으므로 순서 자체가 정보다")]
        public CreatureDefinitionSO[] creatures = new CreatureDefinitionSO[0];

        public CreatureDefinitionSO Find(string id)
        {
            if (string.IsNullOrEmpty(id) || creatures == null) return null;

            foreach (var c in creatures)
                if (c != null && c.id == id) return c;
            return null;
        }
    }
}
