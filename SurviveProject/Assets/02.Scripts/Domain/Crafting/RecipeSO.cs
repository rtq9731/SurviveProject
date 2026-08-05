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
        Campfire = 2    // 화톳불 — 불이 타는 동안만 가공이 진행된다
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
    }
}
