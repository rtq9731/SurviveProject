using System.Collections.Generic;
using UnityEngine;
using Survive.Interaction;
using Survive.Player;

namespace Survive.Items
{
    /// <summary>
    /// 죽은 자리에 남는 가방. 여기까지 돌아와야 벌어온 것을 되찾는다.
    ///
    /// <b>보관함이 아니라 줍는 물건이다.</b> <see cref="Survive.Interaction.StorageContainer"/>를
    /// 재사용하면 창을 여닫는 UI가 공짜로 따라오지만, 그쪽은 <c>ISaveable</c>이라 스스로
    /// 저장 목록에 등록한다. 가방은 플레이 중에 생겨나는 물건이고 불러오기가 되살릴 방법이
    /// 없으므로, 저장본에는 아무도 복원하지 않을 유령 항목만 남는다.
    /// 그래서 <see cref="ItemPickup"/> 쪽 계보를 따른다 — <b>E 한 번에 들어가는 만큼 되돌려받는다.</b>
    /// 자기 시체를 뒤지면서 무엇을 가져갈지 고르는 장면은 스펙 어디에도 없다.
    ///
    /// 자리가 모자라 다 못 받으면 남은 것은 가방에 그대로 있고, 가방도 그 자리에 남는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class DeathDropBag : MonoBehaviour, IInteractable
    {
        Inventory _contents;

        /// <summary>가방 안. 비어 있으면 가방은 곧 사라진다.</summary>
        public Inventory Contents => _contents;

        /// <summary>지금 세계에 남아 있는 가방들. 회수 대상을 찾을 때 쓴다.</summary>
        public static IReadOnlyList<DeathDropBag> Active => _active;
        static readonly List<DeathDropBag> _active = new List<DeathDropBag>();

        void OnEnable() { if (!_active.Contains(this)) _active.Add(this); }
        void OnDisable() => _active.Remove(this);

        /// <summary>런타임 생성용. 떼어낸 것을 그대로 받는다.</summary>
        public void Fill(IReadOnlyList<ItemStack> stacks, int slotCount)
        {
            _contents = new Inventory(Mathf.Max(1, slotCount));
            DeathDrop.Fill(_contents, stacks);
        }

        public bool IsEmpty => !DeathDrop.HasAnything(_contents);

        public string InteractionPrompt => IsEmpty ? "" : "[E] 남긴 것 회수";

        public bool CanInteract(PlayerContext player) =>
            !IsEmpty && player != null && player.Inventory != null &&
            player.Inventory.Inventory != null;

        public void Interact(PlayerContext player)
        {
            if (!CanInteract(player)) return;

            int moved = DeathDrop.Retrieve(_contents, player.Inventory.Inventory);
            Debug.Log($"[DeathDropBag] {moved}개를 회수했다");

            // 다 비었으면 남겨 둘 이유가 없다. 빈 가방이 지형에 널려 있으면
            // 아직 회수할 것이 있는 가방과 구별되지 않는다.
            if (IsEmpty) Destroy(gameObject);
        }
    }
}
