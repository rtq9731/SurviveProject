using System.Collections;
using UnityEngine;
using TMPro;
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
        [SerializeField] TMP_Text label;
        [SerializeField] float refreshInterval = 0.25f;

        ChapterDirector _director;

        IEnumerator Start()
        {
            yield return null;
            if (!GameServices.TryGet<ChapterDirector>(out _director))
            {
                Hide();
                yield break;
            }

            _director.ObjectiveChanged += OnObjectiveChanged;
            OnObjectiveChanged(_director.Current);

            var wait = new WaitForSeconds(refreshInterval);
            while (true)
            {
                Refresh();
                yield return wait;
            }
        }

        void OnDestroy()
        {
            if (_director != null) _director.ObjectiveChanged -= OnObjectiveChanged;
        }

        void OnObjectiveChanged(ObjectiveSO objective)
        {
            if (objective == null) { Hide(); return; }

            if (group != null)
            {
                group.DOKill();
                group.alpha = 0f;
                group.DOFade(1f, 0.3f);
            }
            Refresh();
        }

        void Refresh()
        {
            if (_director == null || label == null) return;
            var objective = _director.Current;
            if (objective == null) { Hide(); return; }

            float p = _director.CurrentProgress;
            label.text = p > 0f && p < 1f
                ? $"◆ {objective.displayText}  ({Mathf.RoundToInt(p * 100f)}%)"
                : $"◆ {objective.displayText}";
        }

        void Hide()
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
