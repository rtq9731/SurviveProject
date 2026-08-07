using UnityEngine;
using Survive.Vitals;

namespace Survive.World
{
    /// <summary>
    /// 산소 보정 영역. 겹치면 가장 유리한 값 하나만 채택된다 (PlayerVitals 규칙).
    /// 발광 버섯 군락처럼 회복 지대는 양수, 매크로늄에 잠긴 자리 같은 위험 지대는 음수.
    ///
    /// <b>액체의 종류는 여기서 보지 않는다.</b> 산소는 성분이 아니라 「숨을 쉴 수 있는가」의
    /// 문제여서, 호수에 잠겨도 매크로늄에 잠겨도 똑같이 준다(기획서 §5.1).
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
