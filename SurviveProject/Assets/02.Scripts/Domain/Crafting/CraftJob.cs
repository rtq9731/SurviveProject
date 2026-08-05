using UnityEngine;

namespace Survive.Crafting
{
    /// <summary>
    /// 대기열에 걸린 제작 항목 하나 — "무엇을 몇 개, 지금 어디까지".
    ///
    /// 제작이 즉시 끝나던 시절에는 이런 것이 필요 없었다. 이제 모든 제작에
    /// 시간이 걸리므로, 만드는 중이라는 상태가 어딘가에 살아 있어야 한다.
    /// 그 자리가 여기다 — MonoBehaviour가 아니라서 손 제작이든 제작대든
    /// 화톳불이든 같은 것을 쓴다.
    ///
    /// 재료는 <b>대기열에 걸 때 전부</b> 빠진다(수량 N이면 N배). 진행하면서
    /// 하나씩 빼면 "재료를 다 모아 두고 걸어 놨는데 중간에 다른 데 써 버려서
    /// 멈춰 있는" 상태가 생기고, 그건 대기열이 아니라 함정이다.
    /// </summary>
    public class CraftJob
    {
        public CraftJob(RecipeSO recipe, int count, float unitSeconds)
        {
            Recipe = recipe;
            Remaining = Mathf.Max(0, count);
            UnitSeconds = Mathf.Max(0f, unitSeconds);
        }

        public RecipeSO Recipe { get; }

        /// <summary>아직 완성되지 않은 개수. 지금 만드는 중인 것도 여기 포함된다.</summary>
        public int Remaining { get; internal set; }

        /// <summary>지금 만드는 <b>한 개</b>에 들인 시간.</summary>
        public float Elapsed { get; internal set; }

        /// <summary>개당 소요 시간. 레시피의 craftSeconds를 걸 때 찍어 둔 값이다.</summary>
        public float UnitSeconds { get; }

        /// <summary>
        /// 다 만들었는데 넣을 자리가 없어 멈춰 있는가.
        /// 산출물을 버리지 않으려면 멈추는 수밖에 없다.
        /// </summary>
        public bool Stalled { get; internal set; }

        public bool IsDone => Remaining <= 0;

        /// <summary>지금 만드는 한 개의 진행도 0~1. 게이지가 읽는 값이다.</summary>
        public float UnitProgress =>
            UnitSeconds <= 0f ? 1f : Mathf.Clamp01(Elapsed / UnitSeconds);

        /// <summary>이 항목이 전부 끝날 때까지 남은 시간(초).</summary>
        public float SecondsLeft =>
            Mathf.Max(0f, UnitSeconds * Remaining - Elapsed);
    }
}
