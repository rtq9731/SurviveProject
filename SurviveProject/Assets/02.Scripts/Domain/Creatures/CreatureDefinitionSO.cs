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

        [Tooltip("Defensive가 반격을 유지하는 시간. Aggressive는 시야에서 놓친 뒤 이만큼 더 쫓다가 돌아간다")]
        public float aggroSeconds = 5f;

        [Header("빛")]
        [Tooltip("밝은 구역을 꺼린다. 소비자만 켠다 — 대상이 빛 안에 있는 동안은 " +
                 "추격하지 않고, 자기가 빛 안에 들면 물러난다. " +
                 "기존 4종은 꺼져 있어 판단이 예전과 같다")]
        public bool avoidsLight = false;

        [Header("소리")]
        [Tooltip("가까워질 때 들리는 기척. 어둠 속에서 보이지 않는 것을 귀로 알아채는 " +
                 "자리다. 비우면 아무 소리도 내지 않고, 재는 컴포넌트조차 붙지 않는다")]
        public Survive.Domain.Audio.AudioCueSO approachCue;

        [Tooltip("기척이 들리기 시작하는 거리(m). 감지 반경보다 넉넉해야 " +
                 "쫓기기 전에 먼저 듣는 순간이 생긴다")]
        public float audibleRange = 30f;

        public LootTableSO drops;

        [Tooltip("순찰 중 가끔 흘리고 가는 유물. 비우면 아무것도 흘리지 않는다 — " +
                 "죽여야 나오는 drops와 달리 이쪽은 살아 있는 동안 시간에 붙는다")]
        public Survive.Progression.RelicShedSO relicShed;

        [Header("도감 — 챕터 2에서 사용")]
        [TextArea(3, 8)] public string codexDescription;
        public Sprite codexSketch;
    }
}
