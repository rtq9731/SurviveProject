using System;
using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Interaction;
using Survive.Player;

namespace Survive.Harvesting
{
    /// <summary>
    /// 자라는 식물. 생태계 순환의 출발점이다.
    ///
    /// 플레이어가 캐거나 생산자가 먹으면 단계가 내려가고, 시간이 지나면 다시 자란다.
    /// 0단계로 오래 방치되면 시들어 사라진다 — 남획하면 그 구역이 빈다.
    /// </summary>
    public class PlantNode : MonoBehaviour, IHoldInteractable
    {
        [SerializeField] PlantNodeSO definition;

        [Tooltip("크기를 조절할 대상. 비우면 이 오브젝트")]
        [SerializeField] Transform visual;

        [Header("피드백")]
        [SerializeField] MMF_Player harvestFeedback;
        [SerializeField] MMF_Player witherFeedback;

        int _단계;
        float _성장타이머;
        float _시든시간;
        bool _소멸됨;

        public PlantNodeSO Definition => definition;
        public int Stage => _단계;
        public bool IsEdible => !_소멸됨 && _단계 > 0;

        /// <summary>먹히거나 캐여서 단계가 내려갈 때.</summary>
        public event Action<PlantNode> Consumed;

        void Awake()
        {
            if (visual == null) visual = transform;
            _단계 = definition != null ? definition.maxStage : 1;
            크기갱신();
        }

        void Update()
        {
            if (_소멸됨 || definition == null) return;

            if (_단계 < definition.maxStage)
            {
                _성장타이머 += Time.deltaTime;
                if (_성장타이머 >= definition.growSeconds)
                {
                    _성장타이머 = 0f;
                    _단계++;
                    _시든시간 = 0f;
                    크기갱신();
                }
            }

            // 0단계로 오래 남아 있으면 시든다
            if (_단계 <= 0 && definition.witherSeconds > 0f)
            {
                _시든시간 += Time.deltaTime;
                if (_시든시간 >= definition.witherSeconds) 시들기();
            }
        }

        void 크기갱신()
        {
            if (definition == null || visual == null) return;
            float t = definition.maxStage <= 0 ? 1f : _단계 / (float)definition.maxStage;
            float s = Mathf.Lerp(definition.minScale, definition.maxScale, t);
            visual.localScale = Vector3.one * s;
            visual.gameObject.SetActive(_단계 > 0);
        }

        void 시들기()
        {
            _소멸됨 = true;
            witherFeedback?.PlayFeedbacks();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 생산자가 한 입 먹는다. 플레이어의 채집과 같은 경로를 쓰되 전리품은 주지 않는다.
        /// </summary>
        /// <returns>얻은 영양가. 먹을 수 없으면 0.</returns>
        public float Eat()
        {
            if (!IsEdible) return 0f;
            _단계--;
            _성장타이머 = 0f;
            _시든시간 = 0f;
            크기갱신();
            Consumed?.Invoke(this);
            return definition.nutritionPerStage;
        }

        // ── 플레이어 채집 ────────────────────────────────────────

        public float HoldDuration => definition != null ? definition.harvestSeconds : 1f;

        public string InteractionPrompt
        {
            get
            {
                if (definition == null || _소멸됨) return "";
                if (_단계 <= 0) return $"{definition.displayName} — 아직 자라지 않았다";
                return $"[E] 길게 눌러 {definition.displayName} 채집";
            }
        }

        public bool CanInteract(PlayerContext player) => IsEdible && player?.Inventory != null;

        public void OnHoldProgress(float normalized) { }
        public void OnHoldCancelled() { }

        public void Interact(PlayerContext player)
        {
            if (!IsEdible) return;

            _단계--;
            _성장타이머 = 0f;
            _시든시간 = 0f;
            크기갱신();
            harvestFeedback?.PlayFeedbacks();

            if (definition.dropsPerStage != null)
            {
                foreach (var stack in definition.dropsPerStage.Roll(new System.Random()))
                {
                    int 남은수 = player.Inventory.Add(stack.item, stack.count);
                    if (남은수 > 0)
                        Debug.LogWarning($"[PlantNode] 인벤토리가 가득 차 {stack.item.displayName} {남은수}개를 넣지 못했습니다.", this);
                }
            }

            Consumed?.Invoke(this);
        }
    }
}
