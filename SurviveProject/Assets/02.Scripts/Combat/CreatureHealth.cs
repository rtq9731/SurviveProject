using System;
using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Audio;
using Survive.Creatures;
using Survive.Domain.Audio;
using Survive.Interaction;
using Survive.Items;

namespace Survive.Combat
{
    /// <summary>생물의 체력과 사망 처리. 사망 시 전리품을 떨군다.</summary>
    [DisallowMultipleComponent]
    public class CreatureHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] CreatureDefinitionSO definition;

        [Header("피드백")]
        [Tooltip("피격 시. 플래시·경직")]
        [SerializeField] MMF_Player hitFeedback;

        [Tooltip("사망 시. 파편·폭발음")]
        [SerializeField] MMF_Player deathFeedback;

        [Header("소리")]
        // 둘 다 비움이 기본이다. 개체별로 다르게 하고 싶을 때만 채운다 —
        // 종별 소리는 CreatureDefinitionSO가 아니라 소리 표에 두는 편이 낫다.
        [Tooltip("피격 시. 비우면 소리 표의 creatureHit")]
        [SerializeField] AudioCueSO hitCue;

        [Tooltip("사망 시. 비우면 소리 표의 creatureDeath")]
        [SerializeField] AudioCueSO deathCue;

        [Tooltip("전리품을 떨굴 때 쓸 프리팹. 비우면 ItemPickup 오브젝트를 즉석에서 만든다")]
        [SerializeField] GameObject pickupPrefab;

        [Tooltip("축적한 스크랩을 떨굴 때 쓸 아이템. 보통 scrap")]
        [SerializeField] ItemDataSO _scrapItem;

        float _health;

        public CreatureDefinitionSO Definition => definition;
        public bool IsDead { get; private set; }
        public event Action<CreatureHealth> Died;
        public event Action<CreatureHealth, DamageInfo> Damaged;

        void Awake() => _health = definition != null ? definition.maxHealth : 10f;

        public void TakeDamage(in DamageInfo info)
        {
            if (IsDead) return;

            _health -= info.Amount;
            hitFeedback?.PlayFeedbacks();

            // 맞은 자리에서 난다. 몸통 한가운데가 아니라 도구가 닿은 점이라야
            // 어느 쪽에서 때렸는지가 소리로 남는다.
            var book = AudioService.Book;
            AudioService.Play(AudioCueBookSO.Or(hitCue, book != null ? book.creatureHit : null),
                              info.HitPoint);

            Damaged?.Invoke(this, info);

            if (_health <= 0f) Die();
        }

        void Die()
        {
            IsDead = true;
            deathFeedback?.PlayFeedbacks();

            // 이 몸은 0.1초 뒤에 사라진다. 자기 몸에 붙은 AudioSource로 냈다면
            // 소리가 잘려 나간다 — 창구를 따로 둔 이유가 바로 이런 자리다.
            var book = AudioService.Book;
            AudioService.Play(AudioCueBookSO.Or(deathCue, book != null ? book.creatureDeath : null),
                              transform.position);

            DropLoot();
            Died?.Invoke(this);
            Destroy(gameObject, 0.1f);
        }

        void DropLoot()
        {
            if (definition?.drops == null) return;

            var loot = definition.drops.Roll(new System.Random());

            // 생산자가 먹어서 축적한 스크랩을 더한다.
            // 배부른 개체를 노리면 더 얻는다 — 관찰에 대한 보상이다.
            var feeding = GetComponent<Survive.Creatures.CreatureFeeding>();
            if (feeding != null && feeding.Stored > 0f && _scrapItem != null)
            {
                int added = Mathf.RoundToInt(feeding.Stored);
                if (added > 0) loot.Add(new Survive.Items.ItemStack(_scrapItem, added));
            }

            // 떨구는 방식은 채집물 파괴와 같아야 한다. ItemDropper에 모아 두었다.
            var origin = transform.position + Vector3.up * 0.3f;
            foreach (var stack in loot)
                for (int i = 0; i < stack.count; i++)
                    ItemDropper.Drop(stack.item, 1, origin, pickupPrefab);
        }
    }
}
