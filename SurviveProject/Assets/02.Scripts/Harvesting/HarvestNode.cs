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

        bool _고갈됨;
        PlayerToolHolder _도구;

        public HarvestNodeSO Definition => definition;
        public bool IsDepleted => _고갈됨;

        void Awake()
        {
            if (visual == null) visual = gameObject;
        }

        ToolItemSO 장착도구 => _도구 != null ? _도구.EquippedTool : null;

        public float HoldDuration
        {
            get
            {
                if (definition == null) return 1f;
                float power = 장착도구 != null ? Mathf.Max(0.01f, 장착도구.harvestPower) : 1f;
                return definition.baseDuration / power;
            }
        }

        public string InteractionPrompt
        {
            get
            {
                if (definition == null || _고갈됨) return "";
                // 홀드형이므로 프롬프트에서 그 사실이 드러나야 한다.
                // "[E]"만 쓰면 탭으로 오해한다.
                if (도구충족(장착도구)) return $"[E] 길게 눌러 {definition.displayName} 채집";
                return $"{definition.displayName} — {도구이름(definition.requiredTool)} 필요";
            }
        }

        public bool CanInteract(PlayerContext player)
        {
            if (definition == null || _고갈됨 || player == null) return false;
            _도구 = player.ToolHolder;
            return 도구충족(장착도구);
        }

        bool 도구충족(ToolItemSO tool)
        {
            if (definition.requiredTool == ToolType.None) return true;
            if (tool == null) return false;
            return tool.toolType == definition.requiredTool && tool.tier >= definition.requiredTier;
        }

        static string 도구이름(ToolType t) => t switch
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
            if (_고갈됨 || definition == null) return;

            startFeedback?.StopFeedbacks();
            _고갈됨 = true;

            if (definition.drops != null)
            {
                var 전리품 = definition.drops.Roll(new System.Random());
                foreach (var stack in 전리품)
                {
                    int 남은수 = player.Inventory.Add(stack.item, stack.count);
                    if (남은수 > 0)
                        Debug.LogWarning($"[HarvestNode] 인벤토리가 가득 차 {stack.item.displayName} {남은수}개를 넣지 못했습니다.", this);
                }
            }

            completeFeedback?.PlayFeedbacks();
            visual.SetActive(false);

            if (definition.respawnSeconds > 0f) StartCoroutine(재생성());
        }

        IEnumerator 재생성()
        {
            yield return new WaitForSeconds(definition.respawnSeconds);
            visual.SetActive(true);
            _고갈됨 = false;
        }
    }
}
