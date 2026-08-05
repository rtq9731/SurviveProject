using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survive.Crafting
{
    /// <summary>
    /// 제작 대기열. 맨 앞 하나만 진행하고 나머지는 순서를 기다린다.
    ///
    /// 동시에 여럿을 돌리면 "제작에 시간이 걸린다"는 규칙이 무의미해진다 —
    /// 열 개를 한꺼번에 걸면 열 배로 빨라지는 셈이기 때문이다. 줄을 세우면
    /// 시간이 실제 비용이 되고, 무엇을 먼저 만들 것인가가 선택이 된다.
    /// </summary>
    public class CraftQueue
    {
        /// <summary>손 제작·제작대·화톳불이 공통으로 쓰는 기본 칸 수.</summary>
        public const int DefaultCapacity = 6;

        readonly List<CraftJob> _jobs = new List<CraftJob>();

        public CraftQueue(int capacity = DefaultCapacity)
        {
            Capacity = Mathf.Max(1, capacity);
        }

        public int Capacity { get; }
        public IReadOnlyList<CraftJob> Jobs => _jobs;
        public int Count => _jobs.Count;
        public bool IsFull => _jobs.Count >= Capacity;
        public bool IsEmpty => _jobs.Count == 0;

        /// <summary>지금 진행 중인 항목. 비어 있으면 null.</summary>
        public CraftJob Active => _jobs.Count > 0 ? _jobs[0] : null;

        /// <summary>줄이 바뀔 때마다 — 걸었을 때, 하나 완성됐을 때, 취소했을 때.</summary>
        public event Action Changed;

        public CraftJob At(int index) =>
            index >= 0 && index < _jobs.Count ? _jobs[index] : null;

        internal void Add(CraftJob job)
        {
            if (job == null) return;
            _jobs.Add(job);
        }

        internal void RemoveAt(int index)
        {
            if (index < 0 || index >= _jobs.Count) return;
            _jobs.RemoveAt(index);
        }

        internal void Clear() => _jobs.Clear();

        internal void RaiseChanged() => Changed?.Invoke();
    }
}
