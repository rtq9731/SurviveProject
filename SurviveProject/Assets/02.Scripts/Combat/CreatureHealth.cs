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

            // <b>난수의 주인은 세계 시드다</b> (<see cref="Survive.World.WorldSeed"/>).
            // 자리는 <b>죽은 자리</b>다 — 생물은 옮겨 다니므로 고정된 자리가 없고,
            // 죽은 자리는 그 판에서 실제로 일어난 일의 좌표라 재현할 수 있는
            // 유일한 값이다. 같은 세계를 두 번 돌려 같은 자리에서 죽으면 같은 것을
            // 떨군다. 다만 <b>죽는 자리 자체</b>는 배회 난수가 정하고, 그쪽은 아직
            // 주인이 없다 — 그날까지 이 굴림의 재현성은 거기에 걸려 있다.
            var loot = definition.drops.Roll(
                Survive.World.WorldSeed.Rng(Survive.World.WorldSeedBranch.CreatureLoot,
                                            transform.position));

            // 생산자가 먹어서 축적한 스크랩을 더한다.
            // 배부른 개체를 노리면 더 얻는다 — 관찰에 대한 보상이다.
            //
            // <b>셈은 여기 두지 않는다</b> (<see cref="Survive.Creatures.FeedingPayoff"/>).
            // 이 한 줄이 기획서 §3.4의 차별화 축이 서느냐를 정하는 배율이라, 묻어 두면
            // 지금 몇 배인지도 잴 수 없다. 규칙으로 세워 두어야 실측이 값을 읽는다.
            var feeding = GetComponent<Survive.Creatures.CreatureFeeding>();
            if (feeding != null && _scrapItem != null)
            {
                int added = Survive.Creatures.FeedingPayoff.Bonus(feeding.Stored);
                if (added > 0) loot.Add(new Survive.Items.ItemStack(_scrapItem, added));
            }

            // 떨구는 방식은 채집물 파괴와 같아야 한다. ItemDropper에 모아 두었다.
            // 한 자리에서 여럿을 떨구므로 흩뿌림에는 순번을 준다. 같은 번호를 주면
            // 파생이 같아져 전부 한 점에 겹쳐 떨어진다.
            var origin = transform.position + Vector3.up * 0.3f;
            int 떨군수 = 0;
            foreach (var stack in loot)
                for (int i = 0; i < stack.count; i++)
                    ItemDropper.Drop(stack.item, 1, origin, pickupPrefab, occasion: 떨군수++);
        }
    }
}
