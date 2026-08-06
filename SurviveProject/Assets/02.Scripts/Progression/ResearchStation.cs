using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Crafting;
using Survive.Interaction;
using Survive.Items;
using Survive.Localization;
using Survive.Player;

namespace Survive.Progression
{
    /// <summary>
    /// 연구대 — <b>AI를 입력한 장치</b>(스펙 §2 채널 2).
    ///
    /// 우주복의 AI는 손에 쥔 것을 그 자리에서 읽어낸다(채널 1). 하지만 낫이 흘리고 간
    /// 물건처럼 읽어서 알 수 없는 것이 있고, 그때 필요한 것은 시간과 전력이다.
    /// 연구대는 그 둘을 대는 자리다 — 스크랩에 갇힌 에너지를 태워 AI에게 추가 연산을
    /// 돌리게 하고, 그 결과로 <b>아는 것이 늘어난다</b>.
    ///
    /// 제작대와 같은 물건이다. 걸어 두고 떠났다가 돌아온다. 다른 것은 돌아왔을 때
    /// 가져갈 것이 없다는 점뿐이다 — 알아낸 것은 이미 원장에 적혔고, 원장은 몸이
    /// 아니라 판에 붙어 있다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ResearchStation : MonoBehaviour, IInteractable, IResearchStation
    {
        /// <summary>Resources 아래에서 연구 목록을 찾는 이름.</summary>
        public const string BookResourceName = "ResearchBook";

        [Tooltip("이 연구대의 이름. 비우면 표의 UI/research_station_default를 쓴다")]
        [SerializeField] string displayName = "";

        [Tooltip("비우면 Resources/ResearchBook을 찾는다")]
        [SerializeField] ResearchBookSO book;

        [SerializeField] MMF_Player openFeedback;
        [SerializeField] MMF_Player completeFeedback;

        readonly ResearchQueue _work = new ResearchQueue();

        /// <summary>
        /// 인스펙터에 적은 이름이 우선이고, 비어 있으면 표에서 꺼낸다.
        /// 목록 머리띠(<c>MenuListing.ResearchHeaderLine</c>)가 이미 쓰던 것과
        /// 같은 이름표라, 같은 이름이 두 자리에서 갈라지지 않는다.
        /// </summary>
        public string StationName => string.IsNullOrWhiteSpace(displayName)
            ? Loc.T("UI", "research_station_default")
            : displayName;
        public ResearchBookSO Book => book;
        public ResearchQueue Work => _work;
        public ItemDataSO EnergyItem => book != null ? book.energyItem : null;

        /// <summary>연구대는 연료를 먹지 않는다. 세워 두면 늘 돌아간다.</summary>
        public bool IsPowered => true;
        public string PausedReason => null;

        /// <summary>이 연구대가 지금까지 끝낸 항목 수. 검증 하네스가 본다.</summary>
        public int CompletedCount { get; private set; }

        /// <summary>마지막으로 끝낸 항목. 검증 하네스가 본다.</summary>
        public ResearchEntrySO LastCompleted { get; private set; }

        void Awake()
        {
            if (book == null) book = Resources.Load<ResearchBookSO>(BookResourceName);
            if (book == null)
                Debug.LogWarning($"[ResearchStation] Resources/{BookResourceName}을(를) 찾지 못했습니다. " +
                                 "연구 항목이 하나도 뜨지 않습니다.", this);
        }

        void Update()
        {
            if (_work.IsEmpty) return;

            var done = ResearchService.Tick(_work, Time.deltaTime, BlueprintGate.Active, IsPowered);
            if (done == null) return;

            CompletedCount++;
            LastCompleted = done;
            completeFeedback?.PlayFeedbacks();

            // 알아낸 것을 말하는 목소리는 하나뿐이다 — 첫 습득 때 말하던 그 AI다.
            // 자막판을 따로 세우면 같은 화자가 두 군데서 다른 방식으로 말하게 된다.
            if (UnlockService.Instance != null) UnlockService.Instance.Announce(done.line);
        }

        // ── 상호작용 ─────────────────────────────────────────────

        public string InteractionPrompt
        {
            get
            {
                if (_work.IsEmpty) return Loc.F("Progress", "research_prompt_idle", StationName);

                float left = ResearchService.TotalSecondsLeft(_work);
                return Loc.F("Progress", "research_prompt_busy", StationName,
                             CraftTimeText.Short(left));
            }
        }

        public bool CanInteract(PlayerContext player) => player != null;

        public void Interact(PlayerContext player)
        {
            openFeedback?.PlayFeedbacks();

            // 제작대와 달리 회수할 것이 없다. 곧바로 목록을 연다.
            var ui = Object.FindAnyObjectByType<Survive.UI.CraftingUI>(FindObjectsInactive.Include);
            if (ui != null) ui.Open(this);
            else Debug.LogWarning("[ResearchStation] CraftingUI를 찾지 못했습니다.", this);
        }
    }
}
