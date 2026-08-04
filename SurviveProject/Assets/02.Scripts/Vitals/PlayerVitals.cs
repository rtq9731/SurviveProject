using System;
using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Core;

namespace Survive.Vitals
{
    /// <summary>
    /// 플레이어의 체력과 산소를 보유하고 매 프레임 갱신한다.
    /// 산소가 0이면 체력이 깎인다 (질식).
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerVitals : MonoBehaviour
    {
        [SerializeField] VitalDefinitionSO healthDefinition;
        [SerializeField] VitalDefinitionSO oxygenDefinition;

        [Tooltip("산소가 0일 때 초당 깎이는 체력")]
        [SerializeField] float suffocationDamagePerSecond = 5f;

        [Header("피드백")]
        [Tooltip("산소가 경고선 아래로 처음 떨어질 때 재생. 경고음·화면 가장자리 맥동")]
        [SerializeField] MMF_Player oxygenWarningFeedback;

        [Tooltip("산소가 경고선 위로 회복될 때 재생")]
        [SerializeField] MMF_Player oxygenRecoveredFeedback;

        [Range(0f, 1f)]
        [Tooltip("이 비율 아래로 내려가면 경고. 챕터 1의 핵심 압박 장치")]
        [SerializeField] float oxygenWarningThreshold = 0.2f;

        [Tooltip("사망 시 재생")]
        [SerializeField] MMF_Player deathFeedback;

        readonly List<IOxygenModifier> _oxygenModifiers = new List<IOxygenModifier>();

        public Vital Health { get; private set; }
        public Vital Oxygen { get; private set; }

        public event Action Died;

        bool _deathReported;
        bool _warning;

        void Awake()
        {
            Health = Create(healthDefinition, 100f);
            Oxygen = Create(oxygenDefinition, 100f);
        }

        static Vital Create(VitalDefinitionSO def, float defaultMax)
        {
            if (def == null) return new Vital(defaultMax, defaultMax);
            return new Vital(def.maxValue, def.startValue);
        }

        void OnEnable() => GameServices.Register(this);
        void OnDisable() => GameServices.Unregister<PlayerVitals>();

        void Update()
        {
            float dt = Time.deltaTime;

            Oxygen.Modify(CurrentOxygenRate() * dt);

            if (Oxygen.IsEmpty)
                Health.Modify(-suffocationDamagePerSecond * dt);
            else if (healthDefinition != null && healthDefinition.passiveRatePerSecond != 0f)
                Health.Modify(healthDefinition.passiveRatePerSecond * dt);

            RefreshOxygenWarning();

            if (Health.IsEmpty && !_deathReported)
            {
                _deathReported = true;
                deathFeedback?.PlayFeedbacks();
                Died?.Invoke();
            }
            // 체력이 돌아오면 다음 사망을 다시 보고한다. 걸쇠를 풀지 않으면
            // 한 판에 한 번만 죽을 수 있고, 사망 드롭도 첫 죽음에서만 걸린다.
            else if (!Health.IsEmpty && _deathReported)
            {
                _deathReported = false;
            }
        }

        void RefreshOxygenWarning()
        {
            bool danger = Oxygen.Normalized <= oxygenWarningThreshold;

            if (danger && !_warning)
            {
                _warning = true;
                oxygenWarningFeedback?.PlayFeedbacks();
            }
            else if (!danger && _warning)
            {
                _warning = false;
                oxygenWarningFeedback?.StopFeedbacks();
                oxygenRecoveredFeedback?.PlayFeedbacks();
            }
        }

        public float CurrentOxygenRate()
        {
            float baseRate = oxygenDefinition != null ? oxygenDefinition.passiveRatePerSecond : -1f;
            return OxygenRate.Calculate(baseRate, _oxygenModifiers);
        }

        public void RegisterOxygenModifier(IOxygenModifier modifier)
        {
            if (modifier != null && !_oxygenModifiers.Contains(modifier))
                _oxygenModifiers.Add(modifier);
        }

        public void UnregisterOxygenModifier(IOxygenModifier modifier)
            => _oxygenModifiers.Remove(modifier);
    }
}
