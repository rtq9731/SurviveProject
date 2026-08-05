using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Interaction;
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
    public class CraftingBench : MonoBehaviour, IInteractable, ICraftStation
    {
        [SerializeField] string displayName = "제작대";
        [SerializeField] StationType stationType = StationType.Bench;
        [SerializeField] MMF_Player openFeedback;

        readonly StationCraftQueue _work = new StationCraftQueue();

        public StationType StationType => stationType;
        public string StationName => displayName;
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
                if (_work.HasOutput) return $"[E] {displayName}에서 {_work.OutputCount}개 회수";
                if (!_work.Queue.IsEmpty)
                {
                    float left = CraftQueueService.TotalSecondsLeft(_work.Queue);
                    return $"[E] {displayName} 사용 (제작 중 {CraftTimeText.Short(left)})";
                }
                return $"[E] {displayName} 사용";
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

            var ui = UnityEngine.Object.FindFirstObjectByType<Survive.UI.CraftingUI>(FindObjectsInactive.Include);
            if (ui != null) ui.Open(this);
            else Debug.LogWarning("[CraftingBench] CraftingUI를 찾지 못했습니다.", this);
        }
    }
}
