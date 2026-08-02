using UnityEngine;
using Survive.Vitals;

namespace Survive.UI
{
    /// <summary>
    /// 기존 ValueBarScript를 감싸 Vital의 변화를 그대로 흘려보낸다.
    /// ValueBarScript의 기존 공개 시그니처는 건드리지 않았다 (씬 참조가 걸려 있다).
    /// </summary>
    [DisallowMultipleComponent]
    public class VitalBarView : MonoBehaviour
    {
        public enum Kind { Health, Oxygen }

        [SerializeField] ValueBarScript bar;
        [SerializeField] Kind kind = Kind.Health;

        Vital _vital;

        void Awake()
        {
            if (bar == null) bar = GetComponent<ValueBarScript>();
        }

        public void Bind(PlayerVitals vitals)
        {
            Unbind();
            if (vitals == null || bar == null) return;

            _vital = kind == Kind.Health ? vitals.Health : vitals.Oxygen;
            if (_vital == null) return;

            bar.RefreshMaxValue(_vital.Max);
            bar.SetValueDirect(_vital.Current);
            _vital.Changed += OnValueChanged;
        }

        void OnDisable() => Unbind();

        void Unbind()
        {
            if (_vital != null) _vital.Changed -= OnValueChanged;
            _vital = null;
        }

        void OnValueChanged(float current, float max) => bar.SetValueDirect(current);
    }
}
