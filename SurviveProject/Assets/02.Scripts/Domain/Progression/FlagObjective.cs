using UnityEngine;

namespace Survive.Progression
{
    /// <summary>
    /// 지정 플래그가 서면 완료. 지역 도달·상호작용·제작이 공통으로 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "Survive/Progression/Objective - Flag")]
    public class FlagObjective : ObjectiveSO
    {
        public string flagKey;
        [Min(1)] public int requiredCount = 1;

        public override float Evaluate(IObjectiveContext ctx)
        {
            if (ctx == null || string.IsNullOrEmpty(flagKey)) return 0f;
            return Mathf.Clamp01(ctx.GetFlag(flagKey) / (float)Mathf.Max(1, requiredCount));
        }
    }
}
