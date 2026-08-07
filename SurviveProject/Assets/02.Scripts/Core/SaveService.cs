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

        static string _root;

        /// <summary>
        /// 저장본이 앉는 폴더. <b>빌드에서는 <see cref="Application.persistentDataPath"/>
        /// 그대로이고, 에디터에서는 그 아래 프로젝트별 갈래 폴더다.</b>
        ///
        /// 규칙과 그 이유는 <see cref="SaveLocation"/>에 적혀 있다. 여기 있는 것은
        /// Unity에게 값을 물어 넘기는 손과, 갈래 폴더를 처음 만들 때의 씨앗뿐이다.
        ///
        /// 한 번 정하면 안 바꾼다 — 재생 도중에 자리가 옮겨 다니면 같은 슬롯의
        /// 쓰기와 읽기가 서로 다른 파일을 보게 된다.
        /// </summary>
        public static string Root
        {
            get
            {
                if (_root != null) return _root;

                var shared = Application.persistentDataPath;
                _root = SaveLocation.RootFor(shared, Application.dataPath, Application.isEditor);

                if (_root != shared) PrepareBranch(_root, shared);
                return _root;
            }
        }

        /// <summary>
        /// 갈래 폴더를 <b>처음 만들 때만</b> 공유 폴더의 저장본을 베껴 온다.
        ///
        /// <b>옮기지도 지우지도 않는다.</b> 원본은 그 자리에 그대로 남는다 —
        /// 사람이 지금 하고 있는 이어하기를 건드리는 것이 애초에 이 라운드가
        /// 막으려던 일이다.
        ///
        /// <b>왜 베끼는가.</b> 안 베끼면 자리를 가르는 순간 모든 에디터가 자기가
        /// 알던 저장본을 잊는다 — 사람의 에디터까지. 한 번 베껴 두면 가르기 직전과
        /// 직후의 행동이 같고, 그 뒤로 갈래마다 따로 자란다.
        ///
        /// 검사가 남긴 슬롯은 안 베낀다. 그것은 그때 그 세션의 쓰레기고,
        /// 새 폴더에 되살릴 이유가 없다.
        /// </summary>
        static void PrepareBranch(string branch, string shared)
        {
            try
            {
                if (Directory.Exists(branch)) return;

                Directory.CreateDirectory(branch);

                foreach (var src in Directory.GetFiles(shared, "save_*.json"))
                {
                    var name = Path.GetFileName(src);
                    if (name.StartsWith("save_" + SaveSlots.IsolationPrefix, StringComparison.Ordinal))
                        continue;

                    File.Copy(src, Path.Combine(branch, name), false);
                }

                Debug.Log($"[SaveService] 이 에디터의 저장 폴더를 갈랐다: {branch}");
            }
            catch (Exception e)
            {
                // 베끼기가 실패해도 저장 자체는 돌아야 한다. 다만 조용히 넘기면
                // "이어하기가 사라졌다"의 이유를 아무도 못 찾는다.
                Debug.LogWarning($"[SaveService] 저장 폴더를 가르는 중 문제: {e.Message}");
            }
        }

        /// <summary>
        /// 이 슬롯의 파일 자리.
        ///
        /// <b>이름을 그대로 쓰지 않고 <see cref="SaveSlots.Resolve"/>를 지난다.</b>
        /// 검사가 도는 동안에는 기본 슬롯을 콕 집은 요청조차 전용 슬롯으로 가야 한다.
        /// 그 판정을 여기 한 곳에 두었기 때문에 <b>쓰기·읽기·존재 확인·삭제가 전부
        /// 같은 답</b>을 얻는다 — 하나라도 빠지면 "지우기만 사람 저장본을 지운다"
        /// 같은 구멍이 난다.
        ///
        /// 폴더도 같은 이유로 <see cref="Root"/> 하나를 지난다.
        /// </summary>
        static string PathFor(string slot) =>
            Path.Combine(Root, SaveSlots.FileNameOf(SaveSlots.Resolve(slot)));

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
