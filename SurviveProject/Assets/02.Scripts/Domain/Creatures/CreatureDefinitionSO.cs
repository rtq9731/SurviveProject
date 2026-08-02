using UnityEngine;
using Survive.Harvesting;

namespace Survive.Creatures
{
    /// <summary>세계관의 영양 단계. 기획서 생물 도감의 분류를 그대로 쓴다.</summary>
    public enum TrophicTier
    {
        Decomposer,   // 분해자 — 눈, 공
        Producer,     // 생산자 — 날개, 열매게
        Consumer1,    // 1차 소비자 — 랩터형
        Consumer2,    // 2차 소비자 — 다관절형
        Consumer3     // 3차 소비자 — 거미형
    }

    public enum LocomotionType { Ground, Flying }

    /// <summary>
    /// 감지·피격에 대한 반응.
    /// Passive: 무시 / Skittish: 도주 / Defensive: 피격 시 반격 후 이탈 / Aggressive: 추격
    /// </summary>
    public enum BehaviorProfile { Passive, Skittish, Defensive, Aggressive }

    [CreateAssetMenu(menuName = "Survive/Creatures/Creature Definition")]
    public class CreatureDefinitionSO : ScriptableObject
    {
        public string id;
        public string displayName;

        public TrophicTier tier = TrophicTier.Decomposer;
        public LocomotionType locomotion = LocomotionType.Ground;
        public BehaviorProfile behavior = BehaviorProfile.Skittish;

        [Header("능력치")]
        public float maxHealth = 20f;
        public float moveSpeed = 3f;
        public float detectRadius = 8f;

        [Header("전투")]
        public float attackDamage = 5f;
        public float attackRange = 1.5f;
        public float attackCooldown = 1.5f;

        [Tooltip("Defensive가 반격을 유지하는 시간")]
        public float aggroSeconds = 5f;

        public LootTableSO drops;

        [Header("도감 — 챕터 2에서 사용")]
        [TextArea(3, 8)] public string codexDescription;
        public Sprite codexSketch;
    }
}
