using System;
using System.Collections.Generic;
using UnityEngine;
using Survive.Items;

namespace Survive.Harvesting
{
    /// <summary>
    /// 드롭 표. rng를 주입받아 결정적으로 테스트할 수 있다.
    /// </summary>
    [CreateAssetMenu(menuName = "Survive/World/Loot Table")]
    public class LootTableSO : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public ItemDataSO item;
            [Min(0)] public int minCount = 1;
            [Min(0)] public int maxCount = 1;

            [Range(0f, 1f)]
            [Tooltip("1이면 항상, 0이면 절대 나오지 않는다")]
            public float chance = 1f;
        }

        public Entry[] entries = new Entry[0];

        /// <summary>
        /// 각 항목을 독립적으로 굴린다. 배타 선택이 아니다.
        /// </summary>
        public List<ItemStack> Roll(System.Random rng)
        {
            var 결과 = new List<ItemStack>();
            if (entries == null) return 결과;
            if (rng == null) rng = new System.Random();

            foreach (var e in entries)
            {
                if (e?.item == null) continue;
                if (e.chance <= 0f) continue;

                if (e.chance < 1f && rng.NextDouble() >= e.chance) continue;

                int min = Mathf.Min(e.minCount, e.maxCount);
                int max = Mathf.Max(e.minCount, e.maxCount);
                int 개수 = min == max ? min : rng.Next(min, max + 1);
                if (개수 <= 0) continue;

                결과.Add(new ItemStack(e.item, 개수));
            }
            return 결과;
        }
    }
}
