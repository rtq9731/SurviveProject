using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Interaction;
using Survive.Player;

namespace Survive.Crafting
{
    /// <summary>제작대. 상호작용하면 제작 UI를 연다.</summary>
    public class CraftingBench : MonoBehaviour, IInteractable
    {
        [SerializeField] string displayName = "제작대";
        [SerializeField] StationType stationType = StationType.Bench;
        [SerializeField] MMF_Player openFeedback;

        public StationType StationType => stationType;

        public string InteractionPrompt => $"[E] {displayName} 사용";
        public bool CanInteract(PlayerContext player) => player != null;

        public void Interact(PlayerContext player)
        {
            openFeedback?.PlayFeedbacks();
            var ui = UnityEngine.Object.FindFirstObjectByType<Survive.UI.CraftingUI>(FindObjectsInactive.Include);
            if (ui != null) ui.Open(stationType);
            else Debug.LogWarning("[CraftingBench] CraftingUI를 찾지 못했습니다.", this);
        }
    }
}
