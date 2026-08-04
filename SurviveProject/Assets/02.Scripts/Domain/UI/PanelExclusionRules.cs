using System.Collections.Generic;

namespace Survive.UI
{
    /// <summary>
    /// 어떤 패널들이 같이 떠 있어도 되는가를 적은 표.
    ///
    /// 예전에는 이 판단이 패널 안에 흩어져 있었다 — 보관함이 열릴 때
    /// "손 제작이 켜져 있으면 끈다"를 보관함 스스로 알고 있었다.
    /// 그러면 규칙 하나를 확인하려고 패널 넷을 다 읽어야 한다.
    /// 표로 모아 두면 규칙만 따로 볼 수 있고, 씬 없이 검증할 수 있다.
    ///
    /// 밀어내기는 <b>방향이 있다</b>. 나중에 연 쪽이 이긴다는 뜻이 아니라,
    /// "보관함이 열리면 손 제작이 닫힌다"와 "손 제작이 열리면 보관함이 닫힌다"가
    /// 서로 다른 규칙이라는 뜻이다. 실제로 보관함은 열리면서 소지품을 같이 여는데,
    /// 소지품이 손 제작을 딸려 연다. 양방향으로 적어 두면 그 순간 보관함이
    /// 제 손으로 부른 목록에 밀려 닫힌다. 한쪽만 적어야 화면이 의도대로 남는다.
    /// 양쪽 다 필요할 때는 <see cref="Mutual"/>로 두 줄을 한 번에 적는다.
    /// </summary>
    public sealed class PanelExclusionRules
    {
        readonly List<Rule> _suppress = new List<Rule>();
        readonly List<Rule> _closeTogether = new List<Rule>();

        readonly struct Rule
        {
            public readonly UIPanelKind From;
            public readonly UIPanelKind To;
            public Rule(UIPanelKind from, UIPanelKind to) { From = from; To = to; }
        }

        // ── 규칙 적기 ────────────────────────────────────────────────

        /// <summary><paramref name="opener"/>가 열리면 <paramref name="target"/>은 닫힌다.</summary>
        public PanelExclusionRules Suppresses(UIPanelKind opener, UIPanelKind target)
        {
            // 자기 자신을 밀어내는 규칙은 뜻이 없다. 받아들이면 열자마자 닫힌다.
            if (opener == target || opener == UIPanelKind.None || target == UIPanelKind.None)
                return this;

            if (!Has(_suppress, opener, target)) _suppress.Add(new Rule(opener, target));
            return this;
        }

        /// <summary>둘 중 어느 쪽이 열리든 다른 쪽이 닫힌다 — 전체화면 패널끼리의 상호 배타.</summary>
        public PanelExclusionRules Mutual(UIPanelKind a, UIPanelKind b)
            => Suppresses(a, b).Suppresses(b, a);

        /// <summary>
        /// <paramref name="closer"/>를 닫으면 <paramref name="companion"/>도 같이 닫는다.
        /// 딸려 열린 것은 딸려 닫혀야 한다 — 소지품을 닫았는데 손 제작 목록만
        /// 화면에 남아 있으면 그것을 닫을 방법이 따로 없다.
        /// </summary>
        public PanelExclusionRules ClosesWith(UIPanelKind closer, UIPanelKind companion)
        {
            if (closer == companion || closer == UIPanelKind.None || companion == UIPanelKind.None)
                return this;

            if (!Has(_closeTogether, closer, companion)) _closeTogether.Add(new Rule(closer, companion));
            return this;
        }

        // ── 규칙 묻기 ────────────────────────────────────────────────

        /// <summary>
        /// <paramref name="opening"/>이 열릴 때 <paramref name="alreadyOpen"/>이 남아 있어도 되는가.
        /// 방향이 있으므로 인자 순서를 바꾸면 답이 달라질 수 있다.
        /// </summary>
        public bool CanCoexist(UIPanelKind opening, UIPanelKind alreadyOpen)
            => !Has(_suppress, opening, alreadyOpen);

        /// <summary><paramref name="closer"/>를 닫을 때 <paramref name="companion"/>도 닫아야 하는가.</summary>
        public bool ClosesTogether(UIPanelKind closer, UIPanelKind companion)
            => Has(_closeTogether, closer, companion);

        /// <summary>
        /// <paramref name="opening"/>이 열릴 때 닫아야 할 것들을 <paramref name="into"/>에 담는다.
        /// 목록은 비우지 않고 덧붙인다 — 호출한 쪽이 재사용 버퍼를 쓰기 때문이다.
        /// </summary>
        public void CollectSuppressed(UIPanelKind opening, IEnumerable<UIPanelKind> openKinds, List<UIPanelKind> into)
        {
            if (openKinds == null || into == null) return;

            foreach (var k in openKinds)
                if (!CanCoexist(opening, k) && !into.Contains(k)) into.Add(k);
        }

        static bool Has(List<Rule> rules, UIPanelKind from, UIPanelKind to)
        {
            for (int i = 0; i < rules.Count; i++)
                if (rules[i].From == from && rules[i].To == to) return true;
            return false;
        }

        // ── 이 게임의 규칙 ───────────────────────────────────────────

        /// <summary>
        /// 지금 화면이 실제로 따르고 있는 규칙. 새 인스턴스를 돌려준다 —
        /// 하나를 돌려쓰면 어딘가에서 규칙을 덧붙인 것이 다른 곳까지 따라간다.
        ///
        /// <list type="bullet">
        /// <item>보관함은 손 제작을 밀어낸다. 둘이 같은 자리를 쓰고, 상자 앞에 선
        ///       사람이 하려는 것은 제작이 아니라 정리다. 제작대에서 연 목록은
        ///       밀어내지 않는다 — 그쪽은 제작대 앞에서만 뜬다.</item>
        /// <item>소지품을 닫으면 손 제작도 닫힌다. 소지품이 딸려 연 목록이기 때문이다.</item>
        /// </list>
        /// </summary>
        public static PanelExclusionRules CreateDefault()
            => new PanelExclusionRules()
                .Suppresses(UIPanelKind.Storage, UIPanelKind.HandCrafting)
                .ClosesWith(UIPanelKind.Inventory, UIPanelKind.HandCrafting);
    }
}
