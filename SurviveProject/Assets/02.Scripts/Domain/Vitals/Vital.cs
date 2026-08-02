using System;
using UnityEngine;

namespace Survive.Vitals
{
    /// <summary>
    /// 체력·산소 같은 0~Max 사이의 값 하나.
    /// MonoBehaviour가 아니므로 Unity 실행 없이 테스트할 수 있다.
    /// </summary>
    public class Vital
    {
        float _current;
        float _max;

        public Vital(float max, float start)
        {
            _max = Mathf.Max(0f, max);
            _current = Mathf.Clamp(start, 0f, _max);
        }

        public float Current => _current;
        public float Max => _max;
        public float Normalized => _max <= 0f ? 0f : _current / _max;
        public bool IsEmpty => _current <= 0f;
        public bool IsFull => _current >= _max;

        /// <summary>값이 실제로 바뀐 경우에만 발생한다. (current, max)</summary>
        public event Action<float, float> Changed;

        public void Modify(float delta)
        {
            float prev = _current;
            _current = Mathf.Clamp(_current + delta, 0f, _max);
            if (!Mathf.Approximately(prev, _current)) Changed?.Invoke(_current, _max);
        }

        public void SetMax(float value)
        {
            _max = Mathf.Max(0f, value);
            float prev = _current;
            _current = Mathf.Clamp(_current, 0f, _max);
            if (!Mathf.Approximately(prev, _current)) Changed?.Invoke(_current, _max);
        }
    }
}
