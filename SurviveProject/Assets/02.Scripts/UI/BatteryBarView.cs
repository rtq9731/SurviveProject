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
                보임(false);
                yield break;
            }

            _lantern.BatteryChanged += 갱신;
            갱신(_lantern.Battery, 100f);
        }

        void OnDestroy()
        {
            if (_lantern != null) _lantern.BatteryChanged -= 갱신;
        }

        void 갱신(float 현재, float 최대)
        {
            보임(_lantern != null && _lantern.IsOn);

            float n = 최대 <= 0f ? 0f : 현재 / 최대;
            if (fill != null)
            {
                fill.fillAmount = n;
                fill.color = Color.Lerp(emptyColor, fullColor, n);
            }
            if (label != null) label.text = $"배터리 {Mathf.RoundToInt(현재)}";
        }

        void 보임(bool 보일까)
        {
            if (group == null) return;
            group.DOKill();
            group.DOFade(보일까 ? 1f : 0f, 0.25f);
        }
    }
}
