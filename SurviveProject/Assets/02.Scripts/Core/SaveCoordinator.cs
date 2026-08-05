using System.Collections;
using System.Linq;
using UnityEngine;
using Survive.Progression;

namespace Survive.Core
{
    /// <summary>
    /// 저장을 실제로 굴리는 곳.
    ///
    /// <see cref="SaveService"/>는 순수 클래스라 스스로 씬을 훑지 못한다.
    /// 여기서 씬의 ISaveable을 모아 등록하고, 체크포인트에서 자동 저장한다.
    ///
    /// 체크포인트를 목표 전환 지점으로 잡은 이유: 챕터 1은 목표 단위로
    /// 진행되므로, 목표 하나를 끝낸 순간이 플레이어가 "여기까지 했다"고
    /// 느끼는 지점과 일치한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class SaveCoordinator : MonoBehaviour
    {
        public const string DefaultSlot = "auto";

        [Tooltip("목표를 하나 끝낼 때마다 자동 저장한다")]
        [SerializeField] bool autoSaveOnObjective = true;

        [Tooltip("시작할 때 저장본이 있으면 불러온다")]
        [SerializeField] bool loadOnStart = false;

        SaveService _service;
        ChapterDirector _chapter;

        public SaveService Service => _service;

        void Awake()
        {
            _service = new SaveService();
            GameServices.Register(_service);
            GameServices.Register(this);
        }

        void OnDestroy()
        {
            if (_chapter != null) _chapter.ObjectiveChanged -= OnObjectiveChanged;
            GameServices.Unregister<SaveCoordinator>();
            GameServices.Unregister<SaveService>();
        }

        IEnumerator Start()
        {
            // 다른 컴포넌트의 Awake가 끝나야 인벤토리 같은 것이 준비된다
            yield return null;
            Collect();

            _chapter = Object.FindAnyObjectByType<ChapterDirector>(FindObjectsInactive.Exclude);
            if (_chapter != null && autoSaveOnObjective)
                _chapter.ObjectiveChanged += OnObjectiveChanged;

            if (loadOnStart && _service.HasSave(DefaultSlot))
            {
                yield return null;      // 챕터의 Start가 먼저 돌게 둔다
                Load();
            }
        }

        /// <summary>씬의 저장 대상을 모은다.</summary>
        public int Collect()
        {
            int n = 0;
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include))
            {
                if (mb is not ISaveable s) continue;
                _service.Register(s);
                n++;
            }
            return n;
        }

        int _lastSavedIndex = -1;

        void OnObjectiveChanged(ObjectiveSO objective)
        {
            // 챕터가 시작될 때도 한 번 불린다. 그때 저장하면 빈 저장본이 생긴다.
            //
            // "첫 호출을 건너뛴다"로 처리했더니 구독 시점에 따라 결과가 달라졌다 —
            // 챕터가 먼저 시작하면 그 첫 호출을 놓치고, 진짜 첫 전환을 건너뛴다.
            // 순서가 아니라 값을 본다.
            if (_chapter == null) return;

            int index = _chapter.CurrentIndex;
            if (index <= 0 || index == _lastSavedIndex) return;

            _lastSavedIndex = index;
            Save();
        }

        public void Save(string slot = DefaultSlot) => _service?.Save(slot);
        public bool Load(string slot = DefaultSlot) => _service != null && _service.Load(slot);
        public bool HasSave(string slot = DefaultSlot) => _service != null && _service.HasSave(slot);
        public void Delete(string slot = DefaultSlot) => _service?.Delete(slot);
    }
}
