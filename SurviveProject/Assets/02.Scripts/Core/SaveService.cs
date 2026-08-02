using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Survive.Core
{
    /// <summary>
    /// 체크포인트 저장. ISaveable을 구현한 컴포넌트를 모아 JSON으로 남긴다.
    /// </summary>
    public class SaveService
    {
        [Serializable]
        class 항목
        {
            public string key;
            public string json;
            public string type;
        }

        [Serializable]
        class 저장본
        {
            public string sceneName;
            public List<항목> entries = new List<항목>();
        }

        readonly List<ISaveable> _saveables = new List<ISaveable>();

        public void Register(ISaveable s)
        {
            if (s != null && !_saveables.Contains(s)) _saveables.Add(s);
        }

        public void Unregister(ISaveable s) => _saveables.Remove(s);

        static string 경로(string slot) =>
            Path.Combine(Application.persistentDataPath, $"save_{slot}.json");

        public bool HasSave(string slot) => File.Exists(경로(slot));

        public void Save(string slot)
        {
            var 본 = new 저장본
            {
                sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            };

            foreach (var s in _saveables)
            {
                if (s == null) continue;
                var state = s.CaptureState();
                if (state == null) continue;

                본.entries.Add(new 항목
                {
                    key = s.SaveKey,
                    type = state.GetType().AssemblyQualifiedName,
                    json = JsonUtility.ToJson(state)
                });
            }

            try
            {
                File.WriteAllText(경로(slot), JsonUtility.ToJson(본, true));
                Debug.Log($"[SaveService] 저장 완료: {경로(slot)} ({본.entries.Count}개 항목)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] 저장 실패: {e.Message}");
            }
        }

        public bool Load(string slot)
        {
            if (!HasSave(slot)) return false;

            저장본 본;
            try
            {
                본 = JsonUtility.FromJson<저장본>(File.ReadAllText(경로(slot)));
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] 불러오기 실패: {e.Message}");
                return false;
            }
            if (본 == null) return false;

            foreach (var entry in 본.entries)
            {
                var target = _saveables.Find(s => s != null && s.SaveKey == entry.key);
                if (target == null) continue;

                var t = Type.GetType(entry.type);
                if (t == null)
                {
                    Debug.LogWarning($"[SaveService] 타입을 찾지 못했습니다: {entry.type}");
                    continue;
                }

                var state = JsonUtility.FromJson(entry.json, t);
                target.RestoreState(state);
            }
            return true;
        }

        public void Delete(string slot)
        {
            if (HasSave(slot)) File.Delete(경로(slot));
        }
    }
}
