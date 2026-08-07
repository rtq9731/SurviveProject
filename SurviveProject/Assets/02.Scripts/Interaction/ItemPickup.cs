using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Audio;
using Survive.Domain.Audio;
using Survive.Items;
using Survive.Localization;
using Survive.Player;

namespace Survive.Interaction
{
    /// <summary>바닥에 떨어져 있는 아이템.</summary>
    public class ItemPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] ItemDataSO item;
        [Min(1)] [SerializeField] int count = 1;

        [Tooltip("획득 성공 시 재생. 획득음·파티클")]
        [SerializeField] MMF_Player pickupFeedback;

        [Tooltip("주울 때 소리. 비우면 소리 표의 itemPickup")]
        [SerializeField] AudioCueSO pickupCue;

        /// <summary>
        /// 화면 한가운데에 뜨는 한 줄. <b>수량이 있고 없고를 두 문장으로 갈랐다</b> —
        /// "…줍기"에 "×3"을 이어 붙이면 그 조각의 자리를 번역가가 옮길 수 없다.
        /// </summary>
        public string InteractionPrompt =>
            item == null ? ""
            : count > 1 ? Loc.F("Prompt", "pickup_many", DataText.Name(item), count)
                        : Loc.F("Prompt", "pickup", DataText.Name(item));

        public bool CanInteract(PlayerContext player) => item != null && player?.Inventory != null;

        public void Interact(PlayerContext player)
        {
            int remaining = player.Inventory.Add(item, count);
            if (remaining <= 0)
            {
                pickupFeedback?.PlayFeedbacks();
                PlayPickupSound();
                Destroy(gameObject);
                return;
            }

            // 일부만 들어갔으면 남은 만큼만 남긴다
            if (remaining != count)
            {
                pickupFeedback?.PlayFeedbacks();
                PlayPickupSound();
                count = remaining;
            }
        }

        /// <summary>
        /// 이 오브젝트는 다음 줄에서 사라질 수 있다. 자기 몸에 붙은 AudioSource로 냈다면
        /// 소리가 시작하자마자 잘린다 — 그래서 창구(<see cref="AudioService"/>)에 맡긴다.
        /// </summary>
        void PlayPickupSound()
        {
            var book = AudioService.Book;
            AudioService.Play(AudioCueBookSO.Or(pickupCue, book != null ? book.itemPickup : null),
                              transform.position);
        }

        /// <summary>런타임 생성용 (전리품 드롭 등).</summary>
        public void Setup(ItemDataSO newItem, int newCount)
        {
            item = newItem;
            count = Mathf.Max(1, newCount);

            Spawned = true;
            RestAt = transform.position;
        }

        // ── 생성 목록이 보는 것 ──────────────────────────────────

        /// <summary>무엇이 떨어져 있는가.</summary>
        public ItemDataSO Item => item;

        /// <summary>몇 개 떨어져 있는가. 일부만 주우면 남은 수로 줄어든다.</summary>
        public int Count => count;

        /// <summary>
        /// <b>실행 중에 태어났는가.</b> <see cref="Setup"/>을 지난 것만 참이다 —
        /// 씬에 미리 놓인 줍기 대상은 씬이 존재의 주인이므로 생성 목록이
        /// 담지 않는다 (<c>BuiltStructure.Spawned</c>와 같은 경계).
        /// </summary>
        public bool Spawned { get; private set; }

        /// <summary>
        /// <b>내려앉은 자리.</b> 저장본에 실리는 것은 <c>transform.position</c>이
        /// 아니라 이 값이다.
        ///
        /// 떨어진 물건은 착지 뒤에도 위아래로 떠다닌다(<c>ItemDropper.Idle</c>).
        /// 지금 자리를 적으면 저장할 때마다 그 물결의 아무 지점이나 집게 되고,
        /// 불러온 뒤 그 높이에서 다시 떠다니므로 <b>저장·불러오기를 되풀이할수록
        /// 물건이 조금씩 떠오른다</b>. 착지 지점은 안 흔들린다.
        /// </summary>
        public Vector3 RestAt { get; private set; }

        /// <summary>착지 지점을 알려 준다. 떨구는 쪽(<c>ItemDropper</c>)이 부른다.</summary>
        public void Adopt(Vector3 restAt) => RestAt = restAt;

        static readonly List<ItemPickup> _active = new List<ItemPickup>();

        /// <summary>지금 바닥에 있는 줍기 대상들. 등록된 차례가 곧 떨어진 차례다.</summary>
        public static IReadOnlyList<ItemPickup> Active => _active;

        /// <summary>
        /// 재생을 켤 때마다 새 세계다. 정적 목록은 도메인 리로드를 끈 에디터에서
        /// 앞 판을 살아서 넘어온다 (<c>BuiltStructure</c>와 같은 자리, 같은 이유).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void 판이_바뀌면_비운다() => _active.Clear();

        void OnEnable()
        {
            if (!_active.Contains(this)) _active.Add(this);
        }

        void OnDisable() => _active.Remove(this);
    }
}
