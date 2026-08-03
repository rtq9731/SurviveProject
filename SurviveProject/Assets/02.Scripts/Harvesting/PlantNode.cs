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

        int _stage;
        float _growTimer;
        float _witherTimer;
        bool _gone;

        public PlantNodeSO Definition => definition;
        public int Stage => _stage;
        public bool IsEdible => !_gone && _stage > 0;

        /// <summary>먹히거나 캐여서 단계가 내려갈 때.</summary>
        public event Action<PlantNode> Consumed;

        void Awake()
        {
            if (visual == null) visual = transform;
            _stage = definition != null ? definition.maxStage : 1;
            RefreshScale();
        }

        void Update()
        {
            if (_gone || definition == null) return;

            if (_stage < definition.maxStage)
            {
                _growTimer += Time.deltaTime;
                if (_growTimer >= definition.growSeconds)
                {
                    _growTimer = 0f;
                    _stage++;
                    _witherTimer = 0f;
                    RefreshScale();
                }
            }

            // 0단계로 오래 남아 있으면 시든다
            if (_stage <= 0 && definition.witherSeconds > 0f)
            {
                _witherTimer += Time.deltaTime;
                if (_witherTimer >= definition.witherSeconds) Wither();
            }
        }

        void RefreshScale()
        {
            if (definition == null || visual == null) return;
            float t = definition.maxStage <= 0 ? 1f : _stage / (float)definition.maxStage;
            float s = Mathf.Lerp(definition.minScale, definition.maxScale, t);
            visual.localScale = Vector3.one * s;
            visual.gameObject.SetActive(_stage > 0);
        }

        void Wither()
        {
            _gone = true;
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
            _stage--;
            _growTimer = 0f;
            _witherTimer = 0f;
            RefreshScale();
            Consumed?.Invoke(this);
            return definition.nutritionPerStage;
        }

        // ── 플레이어 채집 ────────────────────────────────────────

        public float HoldDuration => definition != null ? definition.harvestSeconds : 1f;

        public string InteractionPrompt
        {
            get
            {
                if (definition == null || _gone) return "";
                if (_stage <= 0) return $"{definition.displayName} · 아직 자라지 않았다";
                return $"[E] 길게 눌러 {definition.displayName} 채집";
            }
        }

        public bool CanInteract(PlayerContext player) => IsEdible && player?.Inventory != null;

        public void OnHoldProgress(float normalized) { }
        public void OnHoldCancelled() { }

        public void Interact(PlayerContext player)
        {
            if (!IsEdible) return;

            _stage--;
            _growTimer = 0f;
            _witherTimer = 0f;
            RefreshScale();
            harvestFeedback?.PlayFeedbacks();

            if (definition.dropsPerStage != null)
            {
                foreach (var stack in definition.dropsPerStage.Roll(new System.Random()))
                {
                    int remaining = player.Inventory.Add(stack.item, stack.count);
                    if (remaining > 0)
                        Debug.LogWarning($"[PlantNode] 인벤토리가 가득 차 {stack.item.displayName} {remaining}개를 넣지 못했습니다.", this);
                }
            }

            Consumed?.Invoke(this);
        }
    }
}
