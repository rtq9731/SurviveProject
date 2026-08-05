using UnityEngine;
using Survive.Player;

namespace Survive.World
{
    /// <summary>
    /// 발광 버섯 군락처럼 스스로 빛나는 영역.
    /// 세계관: "천장에 박힌 버섯들이 큰 조명역할을 겸한다"
    /// 안에 있는 동안 랜턴 배터리를 채워 준다 — 거점 구조를 만든다.
    ///
    /// 군락(<see cref="GlowGrove"/>)이 물려 있으면 <b>충전량은 남은 갓에 비례한다.</b>
    /// 갓을 다 따 간 군락이 예전처럼 배터리를 채워 주면 "빛을 팔아 자원을 얻는다"는
    /// 거래가 성립하지 않는다 — 빛이 꺼진 자리에서 충전되는 것을 눈으로 보면
    /// 무엇이 무엇을 주는지 읽을 수 없게 된다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class LightZone : MonoBehaviour
    {
        [Tooltip("이 안에서 초당 회복되는 배터리 양")]
        [SerializeField] float rechargePerSecond = 25f;

        GlowGrove _grove;

        /// <summary>이 구역을 밝히는 군락. 없으면 예전처럼 항상 최대로 채워 준다.</summary>
        public GlowGrove Grove => _grove;

        /// <summary>지금 실제로 초당 채워 주는 양.</summary>
        public float CurrentRechargePerSecond =>
            _grove == null ? rechargePerSecond : rechargePerSecond * _grove.Brightness;

        /// <summary>군락을 물려 준다. <see cref="GlowGroveService"/>가 실행 시점에 부른다.</summary>
        public void BindGrove(GlowGrove grove) => _grove = grove;

        void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        void OnTriggerStay(Collider other)
        {
            float rate = CurrentRechargePerSecond;
            if (rate <= 0f) return;

            var lantern = other.GetComponentInParent<LanternController>();
            if (lantern != null) lantern.Recharge(rate * Time.deltaTime);
        }
    }
}
