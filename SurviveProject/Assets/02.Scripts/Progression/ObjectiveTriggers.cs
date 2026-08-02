using UnityEngine;
using Survive.Combat;
using Survive.Core;
using Survive.Vitals;

namespace Survive.Progression
{
    /// <summary>
    /// 플레이어가 들어오면 ChapterDirector에 플래그를 세운다.
    /// FlagObjective가 이 플래그를 읽어 "지역 도달" 목표를 판정한다.
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

    /// <summary>
    /// 생물이 죽으면 "kill:{id}" 플래그를 누적한다.
    /// KillCreatureObjective가 이 값을 읽는다.
    /// </summary>
    [RequireComponent(typeof(CreatureHealth))]
    public class CreatureKillReporter : MonoBehaviour
    {
        CreatureHealth _health;

        void Awake() => _health = GetComponent<CreatureHealth>();
        void OnEnable() => _health.Died += 보고;
        void OnDisable() => _health.Died -= 보고;

        void 보고(CreatureHealth h)
        {
            var id = h.Definition != null ? h.Definition.id : null;
            if (string.IsNullOrEmpty(id)) return;

            if (GameServices.TryGet<ChapterDirector>(out var dir))
                dir.AddFlag("kill:" + id);
        }
    }

    /// <summary>
    /// 상호작용 대상이 사용되면 플래그를 세운다. 포탈 기동·제작대 사용 등에 붙인다.
    /// </summary>
    public class FlagOnInteract : MonoBehaviour
    {
        [SerializeField] string flagKey;

        public void Raise()
        {
            if (GameServices.TryGet<ChapterDirector>(out var dir))
                dir.SetFlag(flagKey, 1);
        }
    }
}
