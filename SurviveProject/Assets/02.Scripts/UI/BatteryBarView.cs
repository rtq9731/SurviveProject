using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;
using Survive.Core;
using Survive.Localization;
using Survive.World;

namespace Survive.UI
{
    /// <summary>
    /// 랜턴 배터리 표시. 지하의 핵심 압박이므로 눈에 잘 띄어야 한다.
    /// 랜턴을 손에 넣기 전에는 숨긴다.
    ///
    /// <b>불이 꺼졌다고 숨기지 않는다.</b> 랜턴은 상시 점등이 전제라 꺼졌다는 것은
    /// 곧 배터리가 0이라는 뜻이고(스펙 §12), 그때가 이 눈금이 가장 필요한 순간이다.
    /// 켜짐을 기준으로 숨기면 배터리가 다한 순간 눈금까지 함께 사라져,
    /// 플레이어는 어두워진 이유를 화면 어디서도 확인할 수 없게 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class BatteryBarView : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] Image fill;
        [SerializeField] TMP_Text label;
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
            Refresh(_lantern.Battery, LanternRule.MaxBattery);
        }

        void OnDestroy()
        {
            if (_lantern != null) _lantern.BatteryChanged -= Refresh;
            group?.DOKill();
        }

        void Refresh(float current, float max)
        {
            SetVisible(_lantern != null && _lantern.HasLantern);

            float n = max <= 0f ? 0f : current / max;
            if (fill != null)
            {
                fill.fillAmount = n;
                fill.color = Color.Lerp(emptyColor, fullColor, n);
            }
            if (label != null)
                label.text = Loc.F("UI", "battery_amount", Mathf.RoundToInt(current));
        }

        void SetVisible(bool visible)
        {
            if (group == null) return;
            group.DOKill();
            group.DOFade(visible ? 1f : 0f, 0.25f);
        }
    }
}
