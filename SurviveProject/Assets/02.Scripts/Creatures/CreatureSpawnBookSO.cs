using UnityEngine;

namespace Survive.Creatures
{
    /// <summary>
    /// <b>런타임이 생물 프리팹을 찾는 창구.</b>
    ///
    /// <b>왜 필요한가.</b> 낫 프리팹은 <c>05.Prefabs/Creatures/</c>에 있어서
    /// <see cref="Resources.Load"/>로는 닿지 않는다. 프리팹을 <c>Resources/</c>로
    /// 옮기면 그 경로를 물고 있는 E2E 시나리오 여럿이 한꺼번에 깨지고, 무엇보다
    /// <b>프리팹은 병합할 수 없는 단일 파일</b>이라 여러 갈래가 도는 동안 건드리지
    /// 않는 것이 이 저장소의 규율이다.
    ///
    /// 그래서 <b>가리키는 종이만 Resources에 둔다.</b> 이 저장소가 이미
    /// <c>AudioCueBookSO</c>·<c>ResearchBookSO</c>·<c>DiscoveryBookSO</c>를
    /// 같은 방식으로 쓰고 있다 — 새 관례가 아니라 있는 관례다.
    /// </summary>
    [CreateAssetMenu(menuName = "Survive/Creature Spawn Book", fileName = "CreatureSpawnBook")]
    public class CreatureSpawnBookSO : ScriptableObject
    {
        /// <summary><see cref="Resources.Load"/>가 찾는 이름.</summary>
        public const string ResourceName = "CreatureSpawnBook";

        [Tooltip("낫. 밤마다 세워지는 개체다")]
        [SerializeField] GameObject scythe;

        /// <summary>낫 프리팹. 없으면 null이고, 부르는 쪽이 조용히 넘어간다.</summary>
        public GameObject Scythe => scythe;
    }
}
