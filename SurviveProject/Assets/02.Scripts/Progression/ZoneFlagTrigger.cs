using UnityEngine;
using Survive.Core;
using Survive.Vitals;

namespace Survive.Progression
{
    /// <summary>
    /// 플레이어가 들어오면 ChapterDirector에 플래그를 세운다.
    /// FlagObjective가 이 플래그를 읽어 "지역 도달" 목표를 판정한다.
    ///
    /// MonoBehaviour는 클래스명과 파일명이 같아야 Unity가 스크립트를 연결한다.
    /// 한 파일에 여러 개를 넣으면 전부 "script can not be loaded"가 된다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ZoneFlagTrigger : MonoBehaviour
    {
        [SerializeField] string flagKey;
        [SerializeField] bool once = true;

        bool _발동됨;

        void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (once && _발동됨) return;
            if (other.GetComponentInParent<PlayerVitals>() == null) return;

            if (GameServices.TryGet<ChapterDirector>(out var dir))
            {
                dir.SetFlag(flagKey, 1);
                _발동됨 = true;
            }
        }
    }
}
