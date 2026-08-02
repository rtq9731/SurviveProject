using System;
using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Creatures;
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
            Damaged?.Invoke(this, info);

            if (_health <= 0f) Die();
        }

        void Die()
        {
            IsDead = true;
            deathFeedback?.PlayFeedbacks();
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

            foreach (var stack in loot)
            {
                GameObject go;
                if (pickupPrefab != null)
                {
                    go = Instantiate(pickupPrefab, transform.position + Vector3.up * 0.3f, Quaternion.identity);
                }
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.localScale = Vector3.one * 0.3f;
                    go.transform.position = transform.position + Vector3.up * 0.3f;
                }
                go.name = "Drop_" + stack.item.id;

                var pickup = go.GetComponent<ItemPickup>();
                if (pickup == null) pickup = go.AddComponent<ItemPickup>();
                pickup.Setup(stack.item, stack.count);
            }
        }
    }
}
