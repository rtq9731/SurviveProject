using UnityEngine;
using Survive.Interaction;
using Survive.Items;
using Survive.Player;

namespace Survive.Building
{
    /// <summary>
    /// 세운 것을 부수고 재료를 돌려준다.
    ///
    /// 없으면 잘못 지은 순간 재료가 그대로 날아간다. 건설이 "한 번 실수하면
    /// 되돌릴 수 없는 일"이 되면 아무도 실험하지 않는다.
    ///
    /// IInteractable로 만들지 않은 이유: 보관함은 E로 열어야 하는데 같은 오브젝트에
    /// 상호작용이 둘이면 어느 쪽이 잡힐지 알 수 없다. 철거는 R 홀드라는 별도 경로로
    /// 두고, BuildModeController가 조준과 진행도를 맡는다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BuiltStructure))]
    public class StructureDemolisher : MonoBehaviour
    {
        [Tooltip("부수는 데 걸리는 시간")]
        [SerializeField] float holdSeconds = 1.2f;

        [Tooltip("돌려받는 비율. 1이면 전액 환급")]
        [Range(0f, 1f)] [SerializeField] float refundRatio = 1f;

        BuiltStructure _structure;

        void Awake() => _structure = GetComponent<BuiltStructure>();

        public float HoldSeconds => holdSeconds;

        public string DisplayName
        {
            get
            {
                if (_structure == null) _structure = GetComponent<BuiltStructure>();
                var d = _structure != null ? _structure.Definition : null;
                return d != null ? d.displayName : "구조물";
            }
        }

        public void Demolish(PlayerContext player)
        {
            var d = _structure != null ? _structure.Definition : null;

            if (d?.cost != null && player?.Inventory != null)
            {
                foreach (var c in d.cost)
                {
                    if (c?.item == null) continue;
                    int give = Mathf.FloorToInt(c.count * refundRatio);
                    if (give <= 0) continue;

                    // 인벤토리가 가득 차면 바닥에 떨군다. 조용히 사라지면 안 된다.
                    int left = player.Inventory.Add(c.item, give);
                    if (left > 0)
                        ItemDropper.Drop(c.item, left, transform.position + Vector3.up * 0.4f);
                }
            }

            // 보관함이라면 안의 물건도 돌려준다.
            // 통을 부쉈다고 내용물이 증발하면 그건 버그로 보인다.
            var storage = GetComponent<StorageContainer>();
            if (storage != null)
            {
                foreach (var slot in storage.Contents.Slots)
                {
                    if (slot.IsEmpty) continue;
                    ItemDropper.Drop(slot.item, slot.count, transform.position + Vector3.up * 0.5f);
                }
            }

            Destroy(gameObject);
        }
    }
}
