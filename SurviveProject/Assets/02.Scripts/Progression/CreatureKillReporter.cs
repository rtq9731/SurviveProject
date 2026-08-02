using UnityEngine;
using Survive.Combat;
using Survive.Core;

namespace Survive.Progression
{
    /// <summary>
    /// 생물이 죽으면 "kill:{id}" 플래그를 누적한다.
    /// KillCreatureObjective가 이 값을 읽는다.
    /// </summary>
    [RequireComponent(typeof(CreatureHealth))]
    public class CreatureKillReporter : MonoBehaviour
    {
        CreatureHealth _health;

        void Awake() => _health = GetComponent<CreatureHealth>();
        void OnEnable() { if (_health != null) _health.Died += Report; }
        void OnDisable() { if (_health != null) _health.Died -= Report; }

        void Report(CreatureHealth h)
        {
            var id = h.Definition != null ? h.Definition.id : null;
            if (string.IsNullOrEmpty(id)) return;

            if (GameServices.TryGet<ChapterDirector>(out var dir))
                dir.AddFlag("kill:" + id);
        }
    }
}
