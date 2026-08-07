using System;
using System.Collections.Generic;

namespace Survive.Building
{
    /// <summary>
    /// 배가 못 떠나는 이유 넷. <b>고쳐야 할 계통의 목록이기도 하다</b>(세계관 §6).
    ///
    /// 넷은 지어낸 것이 아니라 세계관이 이미 적어 둔 것을 그대로 옮긴 것이다.
    /// 여기에 다섯째를 더하려면 세계관부터 고쳐야 한다 —
    /// 그것이 이 열거형을 따로 두는 유일한 이유다.
    /// </summary>
    public enum LanderSystem
    {
        /// <summary>갈 곳을 모른다. 기준이 되는 별이 하나도 잡히지 않았다.</summary>
        Destination,

        /// <summary>연료가 성간 이동에는 턱없이 모자란다.</summary>
        Fuel,

        /// <summary>항행 시스템이 부실해 은하 단위 측량이 불가능하다.</summary>
        Navigation,

        /// <summary>배 자체가 행성간 이동만 감안한 빈약한 것이다.</summary>
        Hull,
    }

    /// <summary>계통 하나를 고치려 했을 때의 답.</summary>
    public enum RefitVerdict
    {
        /// <summary>고쳤다. <b>오늘은 여기 오는 길이 없다</b> — <see cref="LanderRefit.Repairable"/>이 비어 있다.</summary>
        Done,

        /// <summary>이미 고쳐 둔 계통이다.</summary>
        AlreadyDone,

        /// <summary>이 릴리스에서는 열리지 않는다. 사유는 이 하나뿐이다.</summary>
        NotInThisRelease,
    }

    /// <summary>
    /// 착륙선 개조 축. <b>자리만 열어 두고 아무것도 채우지 않았다.</b>
    ///
    /// <b>왜 지금 만드는가.</b> 최종 목표가 이 배다 — 성간 이동 수단을 확보한다는 것이
    /// 이 배를 고치는 일이다(세계관 §7). 그런데 그것은 EA 범위 안에 다 들어오지 않는다.
    /// 자리가 없으면 저장본에 담을 칸도 없고, 저장본에 칸이 없으면 나중에 넣을 때
    /// 옛 저장본이 통째로 무효가 된다. 그래서 <b>칸은 지금 열고 내용은 비워 둔다.</b>
    ///
    /// <b>가짜로 채우지 않았다는 것을 규칙이 스스로 말한다.</b>
    /// <see cref="Repairable"/>이 빈 배열이므로 <see cref="LanderRefitLedger.TryComplete"/>는
    /// 어떤 계통에도 <see cref="RefitVerdict.Done"/>을 돌려줄 수 없다. 단계표도,
    /// 재료표도, 진행률도 없다 — 있는 것은 <b>고쳤는가/아닌가</b> 넷뿐이다.
    /// 없는 것을 있는 척하는 진행률 막대 하나가 「최종 목표」를 잡무로 만든다.
    ///
    /// 낫의 회수 지시가 개체 쪽에서는 갈 길이 없는 상태로 열려 있는 것과 같은
    /// 모양이다(<c>ScytheFsm.Next</c>) — 통로는 있고 그리로 가는 길만 없다.
    /// </summary>
    public static class LanderRefit
    {
        /// <summary>못 떠나는 이유 넷. 순서는 세계관 §6이 적어 둔 순서다.</summary>
        public static readonly LanderSystem[] Systems =
        {
            LanderSystem.Destination,
            LanderSystem.Fuel,
            LanderSystem.Navigation,
            LanderSystem.Hull,
        };

        /// <summary>
        /// <b>이 릴리스에서 실제로 고칠 수 있는 계통. 비어 있다.</b>
        ///
        /// 하나를 여는 날 여기에 한 줄을 더하면 되고, 그때 게이트가 빨개져서
        /// 사람이 한 번 더 보게 된다. 그것이 이 배열이 존재하는 이유다.
        /// </summary>
        public static readonly LanderSystem[] Repairable = new LanderSystem[0];

        public static bool CanRepair(LanderSystem system) =>
            Array.IndexOf(Repairable, system) >= 0;

        /// <summary>저장본에 적는 이름. 열거값 번호를 적으면 순서를 바꾸는 날 어긋난다.</summary>
        public static string Id(LanderSystem system) => system switch
        {
            LanderSystem.Destination => "destination",
            LanderSystem.Fuel        => "fuel",
            LanderSystem.Navigation  => "navigation",
            LanderSystem.Hull        => "hull",
            _                        => null,
        };

        public static bool TryParse(string id, out LanderSystem system)
        {
            for (int i = 0; i < Systems.Length; i++)
            {
                if (!string.Equals(Id(Systems[i]), id, StringComparison.Ordinal)) continue;
                system = Systems[i];
                return true;
            }

            system = default;
            return false;
        }
    }

    /// <summary>
    /// 어느 계통을 고쳤는지 적어 두는 장부. 저장본이 담는 것이 이것이다.
    ///
    /// <b>모르는 이름은 건너뛴다.</b> 저장본은 열쇠-값 목록이라 덧붙임이 자유롭고,
    /// 그 자유는 <b>읽는 쪽이 모르는 것을 조용히 넘길 때만</b> 성립한다.
    /// 다음 릴리스에서 저장한 것을 이 릴리스가 열어도 터지지 않아야 한다.
    /// </summary>
    public class LanderRefitLedger
    {
        readonly HashSet<LanderSystem> _done = new HashSet<LanderSystem>();

        /// <summary>고쳐 둔 계통 수.</summary>
        public int DoneCount => _done.Count;

        public bool IsDone(LanderSystem system) => _done.Contains(system);

        /// <summary>
        /// 넷을 다 고쳤는가. <b>참이 되는 날이 이 게임의 끝이다.</b>
        /// 오늘 게임 안에는 여기 닿는 길이 없다 — <see cref="LanderRefit.Repairable"/>이
        /// 비어 있으므로 <see cref="TryComplete"/>가 한 칸도 채우지 못한다.
        /// </summary>
        public bool IsSpacefaring => _done.Count >= LanderRefit.Systems.Length;

        /// <summary>
        /// 계통 하나를 고친다. 열려 있지 않은 계통은 <see cref="RefitVerdict.NotInThisRelease"/>다.
        /// </summary>
        public RefitVerdict TryComplete(LanderSystem system)
        {
            if (_done.Contains(system)) return RefitVerdict.AlreadyDone;
            if (!LanderRefit.CanRepair(system)) return RefitVerdict.NotInThisRelease;

            _done.Add(system);
            return RefitVerdict.Done;
        }

        /// <summary>저장본에 적을 이름들. 순서는 <see cref="LanderRefit.Systems"/>를 따른다.</summary>
        public List<string> Capture()
        {
            var ids = new List<string>();
            foreach (var s in LanderRefit.Systems)
                if (_done.Contains(s)) ids.Add(LanderRefit.Id(s));
            return ids;
        }

        /// <summary>저장본에서 되돌린다. 모르는 이름은 건너뛴다.</summary>
        public void Restore(IEnumerable<string> ids)
        {
            _done.Clear();
            if (ids == null) return;

            foreach (var id in ids)
                if (LanderRefit.TryParse(id, out var s)) _done.Add(s);
        }
    }
}
