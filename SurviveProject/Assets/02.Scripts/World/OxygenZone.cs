using UnityEngine;
using Survive.Vitals;

namespace Survive.World
{
    /// <summary>
    /// 산소 보정 영역. 겹치면 가장 유리한 값 하나만 채택된다 (PlayerVitals 규칙).
    /// 발광 버섯 군락처럼 회복 지대는 양수, 모래폭풍 같은 위험 지대는 음수.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class OxygenZone : MonoBehaviour, IOxygenModifier
    {
        [Tooltip("초당 산소 변화량. 양수=회복, 음수=추가 소모")]
        [SerializeField] float oxygenDeltaPerSecond = 5f;

        public float OxygenDeltaPerSecond => oxygenDeltaPerSecond;

        void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            var v = other.GetComponentInParent<PlayerVitals>();
            if (v != null) v.RegisterOxygenModifier(this);
        }

        void OnTriggerExit(Collider other)
        {
            var v = other.GetComponentInParent<PlayerVitals>();
            if (v != null) v.UnregisterOxygenModifier(this);
        }
    }
}
