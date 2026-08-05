using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Audio;
using Survive.Domain.Audio;
using Survive.Vitals;

namespace Survive.Combat
{
    [DisallowMultipleComponent]
    public class PlayerDamageReceiver : MonoBehaviour, IDamageable
    {
        [SerializeField] PlayerVitals vitals;

        [Tooltip("피격 시 재생. 붉은 비네트·진동·경고음")]
        [SerializeField] MMF_Player hurtFeedback;

        [Tooltip("피격 시 소리. 비우면 소리 표의 playerHurt")]
        [SerializeField] AudioCueSO hurtCue;

        void Awake()
        {
            if (vitals == null) vitals = GetComponentInParent<PlayerVitals>();
        }

        public bool IsDead => vitals != null && vitals.Health.IsEmpty;

        public void TakeDamage(in DamageInfo info)
        {
            if (vitals == null || IsDead) return;

            // 자기가 낸 피해는 자기에게 돌아오지 않는다.
            // 때리는 쪽에서 이미 거르지만, 피해가 들어오는 문이 여기 하나뿐이라
            // 문 앞에서 한 번 더 본다. 생물이 때린 것은 가해자가 다르므로 그대로 들어온다.
            if (MeleeTargeting.IsSelfInflicted(transform, info.Source)) return;

            vitals.Health.Modify(-info.Amount);
            hurtFeedback?.PlayFeedbacks();

            // 내가 맞은 소리는 세계 어딘가가 아니라 내 안에서 난다. 2D로 낸다.
            var book = AudioService.Book;
            AudioService.Play2D(AudioCueBookSO.Or(hurtCue, book != null ? book.playerHurt : null));
        }
    }
}
