using System.Collections;
using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Interaction;
using Survive.Items;
using Survive.Player;

namespace Survive.Harvesting
{
    /// <summary>
    /// 누르고 있으면 자원을 주는 채집 노드.
    /// 도구 요건을 만족해야 하며, 필요하면 일정 시간 뒤 재생성된다.
    /// </summary>
    public class HarvestNode : MonoBehaviour, IHoldInteractable
    {
        [SerializeField] HarvestNodeSO definition;

        [Header("표시")]
        [Tooltip("채집 후 숨길 대상. 비우면 이 오브젝트를 숨긴다")]
        [SerializeField] GameObject visual;

        [Header("피드백")]
        [Tooltip("채집을 시작할 때")]
        [SerializeField] MMF_Player startFeedback;

        [Tooltip("채집이 끝났을 때. 파편·획득음")]
        [SerializeField] MMF_Player completeFeedback;

        bool _depleted;
        PlayerToolHolder _toolHolder;

        public HarvestNodeSO Definition => definition;
        public bool IsDepleted => _depleted;

        void Awake()
        {
            if (visual == null) visual = gameObject;
        }

        ToolItemSO equipped => _toolHolder != null ? _toolHolder.EquippedTool : null;

        public float HoldDuration
        {
            get
            {
                if (definition == null) return 1f;
                float power = equipped != null ? Mathf.Max(0.01f, equipped.harvestPower) : 1f;
                return definition.baseDuration / power;
            }
        }

        public string InteractionPrompt
        {
            get
            {
                if (definition == null || _depleted) return "";
                // 홀드형이므로 프롬프트에서 그 사실이 드러나야 한다.
                // "[E]"만 쓰면 탭으로 오해한다.
                if (ToolSatisfied(equipped)) return $"[E] 길게 눌러 {definition.displayName} 채집";
                return $"{definition.displayName} — {ToolName(definition.requiredTool)} 필요";
            }
        }

        public bool CanInteract(PlayerContext player)
        {
            if (definition == null || _depleted || player == null) return false;
            _toolHolder = player.ToolHolder;
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
            if (_depleted || definition == null) return;

            startFeedback?.StopFeedbacks();
            _depleted = true;

            if (definition.drops != null)
            {
                var loot = definition.drops.Roll(new System.Random());
                foreach (var stack in loot)
                {
                    int remaining = player.Inventory.Add(stack.item, stack.count);
                    if (remaining > 0)
                        Debug.LogWarning($"[HarvestNode] 인벤토리가 가득 차 {stack.item.displayName} {remaining}개를 넣지 못했습니다.", this);
                }
            }

            completeFeedback?.PlayFeedbacks();
            visual.SetActive(false);

            if (definition.respawnSeconds > 0f) StartCoroutine(Respawn());
        }

        IEnumerator Respawn()
        {
            yield return new WaitForSeconds(definition.respawnSeconds);
            visual.SetActive(true);
            _depleted = false;
        }
    }
}
