using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;
using Survive.Items;

namespace Survive.UI
{
    /// <summary>
    /// 스크랩은 게이지가 아니라 자원 아이템이다 (설계 D3).
    /// 기존 Water 게이지 슬롯 자리를 재사용해 카운터로 표시한다.
    /// 숫자가 튀지 않도록 DOTween으로 굴린다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ScrapCounterView : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] string format = "스크랩 {0}";
        [SerializeField] float rollSeconds = 0.35f;

        PlayerInventory _inventory;
        int _shown;
        Tween _tween;

        public void Bind(PlayerInventory inventory)
        {
            Unbind();
            _inventory = inventory;
            if (_inventory?.Inventory == null) return;

            _inventory.Inventory.Changed += Refresh;
            _shown = _inventory.ScrapCount;
            Render(_shown);
        }

        void OnDisable() => Unbind();

        void Unbind()
        {
            _tween?.Kill();
            _tween = null;
            if (_inventory?.Inventory != null) _inventory.Inventory.Changed -= Refresh;
            _inventory = null;
        }

        void Refresh()
        {
            int target = _inventory.ScrapCount;
            if (target == _shown) return;

            _tween?.Kill();
            int start = _shown;
            _tween = DOVirtual.Int(start, target, rollSeconds, v =>
            {
                _shown = v;
                Render(v);
            }).SetEase(Ease.OutCubic);
        }

        void Render(int v)
        {
            if (label != null) label.text = string.Format(format, v);
        }
    }
}
