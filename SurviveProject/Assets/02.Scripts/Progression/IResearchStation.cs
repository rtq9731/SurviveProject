using Survive.Items;

namespace Survive.Progression
{
    /// <summary>
    /// 연구를 걸어 둘 수 있는 자리. 지금은 연구대 하나뿐이다.
    ///
    /// <see cref="Survive.Crafting.ICraftStation"/>과 같은 규격이다 — 스테이션이 자기
    /// 큐를 들고 스스로 돌리고, 화면은 이 창구만 본다. 그래서 제작 화면은 연구대를
    /// 제작대와 같은 방식으로 연다(<c>CraftingUI.Open</c>).
    ///
    /// <b>ICraftStation을 그대로 쓰지 않는 이유.</b> 그쪽의 <c>Work</c>에는 완성품이
    /// 쌓이는 회수함(<c>StationCraftQueue.Output</c>)이 딸려 있다. 연구의 산출물은
    /// 아이템이 아니라 원장의 한 줄이라 회수함이 영원히 비어 있게 되고, 그러면
    /// "돌아와서 가져간다"는 스테이션의 계약이 연구대에서만 거짓이 된다. 규격은
    /// 따르되 거짓말하는 필드는 물려받지 않는다.
    /// </summary>
    public interface IResearchStation
    {
        /// <summary>화면 제목에 쓴다.</summary>
        string StationName { get; }

        /// <summary>이 자리에서 볼 수 있는 항목들.</summary>
        ResearchBookSO Book { get; }

        /// <summary>이 자리에 귀속된 대기열.</summary>
        ResearchQueue Work { get; }

        /// <summary>태울 것. 보통 스크랩.</summary>
        ItemDataSO EnergyItem { get; }

        /// <summary>지금 진행할 수 있는가.</summary>
        bool IsPowered { get; }

        /// <summary>진행이 멈춘 이유. 돌아가는 중이면 null.</summary>
        string PausedReason { get; }
    }
}
