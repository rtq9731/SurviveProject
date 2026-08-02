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

        bool _죽음통보함;
        bool _경고중;

        void Awake()
        {
            Health = 만들기(healthDefinition, 100f);
            Oxygen = 만들기(oxygenDefinition, 100f);
        }

        static Vital 만들기(VitalDefinitionSO def, float 기본최대)
        {
            if (def == null) return new Vital(기본최대, 기본최대);
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

            산소경고갱신();

            if (Health.IsEmpty && !_죽음통보함)
            {
                _죽음통보함 = true;
                deathFeedback?.PlayFeedbacks();
                Died?.Invoke();
            }
        }

        void 산소경고갱신()
        {
            bool 위험 = Oxygen.Normalized <= oxygenWarningThreshold;

            if (위험 && !_경고중)
            {
                _경고중 = true;
                oxygenWarningFeedback?.PlayFeedbacks();
            }
            else if (!위험 && _경고중)
            {
                _경고중 = false;
                oxygenWarningFeedback?.StopFeedbacks();
                oxygenRecoveredFeedback?.PlayFeedbacks();
            }
        }

        public float CurrentOxygenRate()
        {
            float 기본 = oxygenDefinition != null ? oxygenDefinition.passiveRatePerSecond : -1f;
            return OxygenRate.Calculate(기본, _oxygenModifiers);
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
