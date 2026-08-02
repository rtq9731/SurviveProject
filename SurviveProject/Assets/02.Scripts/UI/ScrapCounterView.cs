using UnityEngine;
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
        [SerializeField] Text label;
        [SerializeField] string format = "스크랩 {0}";
        [SerializeField] float rollSeconds = 0.35f;

        PlayerInventory _inventory;
        int _표시중;
        Tween _tween;

        public void Bind(PlayerInventory inventory)
        {
            해제();
            _inventory = inventory;
            if (_inventory?.Inventory == null) return;

            _inventory.Inventory.Changed += 갱신;
            _표시중 = _inventory.ScrapCount;
            찍기(_표시중);
        }

        void OnDisable() => 해제();

        void 해제()
        {
            _tween?.Kill();
            _tween = null;
            if (_inventory?.Inventory != null) _inventory.Inventory.Changed -= 갱신;
            _inventory = null;
        }

        void 갱신()
        {
            int 목표 = _inventory.ScrapCount;
            if (목표 == _표시중) return;

            _tween?.Kill();
            int 시작 = _표시중;
            _tween = DOVirtual.Int(시작, 목표, rollSeconds, v =>
            {
                _표시중 = v;
                찍기(v);
            }).SetEase(Ease.OutCubic);
        }

        void 찍기(int v)
        {
            if (label != null) label.text = string.Format(format, v);
        }
    }
}
