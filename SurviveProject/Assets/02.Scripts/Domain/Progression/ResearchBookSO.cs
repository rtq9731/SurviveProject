using UnityEngine;

namespace Survive.Progression
{
    /// <summary>
    /// 연구 항목 목록. <see cref="DiscoveryBookSO"/>·<see cref="Survive.Crafting.RecipeBookSO"/>와
    /// 같은 자리다 — 연구대가 흩어진 에셋을 런타임에 훑지 않고 한 곳에서 받는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Survive/Progression/Research Book")]
    public class ResearchBookSO : ScriptableObject
    {
        public ResearchEntrySO[] entries = new ResearchEntrySO[0];

        /// <summary>
        /// 연구대가 태우는 것. 스크랩이다.
        ///
        /// 항목마다 적지 않고 여기 한 번만 적는다 — 무엇을 태우는가는 이 세계의 규칙이지
        /// 항목별 선택이 아니다(스크랩 = 에너지 저장 매체). 환급하려면 아이템 정의 자체가
        /// 있어야 해서 id 문자열로는 부족하다.
        /// </summary>
        public Survive.Items.ItemDataSO energyItem;

        public ResearchEntrySO Find(string id)
        {
            if (string.IsNullOrEmpty(id) || entries == null) return null;

            foreach (var e in entries)
                if (e != null && e.id == id) return e;
            return null;
        }
    }
}
