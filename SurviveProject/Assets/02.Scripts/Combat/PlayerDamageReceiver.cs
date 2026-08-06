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

        /// <summary>시야 밖(옆을 넘어 뒤쪽)에서 온 것인가. 몸의 정면을 기준으로 잰다.</summary>
        bool 등_뒤였는가(Vector3 sourcePosition)
        {
            var body = GetComponentInParent<Survive.Player.PlayerContext>()?.transform ?? transform;
            float deg = HurtBearing.Degrees(body.forward, body.position, sourcePosition);
            return Mathf.Abs(deg) > 90f;
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (vitals == null || IsDead) return;

            // 자기가 낸 피해는 자기에게 돌아오지 않는다.
            // 때리는 쪽에서 이미 거르지만, 피해가 들어오는 문이 여기 하나뿐이라
            // 문 앞에서 한 번 더 본다. 생물이 때린 것은 가해자가 다르므로 그대로 들어온다.
            if (MeleeTargeting.IsSelfInflicted(transform, info.Source)) return;

            vitals.Health.Modify(-info.Amount);

            // <b>어느 쪽에서 맞았는지를 화면에 얹는다.</b> 랜턴이 앞으로 밀려
            // 등 뒤가 사각이 된 뒤로, 사람이 맞는 자리는 대개 보이지 않는 곳이다.
            // 방향을 말해 주지 않으면 그것은 난이도가 아니라 억울함이 된다(기획서 §9).
            // 체력을 깎은 뒤에 부른다 — 잔향을 세우는 것이 체력 변화 쪽이라,
            // 먼저 부르면 방금 얹은 방향이 곧바로 지워진다.
            if (info.Source != null)
                Survive.Art.PostFxService.ReportHurtFrom(info.Source.transform.position);

            hurtFeedback?.PlayFeedbacks();

            // 내가 맞은 소리는 세계 어딘가가 아니라 내 안에서 난다. 2D로 낸다.
            var book = AudioService.Book;
            var cue = AudioCueBookSO.Or(hurtCue, book != null ? book.playerHurt : null);
            AudioService.Play2D(cue);

            // <b>그리고 못 본 데서 맞았으면 그쪽에서도 한 번 난다.</b> 2D 소리는
            // "맞았다"는 말할 수 있어도 "어디서"는 말하지 못한다. 등 뒤 사각이 생긴
            // 뒤로는 그 "어디서"가 없으면 억울함이 된다(기획서 §9).
            // 앞에서 맞은 것은 이미 눈으로 봤으므로 겹쳐 내지 않는다.
            //
            // 지금은 같은 큐를 자리에 두고 3D로 한 번 더 낸다. <b>전용 큐와 클립은
            // 사람의 몫</b>이지만(실행 스펙 §16), 자리는 여기다 — 클립이 비어 있어도
            // 요청은 세어지므로 배선은 검증된다.
            if (info.Source != null && 등_뒤였는가(info.Source.transform.position))
                AudioService.Play(cue, info.Source.transform.position);
        }
    }
}
