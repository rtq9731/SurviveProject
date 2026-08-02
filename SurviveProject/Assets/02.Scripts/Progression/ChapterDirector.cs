using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Survive.Core;
using Survive.Items;
using Survive.Progression;

namespace Survive.Progression
{
    /// <summary>
    /// 챕터의 목표를 순차로 진행시킨다.
    /// 도메인의 IObjectiveContext를 구현해 목표가 MonoBehaviour를 모르게 한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChapterDirector : MonoBehaviour, IObjectiveContext
    {
        [SerializeField] ChapterSO chapter;

        [Tooltip("목표 완료를 확인하는 간격(초). 매 프레임 돌 필요가 없다")]
        [SerializeField] float checkInterval = 0.25f;

        readonly Dictionary<string, int> _flags = new Dictionary<string, int>();
        PlayerInventory _inventory;

        public ObjectiveSO Current =>
            chapter != null && CurrentIndex >= 0 && CurrentIndex < chapter.objectives.Length
                ? chapter.objectives[CurrentIndex]
                : null;

        public int CurrentIndex { get; private set; }

        public event Action<ObjectiveSO> ObjectiveChanged;
        public event Action<ChapterSO> ChapterCompleted;

        // ── IObjectiveContext ────────────────────────────────────
        public Inventory PlayerInventory => _inventory?.Inventory;
        public int GetFlag(string key) => _flags.TryGetValue(key, out var v) ? v : 0;

        // ── 플래그 조작 ──────────────────────────────────────────
        public void SetFlag(string key, int value)
        {
            if (string.IsNullOrEmpty(key)) return;
            _flags[key] = value;
        }

        public void AddFlag(string key, int delta = 1)
        {
            if (string.IsNullOrEmpty(key)) return;
            _flags[key] = GetFlag(key) + delta;
        }

        void OnEnable() => GameServices.Register(this);
        void OnDisable() => GameServices.Unregister<ChapterDirector>();

        void Start() => StartCoroutine(진행());

        IEnumerator 진행()
        {
            yield return null;
            GameServices.TryGet<PlayerInventory>(out _inventory);

            CurrentIndex = 0;
            ObjectiveChanged?.Invoke(Current);

            var 대기 = new WaitForSeconds(checkInterval);
            while (chapter != null && CurrentIndex < chapter.objectives.Length)
            {
                var 목표 = Current;
                if (목표 != null && 목표.IsComplete(this)) 다음으로();
                yield return 대기;
            }
        }

        void 다음으로()
        {
            CurrentIndex++;
            if (chapter != null && CurrentIndex >= chapter.objectives.Length)
            {
                ObjectiveChanged?.Invoke(null);
                ChapterCompleted?.Invoke(chapter);
                return;
            }
            ObjectiveChanged?.Invoke(Current);
        }

        /// <summary>PlayMode 스모크 테스트용. 현재 목표를 강제로 넘긴다.</summary>
        public void ForceCompleteCurrent() => 다음으로();

        public float CurrentProgress => Current != null ? Current.Evaluate(this) : 0f;
    }
}
