using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Survive.Core;
using Survive.World;

namespace Survive.UI
{
    /// <summary>
    /// 랜턴 배터리 표시. 지하의 핵심 압박이므로 눈에 잘 띄어야 한다.
    /// 랜턴을 켜기 전에는 숨긴다.
    /// </summary>
    [DisallowMultipleComponent]
    public class BatteryBarView : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] Image fill;
        [SerializeField] Text label;
        [SerializeField] Color fullColor = new Color(1f, 0.85f, 0.45f);
        [SerializeField] Color emptyColor = new Color(0.6f, 0.2f, 0.15f);

        LanternController _lantern;

        IEnumerator Start()
        {
            yield return null;
            if (!GameServices.TryGet<LanternController>(out _lantern))
            {
                SetVisible(false);
                yield break;
            }

            _lantern.BatteryChanged += Refresh;
            Refresh(_lantern.Battery, 100f);
        }

        void OnDestroy()
        {
            if (_lantern != null) _lantern.BatteryChanged -= Refresh;
        }

        void Refresh(float current, float max)
        {
            SetVisible(_lantern != null && _lantern.IsOn);

            float n = max <= 0f ? 0f : current / max;
            if (fill != null)
            {
                fill.fillAmount = n;
                fill.color = Color.Lerp(emptyColor, fullColor, n);
            }
            if (label != null) label.text = $"배터리 {Mathf.RoundToInt(current)}";
        }

        void SetVisible(bool visible)
        {
            if (group == null) return;
            group.DOKill();
            group.DOFade(visible ? 1f : 0f, 0.25f);
        }
    }
}
