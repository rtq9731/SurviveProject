using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Survive.Core;
using Survive.Input;

namespace Survive.UI
{
    /// <summary>
    /// 열린 UI 패널을 한 곳에서 관리한다.
    ///
    /// ESC를 듣는 곳은 여기 하나뿐이다. 패널마다 각자 듣게 두면
    /// 어떤 것은 닫히고 어떤 것은 안 닫히는 일이 생긴다.
    ///
    /// 여러 개가 열려 있으면 <b>가장 나중에 연 것부터</b> 하나씩 닫는다.
    /// 열린 것이 없으면 일시정지 메뉴를 연다.
    ///
    /// 순서 규칙은 <see cref="PanelStack{T}"/>가, 같이 떠 있어도 되는지는
    /// <see cref="PanelExclusionRules"/>가 판단한다. 둘 다 Unity를 모르는
    /// 순수 클래스라 씬 없이 확인할 수 있다. 여기 남은 일은 그 판단을
    /// 실제 패널에 대고 실행하는 것뿐이다.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIStateService : MonoBehaviour
    {
        [SerializeField] InputReaderSO input;

        [Tooltip("ESC로 닫을 패널들. 비워 두면 씬에서 자동으로 찾는다")]
        [SerializeField] MonoBehaviour[] panelBehaviours;

        readonly List<IClosablePanel> _panels = new List<IClosablePanel>();
        readonly PanelStack<IClosablePanel> _stack = new PanelStack<IClosablePanel>();
        readonly List<IClosablePanel> _conflicts = new List<IClosablePanel>();

        /// <summary>같이 떠 있어도 되는 조합. 지금은 코드에 적힌 표를 그대로 쓴다.</summary>
        public PanelExclusionRules Rules { get; } = PanelExclusionRules.CreateDefault();

        static readonly PanelExclusionRules FallbackRules = PanelExclusionRules.CreateDefault();

        /// <summary>
        /// 지금 유효한 규칙표. 패널이 자기 짝을 닫을지 물을 때 쓴다.
        ///
        /// 서비스가 씬에 없어도 답은 있어야 한다 — 어느 패널이 어느 패널을
        /// 딸고 다니는가는 씬 배선이 아니라 화면 구성의 문제다.
        /// </summary>
        public static PanelExclusionRules ActiveRules
            => GameServices.TryGet<UIStateService>(out var ui) ? ui.Rules : FallbackRules;

        public bool AnyPanelOpen => _panels.Any(p => p != null && p.IsOpen);

        void OnEnable()
        {
            GameServices.Register(this);
            if (input != null)
            {
                input.PauseEvent += OnEscape;
                input.CancelEvent += OnEscape;
            }
            StartCoroutine(CollectPanels());
        }

        void OnDisable()
        {
            // 일시정지 중에 플레이 모드를 멈추면 timeScale이 0인 채로 남는다.
            // 정적 상태라 다음 실행까지 따라와 게임이 얼어붙는다. 반드시 되돌린다.
            if (IsPaused)
            {
                IsPaused = false;
                Time.timeScale = 1f;
            }

            GameServices.Unregister<UIStateService>();
            if (input == null) return;
            input.PauseEvent -= OnEscape;
            input.CancelEvent -= OnEscape;
        }

        IEnumerator CollectPanels()
        {
            yield return null;   // 패널들의 Awake가 끝나기를 기다린다

            // 여기서 목록을 비우지 않는다. 이 한 프레임 사이에 스스로 등록한
            // 패널이 있을 수 있고, 그것까지 지우면 자기등록이 무의미해진다.
            bool wired = false;
            if (panelBehaviours != null)
            {
                foreach (var b in panelBehaviours)
                    if (b is IClosablePanel p) { AddPanel(p); wired = true; }
            }

            // 인스펙터에서 지정하지 않았으면 씬에서 찾는다.
            // 패널을 새로 추가할 때마다 배선을 잊는 것을 막는다.
            if (!wired)
            {
                foreach (var mb in FindObjectsByType<MonoBehaviour>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (mb is IClosablePanel p) AddPanel(p);
                }
            }
        }

        // ── 등록 ─────────────────────────────────────────────────

        /// <summary>
        /// 패널이 살아날 때 스스로 알린다.
        ///
        /// 인스펙터 배선과 씬 스캔은 그대로 둔 채 <b>덧붙이는</b> 경로다.
        /// 씬에 처음부터 놓여 있던 패널은 어느 쪽이 먼저 도착하든 같은 목록에
        /// 한 번만 들어가고, 런타임에 새로 만들어진 패널은 이쪽으로만 들어온다.
        /// </summary>
        public void RegisterPanel(IClosablePanel panel) => AddPanel(panel);

        /// <summary>패널이 사라질 때. 다시 살아나면 다시 등록된다.</summary>
        public void UnregisterPanel(IClosablePanel panel)
        {
            if (panel == null) return;
            _panels.Remove(panel);
            _stack.Remove(panel);
        }

        void AddPanel(IClosablePanel panel)
        {
            if (panel == null || _panels.Contains(panel)) return;
            _panels.Add(panel);
        }

        // ── 열고 닫기 ────────────────────────────────────────────

        /// <summary>패널이 열릴 때 스스로 알린다. 닫는 순서를 정하기 위해서다.</summary>
        public void NotifyOpened(IClosablePanel panel)
        {
            if (panel == null) return;

            CloseSuppressed(panel);
            _stack.Push(panel);
            AddPanel(panel);
        }

        public void NotifyClosed(IClosablePanel panel) => _stack.Remove(panel);

        /// <summary>새로 열린 패널과 같이 떠 있을 수 없는 것들을 닫는다.</summary>
        void CloseSuppressed(IClosablePanel opening)
        {
            var kind = opening.PanelKind;
            if (kind == UIPanelKind.None) return;

            // 닫는 동안 NotifyClosed가 목록을 건드리므로 먼저 골라 놓고 닫는다.
            _conflicts.Clear();
            foreach (var p in _panels)
            {
                if (p == null || ReferenceEquals(p, opening) || !p.IsOpen) continue;
                if (!Rules.CanCoexist(kind, p.PanelKind)) _conflicts.Add(p);
            }

            foreach (var p in _conflicts) p.Close();
            _conflicts.Clear();
        }

        void OnEscape()
        {
            // 가장 나중에 연 것부터 닫는다
            var top = _stack.PeekOpen(p => p != null && p.IsOpen);
            if (top != null) { top.Close(); return; }

            // 열린 순서를 모르는 패널이 있을 수 있다. 열려 있으면 닫는다.
            foreach (var p in _panels)
            {
                if (p != null && p.IsOpen) { p.Close(); return; }
            }

            // 아무것도 안 열려 있으면 일시정지
            TogglePause();
        }

        // ── 일시정지 ─────────────────────────────────────────────

        public bool IsPaused { get; private set; }

        public void TogglePause()
        {
            IsPaused = !IsPaused;
            Time.timeScale = IsPaused ? 0f : 1f;
            Cursor.lockState = IsPaused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = IsPaused;
        }

        public void CloseAll()
        {
            foreach (var p in _panels)
                if (p != null && p.IsOpen) p.Close();
            _stack.Clear();
        }
    }
}
