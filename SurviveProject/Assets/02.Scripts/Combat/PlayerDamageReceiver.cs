using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Vitals;

namespace Survive.Combat
{
    [DisallowMultipleComponent]
    public class PlayerDamageReceiver : MonoBehaviour, IDamageable
    {
        [SerializeField] PlayerVitals vitals;

        [Tooltip("피격 시 재생. 붉은 비네트·진동·경고음")]
        [SerializeField] MMF_Player hurtFeedback;

        void Awake()
        {
            if (vitals == null) vitals = GetComponentInParent<PlayerVitals>();
        }

        public bool IsDead => vitals != null && vitals.Health.IsEmpty;

        public void TakeDamage(in DamageInfo info)
        {
            if (vitals == null || IsDead) return;
            vitals.Health.Modify(-info.Amount);
            hurtFeedback?.PlayFeedbacks();
        }
    }
}
