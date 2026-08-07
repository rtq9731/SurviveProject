using System;
using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Core;
using Survive.Items;
using Survive.Localization;
using Survive.Player;

namespace Survive.Interaction
{
    /// <summary>
    /// 세워 두고 물건을 넣어 두는 보관함.
    ///
    /// <see cref="LootContainer"/>와 다르다. 저쪽은 미리 채워 둔 것을 한 번 털어가는
    /// 세계의 배치물이고, 이쪽은 플레이어가 넣고 빼는 그릇이다.
    /// 소지품 15칸이 금방 차는데 버릴 수도 없어서, 거점을 만들 이유가 필요했다.
    /// </summary>
    [DisallowMultipleComponent]
    public class StorageContainer : MonoBehaviour, IInteractable, ISaveable
    {
        [Tooltip("비우면 번역 표의 World/storage_default를 쓴다")]
        [SerializeField] string displayName = "";
        [SerializeField] int slotCount = 18;

        [Tooltip("열 때 재생")]
        [SerializeField] MMF_Player openFeedback;

        [Tooltip("세계에 하나뿐이 아니므로 저장 키에 붙일 꼬리표. 비우면 위치로 만든다")]
        [SerializeField] string saveIdOverride;

        Inventory _contents;

        public Inventory Contents => _contents ??= new Inventory(slotCount);

        /// <summary>
        /// 보관함 창의 제목이자 프롬프트에 들어가는 이름.
        /// 인스펙터에 적힌 것이 이기고, 비어 있을 때만 표의 기본 이름을 쓴다 —
        /// 세워 둔 보관함마다 다른 이름을 붙일 수 있어야 하기 때문이다.
        /// </summary>
        public string DisplayName => string.IsNullOrEmpty(displayName)
            ? Loc.T("World", "storage_default")
            : displayName;

        public event Action<StorageContainer> Opened;

        public string InteractionPrompt => Loc.F("Prompt", "storage_open", DisplayName);

        public bool CanInteract(PlayerContext player) => player?.Inventory != null;

        public void Interact(PlayerContext player)
        {
            openFeedback?.PlayFeedbacks();
            Opened?.Invoke(this);

            if (GameServices.TryGet<Survive.UI.StorageUI>(out var ui))
                ui.Open(this, player);
        }

        void OnEnable()
        {
            if (GameServices.TryGet<SaveCoordinator>(out var coord))
                coord.Service?.Register(this);
        }

        // 등록만 하고 빠지지 않으면, 부순 보관함이 저장 목록에 죽은 채로 남는다.
        // 다음 저장에서 그 참조를 건드리는 순간 MissingReferenceException이 난다 —
        // 게임은 계속 돌아가서 눈에 안 띄고, 콘솔에만 쌓인다.
        void OnDisable()
        {
            if (GameServices.TryGet<SaveCoordinator>(out var coord))
                coord.Service?.Unregister(this);
        }

        // ── 저장 ─────────────────────────────────────────────────
        //
        // 보관함은 여러 개가 생긴다. SaveKey가 겹치면 나중 것이 앞의 것을 덮어쓴다.
        // 위치를 키에 넣어 구분한다 — 옮길 수 없는 물건이라 위치가 곧 신원이다.
        //
        // <b>다만 세운 보관함은 이 문으로 안 나간다.</b> 아래 CaptureState를 보라.

        [Serializable]
        public class SaveState
        {
            public List<string> itemIds = new List<string>();
            public List<int> counts = new List<int>();
        }

        /// <summary>
        /// <b>이 몸의 주인이 씬인가.</b> 씬이면 제 절을 갖고, 사람이 세운 것이면
        /// 생성 목록의 줄이 내용물까지 함께 싣는다.
        ///
        /// <b>왜 갈라야 하는가.</b> 세운 보관함은 씬에 몸이 없어서 불러올 때
        /// 생성 목록이 다시 만든다. 그 몸이 태어나는 시점은 「세계」 절을 되돌리는
        /// 도중인데, 저장본의 <c>storage_x_y_z</c> 줄은 <b>그 절보다 앞일 수도 뒤일
        /// 수도 있다</b> — 앞이면 아직 없는 몸을 찾다 실패하고, 저장이 물건을 먹는다.
        /// 몸을 만드는 쪽이 내용물도 실으면 순서라는 물음 자체가 없어진다.
        /// </summary>
        bool 씬이_놓은_것 =>
            !TryGetComponent<Survive.Building.BuiltStructure>(out var built) || !built.Spawned;

        public string SaveKey =>
            string.IsNullOrEmpty(saveIdOverride)
                ? $"storage_{Mathf.RoundToInt(transform.position.x)}_" +
                  $"{Mathf.RoundToInt(transform.position.y)}_" +
                  $"{Mathf.RoundToInt(transform.position.z)}"
                : "storage_" + saveIdOverride;

        /// <summary>
        /// 씬이 놓은 보관함만 제 절을 쓴다. 세운 것은 <c>null</c>을 내어
        /// 저장본에서 아예 빠진다 — <b>내용물의 창구가 하나여야</b> 하기 때문이다.
        /// 둘이 쓰면 같은 물건이 두 곳에 실리고, 그 둘이 어긋나는 날 어느 쪽이
        /// 참인지 아무도 모른다.
        ///
        /// <b>읽는 문은 안 닫는다.</b> 아래 <see cref="RestoreState"/>는 그대로다 —
        /// 세운 보관함의 내용물이 제 절에 실려 있던 <b>옛 저장본</b>이 있고,
        /// 그것들은 그 문으로 그냥 열려야 한다.
        /// </summary>
        public object CaptureState()
        {
            if (!씬이_놓은_것) return null;

            var s = new SaveState();
            foreach (var slot in Contents.Slots)
            {
                if (slot.IsEmpty) continue;
                s.itemIds.Add(slot.item.id);
                s.counts.Add(slot.count);
            }
            return s;
        }

        public void RestoreState(object state)
        {
            if (state is not SaveState s) return;

            _contents = new Inventory(slotCount);

            GameServices.TryGet<PlayerInventory>(out var inv);
            var db = inv != null ? inv.Database : null;
            if (db == null) return;

            for (int i = 0; i < s.itemIds.Count && i < s.counts.Count; i++)
            {
                var item = db.GetById(s.itemIds[i]);
                if (item != null) _contents.TryAdd(item, s.counts[i]);
            }
        }
    }
}
