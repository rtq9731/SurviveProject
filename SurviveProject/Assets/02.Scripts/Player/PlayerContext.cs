using UnityEngine;
using Survive.Items;
using Survive.Vitals;

namespace Survive.Player
{
    /// <summary>
    /// 플레이어 하위 시스템의 단일 접근점.
    /// 상호작용 대상이나 UI가 플레이어의 개별 컴포넌트를 찾아다니지 않게 한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerContext : MonoBehaviour
    {
        public PlayerLocomotion Locomotion { get; private set; }
        public PlayerCameraRig CameraRig { get; private set; }
        public PlayerToolHolder ToolHolder { get; private set; }
        public PlayerVitals Vitals { get; private set; }
        public PlayerInventory Inventory { get; private set; }
        public Survive.Interaction.PlayerInteractor Interactor { get; private set; }
        public Transform Transform { get; private set; }

        void Awake()
        {
            Transform = transform;
            Locomotion = GetComponentInChildren<PlayerLocomotion>(true);
            CameraRig = GetComponentInChildren<PlayerCameraRig>(true);
            ToolHolder = GetComponentInChildren<PlayerToolHolder>(true);
            Vitals = GetComponentInChildren<PlayerVitals>(true);
            Inventory = GetComponentInChildren<PlayerInventory>(true);
            Interactor = GetComponentInChildren<Survive.Interaction.PlayerInteractor>(true);
        }
    }
}
