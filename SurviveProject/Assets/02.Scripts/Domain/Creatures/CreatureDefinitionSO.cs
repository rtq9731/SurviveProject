using UnityEngine;
using Survive.Harvesting;

namespace Survive.Creatures
{
    /// <summary>
    /// 세계관의 영양 단계. 기획서 생물 도감의 분류를 그대로 쓴다.
    ///
    /// <b>도감은 이 값을 화면에 옮기지 않는다</b>(기획서 §4.7,
    /// <see cref="Survive.Progression.CodexCatalog.DescribeCreature"/>). 여기 남는 이유는
    /// 규칙(포식 차수·연구·드롭)이 값 그대로를 읽기 때문이다.
    /// </summary>
    public enum TrophicTier
    {
        Decomposer,   // 분해자 — 눈, 공
        Producer,     // 생산자 — 날개, 열매게
        Consumer1,    // 1차 소비자 — 랩터형
        Consumer2,    // 2차 소비자 — 다관절형
        Consumer3,    // 3차 소비자 — 거미형

        /// <summary>
        /// 생태계 밖 — <b>포식 차수 0</b>. 먹지 않으므로 먹이사슬 어디에도 서지 않는다
        /// (기획서 §4.5, 낫).
        ///
        /// 「다리 개수 = 포식 차수」(§4.1)에서 다리가 없다는 것이 곧 차수 0이고,
        /// 차수 0은 분해자(1차)보다 아래가 아니라 <b>표 밖</b>이다. 그래서 앞의 다섯과
        /// 같은 줄에 세우지 않고 끝에 붙인다 — 값의 순서가 곧 계층 순서인 자리에
        /// 끼워 넣으면 "분해자보다 낮은 계층"이라는 뜻이 생긴다.
        /// </summary>
        Outside,
    }

    /// <summary>
    /// 무엇으로 움직이는가. <b>멀리서 정체를 알리는 정보</b>다(기획서 §4.5) —
    /// 실루엣이 안 보일 만큼 멀어도 걷는지 나는지 미끄러지는지는 보인다.
    /// </summary>
    public enum LocomotionType
    {
        /// <summary>다리로 걷는다. NavMesh 위를 간다.</summary>
        Ground,

        /// <summary>난다. 지면에서 일정 높이를 유지하며 공중을 간다.</summary>
        Flying,

        /// <summary>
        /// 다리 없이 <b>액면 위를 부유한다.</b> 나는 것과 다른 점은 기준면이다 —
        /// 비행은 지면 위 고도를 지키고, 이쪽은 <b>액체의 수면</b>에 붙어 미끄러진다.
        /// 그래서 갈 수 있는 곳도 다르다(<see cref="ScytheHabitat"/>).
        /// </summary>
        Hovering,
    }

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
