using System;
using UnityEngine;

namespace Survive.Core
{
    /// <summary>
    /// 페이로드가 없는 이벤트 채널.
    /// 다른 구체 채널은 각자 자기 이름의 파일에 있다 — ScriptableObject를
    /// 한 파일에 여러 개 넣으면 에셋의 m_Script 참조가 연결되지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Survive/Core/Void Event Channel")]
    public class VoidEventChannelSO : ScriptableObject
    {
        public event Action OnRaised;
        public void Raise() => OnRaised?.Invoke();
        void OnDisable() => OnRaised = null;
    }
}
