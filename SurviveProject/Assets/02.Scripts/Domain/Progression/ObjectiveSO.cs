using System;
using UnityEngine;
using Survive.Items;

namespace Survive.Progression
{
    /// <summary>
    /// 목표가 진행도를 계산할 때 필요한 게임 상태.
    /// 인터페이스로 두어 도메인이 MonoBehaviour를 모르게 한다.
    /// </summary>
    public interface IObjectiveContext
    {
        Inventory PlayerInventory { get; }
        int GetFlag(string key);
    }

    public abstract class ObjectiveSO : ScriptableObject
    {
        public string id;
        [TextArea] public string displayText;

        public abstract float Evaluate(IObjectiveContext ctx);   // 0~1

        public bool IsComplete(IObjectiveContext ctx) => Evaluate(ctx) >= 1f;
    }

    /// <summary>아이템 N개 보유.</summary>
    [CreateAssetMenu(menuName = "Survive/Progression/Objective - Collect Item")]
    public class CollectItemObjective : ObjectiveSO
    {
        public string itemId;
        [Min(1)] public int amount = 1;

        public override float Evaluate(IObjectiveContext ctx)
        {
            var inv = ctx?.PlayerInventory;
            if (inv == null || string.IsNullOrEmpty(itemId)) return 0f;
            return Mathf.Clamp01(inv.CountOf(itemId) / (float)Mathf.Max(1, amount));
        }
    }

    /// <summary>지정 플래그가 1 이상이면 완료. 지역 도달·상호작용·제작이 공통으로 쓴다.</summary>
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

    /// <summary>생물 N마리 처치. 처치 수는 플래그로 누적된다.</summary>
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

    [CreateAssetMenu(menuName = "Survive/Progression/Chapter")]
    public class ChapterSO : ScriptableObject
    {
        public string id;
        public string title;
        public ObjectiveSO[] objectives = new ObjectiveSO[0];
    }
}
