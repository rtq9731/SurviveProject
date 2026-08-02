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
        class entry
        {
            public string key;
            public string json;
            public string type;
        }

        [Serializable]
        class snapshot
        {
            public string sceneName;
            public List<entry> entries = new List<entry>();
        }

        readonly List<ISaveable> _saveables = new List<ISaveable>();

        public void Register(ISaveable s)
        {
            if (s != null && !_saveables.Contains(s)) _saveables.Add(s);
        }

        public void Unregister(ISaveable s) => _saveables.Remove(s);

        static string PathFor(string slot) =>
            Path.Combine(Application.persistentDataPath, $"save_{slot}.json");

        public bool HasSave(string slot) => File.Exists(PathFor(slot));

        public void Save(string slot)
        {
            var save = new snapshot
            {
                sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            };

            foreach (var s in _saveables)
            {
                if (s == null) continue;
                var state = s.CaptureState();
                if (state == null) continue;

                save.entries.Add(new entry
                {
                    key = s.SaveKey,
                    type = state.GetType().AssemblyQualifiedName,
                    json = JsonUtility.ToJson(state)
                });
            }

            try
            {
                File.WriteAllText(PathFor(slot), JsonUtility.ToJson(save, true));
                Debug.Log($"[SaveService] 저장 완료: {PathFor(slot)} ({save.entries.Count}개 항목)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] 저장 실패: {e.Message}");
            }
        }

        public bool Load(string slot)
        {
            if (!HasSave(slot)) return false;

            snapshot save;
            try
            {
                save = JsonUtility.FromJson<snapshot>(File.ReadAllText(PathFor(slot)));
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] 불러오기 실패: {e.Message}");
                return false;
            }
            if (save == null) return false;

            foreach (var entry in save.entries)
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
            if (HasSave(slot)) File.Delete(PathFor(slot));
        }
    }
}
