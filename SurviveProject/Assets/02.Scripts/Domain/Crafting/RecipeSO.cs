using UnityEngine;
using Survive.Items;

namespace Survive.Crafting
{
    /// <summary>
    /// 어디서 만들 수 있는가. 숫자는 이미 만들어진 레시피 에셋에 박혀 있으므로
    /// 새 항목은 <b>뒤에만</b> 붙인다.
    /// </summary>
    public enum StationType
    {
        None = 0,       // 휴대 제작 — 손에서 만든다
        Bench = 1,      // 제작대
        Campfire = 2,   // 화톳불 — 불이 타는 동안만 가공이 진행된다

        /// <summary>
        /// 연구대. 여기서 나오는 것은 아이템이 아니라 <b>아는 것</b>이라 이 값을
        /// 요구하는 레시피는 없다 — 화면이 "지금 어느 자리에 서 있는가"를 구별하는
        /// 데만 쓴다(<see cref="Survive.Progression.ResearchStation"/>).
        /// </summary>
        Research = 3
    }

    [CreateAssetMenu(menuName = "Survive/Crafting/Recipe")]
    public class RecipeSO : ScriptableObject
    {
        public string id;
        public string displayName;

        public ItemStack[] ingredients = new ItemStack[0];
        public ItemStack result;

        /// <summary>
        /// 한 개를 만드는 데 걸리는 시간(초).
        ///
        /// 예전에는 아무도 읽지 않는 값이었다 — 제작은 누르는 즉시 끝났다.
        /// 지금은 이것이 대기열 항목의 수명이다. 0이면 즉시 완성된다.
        /// </summary>
        [Min(0f)] public float craftSeconds = 1f;

        /// <summary>
        /// 이 레시피를 걸 수 있는 자리. <see cref="StationType.None"/>이면
        /// 손에서, 아니면 그 스테이션의 대기열에서만 걸린다.
        /// 화톳불의 배터리 가공도 별개의 체계가 아니라 이 필드로 갈린다.
        /// </summary>
        public StationType requiredStation = StationType.None;

        /// <summary>
        /// 이걸 만들 줄 알아야 한다. 비어 있으면 <b>처음부터 열려 있다</b> —
        /// 청사진을 얹기 전에 만들어 둔 레시피 에셋들이 그대로 동작하는 근거다.
        ///
        /// 재료 요건과는 독립이다. 재료가 있어도 모르면 못 만들고,
        /// 알아도 재료가 없으면 못 만든다.
        /// </summary>
        public Survive.Progression.BlueprintSO requiredBlueprint;
    }
}
