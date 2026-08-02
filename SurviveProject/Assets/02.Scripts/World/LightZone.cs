using UnityEngine;
using Survive.Player;

namespace Survive.World
{
    /// <summary>
    /// 발광 버섯 군락처럼 스스로 빛나는 영역.
    /// 세계관: "천장에 박힌 버섯들이 큰 조명역할을 겸한다"
    /// 안에 있는 동안 랜턴 배터리를 채워 준다 — 거점 구조를 만든다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class LightZone : MonoBehaviour
    {
        [Tooltip("이 안에서 초당 회복되는 배터리 양")]
        [SerializeField] float rechargePerSecond = 25f;

        void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        void OnTriggerStay(Collider other)
        {
            var lantern = other.GetComponentInParent<LanternController>();
            if (lantern != null) lantern.Recharge(rechargePerSecond * Time.deltaTime);
        }
    }
}
