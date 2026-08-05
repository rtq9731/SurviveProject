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
    public class LanternController : MonoBehaviour, ILitZoneSource
    {
        [SerializeField] Light lampLight;
        [SerializeField] PlayerInventory inventory;

        [Header("배터리")]
        [SerializeField] float maxBattery = 100f;
        [SerializeField] float drainPerSecond = 1.6f;

        [Tooltip("배터리 셀 1개로 채워지는 양. 화톳불 추출 레시피의 스크랩 수와 짝이다")]
        [SerializeField] float batteryPerCell = 100f;

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

        // ── ILitZoneSource ───────────────────────────────────────
        // 화톳불(Survive.Building.Campfire)이 이미 같은 방식으로 자신을 내놓는다.
        // 랜턴도 광원이므로 같은 창구로 조회되어야 한다 — 그래야 "여기가 밝은가"를
        // 묻는 쪽이 화톳불인지 랜턴인지 알 필요가 없다. 지금 이것을 묻는 것은
        // 빛을 꺼리는 소비자(Survive.Creatures.CreatureBrain)다.
        //
        // 반경은 fullRange를 그대로 쓴다. 배터리 경고 때 intensity가 트윈으로
        // 흔들리지만 여기에는 반영하지 않는다 — 판정 반경까지 깜빡임에 맞춰
        // 요동치면 포식자가 깜빡임 주기로 붙었다 떨어졌다 하고, 플레이어는
        // 무엇이 자신을 지켜 주는지 읽을 수 없게 된다. 켜졌는가/꺼졌는가만 본다.
        //
        // 중심은 램프의 자리다. 실제 Light 컴포넌트는 프리팹에서 Spot이지만
        // (Player.prefab의 lampLight, m_Type 0), 판정은 방향을 보지 않는 구(球)다 —
        // 이 게임의 랜턴은 전방위 빛 웅덩이로 다루기로 확정됐다.
        public Vector3 LitZoneCenter => lampLight != null ? lampLight.transform.position : transform.position;
        public float LitZoneRadius => fullRange;
        bool ILitZoneSource.IsLit => _on;

        void Awake()
        {
            _battery = maxBattery;
            if (inventory == null) inventory = GetComponentInParent<PlayerInventory>();
            if (lampLight == null) lampLight = GetComponentInChildren<Light>(true);
            SetOn(false);
        }

        void OnEnable()
        {
            GameServices.Register(this);
            LitZoneRegistry.Register(this);
        }

        void OnDisable()
        {
            GameServices.Unregister<LanternController>();
            LitZoneRegistry.Unregister(this);
        }

        void OnDestroy() => _flicker?.Kill();

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
                // 여분 셀을 챙겨 왔다면 여기서 갈아 끼운다. 어둠 속에서 창을 열어
                // 버튼을 찾게 하는 대신, "몇 개를 들고 갈 것인가"를 출발 전의 결정으로
                // 만든다 — 배터리가 시계라는 규칙은 그대로다.
                if (TryInsertBatteryCell()) return;

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

        /// <summary>
        /// 배터리 셀을 하나 끼운다.
        ///
        /// 예전에는 스크랩을 그 자리에서 배터리로 바꿔 넣었다(1개당 20). 스크랩은
        /// 에너지를 <b>담고 있는</b> 매체이지 태우는 물건이 아니고, 담긴 것을 꺼내려면
        /// 열이 든다 — 그래서 이제는 화톳불 앞에서 시간을 들여 셀로 옮긴다.
        /// 현장에서 즉시 해결되던 것이 거점으로 돌아갈 이유가 되었다.
        /// 셀 하나가 채우는 양(<see cref="batteryPerCell"/>)은 그 레시피가 먹는
        /// 스크랩 수 × 20으로 맞춰 두었으므로 총량은 그대로다.
        /// </summary>
        public bool TryInsertBatteryCell(int cells = 1)
        {
            if (inventory?.Inventory == null || cells <= 0) return false;
            if (_battery >= maxBattery) return false;
            if (!inventory.Inventory.TryRemove(BatteryCellId, cells)) return false;

            Recharge(batteryPerCell * cells);
            rechargeFeedback?.PlayFeedbacks();
            return true;
        }

        /// <summary>화톳불에서 뽑아내는 셀의 아이템 id.</summary>
        public const string BatteryCellId = "battery_cell";
    }
}
