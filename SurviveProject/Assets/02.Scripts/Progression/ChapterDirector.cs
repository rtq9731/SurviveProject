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
    public class ChapterDirector : MonoBehaviour, IObjectiveContext, ISaveable
    {
        [SerializeField] ChapterSO chapter;

        [Tooltip("목표 완료를 확인하는 간격(초). 매 프레임 돌 필요가 없다")]
        [SerializeField] float checkInterval = 0.25f;

        readonly Dictionary<string, int> _flags = new Dictionary<string, int>();
        PlayerInventory _inventory;
        bool _restored;

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

        void Start() => StartCoroutine(RunChapter());

        IEnumerator RunChapter()
        {
            yield return null;
            GameServices.TryGet<PlayerInventory>(out _inventory);

            // 불러오기가 Start보다 먼저 끝났을 수 있다. 그때 0으로 되돌리면
            // 복원한 진행도가 사라진다.
            if (!_restored) CurrentIndex = 0;
            ObjectiveChanged?.Invoke(Current);

            var wait = new WaitForSeconds(checkInterval);
            while (chapter != null && CurrentIndex < chapter.objectives.Length)
            {
                var objective = Current;
                if (objective != null && objective.IsComplete(this)) Advance();
                yield return wait;
            }
        }

        void Advance()
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
        public void ForceCompleteCurrent() => Advance();

        public float CurrentProgress => Current != null ? Current.Evaluate(this) : 0f;

        // ── 저장 ─────────────────────────────────────────────────
        //
        // 목표 번호만 저장하면 안 된다. 목표는 플래그를 보고 완료를 판정하므로,
        // 플래그가 없으면 불러온 직후 "아직 안 한 것"으로 되돌아가거나
        // 반대로 이미 지난 목표가 즉시 다시 완료돼 건너뛴다.

        [Serializable]
        public class SaveState
        {
            public int currentIndex;
            public List<string> flagKeys = new List<string>();
            public List<int> flagValues = new List<int>();
        }

        public string SaveKey => "chapter_director";

        public object CaptureState()
        {
            var state = new SaveState { currentIndex = CurrentIndex };
            foreach (var kv in _flags)
            {
                state.flagKeys.Add(kv.Key);
                state.flagValues.Add(kv.Value);
            }
            return state;
        }

        public void RestoreState(object state)
        {
            if (state is not SaveState s) return;

            _flags.Clear();
            for (int i = 0; i < s.flagKeys.Count && i < s.flagValues.Count; i++)
                _flags[s.flagKeys[i]] = s.flagValues[i];

            CurrentIndex = Mathf.Clamp(s.currentIndex, 0,
                chapter != null ? chapter.objectives.Length : 0);
            _restored = true;
            ObjectiveChanged?.Invoke(Current);
        }
    }
}
