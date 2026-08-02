using System;
using UnityEngine;

namespace Survive.Core
{
    [CreateAssetMenu(menuName = "Survive/Core/Void Event Channel")]
    public class VoidEventChannelSO : ScriptableObject
    {
        public event Action OnRaised;
        public void Raise() => OnRaised?.Invoke();
        void OnDisable() => OnRaised = null;
    }

    [CreateAssetMenu(menuName = "Survive/Core/Int Event Channel")]
    public class IntEventChannelSO : EventChannelSO<int> { }

    [CreateAssetMenu(menuName = "Survive/Core/Float Event Channel")]
    public class FloatEventChannelSO : EventChannelSO<float> { }

    [CreateAssetMenu(menuName = "Survive/Core/String Event Channel")]
    public class StringEventChannelSO : EventChannelSO<string> { }
}
