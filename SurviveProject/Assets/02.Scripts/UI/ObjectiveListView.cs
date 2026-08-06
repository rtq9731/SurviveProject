using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;
using Survive.Core;
using Survive.Localization;
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
            group?.DOKill();
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

            // 목표 문구는 DataText를 거친다. 여기서 SO를 직접 읽으면 이 줄만
            // 로케일을 안 따라온다. 줄 모양(머리표·괄호)도 표 안에 있다 —
            // 언어에 따라 진행률을 앞에 두는 편이 자연스러울 수 있다.
            string text = DataText.Text(objective);
            float p = _director.CurrentProgress;

            label.text = p > 0f && p < 1f
                ? Loc.F("UI", "objective_line_progress", text, Mathf.RoundToInt(p * 100f))
                : Loc.F("UI", "objective_line", text);
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
