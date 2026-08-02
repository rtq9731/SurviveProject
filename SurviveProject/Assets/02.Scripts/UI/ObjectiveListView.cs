using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Survive.Core;
using Survive.Progression;

namespace Survive.UI
{
    /// <summary>
    /// 현재 목표와 진행도를 표시한다.
    /// 자막(PanelDialog)과는 별개 패널이다 — 둘이 겹치면 읽기 어렵다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ObjectiveListView : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] Text label;
        [SerializeField] float refreshInterval = 0.25f;

        ChapterDirector _director;

        IEnumerator Start()
        {
            yield return null;
            if (!GameServices.TryGet<ChapterDirector>(out _director))
            {
                숨김();
                yield break;
            }

            _director.ObjectiveChanged += 목표바뀜;
            목표바뀜(_director.Current);

            var 대기 = new WaitForSeconds(refreshInterval);
            while (true)
            {
                갱신();
                yield return 대기;
            }
        }

        void OnDestroy()
        {
            if (_director != null) _director.ObjectiveChanged -= 목표바뀜;
        }

        void 목표바뀜(ObjectiveSO 목표)
        {
            if (목표 == null) { 숨김(); return; }

            if (group != null)
            {
                group.DOKill();
                group.alpha = 0f;
                group.DOFade(1f, 0.3f);
            }
            갱신();
        }

        void 갱신()
        {
            if (_director == null || label == null) return;
            var 목표 = _director.Current;
            if (목표 == null) { 숨김(); return; }

            float p = _director.CurrentProgress;
            label.text = p > 0f && p < 1f
                ? $"◆ {목표.displayText}  ({Mathf.RoundToInt(p * 100f)}%)"
                : $"◆ {목표.displayText}";
        }

        void 숨김()
        {
            if (group != null)
            {
                group.DOKill();
                group.DOFade(0f, 0.3f);
            }
            else if (label != null) label.text = "";
        }
    }
}
