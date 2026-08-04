using System;
using System.Collections.Generic;

namespace Survive.Core
{
    /// <summary>
    /// 저장본 안의 한 칸. 어떤 대상(<see cref="key"/>)의 상태를
    /// 어떤 타입(<see cref="type"/>)의 JSON(<see cref="json"/>)으로 담아 둔 것.
    ///
    /// 상태를 타입별로 나눠 담지 않고 JSON 문자열로 한 겹 싸 둔 이유:
    /// 저장 대상은 컴포넌트마다 다른 모양의 상태를 내놓는데, 그것을 하나의
    /// 정적 스키마로 묶으면 대상이 늘 때마다 저장본 포맷이 바뀐다.
    /// </summary>
    [Serializable]
    public class SaveEntry
    {
        public string key;
        public string json;
        public string type;
    }

    /// <summary>
    /// 한 슬롯의 저장본 전체. 파일·경로와는 무관한 순수 자료구조다 —
    /// 어디에 쓰고 어디서 읽는지는 <c>SaveService</c>가 안다.
    /// </summary>
    [Serializable]
    public class SaveSnapshot
    {
        /// <summary>지금 코드가 쓰는 저장본 버전.</summary>
        public const int CurrentVersion = 1;

        /// <summary>
        /// 저장본 형식 버전. 버전 필드가 없던 시절의 저장본은 0으로 읽힌다 —
        /// 없는 필드를 0으로 채우는 JsonUtility의 성질을 그대로 쓴다.
        /// </summary>
        public int version;

        public string sceneName;
        public List<SaveEntry> entries = new List<SaveEntry>();

        /// <summary>버전 필드가 없던 시절의 저장본인가.</summary>
        public bool IsVersionless => version <= 0;

        public int Count => entries != null ? entries.Count : 0;

        /// <summary>
        /// 칸을 하나 덧붙인다. 같은 키가 이미 있어도 덮어쓰지 않는다 —
        /// 키 충돌은 저장 대상 쪽의 결함이고, 여기서 조용히 지워 버리면
        /// 그 결함이 저장본에서 사라져 추적할 수 없게 된다.
        /// </summary>
        public SaveEntry Add(string key, string type, string json)
        {
            entries ??= new List<SaveEntry>();
            var e = new SaveEntry { key = key, type = type, json = json };
            entries.Add(e);
            return e;
        }

        /// <summary>
        /// 키로 칸을 찾는다. 없으면(= 알 수 없는 키) null.
        /// 같은 키가 둘이면 먼저 넣은 것을 돌려준다.
        /// </summary>
        public SaveEntry Find(string key)
        {
            if (entries == null || string.IsNullOrEmpty(key)) return null;

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e != null && e.key == key) return e;
            }
            return null;
        }
    }
}
