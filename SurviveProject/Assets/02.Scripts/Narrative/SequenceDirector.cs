using System;
using System.Collections;
using UnityEngine;
using Survive.Core;
using Survive.Localization;
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
        bool _playing;

        public bool IsPlaying => _playing;
        public event Action<SequenceSO> SequenceFinished;

        void OnEnable() => GameServices.Register(this);
        void OnDisable() => GameServices.Unregister<SequenceDirector>();

        IEnumerator Start()
        {
            yield return null;
            _player = UnityEngine.Object.FindAnyObjectByType<PlayerContext>(FindObjectsInactive.Exclude);

            if (playOnStart != null)
            {
                yield return new WaitForSeconds(startDelay);
                yield return Play(playOnStart);
            }
        }

        /// <summary>
        /// 자막을 순서대로 띄운다.
        ///
        /// <b>화면에 나가는 글자는 에셋이 아니라 번역 표에서 나온다.</b>
        /// <see cref="DataText.Line(SequenceSO,int)"/>가 <c>Sequence/{id}.lineN.text</c>를
        /// 먼저 뒤지고, 표에 없을 때만 에셋 원문으로 물러선다. 에셋에 적힌 한국어는
        /// <b>초안의 원본</b>이지 화면의 원본이 아니다 — 초안 생성 도구가 그 원문으로
        /// 표의 줄을 짓고, 그 뒤로 화면이 읽는 것은 표뿐이다 (docs/번역-체계.md §10).
        ///
        /// <b>줄마다 다시 조회한다.</b> 재생 중에 로케일이 바뀌면 다음 줄부터
        /// 새 로케일로 나온다. 시작할 때 한 번 모아 두면 그 기회가 사라진다.
        ///
        /// <b>줄이 있는지 없는지도 표를 거친 값으로 판정한다.</b> 에셋 원문이 폴백이라
        /// 두 판정이 어긋날 일은 없고, 반대로 <c>line.text</c>를 보면 그 자리가
        /// 곧 표를 우회하는 두 번째 구멍이 된다.
        /// </summary>
        public IEnumerator Play(SequenceSO sequence)
        {
            if (sequence == null || sequence.lines == null || sequence.lines.Length == 0)
                yield break;

            _playing = true;
            if (sequence.lockInput) LockControls(true);

            for (int i = 0; i < sequence.lines.Length; i++)
            {
                var line = sequence.lines[i];
                if (line == null) continue;

                string 자막 = DataText.Line(sequence, i);
                if (string.IsNullOrWhiteSpace(자막)) continue;

                subtitle?.Show(DataText.Speaker(sequence, i), 자막);
                yield return new WaitForSeconds(line.holdSeconds);
            }

            subtitle?.HideView();
            if (sequence.lockInput) LockControls(false);
            _playing = false;

            SequenceFinished?.Invoke(sequence);
        }

        void LockControls(bool locked)
        {
            _player?.Locomotion?.SetMovementLocked(locked);
            _player?.CameraRig?.SetLookLocked(locked);
        }
    }
}
