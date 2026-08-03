using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;
using Survive.Core;
using Survive.Items;
using Survive.Progression;

namespace Survive.UI
{
    /// <summary>
    /// 조작키를 필요한 순간에 한 번씩 알려준다.
    ///
    /// 곡괭이를 만들어 놓고도 손에 드는 법을 몰라 광맥을 못 캐는 일이 실제로 있었다.
    /// 게임 안 어디에도 "Q = 도구 교체"가 적혀 있지 않았기 때문이다.
    ///
    /// 좌하단에 상시 띄우는 방법도 있지만 화면이 어지러워진다.
    /// 그 키가 처음 필요해지는 순간에만 띄우고 스스로 사라지게 한다 —
    /// 이미 아는 사람에게는 보이지 않는 것이 가장 좋은 안내다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ControlHintView : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] TMP_Text label;

        [Tooltip("한 줄을 띄워 두는 시간")]
        [SerializeField] float holdSeconds = 4.5f;

        [SerializeField] float fadeSeconds = 0.35f;

        readonly Queue<string> _pending = new Queue<string>();
        readonly HashSet<string> _shown = new HashSet<string>();

        PlayerInventory _inventory;
        ChapterDirector _chapter;
        bool _running;

        void Awake()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 0f;
        }

        IEnumerator Start()
        {
            // 플레이어와 챕터가 준비되기를 기다린다
            for (int i = 0; i < 180 && _inventory == null; i++)
            {
                _inventory = Object.FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Exclude);
                if (_inventory != null) break;
                yield return null;
            }

            _chapter = Object.FindFirstObjectByType<ChapterDirector>(FindObjectsInactive.Exclude);

            Show("basics", "[WASD] 이동   [E] 상호작용   [Tab] 소지품");

            if (_inventory?.Inventory != null)
                _inventory.Inventory.Changed += OnInventoryChanged;
        }

        void OnDestroy()
        {
            if (_inventory?.Inventory != null)
                _inventory.Inventory.Changed -= OnInventoryChanged;
        }

        void OnInventoryChanged()
        {
            var inv = _inventory?.Inventory;
            if (inv == null) return;

            // 도구는 손에 들어야 쓸 수 있다. 만든 그 순간에 알려준다.
            if (inv.Has("pickaxe", 1))
                Show("pickaxe", "[Q] 도구를 손에 든다 · 곡괭이로 광맥을 부순다");

            if (inv.Has("lantern", 1))
                Show("lantern", "[F] 랜턴 켜기 / 끄기");
        }

        /// <summary>같은 안내는 한 번만 띄운다.</summary>
        public void Show(string key, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (!string.IsNullOrEmpty(key) && !_shown.Add(key)) return;

            _pending.Enqueue(text);
            if (!_running) StartCoroutine(Drain());
        }

        IEnumerator Drain()
        {
            _running = true;

            while (_pending.Count > 0)
            {
                string text = _pending.Dequeue();
                if (label != null) label.text = text;

                group?.DOKill();
                group?.DOFade(1f, fadeSeconds).SetUpdate(true);

                float t = 0f;
                while (t < holdSeconds) { t += Time.unscaledDeltaTime; yield return null; }

                group?.DOKill();
                group?.DOFade(0f, fadeSeconds).SetUpdate(true);

                float f = 0f;
                while (f < fadeSeconds) { f += Time.unscaledDeltaTime; yield return null; }
            }

            _running = false;
        }
    }
}
