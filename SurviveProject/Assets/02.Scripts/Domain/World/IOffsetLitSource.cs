using UnityEngine;

namespace Survive.World
{
    /// <summary>
    /// <b>사람을 따라다니되 가운데 두지 않는</b> 광원. 지금은 랜턴 하나뿐이다.
    ///
    /// 고정 광원(화톳불·발광 군락)은 <see cref="ILitZoneSource"/>면 충분하다 —
    /// 제자리에 있고 앞뒤가 없기 때문이다. 랜턴은 다르다. 빛 웅덩이가 사람보다
    /// 조금 앞에 있으므로 <b>등 뒤에 사각이 생기고</b>, 그 사각이 이 게임에서
    /// 낫이 사람에게 닿을 수 있는 유일한 길이다(기획서 §9).
    ///
    /// 사각을 알려면 원 하나로는 모자란다. 어디까지 밝은가는
    /// <see cref="ILitZoneSource.LitZoneCenter"/>가 이미 답하지만, <b>어느 쪽이 뒤인가</b>는
    /// 사람이 선 자리와 보는 방향을 함께 알아야 답할 수 있다. 그 둘만 더 내놓는다.
    /// </summary>
    public interface IOffsetLitSource : ILitZoneSource
    {
        /// <summary>빛 웅덩이가 매달려 있는 자리 — 곧 <b>사람이 선 자리</b>다.</summary>
        Vector3 LitAnchor { get; }

        /// <summary>웅덩이를 밀어낸 쪽. 수평 단위벡터이고, 정할 수 없으면 0이다.</summary>
        Vector3 LitForward { get; }
    }
}
