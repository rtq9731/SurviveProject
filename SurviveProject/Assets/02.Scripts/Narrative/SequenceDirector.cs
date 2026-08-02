using System;
using System.Collections;
using UnityEngine;
using Survive.Core;
using Survive.Player;

namespace Survive.Narrative
{
    /// <summary>
    /// 자막 시퀀스를 순차로 재생한다.
    /// 기존 자막의 고장 네 가지(재생 로직 없음·표시 상태 반대·프리팹 하드코딩·가독성)를
    /// 이 시스템이 대체한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class SequenceDirector : MonoBehaviour
    {
        [SerializeField] SubtitleView subtitle;

        [Tooltip("씬 시작 시 자동 재생할 시퀀스. 비우면 재생하지 않는다")]
        [SerializeField] SequenceSO playOnStart;

        [SerializeField] float startDelay = 0.5f;

        PlayerContext _player;
        bool _재생중;

        public bool IsPlaying => _재생중;
        public event Action<SequenceSO> SequenceFinished;

        void OnEnable() => GameServices.Register(this);
        void OnDisable() => GameServices.Unregister<SequenceDirector>();

        IEnumerator Start()
        {
            yield return null;
            _player = UnityEngine.Object.FindFirstObjectByType<PlayerContext>(FindObjectsInactive.Exclude);

            if (playOnStart != null)
            {
                yield return new WaitForSeconds(startDelay);
                yield return Play(playOnStart);
            }
        }

        public IEnumerator Play(SequenceSO sequence)
        {
            if (sequence == null || sequence.lines == null || sequence.lines.Length == 0)
                yield break;

            _재생중 = true;
            if (sequence.lockInput) 조작잠금(true);

            foreach (var line in sequence.lines)
            {
                if (line == null || string.IsNullOrWhiteSpace(line.text)) continue;
                subtitle?.Show(line.speaker, line.text);
                yield return new WaitForSeconds(line.holdSeconds);
            }

            subtitle?.숨기기();
            if (sequence.lockInput) 조작잠금(false);
            _재생중 = false;

            SequenceFinished?.Invoke(sequence);
        }

        void 조작잠금(bool 잠글까)
        {
            _player?.Locomotion?.SetMovementLocked(잠글까);
            _player?.CameraRig?.SetLookLocked(잠글까);
        }
    }
}
