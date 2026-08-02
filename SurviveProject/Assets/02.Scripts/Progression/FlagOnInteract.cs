using UnityEngine;
using Survive.Core;

namespace Survive.Progression
{
    /// <summary>
    /// 호출되면 플래그를 세운다. 포탈 기동·제작대 사용 등에 붙인다.
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
