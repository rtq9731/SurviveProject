using System.Collections.Generic;
using UnityEngine;

namespace Survive.Items
{
    [CreateAssetMenu(menuName = "Survive/Items/Item Database")]
    public class ItemDatabaseSO : ScriptableObject
    {
        public ItemDataSO[] items = new ItemDataSO[0];

        Dictionary<string, ItemDataSO> _byId;

        void OnEnable() => _byId = null;   // 재생성 강제

        void BuildIndex()
        {
            _byId = new Dictionary<string, ItemDataSO>();
            if (items == null) return;
            foreach (var it in items)
            {
                if (it == null || string.IsNullOrWhiteSpace(it.id)) continue;
                _byId[it.id] = it;
            }
        }

        public ItemDataSO GetById(string id)
        {
            TryGetById(id, out var found);
            return found;
        }

        public bool TryGetById(string id, out ItemDataSO item)
        {
            if (_byId == null) BuildIndex();
            if (string.IsNullOrEmpty(id))
            {
                item = null;
                return false;
            }
            return _byId.TryGetValue(id, out item);
        }

        /// <summary>
        /// 데이터 문제를 사람이 읽을 수 있는 문장으로 돌려준다.
        /// 비어 있으면 문제가 없다는 뜻이다.
        /// </summary>
        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();
            var seen = new HashSet<string>();

            if (items == null) return problems;

            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                if (it == null)
                {
                    problems.Add($"{i}번 항목이 비어 있습니다.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(it.id))
                {
                    problems.Add($"{i}번 항목({it.name})의 id가 비어 있습니다.");
                    continue;
                }
                if (!seen.Add(it.id))
                    problems.Add($"id가 중복되었습니다: {it.id}");
            }
            return problems;
        }

        void OnValidate()
        {
            _byId = null;
            foreach (var problems in Validate())
                Debug.LogError($"[ItemDatabaseSO] {problems}", this);
        }
    }
}
