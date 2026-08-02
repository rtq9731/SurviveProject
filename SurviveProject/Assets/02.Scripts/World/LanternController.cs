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
        [SerializeField] float fullIntensity = 2.2f;
        [SerializeField] float fullRange = 14f;

        [Tooltip("배터리가 이 비율 아래로 떨어지면 깜빡인다")]
        [Range(0f, 1f)] [SerializeField] float flickerThreshold = 0.2f;

        [Header("피드백")]
        [Tooltip("배터리 부족 경고 시 재생")]
        [SerializeField] MMF_Player lowBatteryFeedback;

        [Tooltip("스크랩으로 충전할 때 재생")]
        [SerializeField] MMF_Player rechargeFeedback;

        float _배터리;
        bool _켜짐;
        bool _경고중;
        Tween _flicker;

        public float Battery => _배터리;
        public float BatteryNormalized => maxBattery <= 0f ? 0f : _배터리 / maxBattery;
        public bool IsOn => _켜짐;

        public event Action<float, float> BatteryChanged;   // (현재, 최대)

        void Awake()
        {
            _배터리 = maxBattery;
            if (inventory == null) inventory = GetComponentInParent<PlayerInventory>();
            if (lampLight == null) lampLight = GetComponentInChildren<Light>(true);
            SetOn(false);
        }

        void OnEnable() => GameServices.Register(this);
        void OnDisable() => GameServices.Unregister<LanternController>();

        public void SetOn(bool on)
        {
            _켜짐 = on && _배터리 > 0f;
            if (lampLight != null) lampLight.enabled = _켜짐;
            빛갱신();
        }

        public void Toggle() => SetOn(!_켜짐);

        void Update()
        {
            if (!_켜짐) return;

            _배터리 = Mathf.Max(0f, _배터리 - drainPerSecond * Time.deltaTime);
            BatteryChanged?.Invoke(_배터리, maxBattery);

            if (_배터리 <= 0f)
            {
                SetOn(false);
                return;
            }

            경고갱신();
            빛갱신();
        }

        void 경고갱신()
        {
            bool 위험 = BatteryNormalized <= flickerThreshold;

            if (위험 && !_경고중)
            {
                _경고중 = true;
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
            else if (!위험 && _경고중)
            {
                _경고중 = false;
                lowBatteryFeedback?.StopFeedbacks();
                _flicker?.Kill();
                _flicker = null;
            }
        }

        void 빛갱신()
        {
            if (lampLight == null || _경고중) return;   // 깜빡이는 동안은 트윈에 맡긴다
            lampLight.intensity = fullIntensity;
            lampLight.range = fullRange;
        }

        /// <summary>발광 버섯 군락 등에서 무료로 채운다.</summary>
        public void Recharge(float amount)
        {
            if (amount <= 0f) return;
            float 이전 = _배터리;
            _배터리 = Mathf.Min(maxBattery, _배터리 + amount);
            if (!Mathf.Approximately(이전, _배터리)) BatteryChanged?.Invoke(_배터리, maxBattery);
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
