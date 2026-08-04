using System;
using System.Collections.Generic;

namespace Survive.UI
{
    /// <summary>
    /// 열린 패널의 순서를 기억한다.
    ///
    /// ESC 한 번에 무엇이 닫혀야 하는가는 순수하게 순서의 문제다.
    /// 그 판단을 여기 모아 두면 Unity 없이 확인할 수 있다 —
    /// UIStateService는 이 스택이 고른 것을 실제로 닫기만 한다.
    ///
    /// 규칙은 셋뿐이다.
    /// <list type="number">
    /// <item>연 순서대로 쌓는다. 이미 쌓여 있던 것을 다시 열면 맨 위로 올라온다.</item>
    /// <item>닫을 때는 가장 나중에 연 것부터 하나씩.</item>
    /// <item>전체 닫기는 스택을 비운다.</item>
    /// </list>
    ///
    /// <typeparamref name="T"/>는 참조 형식이면 무엇이든 된다. 패널이
    /// MonoBehaviour인지 아닌지는 이 클래스가 알 필요가 없다.
    /// </summary>
    public sealed class PanelStack<T> where T : class
    {
        readonly List<T> _order = new List<T>();

        /// <summary>쌓여 있는 개수. 이미 닫힌 것이 섞여 있을 수 있다 — <see cref="PeekOpen"/>이 걸러낸다.</summary>
        public int Count => _order.Count;

        /// <summary>아래(먼저 연 것)부터 위(나중에 연 것)로.</summary>
        public IReadOnlyList<T> OpenOrder => _order;

        /// <summary>
        /// 연 것을 맨 위에 올린다. 이미 있으면 자리를 옮긴다 —
        /// 방금 다시 연 것이 ESC에 먼저 반응해야 하기 때문이다.
        /// </summary>
        public void Push(T panel)
        {
            if (panel == null) return;
            _order.Remove(panel);
            _order.Add(panel);
        }

        /// <summary>닫힌 것을 뺀다. 없던 것이면 아무 일도 일어나지 않는다.</summary>
        public bool Remove(T panel)
        {
            if (panel == null) return false;
            return _order.Remove(panel);
        }

        public bool Contains(T panel) => panel != null && _order.Contains(panel);

        /// <summary>전체 닫기. 실제로 닫는 것은 호출한 쪽이 하고, 여기서는 기록만 지운다.</summary>
        public void Clear() => _order.Clear();

        /// <summary>
        /// 지금 ESC로 닫아야 할 패널. 없으면 null.
        ///
        /// 위에서부터 내려오며 <paramref name="isOpen"/>이 아니라고 답한 것은
        /// 지우면서 지나간다. 파괴됐거나 다른 경로로 이미 닫힌 패널이
        /// 스택 꼭대기에 남아 ESC를 한 번 삼키는 일을 막는다.
        ///
        /// 찾은 것을 스택에서 빼지는 않는다. 실제로 닫히면 그때
        /// <see cref="Remove"/>가 온다 — 닫히지 않았는데 기록만 사라지면
        /// 다음 ESC가 엉뚱한 패널로 간다.
        /// </summary>
        public T PeekOpen(Func<T, bool> isOpen)
        {
            if (isOpen == null) throw new ArgumentNullException(nameof(isOpen));

            for (int i = _order.Count - 1; i >= 0; i--)
            {
                var p = _order[i];
                if (!isOpen(p)) { _order.RemoveAt(i); continue; }
                return p;
            }
            return null;
        }
    }
}
