using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survive.Progression
{
    /// <summary>
    /// 연구대의 대기열. 맨 앞 하나만 진행하고 나머지는 순서를 기다린다.
    ///
    /// <see cref="Survive.Crafting.CraftQueue"/>와 같은 규율을 따른다 — 동시에 여럿을
    /// 돌리면 "시간이 든다"는 규칙이 무의미해지기 때문이다. 별도의 형으로 두는 이유는
    /// 하나뿐이다: 제작 대기열의 항목은 <b>레시피와 수량</b>이고 이쪽은 <b>연구 항목</b>이라,
    /// 하나로 합치려면 제작 쪽 전부를 일반화해야 한다. 그 대가로 얻는 것은 클래스
    /// 하나를 아끼는 것뿐이고, 잃는 것은 이미 통과하고 있는 제작 규칙의 안정이다.
    /// </summary>
    public class ResearchQueue
    {
        /// <summary>연구대가 한 번에 물고 있을 수 있는 항목 수.</summary>
        public const int DefaultCapacity = 4;

        readonly List<ResearchJob> _jobs = new List<ResearchJob>();

        public ResearchQueue(int capacity = DefaultCapacity)
        {
            Capacity = Mathf.Max(1, capacity);
        }

        public int Capacity { get; }
        public IReadOnlyList<ResearchJob> Jobs => _jobs;
        public int Count => _jobs.Count;
        public bool IsFull => _jobs.Count >= Capacity;
        public bool IsEmpty => _jobs.Count == 0;

        /// <summary>지금 들여다보고 있는 항목. 비어 있으면 null.</summary>
        public ResearchJob Active => _jobs.Count > 0 ? _jobs[0] : null;

        /// <summary>줄이 바뀔 때마다 — 걸었을 때, 하나 끝났을 때, 물렸을 때.</summary>
        public event Action Changed;

        public ResearchJob At(int index) =>
            index >= 0 && index < _jobs.Count ? _jobs[index] : null;

        /// <summary>이 항목이 이미 줄에 서 있는가. 같은 것을 두 번 걸 이유가 없다.</summary>
        public bool Contains(ResearchEntrySO entry)
        {
            if (entry == null) return false;
            foreach (var j in _jobs)
                if (j != null && j.Entry == entry) return true;
            return false;
        }

        /// <summary>이 항목이 줄에서 몇 번째인가. 없으면 -1.</summary>
        public int IndexOf(ResearchEntrySO entry)
        {
            if (entry == null) return -1;
            for (int i = 0; i < _jobs.Count; i++)
                if (_jobs[i] != null && _jobs[i].Entry == entry) return i;
            return -1;
        }

        internal void Add(ResearchJob job)
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
