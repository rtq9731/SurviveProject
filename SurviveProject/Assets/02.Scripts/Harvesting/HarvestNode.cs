using System.Collections;
using UnityEngine;
using DG.Tweening;
using MoreMountains.Feedbacks;
using Survive.Combat;
using Survive.Interaction;
using Survive.Items;
using Survive.Player;

namespace Survive.Harvesting
{
    /// <summary>
    /// 자원을 주는 채집 노드. 두 가지 방식이 있다.
    ///
    /// <b>맨손 채집</b> — 눌러서 채운다. 잔해를 뒤지는 동작이라 시간이 걸린다.
    /// <b>도구 채집</b> — 때려서 부순다. 곡괭이를 만든 보람이 손끝에 있어야 해서,
    /// 광맥을 E로 홀드하는 대신 실제로 내리찍고 부서지는 것을 보게 한다.
    /// 부수면 자원이 인벤토리로 순간이동하지 않고 바닥에 튀어나와 굴러서,
    /// 플레이어가 가서 줍는다.
    /// </summary>
    public class HarvestNode : MonoBehaviour, IHoldInteractable, IDamageable
    {
        [SerializeField] HarvestNodeSO definition;

        [Header("표시")]
        [Tooltip("채집 후 숨길 대상. 비우면 이 오브젝트를 숨긴다")]
        [SerializeField] GameObject visual;

        [Tooltip("떨어뜨릴 아이템의 겉모습. 비우면 임시 큐브로 떨군다")]
        [SerializeField] GameObject dropPrefab;

        [Header("피드백")]
        [Tooltip("채집을 시작할 때")]
        [SerializeField] MMF_Player startFeedback;

        [Tooltip("채집이 끝났을 때. 파편·획득음")]
        [SerializeField] MMF_Player completeFeedback;

        [Tooltip("때렸는데 부서지지는 않았을 때. 파편·타격음")]
        [SerializeField] MMF_Player hitFeedback;

        bool _depleted;
        float _health = -1f;
        PlayerToolHolder _toolHolder;
        Tween _shake;
        Vector3 _baseScale;

        public HarvestNodeSO Definition => definition;
        public bool IsDepleted => _depleted;

        /// <summary>
        /// 실행 시점에 노드를 세우는 서비스가 정의를 물려 준다.
        ///
        /// 씬에 놓인 노드는 인스펙터에서 정의를 받지만, MainScene을 건드리지 않고
        /// 세워야 하는 것들(거대 버섯)은 코드로 붙는다 — 그때 쓰는 창구다.
        /// 이미 정의가 있는 노드는 덮어쓰지 않는다.
        /// </summary>
        public void Bind(HarvestNodeSO def)
        {
            if (def == null || definition != null) return;
            definition = def;
            _health = def.durability;
        }

        /// <summary>도구가 필요한 노드는 눌러서 캐는 게 아니라 부순다.</summary>
        public bool IsBreakable => definition != null && definition.requiredTool != ToolType.None;

        /// <summary>남은 내구도 비율. 0~1. 부술 수 없는 노드는 항상 1.</summary>
        public float HealthNormalized =>
            !IsBreakable || definition.durability <= 0f ? 1f
            : Mathf.Clamp01(CurrentHealth / definition.durability);

        float CurrentHealth
        {
            get
            {
                if (_health < 0f) _health = definition != null ? definition.durability : 1f;
                return _health;
            }
        }

        void Awake()
        {
            if (visual == null) visual = gameObject;
            _baseScale = transform.localScale;
        }

        void OnDestroy() => _shake?.Kill();

        ToolItemSO equipped => _toolHolder != null ? _toolHolder.EquippedTool : null;

        // ── 맨손 채집 (홀드) ─────────────────────────────────────

        public float HoldDuration
        {
            get
            {
                if (definition == null) return 1f;
                if (IsBreakable) return 0f;      // 부수는 노드는 홀드하지 않는다
                float power = equipped != null ? Mathf.Max(0.01f, equipped.harvestPower) : 1f;
                return definition.baseDuration / power;
            }
        }

        public string InteractionPrompt
        {
            get
            {
                if (definition == null || _depleted) return "";

                if (IsBreakable)
                {
                    if (!ToolSatisfied(equipped))
                        return $"{definition.displayName} · {ToolName(definition.requiredTool)} 필요";

                    // 부수는 대상에는 E가 아니라 공격 키를 안내해야 한다
                    int pct = Mathf.CeilToInt(HealthNormalized * 100f);
                    return $"{definition.displayName} · 좌클릭으로 부순다 ({pct}%)";
                }

                // 홀드형이므로 프롬프트에서 그 사실이 드러나야 한다.
                // "[E]"만 쓰면 탭으로 오해한다.
                return $"[E] 길게 눌러 {definition.displayName} 채집";
            }
        }

        public bool CanInteract(PlayerContext player)
        {
            if (definition == null || _depleted || player == null) return false;
            _toolHolder = player.ToolHolder;

            // 부수는 노드는 E로 상호작용하지 않는다. 프롬프트만 보여준다.
            if (IsBreakable) return false;
            return ToolSatisfied(equipped);
        }

        bool ToolSatisfied(ToolItemSO tool)
        {
            if (definition.requiredTool == ToolType.None) return true;
            if (tool == null) return false;
            return tool.toolType == definition.requiredTool && tool.tier >= definition.requiredTier;
        }

        static string ToolName(ToolType t) => t switch
        {
            ToolType.Pickaxe => "곡괭이",
            ToolType.Hammer => "망치",
            ToolType.Axe => "도끼",
            _ => "도구"
        };

        public void OnHoldProgress(float normalized)
        {
            if (normalized > 0f && startFeedback != null && !startFeedback.IsPlaying)
                startFeedback.PlayFeedbacks();
        }

        public void OnHoldCancelled() => startFeedback?.StopFeedbacks();

        public void Interact(PlayerContext player)
        {
            if (_depleted || definition == null || IsBreakable) return;

            startFeedback?.StopFeedbacks();
            Deplete(toInventory: player);
        }

        // ── 도구 채집 (파괴) ─────────────────────────────────────

        bool IDamageable.IsDead => _depleted;

        public void TakeDamage(in DamageInfo info)
        {
            if (_depleted || definition == null || !IsBreakable) return;

            // 맞는 순간의 도구를 본다. 곡괭이 없이 주먹으로는 광맥이 깨지지 않는다.
            var holder = info.Source != null
                ? info.Source.GetComponentInParent<PlayerToolHolder>()
                : null;
            if (holder != null) _toolHolder = holder;

            if (!ToolSatisfied(equipped))
            {
                // 튕겨나가는 반응. 아무 일도 안 일어나면 버그로 오해한다.
                Recoil(0.06f);
                return;
            }

            _health = CurrentHealth - Mathf.Max(0.01f, info.Amount);
            hitFeedback?.PlayFeedbacks();

            if (_health > 0f)
            {
                Recoil(0.14f);
                return;
            }

            Break(info.HitPoint);
        }

        /// <summary>맞은 티가 나게 흔든다. 남은 내구도가 적을수록 크게 흔들린다.</summary>
        void Recoil(float strength)
        {
            _shake?.Kill();
            transform.localScale = _baseScale;
            float extra = Mathf.Lerp(1.4f, 1f, HealthNormalized);
            _shake = transform.DOPunchScale(_baseScale * strength * extra, 0.22f, 9, 0.7f);
        }

        void Break(Vector3 hitPoint)
        {
            _shake?.Kill();
            transform.localScale = _baseScale;

            Vector3 origin = visual != null
                ? visual.GetComponentInChildren<Renderer>()?.bounds.center ?? transform.position
                : transform.position;

            Deplete(toInventory: null, dropAt: origin);
        }

        // ── 공통 ─────────────────────────────────────────────────

        /// <summary>
        /// 노드를 소진시킨다. <paramref name="toInventory"/>가 있으면 바로 넣고,
        /// 없으면 바닥에 떨군다.
        /// </summary>
        void Deplete(PlayerContext toInventory, Vector3? dropAt = null)
        {
            _depleted = true;

            if (definition.drops != null)
            {
                var loot = definition.drops.Roll(new System.Random());
                foreach (var stack in loot)
                {
                    if (toInventory != null)
                    {
                        int remaining = toInventory.Inventory.Add(stack.item, stack.count);
                        if (remaining > 0)
                            Debug.LogWarning($"[HarvestNode] 인벤토리가 가득 차 {stack.item.displayName} " +
                                             $"{remaining}개를 넣지 못했습니다.", this);
                    }
                    else
                    {
                        // 하나씩 떨궈야 흩어지는 맛이 난다
                        for (int i = 0; i < stack.count; i++)
                            ItemDropper.Drop(stack.item, 1, dropAt ?? transform.position, dropPrefab);
                    }
                }
            }

            completeFeedback?.PlayFeedbacks();
            SetVisible(false);

            if (definition.respawnSeconds > 0f) StartCoroutine(Respawn());
        }

        IEnumerator Respawn()
        {
            yield return new WaitForSeconds(definition.respawnSeconds);
            SetVisible(true);
            _health = definition.durability;
            _depleted = false;
        }

        /// <summary>
        /// 캔 자리를 감추거나 되돌린다.
        ///
        /// <b>왜 통째로 끄지 않는 경우가 있는가.</b> <see cref="visual"/>이 이 오브젝트
        /// 자신인데 재생까지 해야 하면, 오브젝트를 끄는 순간 <see cref="Respawn"/>
        /// 코루틴이 함께 죽어 <b>영영 돌아오지 않는다</b>. 그래서 그때만은 보이는 것과
        /// 만져지는 것만 끈다 — <c>GlowCapCluster</c>가 같은 이유로 택한 방식이다.
        ///
        /// 빛도 함께 끈다. 발광하는 거대 버섯을 베면 그 자리의 빛도 사라져야 한다.
        /// 밑동만 남은 자리가 여전히 환하면 무엇을 벤 것인지 알 수 없다.
        ///
        /// 재생하지 않는 노드(씬에 놓인 광맥·잔해 마흔 남짓)는 전과 똑같이
        /// 오브젝트째 끈다. 돌아오지 않을 것에 굳이 다른 길을 낼 이유가 없다.
        /// </summary>
        void SetVisible(bool on)
        {
            if (visual != gameObject || definition == null || definition.respawnSeconds <= 0f)
            {
                visual.SetActive(on);
                return;
            }

            if (_renderers == null) _renderers = GetComponentsInChildren<Renderer>(true);
            if (_colliders == null) _colliders = GetComponentsInChildren<Collider>(true);
            if (_lights == null) _lights = GetComponentsInChildren<Light>(true);

            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].enabled = on;
            for (int i = 0; i < _colliders.Length; i++)
                if (_colliders[i] != null) _colliders[i].enabled = on;
            for (int i = 0; i < _lights.Length; i++)
                if (_lights[i] != null) _lights[i].enabled = on;
        }

        Renderer[] _renderers;
        Collider[] _colliders;
        Light[] _lights;
    }
}
