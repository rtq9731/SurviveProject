using System;
using UnityEngine;
using DG.Tweening;
using MoreMountains.Feedbacks;
using Survive.Core;
using Survive.Items;

namespace Survive.World
{
    /// <summary>
    /// 랜턴. 지하는 빛이 거의 공급되지 않으므로(세계관) 이것이 챕터 1의 핵심 압박이다.
    ///
    /// 배터리가 닳으면 시야가 사라져 사실상 전진이 막힌다.
    /// 채우는 방법은 둘 — 발광 버섯 군락(무료, 거점)이나 스크랩 소모(현장, 대가).
    /// </summary>
    [DisallowMultipleComponent]
    public class LanternController : MonoBehaviour
    {
        [SerializeField] Light lampLight;
        [SerializeField] PlayerInventory inventory;

        [Header("배터리")]
        [SerializeField] float maxBattery = 100f;
        [SerializeField] float drainPerSecond = 1.6f;

        [Tooltip("스크랩 1개로 채워지는 배터리 양")]
        [SerializeField] float batteryPerScrap = 20f;

        [Header("빛")]
        [SerializeField] float fullIntensity = 5.5f;
        [SerializeField] float fullRange = 26f;

        [Tooltip("배터리가 이 비율 아래로 떨어지면 깜빡인다")]
        [Range(0f, 1f)] [SerializeField] float flickerThreshold = 0.2f;

        [Header("피드백")]
        [Tooltip("배터리 부족 경고 시 재생")]
        [SerializeField] MMF_Player lowBatteryFeedback;

        [Tooltip("스크랩으로 충전할 때 재생")]
        [SerializeField] MMF_Player rechargeFeedback;

        float _battery;
        bool _on;
        bool _warning;
        Tween _flicker;

        public float Battery => _battery;
        public float BatteryNormalized => maxBattery <= 0f ? 0f : _battery / maxBattery;
        public bool IsOn => _on;

        public event Action<float, float> BatteryChanged;   // (현재, 최대)

        void Awake()
        {
            _battery = maxBattery;
            if (inventory == null) inventory = GetComponentInParent<PlayerInventory>();
            if (lampLight == null) lampLight = GetComponentInChildren<Light>(true);
            SetOn(false);
        }

        void OnEnable() => GameServices.Register(this);
        void OnDisable() => GameServices.Unregister<LanternController>();

        public void SetOn(bool on)
        {
            _on = on && _battery > 0f;
            if (lampLight != null) lampLight.enabled = _on;
            RefreshLight();
        }

        public void Toggle() => SetOn(!_on);

        void Update()
        {
            if (!_on) return;

            _battery = Mathf.Max(0f, _battery - drainPerSecond * Time.deltaTime);
            BatteryChanged?.Invoke(_battery, maxBattery);

            if (_battery <= 0f)
            {
                SetOn(false);
                return;
            }

            RefreshWarning();
            RefreshLight();
        }

        void RefreshWarning()
        {
            bool danger = BatteryNormalized <= flickerThreshold;

            if (danger && !_warning)
            {
                _warning = true;
                lowBatteryFeedback?.PlayFeedbacks();

                // 꺼지기 직전의 깜빡임. 남은 배터리를 눈으로 알 수 있게 한다.
                if (lampLight != null)
                {
                    _flicker?.Kill();
                    _flicker = lampLight.DOIntensity(fullIntensity * 0.35f, 0.18f)
                                        .SetLoops(-1, LoopType.Yoyo)
                                        .SetEase(Ease.InOutQuad);
                }
            }
            else if (!danger && _warning)
            {
                _warning = false;
                lowBatteryFeedback?.StopFeedbacks();
                _flicker?.Kill();
                _flicker = null;
            }
        }

        void RefreshLight()
        {
            if (lampLight == null || _warning) return;   // 깜빡이는 동안은 트윈에 맡긴다
            lampLight.intensity = fullIntensity;
            lampLight.range = fullRange;
        }

        /// <summary>발광 버섯 군락 등에서 무료로 채운다.</summary>
        public void Recharge(float amount)
        {
            if (amount <= 0f) return;
            float prev = _battery;
            _battery = Mathf.Min(maxBattery, _battery + amount);
            if (!Mathf.Approximately(prev, _battery)) BatteryChanged?.Invoke(_battery, maxBattery);
        }

        /// <summary>스크랩을 태워 현장에서 충전한다.</summary>
        public bool RechargeWithScrap(int scrapCount = 1)
        {
            if (inventory?.Inventory == null || scrapCount <= 0) return false;
            if (!inventory.Inventory.TryRemove(PlayerInventory.ScrapId, scrapCount)) return false;

            Recharge(batteryPerScrap * scrapCount);
            rechargeFeedback?.PlayFeedbacks();
            return true;
        }
    }
}
