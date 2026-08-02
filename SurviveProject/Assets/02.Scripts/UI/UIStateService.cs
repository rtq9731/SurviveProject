using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Survive.Core;
using Survive.InputSystem;

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
    /// </summary>
    [DisallowMultipleComponent]
    public class UIStateService : MonoBehaviour
    {
        [SerializeField] InputReaderSO input;

        [Tooltip("ESC로 닫을 패널들. 비워 두면 씬에서 자동으로 찾는다")]
        [SerializeField] MonoBehaviour[] panelBehaviours;

        readonly List<IClosablePanel> _panels = new List<IClosablePanel>();
        readonly List<IClosablePanel> _열린순서 = new List<IClosablePanel>();

        public bool AnyPanelOpen => _panels.Any(p => p != null && p.IsOpen);

        void OnEnable()
        {
            GameServices.Register(this);
            if (input != null)
            {
                input.PauseEvent += OnEscape;
                input.CancelEvent += OnEscape;
            }
            StartCoroutine(수집());
        }

        void OnDisable()
        {
            GameServices.Unregister<UIStateService>();
            if (input == null) return;
            input.PauseEvent -= OnEscape;
            input.CancelEvent -= OnEscape;
        }

        IEnumerator 수집()
        {
            yield return null;   // 패널들의 Awake가 끝나기를 기다린다

            _panels.Clear();

            if (panelBehaviours != null)
            {
                foreach (var b in panelBehaviours)
                    if (b is IClosablePanel p) _panels.Add(p);
            }

            // 인스펙터에서 지정하지 않았으면 씬에서 찾는다.
            // 패널을 새로 추가할 때마다 배선을 잊는 것을 막는다.
            if (_panels.Count == 0)
            {
                foreach (var mb in FindObjectsByType<MonoBehaviour>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (mb is IClosablePanel p && !_panels.Contains(p)) _panels.Add(p);
                }
            }
        }

        /// <summary>패널이 열릴 때 스스로 알린다. 닫는 순서를 정하기 위해서다.</summary>
        public void NotifyOpened(IClosablePanel panel)
        {
            if (panel == null) return;
            _열린순서.Remove(panel);
            _열린순서.Add(panel);
            if (!_panels.Contains(panel)) _panels.Add(panel);
        }

        public void NotifyClosed(IClosablePanel panel) => _열린순서.Remove(panel);

        void OnEscape()
        {
            // 가장 나중에 연 것부터 닫는다
            for (int i = _열린순서.Count - 1; i >= 0; i--)
            {
                var p = _열린순서[i];
                if (p == null) { _열린순서.RemoveAt(i); continue; }
                if (!p.IsOpen) { _열린순서.RemoveAt(i); continue; }

                p.Close();
                return;
            }

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
            _열린순서.Clear();
        }
    }
}
