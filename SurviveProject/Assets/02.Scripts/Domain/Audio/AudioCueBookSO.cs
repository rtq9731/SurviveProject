using UnityEngine;

namespace Survive.Domain.Audio
{
    /// <summary>
    /// 게임 전체의 소리 자리를 한 장에 모은 표.
    ///
    /// <b>왜 프리팹마다 꽂게 하지 않는가.</b> 소리를 넣는 사람은 "줍는 소리"를
    /// 한 번 정하고 싶지, 아이템 프리팹 스물몇 개를 열어 같은 클립을 스물몇 번
    /// 꽂고 싶지 않다. 그리고 발소리처럼 붙일 컴포넌트가 실행 중에 생기는 자리는
    /// 애초에 인스펙터에서 꽂을 방법이 없다.
    ///
    /// 그래서 기본값은 이 표에서 온다. 컴포넌트에 있는 같은 이름의 칸은
    /// <b>그 개체만 다르게 하고 싶을 때 쓰는 덮어쓰기</b>다 — 이 저장소가
    /// <c>HarvestNode.dropPrefab</c>을 다루는 방식과 같다.
    ///
    /// <b>이 에셋이 없어도 된다.</b> 없으면 모든 자리가 비어 있는 것과 같고,
    /// 게임은 지금과 글자 그대로 똑같이 조용히 돈다.
    /// 두는 자리는 <c>Assets/08.Data/Audio/Resources/AudioCueBook.asset</c>이다.
    /// </summary>
    [CreateAssetMenu(menuName = "Survive/Audio/Audio Cue Book", fileName = "AudioCueBook")]
    public class AudioCueBookSO : ScriptableObject
    {
        [Header("발 — 플레이어")]
        [Tooltip("걸을 때. 여러 개 넣으면 번갈아 난다")]
        public AudioCueSO footstepWalk;

        [Tooltip("뛸 때. 걷기와 달라야 소리로 속도를 안다")]
        public AudioCueSO footstepRun;

        [Tooltip("착지 순간 한 번")]
        public AudioCueSO land;

        [Tooltip("도약 순간 한 번")]
        public AudioCueSO jump;

        [Header("전투 — 휘두름")]
        [Tooltip("도구를 휘두를 때마다. 바람 가르는 소리")]
        public AudioCueSO swing;

        [Tooltip("무언가에 맞았을 때")]
        public AudioCueSO swingHit;

        [Tooltip("아무것도 맞지 않았을 때. 허공을 가른 것을 알려 준다")]
        public AudioCueSO swingMiss;

        [Header("전투 — 맞는 쪽")]
        [Tooltip("생물이 맞을 때")]
        public AudioCueSO creatureHit;

        [Tooltip("생물이 죽을 때")]
        public AudioCueSO creatureDeath;

        [Tooltip("플레이어가 맞을 때")]
        public AudioCueSO playerHurt;

        [Header("손 — 줍기와 채집")]
        [Tooltip("아이템을 주울 때")]
        public AudioCueSO itemPickup;

        [Tooltip("채집 노드를 때렸는데 아직 안 부서졌을 때")]
        public AudioCueSO harvestHit;

        [Tooltip("채집 노드가 부서질 때")]
        public AudioCueSO harvestBreak;

        [Tooltip("맞지 않는 도구로 때렸을 때. 튕기는 소리")]
        public AudioCueSO harvestWrongTool;

        [Tooltip("식물을 캘 때")]
        public AudioCueSO plantHarvest;

        [Tooltip("식물이 시들어 사라질 때")]
        public AudioCueSO plantWither;

        [Header("불")]
        [Tooltip("꺼진 불을 다시 지필 때 한 번")]
        public AudioCueSO campfireIgnite;

        [Tooltip("타는 동안 계속. loop를 켜 둘 것")]
        public AudioCueSO campfireLoop;

        [Header("잠수")]
        // 잠수의 압박은 수치가 아니라 연출이 진다(Survive.World.DiveRule).
        // 화면 쪽(가장자리·게이지)은 코드로 서 있고, 귀 쪽 자리를 여기 비워 둔다 —
        // 클립이 꽂히는 날 코드를 고칠 일이 없게 하려는 것이다.

        [Tooltip("잠수 중 심박. 남은 산소가 줄수록 간격이 짧아진다(DiveRule.BeatSeconds)")]
        public AudioCueSO diveHeartbeat;

        [Tooltip("잠수 중 숨소리. 심박 사이에 깔린다")]
        public AudioCueSO diveBreath;

        /// <summary>
        /// 컴포넌트에 꽂힌 값이 있으면 그것을, 없으면 표의 값을 쓴다.
        /// 이 저장소의 "비움이 기본" 관례를 한 줄로 적어 둔 것이다.
        /// </summary>
        public static AudioCueSO Or(AudioCueSO overrideCue, AudioCueSO fromBook) =>
            overrideCue != null ? overrideCue : fromBook;
    }
}
