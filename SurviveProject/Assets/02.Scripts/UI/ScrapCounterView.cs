using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;
using Survive.Items;
using Survive.Localization;

namespace Survive.UI
{
    /// <summary>
    /// 스크랩은 게이지가 아니라 자원 아이템이다 (설계 D3).
    /// 기존 Water 게이지 슬롯 자리를 재사용해 카운터로 표시한다.
    /// 숫자가 튀지 않도록 DOTween으로 굴린다.
    ///
    /// <b>틀 문자열을 인스펙터에서 뺐다.</b> 예전에는 <c>format = "스크랩 {0}"</c>이
    /// 직렬화 필드였는데, 기본값을 고쳐도 <b>이미 프리팹에 박힌 값</b>은 바뀌지 않는다
    /// (실제로 <c>InfoBar.prefab</c>·<c>CvsUI.prefab</c>에 박혀 있었다). 필드를 없애면
    /// 그 직렬화 값은 읽히지 않고, 틀은 언제나 번역 표에서 나온다 —
    /// 프리팹을 고치지 않고 이 화면을 로케일에 따라오게 하는 유일한 길이다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ScrapCounterView : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
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

        void OnEnable() => Loc.LocaleChanged += Redraw;

        void OnDisable()
        {
            Loc.LocaleChanged -= Redraw;
            Unbind();
        }

        /// <summary>
        /// 수가 바뀔 때만 다시 그리므로, 로케일이 바뀐 프레임에는 아무도
        /// 이 칸을 건드리지 않는다. 그때 묵은 글자가 그대로 남는다.
        /// </summary>
        void Redraw() => Render(_shown);

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
            if (label != null) label.text = Loc.F("UI", "scrap_count", v);
        }
    }
}
