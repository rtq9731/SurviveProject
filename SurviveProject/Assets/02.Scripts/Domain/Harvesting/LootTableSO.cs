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
        ///
        /// <b>주지 않으면 실패한다.</b> 예전에는 <c>rng</c>가 비면 여기서
        /// <c>new System.Random()</c>(시각 시드)을 만들어 썼는데, 그 한 줄이
        /// 뒷문이었다 — <b>옳은 시그니처가 이미 있는데</b> 호출자 셋이 전부
        /// 새 난수를 넘겨서 시그니처가 아무 값도 못 내고 있었다. 조용히
        /// 대신해 주는 폴백은 「주인 없는 난수」를 <b>눈에 안 띄게</b> 만든다.
        /// 여기서 던지면 그 자리가 컴파일이 아니라 첫 굴림에서 바로 드러난다.
        ///
        /// 난수는 <see cref="Survive.World.WorldSeed"/>에서 받는다 —
        /// 세계 시드 하나에서 갈래·자리·굴림 횟수로 파생한 것이다.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="rng"/>가 <c>null</c>일 때. 주인 없는 난수를 만들어
        /// 메우지 않는다.
        /// </exception>
        public List<ItemStack> Roll(System.Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var result = new List<ItemStack>();
            if (entries == null) return result;

            foreach (var e in entries)
            {
                if (e?.item == null) continue;
                if (e.chance <= 0f) continue;

                if (e.chance < 1f && rng.NextDouble() >= e.chance) continue;

                int min = Mathf.Min(e.minCount, e.maxCount);
                int max = Mathf.Max(e.minCount, e.maxCount);
                int count = min == max ? min : rng.Next(min, max + 1);
                if (count <= 0) continue;

                result.Add(new ItemStack(e.item, count));
            }
            return result;
        }
    }
}
