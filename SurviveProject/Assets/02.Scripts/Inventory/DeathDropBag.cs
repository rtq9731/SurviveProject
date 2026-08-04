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
    /// 재사용하면 창을 여닫는 UI가 공짜로 따라오지만, 자기 시체를 뒤지면서
    /// 무엇을 가져갈지 고르는 장면은 스펙 어디에도 없다.
    /// <see cref="ItemPickup"/> 쪽 계보를 따른다 — <b>E 한 번에 들어가는 만큼 되돌려받는다.</b>
    ///
    /// <b>저장은 스스로 하지 않는다.</b> 보관함처럼 <c>ISaveable</c>을 달아 자기를
    /// 등록하면 저장본에는 아무도 복원하지 않을 유령 항목만 남는다 — 가방은 플레이 중에
    /// 생겨나는 물건이라 불러올 때 되받을 주체가 없기 때문이다.
    /// 그 주체는 <see cref="Survive.Vitals.DeathDropService"/>다. 상주하는 그쪽이
    /// 살아 있는 가방들을 한 칸에 모아 적고, 불러올 때 다시 세운다.
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

        /// <summary>
        /// 불러오기용. 이미 채워진 속을 통째로 받는다.
        ///
        /// <see cref="Fill"/>과 나눈 이유: 저 쪽은 스택 목록을 다시 담느라
        /// 슬롯 배치가 바뀔 수 있다. 저장본에서 온 것은 죽을 때의 배치를
        /// 그대로 들고 있으므로 다시 담지 않고 그대로 앉힌다.
        /// </summary>
        public void Adopt(Inventory contents)
        {
            if (contents != null) _contents = contents;
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
