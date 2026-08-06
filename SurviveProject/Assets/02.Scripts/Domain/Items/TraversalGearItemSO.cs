using UnityEngine;
using Survive.World;

namespace Survive.Items
{
    /// <summary>
    /// 지니고 있으면 환경 위협 하나를 뚫어 주는 아이템. P2 스펙 §8-2의 액면 보행 장비가
    /// 첫 사례이고, 그것만을 위해 특수 클래스를 만들지 않는다 —
    /// <see cref="Survive.World.EnvironmentThreat"/>가 위협마다 규칙을 두지 않은 것과 같은 이유다.
    ///
    /// 장비가 내놓는 것은 <b>종류 하나와 용량 하나</b>뿐이고, 판정은 그 둘을 위협과 비교하는 것으로 끝난다.
    /// 그래서 나중에 더 좋은 랜턴이나 더 멀리 가는 액면 보행 장비를 추가할 때
    /// 코드가 아니라 에셋만 늘어난다.
    ///
    /// <b>장착 규칙 — 인벤토리에 있으면 몸에 걸친 것으로 본다.</b> 이 프로젝트는 이미 랜턴에서
    /// 같은 규칙을 택했다(<see cref="Survive.World.LanternRule.EquippedTier"/>: 랜턴은 손에 드는
    /// 도구가 아니라 몸에 다는 조명이고, 자리가 비면 가방에 든 것이 켜진다).
    /// 손 소켓(<see cref="ToolItemSO.socketChildName"/>)은 곡괭이처럼
    /// 휘두르는 물건의 것이고, 발에 신는 액면 보행 장비를 거기 넣으면
    /// 액면을 건너는 동안 곡괭이를 놓아야 한다는, 스펙에 없는 제약이 생긴다.
    /// </summary>
    [CreateAssetMenu(menuName = "Survive/Items/Traversal Gear")]
    public class TraversalGearItemSO : ItemDataSO
    {
        [Tooltip("무엇을 뚫는가")]
        public TraversalGear gear = TraversalGear.SurfaceWalker;

        [Tooltip("감당하는 크기. 단위는 상대하는 위협과 같다 — HazardZone.Magnitude 참고")]
        [Min(0f)] public float capacity;
    }
}
