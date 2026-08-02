using UnityEngine;

namespace Survive.Progression
{
    /// <summary>생물 N마리 처치. 처치 수는 "kill:{id}" 플래그로 누적된다.</summary>
    [CreateAssetMenu(menuName = "Survive/Progression/Objective - Kill Creature")]
    public class KillCreatureObjective : ObjectiveSO
    {
        public string creatureId;
        [Min(1)] public int amount = 1;

        public override float Evaluate(IObjectiveContext ctx)
        {
            if (ctx == null || string.IsNullOrEmpty(creatureId)) return 0f;
            return Mathf.Clamp01(ctx.GetFlag("kill:" + creatureId) / (float)Mathf.Max(1, amount));
        }
    }
}
