using System;

namespace Survive.Crafting
{
    /// <summary>
    /// 제작을 걸어 둘 수 있는 자리 — 제작대, 화톳불, 언젠가의 화로.
    ///
    /// 제작 UI는 이 창구만 보고 동작한다. 그래서 새 스테이션을 붙이는 일이
    /// "이 인터페이스를 구현하고 레시피에 StationType을 적는" 두 걸음으로 끝난다.
    /// </summary>
    public interface ICraftStation
    {
        StationType StationType { get; }

        /// <summary>제작 화면 제목에 쓴다.</summary>
        string StationName { get; }

        /// <summary>이 자리에 귀속된 대기열과 회수함.</summary>
        StationCraftQueue Work { get; }

        /// <summary>지금 진행할 수 있는가. 화톳불은 불이 타고 있어야 한다.</summary>
        bool IsPowered { get; }

        /// <summary>진행이 멈춘 이유. 돌아가는 중이면 null.</summary>
        string PausedReason { get; }

        /// <summary>
        /// 스테이션 고유 동작. 없으면 null.
        /// 화톳불의 연료 보급이 이것이다 — 제작과 성질이 달라 레시피로 적을 수 없다.
        /// </summary>
        StationSideAction SideAction { get; }
    }

    /// <summary>제작 화면에 한 줄 더 붙는 스테이션 전용 버튼.</summary>
    public sealed class StationSideAction
    {
        public StationSideAction(Func<string> label, Func<bool> canRun, Action run)
        {
            Label = label;
            CanRun = canRun;
            Run = run;
        }

        public Func<string> Label { get; }
        public Func<bool> CanRun { get; }
        public Action Run { get; }
    }
}
