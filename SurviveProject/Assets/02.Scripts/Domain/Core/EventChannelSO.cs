using System;
using UnityEngine;

namespace Survive.Core
{
    /// <summary>
    /// 시스템 간 유일한 통신 수단. 발신자와 수신자가 서로를 모른다.
    /// </summary>
    public abstract class EventChannelSO<T> : ScriptableObject
    {
        public event Action<T> OnRaised;

        public void Raise(T payload) => OnRaised?.Invoke(payload);

        /// <summary>
        /// ScriptableObject는 플레이 모드 종료 후에도 살아남아 구독이 남는다.
        /// 에디터에서 유령 구독이 쌓이는 것을 막는다.
        /// </summary>
        protected virtual void OnDisable() => OnRaised = null;
    }
}
