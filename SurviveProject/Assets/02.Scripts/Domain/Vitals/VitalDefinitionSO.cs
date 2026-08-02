using UnityEngine;

namespace Survive.Vitals
{
    [CreateAssetMenu(menuName = "Survive/Vitals/Vital Definition")]
    public class VitalDefinitionSO : ScriptableObject
    {
        [Tooltip("health 또는 oxygen")]
        public string id = "health";

        public string displayName = "체력";

        public float maxValue = 100f;
        public float startValue = 100f;

        [Tooltip("초당 자연 변화량. 산소는 음수, 체력은 0")]
        public float passiveRatePerSecond;
    }
}
