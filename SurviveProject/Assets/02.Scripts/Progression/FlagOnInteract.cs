using UnityEngine;
using Survive.Core;

namespace Survive.Progression
{
    /// <summary>
    /// 호출되면 플래그를 세운다. 제작대 사용처럼 "이 장치를 썼다"를 진행도에 남길 자리에 붙인다.
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
