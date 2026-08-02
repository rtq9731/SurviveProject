using UnityEngine;

namespace Survive.Progression
{
    [CreateAssetMenu(menuName = "Survive/Progression/Chapter")]
    public class ChapterSO : ScriptableObject
    {
        public string id;
        public string title;
        public ObjectiveSO[] objectives = new ObjectiveSO[0];
    }
}
