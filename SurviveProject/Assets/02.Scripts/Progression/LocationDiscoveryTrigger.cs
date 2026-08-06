using UnityEngine;
using Survive.Vitals;

namespace Survive.Progression
{
    /// <summary>
    /// 이 자리에 플레이어가 처음 닿으면 발견이 일어난다 (챕터 1 스펙 §8-2).
    ///
    /// <see cref="ZoneFlagTrigger"/>와 같은 모양이되 세우는 것이 플래그가 아니라
    /// <b>발견</b>이다. 플래그는 목표 판정용이라 되풀이될 수 있지만 발견은 한 번뿐이고,
    /// 그 한 번을 세는 자리는 원장이어야 한다 — <see cref="_fired"/>만 믿으면
    /// 저장본을 불러온 뒤에 같은 자리에서 AI가 또 말한다.
    ///
    /// <b>다시 울리지 않는 것은 이 컴포넌트의 공이 아니다.</b>
    /// <see cref="DiscoveryChannel.Apply"/>가 원장에 열쇠를 넣어 보고 새로 들어갔을
    /// 때만 참을 돌려준다. 여기 있는 <see cref="_fired"/>는 같은 프레임에 콜라이더가
    /// 여러 번 겹칠 때 헛일을 줄이는 것뿐이다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class LocationDiscoveryTrigger : MonoBehaviour
    {
        [Tooltip("DiscoverySO.locationId와 같은 문자열. 어긋나면 밟아도 아무 일도 없다")]
        [SerializeField] string locationId;

        /// <summary>씬에 심을 때 사람이 읽는다. 검증 하네스도 이것으로 짝을 맞춘다.</summary>
        public string LocationId
        {
            get => locationId;
            set => locationId = value;
        }

        /// <summary>여기서 발견이 실제로 일어났는가. 검증 하네스가 본다.</summary>
        public bool Fired => _fired;

        bool _fired;

        void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (_fired) return;
            if (other.GetComponentInParent<PlayerVitals>() == null) return;

            Fire();
        }

        /// <summary>
        /// 밟은 것으로 친다. 검증 하네스가 콜라이더 없이 부를 자리가 필요해서 열어 둔다.
        /// </summary>
        public bool Fire()
        {
            var service = UnlockService.Instance;
            if (service == null) return false;

            if (!LocationDiscovery.TryDiscover(service.Book, service.Ledger, locationId, out var d))
            {
                // 이미 겪은 자리다. 다시 말하지는 않지만 이 콜라이더는 조용해진다.
                _fired = true;
                return false;
            }

            _fired = true;
            service.Announce(d.line);
            return true;
        }
    }
}
