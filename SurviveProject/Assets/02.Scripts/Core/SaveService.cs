using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Survive.Core
{
    /// <summary>
    /// 체크포인트 저장. ISaveable을 구현한 컴포넌트를 모아 JSON으로 남긴다.
    ///
    /// 저장본의 모양과 문자열 변환은 <see cref="SaveSnapshot"/>·
    /// <see cref="SaveSerializer"/>(Domain)가 안다. 여기 남은 것은
    /// 파일이 어디 있는지와 어떤 컴포넌트가 대상인지 — 즉 Unity에
    /// 붙어 있어서 순수하게 만들 수 없는 부분뿐이다.
    /// </summary>
    public class SaveService
    {
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
            var save = new SaveSnapshot
            {
                sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            };

            foreach (var s in _saveables)
            {
                if (s == null) continue;
                var state = s.CaptureState();
                if (state == null) continue;

                save.Add(s.SaveKey, state.GetType().AssemblyQualifiedName, JsonUtility.ToJson(state));
            }

            try
            {
                File.WriteAllText(PathFor(slot), SaveSerializer.Serialize(save));
                Debug.Log($"[SaveService] 저장 완료: {PathFor(slot)} ({save.Count}개 항목)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] 저장 실패: {e.Message}");
            }
        }

        public bool Load(string slot)
        {
            if (!HasSave(slot)) return false;

            string text;
            try
            {
                text = File.ReadAllText(PathFor(slot));
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] 불러오기 실패: {e.Message}");
                return false;
            }

            if (!SaveSerializer.TryDeserialize(text, out var save, out var error))
            {
                Debug.LogError($"[SaveService] 불러오기 실패: {error}");
                return false;
            }

            foreach (var entry in save.entries)
            {
                var target = _saveables.Find(s => s != null && s.SaveKey == entry.key);
                if (target == null) continue;

                var t = string.IsNullOrEmpty(entry.type) ? null : Type.GetType(entry.type);
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
