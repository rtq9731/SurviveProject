using Survive.Player;

namespace Survive.Interaction
{
    public interface IInteractable
    {
        /// <summary>화면에 띄울 문구. 예: "[E] 곡괭이 줍기"</summary>
        string InteractionPrompt { get; }

        bool CanInteract(PlayerContext player);
        void Interact(PlayerContext player);
    }

    /// <summary>채집처럼 일정 시간 누르고 있어야 하는 상호작용.</summary>
    public interface IHoldInteractable : IInteractable
    {
        float HoldDuration { get; }
        void OnHoldProgress(float normalized);
        void OnHoldCancelled();
    }
}
