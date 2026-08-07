using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Core;
using Survive.Interaction;
using Survive.Items;
using Survive.Localization;
using Survive.Player;

namespace Survive.Crafting
{
    /// <summary>
    /// 제작대. 상호작용하면 제작 UI를 연다.
    ///
    /// 이제 제작에는 시간이 걸리고, 그 시간은 <b>제작대의 것</b>이다.
    /// 걸어 놓고 다른 일을 하러 갔다가 돌아와 가져간다 — 제작대가 자리를 차지하는
    /// 물건인 이유가 여기서 생긴다. 서 있어야만 돌아간다면 손으로 만드는 것과
    /// 다를 게 없다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CraftingBench : MonoBehaviour, IInteractable, ICraftStation, ISaveable
    {
        [Tooltip("이 제작대의 이름. 비우면 표의 Craft/bench_default_name을 쓴다")]
        [SerializeField] string displayName = "";

        [SerializeField] StationType stationType = StationType.Bench;
        [SerializeField] MMF_Player openFeedback;

        readonly StationCraftQueue _work = new StationCraftQueue();

        public StationType StationType => stationType;

        /// <summary>
        /// 인스펙터에 적은 이름이 우선이고, 비어 있으면 표에서 꺼낸다.
        /// 기본값을 코드에 한국어로 박아 두면 로케일을 따라오지 못한다.
        /// </summary>
        public string StationName => string.IsNullOrWhiteSpace(displayName)
            ? Loc.T("Craft", "bench_default_name")
            : displayName;

        public StationCraftQueue Work => _work;

        /// <summary>제작대는 연료를 먹지 않는다. 세워 두면 늘 돌아간다.</summary>
        public bool IsPowered => true;
        public string PausedReason => null;
        public StationSideAction SideAction => null;

        void Update()
        {
            if (_work.Queue.IsEmpty) return;
            _work.Tick(Time.deltaTime, IsPowered);
        }

        public string InteractionPrompt
        {
            get
            {
                if (_work.HasOutput)
                    return Loc.F("Craft", "bench_prompt_collect", StationName, _work.OutputCount);
                if (!_work.Queue.IsEmpty)
                {
                    float left = CraftQueueService.TotalSecondsLeft(_work.Queue);
                    return Loc.F("Craft", "bench_prompt_busy", StationName,
                                 CraftTimeText.Short(left));
                }
                return Loc.F("Craft", "bench_prompt_idle", StationName);
            }
        }

        public bool CanInteract(PlayerContext player) => player != null;

        public void Interact(PlayerContext player)
        {
            openFeedback?.PlayFeedbacks();

            // 만들어 놓은 것이 있으면 그것부터다. 가지러 온 사람에게
            // 목록을 먼저 들이밀 이유가 없다.
            if (_work.HasOutput && player?.Inventory?.Inventory != null &&
                _work.CollectInto(player.Inventory.Inventory) > 0)
                return;

            var ui = UnityEngine.Object.FindAnyObjectByType<Survive.UI.CraftingUI>(FindObjectsInactive.Include);
            if (ui != null) ui.Open(this);
            else Debug.LogWarning("[CraftingBench] CraftingUI를 찾지 못했습니다.", this);
        }

        // ── 저장 ─────────────────────────────────────────────────
        //
        // <b>걸어 두고 자리를 뜰 수 있다는 것이 이 물건의 값</b>인데(기획서 §5.4),
        // 저장으로 자리를 뜨면 잃는다면 그 값이 반만 참이다. 재료는 걸 때 이미
        // 빠졌으므로 잃는 것은 시간이 아니라 물건이다.
        //
        // <b>세운 제작대는 이 문으로 안 나간다.</b> 그쪽은 씬에 몸이 없어서
        // 불러올 때 생성 목록이 다시 만들고, 그 줄이 대기열까지 함께 싣는다.
        // 여기까지 실으면 같은 것이 두 곳에 실린다 — StorageContainer가 같은
        // 갈림길에서 같은 답을 냈다.

        void OnEnable()
        {
            if (GameServices.TryGet<SaveCoordinator>(out var coord))
                coord.Service?.Register(this);
        }

        void OnDisable()
        {
            if (GameServices.TryGet<SaveCoordinator>(out var coord))
                coord.Service?.Unregister(this);
        }

        bool 씬이_놓은_것 =>
            !TryGetComponent<Survive.Building.BuiltStructure>(out var built) || !built.Spawned;

        /// <summary>
        /// 제작대도 여럿일 수 있다. 옮길 수 없는 물건이라 자리가 곧 신원이다 —
        /// 보관함과 같은 수법이다.
        /// </summary>
        public string SaveKey =>
            $"bench_{Mathf.RoundToInt(transform.position.x)}_" +
            $"{Mathf.RoundToInt(transform.position.y)}_" +
            $"{Mathf.RoundToInt(transform.position.z)}";

        public object CaptureState()
        {
            if (!씬이_놓은_것) return null;

            return new StationSaveState
            {
                output = StationSaveRule.Capture(_work.Output),
                queued = StationSaveRule.Capture(_work.Queue),
            };
        }

        public void RestoreState(object state)
        {
            if (state is not StationSaveState s) return;

            StationSaveRule.AdoptInto(_work.Output, s.output, 아이템찾기);
            StationSaveRule.AdoptInto(_work.Queue, s.queued, RecipeIndex.Find);
        }

        static ItemDataSO 아이템찾기(string id) =>
            GameServices.TryGet<PlayerInventory>(out var pi) && pi != null && pi.Database != null
                ? pi.Database.GetById(id)
                : null;
    }
}
